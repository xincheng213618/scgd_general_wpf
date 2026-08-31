using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotExternalMcpRefreshTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(Path.GetTempPath(), "ColorVisionMcpRefresh-" + Guid.NewGuid().ToString("N"));

    public CopilotExternalMcpRefreshTests() => Directory.CreateDirectory(_rootDirectory);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RefreshChecksServersBeyondRuntimeToolBudgetWithoutPublishingTools(bool lastServerSucceeds)
    {
        var servers = CreateServers(3);
        using var handler = new McpHandler { ToolCount = 32 };
        var cache = new CopilotMcpToolDiscoveryCache();
        var catalog = new CopilotCapabilityCatalog();
        var provider = new CopilotMcpToolProvider(cache, catalog, handler.CreateClient);
        await using (var initialLease = await provider.DiscoverAsync(Request(servers[0]), CancellationToken.None))
            Assert.Equal(32, initialLease.Tools.Count);
        var initialCatalog = catalog.GetSnapshot();
        CopilotMcpClientHealthRegistry.RecordConnected(servers[2], 32, 32);
        if (!lastServerSucceeds)
            handler.FailedEndpoint = servers[2].Endpoint;

        await using var lease = await provider.RefreshDiscoveryAsync(Request(servers), CancellationToken.None);

        Assert.Empty(lease.Tools);
        Assert.Equal(initialCatalog.Revision, catalog.GetSnapshot().Revision);
        Assert.Equal(initialCatalog.Capabilities.Count, catalog.GetSnapshot().Capabilities.Count);
        Assert.Equal(2, handler.ListCount(servers[0]));
        Assert.Equal(1, handler.ListCount(servers[1]));
        Assert.Equal(1, handler.ListCount(servers[2]));
        Assert.True(CopilotMcpClientHealthRegistry.TryGetSnapshot(servers[0], out var firstHealth));
        Assert.False(firstHealth.UsedCachedDiscovery);
        Assert.Equal(32, firstHealth.ExposedToolCount);
        Assert.True(CopilotMcpClientHealthRegistry.TryGetSnapshot(servers[2], out var lastHealth));
        Assert.Equal(lastServerSucceeds ? CopilotMcpClientHealthState.Connected : CopilotMcpClientHealthState.Unavailable, lastHealth.State);
        Assert.Equal(lastServerSucceeds ? 32 : 0, lastHealth.ExposedToolCount);
        Assert.Equal(4, handler.CreatedClientCount);
        Assert.Equal(handler.CreatedClientCount, handler.DisposedClientCount);
        Assert.Equal(1, handler.MaximumActiveClientCount);
        Assert.Equal(0, handler.ToolCallCount);
    }

    [Fact]
    public async Task RuntimeDiscoveryRetainsIts64ToolBudgetAndApprovalPolicies()
    {
        var servers = CreateServers(3);
        servers[1].AccessPolicy = CopilotMcpClientAccessPolicy.ReadOnly;
        using var handler = new McpHandler { ToolCount = 32 };
        var catalog = new CopilotCapabilityCatalog();
        var provider = new CopilotMcpToolProvider(new CopilotMcpToolDiscoveryCache(), catalog, handler.CreateClient);

        await using (var lease = await provider.DiscoverAsync(Request(servers), CancellationToken.None))
        {
            Assert.Equal(64, lease.Tools.Count);
            Assert.Equal(64, catalog.GetSnapshot().Capabilities.Count);
            Assert.All(lease.Tools.Take(32), tool =>
            {
                Assert.Equal(CopilotToolAccess.Write, tool.Access);
                Assert.Equal(CopilotToolApprovalMode.Always, tool.ApprovalMode);
            });
            Assert.All(lease.Tools.Skip(32), tool =>
            {
                Assert.Equal(CopilotToolAccess.ReadOnly, tool.Access);
                Assert.Equal(CopilotToolApprovalMode.Never, tool.ApprovalMode);
            });
            Assert.Equal(1, handler.ListCount(servers[0]));
            Assert.Equal(1, handler.ListCount(servers[1]));
            Assert.Equal(0, handler.ListCount(servers[2]));
            Assert.Equal(2, handler.InitializeCount);
            Assert.Equal(0, handler.DisposedClientCount);
        }
        Assert.Equal(2, handler.DisposedClientCount);
        Assert.Equal(0, handler.ToolCallCount);
    }

    [Fact]
    public async Task RefreshRetainsEightServerAndPaginationBoundsAndDisposesEachClientBeforeTheNext()
    {
        var servers = CreateServers(9);
        using var handler = new McpHandler { ContinuePages = true };
        var cache = new CopilotMcpToolDiscoveryCache();
        var provider = new CopilotMcpToolProvider(cache, new CopilotCapabilityCatalog(), handler.CreateClient);

        await using var lease = await provider.RefreshDiscoveryAsync(Request(servers), CancellationToken.None);

        Assert.Empty(lease.Tools);
        foreach (var server in servers.Take(8))
        {
            Assert.Equal(CopilotMcpToolDiscoveryPaginator.MaximumPages, handler.ListCount(server));
            Assert.True(cache.TryGet(server, string.Empty, out var discovery));
            Assert.Equal(CopilotMcpToolDiscoveryPaginator.MaximumPages, discovery.Tools.Count);
        }
        Assert.Equal(0, handler.ListCount(servers[8]));
        Assert.Equal(8, handler.CreatedClientCount);
        Assert.Equal(8, handler.DisposedClientCount);
        Assert.Equal(1, handler.MaximumActiveClientCount);
    }

    [Fact]
    public async Task RefreshUsesConnectionTimeoutAndContinuesWithTheNextServer()
    {
        var servers = CreateServers(2);
        servers[0].ConnectionTimeoutSeconds = 1;
        using var handler = new McpHandler { DeferredEndpoint = servers[0].Endpoint };
        var provider = new CopilotMcpToolProvider(new CopilotMcpToolDiscoveryCache(), new CopilotCapabilityCatalog(), handler.CreateClient);

        await using var lease = await provider.RefreshDiscoveryAsync(Request(servers), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(handler.RequestWasCancelled);
        Assert.True(CopilotMcpClientHealthRegistry.TryGetSnapshot(servers[0], out var health));
        Assert.Equal(CopilotMcpClientHealthState.Unavailable, health.State);
        Assert.Equal(1, handler.ListCount(servers[1]));
        Assert.Equal(2, handler.DisposedClientCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CurrentDraftReceivesAllServerHealthWithoutChangingSavedSettings(bool lastServerSucceeds)
    {
        var servers = CreateServers(3);
        using var handler = new McpHandler { ToolCount = 32, DeferredEndpoint = servers[2].Endpoint };
        if (!lastServerSucceeds)
            handler.FailedEndpoint = servers[2].Endpoint;
        using var viewModel = CreateViewModel(handler, servers);
        CopilotMcpClientHealthRegistry.RecordConnected(servers[2], 32, 32);
        var context = new PausedSynchronizationContext();
        var refresh = StartRefresh(viewModel, context);
        await handler.RequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        handler.Release.TrySetResult();
        await context.CallbackPosted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        context.RunPending();
        await refresh.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(viewModel.IsRefreshingExternalMcpClients);
        Assert.False(viewModel.HasAppliedChanges);
        Assert.False(viewModel.HasUnsavedSettings);
        Assert.Equal(3, viewModel.ExternalMcpClientStatuses.Count);
        Assert.Equal(lastServerSucceeds ? "Connected · 32/32 tools" : "Unavailable", viewModel.ExternalMcpClientStatuses[2].StateText);
        Assert.Equal($"External MCP discovery refreshed: {(lastServerSucceeds ? 3 : 2)}/3 server(s) connected.", viewModel.SettingsStatusText);
        Assert.Equal(3, handler.DisposedClientCount);
        Assert.Empty(Directory.EnumerateFiles(_rootDirectory));
    }

    [Theory]
    [InlineData("edit", true)]
    [InlineData("edit", false)]
    [InlineData("edit-back", true)]
    [InlineData("edit-back", false)]
    [InlineData("clear", true)]
    [InlineData("clear", false)]
    [InlineData("invalid", true)]
    [InlineData("invalid", false)]
    [InlineData("close", true)]
    [InlineData("close", false)]
    [InlineData("new-notice", true)]
    [InlineData("new-notice", false)]
    public async Task ChangedOrClosedDraftCannotReceiveAlreadyCompletedRefresh(string change, bool succeeds)
    {
        var server = CreateServers(1)[0];
        using var handler = new McpHandler { DeferredEndpoint = server.Endpoint };
        if (!succeeds)
            handler.FailedEndpoint = server.Endpoint;
        using var viewModel = CreateViewModel(handler, [server]);
        var originalText = viewModel.ExternalMcpServersText;
        var context = new PausedSynchronizationContext();
        var refresh = StartRefresh(viewModel, context);
        await handler.RequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        handler.Release.TrySetResult();
        await context.CallbackPosted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(refresh.IsCompleted);
        Assert.Equal(1, handler.DisposedClientCount);

        switch (change)
        {
            case "edit":
            case "edit-back":
                viewModel.ExternalMcpServersText = CopilotMcpClientConfigurationText.Format(CreateServers(1));
                if (change == "edit-back")
                    viewModel.ExternalMcpServersText = originalText;
                break;
            case "clear":
                viewModel.ExternalMcpServersText = string.Empty;
                break;
            case "invalid":
                viewModel.ExternalMcpServersText = "invalid server line";
                break;
            case "close":
                viewModel.Dispose();
                break;
            case "new-notice":
                viewModel.BackendSyncUrl = "https://changed.example.test/configuration";
                break;
            default:
                throw new InvalidOperationException("Unknown refresh transition.");
        }
        var expectedNotice = viewModel.SettingsStatusText;
        var expectedStatus = viewModel.ExternalMcpClientsStatusText;
        var expectedRows = viewModel.ExternalMcpClientStatuses.ToArray();

        context.RunPending();
        await refresh.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(viewModel.IsRefreshingExternalMcpClients);
        Assert.False(viewModel.HasAppliedChanges);
        Assert.Equal(expectedNotice, viewModel.SettingsStatusText);
        if (change == "new-notice")
            Assert.StartsWith(succeeds ? "1/1 connected" : "0/1 connected", viewModel.ExternalMcpClientsStatusText, StringComparison.Ordinal);
        else
        {
            Assert.Equal(expectedStatus, viewModel.ExternalMcpClientsStatusText);
            Assert.Equal(expectedRows, viewModel.ExternalMcpClientStatuses.ToArray());
        }
        Assert.Empty(Directory.EnumerateFiles(_rootDirectory));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EditingOrClosingCancelsOutstandingRefreshBeforeTheNextServer(bool close)
    {
        var servers = CreateServers(2);
        using var handler = new McpHandler { DeferredEndpoint = servers[0].Endpoint };
        using var viewModel = CreateViewModel(handler, servers);
        var refresh = viewModel.RefreshExternalMcpClientsAsync();
        await handler.RequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        if (close)
            viewModel.Dispose();
        else
            viewModel.ExternalMcpServersText = string.Empty;
        var expectedNotice = viewModel.SettingsStatusText;
        var expectedStatus = viewModel.ExternalMcpClientsStatusText;
        await refresh.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(handler.RequestWasCancelled);
        Assert.False(viewModel.IsRefreshingExternalMcpClients);
        Assert.Equal(expectedNotice, viewModel.SettingsStatusText);
        Assert.Equal(expectedStatus, viewModel.ExternalMcpClientsStatusText);
        Assert.Equal(0, handler.ListCount(servers[1]));
        Assert.Equal(1, handler.CreatedClientCount);
        Assert.Equal(1, handler.DisposedClientCount);
        Assert.Empty(Directory.EnumerateFiles(_rootDirectory));
    }

    private CopilotSettingsViewModel CreateViewModel(McpHandler handler, CopilotMcpClientServerConfig[] servers)
    {
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            McpBearerToken = "test-mcp-token",
            ExternalMcpServers = new ObservableCollection<CopilotMcpClientServerConfig>(servers),
        };
        config.EnsureInitialized();
        var configHandler = new ConfigHandler { ConfigFilePath = Path.Combine(_rootDirectory, "ColorVisionConfig.json") };
        configHandler.Configs[typeof(CopilotConfig)] = config;
        var provider = new CopilotMcpToolProvider(new CopilotMcpToolDiscoveryCache(), new CopilotCapabilityCatalog(), handler.CreateClient);
        return new CopilotSettingsViewModel(configHandler, new CopilotBackendSyncClient(),
            new CopilotChatState { ActiveProfileId = config.Profiles[0].Id }, externalMcpToolProvider: provider);
    }

    private static CopilotMcpClientServerConfig[] CreateServers(int count)
    {
        var id = Guid.NewGuid().ToString("N");
        return Enumerable.Range(0, count).Select(index => new CopilotMcpClientServerConfig
        {
            Name = $"s{index}-{id}",
            Endpoint = $"https://mcp.example.test/{id}/{index}",
        }).ToArray();
    }

    private static CopilotAgentRequest Request(params CopilotMcpClientServerConfig[] servers) => new() { ExternalMcpServers = servers };

    private static Task StartRefresh(CopilotSettingsViewModel viewModel, SynchronizationContext context)
    {
        var previousContext = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            return viewModel.RefreshExternalMcpClientsAsync();
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

    private sealed class McpHandler : HttpMessageHandler
    {
        private readonly ConcurrentDictionary<string, int> _listCounts = new(StringComparer.Ordinal);
        private readonly object _clientLock = new();
        private int _activeClientCount;
        public int ToolCount { get; init; } = 1;
        public bool ContinuePages { get; init; }
        public string? FailedEndpoint { get; set; }
        public string? DeferredEndpoint { get; init; }
        public TaskCompletionSource RequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool RequestWasCancelled { get; private set; }
        public int CreatedClientCount { get; private set; }
        public int DisposedClientCount { get; private set; }
        public int MaximumActiveClientCount { get; private set; }
        public int ToolCallCount { get; private set; }
        public int InitializeCount { get; private set; }

        public int ListCount(CopilotMcpClientServerConfig server) => _listCounts.GetValueOrDefault(server.Endpoint);

        public HttpClient CreateClient()
        {
            lock (_clientLock)
            {
                CreatedClientCount++;
                MaximumActiveClientCount = Math.Max(MaximumActiveClientCount, ++_activeClientCount);
            }
            return new TrackedClient(this, () =>
            {
                lock (_clientLock)
                {
                    DisposedClientCount++;
                    _activeClientCount--;
                }
            }) { Timeout = Timeout.InfiniteTimeSpan };
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method != HttpMethod.Post)
                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
            Assert.NotNull(request.RequestUri);
            Assert.NotNull(request.Content);
            Assert.Null(request.Headers.Authorization);
            using var payload = JsonDocument.Parse(await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            var root = payload.RootElement;
            var method = root.GetProperty("method").GetString();
            if (method is "notifications/initialized" or "notifications/cancelled")
                return new HttpResponseMessage(HttpStatusCode.Accepted);
            var id = root.GetProperty("id").Clone();
            // The SDK probes the newest protocol before falling back to this
            // fixture's initialize-based server, just as it does for older peers.
            if (method == "server/discover")
                return JsonResponse(new { jsonrpc = "2.0", id, error = new { code = -32601, message = "Method not found" } });
            if (method == "initialize")
            {
                InitializeCount++;
                return JsonResponse(new
                {
                    jsonrpc = "2.0", id,
                    result = new
                    {
                        protocolVersion = root.GetProperty("params").GetProperty("protocolVersion").GetString(),
                        capabilities = new { tools = new { } },
                        serverInfo = new { name = "fake-mcp", version = "1.0" },
                    },
                });
            }
            if (method == "tools/list")
            {
                var endpoint = request.RequestUri.AbsoluteUri;
                _listCounts.AddOrUpdate(endpoint, 1, (_, value) => value + 1);
                if (endpoint == DeferredEndpoint)
                {
                    RequestStarted.TrySetResult();
                    try
                    {
                        await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        RequestWasCancelled = true;
                        throw;
                    }
                }
                if (endpoint == FailedEndpoint)
                    return JsonResponse(new { jsonrpc = "2.0", id, error = new { code = -32603, message = "Test discovery failure" } });
                var page = root.TryGetProperty("params", out var parameters)
                    && parameters.TryGetProperty("cursor", out var cursor)
                    && cursor.ValueKind == JsonValueKind.String
                    ? int.Parse(cursor.GetString()!, CultureInfo.InvariantCulture)
                    : 0;
                return JsonResponse(new
                {
                    jsonrpc = "2.0", id,
                    result = new
                    {
                        tools = Enumerable.Range(0, ToolCount).Select(index => new
                        {
                            name = $"tool{page}_{index}",
                            description = "A fake MCP test tool.",
                            inputSchema = new { type = "object", properties = new { }, additionalProperties = false },
                        }).ToArray(),
                        nextCursor = ContinuePages ? (page + 1).ToString(CultureInfo.InvariantCulture) : null,
                    },
                });
            }
            if (method == "tools/call")
                ToolCallCount++;
            throw new InvalidOperationException($"Unexpected MCP method: {method}");
        }

        private static HttpResponseMessage JsonResponse(object payload) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };

        private sealed class TrackedClient(HttpMessageHandler handler, Action onDispose) : HttpClient(handler, disposeHandler: false)
        {
            private int _disposed;

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
                    onDispose();
            }
        }
    }
}
