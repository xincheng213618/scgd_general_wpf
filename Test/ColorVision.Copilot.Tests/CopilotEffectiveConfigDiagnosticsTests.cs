using ColorVision.UI;
using System.Globalization;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotEffectiveConfigDiagnosticsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"copilot-effective-config-{Guid.NewGuid():N}");

    public CopilotEffectiveConfigDiagnosticsTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void ReportRedactsConfigurationSecretsAndUsesTheSelectedProfile()
    {
        const string secret = "private-value-must-not-appear";
        var path = WriteConfig();
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
        File.WriteAllText(futurePath, $$"""{ "{{typeof(CopilotConfig).FullName}}": { "SchemaVersion": {{futureSchema}} } }""");
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

    private string WriteConfig(CopilotConfig? config = null)
    {
        var path = Path.Combine(_directory, "ColorVision.config.json");
        var handler = new ConfigHandler { ConfigFilePath = path, IsAutoSave = false };
        config ??= new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            Profiles = [new CopilotProfileConfig { Id = "local-profile" }],
        };
        Assert.True(handler.TrySave(config, out var errorMessage), errorMessage);
        return path;
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
