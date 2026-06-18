using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace NewAGV.Worker.Services;

public sealed class SeerTcpClient(IOptions<SeerRobotOptions> options, ILogger<SeerTcpClient> logger)
{
    private int _sequence;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<JsonObject> SendAsync(
        int port,
        ushort apiNumber,
        object? payload,
        CancellationToken cancellationToken)
    {
        var robotOptions = options.Value;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(robotOptions.RequestTimeoutSeconds, 1, 30)));

        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(robotOptions.Host, port, timeoutCts.Token);
        await using var stream = tcpClient.GetStream();

        var sequence = unchecked((ushort)Interlocked.Increment(ref _sequence));
        var payloadBytes = payload is null
            ? []
            : JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        var requestHeader = BuildHeader(robotOptions.ProtocolVersion, sequence, payloadBytes.Length, apiNumber);

        await stream.WriteAsync(requestHeader, timeoutCts.Token);
        if (payloadBytes.Length > 0)
        {
            await stream.WriteAsync(payloadBytes, timeoutCts.Token);
        }

        var responseHeader = await ReadExactAsync(stream, 16, timeoutCts.Token);
        var header = ParseHeader(responseHeader);
        var expectedType = apiNumber + 10000;
        if (header.Type != expectedType)
        {
            logger.LogWarning(
                "Unexpected SEER response type. Request={RequestType}, Response={ResponseType}, Expected={ExpectedType}",
                apiNumber,
                header.Type,
                expectedType);
        }

        if (header.Length == 0)
        {
            return [];
        }

        var responseBytes = await ReadExactAsync(stream, checked((int)header.Length), timeoutCts.Token);
        var node = JsonNode.Parse(responseBytes);
        return node as JsonObject ?? [];
    }

    private static byte[] BuildHeader(byte version, ushort sequence, int payloadLength, ushort apiNumber)
    {
        var header = new byte[16];
        header[0] = 0x5A;
        header[1] = version;
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(2, 2), sequence);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4, 4), checked((uint)payloadLength));
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(8, 2), apiNumber);
        return header;
    }

    private static SeerHeader ParseHeader(byte[] bytes)
    {
        if (bytes.Length != 16 || bytes[0] != 0x5A)
        {
            throw new InvalidDataException("Invalid SEER response header.");
        }

        return new SeerHeader(
            bytes[1],
            BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(2, 2)),
            BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(4, 4)),
            BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(8, 2)));
    }

    private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int length, CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        var offset = 0;

        while (offset < length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken);
            if (read == 0)
            {
                throw new IOException("SEER TCP stream ended before the response was complete.");
            }

            offset += read;
        }

        return buffer;
    }

    private sealed record SeerHeader(byte Version, ushort Sequence, uint Length, ushort Type);
}
