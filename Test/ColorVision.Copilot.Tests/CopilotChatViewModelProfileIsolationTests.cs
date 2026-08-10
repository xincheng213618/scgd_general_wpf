using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using ColorVision.Copilot;
using ColorVision.Solution;
using Newtonsoft.Json.Linq;

namespace ColorVision.Copilot.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CopilotChatViewModelProfileIsolationFixture
{
    public const string Name = "CopilotChatViewModel profile isolation";
}

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotChatViewModelProfileIsolationTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task BackgroundTurnCompletionDoesNotApplyProfileFromNewlySelectedConversation()
    {
        var profileA = CreateProfile("profile-a", "Profile A", "model-a");
        var profileB = CreateProfile("profile-b", "Profile B", "model-b");
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            McpBearerToken = "profile-isolation-test-token",
            Profiles = new ObservableCollection<CopilotProfileConfig>
            {
                profileA,
                profileB,
            },
        };
        var conversationA = CopilotConversationRecord.CreateEmpty(profileA.Id, profileA.DisplayLabel);
        conversationA.Id = "conversation-a";
        conversationA.DraftRequestMode = CopilotAgentMode.Explain;
        var conversationB = CopilotConversationRecord.CreateEmpty(profileB.Id, profileB.DisplayLabel);
        conversationB.Id = "conversation-b";
        var state = new CopilotChatState
        {
            ActiveConversationId = conversationA.Id,
            ActiveProfileId = profileA.Id,
            Conversations = new ObservableCollection<CopilotConversationRecord>
            {
                conversationA,
                conversationB,
            },
        };
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = new CopilotChatViewModel(
            new CopilotChatService(),
            new InMemoryStateStore(state),
            config,
            runtime,
            taskHost);
        CopilotHostedAgentRun? activeRun = null;
        try
        {
            Assert.Same(conversationA, viewModel.SelectedConversation);
            Assert.Same(profileA, viewModel.SelectedProfile);

            viewModel.InputText = "Explain conversation A.";
            viewModel.SendCommand.Execute(null);

            var request = await runtime.Entered.WaitAsync(TestTimeout);
            activeRun = taskHost.ActiveRun;
            Assert.NotNull(activeRun);
            Assert.Equal(conversationA.Id, request.ConversationId);
            Assert.Equal(profileA.Id, request.Profile.Id);
            Assert.True(activeRun.IsAgent);
            Assert.True(viewModel.CanSwitchConversation);
            Assert.True(viewModel.SelectConversationCommand.CanExecute(conversationB));

            viewModel.SelectConversationCommand.Execute(conversationB);

            Assert.Same(conversationB, viewModel.SelectedConversation);
            Assert.Same(profileB, viewModel.SelectedProfile);

            runtime.Release();
            await activeRun.Completion.WaitAsync(TestTimeout);

            Assert.Same(conversationB, viewModel.SelectedConversation);
            Assert.Same(profileB, viewModel.SelectedProfile);
            Assert.Equal(profileA.Id, conversationA.ProfileId);
            Assert.Equal(profileA.DisplayLabel, conversationA.ProfileDisplayName);
            Assert.Equal(profileB.Id, conversationB.ProfileId);
            Assert.Equal(profileB.DisplayLabel, conversationB.ProfileDisplayName);
        }
        finally
        {
            runtime.Release();
            var pendingRun = activeRun ?? taskHost.ActiveRun;
            if (pendingRun != null && !pendingRun.Completion.IsCompleted)
            {
                try
                {
                    await pendingRun.Completion.WaitAsync(TestTimeout);
                }
                catch
                {
                }
            }
            viewModel.Dispose();
        }
    }

    [Fact]
    public void PromptHistorySearchUsesAnOverlayAndRestoresTheCompleteComposerDraft()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            McpBearerToken = "composer-search-overlay-test-token",
            Profiles = new ObservableCollection<CopilotProfileConfig> { profile },
        };
        var skillReference = new CopilotAgentSkillReference
        {
            Name = "sample-skill",
            SkillFilePath = Path.GetFullPath(
                Path.Combine("skills", "sample-skill", "SKILL.md")),
        };
        var conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
        conversation.Id = "conversation-a";
        conversation.DraftText = "$sample-skill current draft";
        conversation.DraftRequestMode = CopilotAgentMode.Review;
        conversation.DraftWorkspaceReviewTarget = CopilotWorkspaceReviewTargetContext.WorkingTree();
        conversation.DraftAgentSkillReference = skillReference;
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "older prompt"));
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id,
            ActiveProfileId = profile.Id,
            Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
        };
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = new CopilotChatViewModel(
            new CopilotChatService(),
            new InMemoryStateStore(state),
            config,
            new GatedFailingTurnRuntime(),
            new CopilotAgentTaskHost());
        try
        {
            Assert.True(viewModel.TryOpenPromptHistorySearch());

            viewModel.InputText = "older";

            Assert.Equal("older", viewModel.InputText);
            Assert.Equal("$sample-skill current draft", conversation.DraftText);
            Assert.Equal(CopilotAgentMode.Review, conversation.DraftRequestMode);
            Assert.Equal(
                CopilotWorkspaceReviewTarget.WorkingTree,
                conversation.DraftWorkspaceReviewTarget?.Target);
            Assert.Equal("sample-skill", conversation.DraftAgentSkillReference?.Name);

            viewModel.DismissPromptHistorySearch();

            Assert.Equal("$sample-skill current draft", viewModel.InputText);
            Assert.Equal(CopilotAgentMode.Review, conversation.DraftRequestMode);
            Assert.NotNull(conversation.DraftWorkspaceReviewTarget);
            Assert.Equal("sample-skill", conversation.DraftAgentSkillReference?.Name);
        }
        finally
        {
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task ScheduledTurnConsumesCapturedAttachmentsButPreservesAttachmentsAddedDuringScheduling()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "composer-attachment-race-test-token");
        var conversation = CreateConversation(profile, "conversation-a", "original draft");
        var capturedAttachment = CopilotAttachmentItem.CreateContext("captured context");
        var lateAttachment = CopilotAttachmentItem.CreateContext("late context");
        conversation.Attachments.Add(capturedAttachment);
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(conversation, config, runtime, taskHost);
        CopilotHostedAgentRun? activeRun = null;
        EventHandler<CopilotAgentTaskHostChangedEventArgs>? onHostChanged = null;
        try
        {
            onHostChanged = (_, args) =>
            {
                if (args.Kind == CopilotAgentTaskHostChangeKind.Started)
                    conversation.Attachments.Add(lateAttachment);
            };
            taskHost.Changed += onHostChanged;

            viewModel.SendCommand.Execute(null);

            await runtime.Entered.WaitAsync(TestTimeout);
            activeRun = taskHost.ActiveRun;
            Assert.NotNull(activeRun);
            Assert.Equal(string.Empty, viewModel.InputText);
            Assert.DoesNotContain(capturedAttachment, conversation.Attachments);
            Assert.Contains(lateAttachment, conversation.Attachments);

            runtime.Release();
            await activeRun.Completion.WaitAsync(TestTimeout);
        }
        finally
        {
            if (onHostChanged != null)
                taskHost.Changed -= onHostChanged;
            await CompleteAndDisposeAsync(viewModel, runtime, taskHost, activeRun);
        }
    }

    [Fact]
    public async Task NewerComposerEditDuringSchedulingIsNotClearedByTheCapturedTurn()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "composer-stale-token-test-token");
        var conversation = CreateConversation(profile, "conversation-a", "original draft");
        conversation.DraftRequestMode = CopilotAgentMode.Review;
        conversation.DraftWorkspaceReviewTarget = CopilotWorkspaceReviewTargetContext.WorkingTree();
        var capturedAttachment = CopilotAttachmentItem.CreateContext("captured context");
        conversation.Attachments.Add(capturedAttachment);
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(conversation, config, runtime, taskHost);
        CopilotHostedAgentRun? activeRun = null;
        EventHandler<CopilotAgentTaskHostChangedEventArgs>? onHostChanged = null;
        try
        {
            onHostChanged = (_, args) =>
            {
                if (args.Kind == CopilotAgentTaskHostChangeKind.Started)
                    viewModel.InputText = "newer draft";
            };
            taskHost.Changed += onHostChanged;

            viewModel.SendCommand.Execute(null);

            await runtime.Entered.WaitAsync(TestTimeout);
            activeRun = taskHost.ActiveRun;
            Assert.NotNull(activeRun);
            Assert.Equal("newer draft", viewModel.InputText);
            Assert.Equal("newer draft", conversation.DraftText);
            Assert.Equal(CopilotAgentMode.Review, conversation.DraftRequestMode);
            Assert.NotNull(conversation.DraftWorkspaceReviewTarget);
            Assert.Contains(capturedAttachment, conversation.Attachments);

            runtime.Release();
            await activeRun.Completion.WaitAsync(TestTimeout);
        }
        finally
        {
            if (onHostChanged != null)
                taskHost.Changed -= onHostChanged;
            await CompleteAndDisposeAsync(viewModel, runtime, taskHost, activeRun);
        }
    }

    private static CopilotConfig CreateConfig(CopilotProfileConfig profile, string bearerToken) => new()
    {
        SchemaVersion = CopilotConfig.CurrentSchemaVersion,
        McpBearerToken = bearerToken,
        Profiles = new ObservableCollection<CopilotProfileConfig> { profile },
    };

    private static CopilotConversationRecord CreateConversation(
        CopilotProfileConfig profile,
        string id,
        string draftText)
    {
        var conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
        conversation.Id = id;
        conversation.DraftText = draftText;
        return conversation;
    }

    private static CopilotChatViewModel CreateViewModel(
        CopilotConversationRecord conversation,
        CopilotConfig config,
        ICopilotTurnRuntime runtime,
        CopilotAgentTaskHost taskHost)
    {
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id,
            ActiveProfileId = conversation.ProfileId,
            Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
        };
        return new CopilotChatViewModel(
            new CopilotChatService(),
            new InMemoryStateStore(state),
            config,
            runtime,
            taskHost);
    }

    private static async Task CompleteAndDisposeAsync(
        CopilotChatViewModel viewModel,
        GatedFailingTurnRuntime runtime,
        CopilotAgentTaskHost taskHost,
        CopilotHostedAgentRun? activeRun)
    {
        runtime.Release();
        var pendingRun = activeRun ?? taskHost.ActiveRun;
        if (pendingRun != null && !pendingRun.Completion.IsCompleted)
        {
            try
            {
                await pendingRun.Completion.WaitAsync(TestTimeout);
            }
            catch
            {
            }
        }
        viewModel.Dispose();
    }

    private static CopilotProfileConfig CreateProfile(string id, string name, string model) => new()
    {
        Id = id,
        Name = name,
        VendorType = CopilotVendorType.Custom,
        ProviderType = CopilotProviderType.OpenAICompatible,
        ApiKey = "profile-isolation-test-key",
        BaseUrl = "https://unit.test/v1",
        Model = model,
    };

    private sealed class GatedFailingTurnRuntime : ICopilotTurnRuntime
    {
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<CopilotTurnRequest> Entered => _entered.Task;

        private readonly TaskCompletionSource<CopilotTurnRequest> _entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async IAsyncEnumerable<CopilotTurnEvent> RunAsync(
            CopilotTurnRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            _entered.TrySetResult(request);
            await _release.Task.WaitAsync(cancellationToken);
            yield return new CopilotTurnStartedEvent("profile-isolation-turn", request.Mode);
            throw new InvalidOperationException("Expected profile-isolation test failure.");
        }

        public void Release() => _release.TrySetResult();

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

    private sealed class InMemoryStateStore(CopilotChatState state) : ICopilotChatStateStore
    {
        public string AttachmentDirectoryPath => string.Empty;

        public CopilotChatState Load() => state;

        public void Save(CopilotChatState value)
        {
        }

        public CopilotChatStateSnapshot CaptureSnapshot(CopilotChatState value) =>
            new(new JObject());

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
            "_instance",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("SolutionManager singleton field was not found.");

        private readonly object? _previousInstance = InstanceField.GetValue(null);
        private readonly SolutionManager _testInstance =
            (SolutionManager)RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));

        public IsolatedSolutionManagerScope()
        {
            InstanceField.SetValue(null, _testInstance);
        }

        public void Dispose()
        {
            if (ReferenceEquals(InstanceField.GetValue(null), _testInstance))
                InstanceField.SetValue(null, _previousInstance);
        }
    }
}
