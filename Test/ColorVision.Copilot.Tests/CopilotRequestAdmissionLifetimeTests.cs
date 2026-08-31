using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ColorVision.Copilot;
using ColorVision.Solution;
using Newtonsoft.Json.Linq;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotRequestAdmissionLifetimeTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(false, "delete")]
    [InlineData(false, "archive")]
    [InlineData(false, "dispose")]
    [InlineData(false, "switch")]
    [InlineData(true, "delete")]
    [InlineData(true, "archive")]
    [InlineData(true, "dispose")]
    [InlineData(true, "switch")]
    public void CompletedImageAdmissionHonorsCapturedConversationLifetime(bool retry, string transition)
    {
        RunOnSta(() =>
        {
            var root = Directory.CreateTempSubdirectory("CopilotAdmissionLifetime-").FullName;
            var sourcePath = Path.Combine(root, "source.png");
            var storePath = Path.Combine(root, "attachments");
            CreateImage(sourcePath);
            var profile = new CopilotProfileConfig
            {
                Id = "image-profile",
                Name = "Image profile",
                VendorType = CopilotVendorType.Custom,
                ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "admission-lifetime-test-key",
                BaseUrl = "https://unit.test/v1",
                Model = "image-test-model",
                SupportsImageInput = true,
            };
            var origin = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
            origin.Id = "origin-conversation";
            origin.DraftText = "Inspect the captured image";
            origin.DraftRequestMode = CopilotAgentMode.Chat;
            var other = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
            other.Id = "other-conversation";
            other.DraftText = "Unrelated draft";
            var attachment = CopilotAttachmentItem.CreateImage(sourcePath);
            CopilotChatMessage? previousAssistant = null;
            if (retry)
            {
                origin.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, origin.DraftText)
                {
                    RequestMode = CopilotAgentMode.Chat,
                    Attachments = [attachment],
                    AttachmentSnapshotCaptured = true,
                });
                previousAssistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Previous answer");
                origin.Messages.Add(previousAssistant);
            }
            else
            {
                origin.Attachments.Add(attachment);
            }
            var config = new CopilotConfig
            {
                SchemaVersion = CopilotConfig.CurrentSchemaVersion,
                McpBearerToken = "admission-lifetime-test-token",
                Profiles = [profile],
            };
            var state = new CopilotChatState
            {
                ActiveConversationId = origin.Id,
                ActiveProfileId = profile.Id,
                Conversations = [origin, other],
            };
            using var solutionScope = new IsolatedSolutionManagerScope();
            var runtime = new RecordingTurnRuntime();
            var taskHost = new CopilotAgentTaskHost();
            using var viewModel = new CopilotChatViewModel(
                new CopilotChatService(), new InMemoryStateStore(state, storePath), config, runtime, taskHost);
            using var context = new PausedAdmissionSynchronizationContext();
            var previousContext = SynchronizationContext.Current;
            Task? operation = null;
            try
            {
                SynchronizationContext.SetSynchronizationContext(context);
                operation = retry
                    ? InvokeTask(viewModel, "RetryMessageAsync", [previousAssistant, true],
                        [typeof(CopilotChatMessage), typeof(bool)])
                    : InvokeTask(viewModel, "SendAsync", [], Type.EmptyTypes);

                // This is the actual admission continuation: the source has been read,
                // validated, and copied by PersistAsync, but the UI has not resumed yet.
                Assert.True(context.WaitForCallback(TestTimeout), "Image admission did not post its UI continuation.");
                Assert.False(operation.IsCompleted);
                var storedPath = Assert.Single(Directory.GetFiles(storePath, "image-*.png"));
                Assert.True(File.ReadAllBytes(sourcePath).SequenceEqual(File.ReadAllBytes(storedPath)));
                Assert.Empty(runtime.Requests);
                Assert.False(viewModel.IsBusy);
                Assert.True(viewModel.DeleteConversationCommand.CanExecute(origin));
                var messagesBeforeTransition = origin.Messages.ToArray();

                if (transition == "dispose")
                {
                    viewModel.Dispose();
                }
                else
                {
                    if (transition == "delete")
                        Assert.True(viewModel.Conversations.Remove(origin));
                    else if (transition == "archive")
                        origin.IsArchived = true;
                    Assert.True(viewModel.TrySelectConversation(other.Id));
                }

                var messageMutationsAfterTransition = 0;
                origin.Messages.CollectionChanged += (_, _) => messageMutationsAfterTransition++;
                context.Complete(operation);
                operation.GetAwaiter().GetResult();

                if (transition == "switch")
                {
                    var request = Assert.Single(runtime.Requests);
                    Assert.Equal(origin.Id, request.ConversationId);
                    Assert.Equal("Inspect the captured image", request.UserText);
                    Assert.Equal(storedPath, Assert.Single(request.HostContext.Attachments).Value);
                    Assert.True(messageMutationsAfterTransition > 0);
                    Assert.Equal(2, origin.Messages.Count);
                    Assert.Same(other, viewModel.SelectedConversation);
                    Assert.Equal("Unrelated draft", viewModel.InputText);
                }
                else
                {
                    Assert.Empty(runtime.Requests);
                    Assert.Equal(0, messageMutationsAfterTransition);
                    Assert.Equal(messagesBeforeTransition, origin.Messages.ToArray());
                }
                Assert.Empty(other.Messages);
                Assert.Equal("Unrelated draft", other.DraftText);
                Assert.Null(taskHost.ActiveRun);
                Assert.Empty(taskHost.QueuedRuns);
            }
            finally
            {
                if (operation != null && !operation.IsCompleted)
                    context.Complete(operation);
                SynchronizationContext.SetSynchronizationContext(previousContext);
                viewModel.Dispose();
                var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
                var tempRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
                Assert.True(
                    string.Equals(Path.GetDirectoryName(fullRoot), tempRoot, StringComparison.OrdinalIgnoreCase)
                    && Path.GetFileName(fullRoot).StartsWith("CopilotAdmissionLifetime-", StringComparison.Ordinal),
                    "Refusing to delete a directory outside this test's temporary root.");
                Directory.Delete(fullRoot, recursive: true);
            }
        });
    }

    private static Task InvokeTask(
        CopilotChatViewModel viewModel,
        string name,
        object?[] arguments,
        Type[] parameterTypes)
    {
        var method = typeof(CopilotChatViewModel).GetMethod(
            name, BindingFlags.Instance | BindingFlags.NonPublic, parameterTypes);
        Assert.NotNull(method);
        return Assert.IsAssignableFrom<Task>(method.Invoke(viewModel, arguments));
    }

    private static void CreateImage(string path)
    {
        // A real, bounded PNG keeps the asynchronous disk admission observable without
        // sleeps, fake persistence gates, or changing the production admission service.
        const int dimension = 1_024;
        var pixels = new byte[dimension * dimension * 4];
        new Random(42).NextBytes(pixels);
        var image = BitmapSource.Create(
            dimension, dimension, 96, 96, PixelFormats.Bgra32, null, pixels, dimension * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(path);
        encoder.Save(stream);
        Assert.InRange(stream.Length, 1, CopilotImagePayloadLoader.MaximumImageBytes);
    }

    private sealed class RecordingTurnRuntime : ICopilotTurnRuntime
    {
        public ConcurrentQueue<CopilotTurnRequest> Requests { get; } = new();

        public async IAsyncEnumerable<CopilotTurnEvent> RunAsync(
            CopilotTurnRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Enqueue(request);
            await Task.CompletedTask;
            yield return new CopilotTurnStartedEvent(request.TaskId, request.Mode);
            throw new InvalidOperationException("Expected admission-lifetime fixture failure.");
        }

        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message) =>
            new(CopilotSteeringAdmissionReason.RuntimeUnavailable);

        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;

        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;

        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) => false;

        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(
            CopilotWorkspaceRollbackActionRequest request,
            Action<CopilotAgentEvent> onEvent,
            CancellationToken cancellationToken) =>
            Task.FromException<CopilotWorkspaceRollbackActionResult>(new NotSupportedException());
    }

    private sealed class InMemoryStateStore(CopilotChatState state, string attachmentDirectoryPath)
        : ICopilotChatStateStore
    {
        public string AttachmentDirectoryPath => attachmentDirectoryPath;

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

    private sealed class IsolatedSolutionManagerScope : IDisposable
    {
        private static readonly FieldInfo InstanceField = typeof(SolutionManager).GetField(
            "_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? _previous = InstanceField.GetValue(null);
        private readonly SolutionManager _replacement =
            (SolutionManager)RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));

        public IsolatedSolutionManagerScope() => InstanceField.SetValue(null, _replacement);

        public void Dispose()
        {
            if (ReferenceEquals(InstanceField.GetValue(null), _replacement))
                InstanceField.SetValue(null, _previous);
        }
    }

    private sealed class PausedAdmissionSynchronizationContext : SynchronizationContext, IDisposable
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _callbacks = new();
        private readonly AutoResetEvent _callbackPosted = new(false);

        public override void Post(SendOrPostCallback callback, object? state)
        {
            _callbacks.Enqueue((callback, state));
            _callbackPosted.Set();
        }

        public bool WaitForCallback(TimeSpan timeout) => _callbackPosted.WaitOne(timeout);

        public void Complete(Task operation)
        {
            var deadline = DateTime.UtcNow + TestTimeout;
            var waits = new[] { _callbackPosted, ((IAsyncResult)operation).AsyncWaitHandle };
            while (!operation.IsCompleted)
            {
                if (_callbacks.TryDequeue(out var callback))
                {
                    callback.Callback(callback.State);
                    continue;
                }
                var remaining = deadline - DateTime.UtcNow;
                Assert.True(remaining > TimeSpan.Zero, "The image-admission operation did not finish.");
                Assert.NotEqual(WaitHandle.WaitTimeout, WaitHandle.WaitAny(waits, remaining));
            }
        }

        public void Dispose() => _callbackPosted.Dispose();
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
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "The admission-lifetime STA test did not finish.");
        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
