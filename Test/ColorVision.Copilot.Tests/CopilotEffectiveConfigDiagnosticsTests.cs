using ColorVision.Copilot;
using System.Globalization;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotEffectiveConfigDiagnosticsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"copilot-effective-config-{Guid.NewGuid():N}");

    public CopilotEffectiveConfigDiagnosticsTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void CurrentSettingsAndSessionChangesRefreshTheReportWithoutClaimingToUpdateTheRunningTask()
    {
        var profile = new CopilotProfileConfig { Id = "local-profile", Name = "Local", Model = "model-before" };
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            Profiles = [profile],
            AgentDefaults = new CopilotAgentDefaultsConfig { MaxToolCalls = 31 },
        };
        var state = new CopilotChatState { ActiveProfileId = profile.Id };
        var conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.Name);
        var context = new CopilotEffectiveConfigDiagnosticContext
        {
            Config = config,
            State = state,
            Conversation = conversation,
            SelectedProfile = profile,
            ComposerMode = CopilotAgentMode.Code,
            ConversationRunState = CopilotHostedRunState.Running,
            StateLoadStatus = new CopilotChatStateLoadStatus(CopilotChatStateLoadSource.Backup),
            ConfigFilePath = WriteConfigMetadata(),
        };

        var before = CopilotEffectiveConfigDiagnostics.Format(context);
        profile.Model = "model-after";
        config.AgentDefaults.MaxToolCalls = 47;
        config.McpEnabled = true;
        config.McpPort = 38474;
        conversation.HasResponsePersonalityOverride = true;
        conversation.ResponsePersonality = CopilotResponsePersonality.Friendly;
        state.DefaultFollowUpBehavior = CopilotFollowUpBehavior.Queue;
        var after = CopilotEffectiveConfigDiagnostics.Format(context);

        Assert.Contains("model-before", before, StringComparison.Ordinal);
        Assert.Contains("tools 31", before, StringComparison.Ordinal);
        Assert.Contains("model-after", after, StringComparison.Ordinal);
        Assert.DoesNotContain("model-before", after, StringComparison.Ordinal);
        Assert.Contains("tools 47", after, StringComparison.Ordinal);
        Assert.Contains("来源 会话 ProfileId", after, StringComparison.Ordinal);
        Assert.Contains("应用配置 CopilotConfig.Profiles", after, StringComparison.Ordinal);
        Assert.Contains("来源 应用配置 CopilotConfig.AgentDefaults", after, StringComparison.Ordinal);
        Assert.Contains("会话状态 · 备份恢复", after, StringComparison.Ordinal);
        Assert.Contains("回答风格：友好 · 来源 会话覆盖", after, StringComparison.Ordinal);
        Assert.Contains("运行中 Enter：排队 · 来源 ChatState 保存值", after, StringComparison.Ordinal);
        Assert.Contains("本机 MCP：启用 · listener 未运行 · port 38474", after, StringComparison.Ordinal);
        Assert.Contains("不是正在运行任务的冻结值", after, StringComparison.Ordinal);
        Assert.Contains("设置修改只影响后续请求", after, StringComparison.Ordinal);
        Assert.DoesNotContain("Codex model", after, StringComparison.Ordinal);
        Assert.DoesNotContain("config.toml", after, StringComparison.Ordinal);
        Assert.DoesNotContain("官方默认", after, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportRedactsConfigurationSecretsAndUsesTheSelectedProfile()
    {
        const string secret = "private-value-must-not-appear";
        var path = WriteConfigMetadata();
        var originalConfig = File.ReadAllText(path);
        File.WriteAllText(Path.Combine(_directory, "config.toml"), $"model = \"{secret}\"");
        var profile = new CopilotProfileConfig
        {
            Id = "local-profile",
            Name = "Profile",
            Model = "profile-model",
            ApiKey = secret,
            BaseUrl = $"https://user:{secret}@example.test:8443/private/{secret}?token={secret}#{secret}",
        };
        profile.UseSystemPromptOverride(secret);
        var report = CopilotEffectiveConfigDiagnostics.Format(new CopilotEffectiveConfigDiagnosticContext
        {
            Config = new CopilotConfig
            {
                McpBearerToken = secret,
                BackendSyncUrl = $"https://backend.test/{secret}",
                ExternalMcpServers = [new CopilotMcpClientServerConfig { Endpoint = $"https://mcp.test/{secret}" }],
            },
            State = new CopilotChatState(),
            SelectedProfile = profile,
            ConfigFilePath = path,
            CodexConfigOptions = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
            {
                ConfiguredAutoReviewPolicy = secret,
                ConfiguredShellEnvironmentPolicy = new CopilotCodexShellEnvironmentPolicy
                {
                    Set = new Dictionary<string, string> { ["PRIVATE_NAME"] = secret },
                },
            },
        });

        Assert.Contains("端点：https://example.test:8443 · 凭据 已配置", report, StringComparison.Ordinal);
        Assert.Contains("profile-model", report, StringComparison.Ordinal);
        Assert.Contains($"系统提示 Profile 覆盖（{secret.Length.ToString("N0", CultureInfo.CurrentCulture)} 字符）", report, StringComparison.Ordinal);
        Assert.Contains("set=1", report, StringComparison.Ordinal);
        Assert.Contains("自动复核策略：", report, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, report, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_NAME", report, StringComparison.Ordinal);
        Assert.DoesNotContain("backend.test", report, StringComparison.Ordinal);
        Assert.DoesNotContain("mcp.test", report, StringComparison.Ordinal);
        Assert.DoesNotContain("config.toml", report, StringComparison.Ordinal);
        Assert.Equal(originalConfig, File.ReadAllText(path));
    }

    [Theory]
    [InlineData(null, "FileMissing", "当前文件不存在")]
    [InlineData("{}", "SectionMissing", "当前无 CopilotConfig 节")]
    [InlineData("{broken", "InvalidJson", "JSON 无法解析")]
    public void UnusableCurrentFilesDoNotInventPersistedProfileOrAgentSources(
        string? content,
        string expectedState,
        string expectedStatus)
    {
        var path = Path.Combine(_directory, "ColorVision.config.json");
        if (content != null)
            File.WriteAllText(path, content);
        var report = CopilotEffectiveConfigDiagnostics.Format(new CopilotEffectiveConfigDiagnosticContext
        {
            Config = new CopilotConfig(),
            State = new CopilotChatState(),
            SelectedProfile = new CopilotProfileConfig { Name = "In memory", Model = "memory-model" },
            ConfigFilePath = path,
        });

        Assert.Equal(expectedState, CopilotEffectiveConfigDiagnostics.ProbeConfigFile(path).State.ToString());
        Assert.Contains(expectedStatus, report, StringComparison.Ordinal);
        Assert.Contains("已加载运行时 Profile（当前文件来源未证实）", report, StringComparison.Ordinal);
        Assert.Contains("来源 已加载运行时值（当前文件来源未证实）", report, StringComparison.Ordinal);
        Assert.DoesNotContain("来源 应用配置 CopilotConfig.AgentDefaults", report, StringComparison.Ordinal);
    }

    [Fact]
    public void ProbeKeepsTheSixteenMiBReadBoundaryAndFutureSchemaWriteBlock()
    {
        var oversizedPath = Path.Combine(_directory, "oversized.json");
        using (var stream = File.Create(oversizedPath))
            stream.SetLength(16L * 1024 * 1024 + 1);
        Assert.Equal(CopilotConfigFileProbeState.TooLarge,
            CopilotEffectiveConfigDiagnostics.ProbeConfigFile(oversizedPath).State);

        var futureSchema = CopilotConfig.CurrentSchemaVersion + 1;
        var futurePath = Path.Combine(_directory, "future.json");
        File.WriteAllText(futurePath, $$"""{ "CopilotConfig": { "SchemaVersion": {{futureSchema}} } }""");
        var report = CopilotEffectiveConfigDiagnostics.Format(new CopilotEffectiveConfigDiagnosticContext
        {
            Config = new CopilotConfig { SchemaVersion = futureSchema },
            State = new CopilotChatState(),
            ConfigFilePath = futurePath,
            StateLoadStatus = new CopilotChatStateLoadStatus(CopilotChatStateLoadSource.FutureVersion),
        });

        Assert.Contains("更高版本阻止 CopilotConfig 写入", report, StringComparison.Ordinal);
        Assert.Contains("会话状态 · 更高版本阻止写入", report, StringComparison.Ordinal);
    }

    [Fact]
    public void HostPolicySnapshotReportsEffectiveReviewerAndSandboxWithoutExternalSourceLabels()
    {
        var report = CopilotEffectiveConfigDiagnostics.Format(new CopilotEffectiveConfigDiagnosticContext
        {
            Config = new CopilotConfig(),
            State = new CopilotChatState(),
            CodexConfigOptions = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
            {
                ConfiguredSandboxMode = CopilotCodexSandboxMode.ReadOnly,
                ConfiguredGuardianApprovalEnabled = false,
                ConfiguredApprovalsReviewer = CopilotCodexApprovalsReviewer.AutoReview,
            },
        });

        Assert.Contains("宿主策略（当前提交快照 · 来源 ColorVision）", report, StringComparison.Ordinal);
        Assert.Contains("sandbox_mode：read-only", report, StringComparison.Ordinal);
        Assert.Contains("guardian_approval：false", report, StringComparison.Ordinal);
        Assert.Contains("approvals_reviewer（有效）：user", report, StringComparison.Ordinal);
        Assert.Contains("按需确认 · 内置安全默认", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Codex config", report, StringComparison.Ordinal);
    }

    private string WriteConfigMetadata()
    {
        var path = Path.Combine(_directory, "ColorVision.config.json");
        File.WriteAllText(path, $$"""
            { "CopilotConfig": {
                "SchemaVersion": {{CopilotConfig.CurrentSchemaVersion}},
                "Profiles": [{ "Id": "local-profile" }],
                "AgentDefaults": {},
                "McpEnabled": false
            } }
            """);
        return path;
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
