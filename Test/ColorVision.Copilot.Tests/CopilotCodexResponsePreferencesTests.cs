using ColorVision.Copilot;
using System;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexResponsePreferencesTests
{
    [Fact]
    public void ClosestTrustedFastModeGateIsFrozenAndDropsTheAgentServiceTier()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                service_tier = "fast"

                [features]
                fast_mode = true

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string configDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(configDirectory);
            string configPath = Path.Combine(configDirectory, "config.toml");
            File.WriteAllText(
                configPath,
                "service_tier = \"flex\"\n\n[features]\nfast_mode = false");

            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            File.WriteAllText(
                configPath,
                "service_tier = \"scale\"\n\n[features]\nfast_mode = true");
            var plan = CopilotAgentRequestFactory.Prepare(
                "Inspect the workspace.",
                CopilotAgentMode.Auto,
                submittedContext);
            var request = CopilotAgentRequestFactory.Create(
                plan,
                new CopilotAgentRequestBuildInput
                {
                    Profile = CreateOfficialOpenAiProfile(),
                    AgentDefaults = new CopilotAgentDefaultsConfig(),
                });
            var refreshed = CopilotProjectInstructionDiscoveryConfig.Load(
                globalRoot,
                projectRoot);
            var submitted = submittedContext.ProjectInstructionDiscoveryOptions;

            Assert.False(submitted.ConfiguredFastModeEnabled);
            Assert.True(submitted.HasFastModeEnabledOverride);
            Assert.Equal(CopilotProjectInstructionConfigSources.TrustedProject, submitted.FastModeEnabledSource);
            Assert.Equal("flex", submitted.ConfiguredServiceTier);
            Assert.False(plan.CodexFastModeEnabled);
            Assert.Equal("flex", plan.CodexServiceTier);
            Assert.False(request.CodexFastModeEnabled);
            Assert.Equal(string.Empty, request.CodexServiceTier);
            Assert.True(refreshed.ConfiguredFastModeEnabled);
            Assert.Equal("scale", refreshed.ConfiguredServiceTier);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void ClosestTrustedResponsePreferencesAreFrozenIntoTheAgentRequest()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                service_tier = "fast"
                model_verbosity = "high"

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string configDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(configDirectory);
            string configPath = Path.Combine(configDirectory, "config.toml");
            File.WriteAllText(
                configPath,
                "service_tier = \"flex\"\nmodel_verbosity = \"low\"");

            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            File.WriteAllText(
                configPath,
                "service_tier = \"scale\"\nmodel_verbosity = \"medium\"");
            var plan = CopilotAgentRequestFactory.Prepare(
                "Inspect the workspace.",
                CopilotAgentMode.Auto,
                submittedContext);
            var request = CopilotAgentRequestFactory.Create(
                plan,
                new CopilotAgentRequestBuildInput
                {
                    Profile = CreateOfficialOpenAiProfile(),
                    AgentDefaults = new CopilotAgentDefaultsConfig(),
                });
            var refreshed = CopilotProjectInstructionDiscoveryConfig.Load(
                globalRoot,
                projectRoot);
            var submitted = submittedContext.ProjectInstructionDiscoveryOptions;

            Assert.Equal("flex", submitted.ConfiguredServiceTier);
            Assert.Equal(CopilotCodexModelVerbosity.Low, submitted.ConfiguredModelVerbosity);
            Assert.Equal(CopilotProjectInstructionConfigSources.TrustedProject, submitted.ServiceTierSource);
            Assert.Equal(CopilotProjectInstructionConfigSources.TrustedProject, submitted.ModelVerbositySource);
            Assert.Equal("flex", plan.CodexServiceTier);
            Assert.Equal(CopilotCodexModelVerbosity.Low, plan.CodexModelVerbosity);
            Assert.Equal("flex", request.CodexServiceTier);
            Assert.Equal(CopilotCodexModelVerbosity.Low, request.CodexModelVerbosity);
            Assert.Equal("scale", refreshed.ConfiguredServiceTier);
            Assert.Equal(CopilotCodexModelVerbosity.Medium, refreshed.ConfiguredModelVerbosity);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void UntrustedOrInvalidResponsePreferencesCannotReplaceTheCodexHomeContract()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                service_tier = "fast"
                model_verbosity = "high"

                [features]
                fast_mode = true

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            string configDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(configDirectory);
            File.WriteAllText(
                Path.Combine(configDirectory, "config.toml"),
                "service_tier = \"flex\"\nmodel_verbosity = \"low\"\n\n[features]\nfast_mode = false");

            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);
            Assert.True(untrusted.ConfiguredFastModeEnabled);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, untrusted.FastModeEnabledSource);
            Assert.Equal("fast", untrusted.ConfiguredServiceTier);
            Assert.Equal(CopilotCodexModelVerbosity.High, untrusted.ConfiguredModelVerbosity);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, untrusted.ServiceTierSource);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, untrusted.ModelVerbositySource);
            Assert.Empty(untrusted.AppliedProjectConfigFilePaths);

            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                "service_tier = \"priority tier\"\nmodel_verbosity = \"detailed\"\n\n[features]\nfast_mode = \"disabled\"");
            var invalid = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);
            Assert.True(invalid.ConfiguredFastModeEnabled);
            Assert.False(invalid.HasFastModeEnabledOverride);
            Assert.False(invalid.HasServiceTierOverride);
            Assert.False(invalid.HasModelVerbosityOverride);
            Assert.Equal(string.Empty, invalid.ConfiguredServiceTier);
            Assert.Equal(CopilotCodexModelVerbosity.Unspecified, invalid.ConfiguredModelVerbosity);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("fast", "fast", "priority")]
    [InlineData("FLEX", "flex", "flex")]
    [InlineData("model.tier-2", "model.tier-2", "model.tier-2")]
    public void ServiceTierNormalizationPreservesAdvertisedTokensAndMapsFast(
        string value,
        string expectedConfigured,
        string expectedRequest)
    {
        Assert.True(CopilotCodexServiceTierSelection.TryNormalize(value, out var configured));
        Assert.Equal(expectedConfigured, configured);
        Assert.Equal(expectedRequest, CopilotCodexServiceTierSelection.GetRequestToken(configured));
    }

    [Fact]
    public void ResponsePreferenceDiagnosticsExposeValuesSourcesAndWireMapping()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredServiceTier = "fast",
            HasServiceTierOverride = true,
            ServiceTierSource = CopilotProjectInstructionConfigSources.CodexHome,
            ConfiguredModelVerbosity = CopilotCodexModelVerbosity.High,
            HasModelVerbosityOverride = true,
            ModelVerbositySource = CopilotProjectInstructionConfigSources.CodexHome,
        };
        string memoryReport = CopilotProjectInstructionDiagnostics.Format(
            new CopilotProjectInstructionSnapshot(
                string.Empty,
                string.Empty,
                string.Empty,
                options,
                Array.Empty<CopilotProjectInstructionDocument>()),
            hasActiveAgentRun: false);
        string contextReport = CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
        {
            CodexServiceTier = options.ConfiguredServiceTier,
            HasCodexServiceTierOverride = true,
            CodexServiceTierSourceLabel = options.ServiceTierSourceLabel,
            CodexModelVerbosity = options.ConfiguredModelVerbosity,
            HasCodexModelVerbosityOverride = true,
            CodexModelVerbositySourceLabel = options.ModelVerbositySourceLabel,
        });
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                CodexConfigOptions = options,
            });

        Assert.Contains("Codex service_tier：fast → 请求 priority", memoryReport, StringComparison.Ordinal);
        Assert.Contains("Codex model_verbosity：high", memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.ServiceTierSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains("服务等级：fast → 请求 priority", contextReport, StringComparison.Ordinal);
        Assert.Contains("回答详细度：high", contextReport, StringComparison.Ordinal);
        Assert.Contains("Codex service_tier：fast → 请求 priority", debugReport, StringComparison.Ordinal);
        Assert.Contains("Codex model_verbosity：high", debugReport, StringComparison.Ordinal);
        Assert.Contains("仅 Agent 官方 OpenAI Responses 生效", memoryReport, StringComparison.Ordinal);
        Assert.Contains("仅 Agent 官方 OpenAI Responses 生效", contextReport, StringComparison.Ordinal);
        Assert.Contains("仅 Agent 官方 OpenAI Responses 生效", debugReport, StringComparison.Ordinal);
    }

    [Fact]
    public void FastModeDiagnosticsExplainThatConfiguredServiceTierWillNotBeSent()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredFastModeEnabled = false,
            HasFastModeEnabledOverride = true,
            FastModeEnabledSource = CopilotProjectInstructionConfigSources.CodexHome,
            ConfiguredServiceTier = "fast",
            HasServiceTierOverride = true,
            ServiceTierSource = CopilotProjectInstructionConfigSources.CodexHome,
        };
        string memoryReport = CopilotProjectInstructionDiagnostics.Format(
            new CopilotProjectInstructionSnapshot(
                string.Empty,
                string.Empty,
                string.Empty,
                options,
                Array.Empty<CopilotProjectInstructionDocument>()),
            hasActiveAgentRun: false);
        string contextReport = CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
        {
            CodexFastModeEnabled = false,
            HasCodexFastModeEnabledOverride = true,
            CodexFastModeEnabledSourceLabel = options.FastModeEnabledSourceLabel,
            CodexServiceTier = options.ConfiguredServiceTier,
            HasCodexServiceTierOverride = true,
            CodexServiceTierSourceLabel = options.ServiceTierSourceLabel,
        });
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                CodexConfigOptions = options,
            });

        Assert.Contains("Codex features.fast_mode：false", memoryReport, StringComparison.Ordinal);
        Assert.Contains("Codex service_tier：fast → 不发送", memoryReport, StringComparison.Ordinal);
        Assert.Contains("快速服务等级总闸门：关闭", contextReport, StringComparison.Ordinal);
        Assert.Contains("服务等级：fast → 不发送", contextReport, StringComparison.Ordinal);
        Assert.Contains("Codex features.fast_mode：false", debugReport, StringComparison.Ordinal);
        Assert.Contains("Codex service_tier：fast → 不发送", debugReport, StringComparison.Ordinal);
    }

    private static CopilotProfileConfig CreateOfficialOpenAiProfile()
    {
        return new CopilotProfileConfig
        {
            VendorType = CopilotVendorType.OpenAI,
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "test-key",
            BaseUrl = "https://api.openai.com/v1",
            Model = "gpt-5.5",
            MaxTokens = 4_096,
        };
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-response-preferences-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
