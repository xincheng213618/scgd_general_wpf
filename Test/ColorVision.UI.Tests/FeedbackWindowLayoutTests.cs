using ColorVision.UI.Desktop.Feedback;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopResources = ColorVision.UI.Desktop.Properties.Resources;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class FeedbackWindowLayoutTests
{
    [Fact]
    public void DefaultLayoutKeepsDiagnosticsCollapsedAndPrimaryActionsAvailable()
    {
        WithWindow(window =>
        {
            Assert.False(Element<Expander>(window, "DiagnosticsExpander").IsExpanded);
            Assert.False(string.IsNullOrWhiteSpace(Element<TextBlock>(window, "MessageLabel").Text));
            Assert.Equal(DesktopResources.FeedbackPlaceholder, Element<TextBox>(window, "MessageTextBox").Text);
            Assert.Equal(Visibility.Visible, Element<TextBlock>(window, "EmptyAttachmentsText").Visibility);
            Assert.Equal(DesktopResources.FeedbackDiagnosticsHint, Element<TextBlock>(window, "DiagnosticsHintText").Text);
            AssertSummaryMatchesSelection(window);

            foreach (string name in new[] { "PackLogsButton", "AddFileButton", "AddScreenshotButton", "SendButton" })
                Assert.True(Element<Button>(window, name).IsEnabled);

            Assert.True(Element<Button>(window, "SendButton").IsDefault);
        });
    }

    [Fact]
    public void ExpandingAndCollapsingDiagnosticsPreservesCollectorSelection()
    {
        WithWindow(window =>
        {
            ObservableCollection<CollectorItem> collectors = Collectors(window);
            Assert.NotEmpty(collectors);
            for (int index = 0; index < collectors.Count; index++)
                collectors[index].IsChecked = index % 2 == 0;
            bool[] selections = collectors.Select(item => item.IsChecked).ToArray();
            Expander expander = Element<Expander>(window, "DiagnosticsExpander");

            expander.IsExpanded = true;
            expander.IsExpanded = false;
            expander.IsExpanded = true;

            Assert.Same(collectors, Element<ListBox>(window, "CollectorsList").ItemsSource);
            Assert.Equal(selections, collectors.Select(item => item.IsChecked).ToArray());
            AssertSummaryMatchesSelection(window);
        });
    }

    [Fact]
    public void DiagnosticSummaryTracksSelectionChangesWhileCollapsed()
    {
        WithWindow(window =>
        {
            ObservableCollection<CollectorItem> collectors = Collectors(window);
            Assert.NotEmpty(collectors);
            foreach (CollectorItem item in collectors)
                item.IsChecked = false;
            AssertSummaryMatchesSelection(window);

            collectors[0].IsChecked = true;
            AssertSummaryMatchesSelection(window);

            foreach (CollectorItem item in collectors)
                item.IsChecked = true;
            AssertSummaryMatchesSelection(window);
            Assert.False(Element<Expander>(window, "DiagnosticsExpander").IsExpanded);
        });
    }

    [Fact]
    public void InitialDraftPreservesMessageAndDeduplicatesExistingAttachments()
    {
        using TemporaryAttachments files = new();
        string attachmentPath = files.Create("feedback-details.txt");
        string missingPath = Path.Combine(files.DirectoryPath, "missing.txt");

        WithWindow(window =>
        {
            TextBox message = Element<TextBox>(window, "MessageTextBox");
            Assert.Equal("步骤一：打开相机\n步骤二：开始检测", message.Text);
            message.RaiseEvent(new RoutedEventArgs(UIElement.GotFocusEvent));
            Assert.Equal("步骤一：打开相机\n步骤二：开始检测", message.Text);

            AttachmentItem attachment = Assert.Single(Attachments(window));
            Assert.Equal(attachmentPath, attachment.FilePath);
            Assert.Equal(Visibility.Collapsed, Element<TextBlock>(window, "EmptyAttachmentsText").Visibility);
            Assert.Equal(DesktopResources.FeedbackDiagnosticsHint, Element<TextBlock>(window, "DiagnosticsHintText").Text);
        }, "  步骤一：打开相机\n步骤二：开始检测  ", [attachmentPath, attachmentPath, missingPath, " "]);
    }

    [Fact]
    public void DiagnosticPackageHintAndEmptyStateTrackAttachmentChanges()
    {
        using TemporaryAttachments files = new();
        string packagePath = files.Create("ColorVision_Diagnostics_20260830_120000.zip");
        string notePath = files.Create("feedback-details.txt");

        WithWindow(window =>
        {
            ObservableCollection<AttachmentItem> attachments = Attachments(window);
            TextBlock hint = Element<TextBlock>(window, "DiagnosticsHintText");
            TextBlock emptyState = Element<TextBlock>(window, "EmptyAttachmentsText");
            AttachmentItem package = Assert.Single(attachments, item => item.FilePath == packagePath);
            string selectionSummary = Element<TextBlock>(window, "DiagnosticsSummaryText").Text;
            Assert.Equal(DesktopResources.FeedbackPackageReadyHint, hint.Text);
            Assert.Equal(Visibility.Collapsed, emptyState.Visibility);

            attachments.Remove(package);
            FlushBindings(window);

            Assert.Single(attachments);
            Assert.Equal(DesktopResources.FeedbackDiagnosticsHint, hint.Text);
            Assert.Equal(Visibility.Collapsed, emptyState.Visibility);

            attachments.Clear();
            FlushBindings(window);

            Assert.Equal(Visibility.Visible, emptyState.Visibility);
            Assert.Equal(DesktopResources.FeedbackDiagnosticsHint, hint.Text);

            attachments.Add(package);
            FlushBindings(window);

            Assert.Equal(Visibility.Collapsed, emptyState.Visibility);
            Assert.Equal(DesktopResources.FeedbackPackageReadyHint, hint.Text);
            Assert.Equal(selectionSummary, Element<TextBlock>(window, "DiagnosticsSummaryText").Text);
        }, initialAttachmentPaths: [packagePath, notePath]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MinimumSizeKeepsFooterOutsideTheScrollableContent(bool diagnosticsExpanded)
    {
        WithWindow(window =>
        {
            Element<Expander>(window, "DiagnosticsExpander").IsExpanded = diagnosticsExpanded;
            Grid root = Assert.IsType<Grid>(window.Content);
            ScrollViewer body = Element<ScrollViewer>(window, "FeedbackContentScrollViewer");
            FrameworkElement footer = Element<FrameworkElement>(window, "FeedbackFooter");
            Button send = Element<Button>(window, "SendButton");

            // Measure the actual content without showing a native window or reading system theme state.
            Size size = new(window.MinWidth, window.MinHeight - 40);
            root.Measure(size);
            root.Arrange(new Rect(size));
            root.UpdateLayout();

            Assert.True(body.ActualWidth > 0 && body.ActualHeight > 0);
            Assert.True(footer.ActualWidth > 0 && footer.ActualHeight > 0);
            Rect bodyBounds = BoundsIn(body, root);
            Rect footerBounds = BoundsIn(footer, root);
            Rect sendBounds = BoundsIn(send, root);
            Assert.True(bodyBounds.Bottom <= footerBounds.Top + 1, "Scrollable content must not overlap the fixed footer.");
            Assert.True(footerBounds.Bottom <= root.ActualHeight + 1, "The footer must remain inside the minimum window size.");
            Assert.True(sendBounds.Width > 0 && sendBounds.Height > 0);
            Assert.True(sendBounds.Left >= footerBounds.Left - 1 && sendBounds.Right <= footerBounds.Right + 1);
            Assert.True(sendBounds.Top >= footerBounds.Top - 1 && sendBounds.Bottom <= footerBounds.Bottom + 1);

            double footerTop = footerBounds.Top;
            body.ScrollToEnd();
            root.UpdateLayout();
            Assert.Equal(footerTop, BoundsIn(footer, root).Top, precision: 3);
        });
    }

    private static void WithWindow(Action<FeedbackWindow> action, string? initialMessage = null, IEnumerable<string>? initialAttachmentPaths = null)
    {
        WpfTestHost.Invoke(() =>
        {
            AssemblyHandler.Instance.RegisterAssembly(typeof(FeedbackWindow).Assembly);
            FeedbackWindow window = new(initialMessage, initialAttachmentPaths);
            try
            {
                FlushBindings(window);
                action(window);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static T Element<T>(FeedbackWindow window, string name) where T : FrameworkElement
        => Assert.IsAssignableFrom<T>(window.FindName(name));

    private static ObservableCollection<CollectorItem> Collectors(FeedbackWindow window)
        => Assert.IsType<ObservableCollection<CollectorItem>>(Element<ListBox>(window, "CollectorsList").ItemsSource);

    private static ObservableCollection<AttachmentItem> Attachments(FeedbackWindow window)
        => Assert.IsType<ObservableCollection<AttachmentItem>>(Element<ListBox>(window, "AttachmentsList").ItemsSource);

    private static void AssertSummaryMatchesSelection(FeedbackWindow window)
    {
        ObservableCollection<CollectorItem> collectors = Collectors(window);
        string expected = string.Format(DesktopResources.FeedbackDiagnosticsSummary, collectors.Count(item => item.IsChecked), collectors.Count);
        Assert.Equal(expected, Element<TextBlock>(window, "DiagnosticsSummaryText").Text);
    }

    private static Rect BoundsIn(FrameworkElement element, Visual ancestor)
        => element.TransformToAncestor(ancestor).TransformBounds(new Rect(element.RenderSize));

    private static void FlushBindings(FeedbackWindow window)
        => window.Dispatcher.Invoke(static () => { }, DispatcherPriority.DataBind);

    private sealed class TemporaryAttachments : IDisposable
    {
        private readonly List<string> _paths = [];
        public string DirectoryPath { get; } = Directory.CreateTempSubdirectory("ColorVision-FeedbackWindowTests-").FullName;

        public string Create(string fileName)
        {
            string path = Path.Combine(DirectoryPath, fileName);
            File.WriteAllText(path, "Feedback layout fixture; no diagnostic collection or upload.");
            _paths.Add(path);
            return path;
        }

        public void Dispose()
        {
            foreach (string path in _paths)
                File.Delete(path);
            Directory.Delete(DirectoryPath);
        }
    }
}
