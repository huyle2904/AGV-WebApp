using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using NewAGV.Contracts;

namespace NewAGV.Web.Services;

public sealed class TelemetryClientService(IOptions<ApiOptions> apiOptions, ILogger<TelemetryClientService> logger) : IAsyncDisposable
{
    private HubConnection? _connection;
    private bool _started;

    public event Action<RealtimeEvent>? TelemetryReceived;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
        {
            return;
        }

        _connection = new HubConnectionBuilder()
            .WithUrl($"{apiOptions.Value.BaseUrl.TrimEnd('/')}/hubs/telemetry")
            .WithAutomaticReconnect()
            .Build();

        _connection.On<RealtimeEvent>("ReceiveTelemetry", telemetry => TelemetryReceived?.Invoke(telemetry));
        _connection.Reconnecting += error =>
        {
            logger.LogWarning(error, "Telemetry hub reconnecting.");
            return Task.CompletedTask;
        };

        _connection.Reconnected += connectionId =>
        {
            logger.LogInformation("Telemetry hub reconnected with connection id {ConnectionId}.", connectionId);
            return Task.CompletedTask;
        };

        await _connection.StartAsync(cancellationToken);
        _started = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
