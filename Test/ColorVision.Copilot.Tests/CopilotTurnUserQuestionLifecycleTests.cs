using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotTurnUserQuestionLifecycleTests
{
    private const string TurnId = "run:11111111111111111111111111111111";

    [Fact]
    public void ReducerAcceptsMatchingRequestAndResolution()
    {
        var question = CreateQuestion(TurnId, "Choose a path?");
        var state = CreateStartedState();

        state = Observe(state, CopilotAgentEvent.UserQuestionRequested(question));
        state = Observe(
            state,
            CopilotAgentEvent.UserQuestionResolved(
                question.Resolve(CopilotUserQuestionResolution.Answered, "Option A")));
        state = Observe(state, CopilotAgentEvent.Completed());

        Assert.True(state.AgentCompleted);
    }

    [Fact]
    public void ReducerRejectsResolutionBeforeRequest()
    {
        var question = CreateQuestion(TurnId, "Choose a path?");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(
                CreateStartedState(),
                CopilotAgentEvent.UserQuestionResolved(
                    question.Resolve(CopilotUserQuestionResolution.Cancelled, string.Empty))));

        Assert.Contains("before requesting it", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsSecondQuestionWhileOneIsPending()
    {
        var state = Observe(
            CreateStartedState(),
            CopilotAgentEvent.UserQuestionRequested(CreateQuestion(TurnId, "First question?")));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(
                state,
                CopilotAgentEvent.UserQuestionRequested(CreateQuestion(TurnId, "Second question?"))));

        Assert.Contains("before resolving the active request", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsResolutionForDifferentRequest()
    {
        var first = CreateQuestion(TurnId, "First question?");
        var second = CreateQuestion(TurnId, "Second question?");
        var state = Observe(
            CreateStartedState(),
            CopilotAgentEvent.UserQuestionRequested(first));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(
                state,
                CopilotAgentEvent.UserQuestionResolved(
                    second.Resolve(CopilotUserQuestionResolution.Answered, "Option B"))));

        Assert.Contains("different user question", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsQuestionForDifferentTurn()
    {
        var question = CreateQuestion(
            "run:22222222222222222222222222222222",
            "Wrong turn?");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(
                CreateStartedState(),
                CopilotAgentEvent.UserQuestionRequested(question)));

        Assert.Contains("different turn ID", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsAgentCompletionWithPendingQuestion()
    {
        var state = Observe(
            CreateStartedState(),
            CopilotAgentEvent.UserQuestionRequested(CreateQuestion(TurnId, "Still pending?")));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(state, CopilotAgentEvent.Completed()));

        Assert.Contains("still pending", exception.Message, StringComparison.Ordinal);
    }

    private static CopilotTurnEventState CreateStartedState() =>
        CopilotTurnEventReducer.Reduce(
            CopilotTurnEventState.Create(CopilotAgentMode.Auto, TurnId),
            new CopilotTurnStartedEvent(TurnId, CopilotAgentMode.Auto));

    private static CopilotTurnEventState Observe(
        CopilotTurnEventState state,
        CopilotAgentEvent agentEvent) =>
        CopilotTurnEventReducer.Reduce(state, new CopilotTurnAgentEvent(agentEvent));

    private static CopilotUserQuestionSnapshot CreateQuestion(
        string taskId,
        string questionText)
    {
        Assert.True(CopilotUserQuestionSnapshot.TryCreate(
            "conversation:test",
            taskId,
            new CopilotUserQuestionInput
            {
                Header = "Choice",
                Question = questionText,
                Options =
                [
                    new CopilotUserQuestionInputOption
                    {
                        Label = "Option A",
                        Description = "Use the first path.",
                    },
                    new CopilotUserQuestionInputOption
                    {
                        Label = "Option B",
                        Description = "Use the second path.",
                    },
                ],
            },
            out var question,
            out var error), error);
        return question;
    }
}
