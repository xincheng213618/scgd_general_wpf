using ColorVision.Copilot;
using System.Windows.Input;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationPagingTests
{
    [Theory]
    [InlineData(Key.PageUp)]
    [InlineData(Key.PageDown)]
    public void PlainPageKeysRouteWhenOnlyTheConversationCanMove(Key key)
    {
        var shouldRoute = CopilotChatPanel.ShouldPageConversation(
            key,
            ModifierKeys.None,
            hasComposerOverlay: false,
            promptVerticalOffset: key == Key.PageUp ? 0 : 100,
            promptScrollableHeight: 100,
            conversationVerticalOffset: 200,
            conversationScrollableHeight: 800);

        Assert.True(shouldRoute);
    }

    [Theory]
    [InlineData(ModifierKeys.Control)]
    [InlineData(ModifierKeys.Shift)]
    [InlineData(ModifierKeys.Alt)]
    public void ModifiedPageKeysRemainAvailableToTheInput(ModifierKeys modifiers)
    {
        var shouldRoute = CopilotChatPanel.ShouldPageConversation(
            Key.PageUp,
            modifiers,
            hasComposerOverlay: false,
            promptVerticalOffset: 0,
            promptScrollableHeight: 0,
            conversationVerticalOffset: 200,
            conversationScrollableHeight: 800);

        Assert.False(shouldRoute);
    }

    [Fact]
    public void ComposerOverlaysKeepPriorityOverConversationPaging()
    {
        var shouldRoute = CopilotChatPanel.ShouldPageConversation(
            Key.PageDown,
            ModifierKeys.None,
            hasComposerOverlay: true,
            promptVerticalOffset: 0,
            promptScrollableHeight: 0,
            conversationVerticalOffset: 200,
            conversationScrollableHeight: 800);

        Assert.False(shouldRoute);
    }

    [Theory]
    [InlineData(Key.PageUp, 40, 100)]
    [InlineData(Key.PageDown, 40, 100)]
    public void ScrollablePromptKeepsThePageKey(Key key, double promptOffset, double promptHeight)
    {
        var shouldRoute = CopilotChatPanel.ShouldPageConversation(
            key,
            ModifierKeys.None,
            hasComposerOverlay: false,
            promptVerticalOffset: promptOffset,
            promptScrollableHeight: promptHeight,
            conversationVerticalOffset: 200,
            conversationScrollableHeight: 800);

        Assert.False(shouldRoute);
    }

    [Theory]
    [InlineData(Key.PageUp, 0, 800)]
    [InlineData(Key.PageDown, 800, 800)]
    public void ConversationBoundaryDoesNotConsumeThePageKey(
        Key key,
        double conversationOffset,
        double conversationHeight)
    {
        var shouldRoute = CopilotChatPanel.ShouldPageConversation(
            key,
            ModifierKeys.None,
            hasComposerOverlay: false,
            promptVerticalOffset: key == Key.PageUp ? 0 : 100,
            promptScrollableHeight: 100,
            conversationVerticalOffset: conversationOffset,
            conversationScrollableHeight: conversationHeight);

        Assert.False(shouldRoute);
    }

    [Fact]
    public void NonPageKeysAreNeverRouted()
    {
        var shouldRoute = CopilotChatPanel.ShouldPageConversation(
            Key.Down,
            ModifierKeys.None,
            hasComposerOverlay: false,
            promptVerticalOffset: 0,
            promptScrollableHeight: 0,
            conversationVerticalOffset: 200,
            conversationScrollableHeight: 800);

        Assert.False(shouldRoute);
    }
}
