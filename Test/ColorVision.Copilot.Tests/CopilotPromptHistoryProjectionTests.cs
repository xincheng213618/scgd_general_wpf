using ColorVision.Copilot;
using ColorVision.Solution;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotPromptHistoryProjectionTests
{
    [Fact]
    public void PrimaryActionToolTipBindingTracksNoMatchesAndScopeRecovery()
    {
        StaTest.Run(() =>
        {
            var current = CreateConversation("Current title", "Local historical request");
            var other = CreateConversation("Other title", "Remote historical request");
            using var fixture = new SearchFixture(current, other);
            var viewModel = fixture.ViewModel;
            var button = new Button();
            BindingOperations.SetBinding(button, Button.ToolTipProperty,
                new Binding(nameof(CopilotChatViewModel.PrimaryActionToolTip))
                {
                    Source = viewModel,
                    Mode = BindingMode.OneWay,
                });
            try
            {
                Assert.True(viewModel.TryOpenPromptHistorySearch());
                Assert.True(viewModel.HasPromptHistorySearchResults);
                Assert.Equal("把选中的历史请求恢复到输入框", button.ToolTip);

                viewModel.InputText = "Remote";

                Assert.False(viewModel.HasPromptHistorySearchResults);
                Assert.Equal("请修改历史搜索关键词", button.ToolTip);
                Assert.True(viewModel.TryTogglePromptHistorySearchScope());

                Assert.Equal("Remote historical request", Assert.Single(viewModel.PromptHistorySearchResults).Text);
                Assert.Equal("把选中的历史请求恢复到输入框", button.ToolTip);
            }
            finally
            {
                BindingOperations.ClearAllBindings(button);
            }
        });
    }

    [Fact]
    public void SearchQueriesAndScopeChangesKeepTheRealDraftPressureWithoutMeasuringHistoryAgain()
    {
        var conversation = CreateConversation("Current title", "inspect the historical request");
        conversation.DraftText = new string('中', 15_000);
        using var fixture = new SearchFixture(conversation);
        var viewModel = fixture.ViewModel;
        var expectedLabel = viewModel.ConversationContextUsageLabel;
        var expectedToolTip = viewModel.ConversationContextUsageToolTip;
        Assert.True(viewModel.IsConversationContextUnderPressure);
        Assert.True(viewModel.TryOpenPromptHistorySearch());
        Assert.Equal(expectedLabel, viewModel.ConversationContextUsageLabel);
        Assert.Equal(expectedToolTip, viewModel.ConversationContextUsageToolTip);
        Assert.True(viewModel.IsConversationContextUnderPressure);
        var measurements = ObserveContextMeasurements(viewModel);

        // Visible message DTOs are mutable. Overlay query changes search the new body,
        // but must not also rebuild the unrelated composer history measurement.
        conversation.Messages[0].Content = "inspect " + new string('x', 1_000);
        foreach (var query in new[] { "i", "in", "ins", "inspect" })
        {
            viewModel.InputText = query;
            Assert.Empty(measurements);
            Assert.Equal(expectedLabel, viewModel.ConversationContextUsageLabel);
            Assert.Equal(expectedToolTip, viewModel.ConversationContextUsageToolTip);
            Assert.True(viewModel.IsConversationContextUnderPressure);
        }
        Assert.True(viewModel.TryTogglePromptHistorySearchScope());
        Assert.True(viewModel.TryTogglePromptHistorySearchScope());

        Assert.Empty(measurements);
        Assert.Equal(expectedToolTip, viewModel.ConversationContextUsageToolTip);
        Assert.Equal(new string('中', 15_000), conversation.DraftText);
        viewModel.DismissPromptHistorySearch();
        Assert.Equal(conversation.DraftText, viewModel.InputText);
        Assert.True(viewModel.IsConversationContextUnderPressure);
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("message")]
    public void ChangesWhileSearchingRecalculateTheRealDraftInsteadOfTheQuery(string change)
    {
        var conversation = CreateConversation("Current title", "inspect history");
        conversation.DraftText = new string('中', 15_000);
        using var fixture = new SearchFixture(conversation);
        var viewModel = fixture.ViewModel;
        Assert.True(viewModel.TryOpenPromptHistorySearch());
        viewModel.InputText = "inspect";
        var beforeChange = viewModel.ConversationContextUsageToolTip;
        var measurements = ObserveContextMeasurements(viewModel);

        if (change == "profile")
            viewModel.SelectedProfile = fixture.ShortOutputProfile;
        else
            conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, new string('证', 1_000)));

        Assert.Contains(nameof(CopilotChatViewModel.ConversationContextUsageToolTip), measurements);
        Assert.NotEqual(beforeChange, viewModel.ConversationContextUsageToolTip);
        Assert.True(viewModel.IsConversationContextUnderPressure);
        Assert.Equal("inspect", viewModel.InputText);
        Assert.Equal(new string('中', 15_000), conversation.DraftText);
        var measuredDraftLabel = viewModel.ConversationContextUsageLabel;
        var measuredDraftToolTip = viewModel.ConversationContextUsageToolTip;
        measurements.Clear();
        viewModel.InputText = "inspect history";

        Assert.Empty(measurements);
        Assert.Equal(measuredDraftToolTip, viewModel.ConversationContextUsageToolTip);
        viewModel.DismissPromptHistorySearch();

        Assert.Equal(new string('中', 15_000), viewModel.InputText);
        Assert.Equal(measuredDraftLabel, viewModel.ConversationContextUsageLabel);
        Assert.Equal(measuredDraftToolTip, viewModel.ConversationContextUsageToolTip);
        Assert.True(viewModel.IsConversationContextUnderPressure);
    }

    [Fact]
    public void AcceptingHistoryMeasuresTheFullRestoredRequestAndNormalDraftEditsStillRefreshImmediately()
    {
        var historicalRequest = "inspect " + new string('x', CopilotPromptHistorySearch.MaximumPreviewCharacters + 100);
        var conversation = CreateConversation("Current title", historicalRequest);
        conversation.DraftText = new string('中', 15_000);
        using var fixture = new SearchFixture(conversation);
        var viewModel = fixture.ViewModel;
        Assert.True(viewModel.IsConversationContextUnderPressure);
        var largeDraftUsage = viewModel.ConversationContextUsageToolTip;
        Assert.True(viewModel.TryOpenPromptHistorySearch());
        viewModel.InputText = "inspect";
        var selected = Assert.Single(viewModel.PromptHistorySearchResults);
        Assert.NotEqual(selected.Text, selected.Preview);
        var measurements = ObserveContextMeasurements(viewModel);

        Assert.True(viewModel.TryCompletePromptHistorySearch(selected));

        Assert.Equal(historicalRequest, viewModel.InputText);
        Assert.Equal(historicalRequest, conversation.DraftText);
        Assert.False(viewModel.IsConversationContextUnderPressure);
        Assert.NotEqual(largeDraftUsage, viewModel.ConversationContextUsageToolTip);
        Assert.Contains(nameof(CopilotChatViewModel.ConversationContextUsageToolTip), measurements);
        var historicalRequestUsage = viewModel.ConversationContextUsageToolTip;
        measurements.Clear();

        viewModel.InputText = new string('中', 15_000);

        Assert.True(viewModel.IsConversationContextUnderPressure);
        Assert.Contains(nameof(CopilotChatViewModel.ConversationContextUsageToolTip), measurements);
        measurements.Clear();
        viewModel.InputText = historicalRequest;

        Assert.False(viewModel.IsConversationContextUnderPressure);
        Assert.Equal(historicalRequestUsage, viewModel.ConversationContextUsageToolTip);
        Assert.Contains(nameof(CopilotChatViewModel.ConversationContextUsageToolTip), measurements);
    }

    [Fact]
    public void OpeningWithoutCurrentRequestsFallsBackToVisibleHistoryAndRestoresTheDraft()
    {
        var current = CreateConversation("Current title");
        current.DraftText = "Keep this draft";
        current.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "An assistant answer"));
        current.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, " ") { RequestContent = "Hidden request body" });
        var other = CreateConversation("Other title", "Visible historical request");
        using var fixture = new SearchFixture(current, other);
        var viewModel = fixture.ViewModel;

        Assert.True(viewModel.TryOpenPromptHistorySearch());

        Assert.Equal("全部会话", viewModel.PromptHistorySearchScopeLabel);
        Assert.Equal("Visible historical request", Assert.Single(viewModel.PromptHistorySearchResults).Text);
        Assert.Equal("Keep this draft", current.DraftText);
        viewModel.DismissPromptHistorySearch();
        Assert.Equal("Keep this draft", viewModel.InputText);
        other.IsArchived = true;

        Assert.False(viewModel.TryOpenPromptHistorySearch());

        Assert.False(viewModel.IsPromptHistorySearchOpen);
        Assert.Empty(viewModel.PromptHistorySearchResults);
        Assert.Equal("Keep this draft", viewModel.InputText);
    }

    [Fact]
    public void EquivalentQueriesAndAssistantAppendsKeepResultAndSelectionReferences()
    {
        var conversation = CreateConversation("Current title", "inspect alpha", "inspect beta");
        conversation.DraftText = "Unsent review draft";
        conversation.DraftRequestMode = CopilotAgentMode.Review;
        conversation.DraftWorkspaceReviewTarget = CopilotWorkspaceReviewTargetContext.WorkingTree();
        using var fixture = new SearchFixture(conversation);
        var viewModel = fixture.ViewModel;
        Assert.True(viewModel.TryOpenPromptHistorySearch());
        var original = viewModel.PromptHistorySearchResults.ToArray();
        var selected = original.Single(item => item.Text == "inspect alpha");
        viewModel.SelectedPromptHistorySearchResult = selected;
        var changes = Observe(viewModel.PromptHistorySearchResults);
        var selectionNotifications = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(CopilotChatViewModel.SelectedPromptHistorySearchResult))
                selectionNotifications++;
        };

        viewModel.InputText = "inspect";
        viewModel.InputText = " INSPECT ";
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "A new assistant response"));

        Assert.Empty(changes);
        Assert.Equal(0, selectionNotifications);
        Assert.Same(selected, viewModel.SelectedPromptHistorySearchResult);
        for (var index = 0; index < original.Length; index++)
            Assert.Same(original[index], viewModel.PromptHistorySearchResults[index]);
        Assert.Equal("Unsent review draft", conversation.DraftText);
        Assert.Equal(CopilotAgentMode.Review, conversation.DraftRequestMode);
        Assert.NotNull(conversation.DraftWorkspaceReviewTarget);

        viewModel.DismissPromptHistorySearch();

        Assert.Empty(viewModel.PromptHistorySearchResults);
        Assert.Null(viewModel.SelectedPromptHistorySearchResult);
        Assert.Equal("Unsent review draft", viewModel.InputText);
        Assert.Equal(CopilotAgentMode.Review, conversation.DraftRequestMode);
    }

    [Fact]
    public void FilteringAndNewMatchingRequestsKeepSurvivorsAndSelectionWithoutReset()
    {
        var conversation = CreateConversation("Current title", "inspect alpha", "inspect beta");
        using var fixture = new SearchFixture(conversation);
        var viewModel = fixture.ViewModel;
        Assert.True(viewModel.TryOpenPromptHistorySearch());
        var selected = viewModel.PromptHistorySearchResults.Single(item => item.Text == "inspect alpha");
        viewModel.SelectedPromptHistorySearchResult = selected;
        var changes = Observe(viewModel.PromptHistorySearchResults);

        viewModel.InputText = "alpha";

        Assert.Same(selected, Assert.Single(viewModel.PromptHistorySearchResults));
        Assert.Same(selected, viewModel.SelectedPromptHistorySearchResult);
        Assert.Equal(NotifyCollectionChangedAction.Remove, Assert.Single(changes).Action);
        changes.Clear();

        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "alpha newest"));

        Assert.Equal(["alpha newest", "inspect alpha"], viewModel.PromptHistorySearchResults.Select(item => item.Text));
        Assert.Same(selected, viewModel.PromptHistorySearchResults[1]);
        Assert.Same(selected, viewModel.SelectedPromptHistorySearchResult);
        Assert.Equal(NotifyCollectionChangedAction.Add, Assert.Single(changes).Action);
        changes.Clear();

        viewModel.InputText = "no matching historical request";

        Assert.Empty(viewModel.PromptHistorySearchResults);
        Assert.Null(viewModel.SelectedPromptHistorySearchResult);
        Assert.False(viewModel.HasPromptHistorySearchResults);
        Assert.All(changes, change => Assert.Equal(NotifyCollectionChangedAction.Remove, change.Action));
    }

    [Theory]
    [InlineData("title", "Updated source · 2026-09-02 10:00")]
    [InlineData("time", "Original source · 2026-09-02 10:07")]
    [InlineData("latest", "Current title · 2026-09-03 11:00")]
    public void SameTextWithChangedSourceReplacesOnlyThatResultAndKeepsItSelected(string change, string expectedSource)
    {
        var current = CreateConversation("Current title", "inspect beta");
        var other = CreateConversation("Original source", "inspect alpha");
        other.Messages[0].CreatedAt = new DateTime(2026, 9, 2, 10, 0, 0);
        using var fixture = new SearchFixture(current, other);
        var viewModel = fixture.ViewModel;
        Assert.True(viewModel.TryOpenPromptHistorySearch());
        Assert.True(viewModel.TryTogglePromptHistorySearchScope());
        var original = viewModel.PromptHistorySearchResults.Single(item => item.Text == "inspect alpha");
        var survivor = viewModel.PromptHistorySearchResults.Single(item => item.Text == "inspect beta");
        viewModel.SelectedPromptHistorySearchResult = original;
        var changes = Observe(viewModel.PromptHistorySearchResults);

        if (change == "title")
            other.SetCustomTitle("Updated source");
        else if (change == "time")
            other.Messages[0].CreatedAt = other.Messages[0].CreatedAt.AddMinutes(7);
        else
            current.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "inspect alpha")
            {
                CreatedAt = new DateTime(2026, 9, 3, 11, 0, 0),
            });
        current.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Refresh the visible history"));

        var replacement = viewModel.PromptHistorySearchResults.Single(item => item.Text == original.Text);
        Assert.NotSame(original, replacement);
        Assert.Equal(expectedSource, replacement.SourceSummary);
        Assert.Same(replacement, viewModel.SelectedPromptHistorySearchResult);
        Assert.Same(survivor, viewModel.PromptHistorySearchResults.Single(item => item.Text == survivor.Text));
        Assert.DoesNotContain(changes, item => item.Action == NotifyCollectionChangedAction.Reset);
        Assert.NotEmpty(changes);
        Assert.Same(current, viewModel.SelectedConversation);

        Assert.True(viewModel.TryCompletePromptHistorySearch());

        Assert.Equal("inspect alpha", viewModel.InputText);
        Assert.Equal("inspect alpha", current.DraftText);
        Assert.False(viewModel.IsPromptHistorySearchOpen);
    }

    private static List<NotifyCollectionChangedEventArgs> Observe(ObservableCollection<CopilotPromptHistorySearchItem> collection)
    {
        var changes = new List<NotifyCollectionChangedEventArgs>();
        collection.CollectionChanged += (_, args) => changes.Add(args);
        return changes;
    }

    private static List<string> ObserveContextMeasurements(CopilotChatViewModel viewModel)
    {
        var notifications = new List<string>();
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(CopilotChatViewModel.ConversationContextUsageLabel)
                or nameof(CopilotChatViewModel.ConversationContextUsageToolTip)
                or nameof(CopilotChatViewModel.IsConversationContextUnderPressure)
                or nameof(CopilotChatViewModel.IsConversationContextReduced)
                or nameof(CopilotChatViewModel.ConversationContextCompactionToolTip))
            {
                notifications.Add(args.PropertyName);
            }
        };
        return notifications;
    }

    private static CopilotConversationRecord CreateConversation(string title, params string[] prompts)
    {
        var conversation = CopilotConversationRecord.CreateEmpty("history-projection-profile", "History projection profile");
        conversation.SetCustomTitle(title);
        for (var index = 0; index < prompts.Length; index++)
        {
            conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, prompts[index])
            {
                CreatedAt = new DateTime(2026, 9, 1, 9, index, 0),
            });
        }
        return conversation;
    }

    private sealed class SearchFixture : IDisposable
    {
        private static readonly FieldInfo InstanceField = typeof(SolutionManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? _previousInstance = InstanceField.GetValue(null);
        private readonly SolutionManager _testInstance = (SolutionManager)RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));
        private readonly CopilotAgentTaskHost _host = new();

        public SearchFixture(params CopilotConversationRecord[] conversations)
        {
            InstanceField.SetValue(null, _testInstance);
            var profile = new CopilotProfileConfig
            {
                Id = "history-projection-profile", Name = "History projection profile",
                VendorType = CopilotVendorType.Custom, ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "history-projection-test-key", BaseUrl = "https://unit.test/v1", Model = "test-model",
                MaxTokens = 8_192,
            };
            ShortOutputProfile = profile.Clone();
            ShortOutputProfile.Id = "history-short-output-profile";
            ShortOutputProfile.Name = "Short output profile";
            ShortOutputProfile.MaxTokens = 32;
            Config = new CopilotConfig
            {
                SchemaVersion = CopilotConfig.CurrentSchemaVersion,
                McpBearerToken = "history-projection-test-token",
                Profiles = [profile, ShortOutputProfile],
                AgentDefaults = new CopilotAgentDefaultsConfig
                {
                    ContextWindowTokens = 32_768,
                    AutoCompactConversationHistory = true,
                    AutoCompactThresholdPercent = 85,
                },
            };
            var state = new CopilotChatState
            {
                ActiveConversationId = conversations[0].Id,
                ActiveProfileId = profile.Id,
                Conversations = new ObservableCollection<CopilotConversationRecord>(conversations),
            };
            ViewModel = new CopilotChatViewModel(new CopilotChatService(), new MemoryStateStore(state), Config, new UnusedTurnRuntime(), _host);
        }

        public CopilotChatViewModel ViewModel { get; }
        public CopilotConfig Config { get; }
        public CopilotProfileConfig ShortOutputProfile { get; }

        public void Dispose()
        {
            _host.Shutdown();
            ViewModel.Dispose();
            if (ReferenceEquals(InstanceField.GetValue(null), _testInstance))
                InstanceField.SetValue(null, _previousInstance);
        }
    }

    private sealed class MemoryStateStore(CopilotChatState state) : ICopilotChatStateStore
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

    private sealed class UnusedTurnRuntime : ICopilotTurnRuntime
    {
        public async IAsyncEnumerable<CopilotTurnEvent> RunAsync(CopilotTurnRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            throw new InvalidOperationException("History projection tests must not start a model request.");
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }
        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message) => new(CopilotSteeringAdmissionReason.RuntimeUnavailable);
        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;
        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;
        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) => false;
        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(
            CopilotWorkspaceRollbackActionRequest request, Action<CopilotAgentEvent> onEvent, CancellationToken cancellationToken) =>
            Task.FromException<CopilotWorkspaceRollbackActionResult>(new NotSupportedException());
    }
}
