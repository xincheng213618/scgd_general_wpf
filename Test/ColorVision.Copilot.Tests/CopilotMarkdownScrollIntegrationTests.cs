using ColorVision.Copilot;
using ColorVision.Solution;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotMarkdownScrollIntegrationTests
{
    [Fact]
    public void LoadedMarkdownStreamAndFinalDocumentKeepTheReaderAtTheActualBottom()
    {
        Run(fixture =>
        {
            var initialExtent = fixture.ScrollViewer.ExtentHeight;

            fixture.StreamUntilAnIntermediateDocumentIsRendered();
            fixture.WaitUntil(() => fixture.ScrollViewer.ExtentHeight > initialExtent + 30 && fixture.IsAtBottom,
                "The rendered streaming paragraphs must grow the real list and preserve its bottom offset.");
            var streamingExtent = fixture.ScrollViewer.ExtentHeight;

            fixture.FinishStream();
            fixture.WaitUntil(() => fixture.DocumentText.Contains("FINAL_MARKDOWN_SENTINEL", StringComparison.Ordinal)
                    && fixture.ScrollViewer.ExtentHeight > streamingExtent + 30 && fixture.IsAtBottom,
                "The final Markdown document must be rendered and followed to its actual end.");

            fixture.AssertLoadedMarkdown();
            Assert.True(fixture.IsAtBottom);
        });
    }

    [Fact]
    public void LoadedMarkdownGrowthPreservesTheOffsetOfAReaderAboveTheBottom()
    {
        Run(fixture =>
        {
            var readingOffset = fixture.ScrollViewer.ScrollableHeight - 200;
            fixture.ScrollViewer.ScrollToVerticalOffset(readingOffset);
            fixture.WaitUntil(() => Math.Abs(fixture.ScrollViewer.VerticalOffset - readingOffset) <= 1,
                "The reader must move above the bottom before streaming resumes.");
            Assert.True(fixture.ScrollViewer.ScrollableHeight - fixture.ScrollViewer.VerticalOffset > 100);
            fixture.AssertLoadedMarkdown();
            var initialExtent = fixture.ScrollViewer.ExtentHeight;

            fixture.StreamUntilAnIntermediateDocumentIsRendered();
            fixture.WaitUntil(() => fixture.ScrollViewer.ExtentHeight > initialExtent + 30,
                "The visible Markdown row must really grow while the reader stays above the bottom.");
            fixture.AssertReadingOffset(readingOffset);

            fixture.FinishStream();
            fixture.WaitUntil(() => fixture.DocumentText.Contains("FINAL_MARKDOWN_SENTINEL", StringComparison.Ordinal),
                "The final document must render while the streaming row remains loaded above the bottom.");
            fixture.DrainLayout();

            fixture.AssertLoadedMarkdown();
            fixture.AssertReadingOffset(readingOffset);
        });
    }

    private static void Run(Action<MarkdownScrollFixture> action)
    {
        StaTest.Run(() =>
        {
            var instanceField = typeof(SolutionManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
            var previousInstance = instanceField.GetValue(null);
            var testInstance = (SolutionManager)RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));
            instanceField.SetValue(null, testInstance);
            try
            {
                using var fixture = new MarkdownScrollFixture();
                action(fixture);
            }
            finally
            {
                if (ReferenceEquals(instanceField.GetValue(null), testInstance))
                    instanceField.SetValue(null, previousInstance);
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        }, TimeSpan.FromSeconds(30), "The loaded Markdown scroll integration test did not finish.");
    }

    private sealed class MarkdownScrollFixture : IDisposable
    {
        private readonly CopilotAgentTaskHost _host = new();
        private readonly CopilotChatViewModel _viewModel;
        private readonly CopilotChatPanel _panel;
        private readonly ListBox _messages;
        private readonly Window _window;
        private readonly CopilotChatMessage _streamingMessage;
        private readonly bool _restoredMessageKeptSourceIdentity;

        public MarkdownScrollFixture()
        {
            var profile = new CopilotProfileConfig
            {
                Id = "markdown-scroll-profile", Name = "Markdown scroll profile",
                VendorType = CopilotVendorType.Custom, ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "markdown-scroll-test-key", BaseUrl = "https://unit.test/v1", Model = "test-model",
            };
            var conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.Name);
            for (var index = 0; index < 8; index++)
                conversation.Messages.Add(new CopilotChatMessage(index % 2 == 0 ? CopilotChatRole.User : CopilotChatRole.Assistant,
                    $"Earlier message {index}."));
            var sourceMessage = new CopilotChatMessage(CopilotChatRole.Assistant,
                string.Join("\n\n", Enumerable.Range(0, 48).Select(index => $"Initial Markdown paragraph **{index}**."))
                    + "\n\nINITIAL_MARKDOWN_SENTINEL");
            conversation.Messages.Add(sourceMessage);
            var state = new CopilotChatState
            {
                ActiveConversationId = conversation.Id, ActiveProfileId = profile.Id, Conversations = [conversation],
            };
            var config = new CopilotConfig
            {
                SchemaVersion = CopilotConfig.CurrentSchemaVersion,
                McpBearerToken = "markdown-scroll-test-token", Profiles = [profile],
            };
            _viewModel = new CopilotChatViewModel(new CopilotChatService(), new MemoryStateStore(state), config,
                new UnusedTurnRuntime(), _host);
            _streamingMessage = sourceMessage;
            _restoredMessageKeptSourceIdentity = ReferenceEquals(sourceMessage, _viewModel.Messages.LastOrDefault());
            _panel = new CopilotChatPanel { DataContext = _viewModel };
            _messages = (ListBox)_panel.FindName("MessagesListBox");
            var markdown = new FrameworkElementFactory(typeof(CopilotMarkdownView));
            markdown.SetBinding(CopilotMarkdownView.MarkdownProperty, new Binding(nameof(CopilotChatMessage.Content)));
            markdown.SetValue(FrameworkElement.MinHeightProperty, 80d);
            markdown.SetValue(FrameworkElement.WidthProperty, 400d);
            _messages.ItemTemplate = new DataTemplate { VisualTree = markdown };
            _messages.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel)));
            VirtualizingPanel.SetIsVirtualizing(_messages, true);
            VirtualizingPanel.SetVirtualizationMode(_messages, VirtualizationMode.Recycling);
            VirtualizingPanel.SetScrollUnit(_messages, ScrollUnit.Pixel);
            System.Windows.Controls.ScrollViewer.SetCanContentScroll(_messages, true);
            _window = new Window
            {
                Content = _panel, Width = 620, Height = 760, Left = -10000, Top = -10000,
                WindowStartupLocation = WindowStartupLocation.Manual, WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize, ShowActivated = false, ShowInTaskbar = false, Opacity = 0,
            };
            try
            {
                _window.Show();
                WaitUntil(() => _panel.IsLoaded && FindVisualChild<ScrollViewer>(_messages)?.ViewportHeight > 0,
                    "The hidden native host must load the actual panel and message viewport.");
                ScrollViewer = FindVisualChild<ScrollViewer>(_messages)!;
                ScrollViewer.ScrollToEnd();
                WaitUntil(() => CurrentMarkdown?.IsLoaded == true
                        && DocumentText.Contains("INITIAL_MARKDOWN_SENTINEL", StringComparison.Ordinal),
                    "The virtualized last row must load and render its actual initial Markdown document.");
                ScrollViewer.ScrollToEnd();
                WaitUntil(() => IsAtBottom, "The fixture must begin at the rendered bottom.");
                Assert.True(ScrollViewer.ScrollableHeight > 400);
                AssertLoadedMarkdown();
                Assert.True(CurrentMarkdown!.ActualHeight > ScrollViewer.ViewportHeight + 300);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public ScrollViewer ScrollViewer { get; private set; } = null!;
        public bool IsAtBottom => ScrollViewer.ScrollableHeight - ScrollViewer.VerticalOffset <= 1;
        private CopilotMarkdownView? CurrentMarkdown => _messages.ItemContainerGenerator.ContainerFromItem(_streamingMessage) is ListBoxItem row
            ? FindVisualChild<CopilotMarkdownView>(row) : null;
        public string DocumentText => CurrentMarkdown is { } markdown && FindVisualChild<RichTextBox>(markdown) is { } viewer
            ? new TextRange(viewer.Document.ContentStart, viewer.Document.ContentEnd).Text : string.Empty;

        public void StreamUntilAnIntermediateDocumentIsRendered()
        {
            var updates = 0;
            var producer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(20) };
            producer.Tick += (_, _) =>
            {
                updates++;
                _streamingMessage.Content += $"\n\nSTREAMING_MARKDOWN_SENTINEL **chunk {updates}**.";
            };
            try
            {
                producer.Start();
                WaitUntil(() => updates >= 4 && DocumentText.Contains("STREAMING_MARKDOWN_SENTINEL", StringComparison.Ordinal),
                    "The loaded Markdown control must publish an intermediate document while token updates continue.");
                Assert.True(producer.IsEnabled);
                AssertLoadedMarkdown();
            }
            finally
            {
                producer.Stop();
            }
        }

        public void FinishStream() => _streamingMessage.Content +=
            "\n\n## Final rendered answer\n\nThe stream has completed.\n\nFINAL_MARKDOWN_SENTINEL";

        public void AssertLoadedMarkdown()
        {
            var markdown = Assert.IsType<CopilotMarkdownView>(CurrentMarkdown);
            Assert.True(markdown.IsLoaded);
            Assert.IsType<HwndSource>(PresentationSource.FromVisual(markdown));
            Assert.IsType<RichTextBox>(FindVisualChild<RichTextBox>(markdown));
            Assert.Same(_streamingMessage, markdown.DataContext);
            Assert.Equal(_streamingMessage.Content, markdown.Markdown);
        }

        public void AssertReadingOffset(double offset)
        {
            Assert.InRange(Math.Abs(ScrollViewer.VerticalOffset - offset), 0, 1);
            Assert.True(ScrollViewer.ScrollableHeight - ScrollViewer.VerticalOffset > 100);
        }

        public void WaitUntil(Func<bool> condition, string failure)
        {
            var deadline = Stopwatch.StartNew();
            while (!condition() && deadline.Elapsed < TimeSpan.FromSeconds(5))
                PumpFor(TimeSpan.FromMilliseconds(20));
            Assert.True(condition(), failure + Environment.NewLine + DescribeLoadedState());
            DrainLayout();
        }

        private string DescribeLoadedState()
        {
            var markdown = CurrentMarkdown;
            var scrollViewer = ScrollViewer ?? FindVisualChild<ScrollViewer>(_messages);
            var container = _messages.ItemContainerGenerator.ContainerFromItem(_streamingMessage);
            var text = DocumentText;
            return $"panel loaded={_panel.IsLoaded}, size={_panel.ActualWidth}x{_panel.ActualHeight}; "
                + $"items={_messages.Items.Count}, VM messages={_viewModel.Messages.Count}, "
                + $"last identity={ReferenceEquals(_viewModel.Messages.LastOrDefault(), _streamingMessage)}, source identity={_restoredMessageKeptSourceIdentity}; "
                + $"list last identity={ReferenceEquals(_messages.Items.Cast<object>().LastOrDefault(), _streamingMessage)}, "
                + $"container={container?.GetType().Name ?? "null"}, "
                + $"Markdown loaded={markdown?.IsLoaded}, size={markdown?.ActualWidth}x{markdown?.ActualHeight}, bound chars={markdown?.Markdown.Length}; "
                + $"document chars={text.Length}, tail={text[^Math.Min(text.Length, 180)..]}; "
                + $"content scroll={scrollViewer?.CanContentScroll}, viewport={scrollViewer?.ViewportHeight}, extent={scrollViewer?.ExtentHeight}, offset={scrollViewer?.VerticalOffset}.";
        }

        public void DrainLayout()
        {
            _panel.UpdateLayout();
            var frame = new DispatcherFrame();
            _panel.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
            _panel.UpdateLayout();
        }

        private static void PumpFor(TimeSpan duration)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = duration };
            timer.Tick += (_, _) => frame.Continue = false;
            try
            {
                timer.Start();
                Dispatcher.PushFrame(frame);
            }
            finally
            {
                timer.Stop();
            }
        }

        public void Dispose()
        {
            _window.Close();
            _panel.DataContext = null;
            _host.Shutdown();
            _viewModel.Dispose();
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
            throw new InvalidOperationException("Markdown scroll integration must not start a model request.");
        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message) => new(CopilotSteeringAdmissionReason.RuntimeUnavailable);
        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;
        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;
        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) => false;
        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(
            CopilotWorkspaceRollbackActionRequest request, Action<CopilotAgentEvent> onEvent, CancellationToken cancellationToken) =>
            Task.FromException<CopilotWorkspaceRollbackActionResult>(new NotSupportedException());
    }
}
