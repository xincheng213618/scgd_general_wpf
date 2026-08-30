using ColorVision.Copilot.Mcp;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotLocalMcpDiagnosticLifecycleTests : IDisposable
{
    private const string BearerToken = "local-mcp-lifecycle-test-token";
    private readonly string _rootDirectory = Path.Combine(Path.GetTempPath(), "ColorVisionLocalMcpDiagnostic-" + Guid.NewGuid().ToString("N"));

    public CopilotLocalMcpDiagnosticLifecycleTests() => Directory.CreateDirectory(_rootDirectory);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CurrentDraftReceivesCompletedHandshakeResult(bool succeeds)
    {
        using var handler = new DeferredDiagnosticHandler(succeeds);
        using var client = new HttpClient(handler);
        using var viewModel = CreateViewModel(client);
        var context = new PausedSynchronizationContext();
        var diagnostic = StartDiagnostic(viewModel, context);
        await handler.StatusRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        handler.Release.TrySetResult();
        await context.CallbackPosted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        context.RunPending();
        await diagnostic.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(viewModel.IsTestingMcpConnection);
        Assert.True(viewModel.CanTestMcpConnection);
        Assert.False(viewModel.HasAppliedChanges);
        Assert.False(viewModel.HasUnsavedSettings);
        Assert.StartsWith(succeeds ? "Connected." : "Connection failed:", viewModel.McpConnectionTestText, StringComparison.Ordinal);
        Assert.Equal(succeeds ? "MCP connection test succeeded." : viewModel.McpConnectionTestText, viewModel.SettingsStatusText);
        Assert.Equal(3, handler.Requests.Count);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal(CopilotConfig.DefaultMcpPort, request.Port);
            Assert.Equal("Bearer " + BearerToken, request.Authorization);
        });
        Assert.Empty(Directory.EnumerateFiles(_rootDirectory));
    }

    [Theory]
    [InlineData("port", true)]
    [InlineData("port", false)]
    [InlineData("port-back", true)]
    [InlineData("port-back", false)]
    [InlineData("invalid-port", true)]
    [InlineData("invalid-port", false)]
    [InlineData("invalid-port-back", true)]
    [InlineData("invalid-port-back", false)]
    [InlineData("equivalent-port-text", true)]
    [InlineData("equivalent-port-text", false)]
    [InlineData("token", true)]
    [InlineData("token", false)]
    [InlineData("token-back", true)]
    [InlineData("token-back", false)]
    [InlineData("enabled", true)]
    [InlineData("enabled", false)]
    [InlineData("enabled-back", true)]
    [InlineData("enabled-back", false)]
    [InlineData("close", true)]
    [InlineData("close", false)]
    [InlineData("new-notice", true)]
    [InlineData("new-notice", false)]
    public async Task ChangedOrClosedDraftCannotPublishAlreadyCompletedResult(string change, bool succeeds)
    {
        using var handler = new DeferredDiagnosticHandler(succeeds);
        using var client = new HttpClient(handler);
        using var viewModel = CreateViewModel(client);
        var context = new PausedSynchronizationContext();
        var diagnostic = StartDiagnostic(viewModel, context);
        await handler.StatusRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        handler.Release.TrySetResult();
        await context.CallbackPosted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(diagnostic.IsCompleted);

        ChangeDraft(viewModel, change);
        var expectedNotice = viewModel.SettingsStatusText;
        var expectedStatus = viewModel.McpStatusText;
        var expectedTestText = viewModel.McpConnectionTestText;
        var expectedDiagnostics = viewModel.McpDiagnosticsSummaryText;
        if (change is not "close" and not "new-notice")
        {
            Assert.DoesNotContain("Testing", expectedNotice, StringComparison.Ordinal);
            Assert.Empty(expectedTestText);
        }

        context.RunPending();
        await diagnostic.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(viewModel.IsTestingMcpConnection);
        Assert.False(viewModel.HasAppliedChanges);
        Assert.Equal(expectedNotice, viewModel.SettingsStatusText);
        if (change == "new-notice")
        {
            Assert.Contains("Backend sync settings changed", viewModel.SettingsStatusText, StringComparison.Ordinal);
            Assert.StartsWith(succeeds ? "Connected." : "Connection failed:", viewModel.McpConnectionTestText, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(expectedStatus, viewModel.McpStatusText);
            Assert.Equal(expectedTestText, viewModel.McpConnectionTestText);
            Assert.Equal(expectedDiagnostics, viewModel.McpDiagnosticsSummaryText);
        }
        Assert.Equal(3, handler.Requests.Count);
        Assert.Empty(Directory.EnumerateFiles(_rootDirectory));
    }

    [Theory]
    [InlineData("port")]
    [InlineData("invalid-port")]
    [InlineData("token")]
    [InlineData("enabled")]
    [InlineData("close")]
    public async Task DraftChangesOrCloseCancelOutstandingStatusRequest(string change)
    {
        using var handler = new DeferredDiagnosticHandler(succeeds: true);
        using var client = new HttpClient(handler);
        using var viewModel = CreateViewModel(client);
        var diagnostic = viewModel.TestMcpConnectionAsync();
        await handler.StatusRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        ChangeDraft(viewModel, change);
        var expectedNotice = viewModel.SettingsStatusText;
        var expectedStatus = viewModel.McpStatusText;
        var expectedTestText = viewModel.McpConnectionTestText;
        await diagnostic.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(handler.RequestWasCancelled);
        Assert.False(viewModel.IsTestingMcpConnection);
        Assert.Equal(expectedNotice, viewModel.SettingsStatusText);
        Assert.Equal(expectedStatus, viewModel.McpStatusText);
        Assert.Equal(expectedTestText, viewModel.McpConnectionTestText);
        Assert.DoesNotContain("Connection failed:", viewModel.McpConnectionTestText, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFiles(_rootDirectory));
    }

    [Fact]
    public async Task ANewDiagnosticUsesTheChangedPortAfterTheCancelledRequestFinishes()
    {
        using var handler = new DeferredDiagnosticHandler(succeeds: true);
        using var client = new HttpClient(handler);
        using var viewModel = CreateViewModel(client);
        var firstDiagnostic = viewModel.TestMcpConnectionAsync();
        await handler.StatusRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        viewModel.McpPort++;
        await firstDiagnostic.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(handler.RequestWasCancelled);

        handler.Release.TrySetResult();
        await viewModel.TestMcpConnectionAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("Connected.", viewModel.McpConnectionTestText);
        Assert.True(viewModel.CanTestMcpConnection);
        Assert.Equal(6, handler.Requests.Count);
        Assert.All(handler.Requests.Skip(3), request => Assert.Equal(CopilotConfig.DefaultMcpPort + 1, request.Port));
        Assert.Empty(Directory.EnumerateFiles(_rootDirectory));
    }

    private CopilotSettingsViewModel CreateViewModel(HttpClient client)
    {
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            McpBearerToken = BearerToken,
            McpPort = CopilotConfig.DefaultMcpPort,
            McpEnabled = false,
        };
        config.EnsureInitialized();
        var configHandler = new ConfigHandler { ConfigFilePath = Path.Combine(_rootDirectory, "ColorVisionConfig.json") };
        configHandler.Configs[typeof(CopilotConfig)] = config;
        return new CopilotSettingsViewModel(configHandler, new CopilotBackendSyncClient(client),
            new CopilotChatState { ActiveProfileId = config.Profiles[0].Id }, mcpHttpClient: client);
    }

    private static void ChangeDraft(CopilotSettingsViewModel viewModel, string change)
    {
        switch (change)
        {
            case "port":
            case "port-back":
                viewModel.McpPort++;
                if (change == "port-back")
                    viewModel.McpPort--;
                break;
            case "invalid-port":
            case "invalid-port-back":
                var originalPort = viewModel.McpPortText;
                viewModel.McpPortText = "invalid";
                if (change == "invalid-port-back")
                    viewModel.McpPortText = originalPort;
                break;
            case "equivalent-port-text":
                viewModel.McpPortText = "0" + viewModel.McpPortText;
                break;
            case "token":
            case "token-back":
                viewModel.McpBearerToken = "changed-test-token";
                if (change == "token-back")
                    viewModel.McpBearerToken = BearerToken;
                break;
            case "enabled":
            case "enabled-back":
                viewModel.McpEnabled = !viewModel.McpEnabled;
                if (change == "enabled-back")
                    viewModel.McpEnabled = !viewModel.McpEnabled;
                break;
            case "close":
                viewModel.Dispose();
                break;
            case "new-notice":
                viewModel.BackendSyncUrl = "https://changed.example.test/configuration";
                break;
            default:
                throw new InvalidOperationException("Unknown MCP draft transition.");
        }
    }

    private static Task StartDiagnostic(CopilotSettingsViewModel viewModel, SynchronizationContext context)
    {
        var previousContext = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            return viewModel.TestMcpConnectionAsync();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    public void Dispose() => Directory.Delete(_rootDirectory, recursive: true);

    private sealed class PausedSynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _callbacks = new();
        public TaskCompletionSource CallbackPosted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override void Post(SendOrPostCallback callback, object? state)
        {
            _callbacks.Enqueue((callback, state));
            CallbackPosted.TrySetResult();
        }

        public void RunPending()
        {
            var previousContext = Current;
            try
            {
                SetSynchronizationContext(this);
                while (_callbacks.TryDequeue(out var item))
                    item.Callback(item.State);
            }
            finally
            {
                SetSynchronizationContext(previousContext);
            }
        }
    }

    private sealed class DeferredDiagnosticHandler : HttpMessageHandler
    {
        private readonly bool _succeeds;
        private readonly CopilotMcpRequestHandler _server;
        public TaskCompletionSource StatusRequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<(int Port, string Authorization)> Requests { get; } = [];
        public bool RequestWasCancelled { get; private set; }

        public DeferredDiagnosticHandler(bool succeeds)
        {
            _succeeds = succeeds;
            var settings = new CopilotMcpRuntimeSettings { Enabled = true, BearerToken = BearerToken };
            _server = new CopilotMcpRequestHandler(() => settings, new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
            {
                RuntimeSettingsProvider = () => settings,
                ServerRunningProvider = () => true,
                ServerStatusMessageProvider = () => "Local MCP lifecycle fixture running.",
                ActiveCopilotRunCountProvider = () => 0,
                QueuedCopilotRunCountProvider = () => 0,
            }));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            Requests.Add((request.RequestUri!.Port, request.Headers.Authorization!.ToString()));
            using var payload = JsonDocument.Parse(body);
            var isStatusRequest = payload.RootElement.GetProperty("method").GetString() == "tools/call";
            if (isStatusRequest)
            {
                StatusRequestStarted.TrySetResult();
                try
                {
                    await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    RequestWasCancelled = true;
                    throw;
                }
                if (!_succeeds)
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            var result = await _server.HandleAsync(new CopilotMcpHttpRequest
            {
                Method = request.Method.Method,
                Path = request.RequestUri.AbsolutePath,
                Headers = request.Headers.ToDictionary(header => header.Key, header => string.Join(", ", header.Value), StringComparer.OrdinalIgnoreCase),
                Body = body,
                CallerSource = "tcp://127.0.0.1",
            }, cancellationToken).ConfigureAwait(false);
            var response = new HttpResponseMessage((HttpStatusCode)result.StatusCode)
            {
                Content = new StringContent(result.Body, Encoding.UTF8, "application/json"),
            };
            foreach (var header in result.Headers)
                response.Headers.TryAddWithoutValidation(header.Key, header.Value);
            return response;
        }
    }
}
