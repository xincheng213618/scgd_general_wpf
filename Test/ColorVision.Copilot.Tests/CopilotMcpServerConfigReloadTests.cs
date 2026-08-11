using ColorVision.Copilot.Mcp;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotMcpServerConfigReloadTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(
        Path.GetTempPath(),
        $"ColorVisionMcpReload-{Guid.NewGuid():N}");

    public CopilotMcpServerConfigReloadTests()
    {
        Directory.CreateDirectory(_rootDirectory);
    }

    [Fact]
    public async Task LoadConfigsMovesListenerAndTokenToCurrentConfigGeneration()
    {
        using var listenerFactory = new ReservedListenerFactory(2);
        int firstPort = listenerFactory.Ports[0];
        int secondPort = listenerFactory.Ports[1];
        string configPath = Path.Combine(_rootDirectory, "ColorVisionConfig.json");
        const string firstToken = "mcp-config-generation-one";
        const string secondToken = "mcp-config-generation-two";

        WriteConfig(configPath, CreateConfig(firstPort, firstToken));
        var configHandler = new ConfigHandler { ConfigFilePath = configPath };
        configHandler.LoadConfigs();
        CopilotConfig firstConfig = configHandler.GetRequiredService<CopilotConfig>();

        using var server = new CopilotMcpServer(listenerFactory.Create, CreateThrowingCancellationTokenSource);
        ConfigReloadResult initialBind = configHandler.RegisterReloadParticipants(server);

        Assert.True(initialBind.Succeeded, initialBind.BuildFailureSummary());
        Assert.Equal(HttpStatusCode.OK, await InitializeAsync(firstPort, firstToken));

        WriteConfig(configPath, CreateConfig(secondPort, secondToken));
        ConfigReloadResult reloadResult = configHandler.LoadConfigsWithResult();

        Assert.True(reloadResult.Succeeded, reloadResult.BuildFailureSummary());
        Assert.NotSame(firstConfig, configHandler.GetRequiredService<CopilotConfig>());
        await AssertPortClosedAsync(firstPort);
        Assert.Equal(HttpStatusCode.Unauthorized, await InitializeAsync(secondPort, firstToken));
        Assert.Equal(HttpStatusCode.OK, await InitializeAsync(secondPort, secondToken));

        firstConfig.McpEnabled = false;
        firstConfig.McpPort = firstPort;
        firstConfig.McpBearerToken = "retired-config-token";

        Assert.Equal(HttpStatusCode.OK, await InitializeAsync(secondPort, secondToken));
    }

    [Fact]
    public void ListenerStartupFailureIsCapturedByParticipantBinding()
    {
        var configHandler = new ConfigHandler { ConfigFilePath = Path.Combine(_rootDirectory, "startup-failure.json") };
        configHandler.Configs[typeof(CopilotConfig)] = CreateConfig(47651, "mcp-startup-failure-token");
        using var server = new CopilotMcpServer((_, _) => throw new SocketException((int)SocketError.AddressAlreadyInUse));

        ConfigReloadResult result = configHandler.RegisterReloadParticipants(server);

        Assert.False(result.Succeeded);
        ConfigReloadFailure failure = Assert.Single(result.Failures);
        Assert.Equal(nameof(CopilotMcpServer), failure.OwnerName);
        Assert.Contains("port unavailable", failure.Exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(server.IsRunning);
    }

    [Theory]
    [InlineData(ConfigPreprocessingFailure.Get)]
    [InlineData(ConfigPreprocessingFailure.Save)]
    public async Task ConfigPreprocessingFailureStopsC1AndIsAggregated(ConfigPreprocessingFailure failureMode)
    {
        using var listenerFactory = new ReservedListenerFactory(1);
        int port = listenerFactory.Ports[0];
        string configPath = Path.Combine(_rootDirectory, $"preprocessing-{failureMode}.json");
        const string firstToken = "mcp-preprocessing-c1";
        WriteConfig(configPath, CreateConfig(port, firstToken));
        var configHandler = new ConfigHandler { ConfigFilePath = configPath };
        configHandler.LoadConfigs();
        using var server = new CopilotMcpServer(listenerFactory.Create);
        ConfigReloadResult initialBind = configHandler.RegisterReloadParticipants(server);
        Assert.True(initialBind.Succeeded, initialBind.BuildFailureSummary());
        Assert.Equal(HttpStatusCode.OK, await InitializeAsync(port, firstToken));

        var secondConfig = CreateConfig(port, failureMode == ConfigPreprocessingFailure.Save
            ? string.Empty
            : "mcp-preprocessing-c2");
        var coordinator = new ConfigReloadCoordinator(new FailingConfigService(secondConfig, failureMode));
        ConfigReloadResult result = coordinator.RegisterAndBind(server);

        Assert.False(result.Succeeded);
        ConfigReloadFailure failure = Assert.Single(result.Failures);
        Assert.Equal(nameof(CopilotMcpServer), failure.OwnerName);
        Assert.Contains($"mcp-{failureMode.ToString().ToLowerInvariant()}-failure", failure.Exception.ToString(), StringComparison.Ordinal);
        Assert.False(server.IsRunning);
        await AssertPortClosedAsync(port);
    }

    [Fact]
    public async Task RequestUsesOneRuntimeSettingsSnapshotWhileNextGenerationIsInstalled()
    {
        var firstSettings = new CopilotMcpRuntimeSettings
        {
            Enabled = true,
            Host = "127.0.0.1",
            Port = 41011,
            BearerToken = "mcp-request-snapshot-c1",
        };
        var secondSettings = new CopilotMcpRuntimeSettings
        {
            Enabled = true,
            Host = "127.0.0.1",
            Port = 41012,
            BearerToken = "mcp-request-snapshot-c2",
        };
        CopilotMcpRuntimeSettings currentSettings = firstSettings;
        using var settingsCaptured = new ManualResetEventSlim();
        using var releaseRequest = new ManualResetEventSlim();
        int providerCalls = 0;
        int blockProviderCall = 0;
        var handler = new CopilotMcpRequestHandler(() =>
        {
            CopilotMcpRuntimeSettings capturedSettings = Volatile.Read(ref currentSettings);
            Interlocked.Increment(ref providerCalls);
            if (Interlocked.Exchange(ref blockProviderCall, 0) == 1)
            {
                settingsCaptured.Set();
                if (!releaseRequest.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Timed out waiting to release the captured MCP request settings.");
            }
            return capturedSettings;
        });

        CopilotMcpHttpResponse initializeResponse = await handler.HandleAsync(
            CreateInitializeRequest(firstSettings.BearerToken),
            CancellationToken.None);
        Assert.Equal(200, initializeResponse.StatusCode);
        string sessionId = initializeResponse.Headers[CopilotMcpRequestHandler.SessionHeaderName];

        Volatile.Write(ref blockProviderCall, 1);
        Task<CopilotMcpHttpResponse> firstRequest = Task.Run(() => handler.HandleAsync(
            CreateGetRequest(firstSettings.BearerToken, sessionId),
            CancellationToken.None));
        try
        {
            Assert.True(settingsCaptured.Wait(TimeSpan.FromSeconds(5)), "The MCP request did not capture its C1 runtime settings.");
            Volatile.Write(ref currentSettings, secondSettings);
        }
        finally
        {
            releaseRequest.Set();
        }

        CopilotMcpHttpResponse firstResponse = await firstRequest;
        Assert.Equal(200, firstResponse.StatusCode);
        Assert.Contains(firstSettings.Endpoint, firstResponse.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(secondSettings.Endpoint, firstResponse.Body, StringComparison.Ordinal);
        Assert.Equal(2, Volatile.Read(ref providerCalls));

        CopilotMcpHttpResponse retiredTokenResponse = await handler.HandleAsync(
            CreateGetRequest(firstSettings.BearerToken, sessionId),
            CancellationToken.None);
        Assert.Equal(401, retiredTokenResponse.StatusCode);

        CopilotMcpHttpResponse currentResponse = await handler.HandleAsync(
            CreateGetRequest(secondSettings.BearerToken, sessionId),
            CancellationToken.None);
        Assert.Equal(200, currentResponse.StatusCode);
        Assert.Contains(secondSettings.Endpoint, currentResponse.Body, StringComparison.Ordinal);
    }

    private static CopilotConfig CreateConfig(int port, string bearerToken)
    {
        return new CopilotConfig
        {
            McpEnabled = true,
            McpPort = port,
            McpBearerToken = bearerToken,
        };
    }

    private static void WriteConfig(string path, CopilotConfig config)
    {
        var writer = new ConfigHandler { ConfigFilePath = path };
        writer.Configs[typeof(CopilotConfig)] = config;
        writer.SaveConfigs();
    }

    private static async Task<HttpStatusCode> InitializeAsync(int port, string bearerToken)
    {
        using var handler = new SocketsHttpHandler { UseProxy = false };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        using var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{port}/mcp");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Content = new StringContent(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"config-reload-test","version":"1.0"}}}""",
            Encoding.UTF8,
            "application/json");
        using HttpResponseMessage response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private static CopilotMcpHttpRequest CreateInitializeRequest(string bearerToken)
    {
        return new CopilotMcpHttpRequest
        {
            Method = "POST",
            Path = "/mcp",
            CallerSource = "tcp://config-snapshot-test",
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Authorization"] = $"Bearer {bearerToken}",
            },
            Body = """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"config-snapshot-test","version":"1.0"}}}""",
        };
    }

    private static CopilotMcpHttpRequest CreateGetRequest(string bearerToken, string sessionId)
    {
        return new CopilotMcpHttpRequest
        {
            Method = "GET",
            Path = "/mcp",
            CallerSource = "tcp://config-snapshot-test",
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Authorization"] = $"Bearer {bearerToken}",
                [CopilotMcpRequestHandler.SessionHeaderName] = sessionId,
            },
        };
    }

    private static async Task AssertPortClosedAsync(int port)
    {
        using var client = new TcpClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await client.ConnectAsync(IPAddress.Loopback, port, timeout.Token));
    }

    private static CancellationTokenSource CreateThrowingCancellationTokenSource()
    {
        var source = new CancellationTokenSource();
        _ = source.Token.Register(static () => throw new InvalidOperationException("test cancellation callback failure"));
        return source;
    }

    public enum ConfigPreprocessingFailure
    {
        Get,
        Save,
    }

    private sealed class FailingConfigService : IConfigService
    {
        private readonly CopilotConfig _config;
        private readonly ConfigPreprocessingFailure _failureMode;

        public FailingConfigService(CopilotConfig config, ConfigPreprocessingFailure failureMode)
        {
            _config = config;
            _failureMode = failureMode;
        }

        public IConfig GetRequiredService(Type type)
        {
            if (_failureMode == ConfigPreprocessingFailure.Get)
                throw new InvalidOperationException("mcp-get-failure");
            if (type != typeof(CopilotConfig))
                throw new InvalidOperationException($"Unexpected configuration type: {type.FullName}");
            return _config;
        }

        public T1 GetRequiredService<T1>() where T1 : IConfig =>
            (T1)GetRequiredService(typeof(T1));

        public void SaveConfigs() => throw new NotSupportedException();

        public void LoadConfigs() => throw new NotSupportedException();

        public void Save<T1>() where T1 : IConfig
        {
            if (_failureMode == ConfigPreprocessingFailure.Save)
                throw new InvalidOperationException("mcp-save-failure");
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
            Directory.Delete(_rootDirectory, recursive: true);
    }

    private sealed class ReservedListenerFactory : IDisposable
    {
        private readonly Dictionary<int, TcpListener> _listeners = new();

        public ReservedListenerFactory(int count)
        {
            var ports = new List<int>();
            for (int index = 0; index < count; index++)
            {
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                _listeners.Add(port, listener);
                ports.Add(port);
            }
            Ports = ports;
        }

        public List<int> Ports { get; }

        public TcpListener Create(IPAddress address, int port)
        {
            if (!_listeners.Remove(port, out TcpListener? listener))
                throw new InvalidOperationException($"No reserved listener is available for port {port}.");
            return listener;
        }

        public void Dispose()
        {
            foreach (TcpListener listener in _listeners.Values)
                listener.Stop();
            _listeners.Clear();
        }
    }
}
