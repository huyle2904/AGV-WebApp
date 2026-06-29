using NewAGV.Contracts;

namespace NewAGV.Api.Services;

public sealed class WorkflowValidationService(
    WorkflowDefinitionService definitionService,
    TaskChainCatalogService taskChainCatalogService,
    AgvPlantStore plantStore)
{
    public async Task<WorkflowValidationResult> ValidateWorkflowAsync(Guid workflowId, CancellationToken cancellationToken)
    {
        var workflow = await definitionService.GetWorkflowAsync(workflowId, cancellationToken);
        if (workflow is null)
        {
            return new WorkflowValidationResult
            {
                IsValid = false,
                CanPublish = false,
                CanExecute = false,
                Issues =
                [
                    new WorkflowValidationIssue
                    {
                        Code = "workflow.not_found",
                        Severity = "Error",
                        Message = "Workflow definition was not found."
                    }
                ]
            };
        }

        return await ValidateWorkflowAsync(workflow, cancellationToken);
    }

    public async Task<WorkflowValidationResult> ValidateWorkflowAsync(WorkflowDetailDto workflow, CancellationToken cancellationToken)
    {
        var issues = new List<WorkflowValidationIssue>();

        if (string.IsNullOrWhiteSpace(workflow.Name))
        {
            issues.Add(CreateIssue("workflow.name_required", "Error", "Workflow name is required.", field: nameof(workflow.Name)));
        }

        if (!string.Equals(workflow.ExecutionMode, "Sequential", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(CreateIssue("workflow.execution_mode_invalid", "Error", "Only Sequential execution mode is supported in this phase.", field: nameof(workflow.ExecutionMode)));
        }

        if (workflow.Steps.Count == 0)
        {
            issues.Add(CreateIssue("workflow.steps_required", "Error", "Workflow must contain at least one step.", field: nameof(workflow.Steps)));
        }

        if (!string.IsNullOrWhiteSpace(workflow.AssignedRobotId) && plantStore.GetRobot(workflow.AssignedRobotId) is null)
        {
            issues.Add(CreateIssue("workflow.robot_not_found", "Error", $"Assigned robot '{workflow.AssignedRobotId}' was not found.", field: nameof(workflow.AssignedRobotId)));
        }

        var taskChainLookup = await taskChainCatalogService.GetTaskChainLookupAsync(cancellationToken);

        var orderedSteps = workflow.Steps.OrderBy(step => step.Sequence).ToList();
        for (var index = 0; index < orderedSteps.Count; index++)
        {
            var step = orderedSteps[index];
            var expectedSequence = index + 1;

            if (step.Sequence != expectedSequence)
            {
                issues.Add(CreateIssue(
                    "workflow.step_sequence_invalid",
                    "Error",
                    $"Step sequence must be continuous. Expected {expectedSequence} but found {step.Sequence}.",
                    step.Id,
                    step.Sequence,
                    nameof(step.Sequence)));
            }

            if (!string.Equals(step.StepType, "TaskChain", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(CreateIssue(
                    "workflow.step_type_invalid",
                    "Error",
                    "Only TaskChain steps are supported in this phase.",
                    step.Id,
                    step.Sequence,
                    nameof(step.StepType)));
            }

            if (string.IsNullOrWhiteSpace(step.TaskChainName))
            {
                issues.Add(CreateIssue(
                    "workflow.step_taskchain_required",
                    "Error",
                    "TaskChainName is required.",
                    step.Id,
                    step.Sequence,
                    nameof(step.TaskChainName)));
            }
            else if (!taskChainLookup.TryGetValue(step.TaskChainName, out var taskChainSummary))
            {
                issues.Add(CreateIssue(
                    "workflow.step_taskchain_missing",
                    "Error",
                    $"TaskChain '{step.TaskChainName}' does not exist in the durable taskchain catalog.",
                    step.Id,
                    step.Sequence,
                    nameof(step.TaskChainName)));
            }
            else if (string.Equals(taskChainSummary.Availability, "MissingFromSource", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(CreateIssue(
                    "workflow.step_taskchain_missing",
                    "Error",
                    $"TaskChain '{step.TaskChainName}' is missing from the AGV source catalog.",
                    step.Id,
                    step.Sequence,
                    nameof(step.TaskChainName)));
            }
            else if (string.Equals(taskChainSummary.Availability, "Stale", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(CreateIssue(
                    "workflow.step_taskchain_stale",
                    "Error",
                    $"TaskChain '{step.TaskChainName}' catalog data is stale and must be re-synced before validation can pass.",
                    step.Id,
                    step.Sequence,
                    nameof(step.TaskChainName)));
            }

            if (step.TimeoutSeconds < 0 || step.TimeoutSeconds > 86400)
            {
                issues.Add(CreateIssue(
                    "workflow.step_timeout_invalid",
                    "Error",
                    "TimeoutSeconds must be between 0 and 86400.",
                    step.Id,
                    step.Sequence,
                    nameof(step.TimeoutSeconds)));
            }

            if (step.RetryCount < 0 || step.RetryCount > 10)
            {
                issues.Add(CreateIssue(
                    "workflow.step_retry_invalid",
                    "Error",
                    "RetryCount must be between 0 and 10.",
                    step.Id,
                    step.Sequence,
                    nameof(step.RetryCount)));
            }
        }

        var hasBlockingError = issues.Any(issue => string.Equals(issue.Severity, "Error", StringComparison.OrdinalIgnoreCase));
        return new WorkflowValidationResult
        {
            IsValid = !hasBlockingError,
            CanPublish = !hasBlockingError,
            CanExecute = !hasBlockingError,
            Issues = issues
        };
    }

    private static WorkflowValidationIssue CreateIssue(
        string code,
        string severity,
        string message,
        Guid? stepId = null,
        int? sequence = null,
        string? field = null)
        => new()
        {
            Code = code,
            Severity = severity,
            Message = message,
            StepId = stepId,
            Sequence = sequence,
            Field = field
        };
}
