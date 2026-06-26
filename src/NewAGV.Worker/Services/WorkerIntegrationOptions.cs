namespace NewAGV.Worker.Services;

public sealed class WorkerIntegrationOptions
{
    public const string SectionName = "Integration";

    public bool UseWorkerWorkflowRuntime { get; set; }
}
