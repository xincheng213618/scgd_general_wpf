using ColorVision.Database;
using ColorVision.SocketProtocol;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SocketResources = ColorVision.SocketProtocol.Properties.Resources;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class SocketManagerWindowLayoutTests
{
    [Fact]
    public void DatabaseCleanupButtonInvokesTheInjectedScopedLauncherWithItsOwner()
    {
        WithEnvironment(() =>
        {
            RecordingCleanupLauncher launcher = new();
            SocketManagerWindow window = new(CreateManager([]), cleanupWindowLauncher: launcher);
            try
            {
                Grid root = RefreshLayout(window, minimum: true);
                Button button = Element<Button>(window, "DatabaseCleanupButton");
                Assert.Equal(Visibility.Visible, button.Visibility);
                Assert.True(button.IsEnabled);
                AssertInside(button, root);

                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                Assert.Equal(1, launcher.OpenCalls);
                Assert.Same(window, launcher.Owner);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void MessageListShowsFullDatesAndKeepsClientMetadataInDetailsInsteadOfAColumn()
    {
        SocketMessage message = Message("dated-message", SocketMessageDirection.Received, "capture request");
        WithWindow([message], (window, _) =>
        {
            Element<TextBox>(window, "SearchTextBox").Text = "dated-message";
            ListView list = Element<ListView>(window, "MessagesListView");
            list.SelectedItem = message;
            RefreshLayout(window);
            GridView columns = Assert.IsType<GridView>(list.View);
            GridViewColumn timeColumn = Assert.Single(columns.Columns.Where(column => Equals(column.Header, SocketResources.Time)));

            Assert.DoesNotContain(columns.Columns, column => Equals(column.Header, SocketResources.Client));
            TextBlock renderedTime = Assert.Single(VisualDescendants(list).OfType<TextBlock>()
                .Where(text => text.Text.Contains("2026-08-30", StringComparison.Ordinal)));
            Assert.Contains("12:30:00.000", renderedTime.Text);
            Assert.True(timeColumn.Width >= renderedTime.DesiredSize.Width,
                "The date/time column must fit the complete timestamp at its configured font size.");
            Assert.Equal(message.ClientEndPoint, Element<TextBlock>(window, "DetailClientTextBlock").Text);
        });
    }

    [Fact]
    public void EmptyMessagesAndNoSelectionHaveDistinctVisibleStates()
    {
        WithWindow([], (window, _) =>
        {
            Assert.Empty(Element<ListView>(window, "MessagesListView").Items);
            Assert.Equal(Visibility.Visible, Element<FrameworkElement>(window, "EmptyMessagesPanel").Visibility);
            Assert.False(string.IsNullOrWhiteSpace(Element<TextBlock>(window, "EmptyMessagesTitleTextBlock").Text));
            Assert.False(string.IsNullOrWhiteSpace(Element<TextBlock>(window, "EmptyMessagesHintTextBlock").Text));
            Assert.False(string.IsNullOrWhiteSpace(Element<TextBlock>(window, "MessageCountTextBlock").Text));
            AssertNoSelection(window);
        });
    }

    [Fact]
    public void KeywordAndDirectionFiltersUpdateEmptyStateAndClearTogether()
    {
        WithWindow([], (window, messages) =>
        {
            string emptyTitle = Element<TextBlock>(window, "EmptyMessagesTitleTextBlock").Text;
            SocketMessage received = Message("request-1", SocketMessageDirection.Received, "{\"event\":\"capture\"}");
            SocketMessage sent = Message("response-1", SocketMessageDirection.Sent, "{\"event\":\"completed\"}");
            messages.Add(received);
            messages.Add(sent);
            ListView list = Element<ListView>(window, "MessagesListView");
            ComboBox direction = Element<ComboBox>(window, "DirectionFilterCombo");
            TextBox search = Element<TextBox>(window, "SearchTextBox");
            RefreshLayout(window);

            Assert.Equal(2, list.Items.Count);
            Assert.Equal(Visibility.Collapsed, Element<FrameworkElement>(window, "EmptyMessagesPanel").Visibility);

            direction.SelectedIndex = 1;
            Assert.Same(received, Assert.Single(list.Items.Cast<SocketMessage>()));
            direction.SelectedIndex = 2;
            Assert.Same(sent, Assert.Single(list.Items.Cast<SocketMessage>()));

            search.Text = "request-1";
            RefreshLayout(window);
            Assert.Empty(list.Items);
            Assert.Equal(Visibility.Visible, Element<FrameworkElement>(window, "EmptyMessagesPanel").Visibility);
            string filteredTitle = Element<TextBlock>(window, "EmptyMessagesTitleTextBlock").Text;
            Assert.False(string.IsNullOrWhiteSpace(filteredTitle));
            Assert.NotEqual(emptyTitle, filteredTitle);
            Assert.False(string.IsNullOrWhiteSpace(Element<TextBlock>(window, "EmptyMessagesHintTextBlock").Text));

            Element<Button>(window, "ClearFilterButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            RefreshLayout(window);

            Assert.Equal(string.Empty, search.Text);
            Assert.Equal(0, direction.SelectedIndex);
            Assert.Equal(2, list.Items.Count);
            Assert.Equal(Visibility.Collapsed, Element<FrameworkElement>(window, "EmptyMessagesPanel").Visibility);
            Assert.Equal(2, messages.Count);
        });
    }

    [Theory]
    [InlineData("127.0.0.1:51000")]
    [InlineData("capture")]
    [InlineData("REQUEST-1")]
    [InlineData("sample-payload")]
    [InlineData("201")]
    public void KeywordFilterSearchesTheDisplayedMessageMetadataAndPreview(string keyword)
    {
        SocketMessage matching = Message("request-1", SocketMessageDirection.Received, "sample-payload");
        matching.ResponseCode = 201;
        SocketMessage other = Message("response-2", SocketMessageDirection.Sent, "other payload");
        other.ClientEndPoint = "127.0.0.1:52000";
        other.EventName = "Result";
        other.ResponseCode = 202;

        WithWindow([matching, other], (window, _) =>
        {
            Element<TextBox>(window, "SearchTextBox").Text = keyword;
            RefreshLayout(window);

            Assert.Same(matching, Assert.Single(Element<ListView>(window, "MessagesListView").Items.Cast<SocketMessage>()));
        });
    }

    [Fact]
    public void SelectionShowsMetadataAndFormattingWithoutChangingTheOriginalPayload()
    {
        const string original = "{\"event\":\"capture\",\"payload\":{\"label\":\"测试\",\"count\":2}}";
        SocketMessage message = Message("capture-001", SocketMessageDirection.Sent, original);
        message.ResponseCode = 200;

        WithWindow([message], (window, _) =>
        {
            ListView list = Element<ListView>(window, "MessagesListView");
            list.SelectedItem = message;
            RefreshLayout(window);

            Assert.Same(message, Element<FrameworkElement>(window, "DetailPanel").DataContext);
            Assert.Equal(Visibility.Visible, Element<FrameworkElement>(window, "MessageMetadataPanel").Visibility);
            Assert.Equal(Visibility.Collapsed, Element<FrameworkElement>(window, "NoSelectionPanel").Visibility);
            Assert.Equal(Visibility.Collapsed, Element<FrameworkElement>(window, "EmptyContentPanel").Visibility);
            Assert.Equal(message.ClientEndPoint, Element<TextBlock>(window, "DetailClientTextBlock").Text);
            Assert.Equal(message.EventName, Element<TextBlock>(window, "DetailEventTextBlock").Text);
            Assert.Equal(message.MsgID, Element<TextBlock>(window, "DetailMsgIdTextBlock").Text);
            Assert.Equal("200", Element<TextBlock>(window, "DetailResponseCodeTextBlock").Text);

            TextBox detail = Element<TextBox>(window, "DetailContentTextBox");
            CheckBox prettyPrint = Element<CheckBox>(window, "PrettyPrintCheckBox");
            string formatted = JsonConvert.SerializeObject(JsonConvert.DeserializeObject(original), Formatting.Indented);
            Assert.True(prettyPrint.IsChecked);
            Assert.Equal(formatted, detail.Text);
            Assert.True(Element<Button>(window, "CopyDetailButton").IsEnabled);
            Assert.True(Element<Button>(window, "CopyFormattedDetailButton").IsEnabled);

            // Do not click copy actions: these tests must not replace the user's clipboard.
            prettyPrint.IsChecked = false;
            Assert.Equal(original, detail.Text);
            prettyPrint.IsChecked = true;
            Assert.Equal(formatted, detail.Text);
            Assert.Equal(original, message.Content);
            Assert.True(message.IsContentLoaded);

            list.SelectedItem = null;
            RefreshLayout(window);
            AssertNoSelection(window);
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \r\n\t")]
    public void SelectedEmptyPayloadKeepsMetadataAndShowsAnExplicitEmptyContentState(string? content)
    {
        SocketMessage message = Message("empty-1", SocketMessageDirection.Received, content);

        WithWindow([message], (window, _) =>
        {
            Element<ListView>(window, "MessagesListView").SelectedItem = message;
            RefreshLayout(window);

            Assert.Same(message, Element<FrameworkElement>(window, "DetailPanel").DataContext);
            Assert.Equal(Visibility.Visible, Element<FrameworkElement>(window, "MessageMetadataPanel").Visibility);
            Assert.Equal(Visibility.Collapsed, Element<FrameworkElement>(window, "NoSelectionPanel").Visibility);
            Assert.Equal(Visibility.Visible, Element<FrameworkElement>(window, "EmptyContentPanel").Visibility);
            Assert.True(string.IsNullOrWhiteSpace(Element<TextBox>(window, "DetailContentTextBox").Text));
            Assert.False(Element<Button>(window, "CopyDetailButton").IsEnabled);
            Assert.False(Element<Button>(window, "CopyFormattedDetailButton").IsEnabled);
            Assert.Equal(content, message.Content);
            Assert.True(message.IsContentLoaded);
        });
    }

    [Fact]
    public void PlainTextPayloadRemainsReadableWithPrettyPrintingEnabled()
    {
        const string content = "CAPTURE channel=1\r\nstatus=ready";
        SocketMessage message = Message("text-1", SocketMessageDirection.Received, content);

        WithWindow([message], (window, _) =>
        {
            Element<ListView>(window, "MessagesListView").SelectedItem = message;
            RefreshLayout(window);

            Assert.True(Element<CheckBox>(window, "PrettyPrintCheckBox").IsChecked);
            Assert.Equal(content, Element<TextBox>(window, "DetailContentTextBox").Text);
            Assert.Equal(Visibility.Collapsed, Element<FrameworkElement>(window, "EmptyContentPanel").Visibility);
        });
    }

    [Fact]
    public void WindowsSharingOneMessageCollectionHaveIndependentFiltersAndLifetimes()
    {
        WithEnvironment(() =>
        {
            SocketMessage received = Message("request-1", SocketMessageDirection.Received, "request");
            SocketMessage sent = Message("response-1", SocketMessageDirection.Sent, "response");
            SocketManager manager = CreateManager([received, sent]);
            SocketManagerWindow first = CreateWindow(manager);
            SocketManagerWindow? second = null;
            bool firstClosed = false;
            try
            {
                second = CreateWindow(manager);
                ListView firstList = Element<ListView>(first, "MessagesListView");
                ListView secondList = Element<ListView>(second, "MessagesListView");
                ListCollectionView firstView = Assert.IsType<ListCollectionView>(firstList.ItemsSource);
                Assert.IsType<ListCollectionView>(secondList.ItemsSource);
                Assert.NotSame(firstList.ItemsSource, secondList.ItemsSource);

                Element<TextBox>(first, "SearchTextBox").Text = "request-1";
                Element<ComboBox>(second, "DirectionFilterCombo").SelectedIndex = 2;
                Assert.Same(received, Assert.Single(firstList.Items.Cast<SocketMessage>()));
                Assert.Same(sent, Assert.Single(secondList.Items.Cast<SocketMessage>()));

                first.Close();
                firstClosed = true;
                Assert.Same(sent, Assert.Single(secondList.Items.Cast<SocketMessage>()));

                int closedViewChanges = 0;
                void ClosedViewChanged(object? sender, NotifyCollectionChangedEventArgs args) => closedViewChanges++;
                ((INotifyCollectionChanged)firstView).CollectionChanged += ClosedViewChanged;
                try
                {
                    manager.MessageManager.Messages.Add(Message("response-2", SocketMessageDirection.Sent, "second response"));
                    // Match the closed window's former filter as well, so a retained filter cannot hide a leaked subscription.
                    manager.MessageManager.Messages.Add(Message("request-1-followup", SocketMessageDirection.Received, "later request"));
                    RefreshLayout(second);
                    Assert.Equal(0, closedViewChanges);
                    Assert.Equal(2, secondList.Items.Count);
                    Assert.All(secondList.Items.Cast<SocketMessage>(), item => Assert.Equal(SocketMessageDirection.Sent, item.Direction));
                }
                finally
                {
                    ((INotifyCollectionChanged)firstView).CollectionChanged -= ClosedViewChanged;
                }
            }
            finally
            {
                if (!firstClosed)
                    first.Close();
                second?.Close();
            }
        });
    }

    [Theory]
    [InlineData("zh-CN", false)]
    [InlineData("en-US", false)]
    [InlineData("zh-CN", true)]
    [InlineData("en-US", true)]
    public void MinimumSizeKeepsToolbarAndBothPanesInsideTheWindow(string cultureName, bool dark)
    {
        SocketMessage message = Message("capture-001", SocketMessageDirection.Sent, "{\"status\":\"ready\"}");
        message.ClientEndPoint = "[2001:db8:1234:5678::42]:51000";
        message.EventName = "AnEventNameLongEnoughToRequireTrimmingAtMinimumWidth";

        WithWindow([message], (window, _) =>
        {
            Element<ListView>(window, "MessagesListView").SelectedItem = message;
            Grid root = RefreshLayout(window, minimum: true);
            FrameworkElement toolbar = Element<FrameworkElement>(window, "MessageToolbar");
            FrameworkElement list = Element<ListView>(window, "MessagesListView");
            FrameworkElement messagesPane = Element<FrameworkElement>(window, "MessagesPane");
            FrameworkElement detailsPane = Element<FrameworkElement>(window, "DetailsPane");
            FrameworkElement panes = Element<FrameworkElement>(window, "MessagePanesGrid");

            AssertInside(panes, root);
            AssertInside(messagesPane, root);
            AssertInside(detailsPane, root);
            AssertInside(toolbar, messagesPane);
            AssertInside(list, messagesPane);
            Assert.True(BoundsIn(toolbar, root).Bottom <= BoundsIn(list, root).Top + 1,
                "The wrapping toolbar must have its own row above the message list.");
            Assert.True(BoundsIn(messagesPane, root).Right <= BoundsIn(detailsPane, root).Left + 1,
                "The message list and details pane must not overlap at the minimum window size.");

            foreach (FrameworkElement control in VisualDescendants(toolbar)
                .Where(element => element is Button or TextBox or ComboBox or CheckBox))
            {
                if (control.Visibility == Visibility.Visible && control.ActualWidth > 0 && control.ActualHeight > 0)
                    AssertInside(control, toolbar);
            }
            AssertInside(Element<Button>(window, "CopyDetailButton"), detailsPane);
            AssertInside(Element<Button>(window, "CopyFormattedDetailButton"), detailsPane);
            AssertInside(Element<TextBox>(window, "DetailContentTextBox"), detailsPane);

            if (dark)
            {
                Style sectionTitleStyle = Assert.IsType<Style>(window.FindResource("SectionTitleTextBlock"));
                SolidColorBrush expectedForeground = Assert.IsType<SolidColorBrush>(window.FindResource("GlobalTextBrush"));
                TextBlock[] sectionTitles = VisualDescendants(root).OfType<TextBlock>()
                    .Where(title => ReferenceEquals(title.Style, sectionTitleStyle)).ToArray();
                Assert.NotEmpty(sectionTitles);
                foreach (TextBlock title in sectionTitles)
                {
                    SolidColorBrush actualForeground = Assert.IsType<SolidColorBrush>(title.Foreground);
                    Assert.Equal(expectedForeground.Color, actualForeground.Color);
                    Assert.Equal(expectedForeground.Opacity, actualForeground.Opacity);
                }
            }
        }, cultureName, dark);
    }

    [Fact]
    public void RenderPreviewsOnlyWhenAnOutputDirectoryIsRequested()
    {
        string? outputDirectory = Environment.GetEnvironmentVariable("COLORVISION_SOCKET_PREVIEW_DIRECTORY");
        if (string.IsNullOrWhiteSpace(outputDirectory))
            return;

        Assert.True(Path.IsPathFullyQualified(outputDirectory), "The preview directory must be an absolute path.");
        Directory.CreateDirectory(outputDirectory);
        foreach ((string name, string culture, bool dark, bool minimum, bool populated) in new[]
        {
            ("socket-messages.png", "zh-CN", false, false, true),
            ("socket-empty.png", "zh-CN", false, false, false),
            ("socket-dark-minimum.png", "zh-CN", true, true, true),
            ("socket-english-minimum.png", "en-US", false, true, true),
        })
        {
            SocketMessage[] messages = populated
                ? Enumerable.Range(1, 8).Select(index => Message($"capture-{index:000}",
                    index % 2 == 0 ? SocketMessageDirection.Sent : SocketMessageDirection.Received,
                    $"{{\"EventName\":\"Capture\",\"MsgID\":\"capture-{index:000}\",\"Code\":0,\"data\":{{\"status\":\"ready\"}}}}")).ToArray()
                : [];
            WithWindow(messages, (window, _) =>
            {
                if (populated)
                    Element<ListView>(window, "MessagesListView").SelectedIndex = 0;
                Grid root = RefreshLayout(window, minimum);
                Size viewport = new(minimum ? window.MinWidth : window.Width,
                    (minimum ? window.MinHeight : window.Height) - 40);
                Brush background = Assert.IsAssignableFrom<Brush>(window.FindResource("GlobalBackground"));
                VisualBrush content = new(root)
                {
                    AutoLayoutContent = false,
                    Stretch = Stretch.Fill,
                };
                DrawingVisual preview = new();
                using (DrawingContext drawing = preview.RenderOpen())
                {
                    // Render the full client viewport, including the root's outer margin, on an opaque theme background.
                    drawing.DrawRectangle(background, null, new Rect(viewport));
                    drawing.DrawRectangle(content, null, new Rect(new Point(root.Margin.Left, root.Margin.Top), root.RenderSize));
                }
                RenderTargetBitmap bitmap = new((int)Math.Ceiling(viewport.Width * 1.5),
                    (int)Math.Ceiling(viewport.Height * 1.5), 144, 144, PixelFormats.Pbgra32);
                bitmap.Render(preview);
                byte[] cornerPixel = new byte[4];
                bitmap.CopyPixels(new Int32Rect(bitmap.PixelWidth - 1, bitmap.PixelHeight - 1, 1, 1), cornerPixel, 4, 0);
                Assert.Equal((byte)255, cornerPixel[3]);
                PngBitmapEncoder encoder = new();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using FileStream output = File.Create(Path.Combine(outputDirectory, name));
                encoder.Save(output);
            }, culture, dark);
        }
    }

    private static void AssertNoSelection(SocketManagerWindow window)
    {
        Assert.Null(Element<FrameworkElement>(window, "DetailPanel").DataContext);
        Assert.Equal(Visibility.Visible, Element<FrameworkElement>(window, "NoSelectionPanel").Visibility);
        Assert.Equal(Visibility.Collapsed, Element<FrameworkElement>(window, "MessageMetadataPanel").Visibility);
        Assert.Equal(Visibility.Collapsed, Element<FrameworkElement>(window, "EmptyContentPanel").Visibility);
        Assert.Equal(string.Empty, Element<TextBox>(window, "DetailContentTextBox").Text);
        Assert.False(Element<Button>(window, "CopyDetailButton").IsEnabled);
        Assert.False(Element<Button>(window, "CopyFormattedDetailButton").IsEnabled);
    }

    private static void WithWindow(IEnumerable<SocketMessage> messages,
        Action<SocketManagerWindow, ObservableCollection<SocketMessage>> action,
        string cultureName = "zh-CN", bool dark = false)
    {
        WithEnvironment(() =>
        {
            SocketManager manager = CreateManager(messages);
            SocketManagerWindow window = CreateWindow(manager);
            try
            {
                RefreshLayout(window);
                action(window, manager.MessageManager.Messages);
            }
            finally
            {
                window.Close();
            }
        }, cultureName, dark);
    }

    private static void WithEnvironment(Action action, string cultureName = "zh-CN", bool dark = false)
    {
        WpfTestHost.Invoke(() =>
        {
            CultureInfo previousCulture = CultureInfo.CurrentCulture;
            CultureInfo previousUICulture = CultureInfo.CurrentUICulture;
            CultureInfo? previousResourceCulture = SocketResources.Culture;
            List<ResourceDictionary> addedDictionaries = [];
            try
            {
                CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
                SocketResources.Culture = culture;
                foreach (string source in new[]
                {
                    $"/HandyControl;component/Themes/basic/colors/{(dark ? "colorsdark" : "colors")}.xaml",
                    "/HandyControl;component/Themes/Theme.xaml",
                    $"/ColorVision.Themes;component/Themes/{(dark ? "Dark" : "White")}.xaml",
                    "/ColorVision.Themes;component/Themes/Base.xaml",
                })
                {
                    ResourceDictionary dictionary = new() { Source = new Uri(source, UriKind.Relative) };
                    Application.Current.Resources.MergedDictionaries.Add(dictionary);
                    addedDictionaries.Add(dictionary);
                }
                action();
            }
            finally
            {
                for (int index = addedDictionaries.Count - 1; index >= 0; index--)
                    Application.Current.Resources.MergedDictionaries.Remove(addedDictionaries[index]);
                SocketResources.Culture = previousResourceCulture;
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUICulture;
            }
        });
    }

    private static SocketManagerWindow CreateWindow(SocketManager manager)
    {
        SocketManagerWindow window = new(manager);
        if (window.FindName("AutoScrollCheckBox") is CheckBox autoScroll)
            autoScroll.IsChecked = false;
        return window;
    }

    private static SocketManager CreateManager(IEnumerable<SocketMessage> messages)
    {
        // Loaded payloads make LoadContent return immediately; no real database or config service is initialized.
        var messageManager = (SocketMessageManager)RuntimeHelpers.GetUninitializedObject(typeof(SocketMessageManager));
        messageManager.Messages = new ObservableCollection<SocketMessage>(messages);
        messageManager.Config = new SocketMessageManagerConfig();
        var jsonDispatcher = (SocketJsonDispatcher)RuntimeHelpers.GetUninitializedObject(typeof(SocketJsonDispatcher));
        var textDispatcher = (SocketTextDispatcher)RuntimeHelpers.GetUninitializedObject(typeof(SocketTextDispatcher));
        SocketConfig config = new()
        {
            IPAddress = "127.0.0.1",
            ServerPort = 0,
            SocketBufferSize = 4096,
            SocketPhraseType = SocketPhraseType.Json,
            IsServerEnabled = false,
        };
        return new SocketManager(config, new ProhibitedListenerFactory(),
            _ => throw new InvalidOperationException("Layout tests must not start Socket workers."),
            new SocketWorkerTracker(), jsonDispatcher, textDispatcher, messageManager,
            refreshNetworkAccessStatus: false);
    }

    private static SocketMessage Message(string id, SocketMessageDirection direction, string? content) => new()
    {
        ClientEndPoint = "127.0.0.1:51000",
        Direction = direction,
        MessageTime = new DateTime(2026, 8, 30, 12, 30, 0),
        EventName = "Capture",
        MsgID = id,
        ResponseCode = direction == SocketMessageDirection.Sent ? 0 : null,
        Content = content,
        ContentPreview = GzipTextPayloadCodec.CreatePreview(content, SocketMessagePayloadStorage.PreviewCharacters),
    };

    private static Grid RefreshLayout(SocketManagerWindow window, bool minimum = false)
    {
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        Grid root = Assert.IsType<Grid>(window.Content);
        Size size = new(minimum ? window.MinWidth : window.Width, (minimum ? window.MinHeight : window.Height) - 40);
        root.Measure(size);
        root.Arrange(new Rect(size));
        root.UpdateLayout();
        return root;
    }

    private static T Element<T>(SocketManagerWindow window, string name) where T : FrameworkElement
        => Assert.IsAssignableFrom<T>(window.FindName(name));

    private static Rect BoundsIn(FrameworkElement element, Visual ancestor)
        => element.TransformToAncestor(ancestor).TransformBounds(new Rect(element.RenderSize));

    private static void AssertInside(FrameworkElement element, FrameworkElement ancestor)
    {
        Rect bounds = BoundsIn(element, ancestor);
        Assert.True(bounds.Width > 0 && bounds.Height > 0, $"{element.Name} must have usable layout space.");
        Assert.True(bounds.Left >= -1 && bounds.Top >= -1
            && bounds.Right <= ancestor.ActualWidth + 1 && bounds.Bottom <= ancestor.ActualHeight + 1,
            $"{element.Name} must fit inside {ancestor.Name}: {bounds}, parent {ancestor.RenderSize}.");
    }

    private static IEnumerable<FrameworkElement> VisualDescendants(DependencyObject parent)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is FrameworkElement element)
                yield return element;
            foreach (FrameworkElement descendant in VisualDescendants(child))
                yield return descendant;
        }
    }

    private sealed class ProhibitedListenerFactory : ISocketServerListenerFactory
    {
        public ISocketServerListener Create(SocketServerSettings settings)
            => throw new InvalidOperationException("Layout tests must not create network listeners.");
    }

    private sealed class RecordingCleanupLauncher : ISocketDatabaseCleanupWindowLauncher
    {
        public int OpenCalls { get; private set; }
        public Window? Owner { get; private set; }

        public void OpenWindow(Window owner)
        {
            OpenCalls++;
            Owner = owner;
        }
    }
}
