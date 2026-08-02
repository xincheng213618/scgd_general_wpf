using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotLocalCommandAvailabilityTests
{
    private static readonly CopilotLocalCommand AvailableDuringRun = new(
        "/status",
        "status",
        CopilotLocalCommandKind.Status,
        AvailableWhileAgentRuns: true);

    private static readonly CopilotLocalCommand IdleOnly = new(
        "/model",
        "model",
        CopilotLocalCommandKind.SelectModel);

    [Fact]
    public void IdleContextShowsAndExecutesEveryLocalCommand()
    {
        Assert.True(CopilotLocalCommandAvailabilityPolicy.CanShowSuggestions(
            CopilotLocalCommandComposerContext.Idle));
        Assert.True(CopilotLocalCommandAvailabilityPolicy.CanSuggest(
            AvailableDuringRun,
            CopilotLocalCommandComposerContext.Idle));
        Assert.True(CopilotLocalCommandAvailabilityPolicy.CanSuggest(
            IdleOnly,
            CopilotLocalCommandComposerContext.Idle));
        Assert.True(CopilotLocalCommandAvailabilityPolicy.CanExecute(
            AvailableDuringRun,
            CopilotLocalCommandComposerContext.Idle));
        Assert.True(CopilotLocalCommandAvailabilityPolicy.CanExecute(
            IdleOnly,
            CopilotLocalCommandComposerContext.Idle));
    }

    [Fact]
    public void ActiveRunOnlyShowsAndExecutesCommandsDeclaredAvailable()
    {
        Assert.True(CopilotLocalCommandAvailabilityPolicy.CanShowSuggestions(
            CopilotLocalCommandComposerContext.ActiveRun));
        Assert.True(CopilotLocalCommandAvailabilityPolicy.CanSuggest(
            AvailableDuringRun,
            CopilotLocalCommandComposerContext.ActiveRun));
        Assert.False(CopilotLocalCommandAvailabilityPolicy.CanSuggest(
            IdleOnly,
            CopilotLocalCommandComposerContext.ActiveRun));
        Assert.True(CopilotLocalCommandAvailabilityPolicy.CanExecute(
            AvailableDuringRun,
            CopilotLocalCommandComposerContext.ActiveRun));
        Assert.False(CopilotLocalCommandAvailabilityPolicy.CanExecute(
            IdleOnly,
            CopilotLocalCommandComposerContext.ActiveRun));
    }

    [Theory]
    [InlineData(CopilotLocalCommandComposerContext.AwaitingUserAnswer)]
    [InlineData(CopilotLocalCommandComposerContext.QueuedRun)]
    public void ContextsWithDifferentEnterSemanticsHideAndRejectCommands(
        CopilotLocalCommandComposerContext context)
    {
        Assert.False(CopilotLocalCommandAvailabilityPolicy.CanShowSuggestions(context));
        Assert.False(CopilotLocalCommandAvailabilityPolicy.CanSuggest(AvailableDuringRun, context));
        Assert.False(CopilotLocalCommandAvailabilityPolicy.CanExecute(AvailableDuringRun, context));
    }
}
