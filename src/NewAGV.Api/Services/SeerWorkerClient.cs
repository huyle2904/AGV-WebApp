using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using NewAGV.Contracts;

namespace NewAGV.Api.Services;

public sealed class SeerWorkerClient(HttpClient httpClient, IOptions<IntegrationOptions> options)
{
    public async Task<MissionCommandResult> DispatchAsync(
        MissionCommandRequest request,
        UserRole role,
        CancellationToken cancellationToken)
    {
        var baseUrl = options.Value.WorkerBaseUrl.TrimEnd('/');
        using var response = await httpClient.PostAsJsonAsync(
            $"{baseUrl}/internal/commands/dispatch",
            new WorkerMissionCommandRequest(request, role),
            cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<MissionCommandResult>(cancellationToken: cancellationToken);
        if (result is not null)
        {
            return result;
        }

        return new MissionCommandResult(
            Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            request.RobotId,
            request.CommandType,
            MissionCommandStatus.Rejected,
            $"Worker returned HTTP {(int)response.StatusCode} without a command payload.",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    public async Task<MissionCommandResult> RelocateAsync(
        SeerRelocationRequest request,
        CancellationToken cancellationToken)
    {
        var baseUrl = options.Value.WorkerBaseUrl.TrimEnd('/');
        using var response = await httpClient.PostAsJsonAsync(
            $"{baseUrl}/internal/commands/relocate",
            request,
            cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<MissionCommandResult>(cancellationToken: cancellationToken);
        if (result is not null)
        {
            return result;
        }

        return new MissionCommandResult(
            Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            string.Empty,
            MissionCommandType.Pause,
            MissionCommandStatus.Rejected,
            $"Worker returned HTTP {(int)response.StatusCode} without a relocate payload.",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    public async Task<MissionCommandResult> TeleopDriveAsync(
        TeleopRequest request,
        CancellationToken cancellationToken)
    {
        var baseUrl = options.Value.WorkerBaseUrl.TrimEnd('/');
        using var response = await httpClient.PostAsJsonAsync(
            $"{baseUrl}/internal/commands/teleop",
            request,
            cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<MissionCommandResult>(cancellationToken: cancellationToken);
        if (result is not null)
        {
            return result;
        }

        return new MissionCommandResult(
            Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            string.Empty,
            MissionCommandType.Teleop,
            MissionCommandStatus.Rejected,
            $"Worker returned HTTP {(int)response.StatusCode} without a teleop payload.",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }
}
