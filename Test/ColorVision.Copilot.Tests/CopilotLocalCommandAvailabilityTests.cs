using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotLocalCommandAvailabilityTests
{
    [Fact]
    public void ActiveRunSuggestsQueueOnlyCommandsWithoutMakingThemImmediatelyExecutable()
    {
        var plan = Assert.IsType<CopilotLocalCommand>(
            CopilotLocalCommandCatalog.FindExact("/plan"));

        Assert.False(plan.AvailableWhileAgentRuns);
        Assert.True(CopilotLocalCommandAvailabilityPolicy.CanSuggest(
            plan,
            CopilotLocalCommandComposerContext.ActiveRun));
        Assert.False(CopilotLocalCommandAvailabilityPolicy.CanExecute(
            plan,
            CopilotLocalCommandComposerContext.ActiveRun));

        var suggestions = CopilotLocalCommandCatalog.Suggest(
            "/pl",
            composerContext: CopilotLocalCommandComposerContext.ActiveRun);

        Assert.Contains(suggestions, command => command.Name == "/plan");
    }

    [Fact]
    public void ActiveRunKeepsQueueOnlyArgumentCompletionButRestrictedContextsStayHidden()
    {
        var activeRunSuggestions = CopilotLocalCommandCatalog.Suggest(
            "/review --",
            composerContext: CopilotLocalCommandComposerContext.ActiveRun);

        Assert.Contains(activeRunSuggestions, command => command.Name == "/review --current");
        Assert.Contains(activeRunSuggestions, command => command.Name == "/review --base");
        Assert.Contains(activeRunSuggestions, command => command.Name == "/review --commit");
        Assert.Empty(CopilotLocalCommandCatalog.Suggest(
            "/pl",
            composerContext: CopilotLocalCommandComposerContext.AwaitingUserAnswer));
        Assert.Empty(CopilotLocalCommandCatalog.Suggest(
            "/pl",
            composerContext: CopilotLocalCommandComposerContext.QueuedRun));
    }
}
