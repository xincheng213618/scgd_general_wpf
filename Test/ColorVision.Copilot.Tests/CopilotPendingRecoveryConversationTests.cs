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
public sealed class CopilotPendingRecoveryConversationTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData("select")]
    [InlineData("fork-command")]
    [InlineData("fork-message")]
    public async Task RecoveryBlockedByAttachmentsDoesNotFollowTheUserToAnotherConversation(string transition)
    {
        await using var fixture = new RecoveryFixture();
        fixture.BeginBlockedRecovery();

        if (transition == "select")
        {
            fixture.ViewModel.SelectConversationCommand.Execute(fixture.OtherConversation);
        }
        else if (transition == "fork-command")
        {
            fixture.ViewModel.InputText = "/fork";
            fixture.ViewModel.SendCommand.Execute(null);
        }
        else
        {
            Assert.True(fixture.ViewModel.BranchConversationCommand.CanExecute(fixture.Assistant));
            fixture.ViewModel.BranchConversationCommand.Execute(fixture.Assistant);
        }

        var target = fixture.ViewModel.SelectedConversation!;
        Assert.NotSame(fixture.Conversation, target);
        Assert.Null(target.AgentSessionCheckpoint);
        Assert.Empty(target.Attachments);
        fixture.ViewModel.InputText = "A new independent Agent request";
        fixture.ViewModel.SendCommand.Execute(null);
        var request = await fixture.Runtime.Entered.WaitAsync(TestTimeout);

        Assert.Equal(target.Id, request.ConversationId);
        Assert.Equal("A new independent Agent request", request.UserText);
        Assert.Null(request.Recovery);
        Assert.Null(target.Messages.Last(message => message.IsUser).RecoveryRequest);
        Assert.NotNull(fixture.Conversation.AgentSessionCheckpoint);
        Assert.Equal(fixture.AttachmentPath, Assert.Single(fixture.Conversation.Attachments).Value);
        Assert.True(File.Exists(fixture.AttachmentPath));
    }

    [Theory]
    [InlineData("same")]
    [InlineData("foreign")]
    [InlineData("archived")]
    public async Task RetryingTheSameConversationAfterAttachmentRejectionKeepsFinalAnswerRecovery(string selection)
    {
        await using var fixture = new RecoveryFixture();
        fixture.BeginBlockedRecovery();
        var target = selection switch
        {
            "foreign" => CopilotConversationRecord.CreateEmpty(fixture.Profile.Id, fixture.Profile.DisplayLabel),
            "archived" => fixture.OtherConversation,
            _ => fixture.Conversation,
        };
        if (selection == "archived")
            target.IsArchived = true;
        fixture.ViewModel.SelectConversationCommand.Execute(target);
        Assert.Same(fixture.Conversation, fixture.ViewModel.SelectedConversation);
        fixture.ViewModel.InputText = fixture.ViewModel.InputText;
        fixture.Conversation.Attachments.Clear();

        fixture.ViewModel.SendCommand.Execute(null);
        var request = await fixture.Runtime.Entered.WaitAsync(TestTimeout);

        Assert.Equal(fixture.Conversation.Id, request.ConversationId);
        Assert.Equal(CopilotAgentRecoveryPolicy.FinalizeUserMessage, request.UserText);
        Assert.Equal(CopilotAgentRecoveryMode.Finalize, request.Recovery?.Mode);
        Assert.True(request.Recovery?.PreviousResponseWasInterrupted);
        Assert.True(File.Exists(fixture.AttachmentPath));
    }

    [Fact]
    public async Task ReplacingTheBlockedRecoveryPromptDoesNotFinalizeThePreviousRequest()
    {
        await using var fixture = new RecoveryFixture();
        fixture.BeginBlockedRecovery();
        fixture.Conversation.Attachments.Clear();
        fixture.ViewModel.InputText = "Do a different Agent task";

        fixture.ViewModel.SendCommand.Execute(null);
        var request = await fixture.Runtime.Entered.WaitAsync(TestTimeout);

        Assert.Equal(fixture.Conversation.Id, request.ConversationId);
        Assert.Equal("Do a different Agent task", request.UserText);
        Assert.Null(request.Recovery);
        Assert.Null(fixture.Conversation.Messages.Last(message => message.IsUser).RecoveryRequest);
    }

    [Fact]
    public void RecoveryAlreadyInImageAdmissionKeepsItsCapturedConversationAfterSelectionChanges()
    {
        StaTest.Run(() =>
        {
            var fixture = new RecoveryFixture();
            using var context = new PausedSynchronizationContext();
            var previousContext = SynchronizationContext.Current;
            Task? sending = null;
            try
            {
                fixture.BeginBlockedRecovery();
                fixture.Profile.SupportsImageInput = true;
                var pixels = new byte[512 * 512 * 4];
                new Random(42).NextBytes(pixels);
                var image = BitmapSource.Create(512, 512, 96, 96, PixelFormats.Bgra32, null, pixels, 512 * 4);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                using (var stream = File.Create(fixture.AttachmentPath))
                    encoder.Save(stream);

                SynchronizationContext.SetSynchronizationContext(context);
                var send = typeof(CopilotChatViewModel).GetMethod("SendAsync", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null)!;
                sending = Assert.IsAssignableFrom<Task>(send.Invoke(fixture.ViewModel, null));
                Assert.True(context.CallbackPosted.Wait(TestTimeout), "The image admission continuation was not queued.");
                Assert.False(sending.IsCompleted);
                Assert.False(fixture.Runtime.Entered.IsCompleted);

                fixture.ViewModel.SelectConversationCommand.Execute(fixture.OtherConversation);
                fixture.ViewModel.InputText = "Keep the other conversation draft";
                context.RunPending();
                var request = fixture.Runtime.Entered.WaitAsync(TestTimeout).GetAwaiter().GetResult();

                Assert.Equal(fixture.Conversation.Id, request.ConversationId);
                Assert.Equal(CopilotAgentRecoveryPolicy.FinalizeUserMessage, request.UserText);
                Assert.Equal(CopilotAgentRecoveryMode.Finalize, request.Recovery?.Mode);
                Assert.Same(fixture.OtherConversation, fixture.ViewModel.SelectedConversation);
                Assert.Equal("Keep the other conversation draft", fixture.ViewModel.InputText);
                Assert.Empty(fixture.OtherConversation.Messages);
            }
            finally
            {
                fixture.Runtime.Release();
                try
                {
                    if (sending != null)
                    {
                        while (!sending.IsCompleted)
                        {
                            context.RunPending();
                            if (!sending.IsCompleted)
                                Assert.True(context.CallbackPosted.Wait(TestTimeout), "The send continuation did not finish.");
                        }
                        sending.GetAwaiter().GetResult();
                    }
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previousContext);
                    fixture.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
            }
        }, TimeSpan.FromSeconds(30), "The STA recovery admission test did not finish.");
    }

    private sealed class RecoveryFixture : IAsyncDisposable
    {
        private static readonly FieldInfo SolutionInstanceField = typeof(SolutionManager)
            .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? _previousSolutionInstance = SolutionInstanceField.GetValue(null);
        private readonly object _testSolutionInstance = RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));
        private readonly string _root = Path.Combine(Path.GetTempPath(), "CopilotPendingRecovery-" + Guid.NewGuid().ToString("N"));

        public CopilotProfileConfig Profile { get; } = new()
        {
            Id = "pending-recovery-profile",
            Name = "Pending recovery profile",
            VendorType = CopilotVendorType.Custom,
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "pending-recovery-test-key",
            BaseUrl = "https://unit.test/v1",
            Model = "test-model",
            MaxTokens = 4_096,
            SupportsImageInput = false,
        };
        public CopilotConversationRecord Conversation { get; }
        public CopilotConversationRecord OtherConversation { get; }
        public CopilotChatMessage Assistant { get; }
        public CopilotChatViewModel ViewModel { get; }
        public CopilotAgentTaskHost Host { get; } = new();
        public CapturingTurnRuntime Runtime { get; } = new();
        public string AttachmentPath { get; }

        public RecoveryFixture()
        {
            SolutionInstanceField.SetValue(null, _testSolutionInstance);
            Directory.CreateDirectory(_root);
            AttachmentPath = Path.Combine(_root, "draft.png");
            File.WriteAllBytes(AttachmentPath, Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aF9sAAAAASUVORK5CYII="));
            Conversation = CopilotConversationRecord.CreateEmpty(Profile.Id, Profile.DisplayLabel);
            Conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Original Agent request")
            {
                RequestMode = CopilotAgentMode.Auto,
            });
            Assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Partial final answer")
            {
                RequestMode = CopilotAgentMode.Auto,
                AgentStopReason = CopilotAgentStopReason.Completed,
                WasResponseInterrupted = true,
            };
            Conversation.Messages.Add(Assistant);
            var journal = new CopilotAgentTaskEventJournalBuilder();
            journal.RecordRunStarted();
            journal.RecordStop(CopilotAgentStopReason.Completed);
            Conversation.SetAgentSessionCheckpoint(CopilotAgentSessionCheckpoint.Create(
                Profile, "{}", CopilotCapabilityCatalog.Shared.GetSnapshot(), taskEventJournal: journal.Snapshot()));
            Conversation.Attachments.Add(CopilotAttachmentItem.CreateImage(AttachmentPath));
            OtherConversation = CopilotConversationRecord.CreateEmpty(Profile.Id, Profile.DisplayLabel);
            var state = new CopilotChatState
            {
                ActiveConversationId = Conversation.Id,
                ActiveProfileId = Profile.Id,
                Conversations = [Conversation, OtherConversation],
            };
            var config = new CopilotConfig
            {
                SchemaVersion = CopilotConfig.CurrentSchemaVersion,
                McpBearerToken = "pending-recovery-test-token",
                Profiles = [Profile],
            };
            ViewModel = new CopilotChatViewModel(new CopilotChatService(), new MemoryStateStore(state, _root), config, Runtime, Host);
        }

        public void BeginBlockedRecovery()
        {
            Assert.True(ViewModel.ContinueAgentTasksCommand.CanExecute(Assistant));
            ViewModel.ContinueAgentTasksCommand.Execute(Assistant);
            Assert.Equal("当前模型不支持图片", ViewModel.LocalCommandResultTitle);
            Assert.Equal(CopilotAgentRecoveryPolicy.FinalizeUserMessage, ViewModel.InputText);
            Assert.Null(Host.ActiveRun);
            Assert.False(Runtime.Entered.IsCompleted);
            Assert.Equal(2, Conversation.Messages.Count);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                Runtime.Release();
                if (Host.ActiveRun is { } active)
                    await active.Completion.WaitAsync(TestTimeout);
            }
            finally
            {
                Host.Shutdown();
                ViewModel.Dispose();
                if (ReferenceEquals(SolutionInstanceField.GetValue(null), _testSolutionInstance))
                    SolutionInstanceField.SetValue(null, _previousSolutionInstance);
                var fullRoot = Path.GetFullPath(_root);
                var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (fullRoot.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase)
                    && Path.GetFileName(fullRoot).StartsWith("CopilotPendingRecovery-", StringComparison.Ordinal))
                {
                    Directory.Delete(fullRoot, recursive: true);
                }
            }
        }
    }

    private sealed class MemoryStateStore(CopilotChatState state, string root) : ICopilotChatStateStore
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

    private sealed class CapturingTurnRuntime : ICopilotTurnRuntime
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<CopilotTurnRequest> _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<CopilotTurnRequest> Entered => _entered.Task;
        public void Release() => _release.TrySetResult();

        public async IAsyncEnumerable<CopilotTurnEvent> RunAsync(
            CopilotTurnRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            _entered.TrySetResult(request);
            await _release.Task.WaitAsync(cancellationToken);
            yield return new CopilotTurnStartedEvent("pending-recovery-test", request.Mode);
            throw new InvalidOperationException("Expected pending-recovery test failure.");
        }

        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message) => new(CopilotSteeringAdmissionReason.RuntimeUnavailable);
        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;
        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;
        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) => false;
        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(CopilotWorkspaceRollbackActionRequest request,
            Action<CopilotAgentEvent> onEvent, CancellationToken cancellationToken) => Task.FromException<CopilotWorkspaceRollbackActionResult>(new NotSupportedException());
    }

    private sealed class PausedSynchronizationContext : SynchronizationContext, IDisposable
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _callbacks = new();
        public ManualResetEventSlim CallbackPosted { get; } = new();
        public override void Post(SendOrPostCallback callback, object? state)
        {
            _callbacks.Enqueue((callback, state));
            CallbackPosted.Set();
        }
        public void RunPending()
        {
            CallbackPosted.Reset();
            while (_callbacks.TryDequeue(out var callback))
                callback.Callback(callback.State);
        }
        public void Dispose() => CallbackPosted.Dispose();
    }
}
