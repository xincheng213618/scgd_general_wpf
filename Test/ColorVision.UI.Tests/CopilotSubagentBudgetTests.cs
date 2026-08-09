using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotSubagentBudgetTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void CombinedBudgetPreservesDelegatedDirectAnswerUsage(
        bool explorationUsedDelegatedAnswer,
        bool finalizationUsedDelegatedAnswer,
        bool expected)
    {
        var combined = CopilotSubagentRunner.CombineBudgets(
            new CopilotAgentBudgetSnapshot
            {
                UsedDelegatedDirectAnswer = explorationUsedDelegatedAnswer,
            },
            new CopilotAgentBudgetSnapshot
            {
                UsedDelegatedDirectAnswer = finalizationUsedDelegatedAnswer,
            },
            CopilotAgentRunBudget.MinimumRequestTokenBudget,
            TimeSpan.Zero,
            finalizationCompleted: true);

        Assert.Equal(expected, combined.UsedDelegatedDirectAnswer);
    }
}
