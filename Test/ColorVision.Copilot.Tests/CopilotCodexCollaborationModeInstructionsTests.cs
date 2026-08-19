using ColorVision.Copilot;
using System;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexCollaborationModeInstructionsTests
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
                include_collaboration_mode_instructions = true

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            string projectConfigPath = Path.Combine(projectConfigDirectory, "config.toml");
            File.WriteAllText(projectConfigPath, "include_collaboration_mode_instructions = false");

            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            var submittedPlan = CopilotAgentRequestFactory.Prepare(
                "Plan the requested implementation.",
                CopilotAgentMode.Plan,
                submittedContext);
            var submittedRequest = CopilotAgentRequestFactory.Create(
                submittedPlan,
                new CopilotAgentRequestBuildInput
                {
                    Profile = CopilotProfileConfig.CreateDefault(),
                    AgentDefaults = new CopilotAgentDefaultsConfig(),
                });
            File.WriteAllText(projectConfigPath, "include_collaboration_mode_instructions = true");
            var refreshed = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            var submitted = submittedContext.ProjectInstructionDiscoveryOptions;
            Assert.False(submitted.ConfiguredIncludeCollaborationModeInstructions);
            Assert.True(submitted.HasIncludeCollaborationModeInstructionsOverride);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.TrustedProject,
                submitted.IncludeCollaborationModeInstructionsSource);
            Assert.False(submittedPlan.CodexIncludeCollaborationModeInstructions);
            Assert.False(submittedRequest.CodexIncludeCollaborationModeInstructions);
            Assert.True(refreshed.ConfiguredIncludeCollaborationModeInstructions);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void DisabledSnapshotOmitsModeGuidanceWithoutChangingPlanRuntimePolicy()
    {
        var enabledRequest = CreatePlanRequest(includeCollaborationModeInstructions: true);
        var disabledRequest = CreatePlanRequest(includeCollaborationModeInstructions: false);
        var environment = new CopilotAgentEnvironmentContext();

        string enabledHarness = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
            enabledRequest,
            Array.Empty<ICopilotTool>(),
            environment,
            taskLedgerEnabled: true,
            agentModeEnabled: true);
        string disabledHarness = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
            disabledRequest,
            Array.Empty<ICopilotTool>(),
            environment,
            taskLedgerEnabled: true,
            agentModeEnabled: true);
        var contextBuilder = new CopilotAgentContextBuilder();
        string enabledAnswer = contextBuilder.BuildPreparedUserMessageContent(
            enabledRequest,
            Array.Empty<CopilotToolResult>());
        string disabledAnswer = contextBuilder.BuildPreparedUserMessageContent(
            disabledRequest,
            Array.Empty<CopilotToolResult>());

        Assert.Contains("Operate in user-selected plan-only mode", enabledHarness, StringComparison.Ordinal);
        Assert.Contains("Use one concise outcome-oriented todo list", enabledHarness, StringComparison.Ordinal);
        Assert.Contains("This is a user-selected plan-only request", enabledHarness, StringComparison.Ordinal);
        Assert.DoesNotContain("Operate in user-selected plan-only mode", disabledHarness, StringComparison.Ordinal);
        Assert.DoesNotContain("Use one concise outcome-oriented todo list", disabledHarness, StringComparison.Ordinal);
        Assert.DoesNotContain("This is a user-selected plan-only request", disabledHarness, StringComparison.Ordinal);
        Assert.Contains("Operate in user-selected plan-only mode", enabledAnswer, StringComparison.Ordinal);
        Assert.DoesNotContain("Operate in user-selected plan-only mode", disabledAnswer, StringComparison.Ordinal);
        Assert.Contains("ColorVision Agent runtime", disabledHarness, StringComparison.Ordinal);

        Assert.True(CopilotMicrosoftAgentFrameworkRuntime.IsUpdatePlanToolEnabled(disabledRequest));
        Assert.Contains(
            "update_plan",
            CopilotMicrosoftAgentFrameworkRuntime.BuildCheckpointToolNames(
                disabledRequest,
                ["ReadLocalFile"]));
        Assert.False(CopilotToolRegistry.IsAllowedForMode(
            new CopilotSetThemeTool(),
            disabledRequest));
    }

    [Fact]
    public void DiagnosticsExplainThePromptOnlyBoundary()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredIncludeCollaborationModeInstructions = false,
            HasIncludeCollaborationModeInstructionsOverride = true,
            IncludeCollaborationModeInstructionsSource = CopilotProjectInstructionConfigSources.CodexHome,
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
            Mode = CopilotAgentMode.Plan,
            CodexIncludeCollaborationModeInstructions = false,
            HasCodexIncludeCollaborationModeInstructionsOverride = true,
            CodexIncludeCollaborationModeInstructionsSourceLabel = options.IncludeCollaborationModeInstructionsSourceLabel,
        });
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                ComposerMode = CopilotAgentMode.Plan,
                CodexConfigOptions = options,
            });

        Assert.Contains("Codex include_collaboration_mode_instructions：false", memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.IncludeCollaborationModeInstructionsSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains("当前模式、工具过滤、任务清单与完成循环保持不变", memoryReport, StringComparison.Ordinal);
        Assert.Contains("协作模式说明：省略", contextReport, StringComparison.Ordinal);
        Assert.Contains("当前模式、工具过滤、任务清单与完成循环保持不变", contextReport, StringComparison.Ordinal);
        Assert.Contains("Codex include_collaboration_mode_instructions：false", debugReport, StringComparison.Ordinal);
        Assert.Contains("当前模式、工具过滤、任务清单与完成循环保持不变", debugReport, StringComparison.Ordinal);
    }

    private static CopilotAgentRequest CreatePlanRequest(bool includeCollaborationModeInstructions) => new()
    {
        Profile = CopilotProfileConfig.CreateDefault(),
        UserText = "Plan the requested implementation.",
        Mode = CopilotAgentMode.Plan,
        HarnessFeatures = CopilotAgentHarnessFeatures.Full,
        CodexUpdatePlanEnabled = true,
        CodexIncludeCollaborationModeInstructions = includeCollaborationModeInstructions,
    };

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-collaboration-instructions-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
