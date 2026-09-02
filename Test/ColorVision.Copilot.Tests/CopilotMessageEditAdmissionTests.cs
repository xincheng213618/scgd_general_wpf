using ColorVision.Copilot;
using ColorVision.Solution;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotMessageEditAdmissionTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    private const string OriginalPrompt = "Original request with a captured image.";
    private const string SubmittedEdit = "The edited request submitted before admission.";
    private const string NewerText = "A later edit or draft that must not be consumed.";

    [Theory]
    [InlineData("cancel")]
    [InlineData("reopen")]
    [InlineData("switch-edit-other")]
    [InlineData("replace")]
    [InlineData("unchanged")]
    [InlineData("newer-draft")]
    public void EditedSendRevalidatesItsEditSessionAfterImageAdmission(string transition)
    {
        StaTest.Run(() =>
        {
            using var fixture = new Fixture();
            using var context = new PausedAdmissionContext();
            var previousContext = SynchronizationContext.Current;
            Task? send = null;
            try
            {
                Assert.True(fixture.ViewModel.EditMessageCommand.CanExecute(fixture.OriginalUser));
                fixture.ViewModel.EditMessageCommand.Execute(fixture.OriginalUser);
                Assert.True(fixture.ViewModel.IsEditingMessage);
                fixture.ViewModel.InputText = SubmittedEdit;
                SynchronizationContext.SetSynchronizationContext(context);
                send = InvokeTask(fixture.ViewModel, "SendAsync", [], Type.EmptyTypes);
                Assert.True(context.WaitForCallback(TestTimeout));
                Assert.False(send.IsCompleted);
                Assert.False(fixture.ViewModel.IsBusy);
                Assert.Single(Directory.GetFiles(fixture.StoragePath, "image-*.png"));
                Assert.Empty(fixture.Runtime.Requests);

                // Only the first image-admission continuation is suspended. Later
                // user actions still enter through the actual VM command methods.
                SynchronizationContext.SetSynchronizationContext(previousContext);
                if (transition is "cancel" or "reopen")
                {
                    Assert.True(fixture.ViewModel.CancelMessageEditCommand.CanExecute(null));
                    fixture.ViewModel.CancelMessageEditCommand.Execute(null);
                    Assert.False(fixture.ViewModel.IsEditingMessage);
                    if (transition == "reopen")
                    {
                        fixture.ViewModel.EditMessageCommand.Execute(fixture.OriginalUser);
                        Assert.True(fixture.ViewModel.IsEditingMessage);
                    }
                    fixture.ViewModel.InputText = NewerText;
                }
                else if (transition == "switch-edit-other")
                {
                    Assert.True(fixture.ViewModel.TrySelectConversation(fixture.OtherConversation.Id));
                    Assert.False(fixture.ViewModel.IsEditingMessage);
                    Assert.True(fixture.ViewModel.EditMessageCommand.CanExecute(fixture.OtherUser));
                    fixture.ViewModel.EditMessageCommand.Execute(fixture.OtherUser);
                    fixture.ViewModel.InputText = NewerText;
                    Assert.True(fixture.ViewModel.IsEditingMessage);
                }
                else if (transition == "replace")
                {
                    fixture.ViewModel.InputText = NewerText;
                    var image = Assert.Single(fixture.Conversation.Attachments);
                    InvokeTask(fixture.ViewModel, "RemoveAttachment", [image], [typeof(CopilotAttachmentItem)])
                        .WaitAsync(TestTimeout).GetAwaiter().GetResult();
                    InvokeTask(fixture.ViewModel, "SendAsync", [], Type.EmptyTypes)
                        .WaitAsync(TestTimeout).GetAwaiter().GetResult();
                    Assert.Equal(NewerText, Assert.Single(fixture.Runtime.Requests).UserText);
                    Assert.DoesNotContain(fixture.OriginalUser, fixture.Conversation.Messages);
                    Assert.False(fixture.ViewModel.IsEditingMessage);
                    Assert.NotNull(fixture.Conversation.AgentSessionCheckpoint);
                }
                else if (transition == "newer-draft")
                {
                    fixture.ViewModel.InputText = NewerText;
                    fixture.Conversation.Attachments.Add(CopilotAttachmentItem.CreateContext("Newer draft attachment"));
                }

                var originalMessagesBeforeResume = fixture.Conversation.Messages.ToArray();
                var otherMessagesBeforeResume = fixture.OtherConversation.Messages.ToArray();
                var checkpointBeforeResume = fixture.Conversation.AgentSessionCheckpoint;
                var draftBeforeResume = fixture.ViewModel.InputText;
                var attachmentsBeforeResume = fixture.ViewModel.SelectedConversation!.Attachments.ToArray();
                SynchronizationContext.SetSynchronizationContext(context);
                context.Complete(send);
                send.GetAwaiter().GetResult();

                if (transition is "unchanged" or "newer-draft")
                {
                    var request = Assert.Single(fixture.Runtime.Requests);
                    Assert.Equal(SubmittedEdit, request.UserText);
                    Assert.Equal(fixture.Conversation.Id, request.ConversationId);
                    Assert.Single(request.HostContext.Attachments);
                    Assert.Equal(2, fixture.Conversation.Messages.Count);
                    Assert.Equal(SubmittedEdit, fixture.Conversation.Messages[0].Content);
                    Assert.NotSame(fixture.OriginalUser, fixture.Conversation.Messages[0]);
                    Assert.NotSame(fixture.OriginalAssistant, fixture.Conversation.Messages[1]);
                    if (transition == "unchanged")
                    {
                        Assert.Empty(fixture.ViewModel.InputText);
                        Assert.Empty(fixture.Conversation.Attachments);
                    }
                    else
                    {
                        Assert.Equal(NewerText, fixture.ViewModel.InputText);
                        Assert.Contains(fixture.Conversation.Attachments, item => item.Value == "Newer draft attachment");
                    }
                }
                else
                {
                    Assert.Equal(transition == "replace" ? 1 : 0, fixture.Runtime.Requests.Count);
                    Assert.DoesNotContain(fixture.Runtime.Requests, request => request.UserText == SubmittedEdit);
                    Assert.Equal(originalMessagesBeforeResume, fixture.Conversation.Messages.ToArray());
                    Assert.True(CopilotAgentSessionCheckpoint.AreEquivalent(checkpointBeforeResume, fixture.Conversation.AgentSessionCheckpoint));
                    Assert.Equal(draftBeforeResume, fixture.ViewModel.InputText);
                    Assert.Equal(attachmentsBeforeResume, fixture.ViewModel.SelectedConversation!.Attachments.ToArray());
                    Assert.Equal(transition is "reopen" or "switch-edit-other", fixture.ViewModel.IsEditingMessage);
                }
                Assert.Equal(otherMessagesBeforeResume, fixture.OtherConversation.Messages.ToArray());
                if (transition == "switch-edit-other")
                    Assert.Same(fixture.OtherConversation, fixture.ViewModel.SelectedConversation);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(context);
                if (send is { IsCompleted: false })
                    context.Complete(send);
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }, TimeSpan.FromSeconds(40), "The edit admission test did not finish.");
    }

    private sealed class Fixture : IDisposable
    {
        private static readonly FieldInfo SolutionInstance = typeof(SolutionManager).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? _previousSolution = SolutionInstance.GetValue(null);
        private readonly object _isolatedSolution = RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));
        private readonly string _root = Directory.CreateTempSubdirectory("CopilotMessageEditAdmission-").FullName;

        public Fixture()
        {
            SolutionInstance.SetValue(null, _isolatedSolution);
            var source = Path.Combine(_root, "source.png");
            CreateImage(source);
            StoragePath = Path.Combine(_root, "managed");
            var profile = new CopilotProfileConfig
            {
                Id = "edit-admission-profile", Name = "Edit admission profile",
                VendorType = CopilotVendorType.Custom, ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "edit-admission-test-key", BaseUrl = "https://example.test/v1",
                Model = "edit-admission-model", SupportsImageInput = true,
            };
            Conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
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
            OtherConversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
            OtherUser = new CopilotChatMessage(CopilotChatRole.User, "Other conversation request")
            {
                RequestMode = CopilotAgentMode.Auto,
                AttachmentSnapshotCaptured = true,
            };
            OtherConversation.Messages.Add(OtherUser);
            OtherConversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Other completed answer"));
            var state = new CopilotChatState
            {
                ActiveConversationId = Conversation.Id, ActiveProfileId = profile.Id,
                Conversations = [Conversation, OtherConversation],
            };
            var config = new CopilotConfig
            {
                SchemaVersion = CopilotConfig.CurrentSchemaVersion,
                McpBearerToken = "edit-admission-test-token", Profiles = [profile],
            };
            ViewModel = new CopilotChatViewModel(new CopilotChatService(), new MemoryStore(state, StoragePath), config, Runtime, Host);
        }

        public string StoragePath { get; }
        public CopilotConversationRecord Conversation { get; }
        public CopilotConversationRecord OtherConversation { get; }
        public CopilotChatMessage OriginalUser { get; }
        public CopilotChatMessage OriginalAssistant { get; }
        public CopilotChatMessage OtherUser { get; }
        public CopilotChatViewModel ViewModel { get; }
        public CopilotAgentTaskHost Host { get; } = new();
        public CompletingRuntime Runtime { get; } = new();

        public void Dispose()
        {
            Host.Shutdown();
            ViewModel.Dispose();
            if (ReferenceEquals(SolutionInstance.GetValue(null), _isolatedSolution))
                SolutionInstance.SetValue(null, _previousSolution);
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_root));
            var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            Assert.True(string.Equals(Path.GetDirectoryName(root), temp, StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(root).StartsWith("CopilotMessageEditAdmission-", StringComparison.Ordinal));
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class CompletingRuntime : ICopilotTurnRuntime
    {
        public ConcurrentQueue<CopilotTurnRequest> Requests { get; } = new();
        public async IAsyncEnumerable<CopilotTurnEvent> RunAsync(CopilotTurnRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Enqueue(request);
            var ledger = new CopilotAgentTaskLedgerSnapshot
            {
                Mode = "execute",
                Items = [new CopilotAgentTaskItem { Id = 1, Title = "Continue edited work", Description = "Preserve the new recovery point." }],
            };
            var journal = new CopilotAgentTaskEventJournalBuilder(request.TaskEventJournalBaseline);
            journal.RecordRunStarted();
            journal.RecordTaskLedger(ledger, "edit-admission-test");
            journal.RecordStop(CopilotAgentStopReason.Paused);
            var checkpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(CopilotAgentSessionCheckpoint.Create(request.Profile, "{}",
                CopilotCapabilityCatalog.Shared.GetSnapshot(), taskEventJournal: journal.Snapshot()));
            var result = new CopilotAgentRunResult
            {
                PreparedUserMessageContent = request.UserText, TaskLedger = ledger,
                StopReason = CopilotAgentStopReason.Paused, TaskEventJournal = journal.Snapshot(), SessionCheckpoint = checkpoint,
            };
            await Task.CompletedTask;
            yield return new CopilotTurnStartedEvent(request.TaskId, request.Mode);
            yield return new CopilotTurnPlanUpdatedEvent(CopilotTurnPlanSnapshot.FromTaskLedger(ledger));
            yield return new CopilotTurnAgentEvent(CopilotAgentEvent.CheckpointUpdated(checkpoint, ledger));
            yield return new CopilotTurnAgentEvent(CopilotAgentEvent.CheckpointReady());
            yield return new CopilotTurnAgentEvent(CopilotAgentEvent.AnswerDelta("The edited task paused with recoverable work."));
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
                Assert.True(remaining > TimeSpan.Zero, "The edited send did not finish after image admission.");
                Assert.NotEqual(WaitHandle.WaitTimeout, WaitHandle.WaitAny(waits, remaining));
            }
        }
        public void Dispose() => _posted.Dispose();
    }
}
