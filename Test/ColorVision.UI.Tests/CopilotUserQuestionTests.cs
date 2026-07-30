using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotUserQuestionTests
{
    [Fact]
    public async Task ExactTaskAndRequestAnswerResumesWaitingQuestionOnce()
    {
        var coordinator = new CopilotUserQuestionCoordinator();
        var request = CreateRequest();
        var events = new List<CopilotAgentEvent>();

        var pending = coordinator.AskAsync(
            request,
            CreateInput(),
            events.Add,
            CancellationToken.None);

        var requested = Assert.Single(events);
        Assert.Equal(CopilotAgentEventType.UserQuestionRequested, requested.Type);
        Assert.True(requested.UserQuestion?.IsPending);
        Assert.True(coordinator.HasPendingQuestion);
        Assert.False(coordinator.TryAnswer("run:" + new string('0', 32), requested.UserQuestion!.RequestId, "Option A"));
        Assert.False(coordinator.TryAnswer(request.TaskId, "question:" + new string('0', 32), "Option A"));

        Assert.True(coordinator.TryAnswer(request.TaskId, requested.UserQuestion.RequestId, "Option A"));
        Assert.False(coordinator.TryAnswer(request.TaskId, requested.UserQuestion.RequestId, "Option B"));

        var resolved = await pending;
        Assert.Equal(CopilotUserQuestionResolution.Answered, resolved.Resolution);
        Assert.Equal("Option A", resolved.Answer);
        Assert.False(coordinator.HasPendingQuestion);
        Assert.Collection(
            events,
            item => Assert.Equal(CopilotAgentEventType.UserQuestionRequested, item.Type),
            item => Assert.Equal(CopilotAgentEventType.UserQuestionResolved, item.Type));
    }

    [Fact]
    public async Task CancellationClosesPendingQuestionWithoutAcceptingLateAnswer()
    {
        var coordinator = new CopilotUserQuestionCoordinator();
        var request = CreateRequest();
        var events = new List<CopilotAgentEvent>();
        using var cancellation = new CancellationTokenSource();
        var pending = coordinator.AskAsync(
            request,
            CreateInput(),
            events.Add,
            cancellation.Token);
        var question = Assert.Single(events).UserQuestion!;

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.False(coordinator.HasPendingQuestion);
        Assert.False(coordinator.TryAnswer(request.TaskId, question.RequestId, "late answer"));
        var resolved = Assert.Single(events, item => item.Type == CopilotAgentEventType.UserQuestionResolved);
        Assert.Equal(CopilotUserQuestionResolution.Cancelled, resolved.UserQuestion?.Resolution);
    }

    [Fact]
    public async Task DuplicateOptionsFailBeforePublishingQuestion()
    {
        var coordinator = new CopilotUserQuestionCoordinator();
        var events = new List<CopilotAgentEvent>();
        var input = new CopilotUserQuestionInput
        {
            Header = "Target",
            Question = "Which target should be used?",
            Options =
            [
                new CopilotUserQuestionInputOption { Label = "Same", Description = "First." },
                new CopilotUserQuestionInputOption { Label = "same", Description = "Second." },
            ],
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            coordinator.AskAsync(CreateRequest(), input, events.Add, CancellationToken.None));

        Assert.Empty(events);
        Assert.False(coordinator.HasPendingQuestion);
    }

    [Fact]
    public async Task JournalRecordsLifecycleWithoutPersistingQuestionOrAnswerText()
    {
        var coordinator = new CopilotUserQuestionCoordinator();
        var request = CreateRequest();
        var journal = new CopilotAgentTaskEventJournalBuilder(runId: request.TaskId);
        var events = new List<CopilotAgentEvent>();
        void Emit(CopilotAgentEvent agentEvent)
        {
            events.Add(agentEvent);
            journal.Observe(agentEvent);
        }

        var pending = coordinator.AskAsync(request, CreateInput(), Emit, CancellationToken.None);
        var question = Assert.Single(events).UserQuestion!;
        const string answer = "private answer text";
        Assert.True(coordinator.TryAnswer(request.TaskId, question.RequestId, answer));
        await pending;

        var lifecycle = journal.Snapshot().Events
            .Where(item => item.Type is CopilotAgentTaskEventType.UserQuestionRequested
                or CopilotAgentTaskEventType.UserQuestionResolved)
            .ToArray();
        Assert.Equal(2, lifecycle.Length);
        Assert.All(lifecycle, item =>
        {
            Assert.DoesNotContain(question.Question, item.Summary, StringComparison.Ordinal);
            Assert.DoesNotContain(answer, item.Summary, StringComparison.Ordinal);
            Assert.True(item.IsStructurallyValid());
        });
    }

    [Fact]
    public async Task FrameworkFunctionBindsSchemaArgumentsAndReturnsOnlyAcceptedAnswer()
    {
        var coordinator = new CopilotUserQuestionCoordinator();
        var request = CreateRequest();
        var events = new List<CopilotAgentEvent>();
        var function = new CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge.UserQuestionAIFunction(
            coordinator,
            request,
            events.Add);
        Assert.Contains("alone in a provider response", function.Description, StringComparison.Ordinal);
        var arguments = new AIFunctionArguments
        {
            ["header"] = "Target",
            ["question"] = "Which target should be used?",
            ["options"] = JsonSerializer.SerializeToElement(new[]
            {
                new { label = "Option A", description = "Use the first target." },
                new { label = "Option B", description = "Use the second target." },
            }),
        };

        var invocation = function.InvokeAsync(arguments, CancellationToken.None).AsTask();
        var question = Assert.Single(events).UserQuestion!;
        Assert.True(coordinator.TryAnswer(request.TaskId, question.RequestId, "typed answer"));

        var result = Assert.IsType<string>(await invocation);
        using var document = JsonDocument.Parse(result);
        Assert.Equal("answered", document.RootElement.GetProperty("outcome").GetString());
        Assert.Equal("typed answer", document.RootElement.GetProperty("answer").GetString());
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public void PresenterShowsPendingQuestionAndKeepsResolvedAnswer()
    {
        Assert.True(CopilotUserQuestionSnapshot.TryCreate(
            "conversation",
            CopilotAgentTaskEventIds.CreateRunId(),
            CreateInput(),
            out var question,
            out var error),
            error);
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);

        var requested = CopilotAssistantMessagePresenter.ApplyAgentEvent(
            message,
            CopilotAgentEvent.UserQuestionRequested(question));

        Assert.True(requested.IsHandled);
        Assert.True(message.HasPendingUserQuestion);
        Assert.True(message.IsExecutionInProgress);

        CopilotAssistantMessagePresenter.ApplyAgentEvent(
            message,
            CopilotAgentEvent.UserQuestionResolved(
                question.Resolve(CopilotUserQuestionResolution.Answered, "Option B")));

        Assert.False(message.HasPendingUserQuestion);
        Assert.True(message.HasResolvedUserQuestion);
        Assert.Contains("Option B", message.UserQuestionStatusText, StringComparison.Ordinal);
    }

    [Fact]
    public void QuestionRequestClearsAnyProvisionalAnswerBeforeThePause()
    {
        Assert.True(CopilotMicrosoftAgentFrameworkRuntime.ShouldResetAnswerBeforeEvent(
            CopilotAgentEventType.UserQuestionRequested,
            answerLength: 12));
        Assert.False(CopilotMicrosoftAgentFrameworkRuntime.ShouldResetAnswerBeforeEvent(
            CopilotAgentEventType.UserQuestionRequested,
            answerLength: 0));
    }

    private static CopilotAgentRequest CreateRequest() => new()
    {
        ConversationId = "conversation",
        TaskId = CopilotAgentTaskEventIds.CreateRunId(),
    };

    private static CopilotUserQuestionInput CreateInput() => new()
    {
        Header = "Target",
        Question = "Which target should be used?",
        Options =
        [
            new CopilotUserQuestionInputOption
            {
                Label = "Option A",
                Description = "Use the first target.",
            },
            new CopilotUserQuestionInputOption
            {
                Label = "Option B",
                Description = "Use the second target.",
            },
        ],
    };
}
