using ColorVision.Copilot;
using System;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexAutoCompactionTests
{
    [Fact]
    public void UntrustedOrInvalidAutoCompactionSettingsCannotReplaceTheEffectiveValues()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                model_auto_compact_token_limit = 128_000
                model_auto_compact_token_limit_scope = "total"

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            string configDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(configDirectory);
            File.WriteAllText(
                Path.Combine(configDirectory, "config.toml"),
                """
                model_auto_compact_token_limit = 64_000
                model_auto_compact_token_limit_scope = "body_after_prefix"
                """);

            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);
            Assert.Equal(128_000, untrusted.ConfiguredModelAutoCompactTokenLimit);
            Assert.Equal(
                CopilotModelAutoCompactTokenLimitScope.Total,
                untrusted.EffectiveModelAutoCompactTokenLimitScope);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, untrusted.ModelAutoCompactTokenLimitSource);
            Assert.Empty(untrusted.AppliedProjectConfigFilePaths);

            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                """
                model_auto_compact_token_limit = 0
                model_auto_compact_token_limit_scope = "prefix"
                """);
            var invalid = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);
            Assert.False(invalid.HasModelAutoCompactTokenLimitOverride);
            Assert.False(invalid.HasModelAutoCompactTokenLimitScopeOverride);
            Assert.Equal(
                CopilotModelAutoCompactTokenLimitScope.Total,
                invalid.EffectiveModelAutoCompactTokenLimitScope);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void BodyAfterPrefixExcludesTheCarriedCompactionSummaryFromTheTokenThreshold()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "First request"));
        var boundary = new CopilotChatMessage(CopilotChatRole.Assistant, "First response");
        conversation.Messages.Add(boundary);
        conversation.Compaction = new CopilotConversationCompaction
        {
            StrategyVersion = CopilotConversationCompaction.CurrentStrategyVersion,
            Summary = new string('s', 2_000),
            ThroughMessageId = boundary.Id,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            SourceMessageCount = 2,
            SourceCharacters = 2_100,
        };
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, new string('u', 80)));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, new string('a', 80)));
        var limits = new CopilotConversationHistoryLimits(64, 100_000, 50_000);

        var total = CopilotConversationAutoCompactionPolicy.Evaluate(
            conversation,
            limits,
            pendingPrompt: "continue",
            new CopilotConversationAutoCompactionOptions(
                Enabled: true,
                ThresholdPercent: 85,
                ModelTokenLimit: 200,
                ModelTokenLimitScope: CopilotModelAutoCompactTokenLimitScope.Total));
        var body = CopilotConversationAutoCompactionPolicy.Evaluate(
            conversation,
            limits,
            pendingPrompt: "continue",
            new CopilotConversationAutoCompactionOptions(
                Enabled: true,
                ThresholdPercent: 85,
                ModelTokenLimit: 200,
                ModelTokenLimitScope: CopilotModelAutoCompactTokenLimitScope.BodyAfterPrefix));
        var usage = CopilotConversationAutoCompactionPolicy.Measure(conversation, limits, "continue");

        Assert.True(total.ShouldCompact);
        Assert.Equal(CopilotConversationAutoCompactionTrigger.ConfiguredTokenLimit, total.Trigger);
        Assert.True(total.EvaluatedTokens >= total.ThresholdTokens);
        Assert.False(body.ShouldCompact);
        Assert.True(body.EvaluatedTokens < body.ThresholdTokens);
        Assert.True(usage.CarriedPrefixWeight > 0);
        Assert.Equal(usage.ActiveWeight, usage.CarriedPrefixWeight + usage.BodyAfterPrefixWeight);
    }

    [Fact]
    public void AutoCompactionDiagnosticsExposeTheConfiguredThresholdScopeAndCurrentCounters()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredModelAutoCompactTokenLimit = 64_000,
            HasModelAutoCompactTokenLimitOverride = true,
            ModelAutoCompactTokenLimitSource = CopilotProjectInstructionConfigSources.CodexHome,
            ConfiguredModelAutoCompactTokenLimitScope = CopilotModelAutoCompactTokenLimitScope.BodyAfterPrefix,
            HasModelAutoCompactTokenLimitScopeOverride = true,
            ModelAutoCompactTokenLimitScopeSource = CopilotProjectInstructionConfigSources.TrustedProject,
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
            AutoCompactConversationHistory = true,
            ConfiguredModelAutoCompactTokenLimit = 64_000,
            HasModelAutoCompactTokenLimitOverride = true,
            ModelAutoCompactTokenLimitSourceLabel = options.ModelAutoCompactTokenLimitSourceLabel,
            ModelAutoCompactTokenLimitScope = options.EffectiveModelAutoCompactTokenLimitScope,
            HasModelAutoCompactTokenLimitScopeOverride = true,
            ModelAutoCompactTokenLimitScopeSourceLabel = options.ModelAutoCompactTokenLimitScopeSourceLabel,
            AutoCompactTotalEstimatedTokens = 70_000,
            AutoCompactCarriedPrefixEstimatedTokens = 50_000,
            AutoCompactBodyAfterPrefixEstimatedTokens = 20_000,
        });
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(new CopilotEffectiveConfigDiagnosticContext
        {
            Config = new CopilotConfig(),
            State = new CopilotChatState(),
            CodexConfigOptions = options,
        });

        Assert.Contains("model_auto_compact_token_limit：64,000 Token", memoryReport, StringComparison.Ordinal);
        Assert.Contains("model_auto_compact_token_limit_scope：body_after_prefix", memoryReport, StringComparison.Ordinal);
        Assert.Contains("body_after_prefix 计量 20,000/64,000 Token", contextReport, StringComparison.Ordinal);
        Assert.Contains("carried prefix 50,000 Token", contextReport, StringComparison.Ordinal);
        Assert.Contains("64,000 tokens @ body_after_prefix", debugReport, StringComparison.Ordinal);
        Assert.Contains(options.ModelAutoCompactTokenLimitScopeSourceLabel, debugReport, StringComparison.Ordinal);
    }

    [Fact]
    public void ContextUsagePresentationUsesTheConfiguredScopeForPressure()
    {
        var usage = new CopilotConversationContextUsage(
            UsagePercent: 20,
            WeightUsagePercent: 20,
            MessageUsagePercent: 10,
            ActiveMessageCount: 2,
            ActiveWeight: 800,
            CarriedPrefixWeight: 600,
            BodyAfterPrefixWeight: 200,
            MaximumMessages: 20,
            MaximumWeight: 4_000);

        var body = CopilotConversationContextUsagePresenter.Create(
            usage,
            autoCompactionEnabled: true,
            autoCompactThresholdPercent: 85,
            modelAutoCompactTokenLimit: 100,
            modelAutoCompactTokenLimitScope: CopilotModelAutoCompactTokenLimitScope.BodyAfterPrefix);
        var total = CopilotConversationContextUsagePresenter.Create(
            usage,
            autoCompactionEnabled: true,
            autoCompactThresholdPercent: 85,
            modelAutoCompactTokenLimit: 100,
            modelAutoCompactTokenLimitScope: CopilotModelAutoCompactTokenLimitScope.Total);

        Assert.Contains("body_after_prefix 自动压缩计量为 50/100 Token", body.ToolTip, StringComparison.Ordinal);
        Assert.False(body.IsUnderPressure);
        Assert.Contains("total 自动压缩计量为 200/100 Token", total.ToolTip, StringComparison.Ordinal);
        Assert.True(total.IsUnderPressure);
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-auto-compact-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
