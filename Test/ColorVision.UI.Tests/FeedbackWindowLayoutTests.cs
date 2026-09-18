using ColorVision.UI.Desktop.Feedback;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopResources = ColorVision.UI.Desktop.Properties.Resources;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class FeedbackWindowLayoutTests
{
    [Fact]
    public void DefaultLayoutKeepsDiagnosticActionsAvailableWithoutExpandingTheForm()
    {
        WithWindow(window =>
        {
            Assert.Null(window.FindName("DiagnosticsExpander"));
            Assert.False(string.IsNullOrWhiteSpace(Element<TextBlock>(window, "MessageLabel").Text));
            TextBox message = Element<TextBox>(window, "MessageTextBox");
            Assert.Same(message, FocusManager.GetFocusedElement(window));
            Assert.Equal(string.Empty, message.Text);
            Assert.Equal(Visibility.Visible, Element<TextBlock>(window, "EmptyAttachmentsText").Visibility);
            Assert.Equal(DesktopResources.FeedbackDiagnosticsHint, Element<TextBlock>(window, "DiagnosticsHintText").Text);
            AssertSummaryMatchesSelection(window);

            foreach (string name in new[] { "ConfigureDiagnosticsButton", "PackLogsButton", "AddFileButton", "AddScreenshotButton", "SendButton" })
                Assert.True(Element<Button>(window, name).IsEnabled);

            Assert.True(Element<Button>(window, "SendButton").IsDefault);
        });
    }

    [Fact]
    public void SendingFeedbackSubmitsDirectlyWithoutAnAccountPrompt()
    {
        string source = File.ReadAllText(FindFeedbackWindowSource());

        Assert.DoesNotContain("FeedbackAccountLoginDialog", source, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/auth/login", source, StringComparison.Ordinal);
        Assert.Contains("/api/feedback", source, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyMessageRestoresPlaceholderWhenFocusLeavesAndClearsItOnReturn()
    {
        WithWindow(window =>
        {
            TextBox message = Element<TextBox>(window, "MessageTextBox");
            Assert.Equal(string.Empty, message.Text);

            message.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
            Assert.Equal(DesktopResources.FeedbackPlaceholder, message.Text);

            message.RaiseEvent(new RoutedEventArgs(UIElement.GotFocusEvent));
            Assert.Equal(string.Empty, message.Text);
        });
    }

    [Fact]
    public void ClosingAndReopeningSelectionPreservesChoicesAndUpdatesMainSummary()
    {
        WithWindow(window =>
        {
            ObservableCollection<CollectorItem> collectors = Collectors(window);
            Assert.NotEmpty(collectors);
            FeedbackDiagnosticsWindow selection = new(collectors);
            try
            {
                for (int index = 0; index < collectors.Count; index++)
                    collectors[index].IsChecked = index % 2 == 0;
                bool[] selections = collectors.Select(item => item.IsChecked).ToArray();
                AssertSummaryMatchesSelection(window);
                selection.Close();
                selection = new FeedbackDiagnosticsWindow(collectors);
                Assert.Equal(selections, collectors.Select(item => item.IsChecked).ToArray());
                Assert.Equal(Element<TextBlock>(window, "DiagnosticsSummaryText").Text,
                    Element<TextBlock>(selection, "SelectionSummaryText").Text);
            }
            finally
            {
                selection.Close();
            }
        });
    }

    [Fact]
    public void SharedRangeOnlyChangesSourcesThatImplementTheRangeInterface()
    {
        WithWindow(window =>
        {
            RangeCollector ranged = new();
            BasicCollector basic = new();
            Collectors(window).Add(new CollectorItem(ranged));
            Collectors(window).Add(new CollectorItem(basic));
            Element<ComboBox>(window, "LogRangeComboBox").SelectedValue = 3;
            Assert.Equal(3, ranged.RecentDays);
            Assert.Equal(0, basic.CollectionCalls);
            Assert.All(Collectors(window).Where(item => item.Collector is IFeedbackLogTimeRangeCollector),
                item => Assert.Equal(3, item.SelectedDays));
        });
    }

    [Fact]
    public void SearchPreservesHiddenChoicesAndDefaultSelectionAppliesToAllItems()
    {
        WpfTestHost.Invoke(() =>
        {
            CollectorItem included = new(new BasicCollector());
            CollectorItem optional = new(new RangeCollector());
            FeedbackDiagnosticsWindow selection = new([included, optional]);
            try
            {
                Element<TextBox>(selection, "SearchTextBox").Text = "missing";
                Assert.Empty(Element<ItemsControl>(selection, "CollectorsList").Items);
                Assert.True(included.IsChecked);
                Element<Button>(selection, "SelectAllButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.True(included.IsChecked && optional.IsChecked);
                Element<Button>(selection, "RestoreDefaultsButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.True(included.IsChecked);
                Assert.False(optional.IsChecked);
                Element<TextBox>(selection, "SearchTextBox").Text = "range";
                Assert.Same(optional, Assert.Single(Element<ItemsControl>(selection, "CollectorsList").Items.Cast<CollectorItem>()));
                Element<Button>(selection, "SelectNoneButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.False(included.IsChecked || optional.IsChecked);
            }
            finally
            {
                selection.Close();
            }
        });
    }

    [Fact]
    public void DiagnosticSummaryTracksSelectionChanges()
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
            Assert.Null(window.FindName("DiagnosticsExpander"));
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
    public void MinimumSizeKeepsFooterOutsideTheScrollableContent(bool manyAttachments)
    {
        WithWindow(window =>
        {
            if (manyAttachments)
                for (int index = 0; index < 16; index++)
                    Attachments(window).Add(new AttachmentItem { FilePath = $"attachment-{index}.txt" });
            Grid root = Assert.IsType<Grid>(window.Content);
            ScrollViewer body = Element<ScrollViewer>(window, "FeedbackContentScrollViewer");
            FrameworkElement diagnostics = Element<FrameworkElement>(window, "DiagnosticsPanel");
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
            Rect diagnosticBounds = BoundsIn(diagnostics, root);
            Rect footerBounds = BoundsIn(footer, root);
            Rect sendBounds = BoundsIn(send, root);
            Assert.True(bodyBounds.Bottom <= diagnosticBounds.Top + 1, "Attachments must not overlap the diagnostic controls.");
            Assert.True(diagnosticBounds.Bottom <= footerBounds.Top + 1, "Diagnostic controls must remain above the footer.");
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

    private static T Element<T>(Window window, string name) where T : FrameworkElement
        => Assert.IsAssignableFrom<T>(window.FindName(name));

    private static ObservableCollection<CollectorItem> Collectors(FeedbackWindow window)
        => Assert.IsType<ObservableCollection<CollectorItem>>(Element<Border>(window, "DiagnosticsPanel").DataContext);

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

    private static string FindFeedbackWindowSource([CallerFilePath] string testSourcePath = "")
    {
        string testDirectory = Path.GetDirectoryName(testSourcePath)!;
        return Path.GetFullPath(Path.Combine(
            testDirectory,
            "..",
            "..",
            "UI",
            "ColorVision.UI.Desktop",
            "Feedback",
            "FeedbackWindow.xaml.cs"));
    }

    private class BasicCollector : IFeedbackLogCollector
    {
        public string Name => "Basic";
        public int Order => 0;
        public int CollectionCalls { get; private set; }
        public IEnumerable<(string EntryPath, string FilePath)> CollectFiles()
        {
            CollectionCalls++;
            return [];
        }
    }

    private sealed class RangeCollector : IFeedbackLogCollector, IFeedbackLogTimeRangeCollector
    {
        public string Name => "Range";
        public int Order => 1;
        public bool IsSelectedByDefault => false;
        public int RecentDays { get; set; } = 7;
        public string? LogDirectory => null;
        public IEnumerable<(string EntryPath, string FilePath)> CollectFiles() => [];
    }

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
