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
    [InlineData(false)]
    [InlineData(true)]
    public async Task AutomaticContinuationImageFailureKeepsLaterFollowUpInDraftAcrossRestart(bool newerDraft)
    {
        await using var fixture = new Fixture("active", automaticRecovery: null, newerDraft: false);
        var conversation = fixture.Conversation;
        conversation.Goal = conversation.Goal!.WithState(
            CopilotConversationGoalState.Active, DateTimeOffset.UtcNow, "Explicitly resumed for the fixture.");
        fixture.ViewModel.SelectedProfile!.SupportsImageInput = true;
        Directory.CreateDirectory(fixture.Store.AttachmentDirectoryPath);
        var imagePath = Path.Combine(fixture.Store.AttachmentDirectoryPath, "parent.png");
        await File.WriteAllBytesAsync(imagePath, Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
        var parentImage = Assert.Single(await CopilotImageAttachmentAdmission.PersistAsync(
            [CopilotAttachmentItem.CreateImage(imagePath)], fixture.Store.AttachmentDirectoryPath, CancellationToken.None));
        var parentUser = new CopilotChatMessage(CopilotChatRole.User, Objective) { RequestMode = CopilotAgentMode.Auto };
        parentUser.Attachments.Add(parentImage);
        var parentAssistant = new CopilotChatMessage(CopilotChatRole.Assistant, "The parent work made verified progress.")
        {
            RequestMode = CopilotAgentMode.Auto, AgentStopReason = CopilotAgentStopReason.Completed,
        };
        parentAssistant.MarkThinkingStarted();
        conversation.Messages.Add(parentUser);
        conversation.Messages.Add(parentAssistant);
        CopilotHostedTurnCompletion.CompleteTerminalTurn(conversation, parentAssistant, CopilotTokenUsage.Empty);
        await fixture.FlushAsync();

        // The existing schedule binding replaces only automatic-item creation. Its inherited image
        // uses the same completed-turn capture as production, before the busy Host slot is released.
        var completedSnapshot = new CopilotAgentHostContextSnapshot(string.Empty, string.Empty, parentUser.Attachments);
        var automatic = fixture.ScheduleGoalRequest(automatic: true, completedSnapshot: completedSnapshot);
        var automaticRun = fixture.FollowUpRun!;
        Assert.Equal(parentImage.Id, Assert.Single(automatic.SubmissionContext.Attachments).Id);
        const string laterPrompt = "Continue only after the goal's captured image has been inspected.";
        var laterAttachment = CopilotAttachmentItem.CreateContext("Context captured with the later ordinary follow-up.");
        fixture.ViewModel.InputText = laterPrompt;
        conversation.Attachments.Add(laterAttachment);
        Assert.True(fixture.ViewModel.TryQueueCurrentRunFollowUp());
        var laterRun = Assert.Single(fixture.Host.QueuedRuns, run => run.Id != automaticRun.Id);
        var newerAttachment = CopilotAttachmentItem.CreateContext("Newer unsent context.");
        if (newerDraft)
        {
            fixture.ViewModel.InputText = NewerDraft;
            conversation.Attachments.Add(newerAttachment);
        }
        var automaticCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<CopilotAgentTaskHostChangedEventArgs> observeAutomatic = (_, args) =>
        {
            if (args.Run.Id == automaticRun.Id && args.Kind == CopilotAgentTaskHostChangeKind.Completed)
                automaticCompleted.TrySetResult();
        };
        fixture.Host.Changed += observeAutomatic;
        try
        {
            File.Delete(parentImage.Value);
            fixture.ReleaseBusyRun();
            await automaticRun.Completion.WaitAsync(TestTimeout);
            await automaticCompleted.Task.WaitAsync(TestTimeout);
            Assert.Null(fixture.Host.ActiveRun);
            Assert.Same(laterRun, Assert.Single(fixture.Host.QueuedRuns));
            Assert.Equal(CopilotHostedRunState.Queued, laterRun.State);
            Assert.False(laterRun.HasStarted);
            Assert.False(laterRun.Completion.IsCompleted);
            Assert.Empty(fixture.Runtime.Requests);
            Assert.Equal(CopilotConversationGoalState.Paused, conversation.Goal!.State);
            Assert.Equal(newerDraft ? NewerDraft : string.Empty, conversation.DraftText);
            Assert.DoesNotContain(AutomaticPrompt, conversation.DraftText, StringComparison.Ordinal);
            Assert.Equal(new[] { parentUser, parentAssistant }, conversation.Messages.ToArray());
            Assert.Equal(CopilotAgentStopReason.Completed, parentAssistant.AgentStopReason);
            Assert.False(parentAssistant.WasResponseInterrupted);
            Assert.False(parentAssistant.IsThinkingInProgress);
            Assert.Equal(laterRun.Id, Assert.Single(fixture.ViewModel.QueuedFollowUps).RunId);
            await fixture.FlushAsync();
            var saved = fixture.DiskStore.Load();
            var savedConversation = Assert.Single(saved.Conversations);
            Assert.Equal(CopilotAgentStopReason.Completed, savedConversation.Messages[1].AgentStopReason);
            Assert.False(savedConversation.Messages[1].WasResponseInterrupted);
            Assert.Equal(CopilotConversationGoalState.Paused, savedConversation.Goal!.State);
            var savedLater = Assert.Single(saved.QueuedFollowUpRecoveries);
            Assert.Equal(laterRun.Id, savedLater.RunId);
            Assert.Equal(laterPrompt, savedLater.ComposerState!.Text);
            Assert.Equal(laterAttachment.Id, Assert.Single(savedLater.ComposerState.Attachments).Id);

            await AssertRestartedDraftAsync(fixture, [parentUser, parentAssistant],
                newerDraft ? NewerDraft + Environment.NewLine + Environment.NewLine + laterPrompt : laterPrompt,
                newerDraft ? [newerAttachment, laterAttachment] : [laterAttachment]);
        }
        finally
        {
            fixture.Host.Changed -= observeAutomatic;
        }
    }

    [Theory]
    [InlineData(false, "automatic", false)]
    [InlineData(true, "automatic", false)]
    [InlineData(false, "explicit", false)]
    [InlineData(false, "automatic", true)]
    [InlineData(false, "explicit", true)]
    [InlineData(false, "ordinary", true)]
    public async Task QueuedRequestOnlyRunsAfterItsMessagesAreSaved(bool saveSucceeds, string requestKind, bool restart)
    {
        var automatic = requestKind == "automatic";
        var ordinary = requestKind == "ordinary";
        await using var fixture = new Fixture("active", automaticRecovery: null, newerDraft: !restart);
        var conversation = fixture.Conversation;
        if (!ordinary)
            conversation.Goal = conversation.Goal!.WithState(
                CopilotConversationGoalState.Active, DateTimeOffset.UtcNow, "Explicitly resumed for the fixture.");
        var goalId = conversation.Goal!.Id;
        if (restart)
        {
            var parentUser = new CopilotChatMessage(CopilotChatRole.User, "Complete the parent request.")
            {
                RequestMode = CopilotAgentMode.Auto,
            };
            var parentAssistant = new CopilotChatMessage(CopilotChatRole.Assistant, "The parent request is complete.")
            {
                RequestMode = CopilotAgentMode.Auto, AgentStopReason = CopilotAgentStopReason.Completed,
            };
            parentAssistant.MarkThinkingStarted();
            conversation.Messages.Add(parentUser);
            conversation.Messages.Add(parentAssistant);
            CopilotHostedTurnCompletion.CompleteTerminalTurn(conversation, parentAssistant, CopilotTokenUsage.Empty);
            await fixture.FlushAsync();
        }
        var originalMessages = conversation.Messages.ToArray();
        CopilotQueuedFollowUp queued;
        if (ordinary)
        {
            fixture.ViewModel.InputText = Objective;
            conversation.Attachments.Add(CopilotAttachmentItem.CreateContext(QueuedContext));
            Assert.True(fixture.ViewModel.TryQueueCurrentRunFollowUp());
            queued = Assert.Single(fixture.ViewModel.QueuedFollowUps);
        }
        else
        {
            queued = fixture.ScheduleGoalRequest(automatic);
        }
        var queuedRun = Assert.Single(fixture.Host.QueuedRuns);
        const string laterPrompt = "Continue only after the preceding queued request was saved.";
        var laterAttachment = CopilotAttachmentItem.CreateContext("Context captured with the later ordinary follow-up.");
        var newerAttachment = CopilotAttachmentItem.CreateContext("Newer draft context.");
        CopilotHostedAgentRun? laterRun = null;
        if (restart)
        {
            fixture.ViewModel.InputText = laterPrompt;
            conversation.Attachments.Add(laterAttachment);
            Assert.True(fixture.ViewModel.TryQueueCurrentRunFollowUp());
            laterRun = Assert.Single(fixture.Host.QueuedRuns, run => run.Id != queuedRun.Id);
            fixture.ViewModel.InputText = NewerDraft;
            conversation.Attachments.Add(newerAttachment);
        }
        await fixture.FlushAsync();
        var gate = fixture.Store.BlockMessageSave(queued.RunId);
        var queuedCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<CopilotAgentTaskHostChangedEventArgs> observeQueued = (_, args) =>
        {
            if (args.Run.Id == queuedRun.Id && args.Kind == CopilotAgentTaskHostChangeKind.Completed)
                queuedCompleted.TrySetResult();
        };
        fixture.Host.Changed += observeQueued;
        try
        {
            fixture.ReleaseBusyRun();
            await gate.Entered.WaitAsync(TestTimeout);
            Assert.Empty(fixture.Runtime.Requests);
            Assert.Equal(originalMessages.Length + 2, conversation.Messages.Count);
            Assert.Equal(originalMessages.Select(message => message.Id),
                Assert.Single(fixture.DiskStore.Load().Conversations).Messages.Select(message => message.Id));

            gate.Release(saveSucceeds);
            if (saveSucceeds)
            {
                await fixture.Runtime.Entered.WaitAsync(TestTimeout);
                var request = Assert.Single(fixture.Runtime.Requests);
                Assert.Equal(conversation.Id, request.ConversationId);
                Assert.Equal(automatic ? AutomaticPrompt : Objective, request.UserText);
                fixture.Runtime.Release();
                await queuedRun.Completion.WaitAsync(TestTimeout);
                Assert.Equal(originalMessages.Length + 2, conversation.Messages.Count);
            }
            else
            {
                var failure = await Record.ExceptionAsync(() => queuedRun.Completion.WaitAsync(TestTimeout));
                Assert.IsType<IOException>(failure);
                Assert.Contains("Controlled goal message save failure", failure.ToString(), StringComparison.Ordinal);
                Assert.Empty(fixture.Runtime.Requests);
                Assert.Equal(originalMessages, conversation.Messages.ToArray());
            }
            await queuedCompleted.Task.WaitAsync(TestTimeout);

            Assert.Equal(goalId, conversation.Goal!.Id);
            Assert.Equal(CopilotConversationGoalState.Paused, conversation.Goal.State);
            Assert.Null(fixture.Host.ActiveRun);
            if (restart)
            {
                Assert.Equal(CopilotAgentStopReason.Completed, originalMessages[1].AgentStopReason);
                Assert.False(originalMessages[1].WasResponseInterrupted);
                Assert.False(originalMessages[1].IsThinkingInProgress);
                Assert.Same(laterRun, Assert.Single(fixture.Host.QueuedRuns));
                Assert.Equal(CopilotHostedRunState.Queued, laterRun!.State);
                Assert.False(laterRun.HasStarted);
                Assert.False(laterRun.Completion.IsCompleted);
                Assert.Equal(laterRun.Id, Assert.Single(fixture.Store.LoadedState.QueuedFollowUpRecoveries).RunId);
                Assert.Equal(laterRun.Id, Assert.Single(fixture.ViewModel.QueuedFollowUps).RunId);
            }
            else
            {
                Assert.Empty(fixture.Store.LoadedState.QueuedFollowUpRecoveries);
                Assert.Empty(fixture.ViewModel.QueuedFollowUps);
                Assert.Empty(fixture.Host.QueuedRuns);
            }
            var expectedDraft = automatic ? NewerDraft : NewerDraft + Environment.NewLine + Environment.NewLine + Objective;
            Assert.Equal(expectedDraft, conversation.DraftText);
            Assert.Equal(expectedDraft, fixture.ViewModel.InputText);
            Assert.DoesNotContain(AutomaticPrompt, conversation.DraftText, StringComparison.Ordinal);
            if (!automatic)
                Assert.Contains(conversation.Attachments, item => item.Value == QueuedContext);
            fixture.Store.Disarm();
            await fixture.FlushAsync();
            var saved = fixture.DiskStore.Load();
            var persisted = Assert.Single(saved.Conversations);
            Assert.Equal(CopilotConversationGoalState.Paused, persisted.Goal!.State);
            Assert.Equal(saveSucceeds ? originalMessages.Length + 2 : originalMessages.Length, persisted.Messages.Count);
            Assert.Equal(expectedDraft, persisted.DraftText);
            if (!automatic)
                Assert.Contains(persisted.Attachments, item => item.Value == QueuedContext);
            if (restart)
            {
                var savedLater = Assert.Single(saved.QueuedFollowUpRecoveries);
                Assert.Equal(laterRun!.Id, savedLater.RunId);
                Assert.Equal(laterPrompt, savedLater.ComposerState!.Text);
                Assert.Equal(laterAttachment.Id, Assert.Single(savedLater.ComposerState.Attachments).Id);
                var expectedAttachments = automatic
                    ? new[] { newerAttachment, laterAttachment }
                    : new[] { newerAttachment }.Concat(queued.SubmissionContext.Attachments).Append(laterAttachment).ToArray();
                await AssertRestartedDraftAsync(fixture, originalMessages,
                    expectedDraft + Environment.NewLine + Environment.NewLine + laterPrompt, expectedAttachments);
            }
        }
        finally
        {
            fixture.Host.Changed -= observeQueued;
        }
    }

    private static async Task AssertRestartedDraftAsync(Fixture fixture, CopilotChatMessage[] originalMessages,
        string expectedDraft, CopilotAttachmentItem[] expectedAttachments)
    {
        await fixture.StopAsync();
        var restartedHost = new CopilotAgentTaskHost();
        var restartedRuntime = new GatedRuntime();
        CopilotChatViewModel? restartedViewModel = null;
        try
        {
            var restartedStore = new GatedStore(fixture.DiskStore);
            restartedViewModel = new CopilotChatViewModel(new CopilotChatService(), restartedStore, new CopilotConfig
            {
                SchemaVersion = CopilotConfig.CurrentSchemaVersion,
                McpBearerToken = "unused-goal-recovery-token", Profiles = [fixture.ViewModel.SelectedProfile!.Clone()],
            }, restartedRuntime, restartedHost);
            var restored = Assert.Single(restartedViewModel.Conversations);
            Assert.Null(restartedHost.ActiveRun);
            Assert.Empty(restartedHost.ScheduledRuns);
            Assert.Empty(restartedRuntime.Requests);
            Assert.Empty(restartedViewModel.QueuedFollowUps);
            Assert.Empty(restartedStore.LoadedState.QueuedFollowUpRecoveries);
            Assert.Equal(CopilotConversationGoalState.Paused, restored.Goal!.State);
            Assert.Equal(expectedDraft, restored.DraftText);
            Assert.Equal(expectedDraft, restartedViewModel.InputText);
            Assert.DoesNotContain(AutomaticPrompt, restored.DraftText, StringComparison.Ordinal);
            Assert.Equal(originalMessages.Select(message => message.Id), restored.Messages.Select(message => message.Id));
            Assert.Equal(originalMessages.Select(message => message.Content), restored.Messages.Select(message => message.Content));
            Assert.Equal(CopilotAgentStopReason.Completed, restored.Messages[1].AgentStopReason);
            Assert.False(restored.Messages[1].WasResponseInterrupted);
            Assert.False(restored.Messages[1].IsThinkingInProgress);
            Assert.Equal(originalMessages.SelectMany(message => message.Attachments).Select(item => (item.Id, item.Value)),
                restored.Messages.SelectMany(message => message.Attachments).Select(item => (item.Id, item.Value)));
            Assert.Equal(expectedAttachments.Select(item => (item.Id, item.Value)),
                restored.Attachments.Select(item => (item.Id, item.Value)));
        }
        finally
        {
            var restartedRuns = restartedHost.ScheduledRuns.ToArray();
            restartedRuntime.Release();
            restartedHost.Shutdown();
            try
            {
                foreach (var run in restartedRuns)
                {
                    var failure = await Record.ExceptionAsync(() => run.Completion.WaitAsync(TestTimeout));
                    Assert.IsNotType<TimeoutException>(failure);
                }
            }
            finally
            {
                if (restartedViewModel != null)
                {
                    try
                    {
                        await fixture.FlushAsync(restartedViewModel);
                    }
                    finally
                    {
                        restartedViewModel.Dispose();
                    }
                }
            }
        }
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
        private bool _stopped;

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

        public CopilotQueuedFollowUp ScheduleGoalRequest(bool automatic, CopilotAgentHostContextSnapshot? completedSnapshot = null)
        {
            var execute = typeof(CopilotChatViewModel)
                .GetMethod("ExecuteQueuedFollowUpAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .CreateDelegate<Func<CopilotHostedAgentRun, CopilotQueuedFollowUp, Task>>(ViewModel);
            var context = automatic && completedSnapshot != null
                ? CopilotGoalContinuationContext.Capture(completedSnapshot, Conversation)
                : new CopilotAgentHostContextSnapshot(string.Empty, string.Empty,
                    automatic ? [] : [CopilotAttachmentItem.CreateContext(QueuedContext)]);
            var request = new CopilotQueuedFollowUpRequest(Conversation.Id, Conversation.Title, automatic ? AutomaticPrompt : Objective,
                CopilotAgentMode.Auto, _profile, context,
                null, new CopilotTurnRuntimeConfigSnapshot(new CopilotAgentDefaultsConfig(), []), null,
                GoalId: Conversation.Goal!.Id, AutomaticGoalContinuation: automatic);
            Assert.True(_queue.TrySchedule(request, false, execute, out var queued, out _));
            FollowUpRun = Assert.Single(Host.QueuedRuns);
            return Assert.IsType<CopilotQueuedFollowUp>(queued);
        }

        public void ReleaseBusyRun() => _releaseBusy.TrySetResult();

        public Task FlushAsync(CopilotChatViewModel? targetViewModel = null) => ((Task)typeof(CopilotChatViewModel)
            .GetMethod("FlushStatePersistenceBarrierAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(targetViewModel ?? ViewModel, null)!).WaitAsync(TestTimeout);

        public async Task StopAsync()
        {
            if (_stopped)
                return;
            var runs = Host.ScheduledRuns.Concat([_busyRun]).Distinct().ToArray();
            Store.Disarm();
            _queue.BeginShutdown();
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
                _stopped = true;
                ViewModel.Dispose();
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await StopAsync();
            }
            finally
            {
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
