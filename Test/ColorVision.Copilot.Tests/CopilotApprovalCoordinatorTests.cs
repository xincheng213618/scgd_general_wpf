using ColorVision.Copilot.Mcp;
using System.ComponentModel;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotApprovalCoordinatorTests
{
    private const string ConversationId = "approval-conversation";
    private const string OtherConversationId = "other-conversation";
    private const string TaskId = "approval-task";
    private const string WorkspacePath = @"C:\ColorVision\ApprovalWorkspace";

    [Fact]
    public void ScopeOnlyCarriesTaskForTheSelectedAgentConversation()
    {
        var matching = new CopilotApprovalScope(
            ConversationId,
            ConversationId,
            TaskId,
            WorkspacePath).ToReviewContext();
        var other = new CopilotApprovalScope(
            ConversationId,
            OtherConversationId,
            TaskId,
            WorkspacePath).ToReviewContext();

        Assert.Equal(TaskId, matching.TaskId);
        Assert.Equal(string.Empty, other.TaskId);
    }

    [Fact]
    public void StoreEligibilityUsesFinalConversationTaskAndWorkspaceValidation()
    {
        var store = CopilotMcpConfirmationStore.Instance;
        store.ClearForTests();
        var action = store.Create(
            "Protected action",
            "Validate the final approval scope.",
            "confirmation-required",
            "approval_test",
            "{}",
            _ => Task.FromResult(CopilotMcpToolCallResult.Ok("executed")),
            requestContext: CreateRequestContext());
        try
        {
            Assert.IsAssignableFrom<ICopilotApprovalStore>(store);

            Assert.True(store.ValidateForReview(
                action.ActionId,
                Scope().ToReviewContext()).CanReview);

            var wrongConversation = store.ValidateForReview(
                action.ActionId,
                new CopilotApprovalScope(
                    OtherConversationId,
                    OtherConversationId,
                    TaskId,
                    WorkspacePath).ToReviewContext());
            var wrongTask = store.ValidateForReview(
                action.ActionId,
                new CopilotApprovalScope(
                    ConversationId,
                    ConversationId,
                    "other-task",
                    WorkspacePath).ToReviewContext());
            var wrongWorkspace = store.ValidateForReview(
                action.ActionId,
                new CopilotApprovalScope(
                    ConversationId,
                    ConversationId,
                    TaskId,
                    @"C:\ColorVision\OtherWorkspace").ToReviewContext());

            Assert.Equal(CopilotApprovalEligibilityReason.ContextMismatch, wrongConversation.Reason);
            Assert.Equal(CopilotApprovalEligibilityReason.ContextMismatch, wrongTask.Reason);
            Assert.Equal(CopilotApprovalEligibilityReason.ContextMismatch, wrongWorkspace.Reason);
            Assert.Equal(ConfirmableActionStatus.Pending, action.Status);

            Assert.True(store.Approve(action.ActionId, Scope().ToReviewContext(), out _));
            var alreadyApproved = store.ValidateForReview(
                action.ActionId,
                Scope().ToReviewContext());
            Assert.Equal(CopilotApprovalEligibilityReason.ActionNotPending, alreadyApproved.Reason);
            Assert.Equal(
                CopilotApprovalEligibilityReason.ActionNotFound,
                store.ValidateForReview("missing-action", Scope().ToReviewContext()).Reason);
        }
        finally
        {
            store.Cancel(action.ActionId, out _);
            store.ClearForTests();
        }
    }

    [Fact]
    public void PendingProjectionKeepsConversationIsolationAndReportsGlobalCount()
    {
        var current = CreateAction(
            "current",
            CopilotApprovalSourceKind.InAppAgent,
            ConversationId);
        var other = CreateAction(
            "other",
            CopilotApprovalSourceKind.InAppAgent,
            OtherConversationId);
        var external = CreateAction(
            "external",
            CopilotApprovalSourceKind.ExternalMcp,
            string.Empty);
        var hidden = CreateAction(
            "hidden",
            CopilotApprovalSourceKind.InAppAgent,
            ConversationId,
            userReviewVisible: false);
        var store = new FakeApprovalStore(current, other, external, hidden);
        using var coordinator = new CopilotApprovalCoordinator(store, new CopilotChatState());

        var projection = coordinator.RefreshPendingActions(ConversationId);

        Assert.Equal(2, projection.VisibleCount);
        Assert.Equal(4, projection.TotalPendingCount);
        Assert.Equal([current, external], coordinator.PendingActions);
    }

    [Fact]
    public void ApprovalFactPublicationIsolatesFailingSubscribers()
    {
        var store = CopilotMcpConfirmationStore.Instance;
        store.ClearForTests();
        using var coordinator = new CopilotApprovalCoordinator(store, new CopilotChatState());
        var storeActionsObserved = 0;
        var storeTransitionsObserved = 0;
        var projectionInvalidationsObserved = 0;
        var coordinatorTransitionsObserved = 0;
        EventHandler throwingStoreActions = (_, _) => throw new InvalidOperationException("store actions subscriber failed");
        EventHandler observingStoreActions = (_, _) => storeActionsObserved++;
        EventHandler<ConfirmableActionChangedEventArgs> throwingStoreTransition = (_, _) => throw new InvalidOperationException("store transition subscriber failed");
        EventHandler<ConfirmableActionChangedEventArgs> observingStoreTransition = (_, _) => storeTransitionsObserved++;
        EventHandler throwingProjectionInvalidation = (_, _) => throw new InvalidOperationException("projection subscriber failed");
        EventHandler observingProjectionInvalidation = (_, _) => projectionInvalidationsObserved++;
        EventHandler<CopilotApprovalActionTransitionEventArgs> throwingCoordinatorTransition = (_, _) => throw new InvalidOperationException("coordinator transition subscriber failed");
        EventHandler<CopilotApprovalActionTransitionEventArgs> observingCoordinatorTransition = (_, _) => coordinatorTransitionsObserved++;
        store.ActionsChanged += throwingStoreActions;
        store.ActionsChanged += observingStoreActions;
        store.ActionStatusChanged += throwingStoreTransition;
        store.ActionStatusChanged += observingStoreTransition;
        coordinator.PendingActionsInvalidated += throwingProjectionInvalidation;
        coordinator.PendingActionsInvalidated += observingProjectionInvalidation;
        coordinator.ActionTransitioned += throwingCoordinatorTransition;
        coordinator.ActionTransitioned += observingCoordinatorTransition;

        try
        {
            var action = store.Create(
                "Protected action",
                "Publish one committed approval fact.",
                "confirmation-required",
                "approval_callback_test",
                "{}",
                _ => Task.FromResult(CopilotMcpToolCallResult.Ok("executed")),
                requestContext: CreateRequestContext());

            Assert.Equal(ConfirmableActionStatus.Pending, action.Status);
            Assert.Equal(1, storeActionsObserved);
            Assert.Equal(1, storeTransitionsObserved);
            Assert.Equal(1, projectionInvalidationsObserved);
            Assert.Equal(1, coordinatorTransitionsObserved);
        }
        finally
        {
            store.ActionsChanged -= throwingStoreActions;
            store.ActionsChanged -= observingStoreActions;
            store.ActionStatusChanged -= throwingStoreTransition;
            store.ActionStatusChanged -= observingStoreTransition;
            coordinator.PendingActionsInvalidated -= throwingProjectionInvalidation;
            coordinator.PendingActionsInvalidated -= observingProjectionInvalidation;
            coordinator.ActionTransitioned -= throwingCoordinatorTransition;
            coordinator.ActionTransitioned -= observingCoordinatorTransition;
            store.ClearForTests();
        }
    }

    [Fact]
    public void ApprovalPropertyPublicationCannotInterruptCommittedDecision()
    {
        var store = CopilotMcpConfirmationStore.Instance;
        store.ClearForTests();
        var action = store.Create(
            "Protected action",
            "Publish one committed approval decision.",
            "confirmation-required",
            "approval_property_callback_test",
            "{}",
            _ => Task.FromResult(CopilotMcpToolCallResult.Ok("executed")),
            requestContext: CreateRequestContext());
        var observedProperties = new HashSet<string>(StringComparer.Ordinal);
        PropertyChangedEventHandler throwingSubscriber = (_, _) => throw new InvalidOperationException("property subscriber failed");
        PropertyChangedEventHandler observingSubscriber = (_, e) => observedProperties.Add(e.PropertyName ?? string.Empty);
        action.PropertyChanged += throwingSubscriber;
        action.PropertyChanged += observingSubscriber;

        try
        {
            Assert.True(store.Approve(action.ActionId, Scope().ToReviewContext(), out _));
            Assert.Equal(ConfirmableActionStatus.Approved, action.Status);
            Assert.Contains(nameof(ConfirmableAction.Status), observedProperties);
            Assert.Contains(nameof(ConfirmableAction.IsPending), observedProperties);
        }
        finally
        {
            action.PropertyChanged -= throwingSubscriber;
            action.PropertyChanged -= observingSubscriber;
            store.Cancel(action.ActionId, out _);
            store.ClearForTests();
        }
    }

    [Fact]
    public async Task ApproveAndRejectReturnTypedOutcomes()
    {
        var resume = CreateAction(
            "resume",
            CopilotApprovalSourceKind.InAppAgent,
            ConversationId,
            resumesAgentOnApproval: true);
        var execute = CreateAction(
            "execute",
            CopilotApprovalSourceKind.ColorVisionUi,
            ConversationId,
            executeOnApproval: true);
        var reject = CreateAction(
            "reject",
            CopilotApprovalSourceKind.InAppAgent,
            ConversationId);
        var store = new FakeApprovalStore(resume, execute, reject);
        using var coordinator = new CopilotApprovalCoordinator(store, new CopilotChatState());

        var resumed = await coordinator.ApproveAsync(resume, Scope(), CancellationToken.None);
        var executed = await coordinator.ApproveAsync(execute, Scope(), CancellationToken.None);
        var rejected = coordinator.Reject(reject, Scope());

        Assert.Equal(CopilotApprovalDecisionOutcome.Approved, resumed.Outcome);
        Assert.False(resumed.ExecutedImmediately);
        Assert.Equal(CopilotApprovalDecisionOutcome.ApprovedAndExecuted, executed.Outcome);
        Assert.True(executed.ExecutedImmediately);
        Assert.Equal(CopilotApprovalDecisionOutcome.Rejected, rejected.Outcome);
        Assert.Equal(2, store.ApproveCount);
        Assert.Equal(1, store.ExecuteCount);
        Assert.Equal(1, store.RejectCount);
    }

    [Fact]
    public void StoreEventsPublishImmutableTransitionsAndStopAfterDispose()
    {
        var action = CreateAction(
            "immutable",
            CopilotApprovalSourceKind.InAppAgent,
            ConversationId,
            agentCallId: "call-immutable");
        var store = new FakeApprovalStore(action);
        var coordinator = new CopilotApprovalCoordinator(store, new CopilotChatState());
        CopilotApprovalActionTransition? captured = null;
        var invalidationCount = 0;
        coordinator.ActionTransitioned += (_, e) => captured = e.Transition;
        coordinator.PendingActionsInvalidated += (_, _) => invalidationCount++;

        store.RaiseStatusChanged(action);
        store.RaiseActionsChanged();
        action.Status = ConfirmableActionStatus.Executed;
        action.ExecutionSucceeded = true;
        action.ExecutionResultText = "later result";

        Assert.NotNull(captured);
        Assert.Equal(ConfirmableActionStatus.Pending, captured.Status);
        Assert.Null(captured.ExecutionSucceeded);
        Assert.Equal(string.Empty, captured.ExecutionResultText);
        Assert.Equal(1, invalidationCount);

        coordinator.Dispose();
        store.RaiseStatusChanged(action);
        store.RaiseActionsChanged();

        Assert.Equal(ConfirmableActionStatus.Pending, captured.Status);
        Assert.Equal(1, invalidationCount);
    }

    [Theory]
    [InlineData(ConfirmableActionStatus.Pending, CopilotToolExecutionState.AwaitingApproval)]
    [InlineData(ConfirmableActionStatus.Approved, CopilotToolExecutionState.AwaitingApproval)]
    [InlineData(ConfirmableActionStatus.Executing, CopilotToolExecutionState.Running)]
    [InlineData(ConfirmableActionStatus.Rejected, CopilotToolExecutionState.Denied)]
    [InlineData(ConfirmableActionStatus.Expired, CopilotToolExecutionState.TimedOut)]
    [InlineData(ConfirmableActionStatus.Cancelled, CopilotToolExecutionState.Cancelled)]
    [InlineData(ConfirmableActionStatus.Executed, CopilotToolExecutionState.Completed)]
    public void TransitionMapsApprovalStatusToTraceState(
        ConfirmableActionStatus approvalStatus,
        CopilotToolExecutionState expectedTraceState)
    {
        var (state, _, message, trace) = CreateTraceState(ConversationId, "call-map");
        using var coordinator = new CopilotApprovalCoordinator(
            new FakeApprovalStore(),
            state);
        var completedAt = new DateTimeOffset(2026, 8, 10, 1, 2, 3, TimeSpan.Zero);
        var transition = new CopilotApprovalActionTransition(
            "action-map",
            "call-map",
            CopilotApprovalSourceKind.InAppAgent,
            ConversationId,
            approvalStatus,
            ResumesAgentOnApproval: false,
            ExecutionSucceeded: true,
            ExecutionResultText: "result",
            CompletedAt: completedAt,
            ObservedAtUtc: completedAt);

        var result = coordinator.ApplyTransition(transition);

        Assert.True(result.StateChanged);
        Assert.Equal(1, result.UpdatedConversationCount);
        Assert.Equal(1, result.UpdatedMessageCount);
        Assert.Equal(1, result.UpdatedTraceCount);
        Assert.Equal(expectedTraceState, trace.State);
        Assert.Equal("action-map", trace.ApprovalActionId);
        if (approvalStatus == ConfirmableActionStatus.Executing)
            Assert.True(message.IsExecutionInProgress);
    }

    [Theory]
    [InlineData(CopilotToolExecutionState.Interrupted, ConfirmableActionStatus.Cancelled)]
    [InlineData(CopilotToolExecutionState.Interrupted, ConfirmableActionStatus.Executed)]
    [InlineData(CopilotToolExecutionState.TimedOut, ConfirmableActionStatus.Cancelled)]
    [InlineData(CopilotToolExecutionState.TimedOut, ConfirmableActionStatus.Executed)]
    public void LateApprovalNotificationPreservesClosedUnknownOutcomeEvidenceAcrossRestart(
        CopilotToolExecutionState terminalState,
        ConfirmableActionStatus approvalStatus)
    {
        var (state, conversation, message, _) = CreateTraceState(ConversationId, "rollback-boundary-call");
        const string actionId = "rollback-boundary-action";
        const string warning = "The rollback may still be completing. Inspect the affected path before retrying; do not repeat the write automatically.";
        var path = Path.Combine(Path.GetTempPath(), "rollback-boundary-candidate.txt");
        var completedAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        CopilotAssistantMessagePresenter.ApplyAgentEvent(message, CopilotAgentEvent.FromToolResult(
            new CopilotToolResult
            {
                ToolName = "RollbackWorkspacePatchEnvelope", Success = false,
                Summary = "The rollback crossed its execution boundary without a final result.",
                ErrorMessage = warning, FailureKind = CopilotToolFailureKind.OutcomeUnknown,
                FailureCode = CopilotToolFailureCode.OutcomeUnknown, WorkspaceRecheckPaths = [path],
            }, new CopilotToolExecutionInfo
            {
                CallId = "rollback-boundary-call", ApprovalActionId = actionId,
                ToolName = "RollbackWorkspacePatchEnvelope", Access = CopilotToolAccess.Write,
                Idempotency = CopilotToolIdempotency.NonIdempotent, State = terminalState,
                FailureKind = CopilotToolFailureKind.OutcomeUnknown,
                StartedAtUtc = completedAt.AddSeconds(-1), CompletedAtUtc = completedAt,
            }));
        message.IsExecutionInProgress = false;
        message.MarkThinkingCompleted();
        var trace = Assert.Single(message.AgentTraceEntries);
        using var coordinator = new CopilotApprovalCoordinator(new FakeApprovalStore(), state);

        var update = coordinator.ApplyTransition(new CopilotApprovalActionTransition(
            actionId, "rollback-boundary-call", CopilotApprovalSourceKind.ColorVisionUi, ConversationId,
            approvalStatus, ResumesAgentOnApproval: false, ExecutionSucceeded: false,
            ExecutionResultText: "A generic late approval notification.", CompletedAt: DateTimeOffset.UtcNow,
            ObservedAtUtc: DateTimeOffset.UtcNow));

        var root = Directory.CreateTempSubdirectory("CopilotApprovalTerminalEvidence-").FullName;
        try
        {
            var profile = CopilotProfileConfig.CreateDefault();
            conversation.ProfileId = profile.Id;
            state.ActiveProfileId = profile.Id;
            state.ActiveConversationId = conversation.Id;
            var store = new CopilotChatStateStore(root);
            store.Save(state);
            var restored = new CopilotChatStateStore(root).Load();
            restored.EnsureInitializedAfterRestore(new CopilotConfig { Profiles = [profile] });
            var restoredTrace = Assert.Single(Assert.Single(restored.Conversations).Messages).AgentTraceEntries.Single();

            Assert.False(update.StateChanged);
            foreach (var evidence in new[] { trace, restoredTrace })
            {
                Assert.Equal(terminalState, evidence.State);
                Assert.Equal(completedAt, evidence.CompletedAtUtc);
                Assert.Equal(CopilotToolFailureKind.OutcomeUnknown, evidence.FailureKind);
                Assert.Equal(CopilotToolFailureCode.OutcomeUnknown, evidence.FailureCode);
                Assert.Equal(new[] { path }, evidence.WorkspaceRecheckPaths);
                Assert.Equal(warning, evidence.ErrorMessage);
            }
        }
        finally
        {
            var resolvedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            Assert.True(string.Equals(Path.GetDirectoryName(resolvedRoot), temp, StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(resolvedRoot).StartsWith("CopilotApprovalTerminalEvidence-", StringComparison.Ordinal));
            Directory.Delete(resolvedRoot, recursive: true);
        }
    }

    [Fact]
    public void InAppTransitionOnlyUpdatesItsOwningConversation()
    {
        var first = CreateTraceState(ConversationId, "shared-call");
        var second = CreateTraceState(OtherConversationId, "shared-call");
        first.State.Conversations.Add(second.Conversation);
        using var coordinator = new CopilotApprovalCoordinator(
            new FakeApprovalStore(),
            first.State);

        var result = coordinator.ApplyTransition(new CopilotApprovalActionTransition(
            "scoped-action",
            "shared-call",
            CopilotApprovalSourceKind.InAppAgent,
            ConversationId,
            ConfirmableActionStatus.Rejected,
            ResumesAgentOnApproval: true,
            ExecutionSucceeded: false,
            ExecutionResultText: string.Empty,
            CompletedAt: DateTimeOffset.UtcNow,
            ObservedAtUtc: DateTimeOffset.UtcNow));

        Assert.Equal(1, result.UpdatedTraceCount);
        Assert.Equal(CopilotToolExecutionState.Denied, first.Trace.State);
        Assert.Equal(CopilotToolExecutionState.Pending, second.Trace.State);
    }

    [Fact]
    public void ExternalTransitionCanUpdateMatchingTracesAcrossConversations()
    {
        var first = CreateTraceState(ConversationId, "external-call");
        var second = CreateTraceState(OtherConversationId, "external-call");
        first.State.Conversations.Add(second.Conversation);
        using var coordinator = new CopilotApprovalCoordinator(
            new FakeApprovalStore(),
            first.State);

        var result = coordinator.ApplyTransition(new CopilotApprovalActionTransition(
            "external-action",
            "external-call",
            CopilotApprovalSourceKind.ExternalMcp,
            string.Empty,
            ConfirmableActionStatus.Expired,
            ResumesAgentOnApproval: false,
            ExecutionSucceeded: null,
            ExecutionResultText: string.Empty,
            CompletedAt: DateTimeOffset.UtcNow,
            ObservedAtUtc: DateTimeOffset.UtcNow));

        Assert.Equal(2, result.UpdatedConversationCount);
        Assert.Equal(2, result.UpdatedTraceCount);
        Assert.Equal(CopilotToolExecutionState.TimedOut, first.Trace.State);
        Assert.Equal(CopilotToolExecutionState.TimedOut, second.Trace.State);
    }

    [Theory]
    [InlineData("previous-action")]
    [InlineData("")]
    public void ReusedCallIdDoesNotRebindACompletedHistoricalTrace(string previousActionId)
    {
        var (state, conversation, previousMessage, previousTrace) = CreateTraceState(ConversationId, "reused-call");
        previousTrace.ApprovalActionId = previousActionId;
        previousTrace.State = CopilotToolExecutionState.Completed;
        previousTrace.StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2);
        previousTrace.CompletedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        previousTrace.ResultSummary = "The previous operation completed.";
        previousMessage.IsExecutionInProgress = false;
        var previousCompletion = previousTrace.CompletedAtUtc;
        var current = CreateTraceState(ConversationId, "reused-call");
        conversation.Messages.Add(current.Message);
        using var coordinator = new CopilotApprovalCoordinator(new FakeApprovalStore(), state);

        var result = coordinator.ApplyTransition(new CopilotApprovalActionTransition(
            "current-action", "reused-call", CopilotApprovalSourceKind.InAppAgent, ConversationId,
            ConfirmableActionStatus.Rejected, ResumesAgentOnApproval: true, ExecutionSucceeded: false,
            ExecutionResultText: string.Empty, CompletedAt: DateTimeOffset.UtcNow, ObservedAtUtc: DateTimeOffset.UtcNow));

        Assert.Equal(1, result.UpdatedMessageCount);
        Assert.Equal(1, result.UpdatedTraceCount);
        Assert.Equal(CopilotToolExecutionState.Completed, previousTrace.State);
        Assert.Equal(previousActionId, previousTrace.ApprovalActionId);
        Assert.Equal(previousCompletion, previousTrace.CompletedAtUtc);
        Assert.Equal("The previous operation completed.", previousTrace.ResultSummary);
        Assert.False(previousMessage.IsExecutionInProgress);
        Assert.Equal(CopilotToolExecutionState.Denied, current.Trace.State);
        Assert.Equal("current-action", current.Trace.ApprovalActionId);
    }

    [Fact]
    public void ExactApprovalIdentityWinsOverAnUnboundReusedCallId()
    {
        var (state, _, message, boundTrace) = CreateTraceState(ConversationId, "bound-call");
        boundTrace.ApprovalActionId = "bound-action";
        var unboundTrace = new CopilotAgentTraceEntry
        {
            CallId = "reused-call", ToolName = "approval_test", State = CopilotToolExecutionState.Running,
        };
        message.AgentTraceEntries.Insert(0, unboundTrace);
        using var coordinator = new CopilotApprovalCoordinator(new FakeApprovalStore(), state);

        var result = coordinator.ApplyTransition(new CopilotApprovalActionTransition(
            "bound-action", "reused-call", CopilotApprovalSourceKind.InAppAgent, ConversationId,
            ConfirmableActionStatus.Rejected, ResumesAgentOnApproval: true, ExecutionSucceeded: false,
            ExecutionResultText: string.Empty, CompletedAt: DateTimeOffset.UtcNow, ObservedAtUtc: DateTimeOffset.UtcNow));

        Assert.Equal(1, result.UpdatedTraceCount);
        Assert.Equal(CopilotToolExecutionState.Denied, boundTrace.State);
        Assert.Equal(CopilotToolExecutionState.Running, unboundTrace.State);
        Assert.Empty(unboundTrace.ApprovalActionId);
    }

    [Fact]
    public void ExecutedAgentResumeTransitionDoesNotPrematurelyCompleteTrace()
    {
        var (state, _, message, trace) = CreateTraceState(ConversationId, "resume-call");
        trace.State = CopilotToolExecutionState.Running;
        message.IsExecutionInProgress = true;
        using var coordinator = new CopilotApprovalCoordinator(
            new FakeApprovalStore(),
            state);

        coordinator.ApplyTransition(new CopilotApprovalActionTransition(
            "resume-action",
            "resume-call",
            CopilotApprovalSourceKind.InAppAgent,
            ConversationId,
            ConfirmableActionStatus.Executed,
            ResumesAgentOnApproval: true,
            ExecutionSucceeded: true,
            ExecutionResultText: "framework result",
            CompletedAt: DateTimeOffset.UtcNow,
            ObservedAtUtc: DateTimeOffset.UtcNow));

        Assert.Equal(CopilotToolExecutionState.Running, trace.State);
        Assert.True(message.IsExecutionInProgress);
        Assert.Equal("resume-action", trace.ApprovalActionId);
    }

    private static CopilotApprovalScope Scope() => new(
        ConversationId,
        ConversationId,
        TaskId,
        WorkspacePath);

    private static CopilotConfirmationRequestContext CreateRequestContext() => new()
    {
        SourceKind = CopilotApprovalSourceKind.InAppAgent,
        RequestSource = CopilotMcpToolDispatcher.InAppAgentCallerSource,
        ConversationId = ConversationId,
        TaskId = TaskId,
        WorkspacePath = WorkspacePath,
    };

    private static ConfirmableAction CreateAction(
        string actionId,
        CopilotApprovalSourceKind sourceKind,
        string conversationId,
        bool userReviewVisible = true,
        bool executeOnApproval = false,
        bool resumesAgentOnApproval = false,
        string agentCallId = "") => new()
        {
            ActionId = actionId,
            Title = actionId,
            ToolName = "approval_test",
            RiskLevel = "confirmation-required",
            ArgumentsDigest = new string('a', 64),
            IsUserReviewVisible = userReviewVisible,
            ExecuteOnApproval = executeOnApproval,
            ResumesAgentOnApproval = resumesAgentOnApproval,
            AgentCallId = agentCallId,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            RequestContext = new CopilotConfirmationRequestContext
            {
                SourceKind = sourceKind,
                ConversationId = conversationId,
                TaskId = sourceKind == CopilotApprovalSourceKind.InAppAgent ? TaskId : string.Empty,
                WorkspacePath = WorkspacePath,
            },
        };

    private static (
        CopilotChatState State,
        CopilotConversationRecord Conversation,
        CopilotChatMessage Message,
        CopilotAgentTraceEntry Trace) CreateTraceState(
            string conversationId,
            string callId)
    {
        var state = new CopilotChatState();
        var conversation = CopilotConversationRecord.CreateEmpty(string.Empty, string.Empty);
        conversation.Id = conversationId;
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
        {
            IsExecutionInProgress = true,
        };
        var trace = new CopilotAgentTraceEntry
        {
            CallId = callId,
            ToolName = "approval_test",
            StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
        };
        message.AgentTraceEntries.Add(trace);
        conversation.Messages.Add(message);
        state.Conversations.Add(conversation);
        return (state, conversation, message, trace);
    }

    private sealed class FakeApprovalStore : ICopilotApprovalStore
    {
        private readonly List<ConfirmableAction> _actions;

        public FakeApprovalStore(params ConfirmableAction[] actions)
        {
            _actions = actions.ToList();
        }

        public event EventHandler? ActionsChanged;

        public event EventHandler<ConfirmableActionChangedEventArgs>? ActionStatusChanged;

        public int ApproveCount { get; private set; }

        public int ExecuteCount { get; private set; }

        public int RejectCount { get; private set; }

        public int PendingCount => _actions.Count(action => action.Status == ConfirmableActionStatus.Pending);

        public IReadOnlyList<ConfirmableAction> GetPendingActionsForConversation(string? conversationId) =>
            _actions
                .Where(action => action.Status == ConfirmableActionStatus.Pending
                    && action.IsUserReviewVisible
                    && action.CanReviewFromConversation(conversationId))
                .OrderBy(action => action.ExpiresAt)
                .ToArray();

        public CopilotApprovalEligibility ValidateForReview(
            string actionId,
            CopilotConfirmationReviewContext reviewContext)
        {
            var action = Find(actionId);
            if (action == null)
            {
                return CopilotApprovalEligibility.Denied(
                    CopilotApprovalEligibilityReason.ActionNotFound,
                    "not found");
            }
            if (action.Status != ConfirmableActionStatus.Pending)
            {
                return CopilotApprovalEligibility.Denied(
                    CopilotApprovalEligibilityReason.ActionNotPending,
                    "not pending");
            }
            if (action.RequestContext.SourceKind == CopilotApprovalSourceKind.InAppAgent
                && (!string.Equals(
                        action.RequestContext.ConversationId,
                        reviewContext.ConversationId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        action.RequestContext.TaskId,
                        reviewContext.TaskId,
                        StringComparison.Ordinal)))
            {
                return CopilotApprovalEligibility.Denied(
                    CopilotApprovalEligibilityReason.ContextMismatch,
                    "scope mismatch");
            }
            if (!string.Equals(
                action.RequestContext.WorkspacePath,
                reviewContext.WorkspacePath,
                StringComparison.OrdinalIgnoreCase))
            {
                return CopilotApprovalEligibility.Denied(
                    CopilotApprovalEligibilityReason.ContextMismatch,
                    "workspace mismatch");
            }

            return CopilotApprovalEligibility.Allowed;
        }

        public bool Approve(
            string actionId,
            CopilotConfirmationReviewContext reviewContext,
            out string message)
        {
            var action = Find(actionId);
            if (action == null || !ValidateForReview(actionId, reviewContext).CanReview)
            {
                message = "approval failed";
                return false;
            }

            ApproveCount++;
            action.Status = ConfirmableActionStatus.Approved;
            message = "approved";
            return true;
        }

        public bool Reject(
            string actionId,
            CopilotConfirmationReviewContext reviewContext,
            out string message)
        {
            var action = Find(actionId);
            if (action == null || !ValidateForReview(actionId, reviewContext).CanReview)
            {
                message = "rejection failed";
                return false;
            }

            RejectCount++;
            action.Status = ConfirmableActionStatus.Rejected;
            message = "rejected";
            return true;
        }

        public Task<CopilotMcpToolCallResult> ApproveAndExecuteAsync(
            string actionId,
            CopilotConfirmationReviewContext reviewContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Approve(actionId, reviewContext, out var message))
                return Task.FromResult(CopilotMcpToolCallResult.Fail("approval_failed", message));

            ExecuteCount++;
            Find(actionId)!.Status = ConfirmableActionStatus.Executed;
            return Task.FromResult(CopilotMcpToolCallResult.Ok("executed"));
        }

        public void RaiseActionsChanged() => ActionsChanged?.Invoke(this, EventArgs.Empty);

        public void RaiseStatusChanged(ConfirmableAction action) =>
            ActionStatusChanged?.Invoke(this, new ConfirmableActionChangedEventArgs(action));

        private ConfirmableAction? Find(string actionId) => _actions.FirstOrDefault(action =>
            string.Equals(action.ActionId, actionId, StringComparison.OrdinalIgnoreCase));
    }
}
