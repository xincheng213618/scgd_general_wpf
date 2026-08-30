using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Reflection;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotExternalMcpDiagnosticDraftTests
{
    [Theory]
    [InlineData("refresh", "valid")]
    [InlineData("refresh", "empty")]
    [InlineData("refresh", "invalid")]
    [InlineData("local-edit", "valid")]
    [InlineData("local-edit", "empty")]
    [InlineData("local-edit", "invalid")]
    [InlineData("local-test", "valid")]
    [InlineData("local-test", "empty")]
    [InlineData("local-test", "invalid")]
    [InlineData("copy-text", "valid")]
    [InlineData("copy-text", "empty")]
    [InlineData("copy-text", "invalid")]
    public async Task LocalDiagnosticsKeepCurrentExternalMcpDraft(string entry, string draftKind)
    {
        using var fixture = new SettingsFixture();
        var viewModel = fixture.ViewModel;
        if (entry == "local-test")
            viewModel.McpBearerToken = string.Empty;
        var draftText = draftKind switch
        {
            "valid" => CopilotMcpClientConfigurationText.Format([fixture.DraftServer]),
            "empty" => string.Empty,
            "invalid" => "invalid external MCP server line",
            _ => throw new InvalidOperationException("Unknown draft kind."),
        };
        viewModel.ExternalMcpServersText = draftText;
        viewModel.BackendSyncUrl = "https://changed.example.test/configuration";
        var expectedNotice = viewModel.SettingsStatusText;
        var expectedStatus = viewModel.ExternalMcpClientsStatusText;
        var expectedValidation = viewModel.ExternalMcpServersValidationText;
        var expectedRows = SnapshotRows(viewModel);
        Assert.Equal(draftKind != "invalid", viewModel.IsExternalMcpServersValid);

        switch (entry)
        {
            case "refresh":
                viewModel.RefreshMcpDiagnosticsCommand.Execute(null);
                break;
            case "local-edit":
                viewModel.McpPort++;
                break;
            case "local-test":
                await viewModel.TestMcpConnectionAsync();
                break;
            case "copy-text":
                var builder = typeof(CopilotSettingsViewModel).GetMethod("BuildMcpDiagnosticsClipboardText", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var report = Assert.IsType<string>(builder.Invoke(viewModel, null));
                Assert.StartsWith("ColorVision MCP diagnostics", report, StringComparison.Ordinal);
                break;
            default:
                throw new InvalidOperationException("Unknown diagnostics entry.");
        }

        Assert.Equal(draftText, viewModel.ExternalMcpServersText);
        Assert.Equal(draftKind != "invalid", viewModel.IsExternalMcpServersValid);
        Assert.Equal(expectedValidation, viewModel.ExternalMcpServersValidationText);
        Assert.Equal(expectedStatus, viewModel.ExternalMcpClientsStatusText);
        Assert.Equal(expectedRows, SnapshotRows(viewModel));
        if (entry is "refresh" or "copy-text")
            Assert.Equal(expectedNotice, viewModel.SettingsStatusText);
        else if (entry == "local-edit")
            Assert.Contains("MCP settings changed", viewModel.SettingsStatusText, StringComparison.Ordinal);
        else
            Assert.Equal("MCP connection test failed: token missing.", viewModel.SettingsStatusText);
        Assert.True(viewModel.HasUnsavedSettings);
        fixture.AssertNoPersistenceOrNetwork();
    }

    [Fact]
    public void DiagnosticsRefreshCachedHealthForTheDraftServer()
    {
        using var fixture = new SettingsFixture();
        var viewModel = fixture.ViewModel;
        viewModel.ExternalMcpServersText = CopilotMcpClientConfigurationText.Format([fixture.DraftServer]);
        Assert.Equal("Not checked", Assert.Single(viewModel.ExternalMcpClientStatuses).StateText);
        CopilotMcpClientHealthRegistry.RecordConnected(fixture.DraftServer, discoveredToolCount: 5, exposedToolCount: 2, usedCachedDiscovery: true);
        var expectedNotice = viewModel.SettingsStatusText;

        viewModel.RefreshMcpDiagnosticsCommand.Execute(null);

        var status = Assert.Single(viewModel.ExternalMcpClientStatuses);
        Assert.Equal(fixture.DraftServer.Name, status.ServerName);
        Assert.Equal(fixture.DraftServer.Endpoint, status.Endpoint);
        Assert.Equal("Connected · 2/5 tools", status.StateText);
        Assert.Contains("cached discovery", status.DetailText, StringComparison.Ordinal);
        Assert.Equal("1/1 connected", viewModel.ExternalMcpClientsStatusText);
        Assert.Equal(expectedNotice, viewModel.SettingsStatusText);
        fixture.AssertNoPersistenceOrNetwork();
    }

    [Fact]
    public void DiagnosticsStillRefreshTheSavedServerWhenTheDraftHasNotChanged()
    {
        using var fixture = new SettingsFixture();
        var viewModel = fixture.ViewModel;
        CopilotMcpClientHealthRegistry.RecordUnavailable(fixture.SavedServer, "Fixture service stopped.");

        viewModel.RefreshMcpDiagnosticsCommand.Execute(null);

        var status = Assert.Single(viewModel.ExternalMcpClientStatuses);
        Assert.Equal(fixture.SavedServer.Name, status.ServerName);
        Assert.Equal("Unavailable", status.StateText);
        Assert.Equal("0/1 connected · 1 unavailable", viewModel.ExternalMcpClientsStatusText);
        Assert.False(viewModel.HasUnsavedSettings);
        fixture.AssertNoPersistenceOrNetwork();
    }

    private static (string Name, string Endpoint, string State, string Detail, string Checked)[] SnapshotRows(CopilotSettingsViewModel viewModel)
        => viewModel.ExternalMcpClientStatuses.Select(status =>
            (status.ServerName, status.Endpoint, status.StateText, status.DetailText, status.CheckedText)).ToArray();

    private sealed class SettingsFixture : IDisposable
    {
        private readonly string _rootDirectory = Path.Combine(Path.GetTempPath(), "ColorVisionMcpDraftDiagnostics-" + Guid.NewGuid().ToString("N"));
        private readonly NoNetworkHandler _handler = new();
        private readonly HttpClient _client;
        private readonly CopilotConfig _config;
        private int _externalClientCount;
        public CopilotMcpClientServerConfig SavedServer { get; } = CreateServer("saved");
        public CopilotMcpClientServerConfig DraftServer { get; } = CreateServer("draft");
        public CopilotSettingsViewModel ViewModel { get; }

        public SettingsFixture()
        {
            Directory.CreateDirectory(_rootDirectory);
            _client = new HttpClient(_handler, disposeHandler: false);
            _config = new CopilotConfig
            {
                SchemaVersion = CopilotConfig.CurrentSchemaVersion,
                McpBearerToken = "local-diagnostic-test-token",
                ExternalMcpServers = new ObservableCollection<CopilotMcpClientServerConfig> { SavedServer },
            };
            _config.EnsureInitialized();
            CopilotMcpClientHealthRegistry.RecordConnected(SavedServer, discoveredToolCount: 9, exposedToolCount: 9);
            var configHandler = new ConfigHandler { ConfigFilePath = Path.Combine(_rootDirectory, "ColorVisionConfig.json") };
            configHandler.Configs[typeof(CopilotConfig)] = _config;
            var provider = new CopilotMcpToolProvider(new CopilotMcpToolDiscoveryCache(), new CopilotCapabilityCatalog(), () =>
            {
                _externalClientCount++;
                return new HttpClient(_handler, disposeHandler: false);
            });
            ViewModel = new CopilotSettingsViewModel(configHandler, new CopilotBackendSyncClient(_client),
                new CopilotChatState { ActiveProfileId = _config.Profiles[0].Id }, externalMcpToolProvider: provider, mcpHttpClient: _client);
        }

        public void AssertNoPersistenceOrNetwork()
        {
            Assert.Same(SavedServer, Assert.Single(_config.ExternalMcpServers));
            Assert.False(ViewModel.HasAppliedChanges);
            Assert.False(ViewModel.IsRefreshingExternalMcpClients);
            Assert.Equal(0, _externalClientCount);
            Assert.Equal(0, _handler.RequestCount);
            Assert.Empty(Directory.EnumerateFiles(_rootDirectory));
        }

        public void Dispose()
        {
            ViewModel.Dispose();
            _client.Dispose();
            _handler.Dispose();
            Directory.Delete(_rootDirectory, recursive: true);
        }

        private static CopilotMcpClientServerConfig CreateServer(string label)
        {
            var id = Guid.NewGuid().ToString("N");
            return new CopilotMcpClientServerConfig { Name = label + "-" + id, Endpoint = $"https://mcp.example.test/{label}/{id}" };
        }
    }

    private sealed class NoNetworkHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            throw new InvalidOperationException("Local diagnostics must not send HTTP requests.");
        }
    }
}
