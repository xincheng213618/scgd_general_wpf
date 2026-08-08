using ColorVision.Copilot;
using System;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotCodexGoalsFeatureTests
{
    [Fact]
    public void ClosestTrustedValueIsFrozenAndOmitsTheActiveGoalFromTheRequest()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                features.goals = true

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
                goals = false
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
                "Continue the current task.",
                CopilotAgentMode.Code,
                submittedContext);
            var submittedRequest = CopilotAgentRequestFactory.Create(
                submittedPlan,
                new CopilotAgentRequestBuildInput
                {
                    Profile = CopilotProfileConfig.CreateDefault(),
                    AgentDefaults = new CopilotAgentDefaultsConfig(),
                    ActiveGoalText = "Finish the persistent objective.",
                });
            File.WriteAllText(projectConfigPath, "features.goals = true");
            var refreshed = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            var submitted = submittedContext.ProjectInstructionDiscoveryOptions;
            Assert.False(submitted.ConfiguredGoalsEnabled);
            Assert.True(submitted.HasGoalsEnabledOverride);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.TrustedProject,
                submitted.GoalsEnabledSource);
            Assert.False(submittedPlan.CodexGoalsEnabled);
            Assert.Empty(submittedRequest.ActiveGoalText);
            Assert.True(refreshed.ConfiguredGoalsEnabled);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void EnabledDefaultKeepsTheNormalizedActiveGoal()
    {
        var plan = CopilotAgentRequestFactory.Prepare(
            "Continue the current task.",
            CopilotAgentMode.Code,
            new CopilotAgentHostContextSnapshot(null, null, null));
        var request = CopilotAgentRequestFactory.Create(
            plan,
            new CopilotAgentRequestBuildInput
            {
                Profile = CopilotProfileConfig.CreateDefault(),
                AgentDefaults = new CopilotAgentDefaultsConfig(),
                ActiveGoalText = "  Finish the persistent objective.  ",
            });

        Assert.True(plan.CodexGoalsEnabled);
        Assert.Equal("Finish the persistent objective.", request.ActiveGoalText);
    }

    [Fact]
    public void DisabledFeatureKeepsOnlyInspectionAndSafeShutdownCommandsAvailable()
    {
        Assert.True(CopilotConversationGoalFeaturePolicy.CanManageWhileDisabled(null));
        Assert.True(CopilotConversationGoalFeaturePolicy.CanManageWhileDisabled("pause"));
        Assert.True(CopilotConversationGoalFeaturePolicy.CanManageWhileDisabled("CLEAR"));
        Assert.False(CopilotConversationGoalFeaturePolicy.CanManageWhileDisabled("resume"));
        Assert.False(CopilotConversationGoalFeaturePolicy.CanManageWhileDisabled("edit a new objective"));
        Assert.False(CopilotConversationGoalFeaturePolicy.CanManageWhileDisabled("a new objective"));
    }

    [Fact]
    public void DiagnosticsExplainThePausedLifecycleAndPreservedState()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredGoalsEnabled = false,
            HasGoalsEnabledOverride = true,
            GoalsEnabledSource = CopilotProjectInstructionConfigSources.CodexHome,
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
            CodexGoalsEnabled = false,
            HasCodexGoalsEnabledOverride = true,
            CodexGoalsEnabledSourceLabel = options.GoalsEnabledSourceLabel,
        });
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                ComposerMode = CopilotAgentMode.Code,
                CodexConfigOptions = options,
            });

        Assert.Contains("Codex features.goals：false", memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.GoalsEnabledSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains("已有目标记录保留", memoryReport, StringComparison.Ordinal);
        Assert.Contains("持续目标：暂停", contextReport, StringComparison.Ordinal);
        Assert.Contains("不绑定、计数、评估或自动续作", contextReport, StringComparison.Ordinal);
        Assert.Contains("Codex features.goals：false", debugReport, StringComparison.Ordinal);
        Assert.Contains("/goal 仍可查看、暂停或清除", debugReport, StringComparison.Ordinal);
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-goals-feature-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
