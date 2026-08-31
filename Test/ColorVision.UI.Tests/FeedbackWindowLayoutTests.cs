using ColorVision.UI.Desktop.Feedback;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

    [Fact]
    public void RenderPreviewsOnlyWhenAnOutputDirectoryIsRequested()
    {
        string? outputDirectory = Environment.GetEnvironmentVariable("COLORVISION_FEEDBACK_PREVIEW_DIRECTORY");
        if (string.IsNullOrWhiteSpace(outputDirectory))
            return;

        Assert.True(Path.IsPathFullyQualified(outputDirectory), "The preview output directory must be an absolute path.");
        Directory.CreateDirectory(outputDirectory);
        using TemporaryAttachments files = new();
        string packagePath = files.Create("ColorVision_Diagnostics_20260830_114319.zip");
        foreach ((string name, bool expanded, bool dark, bool minimum) in new[]
        {
            ("feedback-collapsed.png", false, false, false),
            ("feedback-expanded.png", true, false, false),
            ("feedback-dark-collapsed-minimum.png", false, true, true),
            ("feedback-dark-minimum.png", true, true, true),
        })
        {
            WithWindow(window =>
            {
                Expander diagnostics = Element<Expander>(window, "DiagnosticsExpander");
                diagnostics.IsExpanded = expanded;
                foreach (string source in new[]
                {
                    $"/HandyControl;component/Themes/basic/colors/{(dark ? "colorsdark" : "colors")}.xaml",
                    "/HandyControl;component/Themes/Theme.xaml",
                    $"/ColorVision.Themes;component/Themes/{(dark ? "Dark" : "White")}.xaml",
                    "/ColorVision.Themes;component/Themes/Base.xaml",
                })
                    window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source, UriKind.Relative) });

                Grid root = Assert.IsType<Grid>(window.Content);
                ScrollViewer body = Element<ScrollViewer>(window, "FeedbackContentScrollViewer");
                Brush background = Assert.IsAssignableFrom<Brush>(window.FindResource("GlobalBackground"));
                window.Content = null;
                Border preview = new() { Background = background, Child = root };
                preview.Resources.MergedDictionaries.Add(window.Resources);
                Size size = new(minimum ? window.MinWidth : window.Width, (minimum ? window.MinHeight : window.Height) - 40);
                preview.Measure(size);
                preview.Arrange(new Rect(size));
                preview.UpdateLayout();
                if (expanded)
                {
                    body.ScrollToVerticalOffset(Math.Max(0, diagnostics.TranslatePoint(new Point(), (UIElement)body.Content).Y - 12));
                    preview.UpdateLayout();
                }
                SavePreview(preview, size, Path.Combine(outputDirectory, name));

                if (expanded)
                {
                    body.ScrollToEnd();
                    preview.UpdateLayout();
                    SavePreview(preview, size, Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(name)}-bottom.png"));
                    if (!dark)
                    {
                        // A full-height overview also exposes collectors between the top and bottom captures.
                        size.Height += body.ScrollableHeight;
                        body.ScrollToTop();
                        preview.Measure(size);
                        preview.Arrange(new Rect(size));
                        preview.UpdateLayout();
                        SavePreview(preview, size, Path.Combine(outputDirectory, "feedback-expanded-full.png"));
                    }
                }
            }, "打开检测流程后结果未更新。\n重新连接相机后仍可复现，请协助查看。", [packagePath]);
        }
    }

    private static void SavePreview(Visual preview, Size size, string outputPath)
    {
        RenderTargetBitmap bitmap = new((int)Math.Ceiling(size.Width * 1.5), (int)Math.Ceiling(size.Height * 1.5), 144, 144, PixelFormats.Pbgra32);
        bitmap.Render(preview);
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream output = File.Create(outputPath);
        encoder.Save(output);
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
