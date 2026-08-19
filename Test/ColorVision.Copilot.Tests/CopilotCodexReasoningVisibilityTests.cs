using ColorVision.Copilot;
using System;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexReasoningVisibilityTests
{
    [Fact]
    public void ClosestTrustedVisibilityLayerIsFrozenIntoTheSubmittedTurnSnapshot()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                hide_agent_reasoning = true

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string configDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(configDirectory);
            string configPath = Path.Combine(configDirectory, "config.toml");
            File.WriteAllText(configPath, "hide_agent_reasoning = true");

            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            File.WriteAllText(configPath, "hide_agent_reasoning = false");
            var refreshed = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);
            var submitted = submittedContext.ProjectInstructionDiscoveryOptions;

            Assert.True(submitted.ConfiguredHideAgentReasoning);
            Assert.True(submitted.HasHideAgentReasoningOverride);
            Assert.True(submitted.UsesCodexConfig);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.TrustedProject,
                submitted.HideAgentReasoningSource);
            Assert.Contains("hide_agent_reasoning", submitted.HideAgentReasoningSourceLabel, StringComparison.Ordinal);
            Assert.False(refreshed.ConfiguredHideAgentReasoning);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.TrustedProject,
                refreshed.HideAgentReasoningSource);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void UntrustedOrInvalidVisibilityValuesCannotReplaceTheCodexHomeContract()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                hide_agent_reasoning = true

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            string configDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(configDirectory);
            File.WriteAllText(
                Path.Combine(configDirectory, "config.toml"),
                "hide_agent_reasoning = false");

            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);
            Assert.True(untrusted.ConfiguredHideAgentReasoning);
            Assert.True(untrusted.HasHideAgentReasoningOverride);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.CodexHome,
                untrusted.HideAgentReasoningSource);
            Assert.Empty(untrusted.AppliedProjectConfigFilePaths);

            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                "hide_agent_reasoning = \"yes\"");
            var invalid = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);
            Assert.False(invalid.HasHideAgentReasoningOverride);
            Assert.False(invalid.ConfiguredHideAgentReasoning);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void HiddenReasoningIsFilteredOnlyAfterTheTurnProtocolBoundary()
    {
        var mixedChatDelta = new CopilotTurnChatDeltaEvent(
            new CopilotStreamDelta("private reasoning", "public answer"));
        var reasoningOnlyChatDelta = new CopilotTurnChatDeltaEvent(
            new CopilotStreamDelta("private reasoning", string.Empty));
        var agentReasoning = new CopilotTurnAgentEvent(
            CopilotAgentEvent.ReasoningDelta("private reasoning"));
        var agentAnswer = new CopilotTurnAgentEvent(
            CopilotAgentEvent.AnswerDelta("public answer"));

        var filteredChat = Assert.IsType<CopilotTurnChatDeltaEvent>(
            CopilotReasoningVisibility.FilterForPresentation(mixedChatDelta, hideAgentReasoning: true));
        Assert.Empty(filteredChat.Delta.ReasoningContent);
        Assert.Equal("public answer", filteredChat.Delta.Content);
        Assert.Null(CopilotReasoningVisibility.FilterForPresentation(
            reasoningOnlyChatDelta,
            hideAgentReasoning: true));
        Assert.Null(CopilotReasoningVisibility.FilterForPresentation(
            agentReasoning,
            hideAgentReasoning: true));
        Assert.Same(
            agentAnswer,
            CopilotReasoningVisibility.FilterForPresentation(agentAnswer, hideAgentReasoning: true));
        Assert.Same(
            agentReasoning,
            CopilotReasoningVisibility.FilterForPresentation(agentReasoning, hideAgentReasoning: false));
    }

    [Fact]
    public void VisibilityDiagnosticsExposeValueSourceScopeAndFrozenBehavior()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredHideAgentReasoning = true,
            HasHideAgentReasoningOverride = true,
            HideAgentReasoningSource = CopilotProjectInstructionConfigSources.CodexHome,
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
            CodexHideAgentReasoning = true,
            HasCodexHideAgentReasoningOverride = true,
            CodexHideAgentReasoningSourceLabel = options.HideAgentReasoningSourceLabel,
        });
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                CodexConfigOptions = options,
            });

        Assert.Contains("Codex hide_agent_reasoning：true", memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.HideAgentReasoningSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains("推理事件展示：隐藏", contextReport, StringComparison.Ordinal);
        Assert.Contains("Codex hide_agent_reasoning：true", debugReport, StringComparison.Ordinal);
        Assert.Contains("Chat/Agent", memoryReport, StringComparison.Ordinal);
        Assert.Contains("Chat/Agent", contextReport, StringComparison.Ordinal);
        Assert.Contains("Chat/Agent", debugReport, StringComparison.Ordinal);
        Assert.Contains("Token 计量", memoryReport, StringComparison.Ordinal);
        Assert.Contains("Token 计量", contextReport, StringComparison.Ordinal);
        Assert.Contains("Token 计量", debugReport, StringComparison.Ordinal);
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-reasoning-visibility-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
