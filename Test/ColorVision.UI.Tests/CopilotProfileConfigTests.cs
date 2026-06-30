#pragma warning disable CA1707
using ColorVision.Copilot;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace ColorVision.UI.Tests;

public sealed class CopilotProfileConfigTests
{
    [Fact]
    public void Deserialize_IgnoresSystemPromptField()
    {
        var profile = JsonConvert.DeserializeObject<CopilotProfileConfig>(
            JsonConvert.SerializeObject(new { VendorType = CopilotVendorType.DeepSeek, SystemPrompt = "legacy prompt" }))!;

        Assert.False(profile.EnsureValid());
        Assert.Equal(string.Empty, profile.CustomSystemPrompt);
        Assert.Equal(CopilotProfileConfig.DefaultSystemPrompt, profile.SystemPrompt);
    }

    [Fact]
    public void DefaultSystemPrompt_HidesMissingContextAndForbidsProjectGuessing()
    {
        Assert.Contains("do not guess or invent project-specific implementation details", CopilotProfileConfig.DefaultSystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not create a visible section about missing context", CopilotProfileConfig.DefaultSystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not ask the user to provide files", CopilotProfileConfig.DefaultSystemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Serialize_DoesNotPersistBuiltInSystemPrompt()
    {
        var profile = new CopilotProfileConfig
        {
            CustomSystemPrompt = "\u7528\u7b80\u4f53\u4e2d\u6587\u56de\u7b54\u3002",
        };

        var json = JsonConvert.SerializeObject(profile);
        var root = JObject.Parse(json);

        Assert.Null(root.Property("SystemPrompt"));
        Assert.Equal("\u7528\u7b80\u4f53\u4e2d\u6587\u56de\u7b54\u3002", root.Value<string>("CustomSystemPrompt"));
    }

    [Fact]
    public void UseSystemPromptOverride_DoesNotReplaceCustomPrompt()
    {
        var profile = new CopilotProfileConfig
        {
            CustomSystemPrompt = "\u7528\u4e2d\u6587\u56de\u7b54\u3002",
        };

        profile.UseSystemPromptOverride("Return only a short title.");

        Assert.Equal("Return only a short title.", profile.EffectiveSystemPrompt);
        Assert.Equal("\u7528\u4e2d\u6587\u56de\u7b54\u3002", profile.CustomSystemPrompt);
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
