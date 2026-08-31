using ColorVision.Copilot;
using Microsoft.Extensions.AI;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCancelledToolJournalTests
{
    [Theory]
    [InlineData(CopilotAgentControlIntent.Cancel)]
    [InlineData(CopilotAgentControlIntent.Pause)]
    [InlineData(CopilotAgentControlIntent.None)]
    public void ClosingRunWithoutToolResultPreservesUnknownOutcome(CopilotAgentControlIntent intent)
    {
        var journal = CreateStartedToolJournal();
        var openSnapshot = journal.Snapshot();

        var closed = CloseRun(openSnapshot, intent);

        Assert.True(closed.IsStructurallyValid());
        var terminal = Assert.Single(closed.Events, item => item.Type == CopilotAgentTaskEventType.ToolCompleted);
        Assert.Equal(CopilotAgentTaskEventIds.ForCall("running-call"), terminal.SubjectId);
        Assert.Equal(CopilotToolExecutionState.Interrupted.ToString(), terminal.State);
        Assert.Equal(CopilotToolFailureCode.OutcomeUnknown, terminal.FailureCode);
        Assert.Contains("external outcome is unknown", terminal.Summary, StringComparison.Ordinal);
        var stopped = Assert.Single(closed.Events, item => item.Type == CopilotAgentTaskEventType.RunStopped);
        Assert.Equal(StopReason(intent).ToString(), stopped.State);
        Assert.True(terminal.Sequence < stopped.Sequence);
        Assert.DoesNotContain(openSnapshot.Events, item => item.Type == CopilotAgentTaskEventType.ToolCompleted);

        var prompt = CopilotAgentTaskEventJournal.BuildAttemptedToolRecoveryPrompt(closed);
        Assert.Contains(CopilotToolFailureCode.OutcomeUnknown, prompt, StringComparison.Ordinal);
        Assert.Contains("do not retry a write or non-idempotent operation blindly", prompt, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CopilotAgentControlIntent.Cancel)]
    [InlineData(CopilotAgentControlIntent.Pause)]
    [InlineData(CopilotAgentControlIntent.None)]
    public void ClosedRunWithMissingToolResultRequiresReplan(CopilotAgentControlIntent intent)
    {
        var closed = CloseRun(CreateStartedToolJournal().Snapshot(), intent);
        var (profile, capabilities) = CreateCheckpointContext();
        var checkpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(CopilotAgentSessionCheckpoint.Create(
            profile, "{}", capabilities, taskEventJournal: closed));

        var compatibility = checkpoint.EvaluateFor(profile, capabilities);

        Assert.Equal(CopilotAgentCheckpointCompatibilityKind.UncertainToolOutcome, compatibility.Kind);
        Assert.True(compatibility.RequiresReplan);
        Assert.False(compatibility.CanResume);
    }

    [Theory]
    [InlineData(CopilotToolExecutionState.Completed)]
    [InlineData(CopilotToolExecutionState.Failed)]
    [InlineData(CopilotToolExecutionState.Cancelled)]
    public void CancellationPreservesAuthoritativeToolResult(CopilotToolExecutionState state)
    {
        var journal = CreateStartedToolJournal();
        var failureCode = state switch
        {
            CopilotToolExecutionState.Failed => "business_rejected",
            CopilotToolExecutionState.Cancelled => "tool_execution_cancelled",
            _ => string.Empty,
        };
        journal.Observe(CopilotAgentEvent.FromToolResult(
            new CopilotToolResult
            {
                ToolName = "FixtureOperation",
                Success = state == CopilotToolExecutionState.Completed,
                Summary = "Authoritative result received before stopping.",
                FailureKind = state switch
                {
                    CopilotToolExecutionState.Failed => CopilotToolFailureKind.Conflict,
                    CopilotToolExecutionState.Cancelled => CopilotToolFailureKind.Cancelled,
                    _ => CopilotToolFailureKind.None,
                },
                FailureCode = failureCode,
            },
            CreateExecution("running-call", state, completedAtUtc: DateTimeOffset.UtcNow)));
        var original = Assert.Single(journal.Snapshot().Events, item => item.Type == CopilotAgentTaskEventType.ToolCompleted);

        var closed = CloseRun(journal.Snapshot(), CopilotAgentControlIntent.Cancel);

        var terminal = Assert.Single(closed.Events, item => item.Type == CopilotAgentTaskEventType.ToolCompleted);
        Assert.Equal(original.Id, terminal.Id);
        Assert.Equal(original.Sequence, terminal.Sequence);
        Assert.Equal(original.OccurredAtUtc, terminal.OccurredAtUtc);
        Assert.Equal(original.Summary, terminal.Summary);
        Assert.Equal(state.ToString(), terminal.State);
        Assert.Equal(failureCode, terminal.FailureCode);
        Assert.DoesNotContain(closed.Events, item => item.FailureCode == CopilotToolFailureCode.OutcomeUnknown);
    }

    [Fact]
    public void CancellationBeforeApprovalDoesNotInventAnExecutedOperation()
    {
        var journal = CreateStartedToolJournal();
        journal.Observe(CopilotAgentEvent.FromToolResult(
            new CopilotToolResult
            {
                ToolName = "FixtureOperation",
                Success = true,
                Summary = "Approval required before dispatch.",
            },
            CreateExecution("running-call", CopilotToolExecutionState.AwaitingApproval,
                approvalActionId: "pending-action", completedAtUtc: DateTimeOffset.UtcNow)));

        var closed = CloseRun(journal.Snapshot(), CopilotAgentControlIntent.Cancel);

        Assert.DoesNotContain(closed.Events, item => item.Type == CopilotAgentTaskEventType.ToolCompleted);
        Assert.DoesNotContain(closed.Events, item => item.FailureCode == CopilotToolFailureCode.OutcomeUnknown);
        var requested = Assert.Single(closed.Events, item => item.Type == CopilotAgentTaskEventType.ApprovalRequested);
        var denied = Assert.Single(closed.Events, item => item.Type == CopilotAgentTaskEventType.ApprovalDenied);
        Assert.Equal(requested.SubjectId, denied.SubjectId);
        Assert.Equal("approval_cancelled", denied.FailureCode);
        Assert.Contains("protected operation did not execute",
            CopilotAgentTaskEventJournal.BuildAttemptedToolRecoveryPrompt(closed), StringComparison.Ordinal);
    }

    [Fact]
    public void CancellationBeforeBridgeDispatchRetainsNotStartedEvidence()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        journal.RecordProviderToolHistory(CopilotProviderToolHistoryDelta.Capture(
            requestMessages: null,
            responseMessages:
            [
                new ChatMessage(ChatRole.Assistant,
                [
                    new FunctionCallContent("unstarted-call", "FixtureOperation", new Dictionary<string, object?>()),
                ]),
            ]));

        var closed = CloseRun(journal.Snapshot(), CopilotAgentControlIntent.Cancel);

        Assert.DoesNotContain(closed.Events, item => item.Type == CopilotAgentTaskEventType.ToolStarted);
        var terminal = Assert.Single(closed.Events, item => item.Type == CopilotAgentTaskEventType.ToolCompleted);
        Assert.Equal(CopilotToolFailureCode.NotStarted, terminal.FailureCode);
        Assert.DoesNotContain(closed.Events, item => item.FailureCode == CopilotToolFailureCode.OutcomeUnknown);
    }

    [Fact]
    public void ConversationCancellationDiscardsSessionButRetainsUnknownJournalEvidence()
    {
        var (profile, capabilities) = CreateCheckpointContext();
        var checkpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(CopilotAgentSessionCheckpoint.Create(
            profile, "{}", capabilities, taskEventJournal: CreateStartedToolJournal().Snapshot()));
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        Assert.True(conversation.SetAgentSessionCheckpoint(checkpoint));

        Assert.True(conversation.CompleteOpenAgentRun(CopilotAgentStopReason.Cancelled, CopilotAgentControlIntent.Cancel));

        Assert.Null(conversation.AgentSessionCheckpoint);
        var closed = Assert.IsType<CopilotAgentTaskEventJournalSnapshot>(conversation.CurrentAgentTaskEventJournal);
        var terminal = Assert.Single(closed.Events, item => item.Type == CopilotAgentTaskEventType.ToolCompleted);
        Assert.Equal(CopilotToolFailureCode.OutcomeUnknown, terminal.FailureCode);
        Assert.Equal(CopilotToolExecutionState.Interrupted.ToString(), terminal.State);
        Assert.Contains(CopilotToolFailureCode.OutcomeUnknown,
            CopilotAgentTaskEventJournal.BuildAttemptedToolRecoveryPrompt(closed), StringComparison.Ordinal);
    }

    private static CopilotAgentTaskEventJournalBuilder CreateStartedToolJournal()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        journal.Observe(CopilotAgentEvent.ToolStarted(CreateExecution("running-call")));
        return journal;
    }

    private static CopilotAgentTaskEventJournalSnapshot CloseRun(
        CopilotAgentTaskEventJournalSnapshot journal, CopilotAgentControlIntent intent)
    {
        return Assert.IsType<CopilotAgentTaskEventJournalSnapshot>(
            CopilotAgentTaskEventJournal.CloseLatestOpenRun(journal, StopReason(intent), intent));
    }

    private static CopilotAgentStopReason StopReason(CopilotAgentControlIntent intent) => intent switch
    {
        CopilotAgentControlIntent.Cancel => CopilotAgentStopReason.Cancelled,
        CopilotAgentControlIntent.Pause => CopilotAgentStopReason.Paused,
        _ => CopilotAgentStopReason.Interrupted,
    };

    private static CopilotToolExecutionInfo CreateExecution(
        string callId,
        CopilotToolExecutionState state = CopilotToolExecutionState.Running,
        string approvalActionId = "",
        DateTimeOffset? completedAtUtc = null) => new()
    {
        CallId = callId,
        ToolName = "FixtureOperation",
        Access = CopilotToolAccess.Write,
        State = state,
        ApprovalActionId = approvalActionId,
        StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
        CompletedAtUtc = completedAtUtc,
    };

    private static (CopilotProfileConfig Profile, CopilotCapabilityCatalogSnapshot Capabilities) CreateCheckpointContext()
    {
        var profile = new CopilotProfileConfig
        {
            VendorType = CopilotVendorType.Custom,
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "unused-fixture-key",
            BaseUrl = "https://example.test/v1",
            Model = "fixture-model",
        };
        var catalog = new CopilotCapabilityCatalog();
        catalog.PublishSource(CopilotCapabilitySourceKind.BuiltIn, "journal-fixture", "Journal fixture", [new CatalogOnlyTool()]);
        return (profile, catalog.GetSnapshot());
    }

    private sealed class CatalogOnlyTool : ICopilotTool
    {
        public string Name => "FixtureOperation";
        public string Description => "Journal fixture that is never executed.";
        public CopilotToolAccess Access => CopilotToolAccess.Write;
        public bool CanHandle(CopilotAgentRequest request) => true;
        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The journal fixture must not execute a tool.");
    }
}
