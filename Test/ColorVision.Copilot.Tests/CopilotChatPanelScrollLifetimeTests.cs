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
