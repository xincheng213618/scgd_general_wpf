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

}
