using NewAGV.Contracts;

namespace NewAGV.Api.Tests.TestSupport;

internal static class WorkflowTestData
{
    public static CreateWorkflowRequest CreateWorkflow(
        string name = "WF-Validation",
        params WorkflowStepDto[] steps)
    {
        return new CreateWorkflowRequest
        {
            Name = name,
            ExecutionMode = "Sequential",
            Steps = steps
        };
    }

    public static WorkflowStepDto CreateStep(
        int sequence,
        string taskChainName,
        string stepType = "TaskChain",
        int timeoutSeconds = 30,
        int retryCount = 0)
    {
        return new WorkflowStepDto
        {
            Sequence = sequence,
            StepType = stepType,
            TaskChainName = taskChainName,
            TimeoutSeconds = timeoutSeconds,
            RetryCount = retryCount,
            FailurePolicy = WorkflowFailurePolicy.StopWorkflow,
            StopOnFailure = true
        };
    }
}
