using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentTaskEventJournalIntegrityTests
{
    [Fact]
    public void InterruptedStopClosesEveryDanglingToolBeforeRunStop()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        journal.Observe(CopilotAgentEvent.ToolStarted(CreateExecution("call-1")));
        journal.Observe(CopilotAgentEvent.ToolStarted(CreateExecution("call-2")));

        journal.RecordStop(CopilotAgentStopReason.Interrupted);

        var snapshot = journal.Snapshot();
        Assert.True(snapshot.IsStructurallyValid());
        var terminalEvents = snapshot.Events
            .Where(item => item.Type == CopilotAgentTaskEventType.ToolCompleted)
            .ToArray();
        Assert.Collection(
            terminalEvents,
            item => AssertSyntheticTerminal(item, "call-1", CopilotToolExecutionState.Interrupted, "tool_terminal_event_missing"),
            item => AssertSyntheticTerminal(item, "call-2", CopilotToolExecutionState.Interrupted, "tool_terminal_event_missing"));
        var stopped = Assert.Single(snapshot.Events, item => item.Type == CopilotAgentTaskEventType.RunStopped);
        Assert.All(terminalEvents, item => Assert.True(item.Sequence < stopped.Sequence));
    }

    [Fact]
    public void CancelledStopClosesDanglingToolAsCancelled()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        journal.Observe(CopilotAgentEvent.ToolStarted(CreateExecution("cancelled-call")));

        journal.RecordStop(CopilotAgentStopReason.Cancelled);

        var terminal = Assert.Single(
            journal.Snapshot().Events,
            item => item.Type == CopilotAgentTaskEventType.ToolCompleted);
        AssertSyntheticTerminal(
            terminal,
            "cancelled-call",
            CopilotToolExecutionState.Cancelled,
            "tool_execution_cancelled");
    }

    [Fact]
    public void AuthoritativeToolResultIsNotDuplicatedAtStop()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        journal.Observe(CopilotAgentEvent.ToolStarted(CreateExecution("completed-call")));
        journal.Observe(CopilotAgentEvent.FromToolResult(
            new CopilotToolResult
            {
                ToolName = "IntegrityTool",
                Success = true,
                Summary = "Tool completed.",
            },
            CreateExecution(
                "completed-call",
                CopilotToolExecutionState.Completed,
                completedAtUtc: DateTimeOffset.UtcNow)));

        journal.RecordStop(CopilotAgentStopReason.Completed);

        var terminal = Assert.Single(
            journal.Snapshot().Events,
            item => item.Type == CopilotAgentTaskEventType.ToolCompleted);
        Assert.Equal(CopilotToolExecutionState.Completed.ToString(), terminal.State);
        Assert.Equal(string.Empty, terminal.FailureCode);
        Assert.Equal("Tool completed.", terminal.Summary);
    }

    [Fact]
    public void ApprovalDenialIsTerminalWithoutSyntheticToolCompletion()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        journal.Observe(CopilotAgentEvent.ToolStarted(CreateExecution("denied-call")));
        journal.Observe(CopilotAgentEvent.FromToolResult(
            new CopilotToolResult
            {
                ToolName = "IntegrityTool",
                Success = false,
                Summary = "Approval denied.",
                FailureCode = "approval_rejected",
            },
            CreateExecution(
                "denied-call",
                CopilotToolExecutionState.Denied,
                approvalActionId: "approval-denied",
                completedAtUtc: DateTimeOffset.UtcNow)));

        journal.RecordStop(CopilotAgentStopReason.ApprovalDenied);

        var snapshot = journal.Snapshot();
        Assert.DoesNotContain(snapshot.Events, item => item.Type == CopilotAgentTaskEventType.ToolCompleted);
        var denied = Assert.Single(
            snapshot.Events,
            item => item.Type == CopilotAgentTaskEventType.ApprovalDenied);
        Assert.Contains(CopilotAgentTaskEventIds.ForCall("denied-call"), denied.RelatedIds);
        Assert.Equal("approval_rejected", denied.FailureCode);
    }

    [Fact]
    public void InterruptedRunRecoveryRepairsCheckpointJournalBeforeRunStop()
    {
        var profile = CreateProfile();
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        journal.Observe(CopilotAgentEvent.ToolStarted(CreateExecution("crashed-call")));
        var checkpoint = CopilotAgentSessionCheckpoint.Create(
            profile,
            "{}",
            CopilotCapabilityCatalog.Shared.GetSnapshot(),
            taskEventJournal: journal.Snapshot());
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.AgentSessionCheckpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(checkpoint);
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
        {
            RequestMode = CopilotAgentMode.Auto,
            IsExecutionInProgress = true,
        };
        conversation.Messages.Add(assistant);

        Assert.True(CopilotInterruptedAgentRunRecovery.Normalize(conversation, assistant));

        var recovered = Assert.IsType<CopilotAgentSessionCheckpoint>(conversation.AgentSessionCheckpoint);
        var terminal = Assert.Single(
            recovered.TaskEventJournal.Events,
            item => item.Type == CopilotAgentTaskEventType.ToolCompleted);
        AssertSyntheticTerminal(
            terminal,
            "crashed-call",
            CopilotToolExecutionState.Interrupted,
            "tool_terminal_event_missing");
        var stopped = Assert.Single(
            recovered.TaskEventJournal.Events,
            item => item.Type == CopilotAgentTaskEventType.RunStopped);
        Assert.True(terminal.Sequence < stopped.Sequence);
        Assert.Equal(CopilotAgentStopReason.Interrupted, assistant.AgentStopReason);
    }

    private static CopilotToolExecutionInfo CreateExecution(
        string callId,
        CopilotToolExecutionState state = CopilotToolExecutionState.Running,
        string approvalActionId = "",
        DateTimeOffset? completedAtUtc = null)
    {
        return new CopilotToolExecutionInfo
        {
            CallId = callId,
            Round = 1,
            RuntimeName = "IntegrityRuntime",
            ToolName = "IntegrityTool",
            State = state,
            ApprovalActionId = approvalActionId,
            StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
            CompletedAtUtc = completedAtUtc,
        };
    }

    private static void AssertSyntheticTerminal(
        CopilotAgentTaskEvent item,
        string callId,
        CopilotToolExecutionState expectedState,
        string expectedFailureCode)
    {
        Assert.Equal(CopilotAgentTaskEventIds.ForCall(callId), item.SubjectId);
        Assert.Equal("IntegrityTool", item.ToolName);
        Assert.Equal(expectedState.ToString(), item.State);
        Assert.Equal(expectedFailureCode, item.FailureCode);
        Assert.Contains("before a terminal result was recorded", item.Summary, StringComparison.Ordinal);
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
}
