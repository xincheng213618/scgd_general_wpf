using ColorVision.Themes.Controls;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.Themes.Tests;

public partial class ThemeResourceTests
{
    private static readonly string[] MessageBoxButtonNames = ["ButtonOK", "ButtonYes", "ButtonNo", "ButtonCancel"];

    [Theory]
    [InlineData(MessageBoxButton.OK, MessageBoxResult.OK, "ButtonOK", 1)]
    [InlineData(MessageBoxButton.OKCancel, MessageBoxResult.Cancel, "ButtonCancel", 2)]
    [InlineData(MessageBoxButton.YesNo, MessageBoxResult.No, "ButtonNo", 2)]
    [InlineData(MessageBoxButton.YesNoCancel, MessageBoxResult.Cancel, "ButtonCancel", 3)]
    public void MessageBoxDefaultChoiceDoesNotConfirmOnClose(MessageBoxButton buttons, MessageBoxResult dismissal, string defaultName, int count) => Run((app, manager) =>
    {
        manager.ApplyTheme(app, Theme.Light);
        var window = new MessageBoxWindow("确认操作？", "ColorVision", buttons, MessageBoxImage.Question, dismissal);
        Assert.Equal(MessageBoxResult.None, window.MessageBoxResult);
        Assert.True(((Button)window.FindName(defaultName)).IsDefault);
        Assert.Equal(count, MessageBoxButtonNames
            .Count(name => ((Button)window.FindName(name)).Visibility == Visibility.Visible));
        window.Close();
        Assert.Equal(dismissal, window.MessageBoxResult);

        var affirmative = new MessageBoxWindow("确认操作？", "ColorVision", buttons, MessageBoxImage.None,
            buttons is MessageBoxButton.OK or MessageBoxButton.OKCancel ? MessageBoxResult.OK : MessageBoxResult.Yes);
        affirmative.Close();
        Assert.Equal(dismissal, affirmative.MessageBoxResult);
    });

