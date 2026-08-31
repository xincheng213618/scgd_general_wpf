using ColorVision.Copilot;
using ColorVision.Solution;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotGoalQueueRecoveryTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    private const string Objective = "Finish the explicitly requested goal work.";
    private const string NewerDraft = "Keep my newer unsent request.";
    private const string QueuedContext = "Context captured with the explicit goal start.";
    private const string AutomaticPrompt = "Internal automatic continuation: continue the current goal.";

    [Theory]
    [InlineData("active", false)]
    [InlineData("active", true)]
    [InlineData("paused", false)]
    [InlineData("superseded", false)]
    [InlineData("achieved", false)]
    public async Task RestartRestoresExplicitGoalStartToDraftWithoutReactivatingGoal(string goalState, bool newerDraft)
    {
        await using var fixture = new Fixture(goalState, automaticRecovery: false, newerDraft);

        Assert.Contains(Objective, fixture.Conversation.DraftText, StringComparison.Ordinal);
        Assert.Equal(fixture.Conversation.DraftText, fixture.ViewModel.InputText);
        Assert.Contains(fixture.Conversation.Attachments, item => item.Value == QueuedContext);
        if (newerDraft)
        {
            Assert.StartsWith(NewerDraft, fixture.Conversation.DraftText, StringComparison.Ordinal);
            Assert.Contains(fixture.Conversation.Attachments, item => item.Value == "Newer draft context.");
        }
        Assert.Equal(fixture.ExpectedRestoredGoalId, fixture.Conversation.Goal!.Id);
        Assert.Equal(goalState == "achieved" ? CopilotConversationGoalState.Achieved : CopilotConversationGoalState.Paused,
            fixture.Conversation.Goal.State);
        Assert.Empty(fixture.ViewModel.QueuedFollowUps);
        Assert.Empty(fixture.Host.QueuedRuns);
        Assert.Empty(fixture.Store.LoadedState.QueuedFollowUpRecoveries);
        Assert.Empty(fixture.Runtime.Requests);

        await fixture.FlushAsync();
        var persisted = Assert.Single(fixture.DiskStore.Load().Conversations);
        Assert.Equal(fixture.Conversation.DraftText, persisted.DraftText);
        Assert.Contains(persisted.Attachments, item => item.Value == QueuedContext);
        Assert.Equal(fixture.Conversation.Goal.State, persisted.Goal!.State);
    }

    [Fact]
    public async Task RestartDiscardsAutomaticContinuationWithoutRestoringItsInternalPrompt()
    {
        await using var fixture = new Fixture("active", automaticRecovery: true, newerDraft: true);

        Assert.Equal(CopilotConversationGoalState.Paused, fixture.Conversation.Goal!.State);
        Assert.Equal(NewerDraft, fixture.Conversation.DraftText);
        Assert.Equal(NewerDraft, fixture.ViewModel.InputText);
        Assert.Equal("Newer draft context.", Assert.Single(fixture.Conversation.Attachments).Value);
        Assert.DoesNotContain(AutomaticPrompt, fixture.Conversation.DraftText, StringComparison.Ordinal);
        Assert.Empty(fixture.Store.LoadedState.QueuedFollowUpRecoveries);
        Assert.Empty(fixture.ViewModel.QueuedFollowUps);
        Assert.Empty(fixture.Host.QueuedRuns);
        Assert.Empty(fixture.Runtime.Requests);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task GoalBoundRequestOnlyRunsAfterItsMessagesAreSaved(bool saveSucceeds, bool automatic)
    {
        await using var fixture = new Fixture("active", automaticRecovery: null, newerDraft: true);
        fixture.Conversation.Goal = fixture.Conversation.Goal!.WithState(
            CopilotConversationGoalState.Active, DateTimeOffset.UtcNow, "Explicitly resumed for the fixture.");
        var goalId = fixture.Conversation.Goal.Id;
        var queued = fixture.ScheduleGoalRequest(automatic);
        await fixture.FlushAsync();
        var gate = fixture.Store.BlockMessageSave(queued.RunId);
        fixture.ReleaseBusyRun();
        await gate.Entered.WaitAsync(TestTimeout);
        Assert.Empty(fixture.Runtime.Requests);
        Assert.Equal(2, fixture.Conversation.Messages.Count);
        Assert.Empty(Assert.Single(fixture.DiskStore.Load().Conversations).Messages);

        gate.Release(saveSucceeds);
        if (saveSucceeds)
        {
            await fixture.Runtime.Entered.WaitAsync(TestTimeout);
            var request = Assert.Single(fixture.Runtime.Requests);
            Assert.Equal(fixture.Conversation.Id, request.ConversationId);
            Assert.Equal(automatic ? AutomaticPrompt : Objective, request.UserText);
            fixture.Runtime.Release();
            await fixture.FollowUpRun!.Completion.WaitAsync(TestTimeout);
            Assert.Equal(2, fixture.Conversation.Messages.Count);
        }
        else
        {
            var failure = await Record.ExceptionAsync(() => fixture.FollowUpRun!.Completion.WaitAsync(TestTimeout));
            Assert.NotNull(failure);
            Assert.Contains("Controlled goal message save failure", failure.ToString(), StringComparison.Ordinal);
            Assert.Empty(fixture.Runtime.Requests);
            Assert.Empty(fixture.Conversation.Messages);
        }

        Assert.Equal(goalId, fixture.Conversation.Goal!.Id);
        Assert.Equal(CopilotConversationGoalState.Paused, fixture.Conversation.Goal.State);
        Assert.Empty(fixture.Store.LoadedState.QueuedFollowUpRecoveries);
        Assert.Empty(fixture.ViewModel.QueuedFollowUps);
        Assert.Empty(fixture.Host.QueuedRuns);
        if (automatic)
        {
            Assert.Equal(NewerDraft, fixture.Conversation.DraftText);
        }
        else
        {
            Assert.StartsWith(NewerDraft, fixture.Conversation.DraftText, StringComparison.Ordinal);
            Assert.Contains(Objective, fixture.Conversation.DraftText, StringComparison.Ordinal);
            Assert.Contains(fixture.Conversation.Attachments, item => item.Value == QueuedContext);
        }
        Assert.Equal(fixture.Conversation.DraftText, fixture.ViewModel.InputText);
        Assert.DoesNotContain(AutomaticPrompt, fixture.Conversation.DraftText, StringComparison.Ordinal);
        fixture.Store.Disarm();
        await fixture.FlushAsync();
        var persisted = Assert.Single(fixture.DiskStore.Load().Conversations);
        Assert.Equal(CopilotConversationGoalState.Paused, persisted.Goal!.State);
        Assert.Equal(saveSucceeds ? 2 : 0, persisted.Messages.Count);
        Assert.Equal(fixture.Conversation.DraftText, persisted.DraftText);
        if (!automatic)
            Assert.Contains(persisted.Attachments, item => item.Value == QueuedContext);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private static readonly FieldInfo SolutionInstance = typeof(SolutionManager).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? _previousSolution = SolutionInstance.GetValue(null);
        private readonly object _isolatedSolution = RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));
        private readonly string _root = Directory.CreateTempSubdirectory("CopilotGoalQueueRecovery-").FullName;
        private readonly TaskCompletionSource _releaseBusy = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CopilotProfileConfig _profile;
        private readonly CopilotHostedAgentRun _busyRun;
        private readonly CopilotQueuedFollowUpCoordinator _queue;

        public Fixture(string goalState, bool? automaticRecovery, bool newerDraft)
        {
            SolutionInstance.SetValue(null, _isolatedSolution);
            _profile = new CopilotProfileConfig
            {
                Id = "goal-recovery-profile", Name = "Goal recovery fixture",
                VendorType = CopilotVendorType.Custom, ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "unused-goal-recovery-key", BaseUrl = "https://example.test/v1", Model = "goal-fixture-model",
            };
            var originalGoal = CopilotConversationGoal.Create(Objective, DateTimeOffset.UtcNow);
            var conversation = CopilotConversationRecord.CreateEmpty(_profile.Id, _profile.DisplayLabel);
            conversation.SetCustomTitle("Goal recovery fixture"); // Suppress unrelated title model calls.
            conversation.Goal = goalState switch
            {
                "paused" => originalGoal.WithState(CopilotConversationGoalState.Paused, DateTimeOffset.UtcNow, "User paused."),
                "superseded" => CopilotConversationGoal.Create("A replacement goal must not be overwritten.", DateTimeOffset.UtcNow),
                "achieved" => originalGoal.WithState(CopilotConversationGoalState.Achieved, DateTimeOffset.UtcNow, "Goal already completed."),
                _ => originalGoal,
            };
            ExpectedRestoredGoalId = conversation.Goal.Id;
            if (newerDraft)
            {
                conversation.DraftText = NewerDraft;
                conversation.Attachments.Add(CopilotAttachmentItem.CreateContext("Newer draft context."));
            }
            var state = new CopilotChatState
            {
                ActiveConversationId = conversation.Id, ActiveProfileId = _profile.Id,
                Conversations = [conversation],
            };
            if (automaticRecovery.HasValue)
            {
                var prompt = automaticRecovery.Value ? AutomaticPrompt : Objective;
                state.QueuedFollowUpRecoveries.Add(new CopilotQueuedFollowUpRecoveryRecord
                {
                    RunId = "saved-goal-start", ConversationId = conversation.Id,
                    GoalId = originalGoal.Id, AutomaticGoalContinuation = automaticRecovery.Value,
                    ProfileId = _profile.Id, ResumeAfterRestart = !automaticRecovery.Value,
                    QueuedAtUtc = DateTimeOffset.UtcNow,
                    ComposerState = CopilotComposerStash.Capture(prompt, prompt.Length, CopilotAgentMode.Auto,
                        [CopilotAttachmentItem.CreateContext(QueuedContext)]),
                });
            }
            DiskStore = new CopilotChatStateStore(_root);
            DiskStore.Save(state);
            Store = new GatedStore(DiskStore);
            // Hold dispatch while the real constructor restores durable records.
            _busyRun = Host.Start(conversation.Id, CopilotAgentMode.Auto, _ => _releaseBusy.Task);
            var config = new CopilotConfig
            {
                SchemaVersion = CopilotConfig.CurrentSchemaVersion,
                McpBearerToken = "unused-goal-recovery-token", Profiles = [_profile],
            };
            ViewModel = new CopilotChatViewModel(new CopilotChatService(), Store, config, Runtime, Host);
            Conversation = Assert.Single(ViewModel.Conversations);
            _queue = (CopilotQueuedFollowUpCoordinator)typeof(CopilotChatViewModel)
                .GetField("_followUpQueue", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(ViewModel)!;
        }

        public string ExpectedRestoredGoalId { get; }
        public CopilotChatStateStore DiskStore { get; }
        public GatedStore Store { get; }
        public CopilotChatViewModel ViewModel { get; }
        public CopilotConversationRecord Conversation { get; }
        public CopilotAgentTaskHost Host { get; } = new();
        public GatedRuntime Runtime { get; } = new();
        public CopilotHostedAgentRun? FollowUpRun { get; private set; }

        public CopilotQueuedFollowUp ScheduleGoalRequest(bool automatic)
        {
            var execute = typeof(CopilotChatViewModel)
                .GetMethod("ExecuteQueuedFollowUpAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .CreateDelegate<Func<CopilotHostedAgentRun, CopilotQueuedFollowUp, Task>>(ViewModel);
            var request = new CopilotQueuedFollowUpRequest(Conversation.Id, Conversation.Title, automatic ? AutomaticPrompt : Objective,
                CopilotAgentMode.Auto, _profile,
                new CopilotAgentHostContextSnapshot(string.Empty, string.Empty,
                    automatic ? [] : [CopilotAttachmentItem.CreateContext(QueuedContext)]),
                null, new CopilotTurnRuntimeConfigSnapshot(new CopilotAgentDefaultsConfig(), []), null,
                GoalId: Conversation.Goal!.Id, AutomaticGoalContinuation: automatic);
            Assert.True(_queue.TrySchedule(request, false, execute, out var queued, out _));
            FollowUpRun = Assert.Single(Host.QueuedRuns);
            return Assert.IsType<CopilotQueuedFollowUp>(queued);
        }

        public void ReleaseBusyRun() => _releaseBusy.TrySetResult();

        public Task FlushAsync() => ((Task)typeof(CopilotChatViewModel)
            .GetMethod("FlushStatePersistenceBarrierAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(ViewModel, null)!).WaitAsync(TestTimeout);

        public async ValueTask DisposeAsync()
        {
            var runs = Host.ScheduledRuns.Concat([_busyRun]).Distinct().ToArray();
            Store.Disarm();
            Host.Shutdown();
            _releaseBusy.TrySetResult();
            Runtime.Release();
            try
            {
                foreach (var run in runs)
                {
                    var failure = await Record.ExceptionAsync(() => run.Completion.WaitAsync(TestTimeout));
                    Assert.IsNotType<TimeoutException>(failure);
                }
                await FlushAsync();
            }
            finally
            {
                ViewModel.Dispose();
                if (ReferenceEquals(SolutionInstance.GetValue(null), _isolatedSolution))
                    SolutionInstance.SetValue(null, _previousSolution);
                var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_root));
                var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
                Assert.True(string.Equals(Path.GetDirectoryName(root), temp, StringComparison.OrdinalIgnoreCase)
                    && Path.GetFileName(root).StartsWith("CopilotGoalQueueRecovery-", StringComparison.Ordinal));
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class GatedStore(CopilotChatStateStore inner) : ICopilotChatStateStore
    {
        private SaveGate? _gate;
        public CopilotChatState LoadedState { get; private set; } = null!;
        public string AttachmentDirectoryPath => inner.AttachmentDirectoryPath;
        public CopilotChatState Load() => LoadedState = inner.Load();
        public void Save(CopilotChatState state) => inner.Save(state);
        public CopilotChatStateSnapshot CaptureSnapshot(CopilotChatState state) => inner.CaptureSnapshot(state);
        public string Serialize(CopilotChatStateSnapshot snapshot) => inner.Serialize(snapshot);
        public string Serialize(CopilotChatState state) => inner.Serialize(state);
        public int CleanupOrphanedAttachments(CopilotChatState state) => inner.CleanupOrphanedAttachments(state);

        public SaveGate BlockMessageSave(string messageId)
        {
            var gate = new SaveGate(messageId);
            Volatile.Write(ref _gate, gate);
            return gate;
        }

        public async Task SaveSerializedAsync(string serializedState, CancellationToken cancellationToken = default)
        {
            var gate = Volatile.Read(ref _gate);
            if (gate != null && JObject.Parse(serializedState)[nameof(CopilotChatState.Conversations)]?.Children()
                .SelectMany(conversation => (conversation[nameof(CopilotConversationRecord.Messages)] as JArray)?.ToArray() ?? Array.Empty<JToken>())
                .Any(message => message[nameof(CopilotChatMessage.Id)]?.Value<string>() == gate.MessageId) == true)
            {
                gate.SignalEntered();
                if (!await gate.Outcome.WaitAsync(cancellationToken).ConfigureAwait(false))
                    throw new IOException("Controlled goal message save failure.");
            }
            await inner.SaveSerializedAsync(serializedState, cancellationToken).ConfigureAwait(false);
        }

        public void Disarm() => Interlocked.Exchange(ref _gate, null)?.Release(true);
    }

    private sealed class SaveGate(string messageId)
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _outcome = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string MessageId { get; } = messageId;
        public Task Entered => _entered.Task;
        public Task<bool> Outcome => _outcome.Task;
        public void SignalEntered() => _entered.TrySetResult();
        public void Release(bool succeed) => _outcome.TrySetResult(succeed);
    }

    private sealed class GatedRuntime : ICopilotTurnRuntime
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ConcurrentQueue<CopilotTurnRequest> Requests { get; } = new();
        public Task Entered => _entered.Task;
        public void Release() => _release.TrySetResult();

        public async IAsyncEnumerable<CopilotTurnEvent> RunAsync(CopilotTurnRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Enqueue(request);
            _entered.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            var journal = new CopilotAgentTaskEventJournalBuilder(request.TaskEventJournalBaseline);
            journal.RecordRunStarted();
            journal.RecordStop(CopilotAgentStopReason.Paused);
            yield return new CopilotTurnStartedEvent(request.TaskId, request.Mode);
            yield return new CopilotTurnAgentEvent(CopilotAgentEvent.AnswerDelta("Fixture work paused without a model call."));
            yield return new CopilotTurnAgentEvent(CopilotAgentEvent.Completed());
            yield return CopilotTurnCompletedEvent.Completed(request.TaskId, CopilotTurnResult.FromAgent(
                request.Mode, CopilotTokenUsage.Empty, new CopilotAgentRunResult
                {
                    StopReason = CopilotAgentStopReason.Paused,
                    TaskEventJournal = journal.Snapshot(),
                }));
        }

        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message) => new(CopilotSteeringAdmissionReason.RuntimeUnavailable);
        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;
        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;
        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) => false;
        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(CopilotWorkspaceRollbackActionRequest request,
            Action<CopilotAgentEvent> onEvent, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
