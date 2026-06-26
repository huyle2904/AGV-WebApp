using Microsoft.EntityFrameworkCore;
using NewAGV.Contracts;
using NewAGV.Persistence;
using Npgsql;

namespace NewAGV.Worker.Services;

public sealed class WorkerWorkflowRuntimeService(
    NewAgvDbContext dbContext,
    SeerTaskChainService taskChainService,
    ApiSyncClient apiSyncClient,
    ILogger<WorkerWorkflowRuntimeService> logger)
{
    private const string NotImplementedMessage = "Workflow runtime not yet implemented in Worker.";
    private static readonly string[] ActiveRunStatuses =
    [
        WorkflowExecutionStatus.Pending.ToString(),
        WorkflowExecutionStatus.Validating.ToString(),
        WorkflowExecutionStatus.Ready.ToString(),
        WorkflowExecutionStatus.Starting.ToString(),
        WorkflowExecutionStatus.Running.ToString(),
        WorkflowExecutionStatus.Paused.ToString()
    ];

    public async Task<WorkerWorkflowRuntimeResult> StartAsync(
        Guid workflowId,
        WorkerWorkflowStartRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        logger.LogInformation(
            "Worker workflow start skeleton received for workflow {WorkflowId} and robot {RobotId}.",
            workflowId,
            request.RobotId);

        if (workflowId == Guid.Empty)
        {
            return CreateValidationFailedResult("WorkflowDefinitionId is required.");
        }

        if (request.WorkflowDefinitionId != Guid.Empty && request.WorkflowDefinitionId != workflowId)
        {
            return CreateValidationFailedResult("WorkflowDefinitionId route and payload must match.");
        }

        if (string.IsNullOrWhiteSpace(request.RobotId))
        {
            return CreateValidationFailedResult("RobotId is required.");
        }

        var robotId = request.RobotId.Trim();
        var definition = await dbContext.WorkflowDefinitions
            .Include(item => item.Steps.OrderBy(step => step.Sequence))
            .FirstOrDefaultAsync(item => item.Id == workflowId, cancellationToken);

        if (definition is null)
        {
            return new WorkerWorkflowRuntimeResult
            {
                Outcome = WorkerWorkflowRuntimeOutcome.NotFound,
                Message = "Workflow definition was not found."
            };
        }

        if (!string.IsNullOrWhiteSpace(definition.AssignedRobotId) &&
            !string.Equals(definition.AssignedRobotId, robotId, StringComparison.OrdinalIgnoreCase))
        {
            return CreateValidationFailedResult($"Workflow is assigned to robot '{definition.AssignedRobotId}'.");
        }

        if (definition.Steps.Count == 0)
        {
            return CreateValidationFailedResult("Workflow must contain at least one step.");
        }

        var activeRunExists = await dbContext.WorkflowRuns.AnyAsync(
            item => item.RobotId == robotId && ActiveRunStatuses.Contains(item.Status),
            cancellationToken);
        if (activeRunExists)
        {
            return CreateAlreadyActiveResult();
        }

        var run = new WorkflowRunEntity
        {
            WorkflowDefinitionId = definition.Id,
            WorkflowDefinition = definition,
            RobotId = robotId,
            Status = WorkflowExecutionStatus.Pending.ToString(),
            TriggeredBy = NormalizeOptional(request.TriggeredBy),
            StartedAt = DateTimeOffset.UtcNow,
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
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsActiveRunConstraintViolation(exception))
        {
            logger.LogInformation(
                exception,
                "Worker workflow start rejected by active-run unique guard for robot {RobotId}.",
                robotId);
            return CreateAlreadyActiveResult();
        }

        await EmitAsync("workflow.started", run, cancellationToken);

        logger.LogInformation(
            "Worker workflow start accepted workflow {WorkflowId} for robot {RobotId} with run {RunId}.",
            workflowId,
            robotId,
            run.Id);

        await StartFirstPendingStepAsync(run, cancellationToken);
        return new WorkerWorkflowRuntimeResult
        {
            Outcome = WorkerWorkflowRuntimeOutcome.Accepted,
            Message = "Workflow runtime accepted start.",
            RunId = run.Id
        };
    }

    public async Task<WorkerWorkflowRuntimeResult> PauseAsync(
        WorkerWorkflowControlRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        logger.LogInformation("Worker workflow pause received for robot {RobotId}.", request.RobotId);
        var validation = ValidateControlRequest(request);
        if (validation is not null)
        {
            return validation;
        }

        var run = await GetActiveRunForControlAsync(request.RobotId, cancellationToken);
        if (run is null)
        {
            return CreateNotFoundResult("No active workflow run was found.");
        }

        var result = await taskChainService.PauseAsync(cancellationToken);
        if (result.Status == MissionCommandStatus.Rejected)
        {
            return CreateRejectedResult(result.Message, run.Id);
        }

        await UpdateControlStateAsync(
            run,
            WorkflowExecutionStatus.Paused,
            WorkflowStepExecutionStatus.Paused,
            result.Message,
            cancellationToken);

        return CreateAcceptedResult("Workflow pause accepted by Worker.", run.Id);
    }

    public async Task<WorkerWorkflowRuntimeResult> ResumeAsync(
        WorkerWorkflowControlRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        logger.LogInformation("Worker workflow resume received for robot {RobotId}.", request.RobotId);
        var validation = ValidateControlRequest(request);
        if (validation is not null)
        {
            return validation;
        }

        var run = await GetActiveRunForControlAsync(request.RobotId, cancellationToken);
        if (run is null)
        {
            return CreateNotFoundResult("No active workflow run was found.");
        }

        var result = await taskChainService.ResumeAsync(cancellationToken);
        if (result.Status == MissionCommandStatus.Rejected)
        {
            return CreateRejectedResult(result.Message, run.Id);
        }

        await UpdateControlStateAsync(
            run,
            WorkflowExecutionStatus.Running,
            WorkflowStepExecutionStatus.Running,
            result.Message,
            cancellationToken);

        return CreateAcceptedResult("Workflow resume accepted by Worker.", run.Id);
    }

    public async Task<WorkerWorkflowRuntimeResult> CancelAsync(
        WorkerWorkflowControlRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        logger.LogInformation("Worker workflow cancel received for robot {RobotId}.", request.RobotId);
        var validation = ValidateControlRequest(request);
        if (validation is not null)
        {
            return validation;
        }

        var run = await GetActiveRunForControlAsync(request.RobotId, cancellationToken);
        if (run is null)
        {
            return CreateNotFoundResult("No active workflow run was found.");
        }

        var result = await taskChainService.CancelAsync(cancellationToken);
        if (result.Status == MissionCommandStatus.Rejected)
        {
            return CreateRejectedResult(result.Message, run.Id);
        }

        await CancelRunAsync(run, result.Message, cancellationToken);
        return CreateAcceptedResult("Workflow cancel accepted by Worker.", run.Id);
    }

    public async Task<WorkerWorkflowRuntimeStatus> GetActiveRunAsync(
        string? robotId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedRobotId = NormalizeOptional(robotId);
        logger.LogInformation("Worker workflow active-run queried for robot {RobotId}.", normalizedRobotId);

        var query = dbContext.WorkflowRuns
            .AsNoTracking()
            .Where(item => ActiveRunStatuses.Contains(item.Status));

        if (!string.IsNullOrWhiteSpace(normalizedRobotId))
        {
            query = query.Where(item => item.RobotId == normalizedRobotId);
        }

        var run = await query
            .OrderByDescending(item => item.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (run is null)
        {
            return new WorkerWorkflowRuntimeStatus
            {
                RobotId = normalizedRobotId
            };
        }

        return new WorkerWorkflowRuntimeStatus
        {
            ActiveRunId = run.Id,
            WorkflowDefinitionId = run.WorkflowDefinitionId,
            RobotId = run.RobotId,
            Status = ParseWorkflowStatus(run.Status),
            CurrentStepSequence = run.CurrentStepSequence,
            StartedAt = run.StartedAt
        };
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

    private static WorkerWorkflowRuntimeResult? ValidateControlRequest(WorkerWorkflowControlRequest request)
        => string.IsNullOrWhiteSpace(request.RobotId)
            ? CreateValidationFailedResult("RobotId is required.")
            : null;

    private async Task<WorkflowRunEntity?> GetActiveRunForControlAsync(string robotId, CancellationToken cancellationToken)
    {
        var normalizedRobotId = robotId.Trim();
        return await dbContext.WorkflowRuns
            .Include(item => item.WorkflowDefinition)
            .Include(item => item.Steps.OrderBy(step => step.Sequence))
            .FirstOrDefaultAsync(
                item => item.RobotId == normalizedRobotId && ActiveRunStatuses.Contains(item.Status),
                cancellationToken);
    }

    private async Task UpdateControlStateAsync(
        WorkflowRunEntity run,
        WorkflowExecutionStatus workflowStatus,
        WorkflowStepExecutionStatus stepStatus,
        string? message,
        CancellationToken cancellationToken)
    {
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

    private async Task CancelRunAsync(WorkflowRunEntity run, string? message, CancellationToken cancellationToken)
    {
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

    private async Task StartFirstPendingStepAsync(WorkflowRunEntity run, CancellationToken cancellationToken)
        => await StartNextPendingStepAsync(run, cancellationToken);

    private async Task StartNextPendingStepAsync(WorkflowRunEntity run, CancellationToken cancellationToken)
    {
        var step = run.Steps.OrderBy(item => item.Sequence).FirstOrDefault(item => item.Status == WorkflowStepExecutionStatus.Pending.ToString());
        if (step is null)
        {
            run.Status = WorkflowExecutionStatus.Completed.ToString();
            run.CompletedAt = DateTimeOffset.UtcNow;
            run.CurrentStepSequence = null;
            await dbContext.SaveChangesAsync(cancellationToken);
            await EmitAsync("workflow.completed", run, cancellationToken);
            return;
        }

        step.StartedAt = DateTimeOffset.UtcNow;
        step.Status = WorkflowStepExecutionStatus.Starting.ToString();
        step.Message = $"Starting TaskChain '{step.TaskChainName}' (attempt 1).";
        step.Info = "attempt=1";
        run.Status = WorkflowExecutionStatus.Starting.ToString();
        run.CurrentStepSequence = step.Sequence;
        await dbContext.SaveChangesAsync(cancellationToken);
        await EmitAsync("workflow.step.started", run, cancellationToken);

        var result = await taskChainService.ExecuteTaskChainAsync(
            new TaskChainRunRequest
            {
                RobotId = run.RobotId,
                TaskChainName = step.TaskChainName,
                Confirmed = true
            },
            cancellationToken);

        step.TaskChainRunId = result.RunId;
        step.SeerTaskId = result.TaskId;
        step.Status = MapStepStatus(result.Status).ToString();
        step.Message = result.Message;
        if (IsTerminal(result.Status))
        {
            step.CompletedAt = result.CompletedAt ?? DateTimeOffset.UtcNow;
        }

        run.Status = MapWorkflowStatus(result.Status).ToString();
        if (run.Status is "Failed" or "Canceled" or "Completed")
        {
            run.CompletedAt = result.CompletedAt ?? DateTimeOffset.UtcNow;
        }

        if (result.Status == TaskChainRunStatus.Rejected)
        {
            run.ErrorMessage = result.Message;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await EmitAsync("workflow.updated", run, cancellationToken);
    }

    private async Task MonitorRunAsync(WorkflowRunEntity run, CancellationToken cancellationToken)
    {
        var step = run.Steps.FirstOrDefault(item => item.Sequence == run.CurrentStepSequence)
            ?? run.Steps.OrderBy(item => item.Sequence).FirstOrDefault(item => item.Status is "Starting" or "Waiting" or "Running" or "Paused");

        if (step is null)
        {
            await StartNextPendingStepAsync(run, cancellationToken);
            return;
        }

        if (step.Status == WorkflowStepExecutionStatus.Paused.ToString())
        {
            return;
        }

        if (step.TimeoutSeconds > 0 &&
            step.StartedAt is not null &&
            DateTimeOffset.UtcNow - step.StartedAt > TimeSpan.FromSeconds(step.TimeoutSeconds))
        {
            await HandleStepFailureAsync(
                run,
                step,
                WorkflowStepExecutionStatus.TimedOut,
                $"Step timed out after {step.TimeoutSeconds} seconds.",
                cancellationToken);
            return;
        }

        var taskChainStatus = await taskChainService.GetTaskChainStatusAsync(step.TaskChainName, true, cancellationToken);
        if (taskChainStatus is null || taskChainStatus.TaskListStatus is TaskChainStatus.None or TaskChainStatus.NotFound)
        {
            return;
        }

        SeerTaskRuntimeStatus? runtimeStatus = null;
        if (!string.IsNullOrWhiteSpace(taskChainStatus.TaskId))
        {
            runtimeStatus = (await taskChainService.GetTaskRuntimeStatusesAsync(taskChainStatus.TaskId, cancellationToken))
                .FirstOrDefault(item => string.Equals(item.TaskId, taskChainStatus.TaskId, StringComparison.OrdinalIgnoreCase));
        }

        step.SeerTaskId ??= taskChainStatus.TaskId;
        step.ProgressPercent = runtimeStatus?.Percentage;
        var nextStatus = MapStepStatus(taskChainStatus.TaskListStatus);
        step.Message = $"TaskChain '{step.TaskChainName}' is {nextStatus}.";

        if (IsStepInProgress(nextStatus))
        {
            step.Status = nextStatus.ToString();
            run.Status = MapInProgressWorkflowStatus(nextStatus).ToString();
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
            await StartNextPendingStepAsync(run, cancellationToken);
            return;
        }

        if (nextStatus == WorkflowStepExecutionStatus.Canceled)
        {
            await CancelRunAsync(run, step.Message, cancellationToken);
            return;
        }

        await HandleStepFailureAsync(run, step, nextStatus, step.Message, cancellationToken);
    }

    private async Task HandleStepFailureAsync(
        WorkflowRunEntity run,
        WorkflowRunStepEntity step,
        WorkflowStepExecutionStatus status,
        string? message,
        CancellationToken cancellationToken)
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
            await StartNextPendingStepAsync(run, cancellationToken);
            return;
        }

        if (policy == WorkflowFailurePolicy.ContinueWorkflow)
        {
            run.Status = WorkflowExecutionStatus.Running.ToString();
            await dbContext.SaveChangesAsync(cancellationToken);
            await EmitAsync("workflow.updated", run, cancellationToken);
            await StartNextPendingStepAsync(run, cancellationToken);
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

    private async Task EmitAsync(string eventType, WorkflowRunEntity run, CancellationToken cancellationToken)
    {
        if (run.WorkflowDefinition is null)
        {
            await dbContext.Entry(run).Reference(item => item.WorkflowDefinition).LoadAsync(cancellationToken);
        }

        await apiSyncClient.PushWorkflowAsync(
            new InternalWorkflowRunUpdate
            {
                EventType = eventType,
                WorkflowRun = MapRun(run),
                Message = run.ErrorMessage
            },
            cancellationToken);
    }

    private static WorkerWorkflowRuntimeResult CreateAcceptedResult(string message, Guid runId)
        => new()
        {
            Outcome = WorkerWorkflowRuntimeOutcome.Accepted,
            Message = message,
            RunId = runId
        };

    private static WorkerWorkflowRuntimeResult CreateRejectedResult()
        => new()
        {
            Outcome = WorkerWorkflowRuntimeOutcome.Rejected,
            Message = NotImplementedMessage
        };

    private static WorkerWorkflowRuntimeResult CreateRejectedResult(string message, Guid? runId)
        => new()
        {
            Outcome = WorkerWorkflowRuntimeOutcome.Rejected,
            Message = message,
            RunId = runId
        };

    private static WorkerWorkflowRuntimeResult CreateNotFoundResult(string message)
        => new()
        {
            Outcome = WorkerWorkflowRuntimeOutcome.NotFound,
            Message = message
        };

    private static WorkerWorkflowRuntimeResult CreateAlreadyActiveResult()
        => new()
        {
            Outcome = WorkerWorkflowRuntimeOutcome.AlreadyActive,
            Message = "Another workflow is already active on this robot."
        };

    private static WorkerWorkflowRuntimeResult CreateValidationFailedResult(string message)
        => new()
        {
            Outcome = WorkerWorkflowRuntimeOutcome.ValidationFailed,
            Message = message
        };

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
                TaskChainRunId = step.TaskChainRunId,
                SeerTaskId = step.SeerTaskId,
                StartedAt = step.StartedAt,
                CompletedAt = step.CompletedAt,
                ProgressPercent = step.ProgressPercent,
                Info = step.Info,
                Message = step.Message
            }).ToList()
        };

    private static WorkflowStepExecutionStatus MapStepStatus(TaskChainRunStatus status)
        => status switch
        {
            TaskChainRunStatus.Waiting => WorkflowStepExecutionStatus.Waiting,
            TaskChainRunStatus.Running or TaskChainRunStatus.Accepted => WorkflowStepExecutionStatus.Running,
            TaskChainRunStatus.Suspended => WorkflowStepExecutionStatus.Paused,
            TaskChainRunStatus.Completed => WorkflowStepExecutionStatus.Completed,
            TaskChainRunStatus.Canceled => WorkflowStepExecutionStatus.Canceled,
            TaskChainRunStatus.OverTime => WorkflowStepExecutionStatus.TimedOut,
            TaskChainRunStatus.UnknownTaskId => WorkflowStepExecutionStatus.Starting,
            TaskChainRunStatus.Failed or TaskChainRunStatus.Rejected => WorkflowStepExecutionStatus.Failed,
            _ => WorkflowStepExecutionStatus.Starting
        };

    private static WorkflowExecutionStatus MapWorkflowStatus(TaskChainRunStatus status)
        => status switch
        {
            TaskChainRunStatus.Suspended => WorkflowExecutionStatus.Paused,
            TaskChainRunStatus.Completed => WorkflowExecutionStatus.Completed,
            TaskChainRunStatus.Canceled => WorkflowExecutionStatus.Canceled,
            TaskChainRunStatus.OverTime or TaskChainRunStatus.Failed or TaskChainRunStatus.Rejected => WorkflowExecutionStatus.Failed,
            _ => WorkflowExecutionStatus.Running
        };

    private static WorkflowStepExecutionStatus MapStepStatus(TaskChainStatus status)
        => status switch
        {
            TaskChainStatus.Waiting => WorkflowStepExecutionStatus.Waiting,
            TaskChainStatus.Running => WorkflowStepExecutionStatus.Running,
            TaskChainStatus.Suspended => WorkflowStepExecutionStatus.Paused,
            TaskChainStatus.Completed => WorkflowStepExecutionStatus.Completed,
            TaskChainStatus.Canceled => WorkflowStepExecutionStatus.Canceled,
            TaskChainStatus.OverTime => WorkflowStepExecutionStatus.TimedOut,
            TaskChainStatus.Failed => WorkflowStepExecutionStatus.Failed,
            _ => WorkflowStepExecutionStatus.Starting
        };

    private static bool IsStepInProgress(WorkflowStepExecutionStatus status)
        => status is WorkflowStepExecutionStatus.Starting
            or WorkflowStepExecutionStatus.Waiting
            or WorkflowStepExecutionStatus.Running
            or WorkflowStepExecutionStatus.Paused;

    private static WorkflowExecutionStatus MapInProgressWorkflowStatus(WorkflowStepExecutionStatus status)
        => status switch
        {
            WorkflowStepExecutionStatus.Starting => WorkflowExecutionStatus.Starting,
            WorkflowStepExecutionStatus.Paused => WorkflowExecutionStatus.Paused,
            _ => WorkflowExecutionStatus.Running
        };

    private static WorkflowFailurePolicy ParseFailurePolicy(string? value)
        => Enum.TryParse<WorkflowFailurePolicy>(value, true, out var policy)
            ? policy
            : WorkflowFailurePolicy.StopWorkflow;

    private static int GetAttemptCount(WorkflowRunStepEntity step)
    {
        if (string.IsNullOrWhiteSpace(step.Info))
        {
            return 0;
        }

        var parts = step.Info.Split('=', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 && int.TryParse(parts[1], out var attempts) ? attempts : 0;
    }

    private static WorkflowExecutionStatus ParseWorkflowStatus(string? value)
        => Enum.TryParse<WorkflowExecutionStatus>(value, true, out var status)
            ? status
            : WorkflowExecutionStatus.Pending;

    private static WorkflowStepExecutionStatus ParseStepStatus(string? value)
        => Enum.TryParse<WorkflowStepExecutionStatus>(value, true, out var status)
            ? status
            : WorkflowStepExecutionStatus.Pending;

    private static bool IsTerminal(TaskChainRunStatus status)
        => status is TaskChainRunStatus.Completed
            or TaskChainRunStatus.Failed
            or TaskChainRunStatus.Canceled
            or TaskChainRunStatus.OverTime
            or TaskChainRunStatus.Rejected;

    private static bool IsActiveRunConstraintViolation(DbUpdateException exception)
        => FindPostgresException(exception) is { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_workflow_runs_active_robot" };

    private static PostgresException? FindPostgresException(Exception exception)
        => exception switch
        {
            PostgresException postgresException => postgresException,
            { InnerException: not null } => FindPostgresException(exception.InnerException),
            _ => null
        };

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
