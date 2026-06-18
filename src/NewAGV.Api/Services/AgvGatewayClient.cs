using System.Net.Sockets;
using Microsoft.Extensions.Options;
using NewAGV.Contracts;

namespace NewAGV.Api.Services;

public sealed class AgvGatewayClient(HttpClient httpClient, IOptions<IntegrationOptions> options)
{
    public async Task<GatewayHealth> GetHealthAsync(CancellationToken cancellationToken)
    {
        var baseUrl = options.Value.GatewayBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return BuildUnconfiguredHealth();
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return new GatewayHealth(
                baseUrl,
                string.Empty,
                0,
                ConnectivityStatus.Offline,
                "GatewayBaseUrl is not a valid HTTP URL.",
                null,
                null,
                DateTimeOffset.UtcNow);
        }

        var timeout = TimeSpan.FromSeconds(Math.Clamp(options.Value.GatewayHealthTimeoutSeconds, 1, 15));
        var port = uri.IsDefaultPort
            ? uri.Scheme == Uri.UriSchemeHttps ? 443 : 80
            : uri.Port;

        var socketStatus = await ProbeTcpAsync(uri.Host, port, timeout, cancellationToken);
        if (!socketStatus.Connected)
        {
            return new GatewayHealth(
                uri.GetLeftPart(UriPartial.Authority),
                uri.Host,
                port,
                ConnectivityStatus.Offline,
                socketStatus.Message,
                null,
                null,
                DateTimeOffset.UtcNow);
        }

        return await ProbeHttpAsync(uri, port, timeout, cancellationToken);
    }

    private async Task<GatewayHealth> ProbeHttpAsync(Uri uri, int port, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri.GetLeftPart(UriPartial.Authority));
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCts.Token);

            var serverHeader = response.Headers.Server.ToString();
            var status = response.IsSuccessStatusCode ? ConnectivityStatus.Online : ConnectivityStatus.Degraded;
            var message = response.IsSuccessStatusCode
                ? "Gateway responded to HTTP probe."
                : $"Gateway port is open, but HTTP returned {(int)response.StatusCode}.";

            return new GatewayHealth(
                uri.GetLeftPart(UriPartial.Authority),
                uri.Host,
                port,
                status,
                message,
                (int)response.StatusCode,
                string.IsNullOrWhiteSpace(serverHeader) ? null : serverHeader,
                DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new GatewayHealth(
                uri.GetLeftPart(UriPartial.Authority),
                uri.Host,
                port,
                ConnectivityStatus.Degraded,
                "Gateway TCP port is open, but HTTP probe timed out.",
                null,
                null,
                DateTimeOffset.UtcNow);
        }
        catch (HttpRequestException exception)
        {
            return new GatewayHealth(
                uri.GetLeftPart(UriPartial.Authority),
                uri.Host,
                port,
                ConnectivityStatus.Degraded,
                $"Gateway TCP port is open, but HTTP probe failed: {exception.Message}",
                null,
                null,
                DateTimeOffset.UtcNow);
        }
    }

    private static async Task<(bool Connected, string Message)> ProbeTcpAsync(
        string host,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var tcpClient = new TcpClient();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await tcpClient.ConnectAsync(host, port, timeoutCts.Token);
            return (true, "Gateway TCP port is open.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (false, $"Gateway TCP probe timed out after {timeout.TotalSeconds:0}s.");
        }
        catch (SocketException exception)
        {
            return (false, $"Gateway TCP probe failed: {exception.Message}");
        }
    }

    private static GatewayHealth BuildUnconfiguredHealth()
    {
        return new GatewayHealth(
            string.Empty,
            string.Empty,
            0,
            ConnectivityStatus.Offline,
            "GatewayBaseUrl is not configured.",
            null,
            null,
            DateTimeOffset.UtcNow);
    }
}
