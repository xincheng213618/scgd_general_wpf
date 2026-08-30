using ColorVision.Copilot.Mcp;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotMcpConnectionDiagnosticTests
{
    private const string BearerToken = "diagnostic-test-token";
    private static readonly Uri Endpoint = new("http://127.0.0.1:38473/mcp");

    [Fact]
    public async Task CompletesActualServerHandshakeBeforeStatusAndKeepsSessionsPerTest()
    {
        using var handler = new DiagnosticHandler();
        using var client = new HttpClient(handler);

        await CopilotMcpConnectionDiagnostic.TestAsync(client, Endpoint, " " + BearerToken + " ", CancellationToken.None);
        await CopilotMcpConnectionDiagnostic.TestAsync(client, Endpoint, BearerToken, CancellationToken.None);

        Assert.Equal(6, handler.Requests.Count);
        for (var index = 0; index < handler.Requests.Count; index += 3)
        {
            var initialize = handler.Requests[index];
            var notification = handler.Requests[index + 1];
            var status = handler.Requests[index + 2];
            Assert.False(initialize.Headers.ContainsKey(CopilotMcpRequestHandler.SessionHeaderName));
            using var initializeBody = JsonDocument.Parse(initialize.Body);
            Assert.Equal("initialize", initializeBody.RootElement.GetProperty("method").GetString());
            using var notificationBody = JsonDocument.Parse(notification.Body);
            Assert.Equal("notifications/initialized", notificationBody.RootElement.GetProperty("method").GetString());
            Assert.False(notificationBody.RootElement.TryGetProperty("id", out _));
            using var statusBody = JsonDocument.Parse(status.Body);
            Assert.Equal("tools/call", statusBody.RootElement.GetProperty("method").GetString());
            Assert.Equal("get_server_status", statusBody.RootElement.GetProperty("params").GetProperty("name").GetString());
            Assert.Equal(notification.Headers[CopilotMcpRequestHandler.SessionHeaderName], status.Headers[CopilotMcpRequestHandler.SessionHeaderName]);
            Assert.Equal(CopilotMcpRequestHandler.SupportedProtocolVersion, status.Headers[CopilotMcpRequestHandler.ProtocolVersionHeaderName]);
        }
        Assert.NotEqual(handler.Requests[1].Headers[CopilotMcpRequestHandler.SessionHeaderName], handler.Requests[4].Headers[CopilotMcpRequestHandler.SessionHeaderName]);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal("Bearer " + BearerToken, request.Headers["Authorization"]);
            Assert.Contains("application/json", request.Headers["Accept"], StringComparison.Ordinal);
            Assert.Contains("text/event-stream", request.Headers["Accept"], StringComparison.Ordinal);
        });
        Assert.False(client.DefaultRequestHeaders.Contains(CopilotMcpRequestHandler.SessionHeaderName));
        Assert.Null(client.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public async Task StopsAtAuthenticationFailureWithoutCallingTools()
    {
        using var handler = new DiagnosticHandler();
        using var client = new HttpClient(handler);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => CopilotMcpConnectionDiagnostic.TestAsync(client, Endpoint, "wrong-token", CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        Assert.Contains("initialize", error.Message, StringComparison.Ordinal);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("has space")]
    public async Task MissingOrInvalidSessionStopsBeforeInitializedNotification(string? sessionId)
    {
        using var handler = new DiagnosticHandler
        {
            AdjustResponse = (_, response) =>
            {
                response.Headers.Remove(CopilotMcpRequestHandler.SessionHeaderName);
                if (sessionId != null)
                    response.Headers.TryAddWithoutValidation(CopilotMcpRequestHandler.SessionHeaderName, sessionId);
            },
        };
        using var client = new HttpClient(handler);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => CopilotMcpConnectionDiagnostic.TestAsync(client, Endpoint, BearerToken, CancellationToken.None));

        Assert.Contains("session header", error.Message, StringComparison.Ordinal);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"protocolVersion\":\"unsupported\"}}", "protocol version")]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":9,\"result\":{}}", "invalid JSON-RPC")]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"error\":{\"message\":\"diagnostic-test-token secret-session\"}}", "JSON-RPC error")]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":null}", "no JSON-RPC result")]
    [InlineData("[]", "invalid JSON-RPC")]
    public async Task InvalidInitializeCannotReportConnectedOrEchoRemoteSecrets(string body, string expectedError)
    {
        using var handler = new DiagnosticHandler { AdjustResponse = (_, response) => ReplaceContent(response, body) };
        using var client = new HttpClient(handler);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => CopilotMcpConnectionDiagnostic.TestAsync(client, Endpoint, BearerToken, CancellationToken.None));

        Assert.Contains(expectedError, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(BearerToken, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-session", error.Message, StringComparison.Ordinal);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.OK)]
    public async Task NotificationMustBeAcknowledgedBeforeStatus(HttpStatusCode statusCode)
    {
        using var handler = new DiagnosticHandler
        {
            AdjustResponse = (index, response) =>
            {
                if (index == 2)
                    response.StatusCode = statusCode;
            },
        };
        using var client = new HttpClient(handler);

        var error = await Record.ExceptionAsync(() => CopilotMcpConnectionDiagnostic.TestAsync(client, Endpoint, BearerToken, CancellationToken.None));

        Assert.NotNull(error);
        Assert.Contains("notifications/initialized", error.Message, StringComparison.Ordinal);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Theory]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":2,\"result\":{\"isError\":true,\"content\":[]}}", "MCP error")]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":2,\"result\":{}}", "no status text")]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":2,\"result\":{\"content\":[]}}", "no status text")]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":2,\"error\":{\"message\":\"diagnostic-test-token secret-session\"}}", "JSON-RPC error")]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{}}", "invalid JSON-RPC")]
    public async Task InvalidStatusCannotReportConnected(string body, string expectedError)
    {
        using var handler = new DiagnosticHandler
        {
            AdjustResponse = (index, response) =>
            {
                if (index == 3)
                    ReplaceContent(response, body);
            },
        };
        using var client = new HttpClient(handler);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => CopilotMcpConnectionDiagnostic.TestAsync(client, Endpoint, BearerToken, CancellationToken.None));

        Assert.Contains(expectedError, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(BearerToken, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-session", error.Message, StringComparison.Ordinal);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task InitializeAndStatusResponsesRetainSizeLimit(int oversizedResponseIndex)
    {
        using var handler = new DiagnosticHandler
        {
            AdjustResponse = (index, response) =>
            {
                if (index == oversizedResponseIndex)
                    ReplaceContent(response, new string('x', CopilotMcpConnectionDiagnostic.MaximumResponseBytes + 1));
            },
        };
        using var client = new HttpClient(handler);

        await Assert.ThrowsAsync<CopilotHttpContentSizeLimitException>(() => CopilotMcpConnectionDiagnostic.TestAsync(client, Endpoint, BearerToken, CancellationToken.None));

        Assert.Equal(oversizedResponseIndex, handler.Requests.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task CancellationStopsBodyReadsAfterHeaders(int stalledResponseIndex)
    {
        using var stream = new StalledReadStream();
        using var handler = new DiagnosticHandler
        {
            AdjustResponse = (index, response) =>
            {
                if (index != stalledResponseIndex)
                    return;
                response.Content.Dispose();
                response.Content = new StreamContent(stream);
            },
        };
        using var client = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();

        var diagnostic = CopilotMcpConnectionDiagnostic.TestAsync(client, Endpoint, BearerToken, cancellation.Token);
        await stream.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => diagnostic.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(stalledResponseIndex, handler.Requests.Count);
    }

    private static void ReplaceContent(HttpResponseMessage response, string body)
    {
        response.Content.Dispose();
        response.Content = new StringContent(body, Encoding.UTF8, "application/json");
    }

    private sealed class DiagnosticHandler : HttpMessageHandler
    {
        private readonly CopilotMcpRequestHandler _server;
        public List<CopilotMcpHttpRequest> Requests { get; } = [];
        public Action<int, HttpResponseMessage>? AdjustResponse { get; init; }

        public DiagnosticHandler()
        {
            var settings = new CopilotMcpRuntimeSettings { Enabled = true, BearerToken = BearerToken };
            _server = new CopilotMcpRequestHandler(() => settings, new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
            {
                RuntimeSettingsProvider = () => settings,
                ServerRunningProvider = () => true,
                ServerStatusMessageProvider = () => "Diagnostic fixture running.",
                ActiveCopilotRunCountProvider = () => 0,
                QueuedCopilotRunCountProvider = () => 0,
            }));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var serverRequest = new CopilotMcpHttpRequest
            {
                Method = request.Method.Method,
                Path = request.RequestUri!.AbsolutePath,
                Headers = request.Headers.ToDictionary(header => header.Key, header => string.Join(", ", header.Value), StringComparer.OrdinalIgnoreCase),
                Body = await request.Content!.ReadAsStringAsync(cancellationToken),
                CallerSource = "tcp://127.0.0.1",
            };
            Requests.Add(serverRequest);
            var result = await _server.HandleAsync(serverRequest, cancellationToken);
            var response = new HttpResponseMessage((HttpStatusCode)result.StatusCode)
            {
                Content = new StringContent(result.Body, Encoding.UTF8, "application/json"),
            };
            foreach (var header in result.Headers)
                response.Headers.TryAddWithoutValidation(header.Key, header.Value);
            AdjustResponse?.Invoke(Requests.Count, response);
            return response;
        }
    }

    private sealed class StalledReadStream : MemoryStream
    {
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
    }
}
