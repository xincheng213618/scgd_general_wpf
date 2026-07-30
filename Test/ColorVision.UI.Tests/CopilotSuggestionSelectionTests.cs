using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotSuggestionSelectionTests
{
    [Theory]
    [InlineData(0, -1)]
    [InlineData(-1, -1)]
    [InlineData(1, 0)]
    [InlineData(8, 0)]
    public void ResetSelectsTheFirstAvailableSuggestion(int itemCount, int expected)
    {
        Assert.Equal(expected, CopilotSuggestionSelection.Reset(itemCount));
    }

    [Theory]
    [InlineData(-1, 0, -1)]
    [InlineData(0, 3, 0)]
    [InlineData(2, 3, 2)]
    [InlineData(-1, 3, 0)]
    [InlineData(4, 3, 0)]
    public void NormalizeKeepsOnlyAValidSelection(
        int selectedIndex,
        int itemCount,
        int expected)
    {
        Assert.Equal(
            expected,
            CopilotSuggestionSelection.Normalize(selectedIndex, itemCount));
    }

    [Theory]
    [InlineData(-1, 0, false, -1)]
    [InlineData(-1, 3, false, 0)]
    [InlineData(-1, 3, true, 2)]
    [InlineData(0, 3, false, 1)]
    [InlineData(2, 3, false, 0)]
    [InlineData(0, 3, true, 2)]
    [InlineData(2, 3, true, 1)]
    [InlineData(9, 3, false, 0)]
    [InlineData(9, 3, true, 2)]
    public void MoveWrapsAndRecoversFromAStaleSelection(
        int selectedIndex,
        int itemCount,
        bool previous,
        int expected)
    {
        Assert.Equal(
            expected,
            CopilotSuggestionSelection.Move(selectedIndex, itemCount, previous));
    }
}
