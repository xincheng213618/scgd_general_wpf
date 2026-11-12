using Microsoft.Extensions.AI;
using Newtonsoft.Json;
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

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task AgentRuntimePublishesRequestedQuestionBeforeWaitingForAnswer(bool changedModelOnRecovery, bool unicodeAnswer)
    {
        var chatClient = new QuestionCallingChatClient(unicodeQuestion: unicodeAnswer);
        var runtime = CreateRuntime(chatClient);
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
        var releaseResolvedCheckpoint = new TaskCompletionSource(
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
            CreateRequest(async token =>
            {
                if (Volatile.Read(ref checkpointStage) >= 2)
                {
                    resolvedStatePersisted.TrySetResult();
                    await releaseResolvedCheckpoint.Task.WaitAsync(token);
                }
                else if (Volatile.Read(ref checkpointStage) == 1)
                    requestedStatePersisted.TrySetResult();
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
        const string sensitiveSuffix = "\npassword=checkpoint-secret-value\nprofile-key=test-key";
        var answerBody = "Keep the current scope: clarification-recovery-unique-842 " + new string('x', 1_000);
        if (unicodeAnswer)
        {
            var prefix = "clarification-recovery-unique-842 " + new string('答', 1_300)
                + string.Concat(Enumerable.Repeat(" \n", 325));
            const string ending = "中文完整尾部🙂";
            answerBody = prefix + new string('答', CopilotUserQuestionSnapshot.MaximumAnswerCharacters
                - prefix.Length - ending.Length - sensitiveSuffix.Length) + ending;
        }
        var answer = answerBody + sensitiveSuffix;
        CopilotAgentSessionCheckpoint restored;
        CopilotRequestMessage[] clarificationFragments;
        CopilotAgentRunResult result;
        try
        {
            Assert.True(runtime.TryAnswerUserQuestion(
                pendingQuestion.TaskId,
                pendingQuestion.RequestId,
                answer));
            var answeredCheckpoint = await resolvedCheckpointed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await resolvedStatePersisted.Task.WaitAsync(TimeSpan.FromSeconds(10));

            // Simulate process exit after the answer checkpoint has reached persistence,
            // while the tool cannot yet return its answer to the framework history.
            Assert.False(runTask.IsCompleted);
            Assert.Equal(1, chatClient.CallCount);
            var persisted = JsonConvert.SerializeObject(answeredCheckpoint);
            restored = Assert.IsType<CopilotAgentSessionCheckpoint>(
                JsonConvert.DeserializeObject<CopilotAgentSessionCheckpoint>(persisted));
            Assert.True(restored.IsStructurallyValid());
            clarificationFragments = restored.ConversationMemory.Where(IsClarificationFragment).ToArray();
            var clarification = ReadClarificationPayload(clarificationFragments);
            Assert.Contains(pendingQuestion.Question, clarification, StringComparison.Ordinal);
            Assert.Contains(answerBody, clarification, StringComparison.Ordinal);
            if (unicodeAnswer)
            {
                Assert.Equal(CopilotUserQuestionSnapshot.MaximumAnswerCharacters, answer.Length);
                Assert.True(char.IsWhiteSpace(ReadFragmentBody(clarificationFragments[0])[^1]));
            }
            Assert.DoesNotContain("checkpoint-secret-value", persisted, StringComparison.Ordinal);
            Assert.DoesNotContain("test-key", persisted, StringComparison.Ordinal);
            Assert.DoesNotContain(restored.SessionConversationMemory ?? [], message =>
                IsClarificationFragment(message));
            Assert.DoesNotContain("clarification-recovery-unique-842", restored.SerializedSessionJson, StringComparison.Ordinal);
            Assert.Equal(CopilotAgentCheckpointCompatibilityKind.UnresolvedProviderToolCall,
                restored.EvaluateFor(CreateRequest().Profile, CopilotCapabilityCatalog.Shared.GetSnapshot()).Kind);
        }
        finally
        {
            releaseResolvedCheckpoint.TrySetResult();
            result = await runTask.WaitAsync(TimeSpan.FromSeconds(10));
        }

        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        Assert.Equal(2, chatClient.CallCount);
        Assert.Contains(events, item =>
            item.Type == CopilotAgentEventType.UserQuestionResolved
            && item.UserQuestion?.Resolution == CopilotUserQuestionResolution.Answered);
        Assert.Contains(restored.TaskEventJournal.Events, item =>
            item.Type == CopilotAgentTaskEventType.UserQuestionResolved
            && string.Equals(item.State, CopilotUserQuestionResolution.Answered.ToString(), StringComparison.Ordinal));
        Assert.True(result.TaskEventJournal.IsStructurallyValid());
        var finalCheckpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(result.SessionCheckpoint);
        Assert.Contains(answerBody, ReadClarificationPayload(finalCheckpoint.ConversationMemory), StringComparison.Ordinal);
        Assert.All(finalCheckpoint.ConversationMemory, message =>
        {
            Assert.DoesNotContain("checkpoint-secret-value", message.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("test-key", message.Content, StringComparison.Ordinal);
        });

        var recoveredClient = new QuestionCallingChatClient(askQuestion: false);
        var recoveredEvents = new List<CopilotAgentEvent>();
        var recovered = await CreateRuntime(recoveredClient).RunAsync(
            CreateRequest(sessionCheckpoint: restored,
                model: changedModelOnRecovery ? "changed-model" : "test-model"),
            recoveredEvents.Add,
            CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(CopilotAgentStopReason.Completed, recovered.StopReason);
        Assert.DoesNotContain(recoveredEvents, item =>
            item.Type == CopilotAgentEventType.RuntimeDiagnostic
            && item.Text.Contains("session resumed from the persisted conversation checkpoint", StringComparison.Ordinal));
        var recoveredPrompt = string.Join("\n", Assert.Single(recoveredClient.ProviderUserMessages));
        var fragmentCursor = 0;
        foreach (var fragment in clarificationFragments)
        {
            var index = recoveredPrompt.IndexOf(fragment.Content, fragmentCursor, StringComparison.Ordinal);
            Assert.True(index >= fragmentCursor, "The recovered provider request must contain every complete clarification fragment in order.");
            fragmentCursor = index + fragment.Content.Length;
        }
        Assert.DoesNotContain("checkpoint-secret-value", recoveredPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("test-key", recoveredPrompt, StringComparison.Ordinal);

        if (!changedModelOnRecovery)
        {
            var nativeCheckpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(
                JsonConvert.DeserializeObject<CopilotAgentSessionCheckpoint>(JsonConvert.SerializeObject(finalCheckpoint)));
            var nativeClient = new QuestionCallingChatClient(askQuestion: false);
            var nativeEvents = new List<CopilotAgentEvent>();
            var native = await CreateRuntime(nativeClient).RunAsync(
                CreateRequest(sessionCheckpoint: nativeCheckpoint), nativeEvents.Add,
                CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(CopilotAgentStopReason.Completed, native.StopReason);
            Assert.Contains(nativeEvents, item => item.Type == CopilotAgentEventType.RuntimeDiagnostic
                && item.Text.Contains("session resumed from the persisted conversation checkpoint", StringComparison.Ordinal));
            var questionResult = Assert.Single(Assert.Single(nativeClient.ProviderMessages)
                .SelectMany(message => message.Contents).OfType<FunctionResultContent>(),
                content => string.Equals(content.CallId, "question-call", StringComparison.Ordinal));
            var nativeAnswer = JsonConvert.DeserializeObject<Dictionary<string, string>>(questionResult.Result!.ToString()!);
            Assert.Equal(answer, nativeAnswer!["answer"]);
            var nativeUserPrompt = string.Join("\n", Assert.Single(nativeClient.ProviderUserMessages));
            Assert.DoesNotContain("checkpoint-secret-value", nativeUserPrompt, StringComparison.Ordinal);
            Assert.DoesNotContain("test-key", nativeUserPrompt, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AgentRuntimePreservesClarificationAndDeliveredSteeringOrder(bool steeringBeforeClarification)
    {
        const string answer = "clarification-order-answer-A-761";
        const string steering = "Use the later scope: clarification-order-steering-B-924.";
        var chatClient = new QuestionCallingChatClient();
        var runtime = CreateRuntime(chatClient);
        var requested = new TaskCompletionSource<CopilotUserQuestionSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var resolvedCheckpointed = new TaskCompletionSource<CopilotAgentSessionCheckpoint>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var resolvedStatePersisted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseResolvedCheckpoint = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        CopilotAgentSessionCheckpoint? pendingSteeringCheckpoint = null;
        var request = CreateRequest(async token =>
        {
            if (resolvedCheckpointed.Task.IsCompletedSuccessfully)
            {
                resolvedStatePersisted.TrySetResult();
                await releaseResolvedCheckpoint.Task.WaitAsync(token);
            }
        });
        chatClient.RequestStarted = call =>
        {
            // Ingress rejects steering while AskUserQuestion is still pending, including
            // its resolved checkpoint barrier. This callback runs after the tool returns,
            // while its answer is in the next provider request but is not yet stored.
            if (!steeringBeforeClarification && call == 2)
                Assert.True(runtime.EnqueueSteeringMessage(request.TaskId, steering).IsAccepted);
        };
        var runTask = runtime.RunAsync(request, agentEvent =>
        {
            if (steeringBeforeClarification && agentEvent.Type == CopilotAgentEventType.CheckpointReady)
                Assert.True(runtime.EnqueueSteeringMessage(request.TaskId, steering).IsAccepted);
            if (agentEvent is { Type: CopilotAgentEventType.UserQuestionRequested, UserQuestion: not null })
                requested.TrySetResult(agentEvent.UserQuestion);
            if (agentEvent is not { Type: CopilotAgentEventType.CheckpointUpdated, SessionCheckpoint: not null })
                return;

            var checkpoint = agentEvent.SessionCheckpoint;
            if (checkpoint.TaskEventJournal.Events.Any(item =>
                item.Type == CopilotAgentTaskEventType.UserQuestionResolved
                && string.Equals(item.State, CopilotUserQuestionResolution.Answered.ToString(), StringComparison.Ordinal)))
            {
                resolvedCheckpointed.TrySetResult(checkpoint);
            }
            if (!steeringBeforeClarification && chatClient.CallCount == 2
                && checkpoint.SessionConversationMemory?.Any(IsClarificationFragment) == true
                && !checkpoint.TaskEventJournal.Events.Any(item => item.Type == CopilotAgentTaskEventType.SteeringDelivered))
            {
                pendingSteeringCheckpoint ??= checkpoint;
            }
        }, CancellationToken.None);
        var pendingQuestion = await requested.Task.WaitAsync(TimeSpan.FromSeconds(10));
        CopilotAgentRunResult result;
        try
        {
            Assert.True(runtime.TryAnswerUserQuestion(pendingQuestion.TaskId, pendingQuestion.RequestId, answer));
            var answeredCheckpoint = await resolvedCheckpointed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await resolvedStatePersisted.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(1, chatClient.CallCount);
            Assert.Contains(answer, ReadClarificationPayload(answeredCheckpoint.ConversationMemory), StringComparison.Ordinal);
            Assert.DoesNotContain(answeredCheckpoint.SessionConversationMemory ?? [], IsClarificationFragment);
            Assert.DoesNotContain(answer, answeredCheckpoint.SerializedSessionJson, StringComparison.Ordinal);
            Assert.Equal(steeringBeforeClarification, Assert.Single(chatClient.ProviderMessages)
                .Any(message => message.Role == ChatRole.User && message.Text == steering));
            Assert.Equal(steeringBeforeClarification,
                answeredCheckpoint.ConversationMemory.Any(message => message.IsSteering && message.Content == steering));
            Assert.Equal(steeringBeforeClarification,
                answeredCheckpoint.SessionConversationMemory?.Any(message => message.IsSteering && message.Content == steering) == true);
            Assert.Equal(steeringBeforeClarification ? 1 : 0, answeredCheckpoint.TaskEventJournal.Events.Count(item =>
                item.Type == CopilotAgentTaskEventType.SteeringDelivered));
        }
        finally
        {
            releaseResolvedCheckpoint.TrySetResult();
            result = await runTask.WaitAsync(TimeSpan.FromSeconds(10));
        }

        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        Assert.Equal(steeringBeforeClarification ? 2 : 3, chatClient.CallCount);
        if (!steeringBeforeClarification)
        {
            var pendingCheckpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(pendingSteeringCheckpoint);
            Assert.Contains(answer, ReadClarificationPayload(pendingCheckpoint.SessionConversationMemory!), StringComparison.Ordinal);
            Assert.DoesNotContain(pendingCheckpoint.ConversationMemory, message => message.IsSteering && message.Content == steering);
            Assert.DoesNotContain(pendingCheckpoint.SessionConversationMemory!, message => message.IsSteering && message.Content == steering);
            Assert.Contains(steering, pendingCheckpoint.SerializedSessionJson, StringComparison.Ordinal);
        }

        var finalCheckpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(result.SessionCheckpoint);
        var delivered = Assert.Single(finalCheckpoint.TaskEventJournal.Events,
            item => item.Type == CopilotAgentTaskEventType.SteeringDelivered);
        var resolved = Assert.Single(finalCheckpoint.TaskEventJournal.Events,
            item => item.Type == CopilotAgentTaskEventType.UserQuestionResolved
                && item.State == CopilotUserQuestionResolution.Answered.ToString());
        Assert.Equal(steeringBeforeClarification, delivered.Sequence < resolved.Sequence);
        var finalProviderMessages = chatClient.ProviderMessages.Last();
        var answerMessage = Assert.Single(finalProviderMessages, message => message.Contents
            .OfType<FunctionResultContent>().Any(content => content.CallId == "question-call"));
        var nativeAnswer = JsonConvert.DeserializeObject<Dictionary<string, string>>(Assert.Single(
            answerMessage.Contents.OfType<FunctionResultContent>()).Result!.ToString()!);
        Assert.Equal(answer, nativeAnswer!["answer"]);
        var steeringMessage = Assert.Single(finalProviderMessages,
            message => message.Role == ChatRole.User && message.Text == steering);
        Assert.Equal(steeringBeforeClarification,
            Array.IndexOf(finalProviderMessages, steeringMessage) < Array.IndexOf(finalProviderMessages, answerMessage));
        Assert.Single(finalCheckpoint.SessionConversationMemory!, IsClarificationFragment);
        Assert.Single(finalCheckpoint.SessionConversationMemory!, message => message.IsSteering && message.Content == steering);

        var restored = Assert.IsType<CopilotAgentSessionCheckpoint>(JsonConvert.DeserializeObject<CopilotAgentSessionCheckpoint>(
            JsonConvert.SerializeObject(finalCheckpoint)));
        var recoveredClient = new QuestionCallingChatClient(askQuestion: false);
        var recoveredEvents = new List<CopilotAgentEvent>();
        var recovered = await CreateRuntime(recoveredClient).RunAsync(
            CreateRequest(sessionCheckpoint: restored, model: "changed-model"), recoveredEvents.Add,
            CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(CopilotAgentStopReason.Completed, recovered.StopReason);
        Assert.DoesNotContain(recoveredEvents, item => item.Type == CopilotAgentEventType.RuntimeDiagnostic
            && item.Text.Contains("session resumed from the persisted conversation checkpoint", StringComparison.Ordinal));
        var clarification = Assert.Single(restored.ConversationMemory, IsClarificationFragment);
        var recoveredPrompt = string.Join("\n", Assert.Single(recoveredClient.ProviderUserMessages));
        Assert.Equal(1, recoveredPrompt.Split(clarification.Content, StringSplitOptions.None).Length - 1);
        Assert.Equal(1, recoveredPrompt.Split(answer, StringSplitOptions.None).Length - 1);
        Assert.Equal(1, recoveredPrompt.Split(steering, StringSplitOptions.None).Length - 1);
        var memory = restored.ConversationMemory.ToArray();
        var memorySteeringFirst = Array.FindIndex(memory, message => message.IsSteering && message.Content == steering)
            < Array.FindIndex(memory, IsClarificationFragment);
        var recoveredSteeringFirst = recoveredPrompt.IndexOf(steering, StringComparison.Ordinal)
            < recoveredPrompt.IndexOf(clarification.Content, StringComparison.Ordinal);
        Assert.True(memorySteeringFirst == steeringBeforeClarification && recoveredSteeringFirst == steeringBeforeClarification,
            $"Expected steering-before-clarification={steeringBeforeClarification}; checkpoint={memorySteeringFirst}; rebuilt provider={recoveredSteeringFirst}.");
    }

    private static bool IsClarificationFragment(CopilotRequestMessage message) =>
        message.Content.StartsWith("# User clarification", StringComparison.Ordinal);

    private static string ReadFragmentBody(CopilotRequestMessage message)
    {
        const string footer = "\n# End user clarification fragment";
        var start = message.Content.IndexOf("\n\n", StringComparison.Ordinal);
        Assert.True(start >= 0);
        Assert.EndsWith(footer, message.Content, StringComparison.Ordinal);
        return message.Content[(start + 2)..^footer.Length];
    }

    private static string ReadClarificationPayload(IEnumerable<CopilotRequestMessage> memory)
    {
        var fragments = memory.Where(IsClarificationFragment).ToArray();
        Assert.NotEmpty(fragments);
        foreach (var fragment in fragments)
        {
            Assert.Equal("user", fragment.Role);
            Assert.Contains("not authorization", fragment.Content, StringComparison.Ordinal);
            Assert.InRange(CopilotTokenEstimator.EstimateTextWeight(fragment.Content), 1,
                CopilotAgentSessionCheckpoint.MaxConversationMemoryContentLength);
            var body = ReadFragmentBody(fragment);
            for (var index = 0; index < body.Length; index++)
            {
                if (char.IsHighSurrogate(body[index]))
                {
                    Assert.True(index + 1 < body.Length && char.IsLowSurrogate(body[index + 1]));
                    index++;
                }
                else
                {
                    Assert.False(char.IsLowSurrogate(body[index]));
                }
            }
        }
        return string.Concat(fragments.Select(ReadFragmentBody));
    }

    private static CopilotMicrosoftAgentFrameworkRuntime CreateRuntime(IChatClient chatClient) => new(
        new CopilotToolRegistry(Array.Empty<ICopilotTool>()),
        new CopilotAgentContextBuilder(),
        new CopilotToolExecutor(),
        _ => chatClient,
        new EmptyExternalToolProvider());

    private static CopilotAgentRequest CreateRequest(
        Func<CancellationToken, Task>? statePersistenceBarrier = null,
        CopilotAgentSessionCheckpoint? sessionCheckpoint = null,
        string model = "test-model") => new()
    {
        ConversationId = "question-checkpoint-conversation",
        TaskId = CopilotAgentTaskEventIds.CreateRunId(),
        WorkspacePath = Path.GetTempPath(),
        UserText = "Ask one bounded clarification question.",
        TaskIntentText = "Ask one bounded clarification question.",
        Profile = new CopilotProfileConfig
        {
            Id = "question-checkpoint-profile",
            VendorType = CopilotVendorType.Custom,
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "test-key",
            BaseUrl = "https://example.test/v1",
            Model = model,
            MaxTokens = 4_096,
        },
        Mode = CopilotAgentMode.Plan,
        HarnessFeatures = CopilotAgentHarnessFeatures.None,
        CodexApprovalPolicy = CopilotCodexApprovalPolicy.CreateScalar(
            CopilotCodexApprovalPolicyMode.Untrusted),
        StatePersistenceBarrier = statePersistenceBarrier,
        SessionCheckpoint = sessionCheckpoint,
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

    private sealed class QuestionCallingChatClient(bool askQuestion = true, bool unicodeQuestion = false) : IChatClient
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public ConcurrentQueue<string[]> ProviderUserMessages { get; } = new();

        public ConcurrentQueue<ChatMessage[]> ProviderMessages { get; } = new();

        public Action<int>? RequestStarted { get; set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RecordMessages(messages);
            var call = Interlocked.Increment(ref _callCount);
            RequestStarted?.Invoke(call);
            return Task.FromResult(CreateResponse(askQuestion ? call : 2));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RecordMessages(messages);
            var call = Interlocked.Increment(ref _callCount);
            RequestStarted?.Invoke(call);
            await Task.CompletedTask;
            if (askQuestion && call == 1)
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

        private void RecordMessages(IEnumerable<ChatMessage> messages)
        {
            var snapshot = messages.ToArray();
            ProviderMessages.Enqueue(snapshot);
            ProviderUserMessages.Enqueue(snapshot.Where(message => message.Role == ChatRole.User)
                .Select(message => message.Text).ToArray());
        }

        private ChatResponse CreateResponse(int call) => call == 1
            ? new ChatResponse(new ChatMessage(ChatRole.Assistant, [CreateQuestionCall()]))
            {
                FinishReason = ChatFinishReason.ToolCalls,
            }
            : new ChatResponse(new ChatMessage(ChatRole.Assistant, "The selected scope will be used."))
            {
                FinishReason = ChatFinishReason.Stop,
            };

        private FunctionCallContent CreateQuestionCall() => new(
            "question-call",
            "AskUserQuestion",
            new Dictionary<string, object?>
            {
                ["header"] = "Scope",
                ["question"] = unicodeQuestion ? "请选择范围：" + new string('问', 480) : "Which scope should be used?",
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
