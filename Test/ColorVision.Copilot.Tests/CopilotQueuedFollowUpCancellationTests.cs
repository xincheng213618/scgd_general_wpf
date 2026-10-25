using ColorVision.Solution;
using Newtonsoft.Json.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotQueuedFollowUpCancellationTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SendingAnExistingQueuedItemNowDispatchesItAfterCancellingTheActiveTurn(bool differentActiveConversation)
    {
        await using var fixture = new QueueFixture();
        var item = fixture.Schedule(goalBound: false);
        var releaseIndependent = NewSignal();
        CopilotHostedAgentRun? independent = null;
        var active = fixture.ActiveRun;
        try
        {
            if (differentActiveConversation)
            {
                var independentStarted = NewSignal();
                Assert.True(fixture.Host.TrySchedule(fixture.OtherConversation.Id, CopilotAgentMode.Auto,
                    _ =>
                    {
                        independentStarted.TrySetResult();
                        return releaseIndependent.Task;
                    }, out independent));
                Assert.True(fixture.Host.RequestCancel(active.Id));
                fixture.ReleaseActive.TrySetResult();
                await independentStarted.Task.WaitAsync(TestTimeout);
                await ObserveCompletionAsync(active);
                active = independent!;
            }

            Assert.Equal(1, fixture.Host.GetQueuePosition(item.RunId));
            Assert.True(fixture.ViewModel.SendQueuedFollowUpNowCommand.CanExecute(item));
            var handoff = new TaskCompletionSource<CopilotHostedAgentRun?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var promotionNotifications = 0;
            void ObserveHandoff(object? sender, CopilotAgentTaskHostChangedEventArgs args)
            {
                if (args.Kind == CopilotAgentTaskHostChangeKind.QueueChanged && args.Run.Id == item.RunId)
                    Interlocked.Increment(ref promotionNotifications);
                if (args.Kind == CopilotAgentTaskHostChangeKind.Completed && args.Run.Id == active.Id)
                    handoff.TrySetResult(fixture.Host.ActiveRun);
            }
            fixture.Host.Changed += ObserveHandoff;
            try
            {
                fixture.ViewModel.SendQueuedFollowUpNowCommand.Execute(item);

                Assert.True(active.CancellationToken.IsCancellationRequested);
                fixture.ReleaseActive.TrySetResult();
                releaseIndependent.TrySetResult();
                // The host selects its successor before publishing Completed, so
                // this checks the committed handoff without a scheduling delay.
                Assert.Same(fixture.FollowUpRun, await handoff.Task.WaitAsync(TestTimeout));
                await fixture.FollowUpStarted.Task.WaitAsync(TestTimeout);
                Assert.False(fixture.FollowUpRun!.CancellationToken.IsCancellationRequested);
                Assert.Equal(1, Volatile.Read(ref promotionNotifications));
                Assert.Equal(fixture.Conversation.Id, fixture.FollowUpRun.ConversationId);
            }
            finally
            {
                fixture.Host.Changed -= ObserveHandoff;
            }
        }
        finally
        {
            releaseIndependent.TrySetResult();
            if (independent != null)
                await ObserveCompletionAsync(independent);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task OldQueueItemCannotCancelOrEditTheRunAfterItStarts(bool edit, bool goalBound)
    {
        await using var fixture = new QueueFixture();
        var item = fixture.Schedule(goalBound);
        var recovery = Assert.Single(fixture.State.QueuedFollowUpRecoveries);
        var goal = fixture.Conversation.Goal;
        fixture.ReleaseActive.TrySetResult();
        await fixture.FollowUpStarted.Task.WaitAsync(TestTimeout);
        Assert.Empty(fixture.ViewModel.QueuedFollowUps);
        Assert.True(fixture.FollowUpRun!.HasStarted);

        var changed = InvokeQueueAction(fixture.ViewModel, item, edit, out var pausedGoal);

        Assert.False(changed);
        Assert.False(pausedGoal);
        Assert.Equal(CopilotHostedRunState.Running, fixture.FollowUpRun.State);
        Assert.False(fixture.FollowUpRun.CancellationToken.IsCancellationRequested);
        Assert.Same(recovery, Assert.Single(fixture.State.QueuedFollowUpRecoveries));
        Assert.Same(goal, fixture.Conversation.Goal);
        Assert.Empty(fixture.ViewModel.InputText);
        Assert.Empty(fixture.Conversation.DraftText);
        Assert.Empty(fixture.Conversation.Attachments);
        Assert.Empty(fixture.Conversation.Messages);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StillQueuedItemCanBeDeletedOrRestoredForEditing(bool edit)
    {
        await using var fixture = new QueueFixture();
        var item = fixture.Schedule(goalBound: false);
        var unrelated = new CopilotQueuedFollowUpRecoveryRecord
        {
            RunId = "other-recovery",
            ConversationId = fixture.OtherConversation.Id,
            Prompt = "other conversation work",
        };
        fixture.State.QueuedFollowUpRecoveries.Add(unrelated);

        Assert.True(InvokeQueueAction(fixture.ViewModel, item, edit, out var pausedGoal));

        Assert.False(pausedGoal);
        Assert.False(fixture.FollowUpRun!.HasStarted);
        Assert.Empty(fixture.Host.QueuedRuns);
        Assert.Empty(fixture.ViewModel.QueuedFollowUps);
        Assert.Same(unrelated, Assert.Single(fixture.State.QueuedFollowUpRecoveries));
        Assert.False(fixture.ActiveRun.CancellationToken.IsCancellationRequested);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.FollowUpRun.Completion.WaitAsync(TestTimeout));
        Assert.Equal(edit ? item.Prompt : string.Empty, fixture.ViewModel.InputText);
        Assert.Equal(edit ? item.Prompt : string.Empty, fixture.Conversation.DraftText);
        if (edit)
            Assert.Equal("queued context", Assert.Single(fixture.Conversation.Attachments).Value);
        else
            Assert.Empty(fixture.Conversation.Attachments);
        Assert.Empty(fixture.Conversation.Messages);
        Assert.Equal("other draft", fixture.OtherConversation.DraftText);
    }

    [Fact]
    public async Task EditingQueuedFollowUpClosesHistorySearchAndRestoresTheComposer()
    {
        await using var fixture = new QueueFixture();
        var item = fixture.Schedule(goalBound: false, mode: CopilotAgentMode.Plan);
        await fixture.CancelActiveAndLeaveQueueAsync();
        var history = new CopilotChatMessage(CopilotChatRole.User, "earlier request");
        fixture.Conversation.Messages.Add(history);
        Assert.Empty(fixture.Conversation.DraftText);
        Assert.True(fixture.ViewModel.TryOpenPromptHistorySearch());
        Assert.Empty(fixture.ViewModel.InputText);
        Assert.True(fixture.ViewModel.EditQueuedFollowUpCommand.CanExecute(item));

        fixture.ViewModel.EditQueuedFollowUpCommand.Execute(item);

        Assert.Equal(item.Prompt, fixture.Conversation.DraftText);
        Assert.False(fixture.ViewModel.IsPromptHistorySearchOpen);
        Assert.Equal(item.Prompt, fixture.ViewModel.InputText);
        Assert.Equal(CopilotAgentMode.Plan, fixture.Conversation.DraftRequestMode);
        Assert.Equal("queued context", Assert.Single(fixture.Conversation.Attachments).Value);
        Assert.Empty(fixture.Host.QueuedRuns);
        Assert.Empty(fixture.ViewModel.QueuedFollowUps);
        Assert.Empty(fixture.State.QueuedFollowUpRecoveries);
        Assert.False(fixture.FollowUpRun!.HasStarted);
        Assert.False(fixture.FollowUpStarted.Task.IsCompleted);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.FollowUpRun.Completion.WaitAsync(TestTimeout));
        Assert.Same(history, Assert.Single(fixture.Conversation.Messages));
        Assert.Equal("other draft", fixture.OtherConversation.DraftText);
    }

    [Fact]
    public async Task EmptyHistorySearchQueryCannotAllowQueuedEditingToReplaceAnExistingDraft()
    {
        await using var fixture = new QueueFixture();
        var item = fixture.Schedule(goalBound: false, mode: CopilotAgentMode.Plan);
        var recovery = Assert.Single(fixture.State.QueuedFollowUpRecoveries);
        await fixture.CancelActiveAndLeaveQueueAsync();
        fixture.Conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "earlier request"));
        fixture.ViewModel.InputText = "newer draft";
        Assert.True(fixture.ViewModel.TryOpenPromptHistorySearch());
        Assert.Empty(fixture.ViewModel.InputText);
        Assert.False(fixture.ViewModel.EditQueuedFollowUpCommand.CanExecute(item));

        fixture.ViewModel.EditQueuedFollowUpCommand.Execute(item);

        Assert.Equal("newer draft", fixture.Conversation.DraftText);
        Assert.Equal(CopilotAgentMode.Auto, fixture.Conversation.DraftRequestMode);
        Assert.Empty(fixture.Conversation.Attachments);
        Assert.Same(fixture.FollowUpRun, Assert.Single(fixture.Host.QueuedRuns));
        Assert.Same(item, Assert.Single(fixture.ViewModel.QueuedFollowUps));
        Assert.Same(recovery, Assert.Single(fixture.State.QueuedFollowUpRecoveries));
        Assert.False(fixture.FollowUpRun!.HasStarted);
        Assert.False(fixture.FollowUpStarted.Task.IsCompleted);
        fixture.ViewModel.DismissPromptHistorySearch();
        Assert.Equal("newer draft", fixture.ViewModel.InputText);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GoalQueueCancellationDoesNotCancelAnAlreadyStartedRunBeforeItsUiNotification(bool automaticOnly)
    {
        var state = new CopilotChatState();
        var host = new CopilotAgentTaskHost();
        var queue = new CopilotQueuedFollowUpCoordinator(state, host);
        // Match the interval before the UI processes a Started notification.
        host.Changed += (_, args) =>
        {
            if (args.Kind != CopilotAgentTaskHostChangeKind.Started)
                queue.HandleTaskHostChanged(args);
        };
        var releaseActive = NewSignal();
        var started = NewSignal();
        var releaseFollowUp = NewSignal();
        var active = host.Start("conversation", CopilotAgentMode.Auto, _ => releaseActive.Task);
        CopilotHostedAgentRun? followUp = null;
        Assert.True(queue.TrySchedule(CreateRequest("conversation", "goal", true), false,
            (run, _) =>
            {
                followUp = run;
                started.TrySetResult();
                return releaseFollowUp.Task;
            }, out var item, out _));
        try
        {
            releaseActive.TrySetResult();
            await started.Task.WaitAsync(TestTimeout);
            Assert.Same(item, Assert.Single(queue.Items));
            Assert.Empty(host.QueuedRuns);

            var cancelled = automaticOnly
                ? queue.CancelAutomaticGoalContinuations("conversation")
                : queue.CancelGoalWork("conversation");

            Assert.Equal(0, cancelled);
            Assert.False(followUp!.CancellationToken.IsCancellationRequested);
            Assert.Equal(CopilotHostedRunState.Running, followUp.State);
            Assert.Equal(item!.RunId, Assert.Single(state.QueuedFollowUpRecoveries).RunId);
        }
        finally
        {
            releaseActive.TrySetResult();
            releaseFollowUp.TrySetResult();
            await ObserveCompletionAsync(active);
            if (followUp != null)
                await ObserveCompletionAsync(followUp);
        }
    }

    [Fact]
    public async Task ExplicitTaskStopStillCancelsTheActiveRun()
    {
        await using var fixture = new QueueFixture();
        fixture.Schedule(goalBound: false);
        fixture.ReleaseActive.TrySetResult();
        await fixture.FollowUpStarted.Task.WaitAsync(TestTimeout);

        var outcome = CopilotTaskDiagnostics.RequestStop(fixture.Host, fixture.FollowUpRun!.Id);

        Assert.Equal(CopilotTaskStopRequestOutcome.CancelRequested, outcome);
        Assert.Equal(CopilotHostedRunState.CancelRequested, fixture.FollowUpRun.State);
        Assert.True(fixture.FollowUpRun.CancellationToken.IsCancellationRequested);
    }

    private static bool InvokeQueueAction(CopilotChatViewModel viewModel, CopilotQueuedFollowUp item, bool edit, out bool pausedGoal)
    {
        var method = typeof(CopilotChatViewModel).GetMethod(
            edit ? "TryEditQueuedFollowUp" : "TryDeleteQueuedFollowUp", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object?[] arguments = edit ? [item] : [item, false];
        var result = Assert.IsType<bool>(method.Invoke(viewModel, arguments));
        pausedGoal = !edit && Assert.IsType<bool>(arguments[1]);
        return result;
    }

    private static CopilotQueuedFollowUpRequest CreateRequest(string conversationId, string goalId = "", bool? automatic = null) => new(
        conversationId,
        "Conversation",
        "queued request",
        CopilotAgentMode.Auto,
        CopilotProfileConfig.CreateDefault(),
        new CopilotAgentHostContextSnapshot("", "", [CopilotAttachmentItem.CreateContext("queued context")]),
        null,
        new CopilotTurnRuntimeConfigSnapshot(new CopilotAgentDefaultsConfig(), []),
        null,
        GoalId: goalId,
        AutomaticGoalContinuation: automatic);

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task ObserveCompletionAsync(CopilotHostedAgentRun run)
    {
        try
        {
            await run.Completion.WaitAsync(TestTimeout);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private sealed class QueueFixture : IAsyncDisposable
    {
        private static readonly FieldInfo SolutionInstanceField = typeof(SolutionManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? _previousSolutionInstance = SolutionInstanceField.GetValue(null);
        private readonly object _testSolutionInstance = RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));
        private readonly CopilotQueuedFollowUpCoordinator _queue;

        public CopilotChatState State { get; }
        public CopilotConversationRecord Conversation { get; }
        public CopilotConversationRecord OtherConversation { get; }
        public CopilotChatViewModel ViewModel { get; }
        public CopilotAgentTaskHost Host { get; } = new();
        public CopilotHostedAgentRun ActiveRun { get; }
        public CopilotHostedAgentRun? FollowUpRun { get; private set; }
        public TaskCompletionSource ReleaseActive { get; } = NewSignal();
        public TaskCompletionSource FollowUpStarted { get; } = NewSignal();
        private TaskCompletionSource ReleaseFollowUp { get; } = NewSignal();

        public QueueFixture()
        {
            SolutionInstanceField.SetValue(null, _testSolutionInstance);
            var profile = CopilotProfileConfig.CreateDefault();
            var config = new CopilotConfig { Profiles = [profile], McpBearerToken = "queued-cancellation-test-token" };
            Conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
            OtherConversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
            OtherConversation.DraftText = "other draft";
            State = new CopilotChatState
            {
                ActiveConversationId = Conversation.Id,
                ActiveProfileId = profile.Id,
                Conversations = [Conversation, OtherConversation],
            };
            ViewModel = new CopilotChatViewModel(new CopilotChatService(), new MemoryStateStore(State), config, new UnusedTurnRuntime(), Host);
            _queue = (CopilotQueuedFollowUpCoordinator)typeof(CopilotChatViewModel)
                .GetField("_followUpQueue", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(ViewModel)!;
            ActiveRun = Host.Start(Conversation.Id, CopilotAgentMode.Auto, _ => ReleaseActive.Task);
        }

        public CopilotQueuedFollowUp Schedule(bool goalBound, CopilotAgentMode mode = CopilotAgentMode.Auto)
        {
            if (goalBound)
                Conversation.Goal = CopilotConversationGoal.Create("continue the goal", DateTimeOffset.UtcNow);
            var request = CreateRequest(Conversation.Id, Conversation.Goal?.Id ?? "", goalBound ? true : null) with { Mode = mode };
            Assert.True(_queue.TrySchedule(request, false,
                (_, _) =>
                {
                    FollowUpStarted.TrySetResult();
                    return ReleaseFollowUp.Task;
                }, out var item, out _));
            FollowUpRun = Assert.Single(Host.QueuedRuns);
            return Assert.IsType<CopilotQueuedFollowUp>(item);
        }

        public async Task CancelActiveAndLeaveQueueAsync()
        {
            var completed = NewSignal();
            void OnChanged(object? sender, CopilotAgentTaskHostChangedEventArgs args)
            {
                if (args.Kind == CopilotAgentTaskHostChangeKind.Completed && args.Run.Id == ActiveRun.Id)
                    completed.TrySetResult();
            }
            Host.Changed += OnChanged;
            try
            {
                Assert.True(Host.RequestCancel(ActiveRun.Id));
                ReleaseActive.TrySetResult();
                await completed.Task.WaitAsync(TestTimeout);
                await ObserveCompletionAsync(ActiveRun);
            }
            finally
            {
                Host.Changed -= OnChanged;
            }
            Assert.False(ViewModel.IsBusy);
            Assert.Same(FollowUpRun, Assert.Single(Host.QueuedRuns));
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                ReleaseActive.TrySetResult();
                ReleaseFollowUp.TrySetResult();
                Host.Shutdown();
                await ObserveCompletionAsync(ActiveRun);
                if (FollowUpRun != null)
                    await ObserveCompletionAsync(FollowUpRun);
            }
            finally
            {
                ViewModel.Dispose();
                if (ReferenceEquals(SolutionInstanceField.GetValue(null), _testSolutionInstance))
                    SolutionInstanceField.SetValue(null, _previousSolutionInstance);
            }
        }
    }

    private sealed class MemoryStateStore(CopilotChatState state) : ICopilotChatStateStore
    {
        public string AttachmentDirectoryPath => string.Empty;
        public CopilotChatState Load() => state;
        public void Save(CopilotChatState value) { }
        public CopilotChatStateSnapshot CaptureSnapshot(CopilotChatState value) => new(new JObject());
        public string Serialize(CopilotChatStateSnapshot snapshot) => "{}";
        public string Serialize(CopilotChatState value) => "{}";
        public Task SaveSerializedAsync(string serializedState, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public int CleanupOrphanedAttachments(CopilotChatState value) => 0;
    }

    private sealed class UnusedTurnRuntime : ICopilotTurnRuntime
    {
        public IAsyncEnumerable<CopilotTurnEvent> RunAsync(CopilotTurnRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message) => new(CopilotSteeringAdmissionReason.RuntimeUnavailable);
        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;
        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;
        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) => false;
        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(CopilotWorkspaceRollbackActionRequest request, Action<CopilotAgentEvent> onEvent, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
