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

public sealed class CopilotConversationListProjectionTests
{
    [Fact]
    public void AppendingToLongConversationPreservesSidebarItemsAndMessageAnchors()
    {
        var selected = CreateConversation("selected");
        for (var index = 0; index < 1_000; index++)
        {
            selected.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, $"Request {index}"));
            selected.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, $"Answer {index}"));
        }
        using var fixture = new ProjectionFixture(selected, CreateConversation("history-one"), CreateConversation("history-two"));
        var viewModel = fixture.ViewModel;
        var filtered = viewModel.FilteredConversations.ToArray();
        var compact = viewModel.CompactHistoryConversations.ToArray();
        var anchor = selected.Messages[1];
        var filteredChanges = Observe(viewModel.FilteredConversations);
        var compactChanges = Observe(viewModel.CompactHistoryConversations);
        var messageChanges = Observe(selected.Messages);

        selected.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Continue the task"));
        var response = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        selected.Messages.Add(response);
        CopilotAssistantMessagePresenter.ApplyStreamDelta(response, new CopilotStreamDelta(string.Empty, "New answer"));

        Assert.Empty(filteredChanges);
        Assert.Empty(compactChanges);
        Assert.Equal(filtered, viewModel.FilteredConversations);
        Assert.Equal(compact, viewModel.CompactHistoryConversations);
        Assert.Same(selected, viewModel.SelectedConversation);
        Assert.Same(selected.Messages, viewModel.Messages);
        Assert.Same(anchor, selected.Messages[1]);
        Assert.Equal(2, messageChanges.Count);
        Assert.All(messageChanges, change => Assert.Equal(NotifyCollectionChangedAction.Add, change.Action));
        Assert.Equal("New answer", response.Content);
    }

    [Fact]
    public void PinningReordersExistingRowsWithoutRemovingThemOrChangingSelection()
    {
        var selected = CreateConversation("selected");
        var first = CreateConversation("first");
        var pinned = CreateConversation("pin-this");
        using var fixture = new ProjectionFixture(selected, first, pinned);
        var viewModel = fixture.ViewModel;
        var filteredChanges = Observe(viewModel.FilteredConversations);
        var compactChanges = Observe(viewModel.CompactHistoryConversations);

        viewModel.TogglePinConversationCommand.Execute(pinned);

        Assert.True(pinned.IsPinned);
        Assert.Equal([pinned, selected, first], viewModel.FilteredConversations);
        Assert.Equal([pinned, first], viewModel.CompactHistoryConversations);
        Assert.NotEmpty(filteredChanges);
        Assert.NotEmpty(compactChanges);
        Assert.All(filteredChanges, change => Assert.Equal(NotifyCollectionChangedAction.Move, change.Action));
        Assert.All(compactChanges, change => Assert.Equal(NotifyCollectionChangedAction.Move, change.Action));
        Assert.Same(selected, viewModel.SelectedConversation);
    }

    [Fact]
    public void HistoryWindowTracksInsertRemoveAndArchiveWithoutResettingSurvivingRows()
    {
        var selected = CreateConversation("selected");
        var history = Enumerable.Range(1, 5).Select(index => CreateConversation($"history-{index}")).ToArray();
        using var fixture = new ProjectionFixture([selected, .. history]);
        var viewModel = fixture.ViewModel;
        Assert.Equal(history.Take(4), viewModel.CompactHistoryConversations);
        Assert.True(viewModel.HasCompactHistoryOverflow);
        var filteredChanges = Observe(viewModel.FilteredConversations);
        var compactChanges = Observe(viewModel.CompactHistoryConversations);
        var inserted = CreateConversation("inserted");

        viewModel.Conversations.Insert(1, inserted);
        Assert.Equal([inserted, history[0], history[1], history[2]], viewModel.CompactHistoryConversations);
        viewModel.Conversations.Remove(inserted);
        Assert.Equal(history.Take(4), viewModel.CompactHistoryConversations);

        history[0].IsArchived = true;
        selected.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Refresh after archive"));

        Assert.DoesNotContain(history[0], viewModel.FilteredConversations);
        Assert.Equal(history.Skip(1), viewModel.CompactHistoryConversations);
        Assert.False(viewModel.HasCompactHistoryOverflow);
        Assert.DoesNotContain(filteredChanges, change => change.Action == NotifyCollectionChangedAction.Reset);
        Assert.DoesNotContain(compactChanges, change => change.Action == NotifyCollectionChangedAction.Reset);
        Assert.Same(selected, viewModel.SelectedConversation);
    }

    [Fact]
    public void SearchRefreshPublishesOnlyChangedPreviewAndKeepsMatchingRow()
    {
        var selected = CreateConversation("selected");
        var unrelated = CreateConversation("unrelated");
        using var fixture = new ProjectionFixture(selected, unrelated);
        var viewModel = fixture.ViewModel;
        var matchingMessage = new CopilotChatMessage(CopilotChatRole.User, "needle original detail");
        selected.Messages.Add(matchingMessage);
        viewModel.ConversationSearchText = "needle";
        Assert.True(viewModel.FlushConversationSearchRefresh());
        Assert.Same(selected, Assert.Single(viewModel.FilteredConversations));
        Assert.Equal("历史消息 · needle original detail", selected.SearchMatchPreviewText);
        var filteredChanges = Observe(viewModel.FilteredConversations);
        var previews = new List<string>();
        selected.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(CopilotConversationRecord.SearchMatchPreviewText))
                previews.Add(selected.SearchMatchPreviewText);
        };

        selected.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "No matching words"));

        Assert.Empty(filteredChanges);
        Assert.Empty(previews);
        matchingMessage.Content = "needle updated detail";
        selected.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Refresh with the changed historical message"));

        Assert.Empty(filteredChanges);
        Assert.Equal(["历史消息 · needle updated detail"], previews);
        Assert.Same(selected, viewModel.SelectedConversation);

        viewModel.ConversationSearchText = "no-such-search-term";
        Assert.True(viewModel.FlushConversationSearchRefresh());
        Assert.Empty(viewModel.FilteredConversations);
        Assert.Empty(selected.SearchMatchPreviewText);
        Assert.True(viewModel.HasNoConversationSearchResults);
        Assert.Same(selected, viewModel.SelectedConversation);
        viewModel.ClearConversationSearchCommand.Execute(null);
        Assert.Equal([selected, unrelated], viewModel.FilteredConversations);
        Assert.DoesNotContain(filteredChanges, change => change.Action == NotifyCollectionChangedAction.Reset);
    }

    [Fact]
    public void ActivityFilteringClearsExcludedPreviewsAndPreservesPriorityAfterAcknowledgement()
    {
        var selected = CreateConversation("match-selected");
        var ready = CreateActivityConversation("match-ready", CopilotConversationActivityState.Ready, CopilotAgentStopReason.Completed);
        var blocked = CreateActivityConversation("match-blocked", CopilotConversationActivityState.Blocked, CopilotAgentStopReason.Blocked);
        var needsInput = CreateActivityConversation("match-needs-input", CopilotConversationActivityState.NeedsInput, CopilotAgentStopReason.Paused);
        using var fixture = new ProjectionFixture(selected, ready, blocked, needsInput);
        var viewModel = fixture.ViewModel;
        viewModel.ConversationSearchText = "match";
        Assert.True(viewModel.FlushConversationSearchRefresh());
        Assert.NotEmpty(selected.SearchMatchPreviewText);
        var changes = Observe(viewModel.FilteredConversations);

        viewModel.ToggleActivityViewCommand.Execute(null);

        Assert.Equal([needsInput, blocked, ready], viewModel.FilteredConversations);
        Assert.Empty(selected.SearchMatchPreviewText);
        Assert.Same(selected, viewModel.SelectedConversation);
        Assert.All(viewModel.FilteredConversations, conversation => Assert.NotEmpty(conversation.SearchMatchPreviewText));

        viewModel.MarkAllActivityReadCommand.Execute(null);

        Assert.Same(needsInput, Assert.Single(viewModel.FilteredConversations));
        Assert.Empty(ready.SearchMatchPreviewText);
        Assert.Empty(blocked.SearchMatchPreviewText);
        Assert.Equal(CopilotConversationActivityState.NeedsInput, needsInput.AgentActivity?.State);
        Assert.Same(selected, viewModel.SelectedConversation);
        viewModel.ToggleActivityViewCommand.Execute(null);
        Assert.Equal([selected, ready, blocked, needsInput], viewModel.FilteredConversations);
        Assert.DoesNotContain(changes, change => change.Action == NotifyCollectionChangedAction.Reset);
    }

    [Fact]
    public void RetainedTaskRowsRefreshActualBindingsWithoutRebuildingTheList()
    {
        StaTest.Run(() =>
        {
            var selected = CreateConversation("selected");
            var taskConversation = CreateConversation("task");
            using var fixture = new ProjectionFixture(selected, taskConversation);
            var viewModel = fixture.ViewModel;
            var message = CreatePausedTaskMessage();
            taskConversation.Messages.Add(message);
            selected.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Refresh tasks"));
            var task = Assert.Single(viewModel.AgentTasks);
            var changes = Observe(viewModel.AgentTasks);
            var title = BindTaskText(task, nameof(CopilotAgentTaskSummary.Title));
            var status = BindTaskText(task, nameof(CopilotAgentTaskSummary.StatusLabel));
            var detail = BindTaskText(task, nameof(CopilotAgentTaskSummary.DetailLabel));
            var remaining = BindTaskText(task, nameof(CopilotAgentTaskSummary.RemainingCount));
            var resume = new CheckBox();
            BindingOperations.SetBinding(resume, CheckBox.IsCheckedProperty,
                new Binding(nameof(CopilotAgentTaskSummary.CanResume)) { Source = task, Mode = BindingMode.OneWay });
            try
            {
                Assert.Equal("task", title.Text);
                Assert.Equal("已暂停", status.Text);
                Assert.Equal("剩余 1 项", detail.Text);
                Assert.False(resume.IsChecked);

                taskConversation.SetCustomTitle("Renamed task");
                taskConversation.AgentSessionCheckpoint = new CopilotAgentSessionCheckpoint();
                message.AgentStopReason = CopilotAgentStopReason.ProviderFailure;
                message.AgentTaskLedger = new CopilotAgentTaskLedgerSnapshot
                {
                    Mode = "execute",
                    Items = [new() { Id = 1, Title = "First" }, new() { Id = 2, Title = "Second" }],
                };
                message.AgentBlockers = [new CopilotAgentBlockerSnapshot
                {
                    Kind = CopilotAgentBlockerKind.UserDecision, Code = "choose_target", Summary = "Choose a target",
                }];
                selected.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Refresh changed task"));

                Assert.Same(task, Assert.Single(viewModel.AgentTasks));
                Assert.Empty(changes);
                Assert.Equal("Renamed task", title.Text);
                Assert.Equal("模型连接中断", status.Text);
                Assert.Equal("Choose a target", detail.Text);
                Assert.Equal("2", remaining.Text);
                Assert.True(resume.IsChecked);
                taskConversation.SetAgentSessionCheckpoint(null);
                selected.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Refresh removed checkpoint"));
                Assert.False(resume.IsChecked);
                Assert.Empty(changes);
                Assert.Same(selected, viewModel.SelectedConversation);
            }
            finally
            {
                foreach (var target in new System.Windows.DependencyObject[] { title, status, detail, remaining, resume })
                    BindingOperations.ClearAllBindings(target);
            }
        });
    }

    [Fact]
    public void RenameCommandUpdatesTheExistingTaskRowImmediately()
    {
        StaTest.Run(() =>
        {
            var conversation = CreateConversation("Original title");
            using var fixture = new ProjectionFixture(conversation);
            var viewModel = fixture.ViewModel;
            conversation.Messages.Add(CreatePausedTaskMessage());
            var originalMessages = conversation.Messages.ToArray();
            var task = Assert.Single(viewModel.AgentTasks);
            var changes = Observe(viewModel.AgentTasks);
            var title = BindTaskText(task, nameof(CopilotAgentTaskSummary.Title));
            try
            {
                viewModel.InputText = "/rename New task title";
                viewModel.SendCommand.Execute(null);

                Assert.Equal("New task title", title.Text);
                Assert.Equal("New task title", conversation.Title);
                Assert.Same(task, Assert.Single(viewModel.AgentTasks));
                Assert.Empty(changes);
                Assert.Empty(viewModel.InputText);
                Assert.Equal(originalMessages, conversation.Messages);
            }
            finally
            {
                BindingOperations.ClearAllBindings(title);
            }
        });
    }

    [Fact]
    public void TaskRowsMoveByRecencyReplaceNewMessagesAndRemoveUnavailableTasks()
    {
        var selected = CreateConversation("selected");
        var first = CreateConversation("first-task");
        var second = CreateConversation("second-task");
        using var fixture = new ProjectionFixture(selected, first, second);
        var viewModel = fixture.ViewModel;
        var firstMessage = CreatePausedTaskMessage();
        var secondMessage = CreatePausedTaskMessage();
        first.Messages.Add(firstMessage);
        second.Messages.Add(secondMessage);
        first.UpdatedAt = DateTime.Today;
        second.UpdatedAt = DateTime.Today.AddMinutes(-1);
        selected.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Refresh tasks"));
        var firstRow = viewModel.AgentTasks[0];
        var secondRow = viewModel.AgentTasks[1];
        var changes = Observe(viewModel.AgentTasks);

        second.UpdatedAt = first.UpdatedAt.AddMinutes(1);
        selected.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Refresh ordering"));
        Assert.Equal([secondRow, firstRow], viewModel.AgentTasks);
        Assert.Equal(NotifyCollectionChangedAction.Move, Assert.Single(changes).Action);

        var newMessage = CreatePausedTaskMessage();
        first.Messages.Add(newMessage);
        selected.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Refresh successor"));
        Assert.Same(secondRow, viewModel.AgentTasks[0]);
        Assert.NotSame(firstRow, viewModel.AgentTasks[1]);
        Assert.Same(newMessage, viewModel.AgentTasks[1].Message);
        newMessage.IsAgentRecoveryDismissed = true;
        selected.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Refresh dismissed task"));
        Assert.Same(secondRow, Assert.Single(viewModel.AgentTasks));
        second.IsArchived = true;
        selected.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Refresh archive"));
        Assert.Empty(viewModel.AgentTasks);
        Assert.DoesNotContain(changes, change => change.Action == NotifyCollectionChangedAction.Reset);
        Assert.Same(selected, viewModel.SelectedConversation);
    }

    private static TextBlock BindTaskText(CopilotAgentTaskSummary task, string property)
    {
        var target = new TextBlock();
        BindingOperations.SetBinding(target, TextBlock.TextProperty, new Binding(property) { Source = task });
        return target;
    }

    private static CopilotChatMessage CreatePausedTaskMessage() => new(CopilotChatRole.Assistant, "Paused task")
    {
        RequestMode = CopilotAgentMode.Auto,
        AgentStopReason = CopilotAgentStopReason.Paused,
        AgentTaskLedger = new CopilotAgentTaskLedgerSnapshot
        {
            Mode = "execute", Items = [new CopilotAgentTaskItem { Id = 1, Title = "Pending task" }],
        },
    };

    private static List<NotifyCollectionChangedEventArgs> Observe<T>(ObservableCollection<T> collection)
    {
        var changes = new List<NotifyCollectionChangedEventArgs>();
        collection.CollectionChanged += (_, args) => changes.Add(args);
        return changes;
    }

    private static CopilotConversationRecord CreateConversation(string title)
    {
        var conversation = CopilotConversationRecord.CreateEmpty("projection-profile", "Projection profile");
        conversation.SetCustomTitle(title);
        conversation.CreatedAt = conversation.UpdatedAt = conversation.RecencyAt = new DateTime(2026, 9, 1, 12, 0, 0);
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, $"Initial request for {title}"));
        return conversation;
    }

    private static CopilotConversationRecord CreateActivityConversation(
        string title, CopilotConversationActivityState state, CopilotAgentStopReason stopReason)
    {
        var conversation = CreateConversation(title);
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Task result")
        {
            RequestMode = CopilotAgentMode.Auto,
            AgentStopReason = stopReason,
        };
        conversation.Messages.Add(assistant);
        conversation.AgentActivity = CopilotConversationActivity.Create(state, assistant.Id, DateTimeOffset.UtcNow);
        return conversation;
    }

    private sealed class ProjectionFixture : IDisposable
    {
        private static readonly FieldInfo InstanceField = typeof(SolutionManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? _previousInstance = InstanceField.GetValue(null);
        private readonly SolutionManager _testInstance = (SolutionManager)RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));
        private readonly CopilotAgentTaskHost _host = new();

        public CopilotChatViewModel ViewModel { get; }

        public ProjectionFixture(params CopilotConversationRecord[] conversations)
        {
            InstanceField.SetValue(null, _testInstance);
            var profile = new CopilotProfileConfig
            {
                Id = "projection-profile", Name = "Projection profile",
                VendorType = CopilotVendorType.Custom, ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "projection-test-key", BaseUrl = "https://unit.test/v1", Model = "test-model",
            };
            var config = new CopilotConfig
            {
                SchemaVersion = CopilotConfig.CurrentSchemaVersion,
                McpBearerToken = "projection-test-token",
                Profiles = [profile],
            };
            var state = new CopilotChatState
            {
                ActiveConversationId = conversations[0].Id,
                ActiveProfileId = profile.Id,
                Conversations = new ObservableCollection<CopilotConversationRecord>(conversations),
            };
            ViewModel = new CopilotChatViewModel(new CopilotChatService(), new MemoryStateStore(state), config, new UnusedTurnRuntime(), _host);
        }

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
        public Task SaveSerializedAsync(string serializedState, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
        public int CleanupOrphanedAttachments(CopilotChatState value) => 0;
    }

    private sealed class UnusedTurnRuntime : ICopilotTurnRuntime
    {
        public async IAsyncEnumerable<CopilotTurnEvent> RunAsync(CopilotTurnRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            throw new InvalidOperationException("List projection tests must not start a model request.");
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
