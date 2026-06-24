using NewAGV.Contracts;

namespace NewAGV.Web.Services;

public sealed class PlantStateService(
    AgvApiClient apiClient,
    TelemetryClientService telemetryClient,
    ILogger<PlantStateService> logger)
{
    private bool _telemetryHooked;
    private bool _taskChainsRefreshInFlight;

    public List<RobotSummary> Robots { get; } = [];
    public Dictionary<string, RobotTelemetryDetail> RobotDetails { get; } = [];
    public List<MapEntity> MapEntities { get; } = [];
    public List<MissionAuditEntry> AuditEntries { get; } = [];
    public List<ControlPolicy> Policies { get; } = [];
    public List<SeerTaskChainSummary> TaskChains { get; } = [];
    public TaskChainRunSnapshot? ActiveTaskChainRun { get; private set; }
    public SiteHealth Health { get; private set; } = new(false, false, ConnectivityStatus.Offline, "Waiting for API.", 0, DateTimeOffset.UtcNow);
    public bool IsInitialized { get; private set; }

    public event Action? Changed;

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (IsInitialized)
        {
            return;
        }

        await RefreshAllAsync(cancellationToken);

        if (!_telemetryHooked)
        {
            telemetryClient.TelemetryReceived += ApplyTelemetry;
            await telemetryClient.StartAsync(cancellationToken);
            _telemetryHooked = true;
        }

        IsInitialized = true;
        Changed?.Invoke();
    }

    public async Task RefreshAllAsync(CancellationToken cancellationToken = default)
    {
        await RefreshRobotsAsync(cancellationToken);
        await RefreshRobotDetailsAsync(cancellationToken);
        await RefreshMapAsync(cancellationToken);
        await RefreshAuditAsync(cancellationToken);
        await RefreshPoliciesAsync(cancellationToken);
        await RefreshTaskChainsAsync(cancellationToken);
        await RefreshActiveTaskChainRunAsync(cancellationToken);
        Health = await apiClient.GetHealthAsync(cancellationToken);
        Changed?.Invoke();
    }

    public async Task RefreshRobotsAsync(CancellationToken cancellationToken = default)
        => ReplaceWith(Robots, await apiClient.GetRobotsAsync(cancellationToken), robot => robot.Name);

    public async Task RefreshRobotDetailsAsync(CancellationToken cancellationToken = default)
    {
        RobotDetails.Clear();
        foreach (var robot in Robots)
        {
            try
            {
                var detail = await apiClient.GetRobotDetailAsync(robot.RobotId, cancellationToken);
                if (detail is not null)
                {
                    RobotDetails[detail.RobotId] = detail;
                }
            }
            catch (HttpRequestException)
            {
                // Detail data is optional during startup while the worker is still connecting.
            }
        }
    }

    public async Task RefreshMapAsync(CancellationToken cancellationToken = default)
        => ReplaceWith(MapEntities, await apiClient.GetMapEntitiesAsync(cancellationToken), entity => $"{entity.Type}-{entity.Name}");

    public async Task RefreshAuditAsync(CancellationToken cancellationToken = default)
    {
        AuditEntries.Clear();
        AuditEntries.AddRange((await apiClient.GetAuditsAsync(cancellationToken)).OrderByDescending(entry => entry.OccurredAt));
        Changed?.Invoke();
    }

    public async Task RefreshPoliciesAsync(CancellationToken cancellationToken = default)
        => ReplaceWith(Policies, await apiClient.GetPoliciesAsync(cancellationToken), policy => policy.CommandType);

    public async Task RefreshTaskChainsAsync(CancellationToken cancellationToken = default)
    {
        ReplaceWith(TaskChains, await apiClient.GetTaskChainsAsync(cancellationToken), chain => chain.Name);
        Changed?.Invoke();
    }

    public async Task RefreshActiveTaskChainRunAsync(CancellationToken cancellationToken = default)
    {
        ActiveTaskChainRun = await apiClient.GetActiveTaskChainRunAsync(cancellationToken);
        Changed?.Invoke();
    }

    private void ApplyTelemetry(RealtimeEvent telemetry)
    {
        switch (telemetry.EventType)
        {
            case "robot.updated" when telemetry.Robot is not null:
                UpsertRobot(telemetry.Robot);
                if (telemetry.Detail is not null)
                {
                    RobotDetails[telemetry.Detail.RobotId] = telemetry.Detail;
                }

                if (TaskChains.Count == 0 && telemetry.Robot.Connectivity == ConnectivityStatus.Online)
                {
                    _ = RefreshTaskChainsWhenAvailableAsync();
                }
                break;
            case "health.updated" when telemetry.Health is not null:
                Health = telemetry.Health;
                if (TaskChains.Count == 0 && telemetry.Health.IntegrationStatus != ConnectivityStatus.Offline)
                {
                    _ = RefreshTaskChainsWhenAvailableAsync();
                }
                break;
            case "map.updated" when telemetry.MapEntity is not null:
                UpsertMapEntity(telemetry.MapEntity);
                break;
            case "command.ack":
                _ = RefreshAuditSafelyAsync();
                break;
            case var eventType when eventType.StartsWith("taskchain.", StringComparison.OrdinalIgnoreCase):
                if (telemetry.TaskChainRun is not null)
                {
                    ActiveTaskChainRun = telemetry.TaskChainRun;
                    UpsertTaskChainSummary(telemetry.TaskChainRun);
                }

                _ = RefreshAuditSafelyAsync();
                break;
        }

        Changed?.Invoke();
    }

    private async Task RefreshAuditSafelyAsync()
    {
        try
        {
            await RefreshAuditAsync();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to refresh audit log after telemetry event.");
        }
    }

    private async Task RefreshTaskChainsWhenAvailableAsync()
    {
        if (_taskChainsRefreshInFlight)
        {
            return;
        }

        _taskChainsRefreshInFlight = true;
        try
        {
            await RefreshTaskChainsAsync();
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Failed to refresh task chains after telemetry reconnect signal.");
        }
        finally
        {
            _taskChainsRefreshInFlight = false;
        }
    }

    private void UpsertRobot(RobotSummary robot)
    {
        var index = Robots.FindIndex(item => item.RobotId == robot.RobotId);
        if (index >= 0)
        {
            Robots[index] = robot;
        }
        else
        {
            Robots.Add(robot);
        }
    }

    private void UpsertMapEntity(MapEntity entity)
    {
        var index = MapEntities.FindIndex(item => item.EntityId == entity.EntityId);
        if (index >= 0)
        {
            MapEntities[index] = entity;
        }
        else
        {
            MapEntities.Add(entity);
        }
    }

    private void UpsertTaskChainSummary(TaskChainRunSnapshot snapshot)
    {
        var index = TaskChains.FindIndex(item => string.Equals(item.Name, snapshot.Run.TaskChainName, StringComparison.OrdinalIgnoreCase));
        var status = snapshot.Run.Status switch
        {
            TaskChainRunStatus.Completed => TaskChainStatus.Completed,
            TaskChainRunStatus.Failed => TaskChainStatus.Failed,
            TaskChainRunStatus.Canceled => TaskChainStatus.Canceled,
            TaskChainRunStatus.OverTime => TaskChainStatus.OverTime,
            _ => snapshot.TaskChainStatus?.TaskListStatus
        };
        var next = new SeerTaskChainSummary(
            snapshot.Run.TaskChainName,
            index >= 0 ? TaskChains[index].CreatedOn : null,
            status);

        if (index >= 0)
        {
            TaskChains[index] = next;
        }
        else
        {
            TaskChains.Add(next);
        }
    }

    private static void ReplaceWith<T, TKey>(List<T> target, IReadOnlyList<T> source, Func<T, TKey> ordering)
    {
        target.Clear();
        target.AddRange(source.OrderBy(ordering));
    }
}
