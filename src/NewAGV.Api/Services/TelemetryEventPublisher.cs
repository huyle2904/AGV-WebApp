using Microsoft.AspNetCore.SignalR;
using NewAGV.Api.Hubs;
using NewAGV.Contracts;

namespace NewAGV.Api.Services;

public sealed class TelemetryEventPublisher(IHubContext<TelemetryHub> hubContext)
{
    public const int CurrentSchemaVersion = 1;
    private long _nextSequence;

    public long CurrentSequence => Interlocked.Read(ref _nextSequence);

    public Task PublishAsync(RealtimeEvent telemetry, CancellationToken cancellationToken)
    {
        var sequence = Interlocked.Increment(ref _nextSequence);
        var envelope = telemetry with
        {
            Sequence = sequence,
            SchemaVersion = CurrentSchemaVersion
        };

        return hubContext.Clients.All.SendAsync("ReceiveTelemetry", envelope, cancellationToken);
    }

    public long SkipSequence(long count = 1)
    {
        if (count < 1)
        {
            throw new InvalidOperationException("Sequence skip count must be at least 1.");
        }

        return Interlocked.Add(ref _nextSequence, count);
    }
}
