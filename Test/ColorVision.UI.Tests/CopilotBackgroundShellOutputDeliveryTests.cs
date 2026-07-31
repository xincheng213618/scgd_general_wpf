using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using ColorVision.Solution;
using Microsoft.Extensions.AI;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ColorVision.UI.Tests;

[Collection(CopilotApprovalReviewTestGroup.CollectionName)]
public sealed class CopilotBackgroundShellOutputDeliveryTests
{
    [Fact]
    public void SteeringRejectsInvalidInputBeforeLookingForActiveTask()
    {
        using var provider = new CapturingChatClient();
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry([]),
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(),
            _ => provider,
            EmptyExternalToolProvider.Instance,
            new CopilotCapabilityCatalog());

        var emptyAdmission = runtime.EnqueueSteeringMessage(
            string.Empty,
            string.Empty);
        var oversizedAdmission = runtime.EnqueueSteeringMessage(
            CopilotAgentTaskEventIds.CreateRunId(),
            new string('x', 16_001));

        Assert.Equal(
            CopilotSteeringAdmissionReason.InvalidInput,
            emptyAdmission.Reason);
        Assert.Equal(
            CopilotSteeringAdmissionReason.InvalidInput,
            oversizedAdmission.Reason);
    }

    [Fact]
    public async Task SteeringRequiresExactActiveTask()
    {
        using var provider = new BlockingSteeringChatClient();
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry([]),
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(),
            _ => provider,
            EmptyExternalToolProvider.Instance,
            new CopilotCapabilityCatalog());
        var request = CreateRequest(
            "conversation",
            "Wait for steering before completing.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = runtime.RunAsync(
            request,
            _ => { },
            timeout.Token);
        await provider.StreamStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5));

        var staleTaskAdmission = runtime.EnqueueSteeringMessage(
            CopilotAgentTaskEventIds.CreateRunId(),
            "stale steering");
        var activeTaskAdmission = runtime.EnqueueSteeringMessage(
            request.TaskId,
            "active steering");
        provider.ReleaseStream.TrySetResult();
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(
            CopilotSteeringAdmissionReason.NoActiveTask,
            staleTaskAdmission.Reason);
        Assert.True(activeTaskAdmission.IsAccepted);
        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        Assert.DoesNotContain(
            provider.StreamingCalls.SelectMany(call => call),
            message => message.Text.Contains(
                "stale steering",
                StringComparison.Ordinal));
        Assert.Contains(
            provider.StreamingCalls.SelectMany(call => call),
            message => message.Text.Contains(
                "active steering",
                StringComparison.Ordinal));
        Assert.Single(
            result.TaskEventJournal.Events,
            item => item.Type
                == CopilotAgentTaskEventType.SteeringQueued);
    }

    [Fact]
    public async Task SteeringRejectsMessagesBeyondPendingCountBudget()
    {
        using var provider = new BlockingSteeringChatClient();
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry([]),
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(),
            _ => provider,
            EmptyExternalToolProvider.Instance,
            new CopilotCapabilityCatalog());
        var request = CreateRequest(
            "conversation",
            "Wait while the steering queue fills.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = runtime.RunAsync(
            request,
            _ => { },
            timeout.Token);
        await provider.StreamStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5));

        var accepted = Enumerable.Range(1, 9)
            .Select(index => runtime.EnqueueSteeringMessage(
                request.TaskId,
                $"steering {index}"))
            .ToArray();
        provider.ReleaseStream.TrySetResult();
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.All(accepted[..8], item => Assert.True(item.IsAccepted));
        Assert.Equal(
            CopilotSteeringAdmissionReason.QueueFull,
            accepted[8].Reason);
        Assert.Equal(
            8,
            result.TaskEventJournal.Events.Count(
                item => item.Type
                    == CopilotAgentTaskEventType.SteeringQueued));
        Assert.DoesNotContain(
            provider.StreamingCalls.SelectMany(call => call),
            message => message.Text.Contains(
                "steering 9",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task SteeringRejectsMessagesBeyondPendingCharacterBudget()
    {
        using var provider = new BlockingSteeringChatClient();
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry([]),
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(),
            _ => provider,
            EmptyExternalToolProvider.Instance,
            new CopilotCapabilityCatalog());
        var request = CreateRequest(
            "conversation",
            "Wait while the steering character budget fills.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = runtime.RunAsync(
            request,
            _ => { },
            timeout.Token);
        await provider.StreamStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5));

        var firstAdmission = runtime.EnqueueSteeringMessage(
            request.TaskId,
            new string('a', 16_000));
        var secondAdmission = runtime.EnqueueSteeringMessage(
            request.TaskId,
            new string('b', 16_000));
        var overflowAdmission = runtime.EnqueueSteeringMessage(
            request.TaskId,
            "overflow");
        provider.ReleaseStream.TrySetResult();
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(firstAdmission.IsAccepted);
        Assert.True(secondAdmission.IsAccepted);
        Assert.Equal(
            CopilotSteeringAdmissionReason.QueueFull,
            overflowAdmission.Reason);
        Assert.Equal(
            2,
            result.TaskEventJournal.Events.Count(
                item => item.Type
                    == CopilotAgentTaskEventType.SteeringQueued));
        Assert.DoesNotContain(
            provider.StreamingCalls.SelectMany(call => call),
            message => message.Text.Contains(
                "overflow",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task SteeringIsRejectedAfterAgentLoopEntersFinalization()
    {
        using var provider = new CapturingChatClient();
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry([]),
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(),
            _ => provider,
            EmptyExternalToolProvider.Instance,
            new CopilotCapabilityCatalog());
        var request = CreateRequest(
            "conversation",
            "Complete this run before finalization is released.");
        var finalizationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFinalization = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = Task.Run(() => runtime.RunAsync(
                request,
                agentEvent =>
                {
                    if (agentEvent.Type != CopilotAgentEventType.RuntimeDiagnostic
                        || !agentEvent.Text.Contains(
                            "Agent stop reason",
                            StringComparison.Ordinal))
                    {
                        return;
                    }

                    finalizationStarted.TrySetResult();
                    releaseFinalization.Task.GetAwaiter().GetResult();
                },
                timeout.Token),
            timeout.Token);

        CopilotSteeringAdmissionResult lateSteeringAdmission;
        try
        {
            await finalizationStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(5));
            lateSteeringAdmission = runtime.EnqueueSteeringMessage(
                request.TaskId,
                "late steering");
        }
        finally
        {
            releaseFinalization.TrySetResult();
        }

        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(
            CopilotSteeringAdmissionReason.NoActiveTask,
            lateSteeringAdmission.Reason);
        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        Assert.DoesNotContain(
            result.TaskEventJournal.Events,
            item => item.Type
                == CopilotAgentTaskEventType.SteeringQueued);
        Assert.DoesNotContain(
            provider.StreamingCalls.SelectMany(call => call),
            message => message.Text.Contains(
                "late steering",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task SteeringAcceptedAtLoopBoundaryIsDrainedBeforeFinalization()
    {
        using var provider = new CapturingChatClient();
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry([]),
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(),
            _ => provider,
            EmptyExternalToolProvider.Instance,
            new CopilotCapabilityCatalog());
        var request = CreateRequest(
            "conversation",
            "Complete only after the final steering drain.");
        var sealingStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSealing = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = Task.Run(() => runtime.RunAsync(
                request,
                agentEvent =>
                {
                    if (agentEvent.Type != CopilotAgentEventType.RuntimeDiagnostic
                        || !agentEvent.Text.Contains(
                            "live steering input is now sealed",
                            StringComparison.Ordinal))
                    {
                        return;
                    }

                    sealingStarted.TrySetResult();
                    releaseSealing.Task.GetAwaiter().GetResult();
                },
                timeout.Token),
            timeout.Token);

        CopilotSteeringAdmissionResult boundaryAdmission;
        try
        {
            await sealingStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(5));
            boundaryAdmission = runtime.EnqueueSteeringMessage(
                request.TaskId,
                "boundary steering");
        }
        finally
        {
            releaseSealing.TrySetResult();
        }

        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(boundaryAdmission.IsAccepted);
        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        Assert.Equal(2, provider.StreamingCalls.Count);
        Assert.DoesNotContain(
            provider.StreamingCalls[0],
            message => message.Text.Contains(
                "boundary steering",
                StringComparison.Ordinal));
        Assert.Contains(
            provider.StreamingCalls[1],
            message => message.Text.Contains(
                "boundary steering",
                StringComparison.Ordinal));
        Assert.Single(
            result.TaskEventJournal.Events,
            item => item.Type
                == CopilotAgentTaskEventType.SteeringQueued);
    }

    [Fact]
    public async Task NextAgentRunReceivesDelayedOutputBeforeCurrentUserRequest()
    {
        using var provider = new CapturingChatClient();
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry([]),
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(),
            _ => provider,
            EmptyExternalToolProvider.Instance,
            new CopilotCapabilityCatalog());
        Assert.True(runtime.TryEnqueueBackgroundShellCommandOutput(
            CreateOutputEvent("conversation", "ready")));

        var result = await runtime.RunAsync(
            CreateRequest("conversation", "Handle the current user request."),
            _ => { },
            CancellationToken.None);

        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        var messages = Assert.Single(provider.StreamingCalls);
        var userMessages = messages
            .Where(message => message.Role == ChatRole.User)
            .ToArray();
        var finalUserMessage = Assert.Single(userMessages);
        var delayedEventIndex = finalUserMessage.Text.IndexOf(
            "<background_command_output_event>",
            StringComparison.Ordinal);
        var currentRequestIndex = finalUserMessage.Text.IndexOf(
            "Handle the current user request.",
            StringComparison.Ordinal);
        Assert.True(delayedEventIndex >= 0);
        Assert.True(currentRequestIndex > delayedEventIndex);
        Assert.Contains(
            "\"delivery\":\"delayed\"",
            finalUserMessage.Text,
            StringComparison.Ordinal);
        Assert.Contains(
            result.TaskEventJournal.Events,
            item => item.Type
                == CopilotAgentTaskEventType
                    .BackgroundCommandOutputObserved);
    }

    [Fact]
    public async Task NextAgentRunReceivesDelayedTerminalBeforeCurrentUserRequest()
    {
        using var provider = new CapturingChatClient();
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry([]),
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(),
            _ => provider,
            EmptyExternalToolProvider.Instance,
            new CopilotCapabilityCatalog());
        Assert.True(runtime.TryEnqueueBackgroundShellCommandCompletion(
            CreateCompletedCommand("conversation")));

        var result = await runtime.RunAsync(
            CreateRequest("conversation", "Handle the current user request."),
            _ => { },
            CancellationToken.None);

        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        var messages = Assert.Single(provider.StreamingCalls);
        var finalUserMessage = Assert.Single(
            messages,
            message => message.Role == ChatRole.User);
        var terminalEventIndex = finalUserMessage.Text.IndexOf(
            "<background_command_event>",
            StringComparison.Ordinal);
        var currentRequestIndex = finalUserMessage.Text.IndexOf(
            "Handle the current user request.",
            StringComparison.Ordinal);
        Assert.True(terminalEventIndex >= 0);
        Assert.True(currentRequestIndex > terminalEventIndex);
        Assert.Contains(
            "\"background_id\":\"background:deferred\"",
            finalUserMessage.Text,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"state\":\"completed\"",
            finalUserMessage.Text,
            StringComparison.Ordinal);
        Assert.Single(
            result.TaskEventJournal.Events,
            item => item.Type
                == CopilotAgentTaskEventType.BackgroundCommandCompleted);
    }

    [Fact]
    public async Task FailureBeforeFirstProviderUpdateReturnsDeliveryForRetry()
    {
        using var failingProvider =
            new FailBeforeFirstUpdateChatClient();
        using var recoveredProvider = new CapturingChatClient();
        var providerNumber = 0;
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry([]),
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(),
            _ => Interlocked.Increment(ref providerNumber) == 1
                ? failingProvider
                : recoveredProvider,
            EmptyExternalToolProvider.Instance,
            new CopilotCapabilityCatalog());
        Assert.True(runtime.TryEnqueueBackgroundShellCommandOutput(
            CreateOutputEvent("conversation", "retry me")));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runtime.RunAsync(
                CreateRequest("conversation", "First attempt."),
                _ => { },
                CancellationToken.None));
        var failedPrompt = Assert.Single(
            Assert.Single(failingProvider.StreamingCalls),
            message => message.Role == ChatRole.User);

        var recoveredResult = await runtime.RunAsync(
            CreateRequest("conversation", "Retry attempt."),
            _ => { },
            CancellationToken.None);

        Assert.Equal(
            CopilotAgentStopReason.Completed,
            recoveredResult.StopReason);
        var retriedPrompt = Assert.Single(
            Assert.Single(recoveredProvider.StreamingCalls),
            message => message.Role == ChatRole.User);
        Assert.Contains(
            "\"content\":\"retry me\"",
            retriedPrompt.Text,
            StringComparison.Ordinal);
        Assert.Equal(
            ExtractDeliveryId(failedPrompt.Text),
            ExtractDeliveryId(retriedPrompt.Text));
        Assert.Single(
            recoveredResult.TaskEventJournal.Events,
            item => item.Type
                == CopilotAgentTaskEventType
                    .BackgroundCommandOutputObserved);
    }

    [Fact]
    public async Task FailureBeforeFirstProviderUpdateReturnsTerminalForRetry()
    {
        using var failingProvider =
            new FailBeforeFirstUpdateChatClient();
        using var recoveredProvider = new CapturingChatClient();
        var providerNumber = 0;
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry([]),
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(),
            _ => Interlocked.Increment(ref providerNumber) == 1
                ? failingProvider
                : recoveredProvider,
            EmptyExternalToolProvider.Instance,
            new CopilotCapabilityCatalog());
        Assert.True(runtime.TryEnqueueBackgroundShellCommandCompletion(
            CreateCompletedCommand("conversation")));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runtime.RunAsync(
                CreateRequest("conversation", "First attempt."),
                _ => { },
                CancellationToken.None));
        var failedPrompt = Assert.Single(
            Assert.Single(failingProvider.StreamingCalls),
            message => message.Role == ChatRole.User);

        var recoveredResult = await runtime.RunAsync(
            CreateRequest("conversation", "Retry attempt."),
            _ => { },
            CancellationToken.None);

        Assert.Equal(
            CopilotAgentStopReason.Completed,
            recoveredResult.StopReason);
        var retriedPrompt = Assert.Single(
            Assert.Single(recoveredProvider.StreamingCalls),
            message => message.Role == ChatRole.User);
        Assert.Contains(
            "<background_command_event>",
            failedPrompt.Text,
            StringComparison.Ordinal);
        Assert.Contains(
            "<background_command_event>",
            retriedPrompt.Text,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"background_id\":\"background:deferred\"",
            retriedPrompt.Text,
            StringComparison.Ordinal);
        Assert.Single(
            recoveredResult.TaskEventJournal.Events,
            item => item.Type
                == CopilotAgentTaskEventType.BackgroundCommandCompleted);
    }

    [Fact]
    public async Task AnsweredQuestionTransfersQueuedOutputIntoSameAgentRun()
    {
        using var provider = new QuestionThenAnswerChatClient();
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry([]),
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(),
            _ => provider,
            EmptyExternalToolProvider.Instance,
            new CopilotCapabilityCatalog());
        var questionReady =
            new TaskCompletionSource<CopilotUserQuestionSnapshot>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        var request = CreateRequest(
            "conversation",
            "Ask one question, then finish after the answer.");
        var runTask = runtime.RunAsync(
            request,
            agentEvent =>
            {
                if (agentEvent.Type
                        == CopilotAgentEventType.UserQuestionRequested
                    && agentEvent.UserQuestion != null)
                {
                    questionReady.TrySetResult(agentEvent.UserQuestion);
                }
            },
            CancellationToken.None);
        var question = await questionReady.Task.WaitAsync(
            TimeSpan.FromSeconds(5));

        var steeringAdmission = runtime.EnqueueSteeringMessage(
            request.TaskId,
            "steering must wait for the answer");
        Assert.True(runtime.TryEnqueueBackgroundShellCommandOutput(
            CreateOutputEvent(
                "conversation",
                "arrived while waiting")));
        Assert.True(runtime.TryAnswerUserQuestion(
            request.TaskId,
            question.RequestId,
            "typed answer"));

        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        Assert.Equal(
            CopilotSteeringAdmissionReason.PendingUserQuestion,
            steeringAdmission.Reason);
        Assert.DoesNotContain(
            provider.StreamingCalls.SelectMany(call => call),
            message => message.Text.Contains(
                "steering must wait for the answer",
                StringComparison.Ordinal));
        var resumedCall = Assert.Single(
            provider.StreamingCalls,
            call => call.Any(message => message.Text.Contains(
                "<background_command_output_event>",
                StringComparison.Ordinal)));
        var functionResultIndex = resumedCall
            .Select((message, index) => new
            {
                Message = message,
                Index = index,
            })
            .First(item => item.Message.Contents
                .OfType<FunctionResultContent>()
                .Any())
            .Index;
        var outputEventIndex = resumedCall
            .Select((message, index) => new
            {
                Message = message,
                Index = index,
            })
            .First(item => item.Message.Text.Contains(
                "<background_command_output_event>",
                StringComparison.Ordinal))
            .Index;
        var functionResult = resumedCall
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Single();
        Assert.Contains(
            "typed answer",
            Convert.ToString(functionResult.Result) ?? string.Empty,
            StringComparison.Ordinal);
        Assert.True(outputEventIndex > functionResultIndex);
        Assert.Contains(
            "\"content\":\"arrived while waiting\"",
            resumedCall[outputEventIndex].Text,
            StringComparison.Ordinal);
        Assert.Single(
            result.TaskEventJournal.Events,
            item => item.Type
                == CopilotAgentTaskEventType
                    .BackgroundCommandOutputObserved);
    }

    [Fact]
    public async Task CompletionDuringQuestionTransfersOutputBeforeTerminalIntoSameAgentRun()
    {
        using var provider = new QuestionThenAnswerChatClient();
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry([]),
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(),
            _ => provider,
            EmptyExternalToolProvider.Instance,
            new CopilotCapabilityCatalog());
        var questionReady =
            new TaskCompletionSource<CopilotUserQuestionSnapshot>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        var request = CreateRequest(
            "conversation",
            "Ask one question, then finish after the answer.");
        var runTask = runtime.RunAsync(
            request,
            agentEvent =>
            {
                if (agentEvent.Type
                        == CopilotAgentEventType.UserQuestionRequested
                    && agentEvent.UserQuestion != null)
                {
                    questionReady.TrySetResult(agentEvent.UserQuestion);
                }
            },
            CancellationToken.None);
        var question = await questionReady.Task.WaitAsync(
            TimeSpan.FromSeconds(5));

        Assert.True(runtime.TryEnqueueBackgroundShellCommandOutput(
            CreateOutputEvent(
                "conversation",
                "final output before completion")));
        Assert.True(runtime.TryEnqueueBackgroundShellCommandCompletion(
            CreateCompletedCommand("conversation")));
        Assert.True(runtime.TryAnswerUserQuestion(
            request.TaskId,
            question.RequestId,
            "typed answer"));

        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        var resumedCall = Assert.Single(
            provider.StreamingCalls,
            call => call.Any(message => message.Text.Contains(
                "<background_command_event>",
                StringComparison.Ordinal)));
        var functionResultIndex = resumedCall
            .Select((message, index) => new
            {
                Message = message,
                Index = index,
            })
            .First(item => item.Message.Contents
                .OfType<FunctionResultContent>()
                .Any())
            .Index;
        var injectedMessage = resumedCall
            .Select((message, index) => new
            {
                Message = message,
                Index = index,
            })
            .Single(item => item.Message.Text.Contains(
                "<background_command_event>",
                StringComparison.Ordinal));
        var outputEventIndex = injectedMessage.Message.Text.IndexOf(
            "<background_command_output_event>",
            StringComparison.Ordinal);
        var terminalEventIndex = injectedMessage.Message.Text.IndexOf(
            "<background_command_event>",
            StringComparison.Ordinal);
        Assert.True(injectedMessage.Index > functionResultIndex);
        Assert.True(outputEventIndex >= 0);
        Assert.True(terminalEventIndex > outputEventIndex);
        Assert.Contains(
            "\"content\":\"final output before completion\"",
            injectedMessage.Message.Text,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"background_id\":\"background:deferred\"",
            injectedMessage.Message.Text,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"state\":\"completed\"",
            injectedMessage.Message.Text,
            StringComparison.Ordinal);
        Assert.Single(
            result.TaskEventJournal.Events,
            item => item.Type
                == CopilotAgentTaskEventType
                    .BackgroundCommandOutputObserved);
        Assert.Single(
            result.TaskEventJournal.Events,
            item => item.Type
                == CopilotAgentTaskEventType
                    .BackgroundCommandCompleted);
    }

    [Fact]
    public async Task ApprovedToolReceivesDeferredOutputAndTerminalAfterApprovalResponse()
    {
        using var solutionManagerScope = new SolutionManagerTestScope();
        using var provider = new ApprovalThenAnswerChatClient();
        var tool = new ApprovalSignalTool();
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry([tool]),
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(),
            _ => provider,
            EmptyExternalToolProvider.Instance,
            new CopilotCapabilityCatalog());
        var approvalReady = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var agentEvents = new List<CopilotAgentEvent>();
        var request = CreateRequest(
            "conversation",
            "Run the approval signal tool, then finish.");
        var runTask = runtime.RunAsync(
            request,
            agentEvent =>
            {
                agentEvents.Add(agentEvent);
                var actionId = agentEvent.ToolResult?.Approval?.ActionId;
                if (!string.IsNullOrWhiteSpace(actionId))
                    approvalReady.TrySetResult(actionId);
            },
            CancellationToken.None);
        var readySignal = await Task.WhenAny(
            approvalReady.Task,
            runTask,
            Task.Delay(TimeSpan.FromSeconds(5)));
        var prematureResult = ReferenceEquals(readySignal, runTask)
            ? await runTask
            : null;
        Assert.True(
            ReferenceEquals(readySignal, approvalReady.Task),
            $"run={prematureResult?.StopReason}; provider_calls={provider.StreamingCalls.Count}; executions={tool.ExecutionCount}; events="
                + string.Join(
                    " | ",
                    agentEvents.Select(agentEvent =>
                        agentEvent.ToolResult?.Summary
                        ?? agentEvent.Text
                        ?? agentEvent.Type.ToString())));
        var approvalActionId = await approvalReady.Task;

        try
        {
            Assert.True(runtime.TryEnqueueBackgroundShellCommandOutput(
                CreateOutputEvent(
                    "conversation",
                    "output while approval is pending")));
            Assert.True(runtime.TryEnqueueBackgroundShellCommandCompletion(
                CreateCompletedCommand("conversation")));
            Assert.True(CopilotMcpConfirmationStore.Instance.Approve(
                approvalActionId,
                new CopilotConfirmationReviewContext(
                    request.ConversationId,
                    request.TaskId,
                    request.WorkspacePath),
                out var approvalMessage),
                approvalMessage);

            var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
            Assert.Equal(1, tool.ExecutionCount);
            var resumedCall = Assert.Single(
                provider.StreamingCalls,
                call => call.Any(message => message.Text.Contains(
                    "<background_command_event>",
                    StringComparison.Ordinal)));
            var functionResult = resumedCall
                .SelectMany(message => message.Contents)
                .OfType<FunctionResultContent>()
                .Single();
            Assert.Contains(
                "protected test action completed",
                Convert.ToString(functionResult.Result) ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
            var resumedText = string.Join(
                Environment.NewLine,
                resumedCall.Select(message => message.Text));
            var outputEventIndex = resumedText.IndexOf(
                "<background_command_output_event>",
                StringComparison.Ordinal);
            var terminalEventIndex = resumedText.IndexOf(
                "<background_command_event>",
                StringComparison.Ordinal);
            Assert.True(outputEventIndex >= 0);
            Assert.True(terminalEventIndex > outputEventIndex);
            Assert.Contains(
                "\"content\":\"output while approval is pending\"",
                resumedText,
                StringComparison.Ordinal);
            Assert.Single(
                result.TaskEventJournal.Events,
                item => item.Type
                    == CopilotAgentTaskEventType
                        .BackgroundCommandOutputObserved);
            Assert.Single(
                result.TaskEventJournal.Events,
                item => item.Type
                    == CopilotAgentTaskEventType
                        .BackgroundCommandCompleted);
            Assert.Single(
                result.TaskEventJournal.Events,
                item => item.Type
                    == CopilotAgentTaskEventType.ApprovalApproved);
            var journalEvents = result.TaskEventJournal.Events
                .Select((item, index) => new
                {
                    Item = item,
                    Index = index,
                })
                .ToArray();
            var approvalEventIndex = journalEvents.Single(item =>
                item.Item.Type
                    == CopilotAgentTaskEventType.ApprovalApproved).Index;
            var outputJournalIndex = journalEvents.Single(item =>
                item.Item.Type
                    == CopilotAgentTaskEventType
                        .BackgroundCommandOutputObserved).Index;
            var completionJournalIndex = journalEvents.Single(item =>
                item.Item.Type
                    == CopilotAgentTaskEventType
                        .BackgroundCommandCompleted).Index;
            Assert.True(outputJournalIndex > approvalEventIndex);
            Assert.True(completionJournalIndex > outputJournalIndex);
        }
        finally
        {
            CopilotMcpConfirmationStore.Instance.Cancel(
                approvalActionId,
                out _,
                "Approval delivery test cleanup.");
        }
    }

    private static string ExtractDeliveryId(string prompt)
    {
        var match = Regex.Match(
            prompt,
            "\"delivery_id\":\"(?<id>[^\"]+)\"",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success);
        return match.Groups["id"].Value;
    }

    private static CopilotAgentRequest CreateRequest(
        string conversationId,
        string userText,
        string workspacePath = "")
    {
        return new CopilotAgentRequest
        {
            ConversationId = conversationId,
            TaskId = CopilotAgentTaskEventIds.CreateRunId(),
            UserText = userText,
            WorkspacePath = workspacePath,
            Profile = new CopilotProfileConfig
            {
                VendorType = CopilotVendorType.Custom,
                ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "test-key",
                BaseUrl = "https://example.test/v1",
                Model = "test-model",
                MaxTokens = 4_096,
            },
            Mode = CopilotAgentMode.Code,
        };
    }

    private static CopilotBackgroundShellOutputMonitorEventArgs
        CreateOutputEvent(
            string conversationId,
            string content)
    {
        return new CopilotBackgroundShellOutputMonitorEventArgs(
            new CopilotBackgroundShellOutputMonitorSnapshot(
                "monitor:deferred",
                conversationId,
                "background:deferred",
                CopilotBackgroundShellOutputStream.StandardOutput,
                "readiness",
                DateTimeOffset.Parse("2026-07-31T00:00:00Z"),
                DateTimeOffset.Parse("2026-07-31T01:00:00Z"),
                CopilotBackgroundShellOutputMonitorState.Running,
                PublishedEvents: 1,
                SuppressedEvents: 0),
            content,
            suppressedEvents: 0);
    }

    private static CopilotBackgroundShellCommandSnapshot
        CreateCompletedCommand(string conversationId)
    {
        return new CopilotBackgroundShellCommandSnapshot(
            "background:deferred",
            conversationId,
            "task:deferred",
            CopilotShellKind.PowerShell,
            @"C:\workspace",
            "background command",
            new string('a', 64),
            DateTimeOffset.Parse("2026-07-31T00:00:00Z"),
            DateTimeOffset.Parse("2026-07-31T00:00:01Z"),
            42,
            true,
            CopilotBackgroundShellCommandState.Completed,
            0,
            "final output before completion",
            string.Empty)
        {
            ObservedStandardOutputCharacters =
                "final output before completion".Length,
        };
    }

    private sealed class ApprovalSignalTool :
        ICopilotAgentDrivenTool,
        ICopilotFrameworkApprovedTool
    {
        private int _executionCount;

        public string Name => "ApprovalSignalTool";

        public string Description =>
            "Executes one protected test action after approval.";

        public CopilotToolAccess Access => CopilotToolAccess.Write;

        public CopilotToolApprovalMode ApprovalMode =>
            CopilotToolApprovalMode.Always;

        public int ExecutionCount => Volatile.Read(ref _executionCount);

        public bool CanHandle(CopilotAgentRequest request) => true;

        public bool IsAvailable(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(
                "The protected test action requires Framework approval.");
        }

        public Task<CopilotToolResult> ExecuteApprovedAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _executionCount);
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = "The protected test action completed.",
            });
        }
    }

    private sealed class SolutionManagerTestScope : IDisposable
    {
        private readonly FieldInfo _instanceField;
        private readonly object? _previousInstance;

        public SolutionManagerTestScope()
        {
            _instanceField = typeof(SolutionManager).GetField(
                    "_instance",
                    BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(
                    "SolutionManager singleton field was not found.");
            _previousInstance = _instanceField.GetValue(null);
            _instanceField.SetValue(
                null,
                new SolutionManager(
                    restoreLastWorkspace: false,
                    tryCloseWorkspaceDocuments: null));
        }

        public void Dispose()
        {
            _instanceField.SetValue(null, _previousInstance);
        }
    }

    private sealed class ApprovalThenAnswerChatClient : IChatClient
    {
        private int _callCount;

        public List<IReadOnlyList<ChatMessage>> StreamingCalls { get; } =
            new();

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "The approval scenario must remain on the streaming path.");
        }

        public async IAsyncEnumerable<ChatResponseUpdate>
            GetStreamingResponseAsync(
                IEnumerable<ChatMessage> messages,
                ChatOptions? options = null,
                [EnumeratorCancellation]
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StreamingCalls.Add(messages.ToArray());
            if (Interlocked.Increment(ref _callCount) == 1)
            {
                var approvalToolName = options?.Tools?
                    .OfType<AIFunction>()
                    .Select(tool => tool.Name)
                    .First(name => name.Contains(
                        "approval_signal_tool",
                        StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException(
                        "The protected approval tool was not registered.");
                yield return new ChatResponseUpdate(
                    ChatRole.Assistant,
                    [
                        new FunctionCallContent(
                            "call-approval",
                            approvalToolName,
                            new Dictionary<string, object?>()),
                    ])
                {
                    FinishReason = ChatFinishReason.ToolCalls,
                };
                yield break;
            }

            yield return new ChatResponseUpdate(
                ChatRole.Assistant,
                "done")
            {
                FinishReason = ChatFinishReason.Stop,
            };
            await Task.CompletedTask;
        }

        public object? GetService(
            Type serviceType,
            object? serviceKey = null) =>
            serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }
    }

    private sealed class CapturingChatClient : IChatClient
    {
        public List<IReadOnlyList<ChatMessage>> StreamingCalls { get; } =
            new();

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "done"))
            {
                FinishReason = ChatFinishReason.Stop,
            });
        }

        public async IAsyncEnumerable<ChatResponseUpdate>
            GetStreamingResponseAsync(
                IEnumerable<ChatMessage> messages,
                ChatOptions? options = null,
                [EnumeratorCancellation]
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StreamingCalls.Add(messages.ToArray());
            yield return new ChatResponseUpdate(ChatRole.Assistant, "done")
            {
                FinishReason = ChatFinishReason.Stop,
            };
            await Task.CompletedTask;
        }

        public object? GetService(
            Type serviceType,
            object? serviceKey = null) =>
            serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }
    }

    private sealed class BlockingSteeringChatClient : IChatClient
    {
        private int _callCount;

        public TaskCompletionSource StreamStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseStream { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public List<IReadOnlyList<ChatMessage>> StreamingCalls { get; } =
            new();

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "The steering scenario must remain on the streaming path.");
        }

        public async IAsyncEnumerable<ChatResponseUpdate>
            GetStreamingResponseAsync(
                IEnumerable<ChatMessage> messages,
                ChatOptions? options = null,
                [EnumeratorCancellation]
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StreamingCalls.Add(messages.ToArray());
            if (Interlocked.Increment(ref _callCount) == 1)
            {
                StreamStarted.TrySetResult();
                await ReleaseStream.Task.WaitAsync(cancellationToken);
            }

            yield return new ChatResponseUpdate(ChatRole.Assistant, "done")
            {
                FinishReason = ChatFinishReason.Stop,
            };
        }

        public object? GetService(
            Type serviceType,
            object? serviceKey = null) =>
            serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
            ReleaseStream.TrySetResult();
        }
    }

    private sealed class FailBeforeFirstUpdateChatClient : IChatClient
    {
        public List<IReadOnlyList<ChatMessage>> StreamingCalls { get; } =
            new();

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "The failed initial stream must not enter finalization.");
        }

        public async IAsyncEnumerable<ChatResponseUpdate>
            GetStreamingResponseAsync(
                IEnumerable<ChatMessage> messages,
                ChatOptions? options = null,
                [EnumeratorCancellation]
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StreamingCalls.Add(messages.ToArray());
            await Task.CompletedTask;
            if (!cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException(
                    "Provider failed before its first update.");
            }
            yield break;
        }

        public object? GetService(
            Type serviceType,
            object? serviceKey = null) =>
            serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }
    }

    private sealed class QuestionThenAnswerChatClient : IChatClient
    {
        private int _callCount;

        public List<IReadOnlyList<ChatMessage>> StreamingCalls { get; } =
            new();

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "The question scenario must remain on the streaming path.");
        }

        public async IAsyncEnumerable<ChatResponseUpdate>
            GetStreamingResponseAsync(
                IEnumerable<ChatMessage> messages,
                ChatOptions? options = null,
                [EnumeratorCancellation]
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StreamingCalls.Add(messages.ToArray());
            if (Interlocked.Increment(ref _callCount) == 1)
            {
                var observedToolNames = options?.Tools?
                    .OfType<AIFunction>()
                    .Select(tool => tool.Name)
                    .ToArray()
                    ?? Array.Empty<string>();
                var questionToolName = observedToolNames
                    .First(name => name.Contains(
                        "Question",
                        StringComparison.OrdinalIgnoreCase));
                yield return new ChatResponseUpdate(
                    ChatRole.Assistant,
                    [
                        new FunctionCallContent(
                            "call-question",
                            questionToolName,
                            new Dictionary<string, object?>
                            {
                                ["header"] = "Target",
                                ["question"] =
                                    "Which target should be used?",
                                ["options"] =
                                    JsonSerializer.SerializeToElement(
                                    new[]
                                    {
                                        new
                                        {
                                            label =
                                                "Option A (Recommended)",
                                            description =
                                                "Use the first target.",
                                        },
                                        new
                                        {
                                            label = "Option B",
                                            description =
                                                "Use the second target.",
                                        },
                                    }),
                            }),
                    ])
                {
                    FinishReason = ChatFinishReason.ToolCalls,
                };
                yield break;
            }

            yield return new ChatResponseUpdate(
                ChatRole.Assistant,
                "done")
            {
                FinishReason = ChatFinishReason.Stop,
            };
            await Task.CompletedTask;
        }

        public object? GetService(
            Type serviceType,
            object? serviceKey = null) =>
            serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }
    }

    private sealed class EmptyExternalToolProvider :
        ICopilotExternalToolProvider
    {
        public static EmptyExternalToolProvider Instance { get; } = new();

        public Task<CopilotExternalToolLease> DiscoverAsync(
            CopilotAgentRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CopilotExternalToolLease());
    }
}
