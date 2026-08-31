using ColorVision.Common.MVVM;
using ColorVision.UI.Serach;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class SearchPaletteTests
{
    [Fact]
    public async Task EditingQueryInvalidatesOldSelectionBeforeDebounce()
    {
        var model = new SearchPaletteViewModel((_, _, _) => Task.FromResult(Response(Hit("Open"))), _ => true, TimeSpan.FromMilliseconds(100));
        model.Open();
        await model.PendingSearch;
        Assert.True(model.TryGetSelection(out _));
        model.SearchText = "new query";
        Assert.Empty(model.Results);
        Assert.False(model.TryGetSelection(out _));
        model.Close();
        await model.PendingSearch;
        Assert.False(model.IsOpen);
        Assert.False(model.IsSearching);
    }

    [Fact]
    public async Task LatestQueryWinsEvenWhenOldProviderIgnoresCancellation()
    {
        var oldStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldResponse = new TaskCompletionSource<SearchQueryResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var model = new SearchPaletteViewModel((query, _, _) =>
        {
            if (query == "old") { oldStarted.SetResult(); return oldResponse.Task; }
            return Task.FromResult(Response(Hit(query.Length == 0 ? "initial" : query)));
        }, _ => true, TimeSpan.Zero);
        model.Open();
        await model.PendingSearch;
        model.SearchText = "old";
        await oldStarted.Task;
        Task pendingOld = model.PendingSearch;
        model.SearchText = "new";
        await model.PendingSearch;
        oldResponse.SetResult(Response(Hit("obsolete")));
        await pendingOld;
        Assert.Equal("new", Assert.Single(model.Results).Title);
        Assert.True(model.TryGetSelection(out _));
        model.Close();
    }

    [Fact]
    public async Task ClosingPreventsLateResultsAndReopeningStartsFresh()
    {
        int calls = 0;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<SearchQueryResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var model = new SearchPaletteViewModel((_, _, _) =>
        {
            if (++calls == 1) { started.SetResult(); return pending.Task; }
            return Task.FromResult(Response(Hit("current")));
        }, _ => true, TimeSpan.Zero);
        model.Open();
        await started.Task;
        Task oldTask = model.PendingSearch;
        model.Close();
        Assert.False(model.TryGetSelection(out _));
        model.Open();
        await model.PendingSearch;
        pending.SetResult(Response(Hit("stale")));
        await oldTask;
        Assert.Equal("current", Assert.Single(model.Results).Title);
        model.Close();
    }

    [Fact]
    public async Task FailureAndPartialResultsHaveExplicitStatus()
    {
        var model = new SearchPaletteViewModel((query, _, _) => query == "fail"
            ? Task.FromException<SearchQueryResult>(new InvalidOperationException("isolated failure"))
            : Task.FromResult(new SearchQueryResult([Hit("available")], ["offline source"], false)), _ => true, TimeSpan.Zero);
        model.Open();
        await model.PendingSearch;
        Assert.Single(model.Results);
        Assert.Equal(SearchPaletteText.Get("PartialFailure"), model.Status);
        model.SearchText = "fail";
        await model.PendingSearch;
        Assert.True(model.IsEmpty);
        Assert.False(model.TryGetSelection(out _));
        Assert.Equal(SearchPaletteText.Get("SearchFailed"), model.Status);
        model.Close();
    }

    [Fact]
    public async Task CategoriesRefreshAndSelectionStartsAtAnAvailableResult()
    {
        string? lastCategory = null;
        var model = new SearchPaletteViewModel((_, category, _) =>
        {
            lastCategory = category;
            return Task.FromResult(Response(Hit("disabled"), Hit("enabled")));
        }, item => item.Title == "enabled", TimeSpan.Zero);
        model.Open();
        await model.PendingSearch;
        Assert.Equal("enabled", model.Selected?.Title);
        model.Category = model.Categories.Single(item => item.Key == "Settings");
        await model.PendingSearch;
        Assert.Equal("Settings", lastCategory);
        model.MoveSelection(-1);
        Assert.Equal("disabled", model.Selected?.Title);
        model.MoveSelection(-1);
        Assert.Equal("disabled", model.Selected?.Title);
        model.Close();
    }

    [Fact]
    public void DeniedCommandNeverExecutesAndRoutedCommandsUseTheOriginalTarget()
    {
        WpfTestHost.Invoke(() =>
        {
            int deniedCalls = 0;
            var denied = new RelayCommand(_ => deniedCalls++, _ => false);
            Assert.False(SearchCommandExecutor.TryExecute(denied, null, null));
            Assert.Equal(0, deniedCalls);
            var original = new TextBox();
            var unrelated = new TextBox();
            var command = new RoutedCommand();
            int originalCalls = 0;
            int unrelatedCalls = 0;
            original.CommandBindings.Add(new(command, (_, e) => { originalCalls++; e.Handled = true; }, (_, e) => { e.CanExecute = true; e.Handled = true; }));
            unrelated.CommandBindings.Add(new(command, (_, e) => { unrelatedCalls++; e.Handled = true; }, (_, e) => { e.CanExecute = true; e.Handled = true; }));
            Assert.True(SearchCommandExecutor.TryExecute(command, null, original));
            Assert.Equal(1, originalCalls);
            Assert.Equal(0, unrelatedCalls);
            Assert.False(SearchCommandExecutor.TryExecute(command, null, null));
        });
    }

    [Fact]
    public void PaletteClosesBeforeCommandExecutionAndDoesNotExecuteTwice()
    {
        WpfTestHost.Invoke(() =>
        {
            var events = new List<string>();
            var item = Hit("Open", new RelayCommand(_ => events.Add("execute")));
            var control = new SearchControl((_, _, _) => Task.FromResult(Response(item)), _ => events.Add("recent"));
            control.Closed += (_, _) => events.Add("close");
            control.Open(null);
            Complete(control.Model.PendingSearch);
            Assert.True(control.SubmitSelection());
            Assert.Equal(new[] { "close", "execute", "recent" }, events);
            Assert.False(control.SubmitSelection());
        });
    }

    [Fact]
    public void AvailabilityIsRecheckedAfterCloseAndChangedTargetIsRejected()
    {
        WpfTestHost.Invoke(() =>
        {
            bool available = true;
            int calls = 0;
            var control = new SearchControl((_, _, _) => Task.FromResult(Response(Hit("Open", new RelayCommand(_ => calls++, _ => available)))));
            control.Closed += (_, _) => available = false;
            control.Open(null);
            Complete(control.Model.PendingSearch);
            Assert.False(control.SubmitSelection());
            Assert.Equal(0, calls);

            var target = new TextBox { DataContext = new object() };
            var routed = new RoutedCommand();
            target.CommandBindings.Add(new(routed, (_, _) => calls++, (_, e) => { e.CanExecute = true; e.Handled = true; }));
            var routedControl = new SearchControl((_, _, _) => Task.FromResult(Response(Hit("Save", routed))));
            routedControl.Open(target);
            Complete(routedControl.Model.PendingSearch);
            target.DataContext = new object();
            Assert.False(routedControl.SubmitSelection());
            Assert.True(routedControl.Model.IsOpen);
            Assert.Equal(SearchPaletteText.Get("TargetUnavailable"), routedControl.Model.Status);
            Assert.Equal(0, calls);
            routedControl.Close();
        });
    }

    [Fact]
    public void ImeCommitCannotClearANewerCompositionOrSubmitAnAction()
    {
        WpfTestHost.Invoke(() =>
        {
            int calls = 0;
            var control = new SearchControl((_, _, _) => Task.FromResult(Response(Hit("Open", new RelayCommand(_ => calls++)))));
            control.Open(null);
            Complete(control.Model.PendingSearch);
            InvokePrivate(control, "CompositionStarted", control, null);
            Assert.False(control.SubmitSelection());
            InvokePrivate(control, "CompositionCompleted", control, null);
            InvokePrivate(control, "CompositionStarted", control, null);
            control.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.False(control.SubmitSelection());
            Assert.Equal(0, calls);
            control.Close();
        });
    }

    [Fact]
    public void ClosingCannotRetargetARoutedActionThroughAFocusSideEffect()
    {
        WpfTestHost.Invoke(() =>
        {
            int calls = 0;
            var target = new TextBox { DataContext = new object() };
            var command = new RoutedCommand();
            target.CommandBindings.Add(new(command, (_, _) => calls++, (_, e) => { e.CanExecute = true; e.Handled = true; }));
            var control = new SearchControl((_, _, _) => Task.FromResult(Response(Hit("Save", command))));
            control.Closed += (_, _) => target.DataContext = new object();
            control.Open(target);
            Complete(control.Model.PendingSearch);
            Assert.False(control.SubmitSelection());
            Assert.Equal(0, calls);
        });
    }

    [Theory]
    [InlineData("日志级别", "日志")]
    [InlineData("Open OPEN file", "open")]
    [InlineData("Go [x]+ file", "[x]+")]
    [InlineData("没有匹配", "other")]
    public void HighlightingPreservesLiteralText(string title, string query)
    {
        var segments = SearchHighlightTextBlock.SplitHighlights(title, query).ToArray();
        Assert.Equal(title, string.Concat(segments.Select(segment => segment.Text)));
        Assert.Equal(title.Contains(query, StringComparison.OrdinalIgnoreCase), segments.Any(segment => segment.Matched));
    }

    [Theory]
    [InlineData(false, "zh-CN", 720)]
    [InlineData(true, "zh-CN", 720)]
    [InlineData(false, "en-US", 400)]
    [InlineData(true, "en-US", 400)]
    public void RealPaletteXamlLaysOutWithThemesAndNarrowWidths(bool dark, string culture, int width)
        => WithPalette(dark, culture, width, control =>
        {
            Assert.Equal(256, Assert.IsType<TextBox>(control.FindName("Searchbox")).MaxLength);
            Assert.Equal(7, Assert.IsType<ComboBox>(control.FindName("CategoryFilter")).Items.Count);
            ListBox list = Assert.IsType<ListBox>(control.FindName("ListViewSearch"));
            Assert.Equal(5, list.Items.Count);
            Assert.True(list.ActualWidth <= width);
            Assert.True(control.ActualHeight <= 620);
            Assert.DoesNotContain(Descendants(control).OfType<TextBlock>(), text => text.Text.StartsWith("Category", StringComparison.Ordinal));
            AssertPaletteLayout(control, width);
        });

    [Theory]
    [InlineData(false, "zh-CN", 720, 300, false)]
    [InlineData(false, "zh-CN", 720, 300, true)]
    [InlineData(true, "zh-CN", 720, 360, false)]
    [InlineData(true, "zh-CN", 720, 360, true)]
    [InlineData(false, "en-US", 400, 300, false)]
    [InlineData(false, "en-US", 400, 300, true)]
    [InlineData(true, "en-US", 400, 360, false)]
    [InlineData(true, "en-US", 400, 360, true)]
    public void ShortPaletteKeepsControlsVisibleAndScrollsToTheLastResult(bool dark, string culture, int width, int maxHeight, bool withStatus)
        => WithPalette(dark, culture, width, control =>
        {
            AssertPaletteChromeLayout(control, width, maxHeight);
            var list = Assert.IsType<ListBox>(control.FindName("ListViewSearch"));
            ScrollViewer scroll = Assert.Single(Descendants(list).OfType<ScrollViewer>());
            ScrollContentPresenter viewport = Assert.Single(Descendants(list).OfType<ScrollContentPresenter>());
            Assert.True(viewport.ActualHeight > 0, "The result viewport must retain visible space in a short palette.");
            Assert.True(scroll.ScrollableHeight > 0, "Short palettes must scroll results instead of overflowing their footer.");
            var first = Assert.IsType<ListBoxItem>(list.ItemContainerGenerator.ContainerFromIndex(0));
            AssertVisibleResultTitle(first, viewport);

            list.ScrollIntoView(list.Items[list.Items.Count - 1]);
            scroll.ScrollToEnd();
            for (int pass = 0; pass < 2; pass++)
            {
                control.UpdateLayout();
                control.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            }
            Assert.True(scroll.VerticalOffset > 0);
            var last = Assert.IsType<ListBoxItem>(list.ItemContainerGenerator.ContainerFromIndex(list.Items.Count - 1));
            AssertVisibleResultTitle(last, viewport);
            AssertPaletteChromeLayout(control, width, maxHeight);
        }, maxHeight, withStatus);

    [Fact]
    public void RenderPreviewsWhenExplicitlyRequested()
    {
        string? directory = Environment.GetEnvironmentVariable("COLORVISION_SEARCH_PREVIEW_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Assert.True(Path.IsPathFullyQualified(directory));
        Directory.CreateDirectory(directory);
        foreach ((bool dark, string culture, int width) in new[] { (false, "zh-CN", 720), (true, "zh-CN", 720), (true, "en-US", 400) })
        {
            WithPalette(dark, culture, width, control =>
            {
                RenderPalette(control, width, Path.Combine(directory, $"search-{(dark ? "dark" : "light")}-{culture}-{width}.png"));
            });
        }
    }

    private static void WithPalette(bool dark, string culture, int width, Action<SearchControl> action, int maxHeight = 620, bool withStatus = false)
    {
        WpfTestHost.Invoke(() =>
        {
            ResourceDictionary resources = Application.Current.Resources;
            var locals = resources.Keys.Cast<object>().ToDictionary(key => key, key => resources[key]);
            var dictionaries = resources.MergedDictionaries.ToList();
            CultureInfo originalCulture = CultureInfo.CurrentCulture;
            CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
            SearchControl? control = null;
            try
            {
                resources.Clear();
                resources.MergedDictionaries.Clear();
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
                foreach (string source in new[]
                {
                    $"/HandyControl;component/Themes/basic/colors/{(dark ? "colorsdark" : "colors")}.xaml",
                    "/HandyControl;component/Themes/Theme.xaml",
                    $"/ColorVision.Themes;component/Themes/{(dark ? "Dark" : "White")}.xaml",
                    "/ColorVision.Themes;component/Themes/Base.xaml"
                }) resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source, UriKind.Relative) });
                bool english = culture.StartsWith("en", StringComparison.Ordinal);
                SearchResultItem[] items =
                [
                    Hit(english ? "Open log viewer" : "打开日志窗口", category: "Commands", description: english ? "Inspect application logs" : "查看应用运行日志与诊断信息", shortcut: "Ctrl+Alt+L"),
                    Hit(english ? "Log level" : "日志级别", category: "Settings", description: english ? "General · Logging" : "常规 · 控制日志的详细程度"),
                    Hit(english ? "Inspection workflow" : "产品检测流程", category: "Templates", description: english ? "Open the selected workflow template" : "打开对应的工作流模板"),
                    Hit(english ? "Capture image" : "采集图像", category: "FlowNodes", description: english ? "Inspection workflow · Camera" : "产品检测流程 · 相机节点"),
                    Hit(english ? "Advanced inspection tool" : "高级检测工具", new RelayCommand(_ => { }, _ => false), "Tools", english ? "Requires the matching permission" : "当前权限不可用")
                ];
                control = new SearchControl((_, _, _) => Task.FromResult(new SearchQueryResult(items, withStatus ? ["isolated unavailable source"] : [], false)))
                {
                    MaxHeight = maxHeight
                };
                var layoutRoot = new Grid { Width = width, Height = 620 };
                control.VerticalAlignment = VerticalAlignment.Top;
                layoutRoot.Children.Add(control);
                control.Open(null);
                Complete(control.Model.PendingSearch);
                for (int pass = 0; pass < 2; pass++)
                {
                    layoutRoot.Measure(new Size(width, 620));
                    layoutRoot.Arrange(new Rect(0, 0, width, 620));
                    layoutRoot.UpdateLayout();
                    control.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                }
                action(control);
            }
            finally
            {
                control?.Close();
                resources.Clear();
                resources.MergedDictionaries.Clear();
                foreach (ResourceDictionary dictionary in dictionaries) resources.MergedDictionaries.Add(dictionary);
                foreach ((object key, object value) in locals) resources[key] = value;
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUiCulture;
            }
        });
    }

    private static void AssertPaletteLayout(SearchControl control, int width)
    {
        AssertPaletteChromeLayout(control, width, 620);
        var list = Assert.IsType<ListBox>(control.FindName("ListViewSearch"));
        var lastItem = Assert.IsType<ListBoxItem>(list.ItemContainerGenerator.ContainerFromIndex(list.Items.Count - 1));
        AssertInside(lastItem, control, "The final result must not be clipped by the palette.");
        ScrollContentPresenter viewport = Assert.Single(Descendants(list).OfType<ScrollContentPresenter>());
        AssertInside(lastItem, viewport, "The five preview results must fit in the list viewport.");

    }

    private static void AssertPaletteChromeLayout(SearchControl control, int width, int maxHeight)
    {
        Assert.InRange(control.ActualWidth, width - 1, width + 1);
        Assert.InRange(control.ActualHeight, 200, maxHeight);
        var input = Assert.IsType<TextBox>(control.FindName("Searchbox"));
        var filter = Assert.IsType<ComboBox>(control.FindName("CategoryFilter"));
        var list = Assert.IsType<ListBox>(control.FindName("ListViewSearch"));
        ScrollContentPresenter viewport = Assert.Single(Descendants(list).OfType<ScrollContentPresenter>());
        TextBlock footerHint = Assert.Single(Descendants(control).OfType<TextBlock>().Where(text => text.Text == SearchPaletteText.KeyboardHint));
        Button footerSettings = Assert.Single(Descendants(control).OfType<Button>().Where(button => Equals(button.Content, SearchPaletteText.Settings)));
        AssertInside(input, control, "The search input must remain visible in the palette.");
        AssertInside(filter, control, "The category filter must remain visible in the palette.");
        AssertInside(viewport, control, "The result viewport must not overflow the palette.");
        AssertInside(footerHint, control, "The keyboard footer must remain inside the rendered bounds.");
        AssertInside(footerSettings, control, "The settings footer button must remain inside the rendered bounds.");
        Rect inputBounds = input.TransformToAncestor(control).TransformBounds(new Rect(input.RenderSize));
        Rect filterBounds = filter.TransformToAncestor(control).TransformBounds(new Rect(filter.RenderSize));
        Rect viewportBounds = viewport.TransformToAncestor(control).TransformBounds(new Rect(viewport.RenderSize));
        Rect hintBounds = footerHint.TransformToAncestor(control).TransformBounds(new Rect(footerHint.RenderSize));
        Rect settingsBounds = footerSettings.TransformToAncestor(control).TransformBounds(new Rect(footerSettings.RenderSize));
        double footerTop = Math.Min(hintBounds.Top, settingsBounds.Top);
        Assert.True(inputBounds.Bottom <= filterBounds.Top + 1, "The search input and category filter must not overlap.");
        Assert.True(filterBounds.Bottom <= viewportBounds.Top + 1, "The category filter and result viewport must not overlap.");
        Assert.True(viewportBounds.Bottom <= footerTop + 1, "The result viewport and footer must not overlap.");
        if (control.Model.HasStatus)
        {
            TextBlock status = Assert.Single(Descendants(control).OfType<TextBlock>().Where(text => text.Text == control.Model.Status));
            AssertInside(status, control, "A source failure status must remain visible in the short palette.");
            Rect statusBounds = status.TransformToAncestor(control).TransformBounds(new Rect(status.RenderSize));
            Assert.True(statusBounds.Top >= viewportBounds.Bottom - 1 && statusBounds.Bottom <= footerTop + 1,
                "The source failure status must not overlap results or the footer.");
        }

        Color primary = Assert.IsType<SolidColorBrush>(control.FindResource("GlobalTextBrush")).Color;
        Color secondary = Assert.IsType<SolidColorBrush>(control.FindResource("SecondaryTextBrush")).Color;
        Assert.All(Descendants(list).OfType<SearchHighlightTextBlock>(), text => Assert.Equal(primary, Assert.IsType<SolidColorBrush>(text.Foreground).Color));
        Assert.Equal(primary, Assert.IsType<SolidColorBrush>(((TextBox)control.FindName("Searchbox")).Foreground).Color);
        Assert.Equal(secondary, Assert.IsType<SolidColorBrush>(footerHint.Foreground).Color);
        Assert.Equal(secondary, Assert.IsType<SolidColorBrush>(footerSettings.Foreground).Color);
    }

    private static void AssertVisibleResultTitle(ListBoxItem item, ScrollContentPresenter viewport)
    {
        SearchHighlightTextBlock title = Assert.Single(Descendants(item).OfType<SearchHighlightTextBlock>());
        Rect bounds = title.TransformToAncestor(viewport).TransformBounds(new Rect(title.RenderSize));
        Assert.True(title.ActualWidth > 0 && title.ActualHeight > 0 && bounds.Bottom > 0 && bounds.Top < viewport.ActualHeight,
            $"The result title must be visible after scrolling. Title bounds={bounds}; viewport={viewport.RenderSize}.");
    }

    private static void AssertInside(FrameworkElement element, FrameworkElement ancestor, string message)
    {
        Assert.True(element.ActualWidth > 0 && element.ActualHeight > 0, message);
        Rect bounds = element.TransformToAncestor(ancestor).TransformBounds(new Rect(element.RenderSize));
        Assert.True(bounds.Left >= -1 && bounds.Top >= -1 && bounds.Right <= ancestor.ActualWidth + 1 && bounds.Bottom <= ancestor.ActualHeight + 1,
            $"{message} Element bounds={bounds}; ancestor={ancestor.RenderSize}.");
    }

    private static void RenderPalette(SearchControl control, int width, string path)
    {
        control.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        control.UpdateLayout();
        AssertPaletteLayout(control, width);
        Rect bounds = new(0, 0, control.ActualWidth, control.ActualHeight);
        DrawingVisual visual = new();
        using (DrawingContext context = visual.RenderOpen())
        {
            context.DrawRectangle((Brush)control.FindResource("GlobalBackground"), null, bounds);
            // VisualBrush's default relative viewbox follows descendant drawing bounds,
            // not the arranged control size. Fix both maps to the same DIP rectangle
            // so the preview is 1:1 and cannot stretch/crop away the footer.
            VisualBrush brush = new(control)
            {
                AutoLayoutContent = false,
                Stretch = Stretch.None,
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Top,
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = bounds,
                ViewportUnits = BrushMappingMode.Absolute,
                Viewport = bounds,
                TileMode = TileMode.None
            };
            context.DrawRectangle(brush, null, bounds);
        }
        RenderTargetBitmap bitmap = new((int)Math.Ceiling(bounds.Width), (int)Math.Ceiling(bounds.Height), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        Button footerSettings = Assert.Single(Descendants(control).OfType<Button>().Where(button => Equals(button.Content, SearchPaletteText.Settings)));
        Rect footerBounds = footerSettings.TransformToAncestor(control).TransformBounds(new Rect(footerSettings.RenderSize));
        Int32Rect footerPixels = new((int)Math.Floor(footerBounds.Left), (int)Math.Floor(footerBounds.Top),
            (int)Math.Ceiling(footerBounds.Width), (int)Math.Ceiling(footerBounds.Height));
        int stride = footerPixels.Width * 4;
        byte[] pixels = new byte[stride * footerPixels.Height];
        bitmap.CopyPixels(footerPixels, pixels, stride, 0);
        Color foreground = Assert.IsType<SolidColorBrush>(footerSettings.Foreground).Color;
        int foregroundPixels = 0;
        for (int offset = 0; offset < pixels.Length; offset += 4)
        {
            if (pixels[offset + 3] == 255 && Math.Abs(pixels[offset] - foreground.B)
                + Math.Abs(pixels[offset + 1] - foreground.G) + Math.Abs(pixels[offset + 2] - foreground.R) < 60)
                foregroundPixels++;
        }
        Assert.True(foregroundPixels > 3, "The rendered preview must contain the footer label, not only an empty background at its layout coordinates.");
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream output = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        encoder.Save(output);
    }

    private static SearchResultItem Hit(string title, ICommand? command = null, string category = "Commands", string description = "Description", string shortcut = "")
        => new(new SearchMeta { GuidId = title, Header = title, Description = description, CategoryKey = category, Command = command ?? new RelayCommand(_ => { }) }, "test", shortcut);
    private static SearchQueryResult Response(params SearchResultItem[] items) => new(items, [], false);
    private static void InvokePrivate(object instance, string method, params object?[] parameters)
        => instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, parameters);

    private static void Complete(Task task)
    {
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (_, _) => frame.Continue = false;
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            _ = task.ContinueWith(_ => dispatcher.BeginInvoke(DispatcherPriority.Send, () => frame.Continue = false), TaskScheduler.Default);
            timer.Start();
            try { Dispatcher.PushFrame(frame); }
            finally { timer.Stop(); }
        }
        Assert.True(task.IsCompleted, "The isolated search did not finish within five seconds.");
        task.GetAwaiter().GetResult();
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child)) yield return descendant;
        }
    }
}
