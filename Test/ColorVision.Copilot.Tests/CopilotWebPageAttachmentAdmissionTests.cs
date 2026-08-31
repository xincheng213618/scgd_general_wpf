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
public sealed class CopilotWebPageAttachmentAdmissionTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    private const string PageUrl = "https://public.test/captured-page";
    private const string OtherPageUrl = "https://public.test/another-page";
    private const string OriginalBody = "The original webpage evidence captured for this request.";
    private const string UpdatedBody = "New webpage evidence fetched after the request was captured.";
    private const string Prompt = "Inspect the captured image and webpage evidence.";
    private const string NewerDraft = "A newer draft which must survive the earlier webpage fetch.";

    [Theory]
    [InlineData("same-url")]
    [InlineData("new-url")]
    [InlineData("unchanged")]
    public void RefreshDuringImageAdmissionPreservesTheCapturedPageAndTheNewDraftPage(string update)
    {
        RunOnSta(() =>
        {
            using var fixture = new Fixture(includeImage: true);
            using var context = new PausedOperationContext();
            var previousContext = SynchronizationContext.Current;
            Task? send = null;
            try
            {
                SynchronizationContext.SetSynchronizationContext(context);
                send = InvokeSend(fixture.ViewModel);
                Assert.True(context.WaitForCallback(TestTimeout));
                Assert.False(send.IsCompleted);
                Assert.False(fixture.ViewModel.IsBusy);
                Assert.Single(Directory.GetFiles(fixture.StoragePath, "image-*.png"));
                Assert.Empty(fixture.Runtime.Requests);

                if (update != "unchanged")
                {
                    var refreshedUrl = update == "new-url" ? OtherPageUrl : PageUrl;
                    var loaderCalls = 0;
                    fixture.ViewModel.AttachWebPageAsync(fixture.Conversation, refreshedUrl, (url, token) =>
                    {
                        token.ThrowIfCancellationRequested();
                        Assert.Equal(refreshedUrl, url);
                        loaderCalls++;
                        return Task.FromResult(CreateUpdatedPage(url));
                    }).GetAwaiter().GetResult();
                    Assert.Equal(1, loaderCalls);
                    Assert.Equal(Prompt, fixture.ViewModel.InputText);
                    Assert.False(fixture.ViewModel.IsBusy);
                }

                context.Complete(send);
                send.GetAwaiter().GetResult();

                var request = Assert.Single(fixture.Runtime.Requests);
                Assert.Equal(fixture.Conversation.Id, request.ConversationId);
                Assert.Equal(Prompt, request.UserText);
                var sentPage = Assert.Single(request.HostContext.Attachments, item => item.Type == CopilotAttachmentType.WebPage);
                Assert.Equal(PageUrl, sentPage.Source);
                Assert.Equal(OriginalBody, sentPage.Value);
                Assert.DoesNotContain(UpdatedBody, sentPage.Value, StringComparison.Ordinal);
                var userMessage = Assert.Single(fixture.Conversation.Messages, message => message.IsUser);
                var messagePage = Assert.Single(userMessage.Attachments, item => item.Type == CopilotAttachmentType.WebPage);
                Assert.Equal(sentPage.Source, messagePage.Source);
                Assert.Equal(sentPage.Value, messagePage.Value);
                Assert.True(userMessage.AttachmentSnapshotCaptured);
                Assert.Empty(fixture.ViewModel.InputText);

                if (update == "unchanged")
                {
                    Assert.Empty(fixture.Conversation.Attachments);
                }
                else
                {
                    var retainedPage = Assert.Single(fixture.Conversation.Attachments);
                    Assert.Equal(CopilotAttachmentType.WebPage, retainedPage.Type);
                    Assert.Equal(update == "new-url" ? OtherPageUrl : PageUrl, retainedPage.Source);
                    Assert.Contains(UpdatedBody, retainedPage.Value, StringComparison.Ordinal);
                    Assert.DoesNotContain(OriginalBody, retainedPage.Value, StringComparison.Ordinal);
                }
            }
            finally
            {
                if (send is { IsCompleted: false })
                    context.Complete(send);
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        });
    }

    [Theory]
    [InlineData("cancel-edit", true)]
    [InlineData("cancel-edit", false)]
    [InlineData("cancel-fetch", true)]
    [InlineData("close", true)]
    public void AFinishedFetchDoesNotRestoreAnAbandonedPageAttachment(string transition, bool pageInitiallyExists)
    {
        RunOnSta(() =>
        {
            using var fixture = new Fixture(editSource: transition == "cancel-edit", pageInitiallyExists: pageInitiallyExists);
            using var context = new PausedOperationContext();
            var response = new TaskCompletionSource<CopilotFetchedWebPageContent>(TaskCreationOptions.RunContinuationsAsynchronously);
            var previousContext = SynchronizationContext.Current;
            Task? fetch = null;
            CancellationToken fetchToken = default;
            try
            {
                if (transition == "cancel-edit")
                {
                    Assert.True(fixture.ViewModel.EditMessageCommand.CanExecute(fixture.OriginalUser));
                    fixture.ViewModel.EditMessageCommand.Execute(fixture.OriginalUser);
                    Assert.True(fixture.ViewModel.IsEditingMessage);
                }

                var originalPage = pageInitiallyExists ? Assert.Single(fixture.Conversation.Attachments) : null;
                if (!pageInitiallyExists)
                    Assert.Empty(fixture.Conversation.Attachments);
                SynchronizationContext.SetSynchronizationContext(context);
                fetch = fixture.ViewModel.AttachWebPageAsync(fixture.Conversation, PageUrl, (url, token) =>
                {
                    Assert.Equal(PageUrl, url);
                    fetchToken = token;
                    return response.Task;
                });
                Assert.False(fetch.IsCompleted);
                Assert.True(fixture.ViewModel.IsBusy);

                // Exercise actual UI commands, not direct removal from the owner collection.
                if (transition == "cancel-edit")
                {
                    if (originalPage != null)
                        Assert.False(fixture.ViewModel.RemoveAttachmentCommand.CanExecute(originalPage));
                    Assert.True(fixture.ViewModel.CancelMessageEditCommand.CanExecute(null));
                    fixture.ViewModel.CancelMessageEditCommand.Execute(null);
                    Assert.False(fixture.ViewModel.IsEditingMessage);
                    var queued = fixture.ViewModel.QueueExternalPrompt(NewerDraft, startNewConversation: false,
                        sendNow: false, mode: CopilotAgentMode.Chat,
                        contextAttachmentTitle: "Newer draft context", contextAttachmentSourceId: "newer-draft",
                        contextAttachmentItems: [new CopilotContextItem { Title = "Draft", Content = "Newer draft context content." }]);
                    Assert.True(queued.Accepted);
                }
                else
                {
                    fixture.ViewModel.InputText = NewerDraft;
                }

                var retainedAttachments = fixture.Conversation.Attachments.ToArray();
                var originalMessages = fixture.Conversation.Messages.ToArray();
                response.SetResult(CreateUpdatedPage(PageUrl));
                Assert.True(context.WaitForCallback(TestTimeout));
                Assert.False(fetch.IsCompleted);

                if (transition == "cancel-fetch")
                {
                    fixture.ViewModel.PrimaryActionCommand.Execute(null);
                    Assert.True(fetchToken.IsCancellationRequested);
                }
                else if (transition == "close")
                {
                    fixture.ViewModel.Dispose();
                    Assert.True(fetchToken.IsCancellationRequested);
                }

                context.Complete(fetch);
                fetch.GetAwaiter().GetResult();

                Assert.Equal(NewerDraft, fixture.ViewModel.InputText);
                Assert.Equal(retainedAttachments, fixture.Conversation.Attachments.ToArray());
                Assert.Equal(originalMessages, fixture.Conversation.Messages.ToArray());
                Assert.DoesNotContain(fixture.Conversation.Attachments, item => item.Value.Contains(UpdatedBody, StringComparison.Ordinal));
                Assert.Empty(fixture.Runtime.Requests);
                Assert.False(fixture.ViewModel.IsBusy);
                if (transition == "cancel-edit")
                {
                    Assert.Equal(CopilotAttachmentType.Context, Assert.Single(fixture.Conversation.Attachments).Type);
                    if (pageInitiallyExists)
                        Assert.Equal(OriginalBody, Assert.Single(fixture.OriginalUser!.Attachments).Value);
                    else
                        Assert.Empty(fixture.OriginalUser!.Attachments);
                }
                else
                {
                    var retainedPage = Assert.Single(fixture.Conversation.Attachments);
                    Assert.Same(originalPage, retainedPage);
                    Assert.Equal(OriginalBody, retainedPage.Value);
                }
            }
            finally
            {
                response.TrySetResult(CreateUpdatedPage(PageUrl));
                if (fetch is { IsCompleted: false })
                    context.Complete(fetch);
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        });
    }

    private static CopilotFetchedWebPageContent CreateUpdatedPage(string url) =>
        new(url, "Refreshed webpage", string.Empty, UpdatedBody);

    private static Task InvokeSend(CopilotChatViewModel viewModel) =>
        Assert.IsAssignableFrom<Task>(typeof(CopilotChatViewModel)
            .GetMethod("SendAsync", BindingFlags.Instance | BindingFlags.NonPublic, Type.EmptyTypes)!
            .Invoke(viewModel, null));

    private sealed class Fixture : IDisposable
    {
        private static readonly FieldInfo SolutionInstance = typeof(SolutionManager)
            .GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? _previousSolution = SolutionInstance.GetValue(null);
        private readonly object _isolatedSolution = RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));
        private readonly string _root = Directory.CreateTempSubdirectory("CopilotWebPageAdmission-").FullName;

        public Fixture(bool includeImage = false, bool editSource = false, bool pageInitiallyExists = true)
        {
            SolutionInstance.SetValue(null, _isolatedSolution);
            StoragePath = Path.Combine(_root, "attachments");
            var profile = new CopilotProfileConfig
            {
                Id = "webpage-admission-profile", Name = "Webpage admission profile",
                VendorType = CopilotVendorType.Custom, ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "webpage-admission-test-key", BaseUrl = "https://example.test/v1",
                Model = "webpage-admission-model", SupportsImageInput = true,
            };
            Conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
            Conversation.SetCustomTitle("Webpage admission fixture");
            var originalPage = CopilotAttachmentItem.CreateWebPage(PageUrl, "Captured webpage", OriginalBody);
            if (editSource)
            {
                OriginalUser = new CopilotChatMessage(CopilotChatRole.User, Prompt)
                {
                    RequestMode = CopilotAgentMode.Chat,
                    AttachmentSnapshotCaptured = true,
                    Attachments = pageInitiallyExists ? [originalPage] : [],
                };
                Conversation.Messages.Add(OriginalUser);
                Conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Original completed answer")
                {
                    RequestMode = CopilotAgentMode.Chat,
                });
            }
            else
            {
                Conversation.DraftText = Prompt;
                Conversation.DraftRequestMode = CopilotAgentMode.Chat;
                if (includeImage)
                {
                    var imagePath = Path.Combine(_root, "source.png");
                    CreateImage(imagePath);
                    Conversation.Attachments.Add(CopilotAttachmentItem.CreateImage(imagePath));
                }
                Conversation.Attachments.Add(originalPage);
            }

            var state = new CopilotChatState
            {
                ActiveConversationId = Conversation.Id, ActiveProfileId = profile.Id,
                Conversations = [Conversation],
            };
            var config = new CopilotConfig
            {
                SchemaVersion = CopilotConfig.CurrentSchemaVersion,
                McpBearerToken = "webpage-admission-test-token", Profiles = [profile],
            };
            ViewModel = new CopilotChatViewModel(new CopilotChatService(), new MemoryStore(state, StoragePath), config, Runtime, Host);
        }

        public string StoragePath { get; }
        public CopilotConversationRecord Conversation { get; }
        public CopilotChatMessage? OriginalUser { get; }
        public CopilotChatViewModel ViewModel { get; }
        public CopilotAgentTaskHost Host { get; } = new();
        public RecordingTurnRuntime Runtime { get; } = new();

        public void Dispose()
        {
            Host.Shutdown();
            ViewModel.Dispose();
            if (ReferenceEquals(SolutionInstance.GetValue(null), _isolatedSolution))
                SolutionInstance.SetValue(null, _previousSolution);
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_root));
            var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            Assert.True(string.Equals(Path.GetDirectoryName(root), temp, StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(root).StartsWith("CopilotWebPageAdmission-", StringComparison.Ordinal));
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class RecordingTurnRuntime : ICopilotTurnRuntime
    {
        public ConcurrentQueue<CopilotTurnRequest> Requests { get; } = new();

        public async IAsyncEnumerable<CopilotTurnEvent> RunAsync(CopilotTurnRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Enqueue(request);
            await Task.CompletedTask;
            yield return new CopilotTurnStartedEvent(request.TaskId, request.Mode);
            throw new InvalidOperationException("Expected webpage admission fixture stop; no provider was contacted.");
        }

        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message) => new(CopilotSteeringAdmissionReason.RuntimeUnavailable);
        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;
        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;
        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) => false;
        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(CopilotWorkspaceRollbackActionRequest request,
            Action<CopilotAgentEvent> onEvent, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class MemoryStore(CopilotChatState state, string attachmentPath) : ICopilotChatStateStore
    {
        public string AttachmentDirectoryPath => attachmentPath;
        public CopilotChatState Load() => state;
        public void Save(CopilotChatState value) { }
        public CopilotChatStateSnapshot CaptureSnapshot(CopilotChatState value) => new(new JObject());
        public string Serialize(CopilotChatStateSnapshot snapshot) => "{}";
        public string Serialize(CopilotChatState value) => "{}";
        public Task SaveSerializedAsync(string serializedState, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
        public int CleanupOrphanedAttachments(CopilotChatState value) => 0;
    }

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

    private sealed class PausedOperationContext : SynchronizationContext, IDisposable
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
                Assert.True(remaining > TimeSpan.Zero, "The webpage attachment operation did not finish after admission.");
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
        Assert.True(thread.Join(TimeSpan.FromSeconds(40)), "The webpage admission test did not finish.");
        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
