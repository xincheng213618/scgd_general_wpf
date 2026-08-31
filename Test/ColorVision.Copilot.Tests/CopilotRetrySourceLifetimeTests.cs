using ColorVision.Copilot;
using ColorVision.Solution;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotRetrySourceLifetimeTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    private const string OriginalPrompt = "Inspect the original image and continue the task.";
    private const string ReplacementPrompt = "Use the replacement request instead.";

    [Theory]
    [InlineData("replacement")]
    [InlineData("checkpoint")]
    [InlineData("editing")]
    [InlineData("unchanged")]
    [InlineData("switch")]
    [InlineData("switch-and-edit-other")]
    public void RetryRevalidatesItsOriginalTurnAfterImageAdmission(string transition)
    {
        RunOnSta(() =>
        {
            using var fixture = new Fixture();
            using var context = new PausedAdmissionContext();
            var previousContext = SynchronizationContext.Current;
            Task? retry = null;
            try
            {
                SynchronizationContext.SetSynchronizationContext(context);
                Assert.True(fixture.ViewModel.RetryMessageCommand.CanExecute(fixture.OriginalAssistant));
                retry = InvokeTask(fixture.ViewModel, "RetryMessageAsync", [fixture.OriginalAssistant, false],
                    [typeof(CopilotChatMessage), typeof(bool)]);
                Assert.True(context.WaitForCallback(TestTimeout));
                Assert.False(retry.IsCompleted);
                Assert.False(fixture.ViewModel.IsBusy);
                Assert.Single(Directory.GetFiles(fixture.StoragePath, "image-*.png"));
                Assert.Empty(fixture.Runtime.Requests);

                // Keep the first admission continuation suspended while later UI work
                // completes through its own normal request path.
                SynchronizationContext.SetSynchronizationContext(previousContext);
                if (transition is "replacement" or "editing")
                {
                    Assert.True(fixture.ViewModel.EditMessageCommand.CanExecute(fixture.OriginalUser));
                    fixture.ViewModel.EditMessageCommand.Execute(fixture.OriginalUser);
                    Assert.True(fixture.ViewModel.IsEditingMessage);
                    fixture.ViewModel.InputText = ReplacementPrompt;
                    if (transition == "replacement")
                    {
                        var editingAttachment = Assert.Single(fixture.Conversation.Attachments);
                        InvokeTask(fixture.ViewModel, "RemoveAttachment", [editingAttachment], [typeof(CopilotAttachmentItem)])
                            .WaitAsync(TestTimeout).GetAwaiter().GetResult();
                        InvokeTask(fixture.ViewModel, "SendAsync", [], Type.EmptyTypes)
                            .WaitAsync(TestTimeout).GetAwaiter().GetResult();
                        Assert.False(fixture.ViewModel.IsEditingMessage);
                        Assert.Equal(ReplacementPrompt, Assert.Single(fixture.Runtime.Requests).UserText);
                        Assert.DoesNotContain(fixture.OriginalUser, fixture.Conversation.Messages);
                        Assert.True(fixture.ViewModel.ContinueAgentTasksCommand.CanExecute(fixture.Conversation.Messages[^1]));
                    }
                }
                else if (transition == "checkpoint")
                {
                    fixture.PublishRecoverableCheckpoint();
                    Assert.True(fixture.ViewModel.ContinueAgentTasksCommand.CanExecute(fixture.OriginalAssistant));
                    Assert.False(fixture.ViewModel.RetryMessageCommand.CanExecute(fixture.OriginalAssistant));
                }
                else if (transition.StartsWith("switch", StringComparison.Ordinal))
                {
                    Assert.True(fixture.ViewModel.TrySelectConversation(fixture.OtherConversation.Id));
                    if (transition == "switch-and-edit-other")
                    {
                        var otherUser = new CopilotChatMessage(CopilotChatRole.User, "Other conversation request");
                        fixture.OtherConversation.Messages.Add(otherUser);
                        fixture.OtherConversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Other conversation answer"));
                        fixture.ViewModel.InputText = string.Empty;
                        Assert.True(fixture.ViewModel.EditMessageCommand.CanExecute(otherUser));
                        fixture.ViewModel.EditMessageCommand.Execute(otherUser);
                        fixture.ViewModel.InputText = "Edited other conversation draft";
                        Assert.True(fixture.ViewModel.IsEditingMessage);
                    }
                }

                var messagesBeforeResume = fixture.Conversation.Messages.ToArray();
                var otherMessagesBeforeResume = fixture.OtherConversation.Messages.ToArray();
                var checkpointBeforeResume = fixture.Conversation.AgentSessionCheckpoint;
                if (transition is "replacement" or "checkpoint")
                    Assert.NotNull(checkpointBeforeResume);
                SynchronizationContext.SetSynchronizationContext(context);
                context.Complete(retry);
                retry.GetAwaiter().GetResult();

                if (transition is "unchanged" or "switch" or "switch-and-edit-other")
                {
                    var request = Assert.Single(fixture.Runtime.Requests);
                    Assert.Equal(OriginalPrompt, request.UserText);
                    Assert.Equal(fixture.Conversation.Id, request.ConversationId);
                    Assert.Same(fixture.OriginalUser, fixture.Conversation.Messages[0]);
                    Assert.NotSame(fixture.OriginalAssistant, fixture.Conversation.Messages[1]);
                    Assert.Equal(2, fixture.Conversation.Messages.Count);
                    if (transition.StartsWith("switch", StringComparison.Ordinal))
                    {
                        Assert.Same(fixture.OtherConversation, fixture.ViewModel.SelectedConversation);
                        Assert.Equal(transition == "switch-and-edit-other" ? "Edited other conversation draft" : "Other conversation draft",
                            fixture.ViewModel.InputText);
                        Assert.Equal(transition == "switch-and-edit-other", fixture.ViewModel.IsEditingMessage);
                    }
                }
                else
                {
                    Assert.Equal(transition == "replacement" ? 1 : 0, fixture.Runtime.Requests.Count);
                    Assert.DoesNotContain(fixture.Runtime.Requests, request => request.UserText == OriginalPrompt);
                    Assert.Equal(messagesBeforeResume, fixture.Conversation.Messages.ToArray());
                    Assert.True(CopilotAgentSessionCheckpoint.AreEquivalent(checkpointBeforeResume, fixture.Conversation.AgentSessionCheckpoint));
                    if (transition == "editing")
                    {
                        Assert.True(fixture.ViewModel.IsEditingMessage);
                        Assert.Equal(ReplacementPrompt, fixture.ViewModel.InputText);
                    }
                }
                Assert.Equal(otherMessagesBeforeResume, fixture.OtherConversation.Messages.ToArray());
                Assert.Equal(transition == "switch-and-edit-other" ? "Edited other conversation draft" : "Other conversation draft",
                    fixture.OtherConversation.DraftText);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(context);
                if (retry is { IsCompleted: false })
                    context.Complete(retry);
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        });
    }

    private sealed class Fixture : IDisposable
    {
        private static readonly FieldInfo SolutionInstance = typeof(SolutionManager).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? _previousSolution = SolutionInstance.GetValue(null);
        private readonly object _isolatedSolution = RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));
        private readonly string _root = Directory.CreateTempSubdirectory("CopilotRetrySource-").FullName;
        private readonly CopilotProfileConfig _profile;

        public Fixture()
        {
            SolutionInstance.SetValue(null, _isolatedSolution);
            var source = Path.Combine(_root, "source.png");
            CreateImage(source);
            StoragePath = Path.Combine(_root, "managed");
            _profile = new CopilotProfileConfig
            {
                Id = "retry-source-profile", Name = "Retry source profile",
                VendorType = CopilotVendorType.Custom, ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "retry-source-test-key", BaseUrl = "https://example.test/v1",
                Model = "retry-source-model", SupportsImageInput = true,
            };
            Conversation = CopilotConversationRecord.CreateEmpty(_profile.Id, _profile.DisplayLabel);
            OriginalUser = new CopilotChatMessage(CopilotChatRole.User, OriginalPrompt)
            {
                RequestMode = CopilotAgentMode.Auto,
                AttachmentSnapshotCaptured = true,
                Attachments = [CopilotAttachmentItem.CreateImage(source)],
            };
            OriginalAssistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Original completed answer.")
            {
                RequestMode = CopilotAgentMode.Auto,
                AgentStopReason = CopilotAgentStopReason.Completed,
            };
            Conversation.Messages.Add(OriginalUser);
            Conversation.Messages.Add(OriginalAssistant);
            OtherConversation = CopilotConversationRecord.CreateEmpty(_profile.Id, _profile.DisplayLabel);
            OtherConversation.DraftText = "Other conversation draft";
            var state = new CopilotChatState
            {
                ActiveConversationId = Conversation.Id, ActiveProfileId = _profile.Id,
                Conversations = [Conversation, OtherConversation],
            };
            var config = new CopilotConfig
            {
                SchemaVersion = CopilotConfig.CurrentSchemaVersion,
                McpBearerToken = "retry-source-test-token", Profiles = [_profile],
            };
            ViewModel = new CopilotChatViewModel(new CopilotChatService(), new MemoryStore(state, StoragePath), config, Runtime, Host);
        }

        public string StoragePath { get; }
        public CopilotConversationRecord Conversation { get; }
        public CopilotConversationRecord OtherConversation { get; }
        public CopilotChatMessage OriginalUser { get; }
        public CopilotChatMessage OriginalAssistant { get; }
        public CopilotChatViewModel ViewModel { get; }
        public CopilotAgentTaskHost Host { get; } = new();
        public CompletingRuntime Runtime { get; } = new();

        public void PublishRecoverableCheckpoint()
        {
            var result = CreatePausedResult(CopilotResponsePresentationGuidance.CreateRequestProfile(_profile),
                Conversation.CurrentAgentTaskEventJournal, OriginalPrompt);
            OriginalAssistant.AgentTaskLedger = result.TaskLedger;
            OriginalAssistant.AgentStopReason = result.StopReason;
            Assert.True(Conversation.CommitAgentRunState(result.TaskEventJournal, result.SessionCheckpoint));
        }

        public void Dispose()
        {
            Host.Shutdown();
            ViewModel.Dispose();
            if (ReferenceEquals(SolutionInstance.GetValue(null), _isolatedSolution))
                SolutionInstance.SetValue(null, _previousSolution);
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_root));
            var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            Assert.True(string.Equals(Path.GetDirectoryName(root), temp, StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(root).StartsWith("CopilotRetrySource-", StringComparison.Ordinal));
            Directory.Delete(root, recursive: true);
        }
    }

    private static CopilotAgentRunResult CreatePausedResult(CopilotProfileConfig profile,
        CopilotAgentTaskEventJournalSnapshot? previous, string prompt)
    {
        var ledger = new CopilotAgentTaskLedgerSnapshot
        {
            Mode = "execute",
            Items = [new CopilotAgentTaskItem { Id = 1, Title = "Continue the admitted task", Description = "Preserve pending work." }],
        };
        var journal = new CopilotAgentTaskEventJournalBuilder(previous);
        journal.RecordRunStarted();
        journal.RecordTaskLedger(ledger, "paused-admission-test");
        journal.RecordStop(CopilotAgentStopReason.Paused);
        var checkpoint = CopilotAgentSessionCheckpoint.Create(profile, "{}",
            CopilotCapabilityCatalog.Shared.GetSnapshot(), taskEventJournal: journal.Snapshot());
        Assert.NotNull(checkpoint);
        return new CopilotAgentRunResult
        {
            PreparedUserMessageContent = prompt, TaskLedger = ledger,
            StopReason = CopilotAgentStopReason.Paused, TaskEventJournal = journal.Snapshot(), SessionCheckpoint = checkpoint,
        };
    }

    private sealed class CompletingRuntime : ICopilotTurnRuntime
    {
        public ConcurrentQueue<CopilotTurnRequest> Requests { get; } = new();
        public async IAsyncEnumerable<CopilotTurnEvent> RunAsync(CopilotTurnRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Enqueue(request);
            var result = CreatePausedResult(request.Profile, request.TaskEventJournalBaseline, request.UserText);
            await Task.CompletedTask;
            yield return new CopilotTurnStartedEvent(request.TaskId, request.Mode);
            yield return new CopilotTurnPlanUpdatedEvent(CopilotTurnPlanSnapshot.FromTaskLedger(result.TaskLedger));
            yield return new CopilotTurnAgentEvent(CopilotAgentEvent.CheckpointUpdated(result.SessionCheckpoint!, result.TaskLedger));
            yield return new CopilotTurnAgentEvent(CopilotAgentEvent.CheckpointReady());
            yield return new CopilotTurnAgentEvent(CopilotAgentEvent.AnswerDelta("Task paused with recoverable work."));
            yield return new CopilotTurnAgentEvent(CopilotAgentEvent.Completed());
            yield return CopilotTurnCompletedEvent.Completed(request.TaskId, CopilotTurnResult.FromAgent(request.Mode, CopilotTokenUsage.Empty, result));
        }
        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message) => new(CopilotSteeringAdmissionReason.RuntimeUnavailable);
        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;
        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;
        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) => false;
        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(CopilotWorkspaceRollbackActionRequest request,
            Action<CopilotAgentEvent> onEvent, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class MemoryStore(CopilotChatState state, string path) : ICopilotChatStateStore
    {
        public string AttachmentDirectoryPath => path;
        public CopilotChatState Load() => state;
        public void Save(CopilotChatState value) { }
        public CopilotChatStateSnapshot CaptureSnapshot(CopilotChatState value) => new(new JObject());
        public string Serialize(CopilotChatStateSnapshot snapshot) => "{}";
        public string Serialize(CopilotChatState value) => "{}";
        public Task SaveSerializedAsync(string serializedState, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public int CleanupOrphanedAttachments(CopilotChatState value) => 0;
    }

    private static Task InvokeTask(CopilotChatViewModel viewModel, string methodName, object?[] arguments, Type[] parameterTypes) =>
        Assert.IsAssignableFrom<Task>(typeof(CopilotChatViewModel).GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.NonPublic, parameterTypes)!.Invoke(viewModel, arguments));

    private static void CreateImage(string path)
    {
        const int dimension = 1_024;
        var pixels = new byte[dimension * dimension * 4];
        new Random(42).NextBytes(pixels);
        var bitmap = BitmapSource.Create(dimension, dimension, 96, 96, PixelFormats.Bgra32, null, pixels, dimension * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
        Assert.InRange(stream.Length, 1, CopilotImagePayloadLoader.MaximumImageBytes);
    }

    private sealed class PausedAdmissionContext : SynchronizationContext, IDisposable
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _callbacks = new();
        private readonly AutoResetEvent _posted = new(false);
        public override void Post(SendOrPostCallback callback, object? state)
        {
            _callbacks.Enqueue((callback, state));
            _posted.Set();
        }
        public bool WaitForCallback(TimeSpan timeout) => _posted.WaitOne(timeout);
        public void Complete(Task operation)
        {
            var deadline = DateTime.UtcNow + TestTimeout;
            var waits = new[] { _posted, ((IAsyncResult)operation).AsyncWaitHandle };
            while (!operation.IsCompleted)
            {
                if (_callbacks.TryDequeue(out var callback))
                {
                    callback.Callback(callback.State);
                    continue;
                }
                var remaining = deadline - DateTime.UtcNow;
                Assert.True(remaining > TimeSpan.Zero, "Retry admission did not finish.");
                Assert.NotEqual(WaitHandle.WaitTimeout, WaitHandle.WaitAny(waits, remaining));
            }
        }
        public void Dispose() => _posted.Dispose();
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = exception; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(40)), "Retry source test did not finish.");
        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
