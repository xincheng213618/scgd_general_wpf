using ColorVision.Copilot;
using System;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexGoalsFeatureTests
{
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
        Assert.True(CopilotConversationGoalFeaturePolicy.CanManageWhileDisabled("history"));
        Assert.True(CopilotConversationGoalFeaturePolicy.CanManageWhileDisabled("pause"));
        Assert.True(CopilotConversationGoalFeaturePolicy.CanManageWhileDisabled("CLEAR"));
        Assert.False(CopilotConversationGoalFeaturePolicy.CanManageWhileDisabled("resume"));
        Assert.False(CopilotConversationGoalFeaturePolicy.CanManageWhileDisabled("edit a new objective"));
        Assert.False(CopilotConversationGoalFeaturePolicy.CanManageWhileDisabled("a new objective"));

        var command = CopilotLocalCommandCatalog.FindExact("/goal");
        Assert.NotNull(command);
        Assert.Contains(command.Arguments!, argument => argument.Value == "history");
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

}
