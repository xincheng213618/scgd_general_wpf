using ColorVision.Copilot;
using ColorVision.Solution;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotSteeringCancellationRecoveryTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    private const string SteeringText = "Check the remaining constraint before proceeding.";
    private const string NewerDraft = "A newer draft that must remain available.";

    [Theory]
    [InlineData(CopilotAgentControlIntent.Cancel, false)]
    [InlineData(CopilotAgentControlIntent.Cancel, true)]
    [InlineData(CopilotAgentControlIntent.Pause, false)]
    public async Task CancellationDeadlineRestoresUnconfirmedSteeringToItsOwner(
        CopilotAgentControlIntent intent, bool switchConversation)
    {
        await using var fixture = new Fixture();
        var run = await fixture.StartAndSteerAsync();
        fixture.ViewModel.InputText = NewerDraft;
        var attachment = CopilotAttachmentItem.CreateContext("Newer draft attachment");
        fixture.Conversation.Attachments.Add(attachment);
        Assert.True(CopilotSteeringRecovery.TrackPending(fixture.Conversation, "other-run",
            new CopilotSteeringMessageSnapshot("other-steering", "A different run's pending instruction."), DateTimeOffset.UtcNow));
        if (switchConversation)
        {
            fixture.ViewModel.SelectConversationCommand.Execute(fixture.OtherConversation);
            Assert.Same(fixture.OtherConversation, fixture.ViewModel.SelectedConversation);
        }

        Assert.True(intent == CopilotAgentControlIntent.Pause
            ? fixture.Host.RequestPause(run.Id)
            : fixture.Host.RequestCancel(run.Id));
        await WaitForCompletionAsync(run);

        Assert.False(fixture.Runtime.ProducerFinished.IsCompleted);
        Assert.True(fixture.Conversation.Messages.Last().WasResponseInterrupted);
        Assert.Contains(NewerDraft, fixture.Conversation.DraftText, StringComparison.Ordinal);
        Assert.Contains(SteeringText, fixture.Conversation.DraftText, StringComparison.Ordinal);
        Assert.Equal("other-run", Assert.Single(fixture.Conversation.PendingSteeringRecoveries).TaskId);
        Assert.DoesNotContain("A different run's pending instruction.", fixture.Conversation.DraftText, StringComparison.Ordinal);
        Assert.Same(attachment, Assert.Single(fixture.Conversation.Attachments));
        Assert.Equal("Other conversation draft", fixture.OtherConversation.DraftText);
        Assert.Equal(switchConversation ? fixture.OtherConversation.DraftText : fixture.Conversation.DraftText,
            fixture.ViewModel.InputText);
        Assert.Equal(1, fixture.Runtime.RequestCount);

        // A late producer finally cannot replay the recovery after the stream closed.
        var restoredDraft = fixture.Conversation.DraftText;
        fixture.Runtime.Release();
        await fixture.Runtime.ProducerFinished.WaitAsync(TestTimeout);
        Assert.Equal(restoredDraft, fixture.Conversation.DraftText);
    }

    [Fact]
    public async Task TimelyRuntimeRecoveryIsNotRestoredAgainByHostedCompletion()
    {
        await using var fixture = new Fixture(cooperateWithCancellation: true);
        var run = await fixture.StartAndSteerAsync();
        fixture.ViewModel.InputText = NewerDraft;

        Assert.True(fixture.Host.RequestCancel(run.Id));
        await WaitForCompletionAsync(run);

        Assert.True(fixture.Runtime.ProducerFinished.IsCompleted);
        Assert.Empty(fixture.Conversation.PendingSteeringRecoveries);
        Assert.Contains(NewerDraft, fixture.Conversation.DraftText, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(fixture.Conversation.DraftText, SteeringText));
        Assert.Equal(fixture.Conversation.DraftText, fixture.ViewModel.InputText);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitCancellationDoesNotReplayConfirmedSteering(bool checkpointed)
    {
        await using var fixture = new Fixture();
        var run = await fixture.StartAndSteerAsync();
        fixture.Runtime.PublishDelivery(checkpointed);
        await WaitUntilAsync(() => checkpointed
            ? fixture.Conversation.PendingSteeringRecoveries.Count == 0
            : run.GetDeliveredSteeringAwaitingCheckpoint().Messages.Count == 1);
        fixture.ViewModel.InputText = NewerDraft;

        Assert.True(fixture.Host.RequestCancel(run.Id));
        await WaitForCompletionAsync(run);

        Assert.False(fixture.Runtime.ProducerFinished.IsCompleted);
        Assert.Empty(fixture.Conversation.PendingSteeringRecoveries);
        Assert.Equal(NewerDraft, fixture.Conversation.DraftText);
        Assert.Equal(NewerDraft, fixture.ViewModel.InputText);
        Assert.Equal(1, fixture.Runtime.RequestCount);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private static readonly FieldInfo SolutionInstance = typeof(SolutionManager)
            .GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? _previousSolution = SolutionInstance.GetValue(null);
        private readonly object _isolatedSolution = RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));
        private readonly string _root = Directory.CreateTempSubdirectory("CopilotSteeringCancellation-").FullName;

        public Fixture(bool cooperateWithCancellation = false)
        {
            SolutionInstance.SetValue(null, _isolatedSolution);
            var profile = new CopilotProfileConfig
            {
                Id = "steering-cancellation-profile", Name = "Steering cancellation profile",
                VendorType = CopilotVendorType.Custom, ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "steering-cancellation-test-key", BaseUrl = "https://example.test/v1",
                Model = "steering-test-model",
            };
            Conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
            OtherConversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
            OtherConversation.DraftText = "Other conversation draft";
            var state = new CopilotChatState
            {
                ActiveConversationId = Conversation.Id, ActiveProfileId = profile.Id,
                Conversations = [Conversation, OtherConversation],
            };
            var config = new CopilotConfig
            {
                SchemaVersion = CopilotConfig.CurrentSchemaVersion,
                McpBearerToken = "steering-cancellation-test-token", Profiles = [profile],
            };
            Runtime = new GatedSteeringRuntime(cooperateWithCancellation);
            ViewModel = new CopilotChatViewModel(new CopilotChatService(), new MemoryStore(state, _root), config, Runtime, Host);
        }

        public CopilotConversationRecord Conversation { get; }
        public CopilotConversationRecord OtherConversation { get; }
        public CopilotChatViewModel ViewModel { get; }
        public CopilotAgentTaskHost Host { get; } = new();
        public GatedSteeringRuntime Runtime { get; }

        public async Task<CopilotHostedAgentRun> StartAndSteerAsync()
        {
            ViewModel.QueueExternalPrompt("Begin a bounded fixture task.", startNewConversation: false, sendNow: true, mode: CopilotAgentMode.Auto);
            await Runtime.Entered.WaitAsync(TestTimeout);
            await WaitUntilAsync(() => Host.ActiveRun?.CanRequestPause == true);
            var run = Assert.IsType<CopilotHostedAgentRun>(Host.ActiveRun);
            ViewModel.InputText = SteeringText;
            Assert.True(ViewModel.SteerCommand.CanExecute(null));
            ViewModel.SteerCommand.Execute(null);
            Assert.Equal(string.Empty, ViewModel.InputText);
            var recovery = Assert.Single(Conversation.PendingSteeringRecoveries);
            Assert.Equal(run.Id, recovery.TaskId);
            Assert.Equal(SteeringText, recovery.Text);
            return run;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                Runtime.Release();
                if (Host.ActiveRun is { } active)
                {
                    Host.RequestCancel(active.Id);
                    await WaitForCompletionAsync(active);
                }
                if (Runtime.Entered.IsCompleted)
                    await Runtime.ProducerFinished.WaitAsync(TestTimeout);
            }
            finally
            {
                Host.Shutdown();
                ViewModel.Dispose();
                if (ReferenceEquals(SolutionInstance.GetValue(null), _isolatedSolution))
                    SolutionInstance.SetValue(null, _previousSolution);
                var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_root));
                var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
                Assert.True(string.Equals(Path.GetDirectoryName(root), temp, StringComparison.OrdinalIgnoreCase)
                    && Path.GetFileName(root).StartsWith("CopilotSteeringCancellation-", StringComparison.Ordinal));
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class GatedSteeringRuntime(bool cooperateWithCancellation) : ICopilotTurnRuntime
    {
        private readonly TaskCompletionSource<CopilotTurnRequest> _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _producerFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CopilotAgentTaskLedgerSnapshot _ledger = new()
        {
            Mode = "execute",
            Items = [new CopilotAgentTaskItem { Id = 1, Title = "Await fixture control", Description = "No external operation is performed." }],
        };
        private CopilotTurnEventSink? _sink;
        private CopilotTurnRequest? _request;
        private CopilotAgentTaskEventJournalBuilder? _journal;
        private CopilotSteeringMessageSnapshot? _steering;
        private bool _delivered;
        private int _requestCount;

        public Task<CopilotTurnRequest> Entered => _entered.Task;
        public Task ProducerFinished => _producerFinished.Task;
        public int RequestCount => Volatile.Read(ref _requestCount);

        public IAsyncEnumerable<CopilotTurnEvent> RunAsync(CopilotTurnRequest request, CancellationToken cancellationToken) =>
            CopilotTurnEventStream.RunAsync(request.TaskId, request.Mode, async (sink, producerToken) =>
            {
                Interlocked.Increment(ref _requestCount);
                _sink = sink;
                _request = request;
                _journal = new CopilotAgentTaskEventJournalBuilder(request.TaskEventJournalBaseline);
                _journal.RecordRunStarted();
                _journal.RecordTaskLedger(_ledger, "steering-cancellation-fixture");
                PublishCheckpoint(includeSteering: false);
                sink.OnAgentEvent(CopilotAgentEvent.CheckpointReady());
                _entered.TrySetResult(request);
                try
                {
                    // The noncooperative branch models a producer that cannot finish
                    // within the real stream's bounded cancellation grace period.
                    if (cooperateWithCancellation)
                        await _release.Task.WaitAsync(producerToken);
                    else
                        await _release.Task;
                    throw new OperationCanceledException(producerToken);
                }
                finally
                {
                    try
                    {
                        if (!_delivered && _steering is { } message)
                            sink.OnAgentEvent(CopilotAgentEvent.SteeringRecovery([message]));
                    }
                    finally
                    {
                        _producerFinished.TrySetResult();
                    }
                }
            }, cancellationToken, producerShutdownTimeout: cooperateWithCancellation
                ? TimeSpan.FromSeconds(1) : TimeSpan.FromMilliseconds(50));

        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message)
        {
            if (!string.Equals(_request?.TaskId, taskId, StringComparison.Ordinal) || _steering != null)
                return new(CopilotSteeringAdmissionReason.NoActiveTask);
            _steering = new CopilotSteeringMessageSnapshot("fixture-steering", message);
            _journal!.RecordSteering(message);
            return new(CopilotSteeringAdmissionReason.Accepted, _steering.MessageId);
        }

        public void PublishDelivery(bool checkpointed)
        {
            var message = Assert.IsType<CopilotSteeringMessageSnapshot>(_steering);
            _delivered = true;
            _journal!.RecordSteeringDelivered(message.Text);
            _sink!.OnAgentEvent(CopilotAgentEvent.SteeringDelivered([message]));
            if (checkpointed)
                PublishCheckpoint(includeSteering: true);
        }

        private void PublishCheckpoint(bool includeSteering)
        {
            var memory = includeSteering
                ? new[] { new CopilotRequestMessage("user", _steering!.Text) { IsSteering = true } }
                : Array.Empty<CopilotRequestMessage>();
            var checkpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(CopilotAgentSessionCheckpoint.Create(
                _request!.Profile, "{}", CopilotCapabilityCatalog.Shared.GetSnapshot(),
                taskEventJournal: _journal!.Snapshot(), conversationMemory: memory));
            _sink!.OnAgentEvent(CopilotAgentEvent.CheckpointUpdated(checkpoint, _ledger));
        }

        public void Release() => _release.TrySetResult();
        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;
        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;
        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) => false;
        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(CopilotWorkspaceRollbackActionRequest request,
            Action<CopilotAgentEvent> onEvent, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class MemoryStore(CopilotChatState state, string root) : ICopilotChatStateStore
    {
        public string AttachmentDirectoryPath => root;
        public CopilotChatState Load() => state;
        public void Save(CopilotChatState value) { }
        public CopilotChatStateSnapshot CaptureSnapshot(CopilotChatState value) => new(new JObject());
        public string Serialize(CopilotChatStateSnapshot snapshot) => "{}";
        public string Serialize(CopilotChatState value) => "{}";
        public Task SaveSerializedAsync(string serializedState, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public int CleanupOrphanedAttachments(CopilotChatState value) => 0;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TestTimeout;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The steering fixture did not reach its expected state.");
            await Task.Delay(10);
        }
    }

    private static async Task WaitForCompletionAsync(CopilotHostedAgentRun run)
    {
        try
        {
            await run.Completion.WaitAsync(TestTimeout);
        }
        catch (OperationCanceledException) when (run.CancellationToken.IsCancellationRequested)
        {
        }
    }

    private static int CountOccurrences(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;
}
