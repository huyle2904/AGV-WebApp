using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using NewAGV.Contracts;

namespace NewAGV.Worker.Services;

public sealed class SeerRobotMapper(SeerTcpClient tcpClient, IOptions<SeerRobotOptions> options)
{
    public async Task<InternalRobotStateUpdate> ReadRobotStateAsync(CancellationToken cancellationToken)
    {
        var statusPort = options.Value.StatusPort;

        var info = await tcpClient.SendAsync(statusPort, 1000, null, cancellationToken);
        var location = await tcpClient.SendAsync(statusPort, 1004, null, cancellationToken);
        var speed = await tcpClient.SendAsync(statusPort, 1005, null, cancellationToken);
        var blocked = await tcpClient.SendAsync(statusPort, 1006, null, cancellationToken);
        var battery = await tcpClient.SendAsync(statusPort, 1007, null, cancellationToken);
        var estop = await tcpClient.SendAsync(statusPort, 1012, null, cancellationToken);
        var navigation = await tcpClient.SendAsync(statusPort, 1020, null, cancellationToken);
        var localization = await tcpClient.SendAsync(statusPort, 1021, null, cancellationToken);
        var alarms = await tcpClient.SendAsync(statusPort, 1050, null, cancellationToken);
        var controlOwner = await tcpClient.SendAsync(statusPort, 1060, null, cancellationToken);
        var map = await tcpClient.SendAsync(statusPort, 1300, null, cancellationToken);

        return BuildRobotState(info, location, speed, blocked, battery, estop, navigation, localization, alarms, controlOwner, map);
    }

    public async Task<InternalMapSnapshot> ReadMapSnapshotAsync(CancellationToken cancellationToken)
    {
        var statusPort = options.Value.StatusPort;
        var map = await tcpClient.SendAsync(statusPort, 1300, null, cancellationToken);
        var stations = await tcpClient.SendAsync(statusPort, 1301, null, cancellationToken);
        var entities = stations.ArrayValue("stations")?
            .OfType<JsonObject>()
            .Select(ToMapEntity)
            .Where(entity => entity is not null)
            .Select(entity => entity!)
            .ToList() ?? [];

        return new InternalMapSnapshot(map.StringValue("current_map"), entities);
    }

    public InternalRobotStateUpdate BuildRobotState(
        JsonObject info,
        JsonObject location,
        JsonObject speed,
        JsonObject blocked,
        JsonObject battery,
        JsonObject estop,
        JsonObject navigation,
        JsonObject localization,
        JsonObject alarms,
        JsonObject controlOwner,
        JsonObject map)
    {
        var robotId = info.StringValue("vehicle_id")
            ?? info.StringValue("id")
            ?? options.Value.RobotIdFallback;
        var robotName = info.StringValue("vehicle_id")
            ?? info.StringValue("robot_note")
            ?? options.Value.RobotNameFallback;

        var fatals = ParseAlarmItems(alarms.ArrayValue("fatals"));
        var errors = ParseAlarmItems(alarms.ArrayValue("errors"));
        var warnings = ParseAlarmItems(alarms.ArrayValue("warnings"));
        var notices = ParseAlarmItems(alarms.ArrayValue("notices"));
        var fatalCount = fatals.Count;
        var errorCount = errors.Count;
        var warningCount = warnings.Count;
        var taskStatus = navigation.IntValue("task_status");
        var charging = battery.BoolValue("charging") == true;
        var emergency = estop.BoolValue("emergency") == true || estop.BoolValue("driver_emc") == true || estop.BoolValue("soft_emc") == true;
        var isBlocked = blocked.BoolValue("blocked") == true;

        var robot = new RobotSummary(
            robotId,
            robotName,
            ResolveMode(taskStatus, charging, emergency, fatalCount, errorCount),
            ConnectivityStatus.Online,
            ResolveAlarm(fatalCount, errorCount, warningCount, emergency, isBlocked),
            Math.Clamp((int)Math.Round((battery.DoubleValue("battery_level") ?? 0) * 100), 0, 100),
            new Pose2D(
                location.DoubleValue("x") ?? 0,
                location.DoubleValue("y") ?? 0,
                RadiansToDegrees(location.DoubleValue("angle") ?? 0)),
            navigation.StringValue("move_status_info") ?? BuildCurrentTask(navigation),
            navigation.StringValue("target_id"),
            DateTimeOffset.UtcNow);

        var detail = new RobotTelemetryDetail(
            robotId,
            map.StringValue("current_map"),
            map.StringValue("current_map_md5"),
            new SeerBatteryDetail(
                battery.DoubleValue("battery_level"),
                battery.DoubleValue("battery_temp"),
                battery.BoolValue("charging"),
                battery.DoubleValue("voltage"),
                battery.DoubleValue("current"),
                battery.BoolValue("manual_charge"),
                battery.BoolValue("auto_charge")),
            new SeerEstopDetail(
                estop.BoolValue("emergency"),
                estop.BoolValue("driver_emc"),
                estop.BoolValue("soft_emc"),
                estop.BoolValue("electric")),
            new SeerAlarmDetail(
                fatalCount,
                errorCount,
                warningCount,
                notices.Count,
                fatals,
                errors,
                warnings,
                notices),
            new SeerLocalizationDetail(
                localization.IntValue("reloc_status"),
                location.DoubleValue("confidence"),
                localization.IntValue("loc_state"),
                location.IntValue("loc_method"),
                BuildRelocStatusLabel(localization.IntValue("reloc_status")),
                localization.IntValue("reloc_status") == 1),
            new SeerNavigationDetail(
                taskStatus,
                navigation.IntValue("task_type"),
                navigation.StringValue("target_id"),
                navigation.StringArrayValue("finished_path"),
                navigation.StringArrayValue("unfinished_path"),
                navigation.StringValue("move_status_info")),
            new SeerControlOwnerDetail(
                controlOwner.BoolValue("locked"),
                controlOwner.StringValue("ip"),
                controlOwner.IntValue("port"),
                controlOwner.StringValue("nick_name"),
                controlOwner.StringValue("desc")),
            DateTimeOffset.UtcNow);

        return new InternalRobotStateUpdate(robot, detail);
    }

