using ColorVision.Common.MVVM;
using ColorVision.UI.Menus.Base.File;
using ColorVision.UI.Serach;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
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

    [Theory]
    [InlineData(false, "zh-CN")]
    [InlineData(true, "zh-CN")]
    [InlineData(false, "en-US")]
    public void PlaceholderTracksInputAndFocusThroughClearAndReopen(bool dark, string culture)
    {
        WithPalette(dark, culture, 720, control =>
        {
            var input = Assert.IsType<TextBox>(control.FindName("Searchbox"));
            var placeholder = Assert.IsType<TextBlock>(control.FindName("SearchPlaceholder"));
            Assert.Equal(string.Empty, input.Text);
            Assert.Equal(Visibility.Visible, placeholder.Visibility);
            Assert.Equal(SearchPaletteText.Placeholder, System.Windows.Automation.AutomationProperties.GetName(input));

            SetInputFocusState(input, true);
            Assert.Equal(string.Empty, control.Model.SearchText);
            Assert.Equal(Visibility.Collapsed, placeholder.Visibility);

            input.SetCurrentValue(TextBox.TextProperty, "搜索");
            Complete(control.Model.PendingSearch);
            Assert.Equal("搜索", control.Model.SearchText);
            Assert.Equal(Visibility.Collapsed, placeholder.Visibility);
            SetInputFocusState(input, false);
            Assert.Equal(Visibility.Collapsed, placeholder.Visibility);

            control.Model.SearchText = string.Empty;
            Complete(control.Model.PendingSearch);
            Assert.Equal(string.Empty, input.Text);
            Assert.Equal(Visibility.Visible, placeholder.Visibility);

            SetInputFocusState(input, true);
            input.SetCurrentValue(TextBox.TextProperty, "再次搜索");
            Button clear = Assert.Single(Descendants(control).OfType<Button>().Where(button =>
                System.Windows.Automation.AutomationProperties.GetName(button) == SearchPaletteText.Clear));
            clear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Complete(control.Model.PendingSearch);
            Assert.Equal(string.Empty, input.Text);
            Assert.Equal(string.Empty, control.Model.SearchText);
            Assert.Equal(Visibility.Collapsed, placeholder.Visibility);

            input.SetCurrentValue(TextBox.TextProperty, "旧查询");
            control.Close();
            control.Open(null);
            Complete(control.Model.PendingSearch);
            Assert.Equal(string.Empty, input.Text);
            Assert.Equal(Visibility.Collapsed, placeholder.Visibility);
            SetInputFocusState(input, false);
            Assert.Equal(Visibility.Visible, placeholder.Visibility);
        });
    }

    [Theory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Escape)]
    public void ImePreeditHidesPlaceholderWithoutSubmittingOrClosing(Key key)
    {
        WithPalette(false, "zh-CN", 720, control =>
        {
            var input = Assert.IsType<TextBox>(control.FindName("Searchbox"));
            var placeholder = Assert.IsType<TextBlock>(control.FindName("SearchPlaceholder"));
            int closed = 0;
            control.Closed += (_, _) => closed++;
            SearchPaletteEntry? originalSelection = control.Model.Selected;

            SetInputFocusState(input, true);
            InvokePrivate(control, "CompositionStarted", input, null);
            Assert.Equal(string.Empty, input.Text);
            Assert.Equal(Visibility.Collapsed, placeholder.Visibility);

            SetUncommittedInput(control, input, "搜索");
            Assert.Equal(string.Empty, control.Model.SearchText);
            Assert.Equal(Visibility.Collapsed, placeholder.Visibility);
            var keyEvent = new KeyEventArgs(Keyboard.PrimaryDevice, new IsolatedInputSource(), 0, key);
            InvokePrivate(control, "Palette_PreviewKeyDown", input, keyEvent);
            Assert.False(keyEvent.Handled);
            Assert.True(control.Model.IsOpen);
            Assert.Same(originalSelection, control.Model.Selected);
            Assert.False(control.SubmitSelection());
            Assert.Equal(0, closed);

            // Even with delayed source updates, actual nonempty input must hide the hint on blur.
            SetInputFocusState(input, false);
            Assert.Equal("搜索", input.Text);
            Assert.Equal(string.Empty, control.Model.SearchText);
            Assert.Equal(Visibility.Collapsed, placeholder.Visibility);
            input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            InvokePrivate(control, "CompositionCompleted", input, null);
            Complete(control.Model.PendingSearch);
            Assert.Equal("搜索", control.Model.SearchText);
            Assert.Equal(Visibility.Collapsed, placeholder.Visibility);
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
    [InlineData(false)]
    [InlineData(true)]
    public void ChangedDocumentContextRejectsOldRoutedResultsBeforeAndAfterClose(bool changeOnClose)
    {
        WpfTestHost.Invoke(() =>
        {
            bool sameDocument = true;
            int calls = 0;
            var original = new TextBox { DataContext = new object() };
            var command = new RoutedCommand();
            original.CommandBindings.Add(new(command, (_, _) => calls++, (_, e) => { e.CanExecute = true; e.Handled = true; }));
            var control = new SearchControl((_, _, _) => Task.FromResult(Response(Hit("Save", command))));
            if (changeOnClose) control.Closed += (_, _) => sameDocument = false;
            control.Open(original, isCommandContextCurrent: () => sameDocument);
            Complete(control.Model.PendingSearch);
            if (!changeOnClose) sameDocument = false;
            Assert.False(control.SubmitSelection());
            Assert.Equal(0, calls);
            control.Close();
        });
    }

    [Fact]
    public void NullTargetCloseDocumentUsesBusinessOwnerForAvailabilityAndExecution()
    {
        WithSearchCommandOwner(owner =>
        {
            int calls = 0;
            var events = new List<string>();
            owner.CommandBindings.Add(new(MenuClose.CloseDocumentCommand,
                (_, e) => { calls++; events.Add("execute"); e.Handled = true; },
                (_, e) => { e.CanExecute = true; e.Handled = true; }));
            var control = new SearchControl((_, _, _) => Task.FromResult(Response(Hit("Close document", MenuClose.CloseDocumentCommand))),
                _ => events.Add("recent"));
            control.Closed += (_, _) => events.Add("close");
            control.Open(null, owner, () => true);
            Complete(control.Model.PendingSearch);
            Assert.True(Assert.Single(control.Model.Results).IsAvailable);
            Assert.True(control.SubmitSelection());
            Assert.Equal(1, calls);
            Assert.Equal(new[] { "close", "execute", "recent" }, events);
            Assert.False(control.SubmitSelection());
        });
    }

    [Fact]
    public void NullTargetDoesNotFallbackToBusinessOwnerForOtherRoutedCommands()
    {
        WithSearchCommandOwner(owner =>
        {
            int calls = 0;
            var command = new RoutedCommand();
            owner.CommandBindings.Add(new(command, (_, e) => { calls++; e.Handled = true; },
                (_, e) => { e.CanExecute = true; e.Handled = true; }));
            Assert.True(command.CanExecute(null, owner));
            var control = new SearchControl((_, _, _) => Task.FromResult(Response(Hit("Save", command))));
            control.Open(null, owner, () => true);
            Complete(control.Model.PendingSearch);
            Assert.False(Assert.Single(control.Model.Results).IsAvailable);
            Assert.False(control.SubmitSelection());
            Assert.Equal(0, calls);
            control.Close();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullTargetCloseDocumentRejectsChangedContextBeforeAndAfterClose(bool changeOnClose)
    {
        WithSearchCommandOwner(owner =>
        {
            bool sameDocument = true;
            int calls = 0;
            owner.CommandBindings.Add(new(MenuClose.CloseDocumentCommand, (_, e) => { calls++; e.Handled = true; },
                (_, e) => { e.CanExecute = true; e.Handled = true; }));
            var control = new SearchControl((_, _, _) => Task.FromResult(Response(Hit("Close document", MenuClose.CloseDocumentCommand))));
            if (changeOnClose) control.Closed += (_, _) => sameDocument = false;
            control.Open(null, owner, () => sameDocument);
            Complete(control.Model.PendingSearch);
            Assert.True(Assert.Single(control.Model.Results).IsAvailable);
            if (!changeOnClose) sameDocument = false;
            Assert.False(control.SubmitSelection());
            Assert.Equal(0, calls);
            Assert.Equal(!changeOnClose, control.Model.IsOpen);
            control.Close();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullTargetCloseDocumentRejectsClosedBusinessOwnerBeforeAndAfterClose(bool closeOwnerOnClose)
    {
        WithSearchCommandOwner(owner =>
        {
            int calls = 0;
            owner.CommandBindings.Add(new(MenuClose.CloseDocumentCommand, (_, e) => { calls++; e.Handled = true; },
                (_, e) => { e.CanExecute = true; e.Handled = true; }));
            var control = new SearchControl((_, _, _) => Task.FromResult(Response(Hit("Close document", MenuClose.CloseDocumentCommand))));
            if (closeOwnerOnClose) control.Closed += (_, _) => owner.Close();
            control.Open(null, owner, () => true);
            Complete(control.Model.PendingSearch);
            Assert.True(Assert.Single(control.Model.Results).IsAvailable);
            if (!closeOwnerOnClose) owner.Close();
            Assert.False(control.SubmitSelection());
            Assert.Equal(0, calls);
            control.Close();
        });
    }

    [Fact]
    public void ChangedDocumentContextDoesNotDisableUnrelatedApplicationCommands()
    {
        WpfTestHost.Invoke(() =>
        {
            int calls = 0;
            var control = new SearchControl((_, _, _) => Task.FromResult(Response(Hit("Options", new RelayCommand(_ => calls++)))));
            control.Open(null, isCommandContextCurrent: () => false);
            Complete(control.Model.PendingSearch);
            Assert.True(control.SubmitSelection());
            Assert.Equal(1, calls);
        });
    }

    [Fact]
    public void NullTargetKeepsBusinessOwnerInsteadOfTheSearchWindow()
    {
        WpfTestHost.Invoke(() =>
        {
            var owner = new Window { Width = 600, Height = 400, Left = -10000, Top = -10000,
                ShowInTaskbar = false, ShowActivated = false, Opacity = 0, WindowStyle = WindowStyle.None };
            var searchWindow = new Window();
            var control = new SearchControl((_, _, _) => Task.FromResult(Response(Hit("Options"))));
            try
            {
                owner.Show();
                searchWindow.Owner = owner;
                searchWindow.Content = control;
                control.Open(null);
                Complete(control.Model.PendingSearch);
                Assert.Same(owner, typeof(SearchControl).GetField("_targetWindow", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(control));
            }
            finally
            {
                control.Close();
                searchWindow.Content = null;
                searchWindow.Close();
                owner.Close();
            }
        });
    }

    [Fact]
    public void WindowContentDoesNotRepeatTheNativeCaptionOrLimitResultHeight()
    {
        WithPalette(false, "zh-CN", 720, control =>
        {
            Border root = Assert.IsType<Border>(control.FindName("PaletteRoot"));
            Assert.Equal(new CornerRadius(0), root.CornerRadius);
            Assert.Equal(new Thickness(0), root.BorderThickness);
            Assert.DoesNotContain(Descendants(control).OfType<TextBlock>(), text => text.Text == SearchPaletteText.Title);
            Assert.Equal(double.PositiveInfinity, Assert.IsType<ListBox>(control.FindName("ListViewSearch")).MaxHeight);
        });
    }

    [Fact]
    public void HiddenOriginalContentCannotReceiveARoutedResultFromTheIndependentWindow()
    {
        WpfTestHost.Invoke(() =>
        {
            int calls = 0;
            var target = new TextBox { DataContext = new object() };
            var command = new RoutedCommand();
            target.CommandBindings.Add(new(command, (_, _) => calls++, (_, e) => { e.CanExecute = true; e.Handled = true; }));
            var owner = new Window { Content = target, Width = 600, Height = 400, Left = -10000, Top = -10000,
                ShowInTaskbar = false, ShowActivated = false, Opacity = 0, WindowStyle = WindowStyle.None };
            var control = new SearchControl((_, _, _) => Task.FromResult(Response(Hit("Save", command))));
            try
            {
                owner.Show();
                owner.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.True(target.IsLoaded && target.IsVisible);
                control.Open(target, owner);
                Complete(control.Model.PendingSearch);
                target.Visibility = Visibility.Collapsed;
                Assert.True(target.IsLoaded);
                Assert.False(control.SubmitSelection());
                Assert.Equal(0, calls);
            }
            finally
            {
                control.Close();
                owner.Close();
            }
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

    private static void SetInputFocusState(TextBox input, bool focused)
    {
        // Exercise the real XAML dependency-property binding without activating a window
        // or changing the user's keyboard focus. This is not a native IME end-to-end test.
        var key = Assert.IsType<DependencyPropertyKey>(typeof(UIElement)
            .GetField("IsKeyboardFocusWithinPropertyKey", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null));
        input.SetValue(key, focused);
        input.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static void SetUncommittedInput(SearchControl control, TextBox input, string text)
    {
        // IME pre-edit may be visible before WPF commits Text back to the search model.
        input.SetBinding(TextBox.TextProperty, new Binding(nameof(SearchPaletteViewModel.SearchText))
        {
            Source = control.Model,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.Explicit
        });
        input.SetCurrentValue(TextBox.TextProperty, text);
        input.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private sealed class IsolatedInputSource : PresentationSource
    {
        public override Visual RootVisual { get; set; } = new DrawingVisual();
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
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

    private static void WithSearchCommandOwner(Action<Window> action)
    {
        WpfTestHost.Invoke(() =>
        {
            var owner = new Window { DataContext = new object(), Width = 600, Height = 400, Left = -10000, Top = -10000,
                ShowInTaskbar = false, ShowActivated = false, Opacity = 0, WindowStyle = WindowStyle.None };
            try
            {
                owner.Show();
                owner.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.True(owner.IsLoaded && owner.IsVisible);
                action(owner);
            }
            finally
            {
                if (owner.IsVisible) owner.Close();
            }
        });
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
