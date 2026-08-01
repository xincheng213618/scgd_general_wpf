using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationPlanNavigationTests
{
    [Fact]
    public void ViewPlanCommandIsArgumentFreeAndAvailableDuringAgentRuns()
    {
        var invocation = CopilotLocalCommandCatalog.Parse("/view-plan");

        Assert.NotNull(invocation);
        Assert.Equal(CopilotLocalCommandKind.ViewPlan, invocation.Command.Kind);
        Assert.True(invocation.Command.AvailableWhileAgentRuns);
        Assert.Null(CopilotLocalCommandCatalog.Parse("/view-plan older"));
        Assert.Contains(
            CopilotLocalCommandCatalog.Suggest("/view"),
            command => command.Name == "/view-plan");
    }

    [Fact]
    public void LatestCompletedPlanIsSelectedWithoutTreatingIncompletePlansAsNavigable()
    {
        var conversation = new CopilotConversationRecord();
        var firstPlan = CreatePlan("# Plan one", CopilotAgentStopReason.Completed);
        var interruptedPlan = CreatePlan("# Interrupted plan", CopilotAgentStopReason.Interrupted);
        var latestPlan = CreatePlan("# Plan two", CopilotAgentStopReason.Completed);
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "ordinary answer"));
        conversation.Messages.Add(firstPlan);
        conversation.Messages.Add(interruptedPlan);
        conversation.Messages.Add(latestPlan);
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "later ordinary answer"));

        var result = CopilotConversationPlanNavigation.FindLatestCompletedPlan(conversation);

        Assert.Same(latestPlan, result);
        Assert.Null(CopilotConversationPlanNavigation.FindLatestCompletedPlan(null));
        Assert.Null(CopilotConversationPlanNavigation.FindLatestCompletedPlan(new CopilotConversationRecord()));
    }

    [Fact]
    public void NavigationRequestRequiresAnExplicitMessage()
    {
        Assert.Throws<ArgumentNullException>(
            () => new CopilotChatMessageNavigationRequestedEventArgs(null!));
    }

    private static CopilotChatMessage CreatePlan(
        string content,
        CopilotAgentStopReason stopReason)
    {
        return new CopilotChatMessage(CopilotChatRole.Assistant, content)
        {
            RequestMode = CopilotAgentMode.Plan,
            AgentStopReason = stopReason,
        };
    }
}
