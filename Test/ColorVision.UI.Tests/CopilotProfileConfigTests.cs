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
    public void Serialize_PersistsAgentBudgetsButNotInternalModelPolicy()
    {
        var profile = new CopilotProfileConfig
        {
            MaxTokens = 256,
            MaxToolRounds = 3,
            AgentRequestTokenBudget = 48_000,
            MaxAgentPasses = 5,
            AgentTimeoutSeconds = 180,
            Temperature = 0.8,
        };

        var json = JsonConvert.SerializeObject(profile);
        var root = JObject.Parse(json);

        Assert.Null(root.Property("SystemPrompt"));
        Assert.Null(root.Property("CustomSystemPrompt"));
        Assert.Null(root.Property("MaxTokens"));
        Assert.Equal(3, root.Value<int>("MaxToolRounds"));
        Assert.Equal(48_000, root.Value<int>("AgentRequestTokenBudget"));
        Assert.Equal(5, root.Value<int>("MaxAgentPasses"));
        Assert.Equal(180, root.Value<int>("AgentTimeoutSeconds"));
        Assert.Null(root.Property("Temperature"));
        Assert.Null(root.Property("UseAgentFramework"));
    }

    [Fact]
    public void UseSystemPromptOverride_IsRuntimeOnly()
    {
        var profile = new CopilotProfileConfig();

        profile.UseSystemPromptOverride("Return only a short title.");

        Assert.Equal("Return only a short title.", profile.EffectiveSystemPrompt);
        Assert.Null(JObject.Parse(JsonConvert.SerializeObject(profile)).Property("SystemPrompt"));
    }

    [Fact]
    public void Deserialize_LoadsAgentBudgetsAndIgnoresRemovedModelPolicy()
    {
        const string json = """
            {
              "CustomSystemPrompt": "legacy custom prompt",
              "MaxTokens": 128,
              "MaxToolRounds": 2,
              "AgentRequestTokenBudget": 48000,
              "MaxAgentPasses": 3,
              "AgentTimeoutSeconds": 90,
              "Temperature": 1.5,
              "UseAgentFramework": false
            }
            """;

        var profile = JsonConvert.DeserializeObject<CopilotProfileConfig>(json)!;

        Assert.Equal(CopilotProfileConfig.DefaultSystemPrompt, profile.EffectiveSystemPrompt);
        Assert.Equal(CopilotProfileConfig.DefaultMaxTokens, profile.MaxTokens);
        Assert.Equal(2, profile.MaxToolRounds);
        Assert.Equal(48_000, profile.AgentRequestTokenBudget);
        Assert.Equal(3, profile.MaxAgentPasses);
        Assert.Equal(90, profile.AgentTimeoutSeconds);
        Assert.Equal(CopilotProfileConfig.DefaultTemperature, profile.Temperature);
    }

    [Fact]
    public void RunBudget_ResolvesRequestOverrideBeforeProfileDefaultsAndSafetyLimits()
    {
        var profile = new CopilotProfileConfig
        {
            AgentRequestTokenBudget = 96_000,
            MaxToolRounds = 8,
            MaxAgentPasses = 6,
            AgentTimeoutSeconds = 240,
        };
        var resolved = CopilotAgentRunBudget.Resolve(new CopilotAgentRequest
        {
            Profile = profile,
            RunBudgetOverride = new CopilotAgentRunBudgetOverride
            {
                RequestTokenBudget = 20_000,
                MaxAgentPasses = 2,
                TotalDuration = TimeSpan.FromSeconds(30),
            },
        });

        Assert.Equal(20_000, resolved.RequestTokenBudget);
        Assert.Equal(8, resolved.MaxToolCalls);
        Assert.Equal(2, resolved.MaxAgentPasses);
        Assert.Equal(TimeSpan.FromSeconds(30), resolved.TotalDuration);

        var clamped = CopilotAgentRunBudget.Resolve(new CopilotAgentRequest
        {
            Profile = profile,
            RunBudgetOverride = new CopilotAgentRunBudgetOverride
            {
                RequestTokenBudget = 1,
                MaxToolCalls = int.MaxValue,
                MaxAgentPasses = 0,
                TotalDuration = TimeSpan.Zero,
            },
        });

        Assert.Equal(CopilotAgentRunBudget.MinimumRequestTokenBudget, clamped.RequestTokenBudget);
        Assert.Equal(CopilotAgentRunBudget.MaximumToolCalls, clamped.MaxToolCalls);
        Assert.Equal(CopilotAgentRunBudget.MinimumAgentPasses, clamped.MaxAgentPasses);
        Assert.Equal(CopilotAgentRunBudget.MinimumTotalDuration, clamped.TotalDuration);
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

    [Fact]
    public void Config_PersistsPreferredShellAndRepairsInvalidValue()
    {
        var config = new CopilotConfig
        {
            PreferredShell = CopilotShellKind.CommandPrompt,
            McpBearerToken = "test-token",
        };

        var json = JsonConvert.SerializeObject(config);
        var restored = JsonConvert.DeserializeObject<CopilotConfig>(json)!;

        Assert.Equal(CopilotShellKind.CommandPrompt, restored.PreferredShell);
        restored.PreferredShell = (CopilotShellKind)999;
        Assert.True(restored.EnsureInitialized());
        Assert.Equal(CopilotShellKind.Auto, restored.PreferredShell);
    }

    [Fact]
    public void Config_NormalizesAndBoundsDisabledPluginSubagentRoles()
    {
        var config = new CopilotConfig
        {
            McpBearerToken = "test-token",
            DisabledPluginSubagentRoles = new System.Collections.ObjectModel.ObservableCollection<string>(
                Enumerable.Range(0, 300).Select(index => $"sample.plugin/reviewer-{index:000}")),
        };
        config.DisabledPluginSubagentRoles.Add(" SAMPLE.PLUGIN/REVIEWER-000 ");
        config.DisabledPluginSubagentRoles.Add("not-a-role-key");

        Assert.True(config.EnsureInitialized());
        Assert.Equal(256, config.DisabledPluginSubagentRoles.Count);
        Assert.Equal("sample.plugin/reviewer-000", config.DisabledPluginSubagentRoles[0]);
        Assert.DoesNotContain("not-a-role-key", config.DisabledPluginSubagentRoles);

        var json = JsonConvert.SerializeObject(config);
        var restored = JsonConvert.DeserializeObject<CopilotConfig>(json)!;
        Assert.Contains("sample.plugin/reviewer-000", restored.DisabledPluginSubagentRoles);
    }

    [Fact]
    public void EnsureInitialized_MigratesLegacyAgentRequestTokenBudgetOnce()
    {
        var config = new CopilotConfig
        {
            McpBearerToken = "test-token",
            SchemaVersion = 0,
        };
        config.Profiles.Add(new CopilotProfileConfig
        {
            Id = "legacy-agent-budget",
            Name = "Legacy Agent Budget",
            AgentRequestTokenBudget = 65_536,
        });
        config.Profiles.Add(new CopilotProfileConfig
        {
            Id = "custom-agent-budget",
            Name = "Custom Agent Budget",
            AgentRequestTokenBudget = 96_000,
        });

        Assert.True(config.EnsureInitialized());
        Assert.Equal(CopilotConfig.CurrentSchemaVersion, config.SchemaVersion);
        Assert.Equal(CopilotProfileConfig.DefaultAgentRequestTokenBudget, config.FindProfile("legacy-agent-budget")!.AgentRequestTokenBudget);
        Assert.Equal(96_000, config.FindProfile("custom-agent-budget")!.AgentRequestTokenBudget);

        Assert.False(config.EnsureInitialized());
    }
}
