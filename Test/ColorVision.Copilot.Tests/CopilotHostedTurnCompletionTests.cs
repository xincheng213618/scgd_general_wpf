using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotHostedTurnCompletionTests
{
    [Theory]
    [InlineData(CopilotAgentControlIntent.Cancel)]
    [InlineData(CopilotAgentControlIntent.Pause)]
    [InlineData(CopilotAgentControlIntent.None)]
    public async Task CancellationDeadlineDoesNotInventATerminalResultForAStillRunningTool(CopilotAgentControlIntent intent)
    {
        using var cancellation = new CancellationTokenSource();
        var releaseProducer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var producerFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var conversation = CreateConversation(CreateOpenCheckpoint());
        var assistant = CreateAssistant(CopilotAgentMode.Auto);
        var execution = new CopilotToolExecutionInfo
        {
            CallId = "pending-operation",
            ToolName = "FixtureOperation",
            Access = CopilotToolAccess.Write,
            State = CopilotToolExecutionState.Running,
            StartedAtUtc = DateTimeOffset.UtcNow,
        };
        var terminalEvents = new List<CopilotTurnCompletedEvent>();
        var stream = CopilotTurnEventStream.RunAsync("cancellation-evidence", CopilotAgentMode.Auto,
            async (sink, producerToken) =>
            {
                try
                {
                    sink.OnAgentEvent(CopilotAgentEvent.ToolStarted(execution));
                    // No real tool or external side effect: this gate represents an
                    // operation whose authoritative result has not arrived at shutdown.
                    await releaseProducer.Task;
                    throw new OperationCanceledException(producerToken);
                }
                finally
                {
                    producerFinished.TrySetResult();
                }
            }, cancellation.Token, producerShutdownTimeout: TimeSpan.FromMilliseconds(50));
        try
        {
            var enumeration = ConsumeAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => enumeration.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(producerFinished.Task.IsCompleted);
            Assert.Equal(CopilotTurnStatus.Interrupted, Assert.Single(terminalEvents).Status);
            Assert.Equal(CopilotToolExecutionState.Running, Assert.Single(assistant.AgentTraceEntries).State);

            CopilotHostedTurnCompletion.CompleteCancellation(conversation, assistant, intent);

            var trace = Assert.Single(assistant.AgentTraceEntries);
            Assert.Equal(CopilotToolExecutionState.Interrupted, trace.State);
            Assert.Equal(CopilotToolFailureCode.OutcomeUnknown, trace.FailureCode);
            Assert.False(trace.RetryEligible);
            Assert.Contains("external outcome is unknown", trace.ErrorMessage, StringComparison.Ordinal);
            Assert.NotNull(trace.CompletedAtUtc);
            Assert.True(assistant.WasResponseInterrupted);
        }
        finally
        {
            releaseProducer.TrySetResult();
            await producerFinished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        async Task ConsumeAsync()
        {
            await foreach (var turnEvent in stream)
            {
                if (turnEvent is CopilotTurnAgentEvent agent)
                {
                    CopilotAssistantMessagePresenter.ApplyAgentEvent(assistant, agent.Event);
                    cancellation.Cancel();
                }
                else if (turnEvent is CopilotTurnCompletedEvent terminal)
                    terminalEvents.Add(terminal);
            }
        }
    }

    [Fact]
    public void CancellationDistinguishesUnstartedAndUnapprovedCalls()
    {
        var conversation = CreateConversation(CreateOpenCheckpoint());
        var assistant = CreateAssistant(CopilotAgentMode.Auto);
        assistant.AgentTraceEntries.Add(new CopilotAgentTraceEntry
        {
            CallId = "queued",
            ToolName = "QueuedOperation",
            State = CopilotToolExecutionState.Pending,
        });
        assistant.AgentTraceEntries.Add(new CopilotAgentTraceEntry
        {
            CallId = "unapproved",
            ToolName = "ApprovalOperation",
            State = CopilotToolExecutionState.AwaitingApproval,
        });

        CopilotHostedTurnCompletion.CompleteCancellation(conversation, assistant, CopilotAgentControlIntent.Cancel);

        Assert.Equal(CopilotToolFailureCode.NotStarted, assistant.AgentTraceEntries[0].FailureCode);
        Assert.Equal(CopilotToolFailureCode.ApprovalInterrupted, assistant.AgentTraceEntries[1].FailureCode);
        Assert.DoesNotContain(assistant.AgentTraceEntries, trace => trace.FailureCode == CopilotToolFailureCode.OutcomeUnknown);
    }

    [Theory]
    [InlineData(CopilotToolExecutionState.Completed)]
    [InlineData(CopilotToolExecutionState.Failed)]
    [InlineData(CopilotToolExecutionState.Cancelled)]
    public void CancellationPreservesAuthoritativeToolResults(CopilotToolExecutionState terminalState)
    {
        var conversation = CreateConversation(CreateOpenCheckpoint());
        var assistant = CreateAssistant(CopilotAgentMode.Auto);
        var trace = new CopilotAgentTraceEntry
        {
            CallId = "settled-operation",
            ToolName = "SettledOperation",
            State = terminalState,
            CompletedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            ErrorMessage = "Existing result detail",
        };
        assistant.AgentTraceEntries.Add(trace);
        var originalCompletedAt = trace.CompletedAtUtc;

        CopilotHostedTurnCompletion.CompleteCancellation(conversation, assistant, CopilotAgentControlIntent.Cancel);

        Assert.Equal(terminalState, trace.State);
        Assert.Equal(originalCompletedAt, trace.CompletedAtUtc);
        Assert.Equal(string.Empty, trace.FailureCode);
        Assert.Equal("Existing result detail", trace.ErrorMessage);
    }

    [Theory]
    [InlineData(CopilotAgentMode.Chat, true)]
    [InlineData(CopilotAgentMode.Auto, true)]
    [InlineData(CopilotAgentMode.Chat, false)]
    [InlineData(CopilotAgentMode.Auto, false)]
    public void InterruptedTurnRetainsOnlyItsReportedProviderUsage(CopilotAgentMode mode, bool failed)
    {
        var conversation = CreateConversation(CreateOpenCheckpoint());
        conversation.SetLastUsage(new CopilotTokenUsage(900, 90, 990, 400));
        var assistant = CreateAssistant(mode);
        assistant.Content = "Partial answer";
        var usage = new CopilotTokenUsage(120, 30, 150, 80);
        assistant.SetReportedUsage(usage);

        if (failed)
            CopilotHostedTurnCompletion.CompleteFailure(conversation, assistant, "provider failed");
        else
            CopilotHostedTurnCompletion.CompleteCancellation(conversation, assistant, CopilotAgentControlIntent.Cancel);

        Assert.Equal(usage, assistant.ReportedUsage);
        Assert.Equal(usage, conversation.LastUsage);
        Assert.True(assistant.WasResponseInterrupted);
        Assert.Contains("Partial answer", assistant.Content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CopilotAgentControlIntent.Pause)]
    [InlineData(CopilotAgentControlIntent.None)]
    public void NonCancelInterruptionAlsoRetainsReportedProviderUsage(CopilotAgentControlIntent intent)
    {
        var conversation = CreateConversation(CreateOpenCheckpoint());
        var assistant = CreateAssistant(CopilotAgentMode.Auto);
        var usage = new CopilotTokenUsage(120, 0, 120, 0);
        assistant.SetReportedUsage(usage);

        CopilotHostedTurnCompletion.CompleteCancellation(conversation, assistant, intent);

        Assert.Equal(usage, assistant.ReportedUsage);
        Assert.Equal(usage, conversation.LastUsage);
    }

    [Theory]
    [InlineData(CopilotAgentMode.Chat, true)]
    [InlineData(CopilotAgentMode.Auto, true)]
    [InlineData(CopilotAgentMode.Chat, false)]
    [InlineData(CopilotAgentMode.Auto, false)]
    public void InterruptedTurnWithoutReportedUsageDoesNotReusePriorUsageOrEstimatedBudget(CopilotAgentMode mode, bool failed)
    {
        var conversation = CreateConversation(CreateOpenCheckpoint());
        conversation.SetLastUsage(new CopilotTokenUsage(900, 90, 990, 400));
        var assistant = CreateAssistant(mode);
        assistant.AgentRunBudget = new CopilotAgentBudgetSnapshot
        {
            ConsumedTokens = 5_000,
            UsedEstimatedUsage = true,
        };

        if (failed)
            CopilotHostedTurnCompletion.CompleteFailure(conversation, assistant, "provider failed");
        else
            CopilotHostedTurnCompletion.CompleteCancellation(conversation, assistant, CopilotAgentControlIntent.Cancel);

        Assert.Equal(CopilotTokenUsage.Empty, assistant.ReportedUsage);
        Assert.Equal(CopilotTokenUsage.Empty, conversation.LastUsage);
    }

    [Fact]
    public void CancellingChatDoesNotDiscardRetainedAgentCheckpoint()
    {
        var checkpoint = CreateOpenCheckpoint();
        var conversation = CreateConversation(checkpoint);
        var assistant = CreateAssistant(CopilotAgentMode.Chat);

        CopilotHostedTurnCompletion.CompleteCancellation(
            conversation,
            assistant,
            CopilotAgentControlIntent.Cancel);

        Assert.Same(checkpoint, conversation.AgentSessionCheckpoint);
        Assert.Same(checkpoint.TaskEventJournal, conversation.CurrentAgentTaskEventJournal);
        Assert.Equal(CopilotAgentStopReason.None, assistant.AgentStopReason);
    }

    [Fact]
    public void CancellingAgentClosesJournalBeforeDiscardingCheckpoint()
    {
        var checkpoint = CreateOpenCheckpoint();
        var conversation = CreateConversation(checkpoint);
        var assistant = CreateAssistant(CopilotAgentMode.Auto);

        CopilotHostedTurnCompletion.CompleteCancellation(
            conversation,
            assistant,
            CopilotAgentControlIntent.Cancel);

        Assert.Null(conversation.AgentSessionCheckpoint);
        var journal = Assert.IsType<CopilotAgentTaskEventJournalSnapshot>(
            conversation.LatestAgentTaskEventJournal);
        Assert.Equal(CopilotAgentStopReason.Cancelled, assistant.AgentStopReason);
        AssertTerminalRun(
            journal,
            CopilotAgentTaskEventType.CancelRequested,
            CopilotAgentStopReason.Cancelled);
    }

    [Fact]
    public void RepeatedCancellationCompletionDoesNotDuplicateTerminalEvents()
    {
        var checkpoint = CreateOpenCheckpoint();
        var conversation = CreateConversation(checkpoint);

        Assert.True(conversation.CompleteOpenAgentRun(
            CopilotAgentStopReason.Cancelled,
            CopilotAgentControlIntent.Cancel));
        Assert.False(conversation.CompleteOpenAgentRun(
            CopilotAgentStopReason.Cancelled,
            CopilotAgentControlIntent.Cancel));

        var journal = Assert.IsType<CopilotAgentTaskEventJournalSnapshot>(
            conversation.LatestAgentTaskEventJournal);
        Assert.Single(
            journal.Events,
            item => item.Type == CopilotAgentTaskEventType.CancelRequested);
        Assert.Single(
            journal.Events,
            item => item.Type == CopilotAgentTaskEventType.RunStopped);
    }

    [Fact]
    public void LateCancellationDoesNotRewriteTerminalJournalAheadOfCheckpoint()
    {
        var builder = new CopilotAgentTaskEventJournalBuilder();
        builder.RecordRunStarted();
        var laggingCheckpoint = new CopilotAgentSessionCheckpoint
        {
            ProfileKey = "test-profile",
            SerializedSessionJson = "{}",
            TaskEventJournal = builder.Snapshot(),
        };
        builder.RecordStop(CopilotAgentStopReason.Completed);
        var terminalJournal = builder.Snapshot();
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        Assert.True(conversation.CommitAgentRunState(terminalJournal, laggingCheckpoint));

        Assert.True(conversation.CompleteOpenAgentRun(
            CopilotAgentStopReason.Cancelled,
            CopilotAgentControlIntent.Cancel));

        Assert.Null(conversation.AgentSessionCheckpoint);
        Assert.Same(terminalJournal, conversation.LatestAgentTaskEventJournal);
        Assert.DoesNotContain(
            terminalJournal.Events,
            item => item.Type == CopilotAgentTaskEventType.CancelRequested);
        var stopped = Assert.Single(
            terminalJournal.Events,
            item => item.Type == CopilotAgentTaskEventType.RunStopped);
        Assert.Equal(CopilotAgentStopReason.Completed.ToString(), stopped.State);
    }

    [Fact]
    public void AggregateRejectsMismatchedControlAndStopReason()
    {
        var conversation = CreateConversation(CreateOpenCheckpoint());

        Assert.Throws<ArgumentException>(() => conversation.CompleteOpenAgentRun(
            CopilotAgentStopReason.Cancelled,
            CopilotAgentControlIntent.Pause));
    }

    [Fact]
    public void PausingAgentClosesJournalInsideRetainedCheckpoint()
    {
        var checkpoint = CreateOpenCheckpoint();
        var conversation = CreateConversation(checkpoint);
        var assistant = CreateAssistant(CopilotAgentMode.Auto);

        CopilotHostedTurnCompletion.CompleteCancellation(
            conversation,
            assistant,
            CopilotAgentControlIntent.Pause);

        var retainedCheckpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(
            conversation.AgentSessionCheckpoint);
        Assert.NotSame(checkpoint, retainedCheckpoint);
        Assert.Null(conversation.LatestAgentTaskEventJournal);
        Assert.Same(
            retainedCheckpoint.TaskEventJournal,
            conversation.CurrentAgentTaskEventJournal);
        Assert.Equal(CopilotAgentStopReason.Paused, assistant.AgentStopReason);
        AssertTerminalRun(
            retainedCheckpoint.TaskEventJournal,
            CopilotAgentTaskEventType.PauseRequested,
            CopilotAgentStopReason.Paused);
    }

    [Fact]
    public void UnclassifiedAgentCancellationIsPersistedAsInterrupted()
    {
        var checkpoint = CreateOpenCheckpoint();
        var conversation = CreateConversation(checkpoint);
        var assistant = CreateAssistant(CopilotAgentMode.Auto);

        CopilotHostedTurnCompletion.CompleteCancellation(
            conversation,
            assistant,
            CopilotAgentControlIntent.None);

        var retainedCheckpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(
            conversation.AgentSessionCheckpoint);
        Assert.Equal(CopilotAgentStopReason.Interrupted, assistant.AgentStopReason);
        var stopped = Assert.Single(
            retainedCheckpoint.TaskEventJournal.Events,
            item => item.Type == CopilotAgentTaskEventType.RunStopped);
        Assert.Equal(
            CopilotAgentStopReason.Interrupted.ToString(),
            stopped.State);
    }

    [Fact]
    public void FailingAgentClosesJournalAsInterrupted()
    {
        var checkpoint = CreateOpenCheckpoint();
        var conversation = CreateConversation(checkpoint);
        var assistant = CreateAssistant(CopilotAgentMode.Auto);

        CopilotHostedTurnCompletion.CompleteFailure(
            conversation,
            assistant,
            "provider failed");

        var retainedCheckpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(
            conversation.AgentSessionCheckpoint);
        Assert.Equal(CopilotAgentStopReason.Interrupted, assistant.AgentStopReason);
        var stopped = Assert.Single(
            retainedCheckpoint.TaskEventJournal.Events,
            item => item.Type == CopilotAgentTaskEventType.RunStopped);
        Assert.Equal(
            CopilotAgentStopReason.Interrupted.ToString(),
            stopped.State);
    }

    [Fact]
    public void FailingTurnMarksStartedToolWithoutTerminalEvidenceAsOutcomeUnknown()
    {
        var conversation = CreateConversation(CreateOpenCheckpoint());
        var assistant = CreateAssistant(CopilotAgentMode.Auto);
        assistant.AgentTraceEntries.Add(new CopilotAgentTraceEntry
        {
            CallId = "started-write",
            ToolName = "WriteTool",
            State = CopilotToolExecutionState.Running,
            StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
            RetryEligible = true,
        });

        CopilotHostedTurnCompletion.CompleteFailure(
            conversation,
            assistant,
            "provider failed");

        var trace = Assert.Single(assistant.AgentTraceEntries);
        Assert.Equal(CopilotToolExecutionState.Interrupted, trace.State);
        Assert.Equal(CopilotToolFailureCode.OutcomeUnknown, trace.FailureCode);
        Assert.False(trace.RetryEligible);
        Assert.Contains("external outcome is unknown", trace.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void FailingTurnDistinguishesCallsThatDidNotStartOrAwaitedApproval()
    {
        var conversation = CreateConversation(CreateOpenCheckpoint());
        var assistant = CreateAssistant(CopilotAgentMode.Auto);
        assistant.AgentTraceEntries.Add(new CopilotAgentTraceEntry
        {
            CallId = "queued",
            ToolName = "QueuedTool",
            State = CopilotToolExecutionState.Pending,
        });
        assistant.AgentTraceEntries.Add(new CopilotAgentTraceEntry
        {
            CallId = "approval",
            ToolName = "ProtectedTool",
            State = CopilotToolExecutionState.AwaitingApproval,
            ApprovalActionId = "approval-action",
        });

        CopilotHostedTurnCompletion.CompleteFailure(
            conversation,
            assistant,
            "provider failed");

        Assert.Equal(CopilotToolFailureCode.NotStarted, assistant.AgentTraceEntries[0].FailureCode);
        Assert.Contains("tool was not started", assistant.AgentTraceEntries[0].ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(CopilotToolFailureCode.ApprovalInterrupted, assistant.AgentTraceEntries[1].FailureCode);
        Assert.Contains("protected operation was not started", assistant.AgentTraceEntries[1].ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(
            assistant.AgentTraceEntries,
            trace => trace.FailureCode == CopilotToolFailureCode.OutcomeUnknown);
    }

    [Fact]
    public void StartupRecoveryWarnsBeforeRetryingAWriteWithUnknownOutcome()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var assistant = CreateAssistant(CopilotAgentMode.Auto);
        assistant.IsExecutionInProgress = true;
        assistant.AgentTraceEntries.Add(new CopilotAgentTraceEntry
        {
            CallId = "started-write",
            ToolName = "WriteTool",
            State = CopilotToolExecutionState.Running,
            StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
        });
        conversation.Messages.Add(assistant);

        Assert.True(CopilotInterruptedResponseRecovery.Normalize(conversation, assistant));

        Assert.Contains("外部结果未知", assistant.Content, StringComparison.Ordinal);
        Assert.Contains("不要直接重试", assistant.Content, StringComparison.Ordinal);
        Assert.Equal(
            CopilotToolFailureCode.OutcomeUnknown,
            Assert.Single(assistant.AgentTraceEntries).FailureCode);
    }

    [Fact]
    public void FailingChatDoesNotRewriteRetainedAgentCheckpoint()
    {
        var checkpoint = CreateOpenCheckpoint();
        var conversation = CreateConversation(checkpoint);
        var assistant = CreateAssistant(CopilotAgentMode.Chat);

        CopilotHostedTurnCompletion.CompleteFailure(
            conversation,
            assistant,
            "provider failed");

        Assert.Same(checkpoint, conversation.AgentSessionCheckpoint);
        Assert.Same(checkpoint.TaskEventJournal, conversation.CurrentAgentTaskEventJournal);
        Assert.Equal(CopilotAgentStopReason.None, assistant.AgentStopReason);
    }

    private static CopilotConversationRecord CreateConversation(
        CopilotAgentSessionCheckpoint checkpoint)
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        Assert.True(conversation.SetAgentSessionCheckpoint(checkpoint));
        return conversation;
    }

    private static CopilotAgentSessionCheckpoint CreateOpenCheckpoint()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        var candidate = new CopilotAgentSessionCheckpoint
        {
            ProfileKey = "test-profile",
            SerializedSessionJson = "{}",
            TaskEventJournal = journal.Snapshot(),
        };
        Assert.True(CopilotAgentSessionCheckpoint.TryCreateSnapshot(candidate, out var checkpoint));
        return checkpoint;
    }

    private static CopilotChatMessage CreateAssistant(CopilotAgentMode mode)
    {
        return new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
        {
            RequestMode = mode,
        };
    }

    private static void AssertTerminalRun(
        CopilotAgentTaskEventJournalSnapshot journal,
        CopilotAgentTaskEventType expectedControlType,
        CopilotAgentStopReason expectedStopReason)
    {
        var control = Assert.Single(
            journal.Events,
            item => item.Type == expectedControlType);
        var stopped = Assert.Single(
            journal.Events,
            item => item.Type == CopilotAgentTaskEventType.RunStopped);
        Assert.Equal(control.RunId, stopped.RunId);
        Assert.True(control.Sequence < stopped.Sequence);
        Assert.Equal(expectedStopReason.ToString(), stopped.State);
    }
}
