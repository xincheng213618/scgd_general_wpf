using System.Windows.Input;
using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotComposerPagingTests
{
    [Theory]
    [InlineData(Key.PageUp, 1, 3)]
    [InlineData(Key.PageUp, 2, 3)]
    [InlineData(Key.PageDown, 0, 3)]
    [InlineData(Key.PageDown, 1, 3)]
    public void MultilineComposerKeepsPagingKeysWhileCaretCanMove(
        Key key,
        int caretLineIndex,
        int lineCount)
    {
        Assert.False(ShouldPageConversation(
            key,
            caretLineIndex,
            lineCount,
            selectionLength: 0));
    }

    [Theory]
    [InlineData(Key.PageUp, 0, 3)]
    [InlineData(Key.PageDown, 2, 3)]
    [InlineData(Key.PageUp, 0, 1)]
    [InlineData(Key.PageDown, 0, 1)]
    public void ComposerBoundaryAllowsConversationPaging(
        Key key,
        int caretLineIndex,
        int lineCount)
    {
        Assert.True(ShouldPageConversation(
            key,
            caretLineIndex,
            lineCount,
            selectionLength: 0));
    }

    [Theory]
    [InlineData(Key.PageUp, 0)]
    [InlineData(Key.PageDown, 2)]
    public void ComposerSelectionKeepsPagingKeys(Key key, int caretLineIndex)
    {
        Assert.False(ShouldPageConversation(
            key,
            caretLineIndex,
            lineCount: 3,
            selectionLength: 1));
    }

    private static bool ShouldPageConversation(
        Key key,
        int caretLineIndex,
        int lineCount,
        int selectionLength)
    {
        return CopilotChatPanel.ShouldPageConversation(
            key,
            ModifierKeys.None,
            hasComposerOverlay: false,
            caretLineIndex,
            lineCount,
            selectionLength,
            promptVerticalOffset: 0,
            promptScrollableHeight: 0,
            conversationVerticalOffset: key == Key.PageUp ? 100 : 0,
            conversationScrollableHeight: 200);
    }
}
