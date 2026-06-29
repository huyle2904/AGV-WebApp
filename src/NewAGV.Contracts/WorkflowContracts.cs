namespace NewAGV.Contracts;

public enum WorkflowExecutionStatus
{
    Draft,
    Pending,
    Validating,
    Ready,
    Starting,
    Running,
    Paused,
    Completed,
    Failed,
    Canceled
}

public enum WorkflowStepExecutionStatus
{
    Pending,
    Waiting,
    Starting,
    Running,
    Paused,
    Completed,
    Failed,
    Skipped,
    Canceled,
    TimedOut
}

public enum WorkflowFailurePolicy
{
    StopWorkflow,
    ContinueWorkflow,
    RetryStep,
    RequireManualResume
}

public record WorkflowStepDto
{
    public Guid Id { get; set; }
    public int Sequence { get; set; }
    public string StepType { get; set; } = "TaskChain";
    public string TaskChainName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public int TimeoutSeconds { get; set; }
    public int RetryCount { get; set; }
    public WorkflowFailurePolicy FailurePolicy { get; set; } = WorkflowFailurePolicy.StopWorkflow;
    public string? Note { get; set; }
    public bool StopOnFailure { get; set; } = true;
    public string? ParametersJson { get; set; }
}

public record WorkflowSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Version { get; set; } = 1;
    public bool IsPublished { get; set; }
    public string? AssignedRobotId { get; set; }
    public string ExecutionMode { get; set; } = "Sequential";
    public bool RequiresConfirmation { get; set; }
    public bool StopOnFailure { get; set; } = true;
    public bool ManualResume { get; set; }
    public int StepCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public record WorkflowDetailDto : WorkflowSummaryDto
{
    public IReadOnlyList<WorkflowStepDto> Steps { get; set; } = Array.Empty<WorkflowStepDto>();
}

public record CreateWorkflowRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? AssignedRobotId { get; set; }
    public string ExecutionMode { get; set; } = "Sequential";
    public bool RequiresConfirmation { get; set; }
    public bool StopOnFailure { get; set; } = true;
    public bool ManualResume { get; set; }
    public bool IsPublished { get; set; }
    public IReadOnlyList<WorkflowStepDto> Steps { get; set; } = Array.Empty<WorkflowStepDto>();
}

public record UpdateWorkflowRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? AssignedRobotId { get; set; }
    public string ExecutionMode { get; set; } = "Sequential";
    public bool RequiresConfirmation { get; set; }
    public bool StopOnFailure { get; set; } = true;
    public bool ManualResume { get; set; }
    public bool IsPublished { get; set; }
}

public record ReplaceWorkflowStepsRequest
{
    public IReadOnlyList<WorkflowStepDto> Steps { get; set; } = Array.Empty<WorkflowStepDto>();
}

public record DuplicateWorkflowRequest
{
    public string Name { get; set; } = string.Empty;
}

public record PublishWorkflowRequest
{
    public bool IsPublished { get; set; } = true;
}

public record WorkflowValidationIssue
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = "Error";
    public string Message { get; set; } = string.Empty;
    public Guid? StepId { get; set; }
    public int? Sequence { get; set; }
    public string? Field { get; set; }
}

public record WorkflowValidationResult
{
    public bool IsValid { get; set; }
    public bool CanPublish { get; set; }
    public bool CanExecute { get; set; }
    public IReadOnlyList<WorkflowValidationIssue> Issues { get; set; } = Array.Empty<WorkflowValidationIssue>();
}

public record ExecuteWorkflowRequest
{
    public string RobotId { get; set; } = string.Empty;
    public bool Confirmed { get; set; }
    public string? TriggeredBy { get; set; }
}

public record WorkflowRunStepDto
{
    public Guid Id { get; set; }
    public int Sequence { get; set; }
    public string StepType { get; set; } = "TaskChain";
    public string TaskChainName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public int TimeoutSeconds { get; set; }
    public int RetryCount { get; set; }
    public WorkflowFailurePolicy FailurePolicy { get; set; } = WorkflowFailurePolicy.StopWorkflow;
    public string? Note { get; set; }
    public WorkflowStepExecutionStatus Status { get; set; } = WorkflowStepExecutionStatus.Pending;
    public string? TaskChainRunId { get; set; }
    public string? SeerTaskId { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public double? ProgressPercent { get; set; }
    public string? Info { get; set; }
    public string? Message { get; set; }
}

public record WorkflowRunDto
{
    public Guid Id { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public string WorkflowName { get; set; } = string.Empty;
    public int WorkflowVersion { get; set; }
    public string RobotId { get; set; } = string.Empty;
    public WorkflowExecutionStatus Status { get; set; } = WorkflowExecutionStatus.Pending;
    public string? TriggeredBy { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int? CurrentStepSequence { get; set; }
    public string? CanceledBy { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ValidationSnapshotJson { get; set; }
    public IReadOnlyList<WorkflowRunStepDto> Steps { get; set; } = Array.Empty<WorkflowRunStepDto>();
}

public record InternalWorkflowRunUpdate
{
    public string EventType { get; set; } = "workflow.updated";
    public WorkflowRunDto WorkflowRun { get; set; } = new();
    public string? Message { get; set; }
}

public record WorkflowHistoryEntryDto
{
    public Guid RunId { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public string WorkflowName { get; set; } = string.Empty;
    public string RobotId { get; set; } = string.Empty;
    public int? StepSequence { get; set; }
    public string? StepName { get; set; }
    public WorkflowStepExecutionStatus Status { get; set; } = WorkflowStepExecutionStatus.Pending;
    public string? Message { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public record WorkflowControlRequest
{
    public string RobotId { get; set; } = string.Empty;
}

public enum WorkerWorkflowRuntimeOutcome
{
    Accepted,
    Rejected,
    AlreadyActive,
    NotFound,
    ValidationFailed,
    Unknown
}

public record WorkerWorkflowStartRequest
{
    public Guid WorkflowDefinitionId { get; set; }
    public string RobotId { get; set; } = string.Empty;
    public string? TriggeredBy { get; set; }
}

public record WorkerWorkflowControlRequest
{
    public string RobotId { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public record WorkerWorkflowRuntimeResult
{
    public WorkerWorkflowRuntimeOutcome Outcome { get; set; }
    public string? Message { get; set; }
    public Guid? RunId { get; set; }
}

public record WorkerWorkflowRuntimeStatus
{
    public Guid? ActiveRunId { get; set; }
    public Guid? WorkflowDefinitionId { get; set; }
    public string? RobotId { get; set; }
    public WorkflowExecutionStatus? Status { get; set; }
    public int? CurrentStepSequence { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
}
