using Microsoft.Extensions.Options;

namespace NewAGV.Api.Services;

public sealed class TaskChainMonitorService(
    IServiceScopeFactory scopeFactory,
    IOptions<IntegrationOptions> options,
    ILogger<TaskChainMonitorService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromSeconds(Math.Clamp(options.Value.TaskChainPollIntervalSeconds, 1, 10));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var coordinator = scope.ServiceProvider.GetRequiredService<TaskChainCoordinator>();
                await coordinator.PollActiveRunsAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Failed to poll active task chain runs.");
            }

            await Task.Delay(pollInterval, stoppingToken);
        }
    }
}
