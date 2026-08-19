using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotUserQuestionCheckpointTests
{
    [Fact]
    public async Task CoordinatorClosesQuestionWhenCheckpointBarrierRejects()
    {
        var coordinator = new CopilotUserQuestionCoordinator();
        var events = new List<CopilotAgentEvent>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.AskAsync(
                CreateRequest(),
                CreateInput(),
                events.Add,
                _ => ValueTask.FromResult(false),
                CancellationToken.None));

        Assert.Contains("could not be checkpointed", exception.Message, StringComparison.Ordinal);
        Assert.False(coordinator.HasPendingQuestion);
        Assert.Collection(
            events,
            requested =>
            {
                Assert.Equal(CopilotAgentEventType.UserQuestionRequested, requested.Type);
                Assert.Equal(CopilotUserQuestionResolution.Pending, requested.UserQuestion!.Resolution);
            },
            resolved =>
            {
                Assert.Equal(CopilotAgentEventType.UserQuestionResolved, resolved.Type);
                Assert.Equal(CopilotUserQuestionResolution.Cancelled, resolved.UserQuestion!.Resolution);
            });
        Assert.Equal(events[0].UserQuestion!.RequestId, events[1].UserQuestion!.RequestId);
    }

    [Fact]
    public async Task CoordinatorDoesNotAppendCancelledAfterAnsweredObserverFails()
    {
        var coordinator = new CopilotUserQuestionCoordinator();
        var events = new List<CopilotAgentEvent>();
        var askTask = coordinator.AskAsync(
            CreateRequest(),
            CreateInput(),
            agentEvent =>
            {
                events.Add(agentEvent);
                if (agentEvent.UserQuestion?.Resolution == CopilotUserQuestionResolution.Answered)
                    throw new InvalidOperationException("Expected observer failure.");
            },
            _ => ValueTask.FromResult(true),
            CancellationToken.None);
        var requested = Assert.Single(events).UserQuestion!;

        Assert.True(coordinator.TryAnswer(
            requested.TaskId,
            requested.RequestId,
            "Keep the current scope"));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => askTask);

        Assert.Equal("Expected observer failure.", exception.Message);
        Assert.False(coordinator.HasPendingQuestion);
        Assert.Collection(
            events,
            item => Assert.Equal(CopilotAgentEventType.UserQuestionRequested, item.Type),
            item => Assert.Equal(CopilotUserQuestionResolution.Answered, item.UserQuestion!.Resolution));
    }

    [Fact]
    public async Task CoordinatorDoesNotReturnAnswerWhenResolvedCheckpointIsRejected()
    {
        var coordinator = new CopilotUserQuestionCoordinator();
        var events = new List<CopilotAgentEvent>();
        var checkpointCount = 0;
        var askTask = coordinator.AskAsync(
            CreateRequest(),
            CreateInput(),
            events.Add,
            _ => ValueTask.FromResult(Interlocked.Increment(ref checkpointCount) == 1),
            CancellationToken.None);
        var requested = Assert.Single(events).UserQuestion!;

        Assert.True(coordinator.TryAnswer(
            requested.TaskId,
            requested.RequestId,
            "Keep the current scope"));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => askTask);

        Assert.Contains("answer could not be checkpointed", exception.Message, StringComparison.Ordinal);
        Assert.Equal(2, checkpointCount);
        Assert.False(coordinator.HasPendingQuestion);
        Assert.Collection(
            events,
            item => Assert.Equal(CopilotAgentEventType.UserQuestionRequested, item.Type),
            item => Assert.Equal(CopilotUserQuestionResolution.Answered, item.UserQuestion!.Resolution));
    }

    [Fact]
    public async Task AgentRuntimePublishesRequestedQuestionBeforeWaitingForAnswer()
    {
        var chatClient = new QuestionCallingChatClient();
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(Array.Empty<ICopilotTool>()),
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(),
            _ => chatClient,
            new EmptyExternalToolProvider());
        var events = new ConcurrentQueue<CopilotAgentEvent>();
        var requested = new TaskCompletionSource<CopilotUserQuestionSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var checkpointed = new TaskCompletionSource<CopilotAgentSessionCheckpoint>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var resolvedCheckpointed = new TaskCompletionSource<CopilotAgentSessionCheckpoint>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var requestedStatePersisted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var resolvedStatePersisted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var checkpointStage = 0;

        void Observe(CopilotAgentEvent agentEvent)
        {
            events.Enqueue(agentEvent);
            if (agentEvent is { Type: CopilotAgentEventType.UserQuestionRequested, UserQuestion: not null })
                requested.TrySetResult(agentEvent.UserQuestion);
            if (agentEvent is { Type: CopilotAgentEventType.CheckpointUpdated, SessionCheckpoint: not null }
                && agentEvent.SessionCheckpoint.TaskEventJournal.Events.Any(item =>
                    item.Type == CopilotAgentTaskEventType.UserQuestionRequested))
            {
                checkpointed.TrySetResult(agentEvent.SessionCheckpoint);
            }
            if (agentEvent is { Type: CopilotAgentEventType.CheckpointUpdated, SessionCheckpoint: not null }
                && agentEvent.SessionCheckpoint.TaskEventJournal.Events.Any(item =>
                    item.Type == CopilotAgentTaskEventType.UserQuestionResolved
                    && string.Equals(item.State, CopilotUserQuestionResolution.Answered.ToString(), StringComparison.Ordinal)))
            {
                Volatile.Write(ref checkpointStage, 2);
                resolvedCheckpointed.TrySetResult(agentEvent.SessionCheckpoint);
            }
            else if (agentEvent is { Type: CopilotAgentEventType.CheckpointUpdated, SessionCheckpoint: not null }
                && agentEvent.SessionCheckpoint.TaskEventJournal.Events.Any(item =>
                    item.Type == CopilotAgentTaskEventType.UserQuestionRequested))
            {
                Interlocked.CompareExchange(ref checkpointStage, 1, 0);
            }
        }

        var runTask = runtime.RunAsync(
            CreateRequest(_ =>
            {
                if (Volatile.Read(ref checkpointStage) >= 2)
                    resolvedStatePersisted.TrySetResult();
                else if (Volatile.Read(ref checkpointStage) == 1)
                    requestedStatePersisted.TrySetResult();
                return Task.CompletedTask;
            }),
            Observe,
            CancellationToken.None);
        var pendingQuestion = await requested.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var waitingCheckpoint = await checkpointed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await requestedStatePersisted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.False(runTask.IsCompleted);
        Assert.Contains(waitingCheckpoint.TaskEventJournal.Events, item =>
            item.Type == CopilotAgentTaskEventType.UserQuestionRequested
            && string.Equals(
                item.SubjectId,
                CopilotAgentTaskEventIds.ForUserQuestion(pendingQuestion.RequestId),
                StringComparison.Ordinal));
        Assert.True(runtime.TryAnswerUserQuestion(
            pendingQuestion.TaskId,
            pendingQuestion.RequestId,
            "Keep the current scope"));
        var answeredCheckpoint = await resolvedCheckpointed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await resolvedStatePersisted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        Assert.Equal(2, chatClient.CallCount);
        Assert.Contains(events, item =>
            item.Type == CopilotAgentEventType.UserQuestionResolved
            && item.UserQuestion?.Resolution == CopilotUserQuestionResolution.Answered);
        Assert.Contains(answeredCheckpoint.TaskEventJournal.Events, item =>
            item.Type == CopilotAgentTaskEventType.UserQuestionResolved
            && string.Equals(item.State, CopilotUserQuestionResolution.Answered.ToString(), StringComparison.Ordinal));
        Assert.True(result.TaskEventJournal.IsStructurallyValid());
    }

    private static CopilotAgentRequest CreateRequest(
        Func<CancellationToken, Task>? statePersistenceBarrier = null) => new()
    {
        ConversationId = "question-checkpoint-conversation",
        TaskId = CopilotAgentTaskEventIds.CreateRunId(),
        WorkspacePath = Path.GetTempPath(),
        UserText = "Ask one bounded clarification question.",
        TaskIntentText = "Ask one bounded clarification question.",
        Profile = new CopilotProfileConfig
        {
            VendorType = CopilotVendorType.Custom,
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "test-key",
            BaseUrl = "https://example.test/v1",
            Model = "test-model",
            MaxTokens = 4_096,
        },
        Mode = CopilotAgentMode.Plan,
        HarnessFeatures = CopilotAgentHarnessFeatures.None,
        CodexApprovalPolicy = CopilotCodexApprovalPolicy.CreateScalar(
            CopilotCodexApprovalPolicyMode.Untrusted),
        StatePersistenceBarrier = statePersistenceBarrier,
        RunBudgetOverride = new CopilotAgentRunBudgetOverride
        {
            RequestTokenBudget = 16_384,
            MaxToolCalls = 1,
            MaxAgentPasses = 1,
            TotalDuration = TimeSpan.FromSeconds(30),
        },
    };

    private static CopilotUserQuestionInput CreateInput() => new()
    {
        Header = "Scope",
        Question = "Which scope should be used?",
        Options =
        [
            new CopilotUserQuestionInputOption
            {
                Label = "Current (Recommended)",
                Description = "Keep the current bounded scope.",
            },
            new CopilotUserQuestionInputOption
            {
                Label = "Expand",
                Description = "Include adjacent modules.",
            },
        ],
    };

    private sealed class EmptyExternalToolProvider : ICopilotExternalToolProvider
    {
        public Task<CopilotExternalToolLease> DiscoverAsync(
            CopilotAgentRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new CopilotExternalToolLease());
        }
    }

    private sealed class QuestionCallingChatClient : IChatClient
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var call = Interlocked.Increment(ref _callCount);
            return Task.FromResult(CreateResponse(call));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var call = Interlocked.Increment(ref _callCount);
            await Task.CompletedTask;
            if (call == 1)
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, [CreateQuestionCall()])
                {
                    FinishReason = ChatFinishReason.ToolCalls,
                };
                yield break;
            }

            yield return new ChatResponseUpdate(ChatRole.Assistant, "The selected scope will be used.")
            {
                FinishReason = ChatFinishReason.Stop,
            };
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }

        private static ChatResponse CreateResponse(int call) => call == 1
            ? new ChatResponse(new ChatMessage(ChatRole.Assistant, [CreateQuestionCall()]))
            {
                FinishReason = ChatFinishReason.ToolCalls,
            }
            : new ChatResponse(new ChatMessage(ChatRole.Assistant, "The selected scope will be used."))
            {
                FinishReason = ChatFinishReason.Stop,
            };

        private static FunctionCallContent CreateQuestionCall() => new(
            "question-call",
            "AskUserQuestion",
            new Dictionary<string, object?>
            {
                ["header"] = "Scope",
                ["question"] = "Which scope should be used?",
                ["options"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["label"] = "Current (Recommended)",
                        ["description"] = "Keep the current bounded scope.",
                    },
                    new Dictionary<string, object?>
                    {
                        ["label"] = "Expand",
                        ["description"] = "Include adjacent modules.",
                    },
                },
            });
    }
}
