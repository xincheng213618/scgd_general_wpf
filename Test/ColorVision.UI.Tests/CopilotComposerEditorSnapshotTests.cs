using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotComposerEditorSnapshotTests
{
    [Fact]
    public void CapturePreservesMultilineWhitespaceAndCaret()
    {
        const string text = "  first line\r\n\tsecond line  ";

        var snapshot = CopilotComposerEditorSnapshot.Capture(text, 9);

        Assert.Equal(text, snapshot.Text);
        Assert.Equal(9, snapshot.CaretIndex);
    }

    [Fact]
    public void CaptureBoundsTextWithoutSplittingSurrogatePair()
    {
        var limit = CopilotConversationHistoryWindow.MaximumContentCharacterLimit;
        var text = new string('x', limit - 1) + "😀tail";

        var snapshot = CopilotComposerEditorSnapshot.Capture(text, int.MaxValue);

        Assert.Equal(limit - 1, snapshot.Text.Length);
        Assert.False(char.IsHighSurrogate(snapshot.Text[^1]));
        Assert.Equal(snapshot.Text.Length, snapshot.CaretIndex);
    }

    [Fact]
    public void CaptureNormalizesNullTextAndNegativeCaret()
    {
        var snapshot = CopilotComposerEditorSnapshot.Capture(null, -1);

        Assert.Equal(string.Empty, snapshot.Text);
        Assert.Equal(0, snapshot.CaretIndex);
    }
}
