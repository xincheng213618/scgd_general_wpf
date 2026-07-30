using ColorVision.Copilot;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotGoalContinuationTests
{
    [Fact]
    public void ContinueThenAchieveUpdatesPersistentProgressAndUsage()
    {
        var now = DateTimeOffset.Parse("2026-07-30T12:00:00Z");
        var goal = CopilotConversationGoal.Create("Finish the migration and verify all tests", now);
        var continueEvaluation = new CopilotGoalEvaluationResult(
            CopilotGoalEvaluationVerdict.Continue,
            "The migration compiles, but the full regression suite is not yet recorded.",
            new CopilotTokenUsage(20, 5, 25));

        var continued = CopilotGoalContinuationPolicy.Evaluate(
            goal,
            CopilotAgentMode.Auto,
            CopilotAgentStopReason.Completed,
            new CopilotTokenUsage(120, 30, 150).Add(continueEvaluation.Usage),
            continueEvaluation,
            now.AddMinutes(1));

        Assert.Equal(CopilotGoalTurnAction.QueueContinuation, continued.Action);
        Assert.True(continued.Goal.IsActive);
        Assert.Equal(1, continued.Goal.TurnCount);
        Assert.Equal(1, continued.Goal.EvaluationCount);
        Assert.Equal(175, continued.Goal.TokensUsed);
        Assert.Equal(1, continued.Goal.ConsecutiveContinuationCount);
        Assert.Equal(continueEvaluation.Reason, continued.Goal.LastEvaluationReason);

        var achievedEvaluation = new CopilotGoalEvaluationResult(
            CopilotGoalEvaluationVerdict.Achieved,
            "The full regression suite and build both passed.",
            new CopilotTokenUsage(10, 4, 14));
        var achieved = CopilotGoalContinuationPolicy.Evaluate(
            continued.Goal,
            CopilotAgentMode.Auto,
            CopilotAgentStopReason.Completed,
            new CopilotTokenUsage(80, 20, 100).Add(achievedEvaluation.Usage),
            achievedEvaluation,
            now.AddMinutes(2));

        Assert.Equal(CopilotGoalTurnAction.Complete, achieved.Action);
        Assert.True(achieved.Goal.IsAchieved);
        Assert.Equal(2, achieved.Goal.TurnCount);
        Assert.Equal(2, achieved.Goal.EvaluationCount);
        Assert.Equal(289, achieved.Goal.TokensUsed);
        Assert.Equal(0, achieved.Goal.ConsecutiveContinuationCount);
        Assert.True(achieved.Goal.IsStructurallyValid());
    }

    [Theory]
    [InlineData(CopilotAgentStopReason.AwaitingUser)]
    [InlineData(CopilotAgentStopReason.ApprovalDenied)]
    [InlineData(CopilotAgentStopReason.Blocked)]
    [InlineData(CopilotAgentStopReason.ProviderFailure)]
    public void NonCompletedAgentStopsPauseWithoutAnEvaluator(CopilotAgentStopReason stopReason)
    {
        var goal = CopilotConversationGoal.Create("Finish safely", DateTimeOffset.UtcNow);

        var decision = CopilotGoalContinuationPolicy.Evaluate(
            goal,
            CopilotAgentMode.Auto,
            stopReason,
            new CopilotTokenUsage(20, 5, 25),
            evaluation: null,
            DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal(CopilotGoalTurnAction.Pause, decision.Action);
        Assert.Equal(CopilotConversationGoalState.Paused, decision.Goal.State);
        Assert.Equal(1, decision.Goal.TurnCount);
        Assert.Equal(0, decision.Goal.EvaluationCount);
        Assert.Equal(25, decision.Goal.TokensUsed);
        Assert.NotEmpty(decision.Reason);
    }

    [Fact]
    public void ReadOnlyOrPlanModePausesBeforeAutomaticExecutionBroadensScope()
    {
        var goal = CopilotConversationGoal.Create("Implement the approved plan", DateTimeOffset.UtcNow);

        var decision = CopilotGoalContinuationPolicy.Evaluate(
            goal,
            CopilotAgentMode.Plan,
            CopilotAgentStopReason.Completed,
            CopilotTokenUsage.Empty,
            new CopilotGoalEvaluationResult(
                CopilotGoalEvaluationVerdict.Continue,
                "Implementation has not started.",
                CopilotTokenUsage.Empty),
            DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal(CopilotGoalTurnAction.Pause, decision.Action);
        Assert.Contains("避免自动续作扩大到执行权限", decision.Reason, StringComparison.Ordinal);
        Assert.Equal(0, decision.Goal.EvaluationCount);
    }

    [Fact]
    public void RepeatedIncompleteEvaluationsPauseAtTheBoundedContinuationCap()
    {
        var goal = CopilotConversationGoal.Create("Finish a bounded task", DateTimeOffset.UtcNow);
        var evaluation = new CopilotGoalEvaluationResult(
            CopilotGoalEvaluationVerdict.Continue,
            "One verified condition remains.",
            CopilotTokenUsage.Empty);

        for (var round = 1; round <= CopilotGoalContinuationPolicy.MaximumConsecutiveContinuations; round++)
        {
            var decision = CopilotGoalContinuationPolicy.Evaluate(
                goal,
                CopilotAgentMode.Auto,
                CopilotAgentStopReason.Completed,
                CopilotTokenUsage.Empty,
                evaluation,
                DateTimeOffset.UtcNow.AddMinutes(round));
            goal = decision.Goal;
            Assert.Equal(
                round == CopilotGoalContinuationPolicy.MaximumConsecutiveContinuations
                    ? CopilotGoalTurnAction.Pause
                    : CopilotGoalTurnAction.QueueContinuation,
                decision.Action);
        }

        Assert.Equal(CopilotConversationGoalState.Paused, goal.State);
        Assert.Equal(
            CopilotGoalContinuationPolicy.MaximumConsecutiveContinuations,
            goal.ConsecutiveContinuationCount);
        Assert.Contains("避免无界循环", goal.LastEvaluationReason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(
        "VERDICT: ACHIEVED\nREASON: Tests and build passed.",
        "Achieved")]
    [InlineData(
        "verdict: continue\nreason: One acceptance check is missing.",
        "Continue")]
    public void EvaluatorParsesOnlyTheBoundedTwoLineProtocol(
        string content,
        string expected)
    {
        Assert.True(CopilotGoalCompletionEvaluator.TryParse(
            content,
            new CopilotTokenUsage(5, 2, 7),
            out var result));
        Assert.Equal(
            Enum.Parse<CopilotGoalEvaluationVerdict>(expected, ignoreCase: true),
            result.Verdict);
        Assert.Equal(7, result.Usage.EffectiveTotalTokens);

        Assert.False(CopilotGoalCompletionEvaluator.TryParse(
            content + "\nNEXT: do more",
            CopilotTokenUsage.Empty,
            out _));
        Assert.False(CopilotGoalCompletionEvaluator.TryParse(
            "VERDICT: ACHIEVED\nREASON:",
            CopilotTokenUsage.Empty,
            out _));
    }

    [Fact]
    public void EvaluatorEvidenceKeepsOnlyTheNewestBoundedTranscript()
    {
        var transcript = Enumerable.Range(0, 30)
            .Select(index => new CopilotRequestMessage(
                index % 2 == 0 ? "user" : "assistant",
                $"message-{index:D2}-" + new string('x', 2_000)))
            .ToArray();

        var prompt = CopilotGoalCompletionEvaluator.BuildEvidencePrompt(
            "Finish with verified evidence",
            transcript);

        Assert.DoesNotContain("message-00-", prompt, StringComparison.Ordinal);
        Assert.Contains("message-29-", prompt, StringComparison.Ordinal);
        Assert.InRange(
            prompt.Length,
            1,
            CopilotGoalCompletionEvaluator.MaximumEvidenceCharacters + 2_000);
    }

    [Fact]
    public async Task EvaluatorUsesASeparateToolFreeBoundedProviderRequest()
    {
        var handler = new CapturingHandler();
        using var httpClient = new HttpClient(handler);
        var evaluator = new CopilotGoalCompletionEvaluator(new CopilotChatService(httpClient));
        var goal = CopilotConversationGoal.Create(
            "Finish the migration and verify tests",
            DateTimeOffset.UtcNow);

        var result = await evaluator.EvaluateAsync(
            CreateProfile(),
            goal,
            [
                new CopilotRequestMessage("user", "Finish the migration."),
                new CopilotRequestMessage("assistant", "The build and full tests passed."),
            ],
            CancellationToken.None);

        Assert.Equal(CopilotGoalEvaluationVerdict.Achieved, result.Verdict);
        Assert.Equal(16, result.Usage.EffectiveTotalTokens);
        Assert.Equal(1, handler.RequestCount);

        using var payload = JsonDocument.Parse(handler.LastPayload);
        var root = payload.RootElement;
        Assert.Equal(CopilotGoalCompletionEvaluator.MaximumOutputTokens, root.GetProperty("max_tokens").GetInt32());
        Assert.False(root.TryGetProperty("tools", out _));
        var messages = root.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Contains(
            "independent completion evaluator",
            messages[0].GetProperty("content").GetString(),
            StringComparison.Ordinal);
        Assert.Contains(
            "The build and full tests passed.",
            messages[1].GetProperty("content").GetString(),
            StringComparison.Ordinal);
    }

    private static CopilotProfileConfig CreateProfile()
    {
        return new CopilotProfileConfig
        {
            VendorType = CopilotVendorType.Custom,
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "test-key",
            BaseUrl = "https://example.test/v1",
            Model = "test-model",
            MaxTokens = 4_096,
        };
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public string LastPayload { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            LastPayload = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            const string response =
                """
                {
                  "choices": [
                    {
                      "message": {
                        "role": "assistant",
                        "content": "VERDICT: ACHIEVED\nREASON: The build and full tests passed."
                      },
                      "finish_reason": "stop"
                    }
                  ],
                  "usage": {
                    "prompt_tokens": 12,
                    "completion_tokens": 4,
                    "total_tokens": 16
                  }
                }
                """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json"),
            };
        }
    }
}
