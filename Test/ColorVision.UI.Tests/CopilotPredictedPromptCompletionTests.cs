using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotPredictedPromptCompletionTests
{
    [Theory]
    [InlineData("继续检查剩余改动", "", "继续检查剩余改动")]
    [InlineData("继续检查剩余改动", "继续", "检查剩余改动")]
    [InlineData("Run the tests", "run ", "the tests")]
    public void MatchingPrefixReturnsOnlyRemainingText(
        string suggestion,
        string input,
        string expected)
    {
        Assert.True(CopilotPredictedPromptCompletion.TryResolve(
            suggestion,
            input,
            out var remainingText));
        Assert.Equal(expected, remainingText);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("继续检查剩余改动", "改成别的")]
    [InlineData("继续检查剩余改动", "继续检查剩余改动")]
    [InlineData("继续检查剩余改动", "继续检查剩余改动并提交")]
    public void EmptyDivergentOrConsumedInputDoesNotResolve(
        string suggestion,
        string input)
    {
        Assert.False(CopilotPredictedPromptCompletion.TryResolve(
            suggestion,
            input,
            out var remainingText));
        Assert.Empty(remainingText);
    }

    [Theory]
    [InlineData("继续检查剩余改动", "继续", false, false)]
    [InlineData("继续检查剩余改动", "改成别的", false, false)]
    [InlineData("继续检查剩余改动", "继续检查剩余改动", false, true)]
    [InlineData("继续检查剩余改动", "继续", true, true)]
    [InlineData("继续检查剩余改动", "", true, false)]
    public void ClearPolicyConsumesOnlyCompletedOrPendingSuggestions(
        string suggestion,
        string input,
        bool requestPending,
        bool expected)
    {
        Assert.Equal(
            expected,
            CopilotPredictedPromptCompletion.ShouldClear(
                suggestion,
                input,
                requestPending));
    }
}
