namespace NewAGV.Contracts;

public enum UserRole
{
    Operator = 0,
    Engineer = 1,
    Admin = 2
}

public enum RobotMode
{
    Idle,
    Navigating,
    Paused,
    Teleop,
    Charging,
    Fault
}

public enum ConnectivityStatus
{
    Online,
    Degraded,
    Offline
}

public enum AlarmSeverity
{
    None,
    Warning,
    Critical
}

public enum MapEntityType
{
    Station,
    Path,
    Zone,
    Waypoint
}

public enum MissionCommandType
{
    GoToStation,
    Pause,
    Resume,
    Cancel,
    ReturnToHome,
    Teleop
}

public enum MissionCommandStatus
{
    Queued,
    Accepted,
    Completed,
    Rejected
}

public enum TaskChainStatus
{
    None = 0,
    Waiting = 1,
    Running = 2,
    Suspended = 3,
    Completed = 4,
    Failed = 5,
    Canceled = 6,
    OverTime = 7,
    NotFound = 404
}

public enum TaskChainRunStatus
{
    Accepted,
    UnknownTaskId,
    Waiting,
    Running,
    Suspended,
    Completed,
    Failed,
    Canceled,
    OverTime,
    Rejected
}

public enum SeerTaskStatus
{
    None = 0,
    Waiting = 1,
    Running = 2,
    Suspended = 3,
    Completed = 4,
    Failed = 5,
    Canceled = 6,
    OverTime = 7,
    NotFound = 404
}

public record Pose2D(double X, double Y, double HeadingDegrees);

public record RobotSummary(
    string RobotId,
    string Name,
    RobotMode Mode,
    ConnectivityStatus Connectivity,
    AlarmSeverity AlarmState,
    int BatteryPercent,
    Pose2D Pose,
    string? CurrentTask,
    string? TargetEntityId,
    DateTimeOffset LastUpdated);

public record MapEntity
{
    public MapEntity()
    {
    }

    public MapEntity(
        string entityId,
        MapEntityType type,
        string name,
        double x,
        double y,
        double width,
        double height,
        string color,
        int version,
        Dictionary<string, string>? properties = null)
    {
        EntityId = entityId;
        Type = type;
        Name = name;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Color = color;
        Version = version;
        Properties = properties;
    }

    public string EntityId { get; set; } = string.Empty;
    public MapEntityType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string Color { get; set; } = "#38bdf8";
    public int Version { get; set; }
    public Dictionary<string, string>? Properties { get; set; }
}

public record MissionCommandRequest
{
    public MissionCommandRequest()
    {
    }

    public MissionCommandRequest(
        string robotId,
        MissionCommandType commandType,
        string? targetEntityId,
        double? velocityX,
        double? velocityY,
        bool confirmed)
    {
        RobotId = robotId;
        CommandType = commandType;
        TargetEntityId = targetEntityId;
        VelocityX = velocityX;
        VelocityY = velocityY;
        Confirmed = confirmed;
    }

    public string RobotId { get; set; } = string.Empty;
    public MissionCommandType CommandType { get; set; }
    public string? TargetEntityId { get; set; }
    public double? VelocityX { get; set; }
    public double? VelocityY { get; set; }
    public bool Confirmed { get; set; }
}

public record MissionCommandResult(
    string CommandId,
    string RobotId,
    MissionCommandType CommandType,
    MissionCommandStatus Status,
    string Message,
    DateTimeOffset RequestedAt,
    DateTimeOffset CompletedAt);

public record MissionAuditEntry(
    string AuditId,
    string RobotId,
    MissionCommandType? CommandType,
    UserRole RequestedByRole,
    string Message,
    MissionCommandStatus Status,
    DateTimeOffset OccurredAt,
    string? Operation = null)
{
    public string DisplayOperation => Operation ?? CommandType?.ToString() ?? "Unknown";
}

public record SeerBatteryDetail(
    double? BatteryLevel,
    double? BatteryTemperature,
    bool? Charging,
    double? Voltage,
    double? Current,
    bool? ManualCharge,
    bool? AutoCharge);

public record SeerEstopDetail(
    bool? Emergency,
    bool? DriverEmergency,
    bool? SoftEmergency,
    bool? Electric);

public record SeerAlarmItem(
    string Code,
    long? Timestamp,
    string? Description,
    int? Times,
    string? Describe,
    string? Method,
    string? Reason);

