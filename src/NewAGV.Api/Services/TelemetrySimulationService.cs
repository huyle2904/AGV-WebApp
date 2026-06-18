using Microsoft.AspNetCore.SignalR;
using NewAGV.Api.Hubs;
using NewAGV.Contracts;

namespace NewAGV.Api.Services;

public sealed class TelemetrySimulationService(
    AgvPlantStore store,
    IHubContext<TelemetryHub> hubContext,
    ILogger<TelemetrySimulationService> logger) : BackgroundService
{
    private int _tick;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting telemetry simulation service.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var robot in store.GetRobots())
                {
                    var updatedRobot = AdvanceRobot(robot);
                    store.UpdateRobot(updatedRobot);

                    await hubContext.Clients.All.SendAsync(
                        "ReceiveTelemetry",
                        new RealtimeEvent("robot.updated", DateTimeOffset.UtcNow, Robot: updatedRobot),
                        stoppingToken);
                }

                _tick++;
                var health = new SiteHealth(
                    ApiHealthy: true,
                    WorkerHealthy: true,
                    IntegrationStatus: _tick % 9 == 0 ? ConnectivityStatus.Degraded : ConnectivityStatus.Online,
                    Message: _tick % 9 == 0 ? "Simulation adapter responded slowly; running in degraded mode." : "Simulation worker healthy.",
                    ActiveRobotCount: store.GetRobots().Count,
                    UpdatedAt: DateTimeOffset.UtcNow);

                store.UpdateHealth(health);

                await hubContext.Clients.All.SendAsync(
                    "ReceiveTelemetry",
                    new RealtimeEvent("health.updated", DateTimeOffset.UtcNow, Health: health, Message: health.Message),
                    stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Telemetry simulation loop failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    private RobotSummary AdvanceRobot(RobotSummary robot)
    {
        var now = DateTimeOffset.UtcNow;
        var battery = robot.Mode is RobotMode.Charging
            ? Math.Min(100, robot.BatteryPercent + 1)
            : Math.Max(10, robot.BatteryPercent - (robot.Mode is RobotMode.Navigating or RobotMode.Teleop ? 1 : 0));

        if (robot.Mode == RobotMode.Navigating &&
            !string.IsNullOrWhiteSpace(robot.TargetEntityId) &&
            store.GetMapEntity(robot.TargetEntityId) is { } target)
        {
            var deltaX = target.X - robot.Pose.X;
            var deltaY = target.Y - robot.Pose.Y;
            var distance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));

            if (distance < 1.5)
            {
                return robot with
                {
                    Mode = RobotMode.Idle,
                    BatteryPercent = battery,
                    Pose = new Pose2D(target.X, target.Y, robot.Pose.HeadingDegrees),
                    CurrentTask = null,
                    TargetEntityId = null,
                    AlarmState = AlarmSeverity.None,
                    Connectivity = ConnectivityStatus.Online,
                    LastUpdated = now
                };
            }

            var step = 2.5;
            var nextX = robot.Pose.X + (deltaX / distance * step);
            var nextY = robot.Pose.Y + (deltaY / distance * step);
            var heading = Math.Atan2(deltaY, deltaX) * 180 / Math.PI;

            return robot with
            {
                BatteryPercent = battery,
                Pose = new Pose2D(nextX, nextY, heading),
                Connectivity = ConnectivityStatus.Online,
                LastUpdated = now
            };
        }

        return robot with
        {
            BatteryPercent = battery,
            Connectivity = robot.RobotId == "AGV-03" && _tick % 4 == 0 ? ConnectivityStatus.Degraded : ConnectivityStatus.Online,
            LastUpdated = now
        };
    }
}
