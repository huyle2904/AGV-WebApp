using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NewAGV.Api.Data;
using NewAGV.Api.Hubs;
using NewAGV.Contracts;

namespace NewAGV.Api.Services;

public sealed class WorkflowExecutionService(
    NewAgvDbContext dbContext,
    WorkflowDefinitionService definitionService,
    WorkflowValidationService validationService,
    TaskChainCoordinator taskChainCoordinator,
    AgvPlantStore plantStore,
    IHubContext<TelemetryHub> hubContext)
{
    private static readonly string[] ActiveRunStatuses =
    [
        WorkflowExecutionStatus.Pending.ToString(),
        WorkflowExecutionStatus.Validating.ToString(),
        WorkflowExecutionStatus.Ready.ToString(),
        WorkflowExecutionStatus.Starting.ToString(),
        WorkflowExecutionStatus.Running.ToString(),
        WorkflowExecutionStatus.Paused.ToString()
    ];

    public async Task<WorkflowRunDto> ExecuteAsync(Guid workflowId, ExecuteWorkflowRequest request, UserRole requestedByRole, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RobotId))
        {
            throw new InvalidOperationException("RobotId is required.");
        }

        var workflow = await definitionService.GetWorkflowAsync(workflowId, cancellationToken)
            ?? throw new InvalidOperationException("Workflow definition was not found.");

        if (!string.IsNullOrWhiteSpace(workflow.AssignedRobotId) && !string.Equals(workflow.AssignedRobotId, request.RobotId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Workflow is assigned to robot '{workflow.AssignedRobotId}'.");
        }

        if (workflow.RequiresConfirmation && !request.Confirmed)
        {
            throw new InvalidOperationException("Workflow execution requires explicit confirmation.");
        }

        var validation = await validationService.ValidateWorkflowAsync(workflow, cancellationToken);
        if (!validation.CanExecute)
        {
            throw new InvalidOperationException("Workflow validation failed. Resolve validation issues before execution.");
        }

        if (plantStore.GetRobot(request.RobotId) is null)
        {
            throw new InvalidOperationException($"Robot '{request.RobotId}' was not found.");
        }

        var activeWorkflowExists = await dbContext.WorkflowRuns.AnyAsync(
            item => item.RobotId == request.RobotId && ActiveRunStatuses.Contains(item.Status),
            cancellationToken);
        if (activeWorkflowExists)
        {
            throw new InvalidOperationException("Another workflow is already active on this robot.");
        }

        var activeTaskChain = taskChainCoordinator.GetActiveRun(request.RobotId);
        if (activeTaskChain is not null && !IsTerminal(activeTaskChain.Run.Status))
        {
            throw new InvalidOperationException("Another TaskChain is already active on this robot.");
        }

        var definition = await dbContext.WorkflowDefinitions
            .Include(item => item.Steps.OrderBy(step => step.Sequence))
            .FirstAsync(item => item.Id == workflowId, cancellationToken);

        var run = new WorkflowRunEntity
        {
            WorkflowDefinitionId = definition.Id,
            RobotId = request.RobotId.Trim(),
            Status = WorkflowExecutionStatus.Pending.ToString(),
            TriggeredBy = NormalizeOptional(request.TriggeredBy),
            StartedAt = DateTimeOffset.UtcNow,
            ValidationSnapshotJson = JsonSerializer.Serialize(validation),
            Steps = definition.Steps
                .OrderBy(step => step.Sequence)
                .Select(step => new WorkflowRunStepEntity
                {
                    Sequence = step.Sequence,
                    StepType = step.StepType,
                    TaskChainName = step.TaskChainName,
                    DisplayName = step.DisplayName,
                    TimeoutSeconds = step.TimeoutSeconds,
                    RetryCount = step.RetryCount,
                    FailurePolicy = step.FailurePolicy,
                    Note = step.Note,
                    Status = WorkflowStepExecutionStatus.Pending.ToString()
                })
                .ToList()
        };

        dbContext.WorkflowRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);
        await EmitAsync("workflow.started", run, cancellationToken);
        await StartNextPendingStepAsync(run.Id, requestedByRole, cancellationToken);
        return (await GetRunAsync(run.Id, cancellationToken))!;
    }

    public async Task<WorkflowRunDto?> GetRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await dbContext.WorkflowRuns
            .AsNoTracking()
            .Include(item => item.WorkflowDefinition)
            .Include(item => item.Steps.OrderBy(step => step.Sequence))
            .FirstOrDefaultAsync(item => item.Id == runId, cancellationToken);
        return run is null ? null : MapRun(run);
    }

    public async Task<WorkflowRunDto?> GetActiveRunAsync(string? robotId, CancellationToken cancellationToken)
    {
        var query = dbContext.WorkflowRuns
            .AsNoTracking()
            .Include(item => item.WorkflowDefinition)
            .Include(item => item.Steps.OrderBy(step => step.Sequence))
            .Where(item => ActiveRunStatuses.Contains(item.Status));

        if (!string.IsNullOrWhiteSpace(robotId))
        {
            query = query.Where(item => item.RobotId == robotId);
        }

        var run = await query.OrderByDescending(item => item.StartedAt).FirstOrDefaultAsync(cancellationToken);
        return run is null ? null : MapRun(run);
    }

    public async Task<IReadOnlyList<WorkflowHistoryEntryDto>> GetHistoryAsync(string? robotId, CancellationToken cancellationToken)
    {
        var query = dbContext.WorkflowRuns
            .AsNoTracking()
            .Include(item => item.WorkflowDefinition)
            .Include(item => item.Steps)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(robotId))
        {
            query = query.Where(item => item.RobotId == robotId);
        }

        var runs = await query.OrderByDescending(item => item.StartedAt).Take(50).ToListAsync(cancellationToken);
        return runs.Select(run => new WorkflowHistoryEntryDto
        {
            RunId = run.Id,
            WorkflowDefinitionId = run.WorkflowDefinitionId,
            WorkflowName = run.WorkflowDefinition.Name,
            RobotId = run.RobotId,
            StepSequence = run.CurrentStepSequence,
            StepName = run.Steps.FirstOrDefault(step => step.Sequence == run.CurrentStepSequence)?.DisplayName
                ?? run.Steps.FirstOrDefault(step => step.Sequence == run.CurrentStepSequence)?.TaskChainName,
            Status = ParseWorkflowStatus(run.Status),
            Message = run.ErrorMessage ?? run.Steps.OrderByDescending(step => step.Sequence).FirstOrDefault(step => !string.IsNullOrWhiteSpace(step.Message))?.Message,
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt
        }).ToList();
    }

    public async Task<WorkflowRunDto> PauseAsync(WorkflowControlRequest request, UserRole requestedByRole, CancellationToken cancellationToken)
    {
        var run = await RequireActiveRunAsync(request.RobotId, cancellationToken);
        var result = await taskChainCoordinator.PauseAsync(new TaskChainControlRequest { RobotId = run.RobotId }, requestedByRole, cancellationToken);
        if (result.Status == MissionCommandStatus.Rejected)
        {
            throw new InvalidOperationException(result.Message);
        }

        await UpdateControlStateAsync(run.Id, WorkflowExecutionStatus.Paused, WorkflowStepExecutionStatus.Paused, result.Message, cancellationToken);
        return (await GetRunAsync(run.Id, cancellationToken))!;
    }

    public async Task<WorkflowRunDto> ResumeAsync(WorkflowControlRequest request, UserRole requestedByRole, CancellationToken cancellationToken)
    {
        var run = await RequireActiveRunAsync(request.RobotId, cancellationToken);
        var result = await taskChainCoordinator.ResumeAsync(new TaskChainControlRequest { RobotId = run.RobotId }, requestedByRole, cancellationToken);
        if (result.Status == MissionCommandStatus.Rejected)
        {
            throw new InvalidOperationException(result.Message);
        }

        await UpdateControlStateAsync(run.Id, WorkflowExecutionStatus.Running, WorkflowStepExecutionStatus.Running, result.Message, cancellationToken);
        return (await GetRunAsync(run.Id, cancellationToken))!;
    }

    public async Task<WorkflowRunDto> CancelAsync(WorkflowControlRequest request, UserRole requestedByRole, CancellationToken cancellationToken)
    {
        var run = await RequireActiveRunAsync(request.RobotId, cancellationToken);
        var result = await taskChainCoordinator.CancelAsync(new TaskChainControlRequest { RobotId = run.RobotId }, requestedByRole, cancellationToken);
        if (result.Status == MissionCommandStatus.Rejected)
        {
            throw new InvalidOperationException(result.Message);
        }

        await CancelRunAsync(run.Id, result.Message, cancellationToken);
        return (await GetRunAsync(run.Id, cancellationToken))!;
    }

    public async Task MonitorActiveRunsAsync(CancellationToken cancellationToken)
    {
        var activeRuns = await dbContext.WorkflowRuns
            .Include(item => item.WorkflowDefinition)
            .Include(item => item.Steps.OrderBy(step => step.Sequence))
            .Where(item => ActiveRunStatuses.Contains(item.Status))
            .OrderBy(item => item.StartedAt)
            .ToListAsync(cancellationToken);

        foreach (var run in activeRuns)
        {
            await MonitorRunAsync(run, cancellationToken);
        }
    }

    private async Task MonitorRunAsync(WorkflowRunEntity run, CancellationToken cancellationToken)
    {
        var step = run.Steps.FirstOrDefault(item => item.Sequence == run.CurrentStepSequence)
            ?? run.Steps.OrderBy(item => item.Sequence).FirstOrDefault(item => item.Status is "Starting" or "Waiting" or "Running" or "Paused");

        if (step is null)
        {
            await StartNextPendingStepAsync(run.Id, UserRole.Operator, cancellationToken);
            return;
        }

        if (step.TimeoutSeconds > 0 && step.StartedAt is not null && DateTimeOffset.UtcNow - step.StartedAt > TimeSpan.FromSeconds(step.TimeoutSeconds))
        {
            await HandleStepFailureAsync(run, step, WorkflowStepExecutionStatus.TimedOut, $"Step timed out after {step.TimeoutSeconds} seconds.", cancellationToken);
            return;
        }

        var taskChainRun = taskChainCoordinator.GetActiveRun(run.RobotId);
        if (taskChainRun is null)
        {
            taskChainRun = taskChainCoordinator.GetRecentRuns(run.RobotId)
                .OrderByDescending(item => item.LastUpdated)
                .FirstOrDefault(item => IsMatchingTaskChainRun(step, item));
            if (taskChainRun is null)
            {
                return;
            }
        }

        if (!IsMatchingTaskChainRun(step, taskChainRun))
        {
            return;
        }

        step.ProgressPercent = taskChainRun.RuntimeStatus?.Percentage;
        step.Info = taskChainRun.RuntimeStatus?.Info;
        step.Message = taskChainRun.Run.Message;
        step.SeerTaskId ??= taskChainRun.Run.TaskId;

        var nextStatus = MapStepStatus(taskChainRun.Run.Status);
        if (nextStatus is WorkflowStepExecutionStatus.Running or WorkflowStepExecutionStatus.Waiting or WorkflowStepExecutionStatus.Paused)
        {
            step.Status = nextStatus.ToString();
            run.Status = nextStatus == WorkflowStepExecutionStatus.Paused ? WorkflowExecutionStatus.Paused.ToString() : WorkflowExecutionStatus.Running.ToString();
            await dbContext.SaveChangesAsync(cancellationToken);
            await EmitAsync("workflow.updated", run, cancellationToken);
            return;
        }

        if (nextStatus == WorkflowStepExecutionStatus.Completed)
        {
            step.Status = nextStatus.ToString();
            step.CompletedAt = DateTimeOffset.UtcNow;
            run.Status = WorkflowExecutionStatus.Running.ToString();
            await dbContext.SaveChangesAsync(cancellationToken);
            await EmitAsync("workflow.step.completed", run, cancellationToken);
            await StartNextPendingStepAsync(run.Id, UserRole.Operator, cancellationToken);
            return;
        }

        if (nextStatus == WorkflowStepExecutionStatus.Canceled)
        {
            await CancelRunAsync(run.Id, taskChainRun.Run.Message, cancellationToken);
            return;
        }

        await HandleStepFailureAsync(run, step, nextStatus, taskChainRun.Run.Message, cancellationToken);
    }

    private async Task StartNextPendingStepAsync(Guid runId, UserRole requestedByRole, CancellationToken cancellationToken)
    {
        var run = await dbContext.WorkflowRuns
            .Include(item => item.WorkflowDefinition)
            .Include(item => item.Steps.OrderBy(step => step.Sequence))
            .FirstAsync(item => item.Id == runId, cancellationToken);

        var nextStep = run.Steps.OrderBy(item => item.Sequence).FirstOrDefault(item => item.Status == WorkflowStepExecutionStatus.Pending.ToString());
        if (nextStep is null)
        {
            run.Status = WorkflowExecutionStatus.Completed.ToString();
            run.CompletedAt = DateTimeOffset.UtcNow;
            run.CurrentStepSequence = null;
            await dbContext.SaveChangesAsync(cancellationToken);
            await EmitAsync("workflow.completed", run, cancellationToken);
            return;
        }

        var attempt = GetAttemptCount(nextStep) + 1;
        nextStep.Info = $"attempt={attempt}";
        nextStep.StartedAt ??= DateTimeOffset.UtcNow;
        nextStep.Status = WorkflowStepExecutionStatus.Starting.ToString();
        nextStep.Message = $"Starting TaskChain '{nextStep.TaskChainName}' (attempt {attempt}).";
        run.Status = WorkflowExecutionStatus.Starting.ToString();
        run.CurrentStepSequence = nextStep.Sequence;
        await dbContext.SaveChangesAsync(cancellationToken);
        await EmitAsync("workflow.step.started", run, cancellationToken);

        var result = await taskChainCoordinator.ExecuteAsync(new TaskChainRunRequest
        {
            RobotId = run.RobotId,
            TaskChainName = nextStep.TaskChainName,
            Confirmed = true
        }, requestedByRole, cancellationToken);

        if (result.Status == TaskChainRunStatus.Rejected)
        {
            await HandleStepFailureAsync(run, nextStep, WorkflowStepExecutionStatus.Failed, result.Message, cancellationToken);
            return;
        }

        nextStep.SeerTaskId = result.TaskId;
        nextStep.Status = MapStepStatus(result.Status).ToString();
        nextStep.Message = result.Message;
        run.Status = WorkflowExecutionStatus.Running.ToString();
        await dbContext.SaveChangesAsync(cancellationToken);
        await EmitAsync("workflow.updated", run, cancellationToken);
    }

    private async Task HandleStepFailureAsync(WorkflowRunEntity run, WorkflowRunStepEntity step, WorkflowStepExecutionStatus status, string? message, CancellationToken cancellationToken)
    {
        step.Status = status.ToString();
        step.CompletedAt = DateTimeOffset.UtcNow;
        step.Message = message;

        var attempts = GetAttemptCount(step);
        var policy = ParseFailurePolicy(step.FailurePolicy);
        var shouldRetry = policy == WorkflowFailurePolicy.RetryStep && attempts <= step.RetryCount;
        if (shouldRetry)
        {
            step.Status = WorkflowStepExecutionStatus.Pending.ToString();
            step.CompletedAt = null;
            step.Message = $"Retry scheduled after failure: {message}";
            run.Status = WorkflowExecutionStatus.Running.ToString();
            await dbContext.SaveChangesAsync(cancellationToken);
            await EmitAsync("workflow.updated", run, cancellationToken);
            await StartNextPendingStepAsync(run.Id, UserRole.Operator, cancellationToken);
            return;
        }

        if (policy == WorkflowFailurePolicy.ContinueWorkflow)
        {
            run.Status = WorkflowExecutionStatus.Running.ToString();
            await dbContext.SaveChangesAsync(cancellationToken);
            await EmitAsync("workflow.updated", run, cancellationToken);
            await StartNextPendingStepAsync(run.Id, UserRole.Operator, cancellationToken);
            return;
        }

        if (policy == WorkflowFailurePolicy.RequireManualResume)
        {
            step.Status = WorkflowStepExecutionStatus.Paused.ToString();
            run.Status = WorkflowExecutionStatus.Paused.ToString();
            run.ErrorMessage = message;
            await dbContext.SaveChangesAsync(cancellationToken);
            await EmitAsync("workflow.updated", run, cancellationToken);
            return;
        }

        run.Status = WorkflowExecutionStatus.Failed.ToString();
        run.CompletedAt = DateTimeOffset.UtcNow;
        run.ErrorMessage = message;
        await dbContext.SaveChangesAsync(cancellationToken);
        await EmitAsync("workflow.failed", run, cancellationToken);
    }

    private async Task<WorkflowRunEntity> RequireActiveRunAsync(string robotId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(robotId))
        {
            throw new InvalidOperationException("RobotId is required.");
        }

        return await dbContext.WorkflowRuns
            .Include(item => item.WorkflowDefinition)
            .Include(item => item.Steps.OrderBy(step => step.Sequence))
            .FirstOrDefaultAsync(item => item.RobotId == robotId && ActiveRunStatuses.Contains(item.Status), cancellationToken)
            ?? throw new InvalidOperationException("No active workflow run was found.");
    }

    private async Task UpdateControlStateAsync(Guid runId, WorkflowExecutionStatus workflowStatus, WorkflowStepExecutionStatus stepStatus, string? message, CancellationToken cancellationToken)
    {
        var run = await dbContext.WorkflowRuns
            .Include(item => item.WorkflowDefinition)
            .Include(item => item.Steps.OrderBy(step => step.Sequence))
            .FirstAsync(item => item.Id == runId, cancellationToken);
        run.Status = workflowStatus.ToString();
        var step = run.Steps.FirstOrDefault(item => item.Sequence == run.CurrentStepSequence);
        if (step is not null)
        {
            step.Status = stepStatus.ToString();
            step.Message = message;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await EmitAsync("workflow.updated", run, cancellationToken);
    }

    private async Task CancelRunAsync(Guid runId, string? message, CancellationToken cancellationToken)
    {
        var run = await dbContext.WorkflowRuns
            .Include(item => item.WorkflowDefinition)
            .Include(item => item.Steps.OrderBy(step => step.Sequence))
            .FirstAsync(item => item.Id == runId, cancellationToken);
        run.Status = WorkflowExecutionStatus.Canceled.ToString();
        run.CompletedAt = DateTimeOffset.UtcNow;
        run.ErrorMessage = message;
        run.CanceledBy = run.TriggeredBy;
        var step = run.Steps.FirstOrDefault(item => item.Sequence == run.CurrentStepSequence);
        if (step is not null)
        {
            step.Status = WorkflowStepExecutionStatus.Canceled.ToString();
            step.CompletedAt = DateTimeOffset.UtcNow;
            step.Message = message;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await EmitAsync("workflow.canceled", run, cancellationToken);
    }

    private async Task EmitAsync(string eventType, WorkflowRunEntity run, CancellationToken cancellationToken)
    {
        await hubContext.Clients.All.SendAsync(
            "ReceiveTelemetry",
            new RealtimeEvent(eventType, DateTimeOffset.UtcNow, WorkflowRun: MapRun(run), Message: run.ErrorMessage),
            cancellationToken);
    }

    private static WorkflowRunDto MapRun(WorkflowRunEntity run)
        => new()
        {
            Id = run.Id,
            WorkflowDefinitionId = run.WorkflowDefinitionId,
            WorkflowName = run.WorkflowDefinition.Name,
            WorkflowVersion = run.WorkflowDefinition.Version,
            RobotId = run.RobotId,
            Status = ParseWorkflowStatus(run.Status),
            TriggeredBy = run.TriggeredBy,
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt,
            CurrentStepSequence = run.CurrentStepSequence,
            CanceledBy = run.CanceledBy,
            ErrorMessage = run.ErrorMessage,
            ValidationSnapshotJson = run.ValidationSnapshotJson,
            Steps = run.Steps.OrderBy(step => step.Sequence).Select(step => new WorkflowRunStepDto
            {
                Id = step.Id,
                Sequence = step.Sequence,
                StepType = step.StepType,
                TaskChainName = step.TaskChainName,
                DisplayName = step.DisplayName,
                TimeoutSeconds = step.TimeoutSeconds,
                RetryCount = step.RetryCount,
                FailurePolicy = ParseFailurePolicy(step.FailurePolicy),
                Note = step.Note,
                Status = ParseStepStatus(step.Status),
                SeerTaskId = step.SeerTaskId,
                StartedAt = step.StartedAt,
                CompletedAt = step.CompletedAt,
                ProgressPercent = step.ProgressPercent,
                Info = step.Info,
                Message = step.Message
            }).ToList()
        };

    private static WorkflowExecutionStatus ParseWorkflowStatus(string? value)
        => Enum.TryParse<WorkflowExecutionStatus>(value, true, out var status) ? status : WorkflowExecutionStatus.Pending;

    private static WorkflowStepExecutionStatus ParseStepStatus(string? value)
        => Enum.TryParse<WorkflowStepExecutionStatus>(value, true, out var status) ? status : WorkflowStepExecutionStatus.Pending;

    private static WorkflowFailurePolicy ParseFailurePolicy(string? value)
        => Enum.TryParse<WorkflowFailurePolicy>(value, true, out var policy) ? policy : WorkflowFailurePolicy.StopWorkflow;

    private static WorkflowStepExecutionStatus MapStepStatus(TaskChainRunStatus status)
        => status switch
        {
            TaskChainRunStatus.Waiting => WorkflowStepExecutionStatus.Waiting,
            TaskChainRunStatus.Running => WorkflowStepExecutionStatus.Running,
            TaskChainRunStatus.Suspended => WorkflowStepExecutionStatus.Paused,
            TaskChainRunStatus.Completed => WorkflowStepExecutionStatus.Completed,
            TaskChainRunStatus.Canceled => WorkflowStepExecutionStatus.Canceled,
            TaskChainRunStatus.OverTime => WorkflowStepExecutionStatus.TimedOut,
            TaskChainRunStatus.Failed or TaskChainRunStatus.Rejected or TaskChainRunStatus.UnknownTaskId => WorkflowStepExecutionStatus.Failed,
            _ => WorkflowStepExecutionStatus.Starting
        };

    private static bool IsTerminal(TaskChainRunStatus status)
        => status is TaskChainRunStatus.Completed or TaskChainRunStatus.Failed or TaskChainRunStatus.Canceled or TaskChainRunStatus.OverTime or TaskChainRunStatus.Rejected;

    private static int GetAttemptCount(WorkflowRunStepEntity step)
    {
        if (string.IsNullOrWhiteSpace(step.Info))
        {
            return 0;
        }

        var parts = step.Info.Split('=', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 && int.TryParse(parts[1], out var attempts) ? attempts : 0;
    }

    private static bool IsMatchingTaskChainRun(WorkflowRunStepEntity step, TaskChainRunSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(step.SeerTaskId) &&
            !string.IsNullOrWhiteSpace(snapshot.Run.TaskId))
        {
            return string.Equals(step.SeerTaskId, snapshot.Run.TaskId, StringComparison.OrdinalIgnoreCase);
        }

        if (!string.Equals(step.TaskChainName, snapshot.Run.TaskChainName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (step.StartedAt is null)
        {
            return true;
        }

        return snapshot.Run.StartedAt >= step.StartedAt.Value.AddSeconds(-5);
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
