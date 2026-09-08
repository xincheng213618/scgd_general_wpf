using ColorVision.Copilot;
using ColorVision.Solution;
using Newtonsoft.Json.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotChatPanelScrollLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ScrollingUpBeforeThePendingFollowRunsKeepsTheReadersPosition(bool useVirtualization)
    {
        Run(fixture =>
        {
            var upwardScrollEvents = 0;
            fixture.ScrollViewer.ScrollChanged += (_, args) =>
            {
                if (args.VerticalChange < 0)
                    upwardScrollEvents++;
            };
            fixture.GrowStreamingAnswer();
            fixture.ScrollViewer.ScrollToTop();
            fixture.LayoutMessages();
            DrainDispatcher(DispatcherPriority.Input);
            fixture.LayoutMessages();
            Assert.True(upwardScrollEvents > 0, "The real ScrollChanged event must precede the pending Background follow.");
            Assert.InRange(fixture.ScrollViewer.VerticalOffset, 0, 1);
            fixture.NavigationTargets.Clear();

            fixture.Settle();

            Assert.InRange(fixture.ScrollViewer.VerticalOffset, 0, 1);
            Assert.DoesNotContain(fixture.StreamingMessage, fixture.NavigationTargets);
        }, useVirtualization);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitHistoryNavigationWinsOverAnAlreadyPendingBottomFollow(bool useFind)
    {
        Run(fixture =>
        {
            fixture.GrowStreamingAnswer();
            if (useFind)
            {
                fixture.ViewModel.ConversationFindText = "Original needle";
                fixture.ViewModel.OpenConversationFind();
            }
            else
            {
                fixture.ExecuteLocalCommand("/turn 6");
            }

            fixture.Settle();

            Assert.Contains(fixture.FirstMessage, fixture.NavigationTargets);
            Assert.DoesNotContain(fixture.StreamingMessage, fixture.NavigationTargets);
            Assert.InRange(fixture.ScrollViewer.VerticalOffset, 0, 1);
        });
    }

    [Fact]
    public void UnloadingThePanelInvalidatesItsPendingBottomFollow()
    {
        Run(fixture =>
        {
            fixture.GrowStreamingAnswer();
            fixture.Panel.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent, fixture.Panel));
            fixture.ScrollViewer.ScrollToTop();
            fixture.LayoutMessages();
            DrainDispatcher(DispatcherPriority.Input);
            fixture.LayoutMessages();
            Assert.InRange(fixture.ScrollViewer.VerticalOffset, 0, 1);
            fixture.NavigationTargets.Clear();

            fixture.Settle();

            Assert.Empty(fixture.NavigationTargets);
            Assert.InRange(fixture.ScrollViewer.VerticalOffset, 0, 1);
        });
    }

    [Fact]
    public void ReplacingTheViewModelDoesNotLetTheOldFollowOverrideTheNewViewsNavigation()
    {
        Run(fixture =>
        {
            fixture.GrowStreamingAnswer();
            var replacement = fixture.CreateViewModel("Replacement");
            fixture.Panel.DataContext = replacement;
            fixture.LayoutMessages();
            replacement.ConversationFindText = "Replacement needle";
            replacement.OpenConversationFind();

            fixture.Settle();

            Assert.Same(replacement, fixture.Panel.DataContext);
            Assert.Contains(replacement.Messages[0], fixture.NavigationTargets);
            Assert.DoesNotContain(replacement.Messages[^1], fixture.NavigationTargets);
            Assert.DoesNotContain(fixture.StreamingMessage, fixture.NavigationTargets);
            Assert.InRange(fixture.ScrollViewer.VerticalOffset, 0, 1);
        });
    }

    [Fact]
    public void ClosingAPendingFindAllowsTheNextStreamUpdateToFollowTheBottom()
    {
        Run(fixture =>
        {
            fixture.ViewModel.ConversationFindText = "Original needle";
            fixture.ViewModel.OpenConversationFind();
            fixture.ViewModel.CloseConversationFind();
            fixture.GrowStreamingAnswer();

            fixture.Settle();

            Assert.False(fixture.ViewModel.IsConversationFindOpen);
            Assert.DoesNotContain(fixture.FirstMessage, fixture.NavigationTargets);
            fixture.AssertAtBottom();
        });
    }

    [Fact]
    public void ClosingFindDoesNotCancelANewerExplicitTurnNavigation()
    {
        Run(fixture =>
        {
            var requestedTurn = fixture.ViewModel.Messages[6];
            fixture.ViewModel.ConversationFindText = "Original needle";
            fixture.ViewModel.OpenConversationFind();
            fixture.ExecuteLocalCommand("/turn 3");
            fixture.ViewModel.CloseConversationFind();

            fixture.Settle();

            Assert.False(fixture.ViewModel.IsConversationFindOpen);
            Assert.Contains(requestedTurn, fixture.NavigationTargets);
            Assert.DoesNotContain(fixture.FirstMessage, fixture.NavigationTargets);
            Assert.DoesNotContain(fixture.StreamingMessage, fixture.NavigationTargets);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnUninterruptedStreamStillFollowsTheGrowingLastMessage(bool useVirtualization)
    {
        Run(fixture =>
        {
            var originalExtent = fixture.ScrollViewer.ExtentHeight;

            fixture.GrowStreamingAnswer();
            fixture.Settle();

            Assert.True(fixture.ScrollViewer.ExtentHeight > originalExtent + 100);
            fixture.AssertAtBottom();
        }, useVirtualization);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DelayedRowGrowthFollowsTheNewExtentWhenTheReaderWasAtTheBottom(bool useFiniteOffset)
    {
        Run(fixture =>
        {
            if (useFiniteOffset)
                fixture.ScrollViewer.ScrollToVerticalOffset(fixture.ScrollViewer.ScrollableHeight);
            else
                fixture.ScrollViewer.ScrollToEnd();
            fixture.Settle();
            fixture.AssertAtBottom();
            var content = fixture.StreamingMessage.Content;
            var originalExtent = fixture.ScrollViewer.ExtentHeight;

            // Simulate a later layout change without another Content event; this is not Markdown rendering.
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Loaded, () => fixture.IncreaseLastRealizedRowHeight(400));
            fixture.Settle();

            Assert.Equal(content, fixture.StreamingMessage.Content);
            Assert.True(fixture.ScrollViewer.ExtentHeight > originalExtent + 300);
            fixture.AssertAtBottom();
        });
    }

    [Fact]
    public void DelayedRowGrowthPreservesTheOffsetWhenTheReaderHasScrolledUp()
    {
        Run(fixture =>
        {
            fixture.ScrollViewer.ScrollToVerticalOffset(240);
            fixture.Settle();
            var readingOffset = fixture.ScrollViewer.VerticalOffset;
            Assert.True(fixture.ScrollViewer.ScrollableHeight - readingOffset > 100);
            var content = fixture.StreamingMessage.Content;
            var originalExtent = fixture.ScrollViewer.ExtentHeight;
            fixture.NavigationTargets.Clear();

            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Loaded, () => fixture.IncreaseLastRealizedRowHeight(400));
            fixture.Settle();

            Assert.Equal(content, fixture.StreamingMessage.Content);
            Assert.True(fixture.ScrollViewer.ExtentHeight > originalExtent + 300);
            Assert.Equal(readingOffset, fixture.ScrollViewer.VerticalOffset, precision: 3);
            Assert.Empty(fixture.NavigationTargets);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitReturnToLatestStillScrollsToTheBottomFromAnEarlierPosition(bool useVirtualization)
    {
        Run(fixture =>
        {
            fixture.ScrollViewer.ScrollToTop();
            fixture.Settle();
            Assert.InRange(fixture.ScrollViewer.VerticalOffset, 0, 1);
            if (useVirtualization)
                fixture.AssertMessageNotRealized(fixture.StreamingMessage);
            var button = (Button)fixture.Panel.FindName("ScrollToLatestButton");
            Assert.Equal(Visibility.Visible, button.Visibility);

            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
            fixture.Settle();

            fixture.AssertAtBottom();
            fixture.AssertMessageVisible(fixture.StreamingMessage);
            Assert.Equal(Visibility.Collapsed, button.Visibility);
        }, useVirtualization);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ANewUserMessageStillMovesFromTheReadersPositionToTheLatestMessage(bool useVirtualization)
    {
        Run(fixture =>
        {
            fixture.ScrollViewer.ScrollToTop();
            fixture.Settle();
            Assert.InRange(fixture.ScrollViewer.VerticalOffset, 0, 1);
            if (useVirtualization)
                fixture.AssertMessageNotRealized(fixture.StreamingMessage);
            var message = new CopilotChatMessage(CopilotChatRole.User, "The next submitted request");

            fixture.ViewModel.Messages.Add(message);
            fixture.Settle();

            fixture.AssertAtBottom();
            fixture.AssertMessageVisible(message);
        }, useVirtualization);
    }

    private static void Run(Action<ScrollFixture> action, bool useVirtualization = false)
    {
        StaTest.Run(() =>
        {
            try
            {
                using var fixture = new ScrollFixture(useVirtualization);
                action(fixture);
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
    }

    private static void DrainDispatcher(DispatcherPriority priority = DispatcherPriority.ContextIdle)
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(priority, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    private sealed class ScrollFixture : IDisposable
    {
        private static readonly FieldInfo InstanceField = typeof(SolutionManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private static readonly MethodInfo ExecuteCommand = typeof(CopilotChatViewModel).GetMethod("TryExecuteLocalCommand", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly object? _previousInstance = InstanceField.GetValue(null);
        private readonly SolutionManager _testInstance = (SolutionManager)RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));
        private readonly List<CopilotChatViewModel> _viewModels = [];
        private readonly List<CopilotAgentTaskHost> _hosts = [];
        private readonly ListBox _messages;
        private readonly bool _useVirtualization;

        public ScrollFixture(bool useVirtualization)
        {
            _useVirtualization = useVirtualization;
            InstanceField.SetValue(null, _testInstance);
            ViewModel = CreateViewModel("Original");
            Panel = new CopilotChatPanel { DataContext = ViewModel };
            _messages = (ListBox)Panel.FindName("MessagesListBox");
            var text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetBinding(TextBlock.TextProperty, new Binding(nameof(CopilotChatMessage.Content)));
            text.SetValue(FrameworkElement.MinHeightProperty, 80d);
            text.SetValue(FrameworkElement.WidthProperty, 400d);
            text.SetValue(TextBlock.FontSizeProperty, 16d);
            text.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            _messages.ItemTemplate = new DataTemplate { VisualTree = text };
            _messages.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(
                useVirtualization ? typeof(VirtualizingStackPanel) : typeof(StackPanel)));
            VirtualizingPanel.SetIsVirtualizing(_messages, useVirtualization);
            VirtualizingPanel.SetVirtualizationMode(_messages, VirtualizationMode.Recycling);
            VirtualizingPanel.SetScrollUnit(_messages, ScrollUnit.Pixel);
            System.Windows.Controls.ScrollViewer.SetCanContentScroll(_messages, useVirtualization);
            _messages.AddHandler(FrameworkElement.RequestBringIntoViewEvent, new RequestBringIntoViewEventHandler((_, args) =>
            {
                if (args.TargetObject is ListBoxItem { DataContext: CopilotChatMessage message })
                    NavigationTargets.Add(message);
            }), handledEventsToo: true);
            LayoutMessages();
            ScrollViewer = FindVisualChild<ScrollViewer>(_messages)!;
            Assert.NotNull(ScrollViewer);
            Settle();
            Assert.True(ScrollViewer.ScrollableHeight > 400, "The fixture needs a real scrollable message viewport.");
            ScrollViewer.ScrollToEnd();
            Settle();
            AssertAtBottom();
            Assert.IsType<ListBoxItem>(_messages.ItemContainerGenerator.ContainerFromItem(StreamingMessage));
            NavigationTargets.Clear();
        }

        public CopilotChatPanel Panel { get; }
        public CopilotChatViewModel ViewModel { get; }
        public ScrollViewer ScrollViewer { get; }
        public CopilotChatMessage FirstMessage => ViewModel.Messages[0];
        public CopilotChatMessage StreamingMessage => ViewModel.Messages[^1];
        public List<CopilotChatMessage> NavigationTargets { get; } = [];

        public CopilotChatViewModel CreateViewModel(string title)
        {
            var profile = new CopilotProfileConfig
            {
                Id = "scroll-lifetime-profile", Name = "Scroll lifetime profile",
                VendorType = CopilotVendorType.Custom, ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "scroll-lifetime-test-key", BaseUrl = "https://unit.test/v1", Model = "test-model",
            };
            var conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.Name);
            for (var index = 0; index < 6; index++)
            {
                conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, index == 0 ? title + " needle" : title + " request " + index));
                conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, title + " answer " + index));
            }
            var state = new CopilotChatState
            {
                ActiveConversationId = conversation.Id, ActiveProfileId = profile.Id, Conversations = [conversation],
            };
            var config = new CopilotConfig
            {
                SchemaVersion = CopilotConfig.CurrentSchemaVersion,
                McpBearerToken = "scroll-lifetime-test-token", Profiles = [profile],
            };
            var host = new CopilotAgentTaskHost();
            var viewModel = new CopilotChatViewModel(new CopilotChatService(), new MemoryStateStore(state), config, new UnusedTurnRuntime(), host);
            _hosts.Add(host);
            _viewModels.Add(viewModel);
            return viewModel;
        }

        public void GrowStreamingAnswer()
        {
            Assert.IsType<ListBoxItem>(_messages.ItemContainerGenerator.ContainerFromItem(StreamingMessage));
            StreamingMessage.Content += "\n" + string.Join('\n', Enumerable.Repeat("More streamed answer content", 24));
        }

        public void IncreaseLastRealizedRowHeight(double additionalHeight)
        {
            var row = Assert.IsType<ListBoxItem>(_messages.ItemContainerGenerator.ContainerFromItem(_messages.Items[^1]));
            Assert.True(row.ActualHeight > 0);
            row.MinHeight = row.ActualHeight + additionalHeight;
        }

        public void ExecuteLocalCommand(string prompt) => Assert.True((bool)ExecuteCommand.Invoke(ViewModel, [prompt, false, null])!);

        public void LayoutMessages()
        {
            _messages.ApplyTemplate();
            _messages.Measure(new Size(600, 200));
            _messages.Arrange(new Rect(0, 0, 600, 200));
            _messages.UpdateLayout();
            if (!_useVirtualization && Panel.DataContext is CopilotChatViewModel viewModel)
                Assert.All(viewModel.Messages, message => Assert.IsType<ListBoxItem>(_messages.ItemContainerGenerator.ContainerFromItem(message)));
        }

        public void Settle()
        {
            LayoutMessages();
            DrainDispatcher();
            LayoutMessages();
            DrainDispatcher();
        }

        public void AssertAtBottom() => Assert.InRange(ScrollViewer.ScrollableHeight - ScrollViewer.VerticalOffset, 0, 1);

        public void AssertMessageNotRealized(CopilotChatMessage message) =>
            Assert.Null(_messages.ItemContainerGenerator.ContainerFromItem(message));

        public void AssertMessageVisible(CopilotChatMessage message)
        {
            var row = Assert.IsType<ListBoxItem>(_messages.ItemContainerGenerator.ContainerFromItem(message));
            Assert.True(row.ActualHeight > 0);
            var bounds = row.TransformToAncestor(ScrollViewer).TransformBounds(new Rect(row.RenderSize));
            Assert.True(bounds.Top < ScrollViewer.ActualHeight && bounds.Bottom > 0,
                $"The message row [{bounds.Top}, {bounds.Bottom}] must intersect the actual viewport [0, {ScrollViewer.ActualHeight}].");
        }

        public void Dispose()
        {
            Panel.DataContext = null;
            foreach (var host in _hosts)
                host.Shutdown();
            foreach (var viewModel in _viewModels)
                viewModel.Dispose();
            if (ReferenceEquals(InstanceField.GetValue(null), _testInstance))
                InstanceField.SetValue(null, _previousInstance);
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
                return match;
            if (FindVisualChild<T>(child) is { } descendant)
                return descendant;
        }
        return null;
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
        public IAsyncEnumerable<CopilotTurnEvent> RunAsync(CopilotTurnRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Scroll lifetime tests must not start a model request.");

        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message) => new(CopilotSteeringAdmissionReason.RuntimeUnavailable);
        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;
        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;
        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) => false;
        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(
            CopilotWorkspaceRollbackActionRequest request, Action<CopilotAgentEvent> onEvent, CancellationToken cancellationToken) =>
            Task.FromException<CopilotWorkspaceRollbackActionResult>(new NotSupportedException());
    }
}
