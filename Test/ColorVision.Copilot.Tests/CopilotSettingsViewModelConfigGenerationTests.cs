using System.Net.Http;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotSettingsViewModelConfigGenerationTests
{
    [Fact]
    public void ReloadedWindowCannotSaveOrApplyC1ValuesToC2()
    {
        CopilotConfig firstConfig = CreateConfig("c1-profile", "c1-api-key", "c1-mcp-token");
        CopilotConfig secondConfig = CreateConfig("c2-profile", "c2-api-key", "c2-mcp-token");
        var harness = new SettingsBindingsHarness(firstConfig);
        using var viewModel = harness.CreateViewModel();
        harness.ResetCounts();

        viewModel.Profiles[0].Name = "stale-window-profile";
        viewModel.Profiles[0].ApiKey = "stale-window-api-key";
        viewModel.McpBearerToken = "stale-window-mcp-token";
        Assert.True(viewModel.HasUnsavedSettings);

        harness.CurrentConfig = secondConfig;
        harness.RaiseConfigsReloaded();

        Assert.False(viewModel.CanApplySettings);
        Assert.False(viewModel.CanSaveSettings);
        Assert.False(viewModel.Save());
        viewModel.UseSelectedProfileInChatCommand.Execute(null);

        Assert.Equal("c2-profile", secondConfig.Profiles[0].Name);
        Assert.Equal("c2-api-key", secondConfig.Profiles[0].ApiKey);
        Assert.Equal("c2-mcp-token", secondConfig.McpBearerToken);
        Assert.Equal(0, harness.PersistCalls);
        Assert.Equal(0, harness.ApplyCalls);
        Assert.Equal(CopilotSettingsViewModel.StaleConfigGenerationMessage, viewModel.SettingsStatusText);
    }

    [Fact]
    public async Task ReloadedWindowDoesNotStartMcpProfileOrBackendTests()
    {
        CopilotConfig firstConfig = CreateConfig("c1-profile", "c1-api-key", "c1-mcp-token");
        CopilotConfig secondConfig = CreateConfig("c2-profile", "c2-api-key", "c2-mcp-token");
        var harness = new SettingsBindingsHarness(firstConfig);
        using var viewModel = harness.CreateViewModel();
        harness.ResetCounts();

        harness.CurrentConfig = secondConfig;

        await viewModel.TestMcpConnectionAsync();
        await viewModel.TestSelectedProfileConnectionAsync();
        await viewModel.SyncBackendConfigAsync();

        Assert.Equal(0, harness.McpRequestCalls);
        Assert.Equal(0, harness.ModelTestCalls);
        Assert.Equal(0, harness.BackendFetchCalls);
        Assert.Equal(0, harness.PersistCalls);
        Assert.Equal(0, harness.ApplyCalls);
        Assert.Equal("c2-profile", secondConfig.Profiles[0].Name);
        Assert.Equal("c2-mcp-token", secondConfig.McpBearerToken);
        Assert.Equal(CopilotSettingsViewModel.StaleConfigGenerationMessage, viewModel.SettingsStatusText);
    }

    [Fact]
    public void CurrentGenerationWindowStillSavesAndAppliesNormally()
    {
        CopilotConfig config = CreateConfig("current-profile", "current-api-key", "current-mcp-token");
        var harness = new SettingsBindingsHarness(config);
        using var viewModel = harness.CreateViewModel();
        harness.ResetCounts();

        viewModel.Profiles[0].Name = "updated-profile";
        viewModel.Profiles[0].ApiKey = "updated-api-key";
        viewModel.McpBearerToken = "updated-mcp-token";

        Assert.True(viewModel.CanApplySettings);
        Assert.True(viewModel.Save());

        Assert.Equal("updated-profile", config.Profiles[0].Name);
        Assert.Equal("updated-api-key", config.Profiles[0].ApiKey);
        Assert.Equal("updated-mcp-token", config.McpBearerToken);
        Assert.Equal(1, harness.PersistCalls);
        Assert.Equal(1, harness.ApplyCalls);
        Assert.True(viewModel.HasAppliedChanges);
        Assert.False(viewModel.HasUnsavedSettings);
    }

    [Fact]
    public void ReloadDuringSaveCannotApplyC1ValuesOrReportSuccess()
    {
        CopilotConfig firstConfig = CreateConfig("c1-profile", "c1-api-key", "c1-mcp-token");
        CopilotConfig secondConfig = CreateConfig("c2-profile", "c2-api-key", "c2-mcp-token");
        var harness = new SettingsBindingsHarness(firstConfig);
        using var viewModel = harness.CreateViewModel();
        harness.ResetCounts();
        harness.AfterPersist = () => harness.CurrentConfig = secondConfig;

        viewModel.Profiles[0].Name = "stale-window-profile";
        viewModel.McpBearerToken = "stale-window-mcp-token";

        Assert.False(viewModel.Save());

        Assert.Equal("c2-profile", secondConfig.Profiles[0].Name);
        Assert.Equal("c2-mcp-token", secondConfig.McpBearerToken);
        Assert.Equal(1, harness.PersistCalls);
        Assert.Equal(0, harness.ApplyCalls);
        Assert.False(viewModel.HasAppliedChanges);
        Assert.Equal(CopilotSettingsViewModel.StaleConfigGenerationMessage, viewModel.SettingsStatusText);
    }

    [Fact]
    public async Task CurrentGenerationWindowStillRunsConnectionTestsAndBackendSync()
    {
        CopilotConfig config = CreateConfig("current-profile", "current-api-key", "current-mcp-token");
        var harness = new SettingsBindingsHarness(config)
        {
            CompleteOperationsSuccessfully = true,
        };
        using var viewModel = harness.CreateViewModel();
        harness.ResetCounts();

        await viewModel.TestMcpConnectionAsync();
        Assert.Equal("Connected.", viewModel.McpConnectionTestText);

        await viewModel.TestSelectedProfileConnectionAsync();
        Assert.Contains("Connected in", viewModel.SelectedProfileConnectionTestText, StringComparison.Ordinal);

        await viewModel.SyncBackendConfigAsync();

        Assert.Equal(1, harness.McpRequestCalls);
        Assert.Equal(1, harness.ModelTestCalls);
        Assert.Equal(1, harness.BackendFetchCalls);
        Assert.Equal(1, harness.PersistCalls);
        Assert.Equal(0, harness.ApplyCalls);
        Assert.Contains("Revision test-revision", viewModel.BackendSyncStatusText, StringComparison.Ordinal);
    }

    private static CopilotConfig CreateConfig(string profileName, string apiKey, string mcpToken)
    {
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            McpEnabled = false,
            McpPort = CopilotConfig.DefaultMcpPort,
            McpBearerToken = mcpToken,
            BackendSyncUrl = "https://config.example.test",
        };
        config.Profiles.Add(new CopilotProfileConfig
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = profileName,
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = apiKey,
            BaseUrl = "https://models.example.test/v1",
            Model = "test-model",
        });
        config.EnsureInitialized();
        return config;
    }

    private sealed class SettingsBindingsHarness
    {
        private EventHandler? _configsReloaded;

        public SettingsBindingsHarness(CopilotConfig sourceConfig)
        {
            SourceConfig = sourceConfig;
            CurrentConfig = sourceConfig;
        }

        public CopilotConfig SourceConfig { get; }

        public CopilotConfig CurrentConfig { get; set; }

        public int PersistCalls { get; private set; }

        public int ApplyCalls { get; private set; }

        public int ModelTestCalls { get; private set; }

        public int BackendFetchCalls { get; private set; }

        public int McpRequestCalls { get; private set; }

        public bool CompleteOperationsSuccessfully { get; set; }

        public Action? AfterPersist { get; set; }

        public CopilotSettingsViewModel CreateViewModel()
        {
            return new CopilotSettingsViewModel(new CopilotSettingsRuntimeBindings(
                SourceConfig,
                () => CurrentConfig,
                () =>
                {
                    PersistCalls++;
                    AfterPersist?.Invoke();
                },
                () => ApplyCalls++,
                (_, _) =>
                {
                    ModelTestCalls++;
                    if (CompleteOperationsSuccessfully)
                    {
                        return Task.FromResult(new CopilotModelConnectionDiagnosticResult(
                            TimeSpan.Zero,
                            2,
                            0,
                            null,
                            new CopilotChatStreamResult(
                                CopilotTokenUsage.Empty,
                                CopilotChatFinishKind.Complete,
                                "stop")));
                    }

                    return Task.FromException<CopilotModelConnectionDiagnosticResult>(
                        new InvalidOperationException("A stale model diagnostic was started."));
                },
                (_, _, _) =>
                {
                    BackendFetchCalls++;
                    if (CompleteOperationsSuccessfully)
                    {
                        return Task.FromResult(new CopilotBackendConfigResponse
                        {
                            SchemaVersion = 1,
                            Revision = "test-revision",
                        });
                    }

                    return Task.FromException<CopilotBackendConfigResponse>(
                        new InvalidOperationException("A stale backend fetch was started."));
                },
                (_, _, _) =>
                {
                    McpRequestCalls++;
                    if (CompleteOperationsSuccessfully)
                    {
                        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                        {
                            Content = new StringContent(
                                """{"jsonrpc":"2.0","id":1,"result":{"isError":false}}"""),
                        });
                    }

                    return Task.FromException<HttpResponseMessage>(
                        new InvalidOperationException("A stale MCP request was started."));
                },
                handler => _configsReloaded += handler,
                handler => _configsReloaded -= handler));
        }

        public void RaiseConfigsReloaded() => _configsReloaded?.Invoke(this, EventArgs.Empty);

        public void ResetCounts()
        {
            PersistCalls = 0;
            ApplyCalls = 0;
            ModelTestCalls = 0;
            BackendFetchCalls = 0;
            McpRequestCalls = 0;
        }
    }
}
