using NewAGV.Contracts;

namespace NewAGV.Api.Services;

public sealed class CommandDispatcher(
    AgvPlantStore store,
    SeerWorkerClient workerClient,
    TelemetryEventPublisher telemetryPublisher,
    AuditLogService auditLogService,
    MapSnapshotService mapSnapshotService)
{
    public async Task<MissionCommandResult> DispatchAsync(MissionCommandRequest request, UserRole requestedByRole, CancellationToken cancellationToken)
    {
        var requestedAt = DateTimeOffset.UtcNow;
        var commandId = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        var robot = store.GetRobot(request.RobotId);

        if (robot is null)
        {
            return await RejectAsync(commandId, request, requestedByRole, requestedAt, "Robot was not found.", cancellationToken);
        }

        var policy = store.GetPolicies().First(policyItem => policyItem.CommandType == request.CommandType);
        if (!policy.Enabled || requestedByRole < policy.MinimumRole)
        {
            return await RejectAsync(commandId, request, requestedByRole, requestedAt, "Your role is not allowed to send this command.", cancellationToken);
        }

        if (policy.RequiresConfirmation && !request.Confirmed)
        {
            return await RejectAsync(commandId, request, requestedByRole, requestedAt, "This command requires explicit confirmation.", cancellationToken);
        }

        var safetyRejection = BuildSafetyRejection(robot, request);
        if (safetyRejection is not null)
        {
            return await RejectAsync(commandId, request, requestedByRole, requestedAt, safetyRejection, cancellationToken);
        }

        MissionCommandRequest workerRequest;
        try
        {
            workerRequest = request.CommandType switch
            {
                MissionCommandType.GoToStation => await ValidateRouteCommandAsync(request, cancellationToken),
                _ => request
            };
        }
        catch (InvalidOperationException exception)
        {
            return await RejectAsync(commandId, request, requestedByRole, requestedAt, exception.Message, cancellationToken);
        }

        MissionCommandResult result;
        try
        {
            result = await workerClient.DispatchAsync(workerRequest, requestedByRole, cancellationToken);
        }
        catch (Exception exception)
        {
            result = new MissionCommandResult(
                commandId,
                request.RobotId,
                request.CommandType,
                MissionCommandStatus.Rejected,
                $"Worker dispatch failed: {exception.Message}",
                requestedAt,
                DateTimeOffset.UtcNow);
        }

        await auditLogService.RecordAuditAsync(new MissionAuditEntry(
            Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            request.RobotId,
            request.CommandType,
            requestedByRole,
            result.Message,
            result.Status,
            result.CompletedAt),
            cancellationToken);

        await auditLogService.RecordCommandAttemptAsync(
            workerRequest,
            requestedByRole,
            result,
            "api.commands.dispatch",
            cancellationToken);

        await telemetryPublisher.PublishAsync(
            new RealtimeEvent("command.ack", DateTimeOffset.UtcNow, Command: result, Message: result.Message),
            cancellationToken);

        return result;
    }

    private async Task<MissionCommandRequest> ValidateRouteCommandAsync(MissionCommandRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TargetEntityId))
        {
            throw new InvalidOperationException("Target entity is required for GoToStation and ReturnToHome commands.");
        }

        var targetEntity = await mapSnapshotService.GetEntityAsync(request.TargetEntityId, cancellationToken);
        if (targetEntity is null || targetEntity.Type != MapEntityType.Station)
        {
            throw new InvalidOperationException("Target station is not available.");
        }

        return request with { TargetEntityId = targetEntity.EntityId };
    }

    private string? BuildSafetyRejection(RobotSummary robot, MissionCommandRequest request)
    {
        if (robot.Connectivity != ConnectivityStatus.Online)
        {
            return "Robot TCP link is not online.";
        }

        if (!IsMotionCommand(request.CommandType))
        {
            return null;
        }

        var detail = store.GetRobotDetail(request.RobotId);
        if (detail is null)
        {
            return "Live robot telemetry is not available yet.";
        }

        if (detail.Estop?.Emergency == true ||
            detail.Estop?.DriverEmergency == true ||
            detail.Estop?.SoftEmergency == true)
        {
            return "E-stop or driver emergency is active.";
        }

        if (detail.ControlOwner?.Locked == true)
        {
            var owner = detail.ControlOwner.NickName ?? detail.ControlOwner.Ip ?? "another controller";
            return $"Robot control is locked by {owner}.";
        }

        if (detail.Alarm is { FatalCount: > 0 } or { ErrorCount: > 0 })
        {
            return "Robot has active fatal/error alarms.";
        }

        if (detail.Localization?.ReadyForNavigation != true)
        {
            return $"Localization is not ready: {detail.Localization?.StatusLabel ?? "unknown"}.";
        }

        return null;
    }

    private static bool IsMotionCommand(MissionCommandType commandType)
        => commandType is MissionCommandType.GoToStation
            or MissionCommandType.ReturnToHome
            or MissionCommandType.Resume
            or MissionCommandType.Teleop;

    private async Task<MissionCommandResult> RejectAsync(
        string commandId,
        MissionCommandRequest request,
        UserRole requestedByRole,
        DateTimeOffset requestedAt,
        string message,
        CancellationToken cancellationToken)
    {
        var result = new MissionCommandResult(
            commandId,
            request.RobotId,
            request.CommandType,
            MissionCommandStatus.Rejected,
            message,
            requestedAt,
            DateTimeOffset.UtcNow);

        await auditLogService.RecordAuditAsync(new MissionAuditEntry(
            Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            request.RobotId,
            request.CommandType,
            requestedByRole,
            message,
            MissionCommandStatus.Rejected,
            result.CompletedAt),
            cancellationToken);

        await auditLogService.RecordCommandAttemptAsync(
            request,
            requestedByRole,
            result,
            "api.commands.dispatch",
            cancellationToken);

        await telemetryPublisher.PublishAsync(
            new RealtimeEvent("command.ack", DateTimeOffset.UtcNow, Command: result, Message: result.Message),
            cancellationToken);

        return result;
    }
}
