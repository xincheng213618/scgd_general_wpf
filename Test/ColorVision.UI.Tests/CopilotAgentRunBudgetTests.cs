using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentRunBudgetTests
{
    [Fact]
    public void NarrowReadOnlyAuditUsesAnAdaptiveEvidenceBudget()
    {
        var request = Request(
            @"只读审计 C:\workspace\Copilot，列出 1 条可验证的问题；不要修改任何文件，不要执行写操作。");

        var budget = CopilotAgentRunBudget.Resolve(request);

        Assert.Equal(1, budget.NarrowEvidenceResultLimit);
        Assert.Equal(512 * 1024, budget.RequestTokenBudget);
        Assert.Equal(16, budget.MaxToolCalls);
        Assert.Equal(8, budget.MaxAgentPasses);
        Assert.Equal(TimeSpan.FromMinutes(15), budget.TotalDuration);

        var instructions = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
            request,
            [new CopilotGrepTextTool()],
            CopilotAgentEnvironmentContext.Capture(request),
            taskLedgerEnabled: false,
            agentModeEnabled: false);
        Assert.Contains(
            "Once that many high-confidence results are verified, answer immediately",
            instructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "reserve answer text for the final response after the last tool observation",
            instructions,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Read-only audit C:\\workspace\\Copilot and list two verifiable findings. Do not modify files.", 2, 20)]
    [InlineData("只读检查 C:\\workspace\\Copilot，给出三条风险；不要修改任何文件。", 3, 24)]
    public void NarrowEvidenceBudgetRecognizesEnglishAndChineseCounts(
        string userText,
        int expectedResults,
        int expectedToolCalls)
    {
        var budget = CopilotAgentRunBudget.Resolve(Request(userText));

        Assert.Equal(expectedResults, budget.NarrowEvidenceResultLimit);
        Assert.Equal(expectedToolCalls, budget.MaxToolCalls);
    }

    [Theory]
    [InlineData("只读全面审计 C:\\workspace\\Copilot，列出 1 条最严重的问题；不要修改任何文件。")]
    [InlineData("只读审计 C:\\workspace\\Copilot，检查 40 个代码位置并列出 1 条风险；不要修改任何文件。")]
    public void ExhaustiveOrBroadAuditsKeepConfiguredBudgets(string userText)
    {
        var budget = CopilotAgentRunBudget.Resolve(Request(userText));

        Assert.Equal(0, budget.NarrowEvidenceResultLimit);
        Assert.Equal(CopilotAgentRunBudget.MaximumRequestTokenBudget, budget.RequestTokenBudget);
        Assert.Equal(128, budget.MaxToolCalls);
        Assert.Equal(32, budget.MaxAgentPasses);
        Assert.Equal(TimeSpan.FromHours(2), budget.TotalDuration);
    }

    [Fact]
    public void ExplicitRunOverridesAreNeverReplacedByAdaptiveBudgeting()
    {
        var request = Request(
            "只读审计 C:\\workspace\\Copilot，列出 1 条问题；不要修改任何文件。",
            new CopilotAgentRunBudgetOverride
            {
                RequestTokenBudget = 640_000,
                MaxToolCalls = 64,
                MaxAgentPasses = 16,
                TotalDuration = TimeSpan.FromMinutes(30),
            });

        var budget = CopilotAgentRunBudget.Resolve(request);

        Assert.Equal(0, budget.NarrowEvidenceResultLimit);
        Assert.Equal(640_000, budget.RequestTokenBudget);
        Assert.Equal(64, budget.MaxToolCalls);
        Assert.Equal(16, budget.MaxAgentPasses);
        Assert.Equal(TimeSpan.FromMinutes(30), budget.TotalDuration);
    }

    [Fact]
    public void AdaptiveBudgetNeverRaisesSmallerConfiguredLimits()
    {
        var request = new CopilotAgentRequest
        {
            Mode = CopilotAgentMode.Auto,
            UserText = "只读审计 C:\\workspace\\Copilot，列出 1 条问题；不要修改任何文件。",
            SearchRootPaths = [@"C:\workspace"],
            ReadableLocalDirectoryPaths = [@"C:\workspace"],
            RunBudgetDefaults = new CopilotAgentRunBudgetDefaults
            {
                ContextWindowTokens = CopilotAgentDefaultsConfig.DefaultContextWindowTokens,
                RequestTokenBudget = 128_000,
                MaxToolCalls = 12,
                MaxAgentPasses = 4,
                TotalDuration = TimeSpan.FromMinutes(5),
            },
        };

        var budget = CopilotAgentRunBudget.Resolve(request);

        Assert.Equal(1, budget.NarrowEvidenceResultLimit);
        Assert.Equal(128_000, budget.RequestTokenBudget);
        Assert.Equal(12, budget.MaxToolCalls);
        Assert.Equal(4, budget.MaxAgentPasses);
        Assert.Equal(TimeSpan.FromMinutes(5), budget.TotalDuration);
    }

    [Fact]
    public void CompletedNarrowEvidenceAnswerAtToolLimitDoesNotRemainRecoverable()
    {
        var stopReason = CopilotMicrosoftAgentFrameworkRuntime.DetermineStopReason(
            new CopilotAgentTaskLedgerSnapshot { Mode = "execute" },
            new CopilotAgentBudgetSnapshot
            {
                BudgetExhausted = true,
                ToolBudgetExhausted = true,
                NarrowEvidenceResultLimit = 1,
            },
            Array.Empty<CopilotAgentStepRecord>(),
            hasModelFinalAnswer: true);

        Assert.Equal(CopilotAgentStopReason.Completed, stopReason);
    }

    [Fact]
    public void GeneralToolLimitRemainsBudgetExhaustedAfterFinalAnswer()
    {
        var stopReason = CopilotMicrosoftAgentFrameworkRuntime.DetermineStopReason(
            new CopilotAgentTaskLedgerSnapshot { Mode = "execute" },
            new CopilotAgentBudgetSnapshot
            {
                BudgetExhausted = true,
                ToolBudgetExhausted = true,
            },
            Array.Empty<CopilotAgentStepRecord>(),
            hasModelFinalAnswer: true);

        Assert.Equal(CopilotAgentStopReason.BudgetExhausted, stopReason);
    }

    [Fact]
    public void ToolLimitWithRemainingWorkStaysBudgetExhausted()
    {
        var stopReason = CopilotMicrosoftAgentFrameworkRuntime.DetermineStopReason(
            new CopilotAgentTaskLedgerSnapshot
            {
                Mode = "execute",
                Items =
                [
                    new CopilotAgentTaskItem { Id = 1, Title = "Continue audit" },
                ],
            },
            new CopilotAgentBudgetSnapshot
            {
                BudgetExhausted = true,
                ToolBudgetExhausted = true,
            },
            Array.Empty<CopilotAgentStepRecord>(),
            hasModelFinalAnswer: true);

        Assert.Equal(CopilotAgentStopReason.BudgetExhausted, stopReason);
    }

    [Fact]
    public void RequestTokenLimitStaysBudgetExhaustedAfterFinalAnswer()
    {
        var stopReason = CopilotMicrosoftAgentFrameworkRuntime.DetermineStopReason(
            new CopilotAgentTaskLedgerSnapshot { Mode = "execute" },
            new CopilotAgentBudgetSnapshot
            {
                BudgetExhausted = true,
                RequestTokenBudgetExhausted = true,
            },
            Array.Empty<CopilotAgentStepRecord>(),
            hasModelFinalAnswer: true);

        Assert.Equal(CopilotAgentStopReason.BudgetExhausted, stopReason);
    }

    [Theory]
    [InlineData(CopilotAgentEventType.ToolStarted, true)]
    [InlineData(CopilotAgentEventType.ToolProgress, true)]
    [InlineData(CopilotAgentEventType.ToolResult, true)]
    [InlineData(CopilotAgentEventType.RuntimeDiagnostic, false)]
    [InlineData(CopilotAgentEventType.AnswerDelta, false)]
    public void ToolActivityClearsOnlyAnExistingProvisionalAnswer(
        CopilotAgentEventType eventType,
        bool shouldReset)
    {
        Assert.Equal(
            shouldReset,
            CopilotMicrosoftAgentFrameworkRuntime.ShouldResetAnswerBeforeEvent(eventType, answerLength: 12));
        Assert.False(CopilotMicrosoftAgentFrameworkRuntime.ShouldResetAnswerBeforeEvent(eventType, answerLength: 0));
    }

    private static CopilotAgentRequest Request(
        string userText,
        CopilotAgentRunBudgetOverride? runBudgetOverride = null)
    {
        return new CopilotAgentRequest
        {
            Mode = CopilotAgentMode.Auto,
            UserText = userText,
            SearchRootPaths = [@"C:\workspace"],
            ReadableLocalDirectoryPaths = [@"C:\workspace"],
            RunBudgetDefaults = new CopilotAgentRunBudgetDefaults
            {
                ContextWindowTokens = CopilotAgentDefaultsConfig.DefaultContextWindowTokens,
                RequestTokenBudget = CopilotAgentRunBudget.MaximumRequestTokenBudget,
                MaxToolCalls = 128,
                MaxAgentPasses = 32,
                TotalDuration = TimeSpan.FromHours(2),
            },
            RunBudgetOverride = runBudgetOverride,
        };
    }
}
