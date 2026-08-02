using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotComposerCompletionKeysTests
{
    [Theory]
    [InlineData(0, 0, 0, true)]
    [InlineData(5, 0, 5, true)]
    [InlineData(4, 0, 5, false)]
    [InlineData(5, 1, 5, false)]
    [InlineData(-1, 0, 5, false)]
    [InlineData(0, 0, -1, false)]
    public void RightArrowOnlyAcceptsAtUnselectedInputEnd(
        int caretIndex,
        int selectionLength,
        int textLength,
        bool expected)
    {
        Assert.Equal(
            expected,
            CopilotComposerCompletionKeys.CanAcceptRightArrow(
                caretIndex,
                selectionLength,
                textLength));
    }
}
