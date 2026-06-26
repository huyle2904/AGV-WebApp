namespace NewAGV.Api.Services;

public sealed class IntegrationOptions
{
    public const string SectionName = "Integration";

    public bool SeedDemoData { get; set; }
    public bool EnableSimulation { get; set; }
    public string? GatewayBaseUrl { get; set; }
    public int GatewayHealthTimeoutSeconds { get; set; } = 3;
    public string WorkerBaseUrl { get; set; } = "http://localhost:5230";
    public bool UseWorkerWorkflowRuntime { get; set; }
    public int TaskChainPollIntervalSeconds { get; set; } = 2;
}
