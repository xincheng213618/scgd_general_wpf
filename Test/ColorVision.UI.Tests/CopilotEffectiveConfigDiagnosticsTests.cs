using ColorVision.Copilot;
using System.Collections.ObjectModel;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotEffectiveConfigDiagnosticsTests
{
    [Fact]
    public void DebugConfigCommandIsReadOnlyAndAvailableDuringAnActiveRequest()
    {
        var invocation = Assert.IsType<CopilotLocalCommandInvocation>(
            CopilotLocalCommandCatalog.Parse("/debug-config"));

        Assert.Equal(CopilotLocalCommandKind.EffectiveConfig, invocation.Command.Kind);
        Assert.Empty(invocation.Arguments);
        Assert.True(invocation.Command.AvailableWhileAgentRuns);
        Assert.Contains(
            CopilotLocalCommandCatalog.Suggest("/debug"),
            command => command.Name == "/debug-config");
    }

    [Fact]
    public void ProbeReportsSectionMetadataWithoutReturningConfigurationValues()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var filePath = Path.Combine(root, "ColorVisionConfig.json");
            File.WriteAllText(
                filePath,
                """
                {
                  "CopilotConfig": {
                    "SchemaVersion": 5,
                    "Profiles": [
                      {
                        "Id": "profile-1",
                        "ApiKey": "api_key=probe-secret"
                      }
                    ],
                    "AgentDefaults": {},
                    "McpEnabled": true,
                    "McpBearerToken": "token=probe-secret"
                  }
                }
                """);

            var probe = CopilotEffectiveConfigDiagnostics.ProbeConfigFile(filePath);

            Assert.Equal(CopilotConfigFileProbeState.Loaded, probe.State);
            Assert.Equal(5, probe.SchemaVersion);
            Assert.True(probe.HasAgentDefaults);
            Assert.True(probe.HasMcpSettings);
            Assert.Contains("profile-1", probe.PersistedProfileIds);
            Assert.DoesNotContain("probe-secret", probe.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FormatShowsEffectiveSourcesAndValuesWithoutCredentialOrUrlSecrets()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var configPath = Path.Combine(root, "ColorVisionConfig.json");
            var statePath = Path.Combine(root, "chat-state.json");
            File.WriteAllText(
                configPath,
                """
                {
                  "CopilotConfig": {
                    "SchemaVersion": 5,
                    "Profiles": [
                      {
                        "Id": "profile-1",
                        "ApiKey": "api_key=file-secret"
                      }
                    ],
                    "AgentDefaults": {},
                    "McpPort": 38473,
                    "McpBearerToken": "token=file-secret"
                  }
                }
                """);
            File.WriteAllText(statePath, "{}");

            var profile = new CopilotProfileConfig
            {
                Id = "profile-1",
                Name = "Diagnostic profile",
                VendorType = CopilotVendorType.OpenAI,
                ProviderType = CopilotProviderType.OpenAICompatible,
                Model = "gpt-test",
                BaseUrl = "https://user:password@example.com:8443/v1?api_key=url-secret",
                ApiKey = "api_key=runtime-secret",
                ReasoningMode = CopilotReasoningMode.High,
            };
            profile.UseSystemPromptOverride("system prompt secret");
            var config = new CopilotConfig
            {
                SchemaVersion = CopilotConfig.CurrentSchemaVersion,
                Profiles = new ObservableCollection<CopilotProfileConfig> { profile },
                McpEnabled = true,
                McpPort = 38473,
                McpBearerToken = "token=runtime-secret",
                ExternalMcpServers = new ObservableCollection<CopilotMcpClientServerConfig>
                {
                    new() { Name = "Enabled server", Enabled = true },
                    new() { Name = "Disabled server", Enabled = false },
                },
                AgentDefaults = new CopilotAgentDefaultsConfig
                {
                    ContextWindowTokens = 128_000,
                    RequestTokenBudget = 32_768,
                    MaxToolCalls = 64,
                    MaxAgentPasses = 16,
                    TimeoutSeconds = 3_600,
                    PreferredShell = CopilotShellKind.PowerShell,
                    AutoCompactConversationHistory = true,
                    AutoCompactThresholdPercent = 80,
                    AutoCompactInstructions = "Keep decisions.",
                },
            };
            var state = new CopilotChatState
            {
                SchemaVersion = CopilotChatState.CurrentSchemaVersion,
                ActiveProfileId = profile.Id,
                DefaultFollowUpBehavior = CopilotFollowUpBehavior.Queue,
                UseMultilineComposer = true,
                ShowMessageTimestamps = false,
                UseCompactMessageLayout = true,
                EnablePromptHistoryCompletions = false,
            };
            var conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
            conversation.Title = "Config diagnostics";
            conversation.ResponsePersonality = CopilotResponsePersonality.Pragmatic;
            conversation.AdditionalReadRootPaths.Add(root);
            conversation.PrepareFullAccessGrant(
                root,
                "task-1",
                DateTimeOffset.UtcNow.AddMinutes(5));

            var report = CopilotEffectiveConfigDiagnostics.Format(
                new CopilotEffectiveConfigDiagnosticContext
                {
                    Config = config,
                    State = state,
                    Conversation = conversation,
                    SelectedProfile = profile,
                    ComposerMode = CopilotAgentMode.Code,
                    ConfigFilePath = configPath,
                    StateFilePath = statePath,
                    StateLoadStatus = new CopilotChatStateLoadStatus(
                        CopilotChatStateLoadSource.Primary,
                        CopilotChatState.CurrentSchemaVersion),
                    ConversationRunState = CopilotHostedRunState.Running,
                    McpListenerRunning = true,
                });

            Assert.StartsWith("有效配置 · Config diagnostics", report);
            Assert.Contains("生效来源（基础 → 当前任务）", report);
            Assert.Contains("file schema 5 → runtime 6", report);
            Assert.Contains(Path.GetFullPath(configPath), report);
            Assert.Contains(
                $"主状态文件 · file schema {CopilotChatState.CurrentSchemaVersion} · runtime schema {CopilotChatState.CurrentSchemaVersion}",
                report);
            Assert.Contains("Diagnostic profile · gpt-test · 来源 会话 ProfileId", report);
            Assert.Contains("定义：应用配置 CopilotConfig.Profiles", report);
            Assert.Contains("端点：https://example.com:8443 · 凭据 已配置", report);
            Assert.Contains("系统提示 Profile 覆盖", report);
            Assert.Contains("context 128,000 · request 32,768 tokens · tools 64 · passes 16", report);
            Assert.Contains("Shell：PowerShell · 自动压缩 开启 @ 80%", report);
            Assert.Contains("回答风格：务实 · 来源 会话覆盖", report);
            Assert.Contains("权限：自动复核 · 当前任务", report);
            Assert.Contains("附加只读目录：1 个 · 来源 会话状态", report);
            Assert.Contains("运行中 Enter：排队 · 来源 ChatState 保存值", report);
            Assert.Contains("本机 MCP：启用 · listener 运行中 · port 38473 · Bearer 已配置", report);
            Assert.Contains("外部 MCP：1 / 2 个已启用", report);
            Assert.Contains("当前运行已在请求启动时固定模型 Profile", report);
            Assert.DoesNotContain("file-secret", report, StringComparison.Ordinal);
            Assert.DoesNotContain("runtime-secret", report, StringComparison.Ordinal);
            Assert.DoesNotContain("url-secret", report, StringComparison.Ordinal);
            Assert.DoesNotContain("password", report, StringComparison.Ordinal);
            Assert.DoesNotContain("system prompt secret", report, StringComparison.Ordinal);
            Assert.DoesNotContain("Enabled server", report, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("missing", 1)]
    [InlineData("invalid", 3)]
    [InlineData("section", 2)]
    public void ProbeDistinguishesMissingInvalidAndSectionlessFiles(
        string scenario,
        int expectedState)
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var filePath = Path.Combine(root, "config.json");
            if (scenario == "invalid")
                File.WriteAllText(filePath, "{ invalid");
            else if (scenario == "section")
                File.WriteAllText(filePath, """{ "OtherConfig": {} }""");

            var probe = CopilotEffectiveConfigDiagnostics.ProbeConfigFile(filePath);

            Assert.Equal((CopilotConfigFileProbeState)expectedState, probe.State);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MissingSourcesExplainDefaultsWithoutInventingAConfigLayer()
    {
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            AgentDefaults = new CopilotAgentDefaultsConfig(),
        };
        var state = new CopilotChatState
        {
            SchemaVersion = CopilotChatState.CurrentSchemaVersion,
        };

        var report = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = config,
                State = state,
                ConfigFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.json"),
                StateLoadStatus = new CopilotChatStateLoadStatus(CopilotChatStateLoadSource.Fresh),
            });

        Assert.Contains("当前文件不存在 · 继续使用已加载运行时值", report);
        Assert.Contains($"新建内存状态 · runtime schema {CopilotChatState.CurrentSchemaVersion}", report);
        Assert.Contains("当前没有可用 Profile", report);
        Assert.Contains("按需确认 · 内置安全默认", report);
        Assert.Contains("下一次请求会从上述当前值创建独立请求快照", report);
        Assert.Contains("当前文件来源未证实", report);
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "ColorVision.CopilotEffectiveConfigDiagnosticsTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