    [Theory]
    [InlineData("ButtonOK", MessageBoxButton.OKCancel, MessageBoxResult.OK)]
    [InlineData("ButtonCancel", MessageBoxButton.OKCancel, MessageBoxResult.Cancel)]
    [InlineData("ButtonYes", MessageBoxButton.YesNoCancel, MessageBoxResult.Yes)]
    [InlineData("ButtonNo", MessageBoxButton.YesNoCancel, MessageBoxResult.No)]
    public void MessageBoxReportsClickedResultBeforeClosed(string buttonName, MessageBoxButton buttons, MessageBoxResult expected) => Run((app, manager) =>
    {
        manager.ApplyTheme(app, Theme.Light);
        var window = new MessageBoxWindow("内容", "ColorVision", buttons);
        MessageBoxResult observed = MessageBoxResult.None;
        window.Closed += (_, _) => observed = window.MessageBoxResult;
        ((Button)window.FindName(buttonName)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(expected, observed);
        Assert.Equal(expected, window.MessageBoxResult);
    });

    [Fact]
    public void MessageBoxCancelledCloseDoesNotLeaveAnAffirmativeResult() => Run((app, manager) =>
    {
        manager.ApplyTheme(app, Theme.Light);
        var window = new MessageBoxWindow("内容", "ColorVision", MessageBoxButton.OKCancel);
        bool cancel = true;
        window.Closing += (_, e) => e.Cancel = cancel;
        ((Button)window.FindName("ButtonOK")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(MessageBoxResult.None, window.MessageBoxResult);
        cancel = false;
        window.Close();
        Assert.Equal(MessageBoxResult.Cancel, window.MessageBoxResult);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MessageBoxLongTextIsScrollableAndActionsStayVisible(bool dark) => Run((app, manager) =>
    {
        manager.ApplyTheme(app, dark ? Theme.Dark : Theme.Light);
        var window = new MessageBoxWindow(string.Join("\n", Enumerable.Repeat(new string('长', 200), 60)), "ColorVision",
            MessageBoxButton.YesNoCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel)
        {
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000, ShowActivated = false
        };
        try
        {
            window.Show();
            window.UpdateLayout();
            var input = (TextBox)window.FindName("messageBoxText");
            var scroll = (ScrollViewer)input.Template.FindName("PART_ContentHost", input);
            Assert.True(scroll.ScrollableHeight > 0);
            Assert.True(window.ActualWidth <= window.MaxWidth);
            var button = (Button)window.FindName("ButtonCancel");
            var root = (FrameworkElement)window.Content;
            var bounds = button.TransformToAncestor(root).TransformBounds(new Rect(button.RenderSize));
            Assert.True(bounds.Bottom <= root.ActualHeight && bounds.Right <= root.ActualWidth);
            Assert.Equal(Colors.Transparent, ((SolidColorBrush)input.Background).Color);
            Assert.Equal(dark ? Colors.White : Colors.Black, ((SolidColorBrush)input.Foreground).Color);
            var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), Environment.TickCount, Key.Escape)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            window.RaiseEvent(args);
            Assert.True(args.Handled);
            Assert.Equal(MessageBoxResult.Cancel, window.MessageBoxResult);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void MessageBoxEnterInvokesTheRequestedDefaultAction() => Run((app, manager) =>
    {
        manager.ApplyTheme(app, Theme.Light);
        Assert.Equal(MessageBoxResult.No, ObserveMessageBox(window =>
        {
            Assert.Same(window.FindName("ButtonNo"), FocusManager.GetFocusedElement(window));
            InputManager.Current.ProcessInput(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), Environment.TickCount, Key.Enter)
                { RoutedEvent = Keyboard.KeyDownEvent });
            Assert.Equal(MessageBoxResult.No, window.MessageBoxResult);
        }, () => MessageBox1.Show("保存更改？", "ColorVision", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.No)));
    });

    [Fact]
    public void MessageBoxShortTextUsesItsActualTextMetricsAndTracksTheme() => Run((app, manager) =>
    {
        manager.ApplyTheme(app, Theme.Light);
        var window = new MessageBoxWindow("修改配置后，重新连接设备即可生效。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information)
        {
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000, ShowActivated = false
        };
        try
        {
            window.Show();
            window.UpdateLayout();
            var input = (TextBox)window.FindName("messageBoxText");
            Assert.Equal(1, input.LineCount);
            Assert.Equal(Visibility.Collapsed, ((ScrollViewer)input.Template.FindName("PART_ContentHost", input)).ComputedVerticalScrollBarVisibility);
            manager.ApplyThemeChanged(app, Theme.Dark);
            Assert.Equal(Colors.White, ((SolidColorBrush)input.Foreground).Color);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void MessageBoxOptionsAndOwnerUseTheThemedDialog() => Run((app, manager) =>
    {
        manager.ApplyTheme(app, Theme.Light);
        var owner = new Window { Width = 300, Height = 200, Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false };
        try
        {
            owner.Show();
            Assert.Equal(MessageBoxResult.Cancel, ObserveMessageBox(window =>
            {
                Assert.Same(owner, window.Owner);
                Assert.False(window.Topmost);
                Assert.Equal(WindowStartupLocation.CenterOwner, window.WindowStartupLocation);
                Assert.Equal(FlowDirection.RightToLeft, window.FlowDirection);
                Assert.Equal(TextAlignment.Right, ((TextBox)window.FindName("messageBoxText")).TextAlignment);
                Assert.True(((Button)window.FindName("ButtonCancel")).IsDefault);
            }, () => MessageBox1.Show(owner, "内容", "ColorVision", MessageBoxButton.OKCancel, MessageBoxImage.None,
                MessageBoxResult.Cancel, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign)));

            app.MainWindow = owner;
            Assert.Equal(MessageBoxResult.OK, ObserveMessageBox(window => Assert.Same(owner, window.Owner),
                () => MessageBox1.Show("内容")));
        }
        finally { owner.Close(); }
    });

    [Fact]
    public void MessageBoxShowAgainReturnsTheCheckboxAndSkipsSuppressedMessages() => Run((app, manager) =>
    {
        manager.ApplyTheme(app, Theme.Light);
        Assert.True(MessageBox1.ShowAgain("内容", "ColorVision", true));
        Assert.True(ObserveMessageBox(window =>
        {
            var check = (CheckBox)window.FindName("show_again");
            Assert.Equal(Visibility.Visible, check.Visibility);
            check.IsChecked = true;
            Assert.True(window.DontShowAgain);
        }, () => MessageBox1.ShowAgain("内容", "ColorVision", false)));
    });

    [Fact]
    public async Task MessageBoxWorkerCallUsesTheOwnerDispatcher()
    {
        Window? owner = null;
        Run((app, manager) =>
        {
            manager.ApplyTheme(app, Theme.Light);
            owner = new Window { Width = 300, Height = 200, Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false };
            owner.Show();
        });
        Exception? failure = null;
        // Queue inspection from the owner's dispatcher after the worker's Show call reaches its nested modal loop.
        var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle, Dispatcher.Value) { Interval = TimeSpan.FromMilliseconds(20) };
        timer.Tick += (_, _) =>
        {
            var dialog = Application.Current.Windows.OfType<MessageBoxWindow>().FirstOrDefault();
            if (dialog == null) return;
            timer.Stop();
            try { Assert.Same(owner, dialog.Owner); Assert.True(dialog.Dispatcher.CheckAccess()); }
            catch (Exception ex) { failure = ex; }
            finally { dialog.Close(); }
        };
        Dispatcher.Value.Invoke(timer.Start);
        try
        {
            var result = await Task.Run(() => MessageBox1.Show(owner, "后台线程提示", "ColorVision", MessageBoxButton.OKCancel)).WaitAsync(TimeSpan.FromSeconds(10));
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
            Assert.Equal(MessageBoxResult.Cancel, result);
        }
        finally { Dispatcher.Value.Invoke(() => { timer.Stop(); owner?.Close(); }); }
    }

    private static T ObserveMessageBox<T>(Action<MessageBoxWindow> inspect, Func<T> show)
    {
        Exception? failure = null;
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            var window = Application.Current.Windows.OfType<MessageBoxWindow>().Single();
            try { inspect(window); }
            catch (Exception ex) { failure = ex; }
            finally { window.Close(); }
        });
        T result = show();
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        return result;
    }
}
