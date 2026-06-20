using System.Net.Http.Json;
using System.Text.Json;
using NewAGV.Contracts;

namespace NewAGV.Web.Services;

public sealed class AgvApiClient(HttpClient httpClient)
{
    public async Task<IReadOnlyList<RobotSummary>> GetRobotsAsync(CancellationToken cancellationToken = default)
        => await httpClient.GetFromJsonAsync<List<RobotSummary>>("api/fleet/robots", cancellationToken) ?? [];

    public async Task<SiteHealth> GetHealthAsync(CancellationToken cancellationToken = default)
        => await httpClient.GetFromJsonAsync<SiteHealth>("api/fleet/health", cancellationToken)
           ?? new SiteHealth(false, false, ConnectivityStatus.Offline, "API unavailable.", 0, DateTimeOffset.UtcNow);

    public async Task<RobotTelemetryDetail?> GetRobotDetailAsync(string robotId, CancellationToken cancellationToken = default)
        => await httpClient.GetFromJsonAsync<RobotTelemetryDetail>($"api/fleet/robots/{robotId}/detail", cancellationToken);

    public async Task<GatewayHealth> GetGatewayHealthAsync(CancellationToken cancellationToken = default)
        => await httpClient.GetFromJsonAsync<GatewayHealth>("api/integration/gateway", cancellationToken)
           ?? new GatewayHealth(string.Empty, string.Empty, 0, ConnectivityStatus.Offline, "Gateway unavailable.", null, null, DateTimeOffset.UtcNow);

    public async Task<IReadOnlyList<MapEntity>> GetMapEntitiesAsync(CancellationToken cancellationToken = default)
        => await httpClient.GetFromJsonAsync<List<MapEntity>>("api/map/entities", cancellationToken) ?? [];

    public async Task<IReadOnlyList<ControlPolicy>> GetPoliciesAsync(CancellationToken cancellationToken = default)
        => await httpClient.GetFromJsonAsync<List<ControlPolicy>>("api/commands/policies", cancellationToken) ?? [];

    public async Task<IReadOnlyList<MissionAuditEntry>> GetAuditsAsync(CancellationToken cancellationToken = default)
        => await httpClient.GetFromJsonAsync<List<MissionAuditEntry>>("api/audit", cancellationToken) ?? [];

    public async Task<IReadOnlyList<SeerTaskChainSummary>> GetTaskChainsAsync(CancellationToken cancellationToken = default)
        => await httpClient.GetFromJsonAsync<List<SeerTaskChainSummary>>("api/taskchains", cancellationToken) ?? [];

    public async Task<SeerTaskChainStatus?> GetTaskChainAsync(string taskChainName, CancellationToken cancellationToken = default)
        => await httpClient.GetFromJsonAsync<SeerTaskChainStatus>($"api/taskchains/{Uri.EscapeDataString(taskChainName)}", cancellationToken);

    public Task<TaskChainRunSnapshot?> GetActiveTaskChainRunAsync(CancellationToken cancellationToken = default)
        => GetActiveTaskChainRunAsync(null, cancellationToken);

