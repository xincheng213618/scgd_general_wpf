#pragma warning disable CA1707
using ColorVision.Copilot;
using System;
using System.Linq;

namespace ColorVision.UI.Tests;

public sealed class CopilotProfileConfigTests
{
    [Fact]
    public void EnsureValid_ReplacesLegacyChineseDefaultSystemPrompt()
    {
        var profile = new CopilotProfileConfig
        {
            SystemPrompt = "\u4f60\u662f ColorVision Copilot\uff0c\u662f ColorVision \u8f6f\u4ef6\u5185\u7f6e\u7684\u5de5\u7a0b\u52a9\u624b\u3002\u4e0d\u8981\u58f0\u79f0\u81ea\u5df1\u5df2\u7ecf\u6267\u884c\u4e86\u672a\u7531\u5e94\u7528\u4e0a\u4e0b\u6587\u660e\u786e\u63d0\u4f9b\u7684\u64cd\u4f5c\u3002",
        };

        Assert.True(profile.EnsureValid());
        Assert.Equal(CopilotProfileConfig.DefaultSystemPrompt, profile.SystemPrompt);
    }

    [Fact]
    public void EnsureValid_KeepsCustomSystemPrompt()
    {
        var profile = new CopilotProfileConfig
        {
            VendorType = CopilotVendorType.DeepSeek,
            SystemPrompt = "\u8bf7\u7528\u4e2d\u6587\u56de\u7b54\uff0c\u4f46\u4fdd\u6301\u5b89\u5168\u8fb9\u754c\u3002",
        };

        Assert.False(profile.EnsureValid());
        Assert.Equal("\u8bf7\u7528\u4e2d\u6587\u56de\u7b54\uff0c\u4f46\u4fdd\u6301\u5b89\u5168\u8fb9\u754c\u3002", profile.SystemPrompt);
    }

    [Fact]
    public void VendorOptions_UseEnglishLabels()
    {
        var labels = CopilotVendorCatalog.VendorOptions.Select(option => option.Label).ToArray();

        Assert.Contains("Custom", labels);
        Assert.Contains("Zhipu GLM", labels);
        Assert.Contains("Xiaomi Mimo", labels);
        Assert.DoesNotContain("\u81ea\u5b9a\u4e49", labels, StringComparer.Ordinal);
        Assert.DoesNotContain("GLM / \u667a\u8c31", labels, StringComparer.Ordinal);
        Assert.DoesNotContain("\u5c0f\u7c73 Mimo", labels, StringComparer.Ordinal);
    }

    [Fact]
    public void EnsureInitialized_RemovesExpiredTemporaryProfilesAndKeepsDefaultProfile()
    {
        var config = new CopilotConfig
        {
            McpBearerToken = "test-token",
            McpPort = CopilotConfig.DefaultMcpPort,
        };
        config.Profiles.Add(new CopilotProfileConfig
        {
            Id = "builtin-minimax-trial-20260527",
            Name = "MiniMax 2 Trial",
            BaseUrl = "https://example.invalid",
            Model = "expired-trial-model",
            ApiKey = "expired-trial-key",
        });

        Assert.True(config.EnsureInitialized());
        Assert.DoesNotContain(config.Profiles, profile => string.Equals(profile.Id, "builtin-minimax-trial-20260527", StringComparison.Ordinal));
        Assert.NotEmpty(config.Profiles);
        Assert.Contains(config.Profiles, profile => string.Equals(profile.Name, "DeepSeek Default", StringComparison.Ordinal));
    }
}
