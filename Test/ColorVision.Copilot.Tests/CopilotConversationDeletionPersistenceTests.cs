using ColorVision.Copilot;
using ColorVision.Solution;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotConversationDeletionPersistenceTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConfirmedDeletionKeepsImagesUntilTheRemovedConversationIsPersisted(bool selected)
    {
        await using var fixture = new DeletionFixture(selected);
        await fixture.FlushAsync();
        var gate = fixture.Store.BlockSnapshotWithoutConversation(fixture.Target.Id);

        var deleting = fixture.DeleteAsync();
        await gate.Entered.WaitAsync(TestTimeout);

        Assert.True(File.Exists(fixture.ExclusivePath), "The disk snapshot still references this image while its replacement is pending.");
        Assert.Equal(fixture.ImageBytes, File.ReadAllBytes(fixture.ExclusivePath));
        Assert.Contains(fixture.DiskStore.Load().Conversations, conversation => conversation.Id == fixture.Target.Id);
        Assert.False(deleting.IsCompleted, "Deletion must not report success before the state save commits.");

        gate.Release(succeed: true);
        Assert.True(await deleting.WaitAsync(TestTimeout));
        Assert.False(File.Exists(fixture.ExclusivePath));
        Assert.True(File.Exists(fixture.SharedPath));
    }

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public async Task FailedDeletionRestoresTheConversationAndSelectionWithoutDestroyingItsImages(
        bool selected, bool hasOtherConversation, bool archivedOtherBeforeTarget)
    {
        await using var fixture = new DeletionFixture(selected, hasOtherConversation);
        if (archivedOtherBeforeTarget)
        {
            fixture.Other!.IsArchived = true;
            fixture.ViewModel.Conversations.Move(fixture.ViewModel.Conversations.IndexOf(fixture.Other), 0);
        }
        await fixture.FlushAsync();
        var originalSelection = fixture.ViewModel.SelectedConversation;
        var originalIds = fixture.ViewModel.Conversations.Select(conversation => conversation.Id).ToArray();
        var originalIndex = fixture.ViewModel.Conversations.IndexOf(fixture.Target);
        var gate = fixture.Store.BlockSnapshotWithoutConversation(fixture.Target.Id);

        var deleting = fixture.DeleteAsync();
        await gate.Entered.WaitAsync(TestTimeout);
        gate.Release(succeed: false);

        Assert.False(await deleting.WaitAsync(TestTimeout));
        Assert.Same(fixture.Target, fixture.ViewModel.Conversations[originalIndex]);
        Assert.Equal(originalIds, fixture.ViewModel.Conversations.Select(conversation => conversation.Id));
        Assert.Same(originalSelection, fixture.ViewModel.SelectedConversation);
        Assert.Equal(originalSelection!.DraftText, fixture.ViewModel.InputText);
        Assert.Equal(fixture.ImageBytes, File.ReadAllBytes(fixture.ExclusivePath));
        Assert.Equal(fixture.ImageBytes, File.ReadAllBytes(fixture.SharedPath));
        var persisted = fixture.DiskStore.Load();
        Assert.Contains(persisted.Conversations, conversation => conversation.Id == fixture.Target.Id
            && conversation.Attachments.Any(attachment => attachment.Value == fixture.ExclusivePath));
        Assert.Equal(originalSelection.Id, persisted.ActiveConversationId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SuccessfulDeletionRemovesOnlyImagesWithoutAnotherConversationOwner(bool selected)
    {
        await using var fixture = new DeletionFixture(selected);
        await fixture.FlushAsync();

        Assert.True(await fixture.DeleteAsync().WaitAsync(TestTimeout));
        await fixture.FlushAsync();

        Assert.DoesNotContain(fixture.Target, fixture.ViewModel.Conversations);
        Assert.Same(fixture.Other, fixture.ViewModel.SelectedConversation);
        var persisted = fixture.DiskStore.Load();
        Assert.DoesNotContain(persisted.Conversations, conversation => conversation.Id == fixture.Target.Id);
        Assert.Contains(persisted.Conversations, conversation => conversation.Id == fixture.Other!.Id
            && conversation.Attachments.Any(attachment => attachment.Value == fixture.SharedPath));
        Assert.False(File.Exists(fixture.ExclusivePath));
        Assert.Equal(fixture.ImageBytes, File.ReadAllBytes(fixture.SharedPath));
    }

    [Theory]
    [InlineData("active-goal")]
    [InlineData("running-request")]
    public async Task ConfirmedDeletionRechecksRetentionChangedWhileTheDialogWasOpen(string change)
    {
        await using var fixture = new DeletionFixture();
        await fixture.FlushAsync();
        Assert.True(fixture.ViewModel.DeleteConversationCommand.CanExecute(fixture.Target));

        // The native dialog may pump the UI dispatcher. Change the real state
        // after initial admission, then enter the same confirmation continuation.
        if (change == "active-goal")
            fixture.Target.Goal = CopilotConversationGoal.Create("Keep this task", DateTimeOffset.UtcNow);
        else
            fixture.StartGatedRun();

        Assert.False(await fixture.DeleteAsync().WaitAsync(TestTimeout));

        Assert.Contains(fixture.Target, fixture.ViewModel.Conversations);
        Assert.Same(fixture.Target, fixture.ViewModel.SelectedConversation);
        Assert.Equal(fixture.ImageBytes, File.ReadAllBytes(fixture.ExclusivePath));
        Assert.Contains(fixture.DiskStore.Load().Conversations, conversation => conversation.Id == fixture.Target.Id);
        if (change == "running-request")
            Assert.NotNull(fixture.Host.FindRunByConversationId(fixture.Target.Id));
    }

    [Fact]
    public async Task RemovingADraftImageKeepsItsFileWhileTheNewStateIsPending()
    {
        await using var fixture = new DeletionFixture();
        await fixture.FlushAsync();
        var attachment = fixture.Target.Attachments.Single(item => item.Value == fixture.ExclusivePath);
        Assert.True(fixture.ViewModel.RemoveAttachmentCommand.CanExecute(attachment));
        var gate = fixture.Store.BlockSnapshotWithoutAttachment(fixture.Target.Id, fixture.ExclusivePath);

        var removing = fixture.RemoveAttachmentAsync(attachment);
        await gate.Entered.WaitAsync(TestTimeout);

        Assert.Equal(fixture.ImageBytes, File.ReadAllBytes(fixture.ExclusivePath));
        Assert.Contains(fixture.DiskStore.Load().Conversations.Single(conversation => conversation.Id == fixture.Target.Id).Attachments,
            item => item.Value == fixture.ExclusivePath);
        Assert.False(removing.IsCompleted);

        gate.Release(succeed: true);
        await removing.WaitAsync(TestTimeout);
        Assert.False(File.Exists(fixture.ExclusivePath));
    }

    [Fact]
    public async Task FailedDraftImageRemovalRestoresTheAttachmentAtItsOriginalPosition()
    {
        await using var fixture = new DeletionFixture();
        await fixture.FlushAsync();
        var attachment = fixture.Target.Attachments.Single(item => item.Value == fixture.ExclusivePath);
        var originalIndex = fixture.Target.Attachments.IndexOf(attachment);
        var gate = fixture.Store.BlockSnapshotWithoutAttachment(fixture.Target.Id, fixture.ExclusivePath);

        var removing = fixture.RemoveAttachmentAsync(attachment);
        await gate.Entered.WaitAsync(TestTimeout);
        gate.Release(succeed: false);
        var failure = await Record.ExceptionAsync(() => removing.WaitAsync(TestTimeout));

        Assert.IsType<IOException>(failure);
        Assert.Same(attachment, fixture.Target.Attachments[originalIndex]);
        Assert.Equal(fixture.ImageBytes, File.ReadAllBytes(fixture.ExclusivePath));
        Assert.Contains(fixture.DiskStore.Load().Conversations.Single(conversation => conversation.Id == fixture.Target.Id).Attachments,
            item => item.Value == fixture.ExclusivePath);
    }

    [Fact]
    public async Task FailedDraftImageRemovalRestoresItsOwnerWhileADeletionTemporarilyDetachesIt()
    {
        await using var fixture = new DeletionFixture();
        await fixture.FlushAsync();
        var attachment = fixture.Target.Attachments.Single(item => item.Value == fixture.ExclusivePath);
        var originalIndex = fixture.Target.Attachments.IndexOf(attachment);
        var gate = fixture.Store.BlockSnapshotWithoutAttachment(fixture.Target.Id, fixture.ExclusivePath, includeMissingOwner: true);
        var removing = fixture.RemoveAttachmentAsync(attachment);
        await gate.Entered.WaitAsync(TestTimeout);

        // The conversation deletion transaction detaches this same owner while
        // its own save is pending. It can later need to restore the entire draft.
        Assert.True(fixture.ViewModel.Conversations.Remove(fixture.Target));
        try
        {
            gate.Release(succeed: false);
            var failure = await Record.ExceptionAsync(() => removing.WaitAsync(TestTimeout));

            Assert.IsType<IOException>(failure);
            Assert.Same(attachment, fixture.Target.Attachments[originalIndex]);
            Assert.DoesNotContain(fixture.Target, fixture.ViewModel.Conversations);
            Assert.Equal(fixture.ImageBytes, File.ReadAllBytes(fixture.ExclusivePath));
            Assert.Contains(fixture.DiskStore.Load().Conversations.Single(conversation => conversation.Id == fixture.Target.Id).Attachments,
                item => item.Value == fixture.ExclusivePath);
        }
        finally
        {
            fixture.ViewModel.Conversations.Insert(0, fixture.Target);
        }
    }

    [Fact]
    public async Task SuccessfulDraftImageRemovalDeletesExclusiveFilesButPreservesSharedFiles()
    {
        await using var fixture = new DeletionFixture();
        await fixture.FlushAsync();

        await fixture.RemoveAttachmentAsync(fixture.Target.Attachments.Single(item => item.Value == fixture.ExclusivePath)).WaitAsync(TestTimeout);
        await fixture.RemoveAttachmentAsync(fixture.Target.Attachments.Single(item => item.Value == fixture.SharedPath)).WaitAsync(TestTimeout);
        await fixture.FlushAsync();

        Assert.Empty(fixture.Target.Attachments);
        Assert.False(File.Exists(fixture.ExclusivePath));
        Assert.Equal(fixture.ImageBytes, File.ReadAllBytes(fixture.SharedPath));
        var persisted = fixture.DiskStore.Load();
        Assert.Empty(persisted.Conversations.Single(conversation => conversation.Id == fixture.Target.Id).Attachments);
        Assert.Contains(persisted.Conversations.Single(conversation => conversation.Id == fixture.Other!.Id).Attachments,
            item => item.Value == fixture.SharedPath);
    }

    private sealed class DeletionFixture : IAsyncDisposable
    {
        private static readonly FieldInfo SolutionInstanceField = typeof(SolutionManager)
            .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? _previousSolutionInstance = SolutionInstanceField.GetValue(null);
        private readonly object _testSolutionInstance = RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));
        private readonly string _root = Path.Combine(Path.GetTempPath(), "CopilotDeletionPersistence-" + Guid.NewGuid().ToString("N"));
        private readonly TaskCompletionSource _releaseRun = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<Task> _operations = [];

        public byte[] ImageBytes { get; } = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aF9sAAAAASUVORK5CYII=");
        public CopilotChatStateStore DiskStore { get; }
        public GatedStateStore Store { get; }
        public CopilotChatViewModel ViewModel { get; }
        public CopilotConversationRecord Target { get; }
        public CopilotConversationRecord? Other { get; }
        public CopilotAgentTaskHost Host { get; } = new();
        public string ExclusivePath { get; }
        public string SharedPath { get; }

        public DeletionFixture(bool selected = true, bool hasOtherConversation = true)
        {
            SolutionInstanceField.SetValue(null, _testSolutionInstance);
            DiskStore = new CopilotChatStateStore(_root);
            Directory.CreateDirectory(DiskStore.AttachmentDirectoryPath);
            ExclusivePath = Path.Combine(DiskStore.AttachmentDirectoryPath, "exclusive.png");
            SharedPath = Path.Combine(DiskStore.AttachmentDirectoryPath, "shared.png");
            File.WriteAllBytes(ExclusivePath, ImageBytes);
            File.WriteAllBytes(SharedPath, ImageBytes);
            var profile = new CopilotProfileConfig
            {
                Id = "deletion-test-profile", Name = "Deletion test profile",
                VendorType = CopilotVendorType.Custom, ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "deletion-test-key", BaseUrl = "https://unit.test/v1", Model = "test-model", MaxTokens = 4_096,
            };
            var target = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
            target.Title = "Delete this conversation";
            target.DraftText = "Preserve my draft until deletion is saved";
            target.Attachments.Add(CopilotAttachmentItem.CreateImage(ExclusivePath));
            target.Attachments.Add(CopilotAttachmentItem.CreateImage(SharedPath));
            var other = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
            other.Title = "Keep this conversation";
            other.DraftText = "Keep the other draft";
            other.Attachments.Add(CopilotAttachmentItem.CreateImage(SharedPath));
            DiskStore.Save(new CopilotChatState
            {
                ActiveConversationId = selected ? target.Id : other.Id,
                ActiveProfileId = profile.Id,
                Conversations = hasOtherConversation ? [target, other] : [target],
            });
            Store = new GatedStateStore(DiskStore);
            var config = new CopilotConfig
            {
                SchemaVersion = CopilotConfig.CurrentSchemaVersion,
                McpBearerToken = "deletion-test-token",
                Profiles = [profile],
            };
            ViewModel = new CopilotChatViewModel(new CopilotChatService(), Store, config, new UnusedTurnRuntime(), Host);
            Target = ViewModel.Conversations.Single(conversation => conversation.Id == target.Id);
            Other = ViewModel.Conversations.SingleOrDefault(conversation => conversation.Id == other.Id);
        }

        public Task FlushAsync() => InvokeTask("FlushStatePersistenceBarrierAsync").WaitAsync(TestTimeout);

        public Task<bool> DeleteAsync()
        {
            var task = DeleteCoreAsync();
            _operations.Add(task);
            return task;
        }

        private async Task<bool> DeleteCoreAsync()
        {
            var task = InvokeTask("DeleteConfirmedConversationAsync", Target);
            await task;
            var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
            return (bool)result.GetType().GetProperty("Deleted")!.GetValue(result)!;
        }

        public Task RemoveAttachmentAsync(CopilotAttachmentItem attachment)
        {
            // Exercise the command's actual handler without async-void error
            // reporting. The pre-fix handler returns void; null is completed work.
            var task = InvokeTask("RemoveAttachment", attachment);
            _operations.Add(task);
            return task;
        }

        private Task InvokeTask(string method, params object?[] arguments) =>
            typeof(CopilotChatViewModel).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(ViewModel, arguments) as Task ?? Task.CompletedTask;

        public void StartGatedRun() => Host.Start(Target.Id, CopilotAgentMode.Auto, _ => _releaseRun.Task);

        public async ValueTask DisposeAsync()
        {
            try
            {
                Store.ReleasePendingSave();
                _releaseRun.TrySetResult();
                foreach (var operation in _operations)
                    _ = await Record.ExceptionAsync(() => operation.WaitAsync(TestTimeout));
                if (Host.ActiveRun is { } active)
                    await active.Completion.WaitAsync(TestTimeout);
                await FlushAsync();
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
                    && Path.GetFileName(fullRoot).StartsWith("CopilotDeletionPersistence-", StringComparison.Ordinal))
                {
                    Directory.Delete(fullRoot, recursive: true);
                }
            }
        }
    }

    private sealed class GatedStateStore(CopilotChatStateStore inner) : ICopilotChatStateStore
    {
        private SaveGate? _gate;
        public string AttachmentDirectoryPath => inner.AttachmentDirectoryPath;
        public CopilotChatState Load() => inner.Load();
        public void Save(CopilotChatState state) => inner.Save(state);
        public CopilotChatStateSnapshot CaptureSnapshot(CopilotChatState state) => inner.CaptureSnapshot(state);
        public string Serialize(CopilotChatStateSnapshot snapshot) => inner.Serialize(snapshot);
        public string Serialize(CopilotChatState state) => inner.Serialize(state);
        public int CleanupOrphanedAttachments(CopilotChatState state) => inner.CleanupOrphanedAttachments(state);

        public SaveGate BlockSnapshotWithoutConversation(string conversationId) => Arm(
            snapshot => FindConversation(snapshot, conversationId) == null);

        public SaveGate BlockSnapshotWithoutAttachment(string conversationId, string path, bool includeMissingOwner = false) => Arm(snapshot =>
        {
            var conversation = FindConversation(snapshot, conversationId);
            if (conversation == null)
                return includeMissingOwner;
            return !(conversation[nameof(CopilotConversationRecord.Attachments)]?.Children()
                .Any(attachment => attachment[nameof(CopilotAttachmentItem.Value)]?.Value<string>() == path) ?? false);
        });

        private SaveGate Arm(Func<JObject, bool> predicate)
        {
            var gate = new SaveGate(predicate);
            Volatile.Write(ref _gate, gate);
            return gate;
        }

        private static JToken? FindConversation(JObject snapshot, string id) =>
            snapshot[nameof(CopilotChatState.Conversations)]?.Children()
                .SingleOrDefault(conversation => conversation[nameof(CopilotConversationRecord.Id)]?.Value<string>() == id);

        public async Task SaveSerializedAsync(string serializedState, CancellationToken cancellationToken = default)
        {
            var gate = Volatile.Read(ref _gate);
            if (gate != null && gate.Matches(JObject.Parse(serializedState)))
            {
                gate.SignalEntered();
                if (!await gate.Outcome.WaitAsync(cancellationToken).ConfigureAwait(false))
                    throw new IOException("Controlled state commit failure.");
            }
            await inner.SaveSerializedAsync(serializedState, cancellationToken).ConfigureAwait(false);
        }

        public void ReleasePendingSave() => Interlocked.Exchange(ref _gate, null)?.Release(succeed: true);
    }

    private sealed class SaveGate(Func<JObject, bool> matches)
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _outcome = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Entered => _entered.Task;
        public Task<bool> Outcome => _outcome.Task;
        public bool Matches(JObject snapshot) => matches(snapshot);
        public void SignalEntered() => _entered.TrySetResult();
        public void Release(bool succeed) => _outcome.TrySetResult(succeed);
    }

    private sealed class UnusedTurnRuntime : ICopilotTurnRuntime
    {
        public IAsyncEnumerable<CopilotTurnEvent> RunAsync(CopilotTurnRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Deletion tests must not invoke a model.");
        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message) => new(CopilotSteeringAdmissionReason.RuntimeUnavailable);
        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;
        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;
        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) => false;
        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(CopilotWorkspaceRollbackActionRequest request,
            Action<CopilotAgentEvent> onEvent, CancellationToken cancellationToken) => Task.FromException<CopilotWorkspaceRollbackActionResult>(new NotSupportedException());
    }
}