public record SeerAlarmDetail(
    int FatalCount,
    int ErrorCount,
    int WarningCount,
    int NoticeCount,
    IReadOnlyList<SeerAlarmItem> Fatals,
    IReadOnlyList<SeerAlarmItem> Errors,
    IReadOnlyList<SeerAlarmItem> Warnings,
    IReadOnlyList<SeerAlarmItem> Notices);

public record SeerLocalizationDetail(
    int? RelocStatus,
    double? Confidence,
    int? LocState,
    int? LocMethod,
    string StatusLabel,
    bool ReadyForNavigation);

public record SeerNavigationDetail(
    int? TaskStatus,
    int? TaskType,
    string? TargetId,
    IReadOnlyList<string> FinishedPath,
    IReadOnlyList<string> UnfinishedPath,
    string? MoveStatusInfo);

public record SeerControlOwnerDetail(
    bool? Locked,
    string? Ip,
    int? Port,
    string? NickName,
    string? Description);

public record RobotTelemetryDetail(
    string RobotId,
    string? CurrentMap,
    string? CurrentMapMd5,
    SeerBatteryDetail? Battery,
    SeerEstopDetail? Estop,
    SeerAlarmDetail? Alarm,
    SeerLocalizationDetail? Localization,
    SeerNavigationDetail? Navigation,
    SeerControlOwnerDetail? ControlOwner,
    DateTimeOffset UpdatedAt);

public record WorkerMissionCommandRequest(
    MissionCommandRequest Request,
    UserRole RequestedByRole);

public record InternalRobotStateUpdate(
    RobotSummary Robot,
    RobotTelemetryDetail? Detail);

public record InternalMapSnapshot(
    string? CurrentMap,
    IReadOnlyList<MapEntity> Entities);

public record ControlPolicy(
    MissionCommandType CommandType,
    bool RequiresConfirmation,
    UserRole MinimumRole,
    bool Enabled,
    string Description);

public record SeerTaskChainSummary(
    string Name,
    DateTimeOffset? CreatedOn,
    TaskChainStatus? LastKnownStatus);

public record SeerTaskChainStatus(
    string TaskListName,
    TaskChainStatus TaskListStatus,
    string? TaskId,
    bool? Loop,
    int? ActionGroupId,
    IReadOnlyList<int> ActionIds,
    int? BatteryPercent);

public record SeerTaskRuntimeStatus(
    string TaskId,
    TaskChainStatus Status,
    int? Type,
    string? ClosestTarget,
    string? SourceName,
    string? TargetName,
    double? Percentage,
    double? Distance,
    string? Info);

public record TaskChainRunRequest
{
    public string RobotId { get; set; } = string.Empty;
    public string TaskChainName { get; set; } = string.Empty;
    public bool Confirmed { get; set; }
}

public record TaskChainControlRequest
{
    public string RobotId { get; set; } = string.Empty;
}

public record TaskChainRunResult(
    string RunId,
    string RobotId,
    string TaskChainName,
    TaskChainRunStatus Status,
    string Message,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? TaskId);

public record TaskChainRunSnapshot(
    TaskChainRunResult Run,
    SeerTaskChainStatus? TaskChainStatus,
    SeerTaskRuntimeStatus? RuntimeStatus,
    UserRole RequestedByRole,
    DateTimeOffset LastUpdated);

public record SiteHealth(
    bool ApiHealthy,
    bool WorkerHealthy,
    ConnectivityStatus IntegrationStatus,
    string Message,
    int ActiveRobotCount,
    DateTimeOffset UpdatedAt);

public record GatewayHealth(
    string BaseUrl,
    string Host,
    int Port,
    ConnectivityStatus Status,
    string Message,
    int? HttpStatusCode,
    string? ServerHeader,
    DateTimeOffset CheckedAt);

public record SeerRelocationRequest(double X, double Y, double Angle);

public record TeleopRequest(double VelocityX, double VelocityY, double AngularVelocity);

public record RealtimeEvent(
    string EventType,
    DateTimeOffset OccurredAt,
    RobotSummary? Robot = null,
    RobotTelemetryDetail? Detail = null,
    MissionCommandResult? Command = null,
    TaskChainRunSnapshot? TaskChainRun = null,
    MapEntity? MapEntity = null,
    SiteHealth? Health = null,
    string? Message = null);


