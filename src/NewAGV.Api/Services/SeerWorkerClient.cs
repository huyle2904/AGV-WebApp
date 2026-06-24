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
        var result = await PostAsync<MissionCommandResult>(
            "internal/commands/dispatch",
            new WorkerMissionCommandRequest(request, role),
            cancellationToken);

        return result ?? new MissionCommandResult(
            Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            request.RobotId,
            request.CommandType,
            MissionCommandStatus.Rejected,
            "Worker returned no command payload.",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    public async Task<MissionCommandResult> RelocateAsync(
        SeerRelocationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await PostAsync<MissionCommandResult>("internal/commands/relocate", request, cancellationToken);
        return result ?? new MissionCommandResult(
            Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            string.Empty,
            MissionCommandType.Pause,
            MissionCommandStatus.Rejected,
            "Worker returned no relocate payload.",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    public async Task<MissionCommandResult> TeleopDriveAsync(
        TeleopRequest request,
        CancellationToken cancellationToken)
    {
        var result = await PostAsync<MissionCommandResult>("internal/commands/teleop", request, cancellationToken);
        return result ?? new MissionCommandResult(
            Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            string.Empty,
            MissionCommandType.Teleop,
            MissionCommandStatus.Rejected,
            "Worker returned no teleop payload.",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<SeerTaskChainSummary>> GetTaskChainsAsync(CancellationToken cancellationToken)
        => await GetAsync<List<SeerTaskChainSummary>>("internal/taskchains", cancellationToken) ?? [];

    public async Task<SeerTaskChainStatus?> GetTaskChainAsync(string taskChainName, bool withRobotStatus, CancellationToken cancellationToken)
        => await GetAsync<SeerTaskChainStatus>(
            $"internal/taskchains/{Uri.EscapeDataString(taskChainName)}?withRobotStatus={withRobotStatus.ToString().ToLowerInvariant()}",
            cancellationToken);

    public async Task<TaskChainRunResult> ExecuteTaskChainAsync(TaskChainRunRequest request, CancellationToken cancellationToken)
    {
        var result = await PostAsync<TaskChainRunResult>("internal/taskchains/execute", request, cancellationToken);
        return result ?? new TaskChainRunResult(
            Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            request.RobotId,
            request.TaskChainName,
            TaskChainRunStatus.Rejected,
            "Worker returned no task chain payload.",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null);
    }

    public async Task<IReadOnlyList<SeerTaskRuntimeStatus>> GetTaskRuntimeStatusesAsync(string? taskId, CancellationToken cancellationToken)
    {
        var path = string.IsNullOrWhiteSpace(taskId)
            ? "internal/task-runtime"
            : $"internal/task-runtime?taskId={Uri.EscapeDataString(taskId)}";

        return await GetAsync<List<SeerTaskRuntimeStatus>>(path, cancellationToken) ?? [];
    }

    public async Task<MissionCommandResult> PauseTaskChainAsync(CancellationToken cancellationToken)
        => await PostControlAsync("internal/taskchains/pause", MissionCommandType.Pause, cancellationToken);

    public async Task<MissionCommandResult> ResumeTaskChainAsync(CancellationToken cancellationToken)
        => await PostControlAsync("internal/taskchains/resume", MissionCommandType.Resume, cancellationToken);

    public async Task<MissionCommandResult> CancelTaskChainAsync(CancellationToken cancellationToken)
        => await PostControlAsync("internal/taskchains/cancel", MissionCommandType.Cancel, cancellationToken);

    private async Task<MissionCommandResult> PostControlAsync(string path, MissionCommandType commandType, CancellationToken cancellationToken)
    {
        var result = await PostAsync<MissionCommandResult>(path, new { }, cancellationToken);
        return result ?? new MissionCommandResult(
            Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            string.Empty,
            commandType,
            MissionCommandStatus.Rejected,
            "Worker returned no control payload.",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(BuildUrl(path), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }

    private async Task<T?> PostAsync<T>(string path, object payload, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(BuildUrl(path), payload, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }

    private string BuildUrl(string path)
        => $"{options.Value.WorkerBaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
}
