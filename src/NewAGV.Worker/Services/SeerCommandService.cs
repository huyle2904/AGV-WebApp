using Microsoft.Extensions.Options;
using NewAGV.Contracts;

namespace NewAGV.Worker.Services;

public sealed class SeerCommandService(SeerTcpClient tcpClient, IOptions<SeerRobotOptions> options)
{
    public async Task<MissionCommandResult> DispatchAsync(
        MissionCommandRequest request,
        UserRole requestedByRole,
        CancellationToken cancellationToken)
    {
        var requestedAt = DateTimeOffset.UtcNow;
        var commandId = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

        try
        {
            var response = request.CommandType switch
            {
                MissionCommandType.GoToStation => await SendGoToStationAsync(request, cancellationToken),
                MissionCommandType.ReturnToHome => await SendGoToStationAsync(request with { TargetEntityId = options.Value.HomeStationId }, cancellationToken),
                MissionCommandType.Pause => await tcpClient.SendAsync(options.Value.NavigationPort, 3001, null, cancellationToken),
                MissionCommandType.Resume => await tcpClient.SendAsync(options.Value.NavigationPort, 3002, null, cancellationToken),
                MissionCommandType.Cancel => await tcpClient.SendAsync(options.Value.NavigationPort, 3003, null, cancellationToken),
                MissionCommandType.Teleop => throw new InvalidOperationException("Teleop is disabled for phase 1."),
                _ => throw new InvalidOperationException($"Unsupported command type {request.CommandType}.")
            };

            return BuildResult(commandId, request, response.IntValue("ret_code") ?? 0, response.StringValue("err_msg"), requestedAt);
        }
        catch (Exception exception)
        {
            return new MissionCommandResult(
                commandId,
                request.RobotId,
                request.CommandType,
                MissionCommandStatus.Rejected,
                exception.Message,
                requestedAt,
                DateTimeOffset.UtcNow);
        }
    }

    public async Task<MissionCommandResult> RelocateAsync(SeerRelocationRequest request, CancellationToken cancellationToken)
    {
        var requestedAt = DateTimeOffset.UtcNow;
        var commandRequest = new MissionCommandRequest(
            options.Value.RobotIdFallback,
            MissionCommandType.GoToStation,
            "SELF_POSITION",
            null,
            null,
            true);

        try
        {
            var response = await tcpClient.SendAsync(
                options.Value.ControlPort,
                2002,
                new Dictionary<string, object?>
                {
                    ["x"] = request.X,
                    ["y"] = request.Y,
                    ["angle"] = request.Angle
                },
                cancellationToken);

            return BuildResult(
                Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
                commandRequest,
                response.IntValue("ret_code") ?? 0,
                response.StringValue("err_msg"),
                requestedAt);
        }
        catch (Exception exception)
        {
            return new MissionCommandResult(
                Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
                options.Value.RobotIdFallback,
                MissionCommandType.GoToStation,
                MissionCommandStatus.Rejected,
                $"Relocation failed: {exception.Message}",
                requestedAt,
                DateTimeOffset.UtcNow);
        }
    }

    private Task<System.Text.Json.Nodes.JsonObject> SendGoToStationAsync(
        MissionCommandRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TargetEntityId))
        {
            throw new InvalidOperationException("Target station is required.");
        }

        return tcpClient.SendAsync(
            options.Value.NavigationPort,
            3051,
            new Dictionary<string, object?>
            {
                ["source_id"] = "SELF_POSITION",
                ["id"] = request.TargetEntityId,
                ["task_id"] = Guid.NewGuid().ToString("N")
            },
            cancellationToken);
    }

    private static MissionCommandResult BuildResult(
        string commandId,
        MissionCommandRequest request,
        int retCode,
        string? errorMessage,
        DateTimeOffset requestedAt)
    {
        var accepted = retCode == 0;
        return new MissionCommandResult(
            commandId,
            request.RobotId,
            request.CommandType,
            accepted ? MissionCommandStatus.Accepted : MissionCommandStatus.Rejected,
            accepted
                ? $"{request.CommandType} accepted by SEER AGV."
                : $"{request.CommandType} rejected by SEER AGV. ret_code={retCode}. {errorMessage}",
            requestedAt,
            DateTimeOffset.UtcNow);
    }
}
