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
    [InlineData("context-retry")]
    [InlineData("context-refresh")]
    public void RetryRevalidatesItsOriginalTurnAfterImageAdmission(string transition)
    {
        StaTest.Run(() =>
        {
            using var fixture = new Fixture();
            using var context = new CopilotPausedAdmissionContext(
                TestTimeout,
                "Retry admission did not finish.");
            var previousContext = SynchronizationContext.Current;
            var hasContextTransition = transition is "context-retry" or "context-refresh";
            var refreshExternalContext = transition == "context-refresh";
            var contextSourceId = "retry-context:" + fixture.Conversation.Id;
            var previousLiveContext = hasContextTransition ? CopilotLiveContextRegistry.Current : null;
            CopilotAttachmentItem? originalContext = null;
            CopilotAttachmentItem? newerContext = null;
            Task? retry = null;
            try
            {
                if (hasContextTransition)
                {
                    var staged = fixture.ViewModel.QueueExternalPrompt(OriginalPrompt,
                        startNewConversation: false, sendNow: false,
                        contextAttachmentTitle: "Saved measurement snapshot",
                        contextAttachmentSourceId: contextSourceId,
                        contextAttachmentItems: [new CopilotContextItem { Title = "Measurement", Content = "Original measured result" }]);
                    Assert.True(staged.Accepted);
                    Assert.False(staged.WasSent);
                    originalContext = Assert.Single(fixture.Conversation.Attachments).CreateSnapshot();
                    fixture.OriginalUser.Attachments.Add(originalContext.CreateSnapshot());
                    CopilotLiveContextRegistry.Publish(new CopilotLiveContext
                    {
                        SourceId = contextSourceId,
                        Title = "Current measurement",
                        Summary = "Latest measurement summary",
                        AttachmentTitle = "Current measurement snapshot",
                        SnapshotItems = [new CopilotContextItem { Title = "Measurement", Content = "Latest measured result" }],
                    });
                }
                SynchronizationContext.SetSynchronizationContext(context);
                Assert.True(fixture.ViewModel.RetryMessageCommand.CanExecute(fixture.OriginalAssistant));
                retry = InvokeTask(fixture.ViewModel, "RetryMessageAsync", [fixture.OriginalAssistant, refreshExternalContext],
                    [typeof(CopilotChatMessage), typeof(bool)]);
                Assert.True(context.WaitForCallback(TestTimeout));
                Assert.False(retry.IsCompleted);
                Assert.False(fixture.ViewModel.IsBusy);
                Assert.Single(Directory.GetFiles(fixture.StoragePath, "image-*.png"));
                Assert.Empty(fixture.Runtime.Requests);

                // Keep the first admission continuation suspended while later UI work
                // completes through its own normal request path.
                SynchronizationContext.SetSynchronizationContext(previousContext);
                if (hasContextTransition)
                {
                    var staged = fixture.ViewModel.QueueExternalPrompt("Newer composer draft",
                        startNewConversation: false, sendNow: false,
                        contextAttachmentTitle: "Newer draft measurement snapshot",
                        contextAttachmentSourceId: contextSourceId,
                        contextAttachmentItems: [new CopilotContextItem { Title = "Measurement", Content = "Newer draft measured result" }]);
                    Assert.True(staged.Accepted);
                    Assert.False(staged.WasSent);
                    newerContext = Assert.Single(fixture.Conversation.Attachments);
                    Assert.NotEqual(originalContext!.Id, newerContext.Id);
                }
                else if (transition is "replacement" or "editing")
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

                if (transition is "unchanged" or "switch" or "switch-and-edit-other" or "context-retry" or "context-refresh")
                {
                    var request = Assert.Single(fixture.Runtime.Requests);
                    Assert.Equal(OriginalPrompt, request.UserText);
                    Assert.Equal(fixture.Conversation.Id, request.ConversationId);
                    Assert.Same(fixture.OriginalUser, fixture.Conversation.Messages[0]);
                    Assert.NotSame(fixture.OriginalAssistant, fixture.Conversation.Messages[1]);
                    Assert.Equal(2, fixture.Conversation.Messages.Count);
                    if (hasContextTransition)
                    {
                        Assert.Equal(refreshExternalContext, request.RefreshExternalContext);
                        var sentContext = Assert.Single(request.HostContext.Attachments,
                            attachment => attachment.Type == CopilotAttachmentType.Context);
                        Assert.Equal(originalContext!.Id, sentContext.Id);
                        Assert.DoesNotContain("Newer draft measured result", sentContext.Value, StringComparison.Ordinal);
                        var savedContext = Assert.Single(fixture.OriginalUser.Attachments,
                            attachment => attachment.Type == CopilotAttachmentType.Context);
                        Assert.Equal((originalContext.Id, originalContext.Title, originalContext.Value),
                            (savedContext.Id, savedContext.Title, savedContext.Value));
                        Assert.Equal(contextSourceId, request.HostContext.LiveContext?.SourceId);
                        Assert.Equal("Latest measurement summary", request.HostContext.LiveContext?.Summary);
                        Assert.Equal("Latest measured result", Assert.Single(request.HostContext.LiveContext!.SnapshotItems).Content);
                        Assert.Equal("Newer composer draft", fixture.ViewModel.InputText);
                        Assert.Same(newerContext, Assert.Single(fixture.Conversation.Attachments));
                        Assert.Contains("Newer draft measured result", newerContext!.Value, StringComparison.Ordinal);
                        if (refreshExternalContext)
                        {
                            Assert.Contains("Latest measured result", sentContext.Value, StringComparison.Ordinal);
                            Assert.DoesNotContain("Original measured result", sentContext.Value, StringComparison.Ordinal);
                        }
                        else
                        {
                            Assert.Equal(originalContext.Value, sentContext.Value);
                        }
                        Assert.Equal(refreshExternalContext ? "Current measurement snapshot" : originalContext.Title, sentContext.Title);
                    }
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
                try
                {
                    SynchronizationContext.SetSynchronizationContext(context);
                    if (retry is { IsCompleted: false })
                        context.Complete(retry);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previousContext);
                    if (hasContextTransition)
                    {
                        if (previousLiveContext == null)
                            CopilotLiveContextRegistry.Clear(contextSourceId);
                        else
                            CopilotLiveContextRegistry.Publish(previousLiveContext);
                    }
                }
            }
        }, TimeSpan.FromSeconds(40), "Retry source test did not finish.");
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

}
