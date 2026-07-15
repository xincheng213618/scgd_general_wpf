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
    public void Serialize_ProfileDoesNotPersistAgentDefaultsOrInternalModelPolicy()
    {
        var profile = new CopilotProfileConfig
        {
            MaxTokens = 256,
            Temperature = 0.8,
        };

        var json = JsonConvert.SerializeObject(profile);
        var root = JObject.Parse(json);

        Assert.Null(root.Property("SystemPrompt"));
        Assert.Null(root.Property("CustomSystemPrompt"));
        Assert.Null(root.Property("MaxTokens"));
        Assert.Null(root.Property("MaxToolRounds"));
        Assert.Null(root.Property("AgentRequestTokenBudget"));
        Assert.Null(root.Property("MaxAgentPasses"));
        Assert.Null(root.Property("AgentTimeoutSeconds"));
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
    public void Deserialize_IgnoresRemovedAgentAndModelPolicyFields()
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
        Assert.Equal(CopilotProfileConfig.DefaultTemperature, profile.Temperature);
        var serialized = JObject.Parse(JsonConvert.SerializeObject(profile));
        Assert.Null(serialized.Property("MaxToolRounds"));
        Assert.Null(serialized.Property("AgentRequestTokenBudget"));
        Assert.Null(serialized.Property("MaxAgentPasses"));
        Assert.Null(serialized.Property("AgentTimeoutSeconds"));
    }

    [Fact]
    public void RunBudget_ResolvesRequestOverrideBeforeIndependentDefaultsAndSafetyLimits()
    {
        var defaults = new CopilotAgentRunBudgetDefaults
        {
            ContextWindowTokens = 800_000,
            RequestTokenBudget = 96_000,
            MaxToolCalls = 8,
            MaxAgentPasses = 6,
            TotalDuration = TimeSpan.FromSeconds(240),
        };
        var resolved = CopilotAgentRunBudget.Resolve(new CopilotAgentRequest
        {
            Profile = new CopilotProfileConfig(),
            RunBudgetDefaults = defaults,
            RunBudgetOverride = new CopilotAgentRunBudgetOverride
            {
                RequestTokenBudget = 20_000,
                MaxAgentPasses = 2,
                TotalDuration = TimeSpan.FromSeconds(30),
            },
        });

        Assert.Equal(20_000, resolved.RequestTokenBudget);
        Assert.Equal(800_000, resolved.ContextWindowTokens);
        Assert.Equal(8, resolved.MaxToolCalls);
        Assert.Equal(2, resolved.MaxAgentPasses);
        Assert.Equal(TimeSpan.FromSeconds(30), resolved.TotalDuration);

        var clamped = CopilotAgentRunBudget.Resolve(new CopilotAgentRequest
        {
            Profile = new CopilotProfileConfig(),
            RunBudgetDefaults = defaults,
            RunBudgetOverride = new CopilotAgentRunBudgetOverride
            {
                ContextWindowTokens = 1,
                RequestTokenBudget = 1,
                MaxToolCalls = int.MaxValue,
                MaxAgentPasses = 0,
                TotalDuration = TimeSpan.Zero,
            },
        });

        Assert.Equal(CopilotAgentTokenBudget.MinimumContextWindowTokens, clamped.ContextWindowTokens);
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
    public void Config_PersistsIndependentAgentDefaults()
    {
        var config = new CopilotConfig
        {
            AgentDefaults = new CopilotAgentDefaultsConfig
            {
                ContextWindowTokens = 950_000,
                RequestTokenBudget = 900_000,
                MaxToolCalls = 96,
                MaxAgentPasses = 24,
                TimeoutSeconds = 5_400,
                PreferredShell = CopilotShellKind.CommandPrompt,
            },
            McpBearerToken = "test-token",
        };

        var json = JsonConvert.SerializeObject(config);
        var restored = JsonConvert.DeserializeObject<CopilotConfig>(json)!;

        Assert.Equal(950_000, restored.AgentDefaults.ContextWindowTokens);
        Assert.Equal(900_000, restored.AgentDefaults.RequestTokenBudget);
        Assert.Equal(96, restored.AgentDefaults.MaxToolCalls);
        Assert.Equal(24, restored.AgentDefaults.MaxAgentPasses);
        Assert.Equal(5_400, restored.AgentDefaults.TimeoutSeconds);
        Assert.Equal(CopilotShellKind.CommandPrompt, restored.AgentDefaults.PreferredShell);
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
    public void EnsureInitialized_UsesLargeIndependentAgentDefaultsWithoutProfileMigration()
    {
        var config = new CopilotConfig
        {
            McpBearerToken = "test-token",
            SchemaVersion = 0,
        };
        config.Profiles.Add(new CopilotProfileConfig
        {
            Id = "model-only-profile",
            Name = "Model Only Profile",
        });

        Assert.True(config.EnsureInitialized());
        Assert.Equal(CopilotConfig.CurrentSchemaVersion, config.SchemaVersion);
        Assert.Equal(1_048_576, config.AgentDefaults.ContextWindowTokens);
        Assert.Equal(1_048_576, config.AgentDefaults.RequestTokenBudget);
        Assert.Equal(128, config.AgentDefaults.MaxToolCalls);
        Assert.Equal(32, config.AgentDefaults.MaxAgentPasses);
        Assert.Equal(7_200, config.AgentDefaults.TimeoutSeconds);
        Assert.Equal(CopilotShellKind.Auto, config.AgentDefaults.PreferredShell);
        Assert.NotNull(config.FindProfile("model-only-profile"));

        Assert.False(config.EnsureInitialized());
    }
}
