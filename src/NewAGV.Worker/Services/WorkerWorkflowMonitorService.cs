using Microsoft.Extensions.Options;

namespace NewAGV.Worker.Services;

public sealed class WorkerWorkflowMonitorService(
    IServiceScopeFactory scopeFactory,
    IOptions<WorkerIntegrationOptions> options,
    ILogger<WorkerWorkflowMonitorService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (options.Value.UseWorkerWorkflowRuntime)
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var runtimeService = scope.ServiceProvider.GetRequiredService<WorkerWorkflowRuntimeService>();
                    await runtimeService.MonitorActiveRunsAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to monitor Worker-owned workflow runs.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
