using Microsoft.EntityFrameworkCore;
using NewAGV.Api.Data;
using NewAGV.Contracts;

namespace NewAGV.Api.Services;

public sealed class WorkflowDefinitionService(NewAgvDbContext dbContext)
{
    public async Task<IReadOnlyList<WorkflowSummaryDto>> GetWorkflowsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.WorkflowDefinitions
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .Select(item => new WorkflowSummaryDto
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                Version = item.Version,
                IsPublished = item.IsPublished,
                AssignedRobotId = item.AssignedRobotId,
                ExecutionMode = item.ExecutionMode,
                RequiresConfirmation = item.RequiresConfirmation,
                StopOnFailure = item.StopOnFailure,
                ManualResume = item.ManualResume,
                StepCount = item.Steps.Count,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkflowDetailDto?> GetWorkflowAsync(Guid workflowId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.WorkflowDefinitions
            .AsNoTracking()
            .Include(item => item.Steps.OrderBy(step => step.Sequence))
            .FirstOrDefaultAsync(item => item.Id == workflowId, cancellationToken);

        return entity is null ? null : MapDetail(entity);
    }

    public async Task<WorkflowDetailDto> CreateWorkflowAsync(CreateWorkflowRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        await EnsureNameIsUniqueAsync(name, null, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var entity = new WorkflowDefinitionEntity
        {
            Name = name,
            Description = NormalizeOptional(request.Description),
            AssignedRobotId = NormalizeOptional(request.AssignedRobotId),
            ExecutionMode = NormalizeExecutionMode(request.ExecutionMode),
            RequiresConfirmation = request.RequiresConfirmation,
            StopOnFailure = request.StopOnFailure,
            ManualResume = request.ManualResume,
            IsPublished = request.IsPublished,
            CreatedAt = now,
            UpdatedAt = now
        };

        entity.Steps = MaterializeSteps(request.Steps, entity.Id);

        dbContext.WorkflowDefinitions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapDetail(entity);
    }

    public async Task<WorkflowDetailDto?> UpdateWorkflowAsync(Guid workflowId, UpdateWorkflowRequest request, CancellationToken cancellationToken)
    {
        var entity = await dbContext.WorkflowDefinitions
            .Include(item => item.Steps.OrderBy(step => step.Sequence))
            .FirstOrDefaultAsync(item => item.Id == workflowId, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var name = request.Name.Trim();
        await EnsureNameIsUniqueAsync(name, workflowId, cancellationToken);

        entity.Name = name;
        entity.Description = NormalizeOptional(request.Description);
        entity.AssignedRobotId = NormalizeOptional(request.AssignedRobotId);
        entity.ExecutionMode = NormalizeExecutionMode(request.ExecutionMode);
        entity.RequiresConfirmation = request.RequiresConfirmation;
        entity.StopOnFailure = request.StopOnFailure;
        entity.ManualResume = request.ManualResume;
        entity.IsPublished = request.IsPublished;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapDetail(entity);
    }

    public async Task<WorkflowDetailDto?> ReplaceStepsAsync(Guid workflowId, ReplaceWorkflowStepsRequest request, CancellationToken cancellationToken)
    {
        var entity = await dbContext.WorkflowDefinitions
            .Include(item => item.Steps.OrderBy(step => step.Sequence))
            .FirstOrDefaultAsync(item => item.Id == workflowId, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var existingSteps = entity.Steps.ToList();
        if (existingSteps.Count > 0)
        {
            dbContext.WorkflowSteps.RemoveRange(existingSteps);
            entity.Steps.Clear();
        }

        var replacementSteps = MaterializeSteps(request.Steps, workflowId);
        dbContext.WorkflowSteps.AddRange(replacementSteps);
        entity.Steps = replacementSteps;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapDetail(entity);
    }

    public async Task<WorkflowDetailDto?> DuplicateAsync(Guid workflowId, DuplicateWorkflowRequest request, CancellationToken cancellationToken)
    {
        var source = await dbContext.WorkflowDefinitions
            .AsNoTracking()
            .Include(item => item.Steps.OrderBy(step => step.Sequence))
            .FirstOrDefaultAsync(item => item.Id == workflowId, cancellationToken);

        if (source is null)
        {
            return null;
        }

        var name = request.Name.Trim();
        await EnsureNameIsUniqueAsync(name, null, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var entity = new WorkflowDefinitionEntity
        {
            Name = name,
            Description = source.Description,
            Version = source.Version,
            IsPublished = false,
            AssignedRobotId = source.AssignedRobotId,
            ExecutionMode = source.ExecutionMode,
            RequiresConfirmation = source.RequiresConfirmation,
            StopOnFailure = source.StopOnFailure,
            ManualResume = source.ManualResume,
            CreatedAt = now,
            UpdatedAt = now
        };

        entity.Steps = source.Steps
            .OrderBy(step => step.Sequence)
            .Select((step, index) => new WorkflowStepDto
            {
                Sequence = index + 1,
                StepType = step.StepType,
                TaskChainName = step.TaskChainName,
                DisplayName = step.DisplayName,
                TimeoutSeconds = step.TimeoutSeconds,
                RetryCount = step.RetryCount,
                FailurePolicy = ParseFailurePolicy(step.FailurePolicy),
                Note = step.Note,
                StopOnFailure = step.StopOnFailure,
                ParametersJson = step.ParametersJson
            })
            .Pipe(steps => MaterializeSteps(steps, entity.Id));

        dbContext.WorkflowDefinitions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapDetail(entity);
    }

    public async Task<WorkflowDetailDto?> SetPublishStateAsync(Guid workflowId, PublishWorkflowRequest request, CancellationToken cancellationToken)
    {
        var entity = await dbContext.WorkflowDefinitions
            .Include(item => item.Steps.OrderBy(step => step.Sequence))
            .FirstOrDefaultAsync(item => item.Id == workflowId, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        entity.IsPublished = request.IsPublished;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapDetail(entity);
    }

    public async Task<bool> DeleteWorkflowAsync(Guid workflowId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.WorkflowDefinitions
            .Include(item => item.Runs)
            .FirstOrDefaultAsync(item => item.Id == workflowId, cancellationToken);

        if (entity is null)
        {
            return false;
        }

        var hasActiveRun = entity.Runs.Any(run => run.Status is "Pending" or "Validating" or "Ready" or "Starting" or "Running" or "Paused");
        if (hasActiveRun)
        {
            throw new InvalidOperationException("Cannot delete a workflow with an active run.");
        }

        if (entity.Runs.Count > 0)
        {
            dbContext.WorkflowRuns.RemoveRange(entity.Runs);
        }

        dbContext.WorkflowDefinitions.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task EnsureNameIsUniqueAsync(string name, Guid? currentWorkflowId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Workflow name is required.");
        }

        var exists = await dbContext.WorkflowDefinitions.AnyAsync(
            item => item.Name == name && item.Id != currentWorkflowId,
            cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException($"Workflow name '{name}' already exists.");
        }
    }

    private static List<WorkflowStepEntity> MaterializeSteps(IEnumerable<WorkflowStepDto> steps, Guid workflowId)
        => steps
            .OrderBy(step => step.Sequence)
            .Select((step, index) => new WorkflowStepEntity
            {
                WorkflowDefinitionId = workflowId,
                Sequence = index + 1,
                StepType = NormalizeStepType(step.StepType),
                TaskChainName = step.TaskChainName.Trim(),
                DisplayName = NormalizeOptional(step.DisplayName),
                TimeoutSeconds = step.TimeoutSeconds,
                RetryCount = step.RetryCount,
                FailurePolicy = NormalizeFailurePolicy(step.FailurePolicy),
                Note = NormalizeOptional(step.Note),
                StopOnFailure = step.StopOnFailure,
                ParametersJson = NormalizeOptional(step.ParametersJson)
            })
            .ToList();

    private static WorkflowDetailDto MapDetail(WorkflowDefinitionEntity entity)
        => new()
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Version = entity.Version,
            IsPublished = entity.IsPublished,
            AssignedRobotId = entity.AssignedRobotId,
            ExecutionMode = entity.ExecutionMode,
            RequiresConfirmation = entity.RequiresConfirmation,
            StopOnFailure = entity.StopOnFailure,
            ManualResume = entity.ManualResume,
            StepCount = entity.Steps.Count,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            Steps = entity.Steps
                .OrderBy(step => step.Sequence)
                .Select(step => new WorkflowStepDto
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
                    StopOnFailure = step.StopOnFailure,
                    ParametersJson = step.ParametersJson
                })
                .ToList()
        };

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeExecutionMode(string? executionMode)
        => string.IsNullOrWhiteSpace(executionMode) ? "Sequential" : executionMode.Trim();

    private static string NormalizeStepType(string? stepType)
        => string.IsNullOrWhiteSpace(stepType) ? "TaskChain" : stepType.Trim();

    private static string NormalizeFailurePolicy(WorkflowFailurePolicy policy)
        => policy.ToString();

    private static WorkflowFailurePolicy ParseFailurePolicy(string? value)
        => Enum.TryParse<WorkflowFailurePolicy>(value, true, out var policy)
            ? policy
            : WorkflowFailurePolicy.StopWorkflow;
}

file static class WorkflowDefinitionServiceLinqExtensions
{
    public static TResult Pipe<TSource, TResult>(this TSource source, Func<TSource, TResult> selector) => selector(source);
}