    private static MapEntity? ToMapEntity(JsonObject station)
    {
        var id = station.StringValue("id");
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var type = string.Equals(station.StringValue("type"), "ChargePoint", StringComparison.OrdinalIgnoreCase)
            ? MapEntityType.Station
            : MapEntityType.Station;

        return new MapEntity(
            id,
            type,
            station.StringValue("desc") is { Length: > 0 } description ? description : id,
            station.DoubleValue("x") ?? 0,
            station.DoubleValue("y") ?? 0,
            2,
            2,
            "#0f766e",
            1,
            new Dictionary<string, string>
            {
                ["seerType"] = station.StringValue("type") ?? "Station",
                ["headingRad"] = (station.DoubleValue("r") ?? 0).ToString("0.####")
            });
    }

    private static RobotMode ResolveMode(int? taskStatus, bool charging, bool emergency, int fatalCount, int errorCount)
    {
        if (emergency || fatalCount > 0 || errorCount > 0)
        {
            return RobotMode.Fault;
        }

        if (charging)
        {
            return RobotMode.Charging;
        }

        return taskStatus switch
        {
            (int)SeerTaskStatus.Running => RobotMode.Navigating,
            (int)SeerTaskStatus.Suspended => RobotMode.Paused,
            _ => RobotMode.Idle
        };
    }

    private static AlarmSeverity ResolveAlarm(int fatalCount, int errorCount, int warningCount, bool emergency, bool blocked)
    {
        if (fatalCount > 0 || errorCount > 0 || emergency)
        {
            return AlarmSeverity.Critical;
        }

        return warningCount > 0 || blocked ? AlarmSeverity.Warning : AlarmSeverity.None;
    }

    private static string? BuildCurrentTask(JsonObject navigation)
    {
        var target = navigation.StringValue("target_id");
        var status = navigation.IntValue("task_status");
        return string.IsNullOrWhiteSpace(target) ? null : $"Task {status ?? 0}: {target}";
    }

    private static IReadOnlyList<SeerAlarmItem> ParseAlarmItems(JsonArray? array)
    {
        if (array is null)
        {
            return [];
        }

        return array
            .OfType<JsonObject>()
            .Select(ParseAlarmItem)
            .ToList();
    }

    private static SeerAlarmItem ParseAlarmItem(JsonObject item)
    {
        var codeProperty = item.FirstOrDefault(property => property.Key.All(char.IsDigit));
        long? timestamp = codeProperty.Value is null || !long.TryParse(codeProperty.Value.ToString(), out var parsedTimestamp)
            ? null
            : parsedTimestamp;

        return new SeerAlarmItem(
            codeProperty.Key ?? "unknown",
            timestamp,
            item.StringValue("desc"),
            item.IntValue("times"),
            item.StringValue("describe"),
            item.StringValue("method"),
            item.StringValue("reason"));
    }

    private static string BuildRelocStatusLabel(int? relocStatus)
        => relocStatus switch
        {
            0 => "Initializing",
            1 => "Success",
            2 => "Relocating",
            3 => "Completed, confirmation may be required",
            null => "Unknown",
            _ => $"Unknown ({relocStatus})"
        };

    private static double RadiansToDegrees(double radians) => radians * 180 / Math.PI;
}
