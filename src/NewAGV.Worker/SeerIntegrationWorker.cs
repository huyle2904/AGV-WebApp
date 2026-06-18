using Microsoft.Extensions.Options;
using NewAGV.Contracts;
using NewAGV.Worker.Services;

namespace NewAGV.Worker;

public sealed class SeerIntegrationWorker(
    SeerRobotMapper mapper,
    ApiSyncClient apiSyncClient,
    IOptions<SeerRobotOptions> options,
    ILogger<SeerIntegrationWorker> logger) : BackgroundService
{
    private DateTimeOffset _lastMapSync = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "SEER integration worker started. Robot={Host}, API={ApiBaseUrl}",
            options.Value.Host,
            options.Value.ApiBaseUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var robotUpdate = await mapper.ReadRobotStateAsync(stoppingToken);
                await apiSyncClient.PushRobotAsync(robotUpdate, stoppingToken);

                if (DateTimeOffset.UtcNow - _lastMapSync > TimeSpan.FromSeconds(options.Value.MapSyncIntervalSeconds))
                {
                    var mapSnapshot = await mapper.ReadMapSnapshotAsync(stoppingToken);
                    await apiSyncClient.PushMapAsync(mapSnapshot, stoppingToken);
                    _lastMapSync = DateTimeOffset.UtcNow;
                }

                await apiSyncClient.PushHealthAsync(
                    new SiteHealth(
                        ApiHealthy: true,
                        WorkerHealthy: true,
                        IntegrationStatus: ConnectivityStatus.Online,
                        Message: $"Connected to SEER AGV at {options.Value.Host}.",
                        ActiveRobotCount: 1,
                        UpdatedAt: DateTimeOffset.UtcNow),
                    stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Failed to poll SEER AGV.");

                await apiSyncClient.PushHealthAsync(
                    new SiteHealth(
                        ApiHealthy: true,
                        WorkerHealthy: false,
                        IntegrationStatus: ConnectivityStatus.Offline,
                        Message: $"SEER AGV connection failed: {exception.Message}",
                        ActiveRobotCount: 0,
                        UpdatedAt: DateTimeOffset.UtcNow),
                    stoppingToken);
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, options.Value.PollIntervalSeconds)), stoppingToken);
        }
    }
}
