using ColorVision.Copilot;
using ColorVision.Solution;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotConversationFindNavigationTests
{
    [Theory]
    [InlineData(nameof(CopilotChatMessage.Content))]
    [InlineData(nameof(CopilotChatMessage.ReasoningContent))]
    [InlineData(nameof(CopilotChatMessage.ExecutionContent))]
    public void UnrelatedStreamChangesDoNotNavigateBackToTheUnchangedFindMatch(string propertyName)
    {
        Run(fixture =>
        {
            fixture.OpenFind("needle");
            Assert.Same(fixture.FirstMatch, fixture.ViewModel.CurrentConversationFindMatch);
            fixture.NavigationTargets.Clear();

            var property = typeof(CopilotChatMessage).GetProperty(propertyName)!;
            for (var index = 0; index < 8; index++)
                property.SetValue(fixture.StreamingMessage, $"Unrelated streamed text {index}");
            DrainDispatcher();

            Assert.Same(fixture.FirstMatch, fixture.ViewModel.CurrentConversationFindMatch);
            Assert.Equal("1 / 2", fixture.ViewModel.ConversationFindStatusText);
            Assert.DoesNotContain(fixture.FirstMatch, fixture.NavigationTargets);
        });
    }

    [Fact]
    public void ChangedSelectionAndExplicitFindNavigationStillBringTheRequestedMessageIntoView()
    {
        Run(fixture =>
        {
            fixture.OpenFind("needle");
            Assert.Contains(fixture.FirstMatch, fixture.NavigationTargets);

            fixture.AssertNavigatesTo(fixture.SecondMatch, () => Assert.True(fixture.ViewModel.MoveConversationFind(previous: false)));
            fixture.AssertNavigatesTo(fixture.FirstMatch, () => Assert.True(fixture.ViewModel.MoveConversationFind(previous: true)));
            fixture.AssertNavigatesTo(fixture.SecondMatch, () => fixture.ViewModel.ConversationFindText = "second needle");
            Assert.Equal("1 / 1", fixture.ViewModel.ConversationFindStatusText);

            // A deliberate next/previous/open remains a navigation even with a single match.
            fixture.AssertNavigatesTo(fixture.SecondMatch, () => Assert.True(fixture.ViewModel.MoveConversationFind(previous: false)));
            fixture.AssertNavigatesTo(fixture.SecondMatch, () => Assert.True(fixture.ViewModel.MoveConversationFind(previous: true)));
            fixture.AssertNavigatesTo(fixture.SecondMatch, fixture.ViewModel.OpenConversationFind);

            fixture.ViewModel.ConversationFindText = "needle";
            Assert.True(fixture.ViewModel.MoveConversationFind(previous: true));
            DrainDispatcher();
            Assert.Same(fixture.FirstMatch, fixture.ViewModel.CurrentConversationFindMatch);
            fixture.AssertNavigatesTo(fixture.SecondMatch, () => fixture.FirstMatch.Content = "This match was removed by a streamed edit");
            Assert.Same(fixture.SecondMatch, fixture.ViewModel.CurrentConversationFindMatch);
        });
    }

    [Fact]
    public void ExplicitTurnNavigationCanReturnToTheSameMessageRepeatedly()
    {
        Run(fixture =>
        {
            fixture.AssertNavigatesTo(fixture.FirstMatch, () => fixture.ExecuteLocalCommand("/turn 2"));
            fixture.AssertNavigatesTo(fixture.FirstMatch, () => fixture.ExecuteLocalCommand("/turn 2"));
            Assert.Same(fixture.Conversation, fixture.ViewModel.SelectedConversation);
            Assert.False(fixture.ViewModel.IsConversationFindOpen);
        });
    }

    [Fact]
    public void ClosingFindBeforeTheDispatcherRunsCancelsItsPendingNavigation()
    {
        Run(fixture =>
        {
            fixture.ViewModel.ConversationFindText = "needle";
            fixture.ViewModel.OpenConversationFind();
            fixture.ViewModel.CloseConversationFind();
            DrainDispatcher();

            Assert.False(fixture.ViewModel.IsConversationFindOpen);
            Assert.DoesNotContain(fixture.FirstMatch, fixture.NavigationTargets);
        });
    }

    [Fact]
    public void SwitchingConversationBeforeTheDispatcherRunsDoesNotNavigateToTheOldMessage()
    {
        Run(fixture =>
        {
            fixture.ViewModel.ConversationFindText = "needle";
            fixture.ViewModel.OpenConversationFind();
            Assert.True(fixture.ViewModel.TrySelectConversation(fixture.OtherConversation.Id));
            fixture.LayoutMessages();
            DrainDispatcher();

            Assert.Same(fixture.OtherConversation, fixture.ViewModel.SelectedConversation);
            Assert.DoesNotContain(fixture.FirstMatch, fixture.NavigationTargets);
            Assert.DoesNotContain(fixture.SecondMatch, fixture.NavigationTargets);
        });
    }

    [Fact]
    public void MultiplePendingFindNavigationsOnlyBringTheLatestSelectionIntoView()
    {
        Run(fixture =>
        {
            fixture.ViewModel.ConversationFindText = "needle";
            fixture.ViewModel.OpenConversationFind();
            Assert.True(fixture.ViewModel.MoveConversationFind(previous: false));
            DrainDispatcher();

            Assert.Same(fixture.SecondMatch, fixture.ViewModel.CurrentConversationFindMatch);
            Assert.DoesNotContain(fixture.FirstMatch, fixture.NavigationTargets);
            Assert.Contains(fixture.SecondMatch, fixture.NavigationTargets);
        });
    }

    private static void Run(Action<FindFixture> action)
    {
        StaTest.Run(() =>
        {
            try
            {
                using var fixture = new FindFixture();
                action(fixture);
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
    }

    private static void DrainDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    private sealed class FindFixture : IDisposable
    {
        private static readonly FieldInfo InstanceField = typeof(SolutionManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private static readonly MethodInfo ExecuteCommand = typeof(CopilotChatViewModel).GetMethod("TryExecuteLocalCommand", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly object? _previousInstance = InstanceField.GetValue(null);
        private readonly SolutionManager _testInstance = (SolutionManager)RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));
        private readonly CopilotAgentTaskHost _host = new();
        private readonly CopilotChatPanel _panel;
        private readonly ListBox _messages;

        public FindFixture()
        {
            InstanceField.SetValue(null, _testInstance);
            var profile = new CopilotProfileConfig
            {
                Id = "find-navigation-profile", Name = "Find navigation profile",
                VendorType = CopilotVendorType.Custom, ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "find-navigation-test-key", BaseUrl = "https://unit.test/v1", Model = "test-model",
            };
            Conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.Name);
            Conversation.Messages.Add(FirstMatch);
            Conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "First answer"));
            Conversation.Messages.Add(SecondMatch);
            Conversation.Messages.Add(StreamingMessage);
            OtherConversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.Name);
            OtherConversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Other conversation"));
            OtherConversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Other answer"));
            var state = new CopilotChatState
            {
                ActiveConversationId = Conversation.Id,
                ActiveProfileId = profile.Id,
                Conversations = new ObservableCollection<CopilotConversationRecord> { Conversation, OtherConversation },
            };
            var config = new CopilotConfig
            {
                SchemaVersion = CopilotConfig.CurrentSchemaVersion,
                McpBearerToken = "find-navigation-test-token", Profiles = [profile],
            };
            ViewModel = new CopilotChatViewModel(new CopilotChatService(), new MemoryStateStore(state), config, new UnusedTurnRuntime(), _host);
            _panel = new CopilotChatPanel { DataContext = ViewModel };
            _messages = (ListBox)_panel.FindName("MessagesListBox");

            // Keep the production panel subscriptions and navigation, with inexpensive realized rows.
            var text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetBinding(TextBlock.TextProperty, new Binding(nameof(CopilotChatMessage.Content)));
            text.SetValue(FrameworkElement.HeightProperty, 80d);
            _messages.ItemTemplate = new DataTemplate { VisualTree = text };
            _messages.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(StackPanel)));
            VirtualizingPanel.SetIsVirtualizing(_messages, false);
            ScrollViewer.SetCanContentScroll(_messages, false);
            _messages.AddHandler(FrameworkElement.RequestBringIntoViewEvent, new RequestBringIntoViewEventHandler((_, args) =>
            {
                if (args.TargetObject is ListBoxItem { DataContext: CopilotChatMessage message })
                    NavigationTargets.Add(message);
            }), handledEventsToo: true);
            LayoutMessages();
            DrainDispatcher();
            NavigationTargets.Clear();
        }

        public CopilotChatViewModel ViewModel { get; }
        public CopilotConversationRecord Conversation { get; }
        public CopilotConversationRecord OtherConversation { get; }
        public CopilotChatMessage FirstMatch { get; } = new(CopilotChatRole.User, "first needle");
        public CopilotChatMessage SecondMatch { get; } = new(CopilotChatRole.User, "second needle");
        public CopilotChatMessage StreamingMessage { get; } = new(CopilotChatRole.Assistant, "Streaming answer");
        public List<CopilotChatMessage> NavigationTargets { get; } = [];

        public void LayoutMessages()
        {
            _messages.ApplyTemplate();
            _messages.Measure(new Size(600, 160));
            _messages.Arrange(new Rect(0, 0, 600, 160));
            _messages.UpdateLayout();
            Assert.All(ViewModel.Messages, message => Assert.IsType<ListBoxItem>(_messages.ItemContainerGenerator.ContainerFromItem(message)));
        }

        public void OpenFind(string query)
        {
            ViewModel.ConversationFindText = query;
            ViewModel.OpenConversationFind();
            DrainDispatcher();
        }

        public void AssertNavigatesTo(CopilotChatMessage message, Action navigate)
        {
            NavigationTargets.Clear();
            navigate();
            DrainDispatcher();
            Assert.Contains(message, NavigationTargets);
        }

        public void ExecuteLocalCommand(string prompt) => Assert.True((bool)ExecuteCommand.Invoke(ViewModel, [prompt, false, null])!);

        public void Dispose()
        {
            _panel.DataContext = null;
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
            throw new InvalidOperationException("Find navigation tests must not start a model request.");
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
