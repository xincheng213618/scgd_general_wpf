using ColorVision.Copilot;
using ColorVision.Solution;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotAttachmentRemovalEditLifetimeTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData("cancel")]
    [InlineData("reopen")]
    [InlineData("same-edit")]
    public async Task FailedRemovalRestoresOnlyTheEditSessionThatRemovedTheImage(string transition)
    {
        await using var fixture = new Fixture();
        fixture.BeginEdit();
        var removed = fixture.Target.Attachments.Single(item => item.Type == CopilotAttachmentType.Image);
        var originalIndex = fixture.Target.Attachments.IndexOf(removed);
        await fixture.FlushAsync();
        var gate = fixture.Store.BlockSaves();

        var removing = fixture.RemoveAsync(removed);
        await gate.Entered.WaitAsync(TestTimeout);
        Assert.False(removing.IsCompleted);
        Assert.Equal(fixture.ImageBytes, File.ReadAllBytes(fixture.ImagePath));

        if (transition != "same-edit")
        {
            Assert.True(fixture.ViewModel.CancelMessageEditCommand.CanExecute(null));
            fixture.ViewModel.CancelMessageEditCommand.Execute(null);
            Assert.Empty(fixture.Target.Attachments);
            if (transition == "reopen")
                fixture.BeginEdit();
        }
        fixture.ViewModel.InputText = "Newer text after the removal started";
        var expected = fixture.Target.Attachments.ToList();
        if (transition == "same-edit")
            expected.Insert(originalIndex, removed);

        gate.Release(succeed: false);
        Assert.IsType<IOException>(await Record.ExceptionAsync(() => removing.WaitAsync(TestTimeout)));

        Assert.Equal(transition != "cancel", fixture.ViewModel.IsEditingMessage);
        Assert.Equal("Newer text after the removal started", fixture.ViewModel.InputText);
        Assert.Equal("Newer text after the removal started", fixture.Target.DraftText);
        Assert.Equal(expected.Count, fixture.Target.Attachments.Count);
        for (var index = 0; index < expected.Count; index++)
            Assert.Same(expected[index], fixture.Target.Attachments[index]);
        Assert.Single(fixture.OriginalUser.Attachments, item => item.Value == fixture.ImagePath);
        Assert.Equal(fixture.ImageBytes, File.ReadAllBytes(fixture.ImagePath));
        Assert.Equal(2, fixture.Target.Messages.Count);
    }

    [Fact]
    public async Task FailedOrdinaryDraftRemovalRestoresItsOriginalOwnerAfterConversationSwitch()
    {
        await using var fixture = new Fixture();
        fixture.ViewModel.InputText = "Original ordinary draft";
        foreach (var item in fixture.OriginalUser.Attachments)
            fixture.Target.Attachments.Add(item.CreateSnapshot());
        var expected = fixture.Target.Attachments.ToArray();
        var removed = expected.Single(item => item.Type == CopilotAttachmentType.Image);
        await fixture.FlushAsync();
        var gate = fixture.Store.BlockSaves();

        var removing = fixture.RemoveAsync(removed);
        await gate.Entered.WaitAsync(TestTimeout);
        Assert.True(fixture.ViewModel.TrySelectConversation(fixture.Other.Id));
        fixture.ViewModel.InputText = "Other conversation's newer draft";
        var otherAttachments = fixture.Other.Attachments.ToArray();
        gate.Release(succeed: false);
        Assert.IsType<IOException>(await Record.ExceptionAsync(() => removing.WaitAsync(TestTimeout)));

        Assert.Same(fixture.Other, fixture.ViewModel.SelectedConversation);
        Assert.Equal("Other conversation's newer draft", fixture.ViewModel.InputText);
        Assert.Equal("Original ordinary draft", fixture.Target.DraftText);
        Assert.Equal(expected, fixture.Target.Attachments.ToArray());
        Assert.Equal(otherAttachments, fixture.Other.Attachments.ToArray());
        Assert.Equal(fixture.ImageBytes, File.ReadAllBytes(fixture.ImagePath));
    }

    [Theory]
    [InlineData("failure")]
    [InlineData("success")]
    [InlineData("cancel-before-failure")]
    [InlineData("failure-delete-shared")]
    public async Task OrdinaryDraftRemovalKeepsItsRecoverySeparateFromAnEditStartedWhileSaving(string outcome)
    {
        await using var fixture = new Fixture();
        var draftImagePath = Path.Combine(fixture.Store.AttachmentDirectoryPath, "ordinary-draft.png");
        File.WriteAllBytes(draftImagePath, fixture.ImageBytes);
        var draftImage = CopilotAttachmentItem.CreateImage(draftImagePath);
        fixture.Target.Attachments.Add(draftImage);
        if (outcome == "failure-delete-shared")
            fixture.Other.Attachments.Add(draftImage.CreateSnapshot());
        Assert.Equal(string.Empty, fixture.ViewModel.InputText);
        Assert.NotEqual(fixture.ImagePath, draftImagePath);
        await fixture.FlushAsync();
        var gate = fixture.Store.BlockSaves();

        var removing = fixture.RemoveAsync(draftImage);
        await gate.Entered.WaitAsync(TestTimeout);
        Assert.Empty(fixture.Target.Attachments);
        Assert.False(removing.IsCompleted);
        fixture.BeginEdit();
        var editAttachments = fixture.Target.Attachments.ToArray();
        Assert.Single(editAttachments, item => item.Value == fixture.ImagePath);
        Assert.DoesNotContain(editAttachments, item => item.Value == draftImagePath);
        var cancelledBeforeFailure = outcome == "cancel-before-failure";
        if (cancelledBeforeFailure)
        {
            Assert.True(fixture.ViewModel.CancelMessageEditCommand.CanExecute(null));
            fixture.ViewModel.CancelMessageEditCommand.Execute(null);
            Assert.Empty(fixture.Target.Attachments);
        }

        var succeeded = outcome == "success";
        gate.Release(succeed: succeeded);
        var failure = await Record.ExceptionAsync(() => removing.WaitAsync(TestTimeout));
        if (succeeded)
            Assert.Null(failure);
        else
            Assert.IsType<IOException>(failure);

        if (!cancelledBeforeFailure)
        {
            Assert.True(fixture.ViewModel.IsEditingMessage);
            Assert.Equal(fixture.OriginalUser.Content, fixture.ViewModel.InputText);
            Assert.Equal(editAttachments, fixture.Target.Attachments.ToArray());
            Assert.DoesNotContain(fixture.Target.Attachments, item => item.Value == draftImagePath);
            if (outcome == "failure-delete-shared")
            {
                fixture.Store.ReleasePendingSave();
                await fixture.FlushAsync();
                Assert.True(fixture.ViewModel.DeleteConversationCommand.CanExecute(fixture.Other));
                Assert.True(await fixture.DeleteConfirmedAsync(fixture.Other).WaitAsync(TestTimeout));
                Assert.DoesNotContain(fixture.Other, fixture.ViewModel.Conversations);
                Assert.True(fixture.ViewModel.IsEditingMessage);
                Assert.Equal(editAttachments, fixture.Target.Attachments.ToArray());
                Assert.True(File.Exists(draftImagePath));
            }
            Assert.True(fixture.ViewModel.CancelMessageEditCommand.CanExecute(null));
            fixture.ViewModel.CancelMessageEditCommand.Execute(null);
        }

        Assert.False(fixture.ViewModel.IsEditingMessage);
        Assert.Equal(string.Empty, fixture.ViewModel.InputText);
        if (succeeded)
        {
            Assert.Empty(fixture.Target.Attachments);
            Assert.False(File.Exists(draftImagePath));
        }
        else
        {
            var restored = Assert.Single(fixture.Target.Attachments);
            Assert.Equal(draftImage.Id, restored.Id);
            Assert.Equal(draftImagePath, restored.Value);
            Assert.Equal(fixture.ImageBytes, File.ReadAllBytes(draftImagePath));
        }
        Assert.Equal(fixture.ImageBytes, File.ReadAllBytes(fixture.ImagePath));
        Assert.Single(fixture.OriginalUser.Attachments, item => item.Value == fixture.ImagePath);
        Assert.DoesNotContain(fixture.OriginalUser.Attachments, item => item.Value == draftImagePath);
        Assert.Equal(2, fixture.Target.Messages.Count);
    }

    [Theory]
    [InlineData("same-source")]
    [InlineData("same-title")]
    [InlineData("different-source")]
    [InlineData("same-source-edit-other")]
    public async Task FailedContextRemovalDoesNotRestoreAReplacedContext(string replacement)
    {
        await using var fixture = new Fixture();
        CopilotChatMessage? otherUser = null;
        if (replacement == "same-source-edit-other")
        {
            fixture.Other.DraftText = string.Empty;
            fixture.Other.Attachments.Clear();
            otherUser = new CopilotChatMessage(CopilotChatRole.User, "Edit the other conversation's request")
            {
                RequestMode = CopilotAgentMode.Auto,
                AttachmentSnapshotCaptured = true,
            };
            fixture.Other.Messages.Add(otherUser);
            fixture.Other.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Other completed answer")
            {
                RequestMode = CopilotAgentMode.Auto,
                AgentStopReason = CopilotAgentStopReason.Completed,
            });
        }
        var originalSource = replacement == "same-title" ? null : "measurement-source";
        var original = CopilotAttachmentItem.CreateContext("Old measurement", "Measurement", originalSource);
        fixture.Target.Attachments.Add(original);
        await fixture.FlushAsync();
        var gate = fixture.Store.BlockSaves();

        var removing = fixture.RemoveAsync(original);
        await gate.Entered.WaitAsync(TestTimeout);
        var replacementSource = replacement == "different-source" ? "another-source" : originalSource;
        var queued = fixture.ViewModel.QueueExternalPrompt("Use the updated measurement", startNewConversation: false,
            sendNow: false, contextAttachmentTitle: "Measurement", contextAttachmentSourceId: replacementSource,
            contextAttachmentItems: [new CopilotContextItem { Title = "Measurement", Content = "New measurement" }]);
        Assert.True(queued.Accepted);
        var newer = Assert.Single(fixture.Target.Attachments);
        Assert.Contains("New measurement", newer.Value, StringComparison.Ordinal);
        if (otherUser != null)
        {
            Assert.True(fixture.ViewModel.TrySelectConversation(fixture.Other.Id));
            Assert.True(fixture.ViewModel.EditMessageCommand.CanExecute(otherUser));
            fixture.ViewModel.EditMessageCommand.Execute(otherUser);
            Assert.True(fixture.ViewModel.IsEditingMessage);
        }
        gate.Release(succeed: false);
        Assert.IsType<IOException>(await Record.ExceptionAsync(() => removing.WaitAsync(TestTimeout)));

        Assert.Equal(otherUser?.Content ?? "Use the updated measurement", fixture.ViewModel.InputText);
        Assert.Equal("Use the updated measurement", fixture.Target.DraftText);
        if (otherUser != null)
        {
            Assert.Same(fixture.Other, fixture.ViewModel.SelectedConversation);
            Assert.True(fixture.ViewModel.IsEditingMessage);
            Assert.Empty(fixture.Other.Attachments);
        }
        Assert.Contains(newer, fixture.Target.Attachments);
        if (replacement == "different-source")
        {
            Assert.Equal(2, fixture.Target.Attachments.Count);
            Assert.Same(original, fixture.Target.Attachments[0]);
        }
        else
        {
            Assert.Same(newer, Assert.Single(fixture.Target.Attachments));
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private static readonly FieldInfo SolutionInstance = typeof(SolutionManager)
            .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? _previousSolution = SolutionInstance.GetValue(null);
        private readonly object _isolatedSolution = RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));
        private readonly string _root = Directory.CreateTempSubdirectory("CopilotRemovalEditLifetime-").FullName;
        private readonly List<Task> _operations = [];

        public Fixture()
        {
            SolutionInstance.SetValue(null, _isolatedSolution);
            var diskStore = new CopilotChatStateStore(_root);
            Directory.CreateDirectory(diskStore.AttachmentDirectoryPath);
            ImagePath = Path.Combine(diskStore.AttachmentDirectoryPath, "historical.png");
            File.WriteAllBytes(ImagePath, ImageBytes);
            var profile = new CopilotProfileConfig
            {
                Id = "removal-edit-profile", Name = "Removal edit profile",
                VendorType = CopilotVendorType.Custom, ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "removal-edit-test-key", BaseUrl = "https://example.test/v1", Model = "test-model",
                SupportsImageInput = true,
            };
            var target = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
            target.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Historical request")
            {
                RequestMode = CopilotAgentMode.Auto,
                AttachmentSnapshotCaptured = true,
                Attachments =
                [
                    CopilotAttachmentItem.CreateContext("Before image", "Before"),
                    CopilotAttachmentItem.CreateImage(ImagePath),
                    CopilotAttachmentItem.CreateContext("After image", "After"),
                ],
            });
            target.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Historical completed answer")
            {
                RequestMode = CopilotAgentMode.Auto,
                AgentStopReason = CopilotAgentStopReason.Completed,
            });
            var other = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
            other.DraftText = "Other draft";
            other.Attachments.Add(CopilotAttachmentItem.CreateContext("Unrelated context", "Other context"));
            diskStore.Save(new CopilotChatState
            {
                ActiveConversationId = target.Id, ActiveProfileId = profile.Id, Conversations = [target, other],
            });
            Store = new GatedStateStore(diskStore);
            var config = new CopilotConfig
            {
                SchemaVersion = CopilotConfig.CurrentSchemaVersion,
                McpBearerToken = "removal-edit-test-token", Profiles = [profile],
            };
            ViewModel = new CopilotChatViewModel(new CopilotChatService(), Store, config, new UnusedTurnRuntime(), Host);
            Target = ViewModel.Conversations.Single(conversation => conversation.Id == target.Id);
            Other = ViewModel.Conversations.Single(conversation => conversation.Id == other.Id);
            OriginalUser = Target.Messages[0];
        }

        public byte[] ImageBytes { get; } = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aF9sAAAAASUVORK5CYII=");
        public string ImagePath { get; }
        public GatedStateStore Store { get; }
        public CopilotChatViewModel ViewModel { get; }
        public CopilotConversationRecord Target { get; }
        public CopilotConversationRecord Other { get; }
        public CopilotChatMessage OriginalUser { get; }
        private CopilotAgentTaskHost Host { get; } = new();

        public void BeginEdit()
        {
            Assert.True(ViewModel.EditMessageCommand.CanExecute(OriginalUser));
            ViewModel.EditMessageCommand.Execute(OriginalUser);
            Assert.True(ViewModel.IsEditingMessage);
        }

        public Task FlushAsync() => InvokeTask("FlushStatePersistenceBarrierAsync").WaitAsync(TestTimeout);

        public Task RemoveAsync(CopilotAttachmentItem attachment)
        {
            Assert.True(ViewModel.RemoveAttachmentCommand.CanExecute(attachment));
            // Await the command's real handler so its persistence error is observable.
            var operation = InvokeTask("RemoveAttachment", attachment);
            _operations.Add(operation);
            return operation;
        }

        public async Task<bool> DeleteConfirmedAsync(CopilotConversationRecord conversation)
        {
            var operation = InvokeTask("DeleteConfirmedConversationAsync", conversation);
            _operations.Add(operation);
            await operation;
            var result = operation.GetType().GetProperty("Result")!.GetValue(operation)!;
            return (bool)result.GetType().GetProperty("Deleted")!.GetValue(result)!;
        }

        private Task InvokeTask(string name, params object?[] arguments) =>
            Assert.IsAssignableFrom<Task>(typeof(CopilotChatViewModel)
                .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(ViewModel, arguments));

        public async ValueTask DisposeAsync()
        {
            try
            {
                Store.ReleasePendingSave();
                foreach (var operation in _operations)
                    _ = await Record.ExceptionAsync(() => operation.WaitAsync(TestTimeout));
                await FlushAsync();
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
                    && Path.GetFileName(root).StartsWith("CopilotRemovalEditLifetime-", StringComparison.Ordinal));
                Directory.Delete(root, recursive: true);
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

        public SaveGate BlockSaves()
        {
            var gate = new SaveGate();
            Volatile.Write(ref _gate, gate);
            return gate;
        }

        public async Task SaveSerializedAsync(string serializedState, CancellationToken cancellationToken = default)
        {
            var gate = Volatile.Read(ref _gate);
            if (gate != null)
            {
                gate.SignalEntered();
                // Keep every retry failing, including snapshots taken after an edit
                // was reopened. A changed snapshot must not silently release the gate.
                if (!await gate.Outcome.WaitAsync(cancellationToken).ConfigureAwait(false))
                    throw new IOException("Controlled attachment removal save failure.");
            }
            await inner.SaveSerializedAsync(serializedState, cancellationToken).ConfigureAwait(false);
        }

        public void ReleasePendingSave() => Interlocked.Exchange(ref _gate, null)?.Release(succeed: true);
    }

    private sealed class SaveGate
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _outcome = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Entered => _entered.Task;
        public Task<bool> Outcome => _outcome.Task;
        public void SignalEntered() => _entered.TrySetResult();
        public void Release(bool succeed) => _outcome.TrySetResult(succeed);
    }

    private sealed class UnusedTurnRuntime : ICopilotTurnRuntime
    {
        public IAsyncEnumerable<CopilotTurnEvent> RunAsync(CopilotTurnRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Attachment removal tests must not invoke a model.");
        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message) => new(CopilotSteeringAdmissionReason.RuntimeUnavailable);
        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;
        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;
        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) => false;
        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(CopilotWorkspaceRollbackActionRequest request,
            Action<CopilotAgentEvent> onEvent, CancellationToken cancellationToken) => Task.FromException<CopilotWorkspaceRollbackActionResult>(new NotSupportedException());
    }
}
