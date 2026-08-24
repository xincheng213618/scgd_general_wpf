using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotHttpTransportIsolationTests
{
    [Fact]
    public async Task ProviderClientsDoNotShareResponseCookiesAcrossProfiles()
    {
        await AssertCookieIsNotSharedAsync(
            () => CopilotProviderHttpTransport.CreateClient("cookie-test-profile-a"),
            () => CopilotProviderHttpTransport.CreateClient("cookie-test-profile-b"));
    }

    [Fact]
    public async Task McpClientsDoNotShareResponseCookiesAcrossServers()
    {
        await AssertCookieIsNotSharedAsync(
            () => CopilotMcpHttpTransport.CreateClient(TimeSpan.FromSeconds(5)),
            () => CopilotMcpHttpTransport.CreateClient(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task BackendSyncClientsDoNotShareResponseCookiesAcrossEndpoints()
    {
        await AssertCookieIsNotSharedAsync(CreateBackendSyncClient, CreateBackendSyncClient);
    }

    [Theory]
    [InlineData(HttpStatusCode.TemporaryRedirect)]
    [InlineData(HttpStatusCode.PermanentRedirect)]
    public async Task ProviderClientsDoNotFollowRedirects(HttpStatusCode redirectStatus)
    {
        await AssertRedirectIsNotFollowedAsync(
            () => CopilotProviderHttpTransport.CreateClient("redirect-test-profile"),
            redirectStatus);
    }

    [Theory]
    [InlineData(HttpStatusCode.TemporaryRedirect)]
    [InlineData(HttpStatusCode.PermanentRedirect)]
    public async Task McpClientsDoNotFollowRedirects(HttpStatusCode redirectStatus)
    {
        await AssertRedirectIsNotFollowedAsync(
            () => CopilotMcpHttpTransport.CreateClient(TimeSpan.FromSeconds(5)),
            redirectStatus);
    }

    [Theory]
    [InlineData(HttpStatusCode.TemporaryRedirect)]
    [InlineData(HttpStatusCode.PermanentRedirect)]
    public async Task BackendSyncClientsDoNotFollowRedirects(HttpStatusCode redirectStatus)
    {
        await AssertRedirectIsNotFollowedAsync(CreateBackendSyncClient, redirectStatus);
    }

    private static async Task AssertCookieIsNotSharedAsync(
        Func<HttpClient> createSourceClient,
        Func<HttpClient> createDestinationClient)
    {
        using var sourceListener = new TcpListener(IPAddress.Loopback, 0);
        using var destinationListener = new TcpListener(IPAddress.Loopback, 0);
        sourceListener.Start();
        destinationListener.Start();

        var cookieName = "copilot_isolation_" + Guid.NewGuid().ToString("N");
        var cookieValue = Guid.NewGuid().ToString("N");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var sourceExchange = ExchangeOnceAsync(
            sourceListener,
            $"Set-Cookie: {cookieName}={cookieValue}; Path=/; HttpOnly\r\n",
            timeout.Token);
        await SendRequestAsync(createSourceClient, GetListenerUri(sourceListener), timeout.Token);
        await sourceExchange;

        var destinationExchange = ExchangeOnceAsync(destinationListener, string.Empty, timeout.Token);
        await SendRequestAsync(createDestinationClient, GetListenerUri(destinationListener), timeout.Token);
        var destinationHeaders = await destinationExchange;

        var cookieHeaders = destinationHeaders.Headers
            .Where(line => line.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            $"{cookieName}={cookieValue}",
            string.Join("; ", cookieHeaders),
            StringComparison.Ordinal);
    }

    private static async Task AssertRedirectIsNotFollowedAsync(
        Func<HttpClient> createClient,
        HttpStatusCode redirectStatus)
    {
        using var sourceListener = new TcpListener(IPAddress.Loopback, 0);
        using var destinationListener = new TcpListener(IPAddress.Loopback, 0);
        sourceListener.Start();
        destinationListener.Start();

        var authorization = "redirect-bearer-" + Guid.NewGuid().ToString("N");
        var apiKey = "redirect-api-key-" + Guid.NewGuid().ToString("N");
        var mcpSessionId = "redirect-mcp-session-" + Guid.NewGuid().ToString("N");
        var backendSignature = "redirect-signature-" + Guid.NewGuid().ToString("N");
        var prompt = "redirect-prompt-" + Guid.NewGuid().ToString("N");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var stopDestinationProbe = new CancellationTokenSource();
        using var destinationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            timeout.Token,
            stopDestinationProbe.Token);

        var destinationExchange = ExchangeOnceAsync(
            destinationListener,
            string.Empty,
            destinationCancellation.Token);
        var sourceExchange = ExchangeOnceAsync(
            sourceListener,
            $"Location: {GetListenerUri(destinationListener)}\r\n",
            timeout.Token,
            $"HTTP/1.1 {(int)redirectStatus} Redirect");

        try
        {
            using var client = createClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, GetListenerUri(sourceListener));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authorization);
            request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
            request.Headers.TryAddWithoutValidation("Mcp-Session-Id", mcpSessionId);
            request.Headers.TryAddWithoutValidation("X-ColorVision-Signature", backendSignature);
            request.Content = new ByteArrayContent(Encoding.ASCII.GetBytes(prompt));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            var sourceRequest = await sourceExchange;

            Assert.Equal(redirectStatus, response.StatusCode);
            Assert.Equal("Bearer " + authorization, GetHeader(sourceRequest.Headers, "Authorization"));
            Assert.Equal(apiKey, GetHeader(sourceRequest.Headers, "x-api-key"));
            Assert.Equal(mcpSessionId, GetHeader(sourceRequest.Headers, "Mcp-Session-Id"));
            Assert.Equal(backendSignature, GetHeader(sourceRequest.Headers, "X-ColorVision-Signature"));
            Assert.Equal(prompt, sourceRequest.Body);
            var destinationRequest = await StopDestinationProbeAsync(
                destinationExchange,
                stopDestinationProbe,
                timeout);
            Assert.Null(destinationRequest);
        }
        finally
        {
            stopDestinationProbe.Cancel();
            try
            {
                await destinationExchange;
            }
            catch (OperationCanceledException) when (destinationCancellation.IsCancellationRequested)
            {
            }
        }
    }

    private static async Task<CapturedHttpRequest?> StopDestinationProbeAsync(
        Task<CapturedHttpRequest> destinationExchange,
        CancellationTokenSource stopDestinationProbe,
        CancellationTokenSource timeout)
    {
        stopDestinationProbe.Cancel();
        try
        {
            return await destinationExchange;
        }
        catch (OperationCanceledException) when (
            stopDestinationProbe.IsCancellationRequested
            && !timeout.IsCancellationRequested)
        {
            return null;
        }
    }

    private static async Task SendRequestAsync(
        Func<HttpClient> createClient,
        Uri endpoint,
        CancellationToken cancellationToken)
    {
        using var client = createClient();
        using var response = await client.GetAsync(endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static HttpClient CreateBackendSyncClient()
    {
        var loopbackHandlerField = typeof(CopilotBackendSyncClient).GetField(
            "LoopbackHandler",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(loopbackHandlerField);
        var handler = Assert.IsAssignableFrom<HttpMessageHandler>(loopbackHandlerField.GetValue(null));
        return new HttpClient(handler, disposeHandler: false)
        {
            Timeout = TimeSpan.FromSeconds(5),
        };
    }

    private static Uri GetListenerUri(TcpListener listener)
    {
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        return new Uri($"http://127.0.0.1:{endpoint.Port}/cookie-isolation");
    }

    private static string? GetHeader(IReadOnlyList<string> headers, string name)
    {
        var prefix = name + ":";
        var header = headers.FirstOrDefault(line => line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return header?[prefix.Length..].Trim();
    }

    private static async Task<CapturedHttpRequest> ExchangeOnceAsync(
        TcpListener listener,
        string additionalResponseHeaders,
        CancellationToken cancellationToken,
        string statusLine = "HTTP/1.1 200 OK")
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
        var requestHeaders = new List<string>();
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                throw new EndOfStreamException("The loopback client closed before completing its HTTP headers.");
            if (line.Length == 0)
                break;
            requestHeaders.Add(line);
        }

        var contentLengthText = GetHeader(requestHeaders, "Content-Length");
        var contentLength = string.IsNullOrWhiteSpace(contentLengthText)
            ? 0
            : int.Parse(contentLengthText, System.Globalization.CultureInfo.InvariantCulture);
        if (contentLength is < 0 or > 64 * 1024)
            throw new InvalidDataException("The loopback request body length is outside the test limit.");
        var requestBody = new char[contentLength];
        var totalRead = 0;
        while (totalRead < requestBody.Length)
        {
            var read = await reader.ReadAsync(requestBody.AsMemory(totalRead), cancellationToken);
            if (read == 0)
                throw new EndOfStreamException("The loopback client closed before completing its HTTP body.");
            totalRead += read;
        }

        var responseBytes = Encoding.ASCII.GetBytes(
            statusLine + "\r\n"
            + "Content-Length: 0\r\n"
            + "Connection: close\r\n"
            + additionalResponseHeaders
            + "\r\n");
        await stream.WriteAsync(responseBytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        return new CapturedHttpRequest(requestHeaders, new string(requestBody));
    }

    private sealed record CapturedHttpRequest(IReadOnlyList<string> Headers, string Body);
}
