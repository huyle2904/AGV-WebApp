using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using NewAGV.Contracts;

namespace NewAGV.Api.Services;

public sealed class AgvPlantStore
{
    private readonly ConcurrentDictionary<string, RobotSummary> _robots = new();
    private readonly ConcurrentDictionary<string, RobotTelemetryDetail> _robotDetails = new();
    private readonly ConcurrentDictionary<string, MapEntity> _mapEntities = new();
    private readonly List<MissionAuditEntry> _audits = [];
    private readonly List<ControlPolicy> _policies =
    [
        new(MissionCommandType.GoToStation, true, UserRole.Operator, true, "Route a robot to a configured station."),
        new(MissionCommandType.Pause, false, UserRole.Operator, true, "Pause the active mission safely."),
        new(MissionCommandType.Resume, false, UserRole.Operator, true, "Resume a paused mission."),
        new(MissionCommandType.Cancel, true, UserRole.Operator, true, "Cancel the active mission."),
        new(MissionCommandType.ReturnToHome, true, UserRole.Operator, true, "Return the robot to the home station."),
        new(MissionCommandType.Teleop, true, UserRole.Engineer, false, "Restricted open-loop teleoperation.")
    ];

    private readonly object _auditLock = new();

    public AgvPlantStore(IOptions<IntegrationOptions> options)
    {
        if (options.Value.SeedDemoData)
        {
            SeedMapEntities();
            SeedRobots();
        }

        Health = new SiteHealth(
            ApiHealthy: true,
            WorkerHealthy: options.Value.EnableSimulation,
            IntegrationStatus: options.Value.EnableSimulation ? ConnectivityStatus.Online : ConnectivityStatus.Offline,
            Message: options.Value.EnableSimulation
                ? "Simulation worker is enabled."
                : "No SEER adapter is configured yet.",
            ActiveRobotCount: _robots.Count,
            UpdatedAt: DateTimeOffset.UtcNow);
    }

    public SiteHealth Health { get; private set; }

    public IReadOnlyList<RobotSummary> GetRobots() => _robots.Values.OrderBy(robot => robot.Name).ToList();

    public RobotTelemetryDetail? GetRobotDetail(string robotId)
        => _robotDetails.TryGetValue(robotId, out var detail) ? detail : null;

    public IReadOnlyList<MapEntity> GetMapEntities() => _mapEntities.Values
        .OrderBy(entity => entity.Type)
        .ThenBy(entity => entity.Name)
        .ToList();

    public IReadOnlyList<MissionAuditEntry> GetAudits()
    {
        lock (_auditLock)
        {
            return _audits.OrderByDescending(audit => audit.OccurredAt).ToList();
        }
    }

    public IReadOnlyList<ControlPolicy> GetPolicies() => _policies;

    public RobotSummary? GetRobot(string robotId) => _robots.TryGetValue(robotId, out var robot) ? robot : null;

    public MapEntity? GetMapEntity(string entityId) => _mapEntities.TryGetValue(entityId, out var entity) ? entity : null;

    public MapEntity UpsertMapEntity(MapEntity entity)
    {
        var nextVersion = _mapEntities.TryGetValue(entity.EntityId, out var current)
            ? current.Version + 1
            : 1;

        var normalized = entity with
        {
            Version = nextVersion,
            Properties = entity.Properties ?? new Dictionary<string, string>()
        };

        _mapEntities[normalized.EntityId] = normalized;
        return normalized;
    }

    public bool DeleteMapEntity(string entityId)
    {
        return _mapEntities.TryRemove(entityId, out _);
    }

    public void ReplaceMapEntities(IEnumerable<MapEntity> entities)
    {
        _mapEntities.Clear();
        foreach (var entity in entities)
        {
            _mapEntities[entity.EntityId] = entity;
        }
    }

    public void UpdateRobot(RobotSummary robot)
    {
        _robots[robot.RobotId] = robot;
        Health = Health with
        {
            ActiveRobotCount = _robots.Count,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public void UpdateRobotDetail(RobotTelemetryDetail detail)
    {
        _robotDetails[detail.RobotId] = detail;
    }

    public void UpdateHealth(SiteHealth health)
    {
        Health = health;
    }

    public void AddAudit(MissionAuditEntry auditEntry)
    {
        lock (_auditLock)
        {
            _audits.Add(auditEntry);
            if (_audits.Count > 250)
            {
                _audits.RemoveRange(0, _audits.Count - 250);
            }
        }
    }

    private void SeedRobots()
    {
        var now = DateTimeOffset.UtcNow;
        _robots["AGV-01"] = new RobotSummary("AGV-01", "Forklift Alpha", RobotMode.Idle, ConnectivityStatus.Online, AlarmSeverity.None, 88, new Pose2D(14, 10, 0), null, null, now);
        _robots["AGV-02"] = new RobotSummary("AGV-02", "Cart Beta", RobotMode.Navigating, ConnectivityStatus.Online, AlarmSeverity.Warning, 64, new Pose2D(48, 22, 135), "Inbound to ST-203", "ST-203", now);
        _robots["AGV-03"] = new RobotSummary("AGV-03", "Tugger Gamma", RobotMode.Charging, ConnectivityStatus.Degraded, AlarmSeverity.None, 27, new Pose2D(12, 42, 180), "Docked at charge bay", "HOME-01", now);
    }

    private void SeedMapEntities()
    {
        var entities = new[]
        {
            new MapEntity("ST-101", MapEntityType.Station, "Inbound A", 14, 10, 8, 8, "#7dd3fc", 1),
            new MapEntity("ST-203", MapEntityType.Station, "Outbound B", 72, 18, 8, 8, "#86efac", 1),
            new MapEntity("HOME-01", MapEntityType.Station, "Charge Bay", 12, 42, 10, 10, "#fcd34d", 1),
            new MapEntity("ZONE-01", MapEntityType.Zone, "Pedestrian Buffer", 38, 8, 18, 14, "#fca5a5", 1),
            new MapEntity("PATH-01", MapEntityType.Path, "Main Corridor", 22, 20, 46, 4, "#94a3b8", 1),
            new MapEntity("WP-01", MapEntityType.Waypoint, "Queue Point", 44, 32, 4, 4, "#c4b5fd", 1)
        };

        foreach (var entity in entities)
        {
            _mapEntities[entity.EntityId] = entity;
        }
    }
}
