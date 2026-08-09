using ColorVision.Copilot;
using System;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotCodexDefaultModeRequestUserInputTests
{
    [Fact]
    public void ClosestTrustedValueIsFrozenIntoTheSubmittedRequest()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                features.default_mode_request_user_input = false

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            string projectConfigPath = Path.Combine(projectConfigDirectory, "config.toml");
            File.WriteAllText(
                projectConfigPath,
                """
                [features]
                default_mode_request_user_input = true
                """);

            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            var submittedPlan = CopilotAgentRequestFactory.Prepare(
                "Implement the requested change.",
                CopilotAgentMode.Code,
                submittedContext);
            var submittedRequest = CopilotAgentRequestFactory.Create(
                submittedPlan,
                new CopilotAgentRequestBuildInput
                {
                    Profile = CopilotProfileConfig.CreateDefault(),
                    AgentDefaults = new CopilotAgentDefaultsConfig(),
                });
            File.WriteAllText(projectConfigPath, "features.default_mode_request_user_input = false");
            var refreshed = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            var submitted = submittedContext.ProjectInstructionDiscoveryOptions;
            Assert.True(submitted.ConfiguredDefaultModeRequestUserInputEnabled);
            Assert.True(submitted.HasDefaultModeRequestUserInputEnabledOverride);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.TrustedProject,
                submitted.DefaultModeRequestUserInputEnabledSource);
            Assert.True(submittedPlan.CodexDefaultModeRequestUserInputEnabled);
            Assert.True(submittedRequest.CodexDefaultModeRequestUserInputEnabled);
            Assert.False(refreshed.ConfiguredDefaultModeRequestUserInputEnabled);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void DefaultModesRequireTheFeatureWhilePlanModeDoesNot()
    {
        var planRequest = CreateRequest(CopilotAgentMode.Plan);
        var defaultRequest = CreateRequest(CopilotAgentMode.Code);
        var enabledDefaultRequest = CreateRequest(
            CopilotAgentMode.Code,
            defaultModeEnabled: true);
        var globallyDisabledDefaultRequest = CreateRequest(
            CopilotAgentMode.Auto,
            defaultModeEnabled: true,
            toolEnabled: false);

        Assert.True(CopilotMicrosoftAgentFrameworkRuntime.IsRequestUserInputToolEnabled(planRequest));
        Assert.False(CopilotMicrosoftAgentFrameworkRuntime.IsRequestUserInputToolEnabled(defaultRequest));
        Assert.True(CopilotMicrosoftAgentFrameworkRuntime.IsRequestUserInputToolEnabled(enabledDefaultRequest));
        Assert.False(CopilotMicrosoftAgentFrameworkRuntime.IsRequestUserInputToolEnabled(globallyDisabledDefaultRequest));
        Assert.Contains(
            "AskUserQuestion",
            CopilotMicrosoftAgentFrameworkRuntime.BuildCheckpointToolNames(
                enabledDefaultRequest,
                ["ReadLocalFile"]));
        Assert.DoesNotContain(
            "AskUserQuestion",
            CopilotMicrosoftAgentFrameworkRuntime.BuildCheckpointToolNames(
                defaultRequest,
                ["ReadLocalFile"]));
    }

    [Fact]
    public void DefaultModePromptInstructionsFollowTheEffectiveToolSurface()
    {
        var disabledRequest = CreateRequest(CopilotAgentMode.Code);
        var enabledRequest = CreateRequest(
            CopilotAgentMode.Code,
            defaultModeEnabled: true);
        var environment = CopilotAgentEnvironmentContext.Capture(enabledRequest);

        string disabledPrompt = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
            disabledRequest,
            Array.Empty<ICopilotTool>(),
            environment,
            taskLedgerEnabled: false,
            agentModeEnabled: true);
        string enabledPrompt = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
            enabledRequest,
            Array.Empty<ICopilotTool>(),
            environment,
            taskLedgerEnabled: false,
            agentModeEnabled: true);

        Assert.DoesNotContain("AskUserQuestion is a structured clarification pause", disabledPrompt, StringComparison.Ordinal);
        Assert.Contains("AskUserQuestion is a structured clarification pause", enabledPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticsExplainThePlanAndGlobalToolBoundaries()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredDefaultModeRequestUserInputEnabled = true,
            HasDefaultModeRequestUserInputEnabledOverride = true,
            DefaultModeRequestUserInputEnabledSource = CopilotProjectInstructionConfigSources.CodexHome,
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
            ProfileLabel = "Profile",
            Mode = CopilotAgentMode.Code,
            CodexDefaultModeRequestUserInputEnabled = true,
            HasCodexDefaultModeRequestUserInputEnabledOverride = true,
            CodexDefaultModeRequestUserInputEnabledSourceLabel = options.DefaultModeRequestUserInputEnabledSourceLabel,
        });
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                ComposerMode = CopilotAgentMode.Code,
                CodexConfigOptions = options,
            });

        Assert.Contains("Codex features.default_mode_request_user_input：true", memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.DefaultModeRequestUserInputEnabledSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains("总开关约束", memoryReport, StringComparison.Ordinal);
        Assert.Contains("Default 模式结构化提问：开放", contextReport, StringComparison.Ordinal);
        Assert.Contains("全局工具开关", contextReport, StringComparison.Ordinal);
        Assert.Contains("Codex features.default_mode_request_user_input：true", debugReport, StringComparison.Ordinal);
        Assert.Contains("tools.experimental_request_user_input.enabled", debugReport, StringComparison.Ordinal);
    }

    private static CopilotAgentRequest CreateRequest(
        CopilotAgentMode mode,
        bool defaultModeEnabled = false,
        bool toolEnabled = true) => new()
    {
        Profile = CopilotProfileConfig.CreateDefault(),
        UserText = "Complete the requested task.",
        Mode = mode,
        CodexExperimentalRequestUserInputEnabled = toolEnabled,
        CodexDefaultModeRequestUserInputEnabled = defaultModeEnabled,
    };

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-default-input-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
