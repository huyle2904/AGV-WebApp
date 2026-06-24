using Microsoft.Extensions.DependencyInjection;

namespace NewAGV.Api.Services;

public sealed class WorkflowMonitorService(
    IServiceScopeFactory scopeFactory,
    ILogger<WorkflowMonitorService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var executionService = scope.ServiceProvider.GetRequiredService<WorkflowExecutionService>();
                await executionService.MonitorActiveRunsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to monitor active workflow runs.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
