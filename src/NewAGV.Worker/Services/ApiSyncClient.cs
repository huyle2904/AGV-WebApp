using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using NewAGV.Contracts;

namespace NewAGV.Worker.Services;

public sealed class ApiSyncClient(HttpClient httpClient, IOptions<SeerRobotOptions> options, ILogger<ApiSyncClient> logger)
{
    public Task PushRobotAsync(InternalRobotStateUpdate update, CancellationToken cancellationToken)
        => PostAsync("internal/sync/robot", update, cancellationToken);

    public Task PushMapAsync(InternalMapSnapshot snapshot, CancellationToken cancellationToken)
        => PostAsync("internal/sync/map", snapshot, cancellationToken);

    public Task PushHealthAsync(SiteHealth health, CancellationToken cancellationToken)
        => PostAsync("internal/sync/health", health, cancellationToken);

    public Task PushWorkflowAsync(InternalWorkflowRunUpdate update, CancellationToken cancellationToken)
        => PostAsync("internal/sync/workflow", update, cancellationToken);

    private async Task PostAsync<T>(string path, T payload, CancellationToken cancellationToken)
    {
        var url = $"{options.Value.ApiBaseUrl.TrimEnd('/')}/{path}";
        try
        {
            using var response = await httpClient.PostAsJsonAsync(url, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("API sync POST {Path} returned HTTP {StatusCode}.", path, (int)response.StatusCode);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "API sync POST {Path} failed.", path);
        }
    }
}
