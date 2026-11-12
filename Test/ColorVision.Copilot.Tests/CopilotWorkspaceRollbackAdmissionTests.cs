using ColorVision.Copilot.Mcp;
using ColorVision.Solution;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotWorkspaceRollbackAdmissionTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public void PendingRollbackPreparationRejectsDuplicateCommand()
    {
        using var fixture = new Fixture();
        fixture.Start();

        Assert.False(fixture.ViewModel.RequestWorkspaceRollbackCommand.CanExecute(fixture.Trace));
        fixture.ViewModel.RequestWorkspaceRollbackCommand.Execute(fixture.Trace);
        Assert.Equal(1, fixture.Runtime.RequestCount);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void LateRollbackFailurePreservesTheSelectedConversationFeedback(bool switchConversation, bool throws)
    {
        using var fixture = new Fixture();
        fixture.Start();
        if (switchConversation)
            Assert.True(fixture.ViewModel.TrySelectConversation(fixture.Other.Id));
        fixture.ViewModel.InputText = "/help";
        Assert.True(fixture.ViewModel.SendCommand.CanExecute(null));
        fixture.ViewModel.SendCommand.Execute(null);
        var expectedTitle = fixture.ViewModel.LocalCommandResultTitle;
        var expectedText = fixture.ViewModel.LocalCommandResultText;
        Assert.NotEmpty(expectedText);

        if (throws)
            fixture.CompleteException(new InvalidOperationException("Old rollback preparation failed."));
        else
            fixture.Complete(CopilotWorkspaceRollbackActionResult.Failed("Old rollback preparation failed."));

        Assert.Equal(expectedTitle, fixture.ViewModel.LocalCommandResultTitle);
        Assert.Equal(expectedText, fixture.ViewModel.LocalCommandResultText);
        Assert.Same(switchConversation ? fixture.Other : fixture.Conversation, fixture.ViewModel.SelectedConversation);
    }

    [Fact]
    public void CurrentRollbackFailureRemainsVisibleAndReleasesItsPreparationReservation()
    {
        using var fixture = new Fixture();
        fixture.Start();

        fixture.Complete(CopilotWorkspaceRollbackActionResult.Failed("The rollback preview is no longer valid."));

        Assert.Equal("The rollback preview is no longer valid.", fixture.ViewModel.LocalCommandResultText);
        Assert.True(fixture.ViewModel.RequestWorkspaceRollbackCommand.CanExecute(fixture.Trace));
        fixture.Start();
        Assert.Equal(2, fixture.Runtime.RequestCount);
    }

    [Theory]
    [InlineData("switch")]
    [InlineData("dispose")]
    [InlineData("new-run")]
    public void InvalidatedPreparationCancelsOnlyItsOwnLatePendingAction(string transition)
    {
        using var fixture = new Fixture();
        fixture.Start();
        Assert.True(fixture.Runtime.PreparationToken.CanBeCanceled);
        if (transition == "switch")
            Assert.True(fixture.ViewModel.TrySelectConversation(fixture.Other.Id));
        else if (transition == "dispose")
        {
            fixture.ViewModel.Dispose();
            Assert.True(fixture.Runtime.PreparationToken.IsCancellationRequested);
        }
        else
            fixture.StartNewRun();
        var unrelated = fixture.CreateAction();
        var lateAction = fixture.CreateAction();
        var originalFeedback = fixture.ViewModel.PendingActionFeedbackText;

        fixture.Complete(new CopilotWorkspaceRollbackActionResult(lateAction, string.Empty));

        Assert.Equal(ConfirmableActionStatus.Cancelled, lateAction.Status);
        Assert.Equal(ConfirmableActionStatus.Pending, unrelated.Status);
        Assert.Equal(originalFeedback, fixture.ViewModel.PendingActionFeedbackText);
        Assert.Equal(0, fixture.ExecutionCount);
    }

    [Fact]
    public void AwaitingApprovalEventDoesNotMarkTheOriginalChangeAsRolledBack()
    {
        using var fixture = new Fixture();
        fixture.Start();

        fixture.RaiseRollbackEvent(CopilotToolExecutionState.AwaitingApproval);

        Assert.False(fixture.Trace.WorkspaceChangeSetRolledBack);
        Assert.True(fixture.Trace.CanRequestWorkspaceRollback);
        Assert.Contains(fixture.Conversation.Messages.SelectMany(message => message.AgentTraceEntries),
            entry => entry.ToolName == "RollbackWorkspacePatchEnvelope" && entry.State == CopilotToolExecutionState.AwaitingApproval);
        fixture.Complete(CopilotWorkspaceRollbackActionResult.Failed("Fixture preview finished without approval."));
    }

    [Fact]
    public async Task ApprovedRollbackExecutionKeepsItsOriginalConversationEvidenceAfterSelectionChanges()
    {
        using var fixture = new Fixture();
        fixture.Start();
        var action = fixture.CreateAction(raiseCompletedEvent: true);
        Assert.True(CopilotMcpConfirmationStore.Instance.Approve(action.ActionId,
            new CopilotConfirmationReviewContext(fixture.Conversation.Id, string.Empty, string.Empty), out _));
        // The store decision is already recorded; the ViewModel must not open another review window.
        fixture.Complete(new CopilotWorkspaceRollbackActionResult(action, string.Empty));
        Assert.True(fixture.ViewModel.TrySelectConversation(fixture.Other.Id));

        var result = await CopilotMcpConfirmationStore.Instance.ExecuteApprovedAsync(action.ActionId, action.ToolName,
            action.ArgumentsDigest, action.RequestContext.ResolveExecutionScope(), CancellationToken.None)
            .WaitAsync(TestTimeout);

        Assert.True(result.Success);
        Assert.True(fixture.Trace.WorkspaceChangeSetRolledBack);
        Assert.Same(fixture.Other, fixture.ViewModel.SelectedConversation);
        Assert.Empty(fixture.Other.Messages);
        Assert.Equal(1, fixture.ExecutionCount);
    }

    [Theory]
    [InlineData(CopilotToolExecutionState.Interrupted)]
    [InlineData(CopilotToolExecutionState.TimedOut)]
    public async Task UnknownDirectRollbackImmediatelyWarnsWithoutReplacingConfirmedDiff(CopilotToolExecutionState terminalState)
    {
        using var fixture = new Fixture();
        var assistant = Assert.Single(fixture.Conversation.Messages);
        const string confirmedDiff = "--- a/confirmed.txt\n+++ b/confirmed.txt\n@@ -1 +1 @@\n-before\n+after\n";
        assistant.ApplyWorkspaceDiff(new(confirmedDiff, 1, false));
        Assert.False(assistant.HasWorkspaceDiffWarning);
        var recheckPath = Path.Combine(Path.GetTempPath(), "CopilotDirectRollbackFixture", "possibly-changed.txt");
        fixture.Start();
        ConfirmableAction? action = null;
        action = fixture.CreateAction(execute: () =>
        {
            Assert.Equal(ConfirmableActionStatus.Executing, action!.Status);
            fixture.Runtime.OnEvent!(CopilotAgentEvent.FromToolResult(new CopilotToolResult
            {
                ToolName = "RollbackWorkspacePatchEnvelope", Success = false,
                Summary = "The workspace rollback ended before its final outcome was known.",
                ErrorMessage = "Verify the affected files before retrying.",
                FailureKind = CopilotToolFailureKind.OutcomeUnknown,
                FailureCode = CopilotToolFailureCode.OutcomeUnknown,
                WorkspaceRecheckPaths = [recheckPath],
            }, new CopilotToolExecutionInfo
            {
                CallId = "rollback-fixture-call", ToolName = "RollbackWorkspacePatchEnvelope",
                Access = CopilotToolAccess.Write, State = terminalState,
                FailureKind = CopilotToolFailureKind.OutcomeUnknown,
                ApprovalActionId = action.ActionId,
                WorkspaceRecheckPaths = [recheckPath],
            }));
            return CopilotMcpToolCallResult.Fail(CopilotToolFailureCode.OutcomeUnknown,
                "Verify the affected files before retrying.", CopilotToolFailureKind.OutcomeUnknown);
        });
        Assert.True(CopilotMcpConfirmationStore.Instance.Approve(action.ActionId,
            new CopilotConfirmationReviewContext(fixture.Conversation.Id, string.Empty, string.Empty), out _));
        fixture.Complete(new CopilotWorkspaceRollbackActionResult(action, string.Empty));

        var result = await CopilotMcpConfirmationStore.Instance.ExecuteApprovedAsync(action.ActionId, action.ToolName,
            action.ArgumentsDigest, action.RequestContext.ResolveExecutionScope(), CancellationToken.None)
            .WaitAsync(TestTimeout);

        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.OutcomeUnknown, result.FailureKind);
        Assert.False(fixture.Trace.WorkspaceChangeSetRolledBack);
        Assert.Equal(1, fixture.ExecutionCount);
        Assert.True(assistant.HasWorkspaceDiffWarning);
        Assert.Contains(recheckPath, assistant.WorkspaceDiffWarning, StringComparison.Ordinal);
        Assert.Equal(confirmedDiff, assistant.WorkspaceDiff);
        Assert.Equal(1, assistant.WorkspaceDiffFileCount);
        Assert.False(assistant.IsWorkspaceDiffTruncated);
        Assert.Contains("先前确认", assistant.WorkspaceDiffHeader, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectRollbackWarningMergesStructuredPathsAndKeepsOneBoundedLegacyTail()
    {
        using var fixture = new Fixture();
        var assistant = Assert.Single(fixture.Conversation.Messages);
        var firstPath = Path.Combine(Path.GetTempPath(), "CopilotDirectRollbackFixture", "first.txt");
        var secondPath = Path.Combine(Path.GetTempPath(), "CopilotDirectRollbackFixture", "second.txt");
        var structuredWarning = CopilotTurnWorkspaceDiffAccumulator.BuildVerificationWarning([firstPath]);
        const string separator = "\n\n先前待核查记录：\n";
        const string legacyPrefix = "Legacy verification evidence: ";
        var retainedLegacyCharacters = CopilotTurnWorkspaceDiffAccumulator.MaxVerificationWarningCharacters
            - 1 - structuredWarning.Length - separator.Length;
        assistant.WorkspaceDiffWarning = legacyPrefix
            + new string('x', retainedLegacyCharacters - legacyPrefix.Length - 1) + "😀" + " More legacy evidence.";
        assistant.AgentTraceEntries.Add(new CopilotAgentTraceEntry
        {
            CallId = "unknown-direct-rollback-first", ToolName = "RollbackWorkspacePatchEnvelope",
            Access = CopilotToolAccess.Write, State = CopilotToolExecutionState.Interrupted,
            FailureKind = CopilotToolFailureKind.OutcomeUnknown, WorkspaceRecheckPaths = [firstPath],
        });

        Assert.True(assistant.RefreshWorkspaceRecheckWarning());
        Assert.Equal(CopilotTurnWorkspaceDiffAccumulator.MaxVerificationWarningCharacters - 1, assistant.WorkspaceDiffWarning.Length);
        Assert.DoesNotContain('\uD83D', assistant.WorkspaceDiffWarning);
        Assert.DoesNotContain('\uDE00', assistant.WorkspaceDiffWarning);
        Assert.Contains(legacyPrefix, assistant.WorkspaceDiffWarning, StringComparison.Ordinal);
        var firstWarning = assistant.WorkspaceDiffWarning;
        Assert.False(assistant.RefreshWorkspaceRecheckWarning());
        Assert.Equal(firstWarning, assistant.WorkspaceDiffWarning);

        assistant.AgentTraceEntries.Add(new CopilotAgentTraceEntry
        {
            CallId = "unknown-direct-rollback-second", ToolName = "RollbackWorkspacePatchEnvelope",
            Access = CopilotToolAccess.Write, State = CopilotToolExecutionState.TimedOut,
            FailureKind = CopilotToolFailureKind.OutcomeUnknown, WorkspaceRecheckPaths = [secondPath],
        });
        Assert.True(assistant.RefreshWorkspaceRecheckWarning());
        Assert.Contains(firstPath, assistant.WorkspaceDiffWarning, StringComparison.Ordinal);
        Assert.Contains(secondPath, assistant.WorkspaceDiffWarning, StringComparison.Ordinal);
        Assert.Contains(legacyPrefix, assistant.WorkspaceDiffWarning, StringComparison.Ordinal);
        Assert.Equal(1, assistant.WorkspaceDiffWarning.Split(separator, StringSplitOptions.None).Length - 1);
        Assert.InRange(assistant.WorkspaceDiffWarning.Length, 1, CopilotTurnWorkspaceDiffAccumulator.MaxVerificationWarningCharacters);
        Assert.False(assistant.RefreshWorkspaceRecheckWarning());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectRollbackCheckpointAwaitsDurableSaveAndPropagatesOriginalFailure(bool saveFails)
    {
        using var fixture = new Fixture();
        var releaseSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Store.SaveRelease = releaseSave;
        fixture.Start();
        var barrier = Assert.IsType<Func<CancellationToken, Task>>(fixture.Runtime.Request!.StatePersistenceBarrier);
        var save = barrier(CancellationToken.None);
        try
        {
            await fixture.Store.SaveEntered.Task.WaitAsync(TestTimeout);
            Assert.False(save.IsCompleted);
            if (saveFails)
            {
                var expectedFailure = new IOException("The fixture could not durably save the rollback checkpoint.");
                releaseSave.SetException(expectedFailure);
                var actualFailure = await Assert.ThrowsAsync<IOException>(() => save.WaitAsync(TestTimeout));
                Assert.Same(expectedFailure, actualFailure);
            }
            else
            {
                releaseSave.SetResult();
                await save.WaitAsync(TestTimeout);
            }
        }
        finally
        {
            releaseSave.TrySetResult();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LatePreparationEventsCannotWriteToDeletedOrDisposedSource(bool dispose)
    {
        using var fixture = new Fixture();
        fixture.Start();
        if (dispose)
            fixture.ViewModel.Dispose();
        else
            fixture.ViewModel.Conversations.Remove(fixture.Conversation);
        var traceCount = fixture.Conversation.Messages.SelectMany(message => message.AgentTraceEntries).Count();

        fixture.RaiseRollbackEvent(CopilotToolExecutionState.AwaitingApproval);
        fixture.Complete(CopilotWorkspaceRollbackActionResult.Failed("The old source no longer exists."));

        Assert.Equal(traceCount, fixture.Conversation.Messages.SelectMany(message => message.AgentTraceEntries).Count());
        Assert.False(fixture.Trace.WorkspaceChangeSetRolledBack);
    }

    private sealed class Fixture : IDisposable
    {
        private static readonly FieldInfo SolutionInstance = typeof(SolutionManager)
            .GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? _previousSolution = SolutionInstance.GetValue(null);
        private readonly object _isolatedSolution = RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));
        private readonly PausedContext _context = new();
        private readonly List<ConfirmableAction> _actions = [];
        private readonly TaskCompletionSource _releaseNewRun = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Fixture()
        {
            SolutionInstance.SetValue(null, _isolatedSolution);
            var profile = new CopilotProfileConfig
            {
                Id = "rollback-admission-profile", Name = "Rollback admission fixture",
                VendorType = CopilotVendorType.Custom, ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "unused-test-key", BaseUrl = "https://example.test/v1", Model = "fixture-model",
            };
            Conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
            Other = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
            var state = new CopilotChatState
            {
                ActiveConversationId = Conversation.Id, ActiveProfileId = profile.Id,
                Conversations = [Conversation, Other],
            };
            Store = new MemoryStore(state);
            ViewModel = new CopilotChatViewModel(new CopilotChatService(), Store,
                new CopilotConfig { Profiles = [profile], McpBearerToken = "unused-test-token" }, Runtime, Host);
            var changeSetId = "workspace-change-set:" + Guid.NewGuid().ToString("N");
            Trace = CopilotAgentTraceEntry.FromResult(new CopilotToolExecutionInfo
            {
                CallId = "original-patch", ToolName = "ApplyWorkspacePatchEnvelope",
                State = CopilotToolExecutionState.Completed,
            }, new CopilotToolResult
            {
                ToolName = "ApplyWorkspacePatchEnvelope", Success = true,
                Content = $"change_set_id: {changeSetId}\nexpires_at_utc: {DateTimeOffset.UtcNow.AddMinutes(5):O}",
            });
            var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "The workspace patch completed.");
            assistant.AgentTraceEntries.Add(Trace);
            Conversation.Messages.Add(assistant);
            Assert.True(Trace.CanRequestWorkspaceRollback);
        }

        public CopilotChatViewModel ViewModel { get; }
        public CopilotConversationRecord Conversation { get; }
        public CopilotConversationRecord Other { get; }
        public CopilotAgentTraceEntry Trace { get; }
        public DeferredRuntime Runtime { get; } = new();
        public CopilotAgentTaskHost Host { get; } = new();
        public MemoryStore Store { get; }
        public int ExecutionCount { get; private set; }

        public void Start()
        {
            Assert.True(ViewModel.RequestWorkspaceRollbackCommand.CanExecute(Trace));
            var previousCount = Runtime.RequestCount;
            _context.Run(() => ViewModel.RequestWorkspaceRollbackCommand.Execute(Trace));
            Assert.Equal(previousCount + 1, Runtime.RequestCount);
        }

        public void StartNewRun()
        {
            var previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try { Host.Start(Conversation.Id, CopilotAgentMode.Auto, _ => _releaseNewRun.Task); }
            finally { SynchronizationContext.SetSynchronizationContext(previousContext); }
        }

        public ConfirmableAction CreateAction(bool raiseCompletedEvent = false, Func<CopilotMcpToolCallResult>? execute = null)
        {
            var action = CopilotMcpConfirmationStore.Instance.Create("Rollback prepared by the fixture", "Exact rollback fixture.",
                "confirmation-required", "RollbackWorkspacePatchEnvelope", "{}", _ =>
                {
                    ExecutionCount++;
                    if (raiseCompletedEvent)
                        RaiseRollbackEvent(CopilotToolExecutionState.Completed);
                    return Task.FromResult(execute?.Invoke() ?? CopilotMcpToolCallResult.Ok("executed"));
                }, executeOnApproval: true, requestContext: new CopilotConfirmationRequestContext
                {
                    SourceKind = CopilotApprovalSourceKind.ColorVisionUi,
                    ConversationId = Conversation.Id,
                    WorkspacePath = string.Empty,
                });
            _actions.Add(action);
            return action;
        }

        public void RaiseRollbackEvent(CopilotToolExecutionState state) => Runtime.OnEvent!(CopilotAgentEvent.FromToolResult(
            new CopilotToolResult
            {
                ToolName = "RollbackWorkspacePatchEnvelope", Success = true,
                Content = $"change_set_id: {Trace.WorkspaceChangeSetId}",
            }, new CopilotToolExecutionInfo
            {
                CallId = "rollback-fixture-call", ToolName = "RollbackWorkspacePatchEnvelope", State = state,
            }));

        public void Complete(CopilotWorkspaceRollbackActionResult result)
        {
            Assert.True(Runtime.Result.TrySetResult(result));
            Assert.True(_context.Posted.WaitOne(TestTimeout), "The rollback continuation was not posted.");
            _context.RunPending();
        }

        public void CompleteException(Exception exception)
        {
            Assert.True(Runtime.Result.TrySetException(exception));
            Assert.True(_context.Posted.WaitOne(TestTimeout), "The rollback continuation was not posted.");
            _context.RunPending();
        }

        public void Dispose()
        {
            ViewModel.Dispose();
            if (Runtime.Result.TrySetResult(CopilotWorkspaceRollbackActionResult.Failed("Fixture cleanup."))
                && Runtime.RequestCount > 0)
            {
                Assert.True(_context.Posted.WaitOne(TestTimeout));
                _context.RunPending();
            }
            var activeRun = Host.ActiveRun;
            Host.Shutdown();
            _releaseNewRun.TrySetResult();
            if (activeRun != null)
            {
                var exception = Record.Exception(() => activeRun.Completion.WaitAsync(TestTimeout).GetAwaiter().GetResult());
                Assert.IsNotType<TimeoutException>(exception);
            }
            foreach (var action in _actions)
                CopilotMcpConfirmationStore.Instance.Cancel(action.ActionId, out _);
            _context.RunPending();
            _context.Dispose();
            if (ReferenceEquals(SolutionInstance.GetValue(null), _isolatedSolution))
                SolutionInstance.SetValue(null, _previousSolution);
        }
    }

    private sealed class PausedContext : SynchronizationContext, IDisposable
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _callbacks = new();
        public AutoResetEvent Posted { get; } = new(false);
        public override void Post(SendOrPostCallback callback, object? state)
        {
            _callbacks.Enqueue((callback, state));
            Posted.Set();
        }
        public void Run(Action action)
        {
            var previous = Current;
            SetSynchronizationContext(this);
            try { action(); }
            finally { SetSynchronizationContext(previous); }
        }
        public void RunPending()
        {
            var previous = Current;
            // Freeze only the requested admission boundary. Released work must not
            // make independent feedback timers inherit this short-lived context.
            SetSynchronizationContext(null);
            try
            {
                while (_callbacks.TryDequeue(out var callback))
                    callback.Callback(callback.State);
            }
            finally { SetSynchronizationContext(previous); }
        }
        public void Dispose() => Posted.Dispose();
    }

    private sealed class DeferredRuntime : ICopilotTurnRuntime
    {
        public int RequestCount { get; private set; }
        public CancellationToken PreparationToken { get; private set; }
        public Action<CopilotAgentEvent>? OnEvent { get; private set; }
        public CopilotWorkspaceRollbackActionRequest? Request { get; private set; }
        public TaskCompletionSource<CopilotWorkspaceRollbackActionResult> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(CopilotWorkspaceRollbackActionRequest request,
            Action<CopilotAgentEvent> onEvent, CancellationToken cancellationToken)
        {
            RequestCount++;
            Request = request;
            PreparationToken = cancellationToken;
            OnEvent = onEvent;
            return Result.Task;
        }
        public IAsyncEnumerable<CopilotTurnEvent> RunAsync(CopilotTurnRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message) => new(CopilotSteeringAdmissionReason.RuntimeUnavailable);
        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;
        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;
        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) => false;
    }

    private sealed class MemoryStore(CopilotChatState state) : ICopilotChatStateStore
    {
        public TaskCompletionSource? SaveRelease { get; set; }
        public TaskCompletionSource SaveEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string AttachmentDirectoryPath => string.Empty;
        public CopilotChatState Load() => state;
        public void Save(CopilotChatState value) { }
        public CopilotChatStateSnapshot CaptureSnapshot(CopilotChatState value) => new(new JObject());
        public string Serialize(CopilotChatStateSnapshot value) => "{}";
        public string Serialize(CopilotChatState value) => "{}";
        public async Task SaveSerializedAsync(string serializedState, CancellationToken cancellationToken = default)
        {
            if (SaveRelease is { } release)
            {
                SaveEntered.TrySetResult();
                await release.Task.WaitAsync(cancellationToken);
            }
        }
        public int CleanupOrphanedAttachments(CopilotChatState value) => 0;
    }
}