    public async Task<TaskChainRunSnapshot?> GetActiveTaskChainRunAsync(string? robotId, CancellationToken cancellationToken = default)
    {
        var uri = string.IsNullOrWhiteSpace(robotId)
            ? "api/taskchains/active-run"
            : $"api/taskchains/active-run?robotId={Uri.EscapeDataString(robotId)}";
        using var response = await httpClient.GetAsync(uri, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            return null;
        }

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        return JsonSerializer.Deserialize<TaskChainRunSnapshot>(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    public async Task<IReadOnlyList<TaskChainRunSnapshot>> GetTaskChainHistoryAsync(string? robotId, CancellationToken cancellationToken = default)
    {
        var uri = string.IsNullOrWhiteSpace(robotId)
            ? "api/taskchains/history"
            : $"api/taskchains/history?robotId={Uri.EscapeDataString(robotId)}";
        return await httpClient.GetFromJsonAsync<List<TaskChainRunSnapshot>>(uri, cancellationToken) ?? [];
    }

    public async Task<MissionCommandResult> DispatchAsync(MissionCommandRequest request, UserRole role, CancellationToken cancellationToken = default)
    {
        using var message = CreateRequest(HttpMethod.Post, "api/commands/dispatch", role);
        message.Content = JsonContent.Create(request);
        using var response = await httpClient.SendAsync(message, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<MissionCommandResult>(cancellationToken: cancellationToken);

        return result ?? new MissionCommandResult(
            Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            request.RobotId,
            request.CommandType,
            MissionCommandStatus.Rejected,
            "Backend did not return a response.",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    public async Task<MissionCommandResult> RelocateAsync(SeerRelocationRequest request, UserRole role, CancellationToken cancellationToken = default)
    {
        using var message = CreateRequest(HttpMethod.Post, "api/commands/relocate", role);
        message.Content = JsonContent.Create(request);
        using var response = await httpClient.SendAsync(message, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<MissionCommandResult>(cancellationToken: cancellationToken);

        return result ?? new MissionCommandResult(
            Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            string.Empty,
            MissionCommandType.Pause,
            MissionCommandStatus.Rejected,
            "Backend did not return a response.",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    public async Task<MissionCommandResult> TeleopDriveAsync(TeleopRequest request, UserRole role, CancellationToken cancellationToken = default)
    {
        using var message = CreateRequest(HttpMethod.Post, "api/commands/teleop", role);
        message.Content = JsonContent.Create(request);
        using var response = await httpClient.SendAsync(message, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<MissionCommandResult>(cancellationToken: cancellationToken);

        return result ?? new MissionCommandResult(
            Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            string.Empty,
            MissionCommandType.Teleop,
            MissionCommandStatus.Rejected,
            "Backend did not return a response.",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    public async Task<TaskChainRunResult> ExecuteTaskChainAsync(TaskChainRunRequest request, UserRole role, CancellationToken cancellationToken = default)
    {
        using var message = CreateRequest(HttpMethod.Post, "api/taskchains/execute", role);
        message.Content = JsonContent.Create(request);
        using var response = await httpClient.SendAsync(message, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<TaskChainRunResult>(cancellationToken: cancellationToken);

        return result ?? new TaskChainRunResult(
            Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            request.RobotId,
            request.TaskChainName,
            TaskChainRunStatus.Rejected,
            "Backend did not return a task chain response.",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null);
    }

    public Task<MissionCommandResult> PauseTaskChainAsync(string robotId, UserRole role, CancellationToken cancellationToken = default)
        => SendTaskChainControlAsync("api/taskchains/pause", MissionCommandType.Pause, new TaskChainControlRequest { RobotId = robotId }, role, cancellationToken);

    public Task<MissionCommandResult> ResumeTaskChainAsync(string robotId, UserRole role, CancellationToken cancellationToken = default)
        => SendTaskChainControlAsync("api/taskchains/resume", MissionCommandType.Resume, new TaskChainControlRequest { RobotId = robotId }, role, cancellationToken);

    public Task<MissionCommandResult> CancelTaskChainAsync(string robotId, UserRole role, CancellationToken cancellationToken = default)
        => SendTaskChainControlAsync("api/taskchains/cancel", MissionCommandType.Cancel, new TaskChainControlRequest { RobotId = robotId }, role, cancellationToken);

    public async Task<MapEntity> UpsertMapEntityAsync(MapEntity entity, UserRole role, CancellationToken cancellationToken = default)
    {
        using var message = CreateRequest(HttpMethod.Post, "api/map/entities", role);
        message.Content = JsonContent.Create(entity);
        using var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MapEntity>(cancellationToken: cancellationToken))!;
    }

    public async Task DeleteMapEntityAsync(string entityId, UserRole role, CancellationToken cancellationToken = default)
    {
        using var message = CreateRequest(HttpMethod.Delete, $"api/map/entities/{entityId}", role);
        using var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<MissionCommandResult> SendTaskChainControlAsync(
        string uri,
        MissionCommandType commandType,
        TaskChainControlRequest request,
        UserRole role,
        CancellationToken cancellationToken)
    {
        using var message = CreateRequest(HttpMethod.Post, uri, role);
        message.Content = JsonContent.Create(request);
        using var response = await httpClient.SendAsync(message, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<MissionCommandResult>(cancellationToken: cancellationToken);

        return result ?? new MissionCommandResult(
            Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            request.RobotId,
            commandType,
            MissionCommandStatus.Rejected,
            "Backend did not return a control response.",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string uri, UserRole role)
    {
        var message = new HttpRequestMessage(method, uri);
        message.Headers.Add("X-Demo-Role", role.ToString());
        return message;
    }
}
