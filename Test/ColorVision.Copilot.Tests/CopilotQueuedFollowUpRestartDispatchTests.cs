using ColorVision.Solution;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotQueuedFollowUpRestartDispatchTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    private const string UnsafeRunId = "restart-unsafe-follow-up";
    private const string SafeRunId = "restart-safe-follow-up";
    private const string SafeSuccessorRunId = "restart-safe-successor";
    private const string CommandRunId = "restart-local-command";
    private const string UnsafePrompt = "Continue only after I explicitly restart the interrupted conversation.";
    private const string SafePrompt = "Continue the completed conversation.";
    private const string SafeSuccessorPrompt = "Verify the completed conversation's follow-up.";

    [Fact]
    public async Task SendingAQueuedItemNowDuringLocalCommandPreparationPromotesWithoutCancellingTheCommand()
    {
        await using var fixture = new RestartFixture(queuedLocalCommand: true);
        await fixture.Store.CommandSaveEntered.Task.WaitAsync(TestTimeout);
        var commandRun = Assert.IsType<CopilotHostedAgentRun>(fixture.Host.ActiveRun);
        Assert.Equal(CommandRunId, commandRun.Id);
        Assert.True(commandRun.IsQueuedLocalCommand);
        Assert.Null(typeof(CopilotChatViewModel)
            .GetField("_queuedLocalCommandExecution", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.ViewModel));
        var target = Assert.Single(fixture.ViewModel.QueuedFollowUps, item => item.RunId == SafeSuccessorRunId);
        var targetRun = Assert.Single(fixture.Host.QueuedRuns, run => run.Id == SafeSuccessorRunId);
        Assert.Equal(2, fixture.Host.GetQueuePosition(target.RunId));
        Assert.True(fixture.ViewModel.SendQueuedFollowUpNowCommand.CanExecute(target));

        fixture.ViewModel.SendQueuedFollowUpNowCommand.Execute(target);

        Assert.Equal(new[] { SafeSuccessorRunId, SafeRunId }, fixture.Host.QueuedRuns.Select(run => run.Id));
        Assert.Same(commandRun, fixture.Host.ActiveRun);
        Assert.Equal(CopilotHostedRunState.Running, commandRun.State);
        Assert.False(commandRun.CancellationToken.IsCancellationRequested);
        Assert.False(targetRun.HasStarted);
        fixture.Store.ReleaseCommandSave.TrySetResult();
        Assert.Same(targetRun, await fixture.CompletedHandoffAsync(CommandRunId));
        await fixture.Runtime.Gate(SafeSuccessorPrompt).Entered.Task.WaitAsync(TestTimeout);
        fixture.Runtime.Gate(SafeSuccessorPrompt).Release.TrySetResult();
        var blockerRun = Assert.Single(fixture.Host.ScheduledRuns, run => run.Id == SafeRunId);
        Assert.Same(blockerRun, await fixture.CompletedHandoffAsync(SafeSuccessorRunId));
        await fixture.Runtime.Gate(SafePrompt).Entered.Task.WaitAsync(TestTimeout);
        fixture.Runtime.Gate(SafePrompt).Release.TrySetResult();
        Assert.Null(await fixture.CompletedHandoffAsync(SafeRunId));
        Assert.Equal(new[] { SafeSuccessorPrompt, SafePrompt }, fixture.Runtime.Requests.Select(request => request.UserText));
    }

    [Fact]
    public async Task CompletedRecoveredConversationCannotAutomaticallyStartAnInterruptedConversationQueue()
    {
        await using var fixture = new RestartFixture();
        await fixture.Runtime.Gate(SafePrompt).Entered.Task.WaitAsync(TestTimeout);
        var safeRun = Assert.IsType<CopilotHostedAgentRun>(fixture.Host.ActiveRun);
        var unsafeRun = Assert.Single(fixture.Host.QueuedRuns, run => run.Id == UnsafeRunId);
        var safeSuccessor = Assert.Single(fixture.Host.QueuedRuns, run => run.Id == SafeSuccessorRunId);
        var unsafeItem = Assert.Single(fixture.ViewModel.QueuedFollowUps, item => item.RunId == UnsafeRunId);
        Assert.Equal(SafeRunId, safeRun.Id);
        Assert.False(fixture.Runtime.Gate(UnsafePrompt).Entered.Task.IsCompleted);

        fixture.Runtime.Gate(SafePrompt).Release.TrySetResult();

        // Completed is published after the next slot is selected. A gated item
        // in another conversation must be skipped in favor of the legal successor.
        Assert.Same(safeSuccessor, await fixture.CompletedHandoffAsync(SafeRunId));
        await fixture.Runtime.Gate(SafeSuccessorPrompt).Entered.Task.WaitAsync(TestTimeout);
        fixture.Runtime.Gate(SafeSuccessorPrompt).Release.TrySetResult();
        Assert.Null(await fixture.CompletedHandoffAsync(SafeSuccessorRunId));
        Assert.False(unsafeRun.HasStarted);
        Assert.False(fixture.Runtime.Gate(UnsafePrompt).Entered.Task.IsCompleted);
        Assert.Same(unsafeRun, Assert.Single(fixture.Host.QueuedRuns));

        Assert.True(fixture.ViewModel.SendQueuedFollowUpNowCommand.CanExecute(unsafeItem));
        fixture.ViewModel.SendQueuedFollowUpNowCommand.Execute(unsafeItem);
        await fixture.Runtime.Gate(UnsafePrompt).Entered.Task.WaitAsync(TestTimeout);
        fixture.Runtime.Gate(UnsafePrompt).Release.TrySetResult();
        Assert.Null(await fixture.CompletedHandoffAsync(UnsafeRunId));
        Assert.Equal(new[] { SafePrompt, SafeSuccessorPrompt, UnsafePrompt },
            fixture.Runtime.Requests.Select(request => request.UserText));
        Assert.Equal(fixture.SafeConversationId, fixture.Runtime.Requests.ElementAt(0).ConversationId);
        Assert.Equal(fixture.SafeConversationId, fixture.Runtime.Requests.ElementAt(1).ConversationId);
        Assert.Equal(fixture.UnsafeConversationId, fixture.Runtime.Requests.ElementAt(2).ConversationId);
    }

    private sealed class RestartFixture : IAsyncDisposable
    {
        private static readonly FieldInfo SolutionInstance = typeof(SolutionManager).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? _previousSolution = SolutionInstance.GetValue(null);
        private readonly object _isolatedSolution = RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));
        private readonly string _root = Directory.CreateTempSubdirectory("CopilotQueueRestartDispatch-").FullName;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<CopilotHostedAgentRun?>> _handoffs = new(StringComparer.Ordinal);

        public RestartFixture(bool queuedLocalCommand = false)
        {
            SolutionInstance.SetValue(null, _isolatedSolution);
            var profile = new CopilotProfileConfig
            {
                Id = "queue-restart-profile", Name = "Queue restart fixture",
                VendorType = CopilotVendorType.Custom, ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "unused-queue-restart-key", BaseUrl = "https://example.test/v1", Model = "queue-fixture-model",
            };
            var unsafeConversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
            unsafeConversation.SetCustomTitle("Interrupted conversation");
            var interrupted = new CopilotChatMessage(CopilotChatRole.Assistant, "The previous turn was interrupted.")
            {
                RequestMode = CopilotAgentMode.Auto, AgentStopReason = CopilotAgentStopReason.ProviderFailure,
            };
            interrupted.MarkResponseInterrupted("application exited");
            unsafeConversation.Messages.Add(interrupted);
            UnsafeConversationId = unsafeConversation.Id;
            var safeConversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
            safeConversation.SetCustomTitle("Completed conversation");
            safeConversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "The previous turn completed.")
            {
                RequestMode = CopilotAgentMode.Auto, AgentStopReason = CopilotAgentStopReason.Completed,
            });
            SafeConversationId = safeConversation.Id;
            var state = new CopilotChatState
            {
                ActiveConversationId = safeConversation.Id, ActiveProfileId = profile.Id,
                Conversations = [unsafeConversation, safeConversation],
                QueuedFollowUpRecoveries =
                [
                    CreateRecovery(UnsafeRunId, unsafeConversation.Id, UnsafePrompt, profile.Id),
                    CreateRecovery(SafeRunId, safeConversation.Id, SafePrompt, profile.Id),
                    CreateRecovery(SafeSuccessorRunId, safeConversation.Id, SafeSuccessorPrompt, profile.Id),
                ],
            };
            var diskStore = new CopilotChatStateStore(_root);
            if (queuedLocalCommand)
            {
                var commandRecovery = CreateRecovery(CommandRunId, safeConversation.Id, "/help", profile.Id);
                commandRecovery.IsLocalCommand = true;
                state.QueuedFollowUpRecoveries =
                [
                    commandRecovery,
                    CreateRecovery(SafeRunId, safeConversation.Id, SafePrompt, profile.Id),
                    CreateRecovery(SafeSuccessorRunId, safeConversation.Id, SafeSuccessorPrompt, profile.Id),
                ];
            }
            diskStore.Save(state);
            Store = new GatedStore(new CopilotChatStateStore(_root), queuedLocalCommand ? CommandRunId : string.Empty);
            Host.Changed += (_, args) =>
            {
                if (args.Kind == CopilotAgentTaskHostChangeKind.Completed)
                    Handoff(args.Run.Id).TrySetResult(Host.ActiveRun);
            };
            // The real constructor loads, classifies, and restores the durable queue.
            ViewModel = new CopilotChatViewModel(new CopilotChatService(), Store,
                new CopilotConfig
                {
                    SchemaVersion = CopilotConfig.CurrentSchemaVersion,
                    McpBearerToken = "unused-queue-restart-token", Profiles = [profile],
                }, Runtime, Host);
        }

        public string UnsafeConversationId { get; }
        public string SafeConversationId { get; }
        public CopilotChatViewModel ViewModel { get; }
        public GatedStore Store { get; }
        public CopilotAgentTaskHost Host { get; } = new();
        public GatedRuntime Runtime { get; } = new();

        public Task<CopilotHostedAgentRun?> CompletedHandoffAsync(string runId) => Handoff(runId).Task.WaitAsync(TestTimeout);

        private TaskCompletionSource<CopilotHostedAgentRun?> Handoff(string runId) => _handoffs.GetOrAdd(runId,
            _ => new(TaskCreationOptions.RunContinuationsAsynchronously));

        public async ValueTask DisposeAsync()
        {
            var pending = Host.ScheduledRuns.ToArray();
            Host.Shutdown();
            Store.ReleaseCommandSave.TrySetResult();
            Runtime.ReleaseAll();
            try
            {
                foreach (var run in pending)
                {
                    var failure = await Record.ExceptionAsync(() => run.Completion.WaitAsync(TestTimeout));
                    Assert.IsNotType<TimeoutException>(failure);
                }
                await ((Task)typeof(CopilotChatViewModel)
                    .GetMethod("FlushStatePersistenceBarrierAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(ViewModel, null)!).WaitAsync(TestTimeout);
            }
            finally
            {
                ViewModel.Dispose();
                if (ReferenceEquals(SolutionInstance.GetValue(null), _isolatedSolution))
                    SolutionInstance.SetValue(null, _previousSolution);
                var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_root));
                var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
                Assert.True(string.Equals(Path.GetDirectoryName(root), temp, StringComparison.OrdinalIgnoreCase)
                    && Path.GetFileName(root).StartsWith("CopilotQueueRestartDispatch-", StringComparison.Ordinal));
                Directory.Delete(root, recursive: true);
            }
        }

        private static CopilotQueuedFollowUpRecoveryRecord CreateRecovery(string runId, string conversationId, string prompt, string profileId) => new()
        {
            RunId = runId, ConversationId = conversationId, ProfileId = profileId, ResumeAfterRestart = true,
            ComposerState = CopilotComposerStash.Capture(prompt, prompt.Length, CopilotAgentMode.Auto, []),
        };
    }

    private sealed class GatedStore(CopilotChatStateStore inner, string blockedCommandRunId) : ICopilotChatStateStore
    {
        public TaskCompletionSource CommandSaveEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseCommandSave { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string AttachmentDirectoryPath => inner.AttachmentDirectoryPath;
        public CopilotChatState Load() => inner.Load();
        public void Save(CopilotChatState state) => inner.Save(state);
        public CopilotChatStateSnapshot CaptureSnapshot(CopilotChatState state) => inner.CaptureSnapshot(state);
        public string Serialize(CopilotChatStateSnapshot snapshot) => inner.Serialize(snapshot);
        public string Serialize(CopilotChatState state) => inner.Serialize(state);
        public int CleanupOrphanedAttachments(CopilotChatState state) => inner.CleanupOrphanedAttachments(state);

        public async Task SaveSerializedAsync(string serializedState, CancellationToken cancellationToken = default)
        {
            var recoveries = JObject.Parse(serializedState)[nameof(CopilotChatState.QueuedFollowUpRecoveries)]?.Children().ToArray()
                ?? Array.Empty<JToken>();
            if (blockedCommandRunId.Length > 0 && recoveries.Any(record =>
                record[nameof(CopilotQueuedFollowUpRecoveryRecord.RunId)]?.Value<string>() == blockedCommandRunId
                && record[nameof(CopilotQueuedFollowUpRecoveryRecord.ResumeAfterRestart)]?.Value<bool>() != true))
            {
                CommandSaveEntered.TrySetResult();
                await ReleaseCommandSave.Task.WaitAsync(cancellationToken);
            }
            await inner.SaveSerializedAsync(serializedState, cancellationToken);
        }
    }

    private sealed class RuntimeGate
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class GatedRuntime : ICopilotTurnRuntime
    {
        private readonly Dictionary<string, RuntimeGate> _gates = new(StringComparer.Ordinal)
        {
            [UnsafePrompt] = new(), [SafePrompt] = new(), [SafeSuccessorPrompt] = new(),
        };

        public ConcurrentQueue<CopilotTurnRequest> Requests { get; } = new();
        public RuntimeGate Gate(string prompt) => _gates[prompt];

        public void ReleaseAll()
        {
            foreach (var gate in _gates.Values)
                gate.Release.TrySetResult();
        }

        public async IAsyncEnumerable<CopilotTurnEvent> RunAsync(CopilotTurnRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Enqueue(request);
            var gate = Gate(request.UserText);
            gate.Entered.TrySetResult();
            await gate.Release.Task.WaitAsync(cancellationToken);
            var journal = new CopilotAgentTaskEventJournalBuilder(request.TaskEventJournalBaseline);
            journal.RecordRunStarted();
            journal.RecordStop(CopilotAgentStopReason.Completed);
            var result = new CopilotAgentRunResult
            {
                PreparedUserMessageContent = request.UserText,
                StopReason = CopilotAgentStopReason.Completed,
                TaskEventJournal = journal.Snapshot(),
            };
            yield return new CopilotTurnStartedEvent(request.TaskId, request.Mode);
            yield return new CopilotTurnPlanUpdatedEvent(CopilotTurnPlanSnapshot.FromTaskLedger(result.TaskLedger));
            yield return new CopilotTurnAgentEvent(CopilotAgentEvent.AnswerDelta("Fixture request completed."));
            yield return new CopilotTurnAgentEvent(CopilotAgentEvent.Completed());
            yield return CopilotTurnCompletedEvent.Completed(request.TaskId, CopilotTurnResult.FromAgent(
                request.Mode, CopilotTokenUsage.Empty, result));
        }

        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message) => new(CopilotSteeringAdmissionReason.RuntimeUnavailable);
        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;
        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;
        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) => false;
        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(CopilotWorkspaceRollbackActionRequest request,
            Action<CopilotAgentEvent> onEvent, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
