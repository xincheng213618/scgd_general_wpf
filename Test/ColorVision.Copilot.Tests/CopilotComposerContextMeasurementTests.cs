using System.Reflection;
using System.IO;
using System.Runtime.CompilerServices;
using ColorVision.Copilot;
using ColorVision.Solution;
using ColorVision.Solution.Workspace;
using Newtonsoft.Json.Linq;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotComposerContextMeasurementTests
{
    [Fact]
    public void SwitchingConversationMeasuresOnceAfterLoadingItsDraftProfileAndAttachments()
    {
        using var fixture = new ViewModelFixture();
        var viewModel = fixture.ViewModel;
        viewModel.InputText = "Original draft";
        var originalProfile = viewModel.SelectedProfile;
        var originalContextUsage = viewModel.ConversationContextUsageToolTip;
        var originalPressure = viewModel.IsConversationContextUnderPressure;
        var other = fixture.OtherConversation;
        other.ProfileId = fixture.ShortOutputProfile.Id;
        other.ProfileDisplayName = fixture.ShortOutputProfile.DisplayLabel;
        other.DraftText = new string('中', 28_000);
        other.DraftRequestMode = CopilotAgentMode.Chat;
        var attachment = CopilotAttachmentItem.CreateContext("Other context body", "Other context");
        other.Attachments.Add(attachment);
        var measurements = ObserveMeasurements(viewModel);

        viewModel.SelectedConversation = other;

        Assert.Single(measurements);
        Assert.Same(other, viewModel.SelectedConversation);
        Assert.Same(fixture.ShortOutputProfile, viewModel.SelectedProfile);
        Assert.Equal(other.DraftText, viewModel.InputText);
        Assert.Equal(CopilotAgentMode.Chat, other.DraftRequestMode);
        Assert.Same(attachment, Assert.Single(viewModel.Attachments));
        Assert.True(viewModel.HasAttachments);
        Assert.True(viewModel.IsConversationContextUnderPressure);
        Assert.Contains("Prompt: 28000 characters", viewModel.PrimaryActionToolTip, StringComparison.Ordinal);
        Assert.Contains("Conversation context: None", viewModel.PrimaryActionToolTip, StringComparison.Ordinal);
        Assert.Contains("Attachments: 1 total (1 context)", viewModel.PrimaryActionToolTip, StringComparison.Ordinal);

        measurements.Clear();
        viewModel.SelectedConversation = fixture.Conversation;

        Assert.Single(measurements);
        Assert.Same(originalProfile, viewModel.SelectedProfile);
        Assert.Equal("Original draft", viewModel.InputText);
        Assert.Equal(CopilotAgentMode.Code, fixture.Conversation.DraftRequestMode);
        Assert.Empty(viewModel.Attachments);
        Assert.False(viewModel.HasAttachments);
        Assert.Equal(originalPressure, viewModel.IsConversationContextUnderPressure);
        Assert.Equal(originalContextUsage, viewModel.ConversationContextUsageToolTip);
        Assert.Contains("retained from 14 message(s)", viewModel.PrimaryActionToolTip, StringComparison.Ordinal);
        Assert.Equal(0, fixture.Runtime.RequestCount);
    }

    [Fact]
    public void EachSelectedAttachmentCollectionChangeMeasuresOnceAndPublishesItsFinalPreview()
    {
        using var fixture = new ViewModelFixture();
        var viewModel = fixture.ViewModel;
        viewModel.InputText = "Keep this draft";
        var measurements = ObserveMeasurements(viewModel);
        var attachmentNotifications = new List<string>();
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(CopilotChatViewModel.Attachments) or nameof(CopilotChatViewModel.HasAttachments))
                attachmentNotifications.Add(args.PropertyName);
        };
        var original = CopilotAttachmentItem.CreateContext("First body", "Original context");
        var replacement = CopilotAttachmentItem.CreateContext("Replacement body", "Replacement context");

        viewModel.Attachments.Add(original);

        Assert.Single(measurements);
        Assert.True(viewModel.HasAttachments);
        Assert.Same(original, Assert.Single(viewModel.Attachments));
        Assert.Contains(nameof(CopilotChatViewModel.HasAttachments), attachmentNotifications);
        Assert.Contains("Attachments: 1 total (1 context)", viewModel.PrimaryActionToolTip, StringComparison.Ordinal);

        measurements.Clear();
        viewModel.Attachments[0] = replacement;

        Assert.Single(measurements);
        Assert.Same(replacement, Assert.Single(viewModel.Attachments));
        Assert.Equal("Replacement context", viewModel.Attachments[0].Title);
        Assert.Equal("Replacement body", viewModel.Attachments[0].Value);

        measurements.Clear();
        attachmentNotifications.Clear();
        viewModel.Attachments.Remove(replacement);

        Assert.Single(measurements);
        Assert.False(viewModel.HasAttachments);
        Assert.Empty(viewModel.Attachments);
        Assert.Contains(nameof(CopilotChatViewModel.HasAttachments), attachmentNotifications);
        Assert.Contains("Attachments: None", viewModel.PrimaryActionToolTip, StringComparison.Ordinal);
        Assert.Equal("Keep this draft", viewModel.InputText);
        Assert.Equal("Keep this draft", fixture.Conversation.DraftText);
    }

    [Fact]
    public async Task AddingAndRemovingAFileDoesNotRepeatTheCollectionMeasurementInAttachmentStateUpdates()
    {
        using var file = new TemporaryTextFile();
        using var fixture = new ViewModelFixture();
        var viewModel = fixture.ViewModel;
        var measurements = ObserveMeasurements(viewModel);

        Assert.Equal(1, viewModel.AddFileAttachments([file.Path]));

        Assert.Single(measurements);
        var attachment = Assert.Single(viewModel.Attachments);
        Assert.Equal(file.Path, attachment.Value);
        Assert.Equal(CopilotAttachmentType.File, attachment.Type);
        Assert.Contains("Attachments: 1 total (1 file)", viewModel.PrimaryActionToolTip, StringComparison.Ordinal);
        Assert.True(viewModel.RemoveAttachmentCommand.CanExecute(attachment));

        measurements.Clear();
        // Await the command's actual async operation, including its save barrier.
        await (Task)typeof(CopilotChatViewModel).GetMethod("RemoveAttachment", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(viewModel, [attachment])!;

        Assert.Single(measurements);
        Assert.Empty(viewModel.Attachments);
        Assert.False(viewModel.HasAttachments);
        Assert.Contains("Attachments: None", viewModel.PrimaryActionToolTip, StringComparison.Ordinal);
        Assert.True(File.Exists(file.Path));
        Assert.Equal(0, fixture.Runtime.RequestCount);
    }

    [Fact]
    public void UpdatingAnotherConversationsAttachmentsDoesNotMeasureOrReplaceTheSelectedDraft()
    {
        using var fixture = new ViewModelFixture();
        var viewModel = fixture.ViewModel;
        viewModel.InputText = "Keep the selected draft";
        var preview = viewModel.PrimaryActionToolTip;
        var profile = viewModel.SelectedProfile;
        var measurements = ObserveMeasurements(viewModel);
        CopilotContextItem[] context = [new() { Title = "Background context", Content = "Background body" }];

        var attached = (bool)typeof(CopilotChatViewModel).GetMethod("AttachExternalContextSnapshot", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(viewModel, [fixture.OtherConversation, "Background context", "background-source", context])!;

        Assert.True(attached);
        Assert.Empty(measurements);
        Assert.Single(fixture.OtherConversation.Attachments);
        Assert.Contains("Background body", fixture.OtherConversation.Attachments[0].Value, StringComparison.Ordinal);
        Assert.Same(fixture.Conversation, viewModel.SelectedConversation);
        Assert.Same(profile, viewModel.SelectedProfile);
        Assert.Empty(viewModel.Attachments);
        Assert.Equal("Keep the selected draft", viewModel.InputText);
        Assert.Equal(preview, viewModel.PrimaryActionToolTip);
    }

    [Fact]
    public void ActiveDocumentAndLiveContextChangesStillPublishCurrentAttachmentActionsAndPreview()
    {
        using var file = new TemporaryTextFile();
        using var fixture = new ViewModelFixture();
        var viewModel = fixture.ViewModel;
        var previousDocument = WorkspaceManager.SelectedContentId;
        var previousContext = CopilotLiveContextRegistry.Current;
        var sourceId = $"measurement-context-{Guid.NewGuid():N}";
        var measurements = ObserveMeasurements(viewModel);
        var notifications = new List<string?>();
        viewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        try
        {
            WorkspaceManager.OnContentIdSelected(file.Path);

            Assert.InRange(measurements.Count, 0, 1);
            Assert.True(viewModel.HasActiveDocument);
            Assert.True(viewModel.CanAttachActiveDocument);
            Assert.Contains(System.IO.Path.GetFileName(file.Path), viewModel.ActiveDocumentAttachmentMenuText, StringComparison.Ordinal);
            Assert.Contains(nameof(CopilotChatViewModel.ActiveDocumentAttachmentMenuText), notifications);

            measurements.Clear();
            notifications.Clear();
            CopilotLiveContextRegistry.Publish(new CopilotLiveContext
            {
                SourceId = sourceId, Title = "Live inspection", AttachmentTitle = "Current inspection snapshot",
                Summary = "Current inspection summary",
                SnapshotItems = [new CopilotContextItem { Title = "Inspection", Content = "Inspection evidence" }],
            });

            Assert.InRange(measurements.Count, 0, 1);
            Assert.True(viewModel.HasAvailableCurrentLiveContext);
            Assert.True(viewModel.CanAttachCurrentLiveContext);
            Assert.True(viewModel.HasComposerAttachmentItems);
            Assert.Equal("Current inspection snapshot", viewModel.CurrentLiveContextAttachmentLabel);
            Assert.Contains("Window context: Live summary available for this request", viewModel.PrimaryActionToolTip, StringComparison.Ordinal);
            Assert.Contains(nameof(CopilotChatViewModel.CurrentLiveContextAttachmentLabel), notifications);
            Assert.Contains(nameof(CopilotChatViewModel.PrimaryActionToolTip), notifications);
        }
        finally
        {
            WorkspaceManager.OnContentIdSelected(previousDocument);
            if (previousContext != null)
                CopilotLiveContextRegistry.Publish(previousContext);
            else
                CopilotLiveContextRegistry.Clear(sourceId);
        }
    }

    private static List<string> ObserveMeasurements(CopilotChatViewModel viewModel)
    {
        var measurements = new List<string>();
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(CopilotChatViewModel.ConversationContextUsageToolTip))
                measurements.Add(viewModel.ConversationContextUsageToolTip);
        };
        return measurements;
    }

    [Fact]
    public void InputProfileConversationAndMessageTransitionsRefreshTheDisplayedContext()
    {
        using var fixture = new ViewModelFixture();
        var viewModel = fixture.ViewModel;
        var conversation = fixture.Conversation;
        Assert.Contains("retained from 14 message(s)", viewModel.PrimaryActionToolTip, StringComparison.Ordinal);

        viewModel.SelectedProfile = fixture.ShortOutputProfile;
        Assert.False(viewModel.IsConversationContextReduced);
        Assert.Contains("14 message(s), 14 characters", viewModel.PrimaryActionToolTip, StringComparison.Ordinal);

        // Message bodies are mutable DTOs. Tooltip reads use the last completed UI refresh;
        // the next actual input change must measure the current bodies again.
        conversation.Messages[0].Content = new string('x', 101);
        Assert.Contains("14 message(s), 14 characters", viewModel.PrimaryActionToolTip, StringComparison.Ordinal);
        viewModel.InputText = "continue";
        Assert.Contains("14 message(s), 114 characters", viewModel.PrimaryActionToolTip, StringComparison.Ordinal);

        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "y"));
        Assert.Contains("15 message(s), 115 characters", viewModel.PrimaryActionToolTip, StringComparison.Ordinal);
        viewModel.SelectedConversation = fixture.OtherConversation;
        Assert.Contains("Conversation context: None", viewModel.PrimaryActionToolTip, StringComparison.Ordinal);
        viewModel.InputText = new string('中', 15_000);
        Assert.True(viewModel.IsConversationContextUnderPressure);
        viewModel.InputText = "continue";
        Assert.False(viewModel.IsConversationContextUnderPressure);
        viewModel.SelectedConversation = conversation;
        Assert.Contains("15 message(s), 115 characters", viewModel.PrimaryActionToolTip, StringComparison.Ordinal);

        conversation.Compaction = new CopilotConversationCompaction
        {
            StrategyVersion = CopilotConversationCompaction.CurrentStrategyVersion,
            Summary = "Previous work",
            ThroughMessageId = conversation.Messages[13].Id,
        };
        // Compaction completion publishes through this existing metadata refresh boundary.
        typeof(CopilotChatViewModel).GetMethod("UpdateConversationMetadata", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(viewModel, [conversation, false]);
        Assert.Contains("Conversation context: 2 message(s)", viewModel.PrimaryActionToolTip, StringComparison.Ordinal);
        Assert.False(viewModel.IsConversationContextReduced);
        Assert.Equal(0, fixture.Runtime.RequestCount);
    }

    [Fact]
    public void SendRechecksTheCurrentGoalBudgetWithoutUsingTheLastDisplayedMeasurement()
    {
        using var fixture = new ViewModelFixture();
        var viewModel = fixture.ViewModel;
        viewModel.InputText = new string('x', 1_000);
        var priorUsage = viewModel.ConversationContextUsageToolTip;
        fixture.Conversation.Goal = CopilotConversationGoal.Create(new string('中', 4_000), DateTimeOffset.UtcNow);
        Assert.Equal(priorUsage, viewModel.ConversationContextUsageToolTip);

        viewModel.SendCommand.Execute(null);

        Assert.Equal("输入过长", viewModel.LocalCommandResultTitle);
        Assert.Equal(0, fixture.Runtime.RequestCount);
        Assert.Equal(14, fixture.Conversation.Messages.Count);
        Assert.Equal(new string('x', 1_000), viewModel.InputText);
    }

    private sealed class ViewModelFixture : IDisposable
    {
        private readonly IsolatedSolutionManagerScope _solutionManager = new();

        public CopilotConversationRecord Conversation { get; }
        public CopilotConversationRecord OtherConversation { get; }
        public CopilotProfileConfig ShortOutputProfile { get; }
        public RejectingRuntime Runtime { get; } = new();
        public CopilotChatViewModel ViewModel { get; }

        public ViewModelFixture()
        {
            var profile = CreateProfile("long-output", 8_192);
            ShortOutputProfile = CreateProfile("short-output", 32);
            Conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
            Conversation.DraftRequestMode = CopilotAgentMode.Code;
            for (var index = 0; index < 14; index++)
                Conversation.Messages.Add(new CopilotChatMessage(index % 2 == 0 ? CopilotChatRole.User : CopilotChatRole.Assistant, "x"));
            OtherConversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
            var state = new CopilotChatState
            {
                ActiveConversationId = Conversation.Id,
                ActiveProfileId = profile.Id,
                Conversations = [Conversation, OtherConversation],
            };
            var config = new CopilotConfig
            {
                SchemaVersion = CopilotConfig.CurrentSchemaVersion,
                McpBearerToken = "context-measurement-test-token",
                Profiles = [profile, ShortOutputProfile],
                AgentDefaults = new CopilotAgentDefaultsConfig
                {
                    ContextWindowTokens = 32_768,
                    RequestTokenBudget = 4_096,
                    AutoCompactConversationHistory = true,
                    AutoCompactThresholdPercent = 85,
                },
            };
            ViewModel = new CopilotChatViewModel(new CopilotChatService(), new InMemoryStateStore(state), config, Runtime, new CopilotAgentTaskHost());
        }

        public void Dispose()
        {
            ViewModel.Dispose();
            _solutionManager.Dispose();
        }

        private static CopilotProfileConfig CreateProfile(string id, int maxTokens) => new()
        {
            Id = id,
            Name = id,
            VendorType = CopilotVendorType.Custom,
            ProviderType = CopilotProviderType.OpenAICompatible,
            BaseUrl = "https://example.test/v1",
            ApiKey = "context-measurement-test-key",
            Model = "context-measurement-test-model",
            MaxTokens = maxTokens,
        };
    }

    private sealed class RejectingRuntime : ICopilotTurnRuntime
    {
        public int RequestCount { get; private set; }

        public IAsyncEnumerable<CopilotTurnEvent> RunAsync(CopilotTurnRequest request, CancellationToken cancellationToken)
        {
            RequestCount++;
            throw new InvalidOperationException("The measurement regression must not admit a provider request.");
        }

        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message) => new(CopilotSteeringAdmissionReason.RuntimeUnavailable);
        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;
        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;
        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) => false;
        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(CopilotWorkspaceRollbackActionRequest request, Action<CopilotAgentEvent> onEvent, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class InMemoryStateStore(CopilotChatState state) : ICopilotChatStateStore
    {
        public string AttachmentDirectoryPath => string.Empty;
        public CopilotChatState Load() => state;
        public void Save(CopilotChatState value) { }
        public CopilotChatStateSnapshot CaptureSnapshot(CopilotChatState value) => new(new JObject());
        public string Serialize(CopilotChatStateSnapshot snapshot) => "{}";
        public string Serialize(CopilotChatState value) => "{}";
        public Task SaveSerializedAsync(string serializedState, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public int CleanupOrphanedAttachments(CopilotChatState value) => 0;
    }

    private sealed class IsolatedSolutionManagerScope : IDisposable
    {
        private static readonly FieldInfo InstanceField = typeof(SolutionManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? _previous = InstanceField.GetValue(null);
        private readonly SolutionManager _replacement = (SolutionManager)RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));

        public IsolatedSolutionManagerScope() => InstanceField.SetValue(null, _replacement);

        public void Dispose()
        {
            if (ReferenceEquals(InstanceField.GetValue(null), _replacement))
                InstanceField.SetValue(null, _previous);
        }
    }

    private sealed class TemporaryTextFile : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"colorvision-context-measurement-{Guid.NewGuid():N}.txt");

        public TemporaryTextFile() => File.WriteAllText(Path, "Context measurement attachment");

        public void Dispose() => File.Delete(Path);
    }
}
