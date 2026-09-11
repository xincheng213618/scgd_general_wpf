using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class SingleInstanceStartupWindowTests
{
    [Theory]
    [InlineData("ForceButton")]
    [InlineData("OpenButton")]
    [InlineData("CancelButton")]
    public void FailureWindowShowsReasonAndCompletesTheSelectedAction(string buttonName)
    {
        WpfTestHost.Invoke(() =>
        {
            int attempts = 0;
            var window = new SingleInstanceStartupWindow((_, _) => ++attempts == 1
                ? Task.FromException(new TimeoutException("旧进程 PID 123 在 5 秒后仍未退出"))
                : Task.CompletedTask, "123");
            ShowAndCheck(window, () =>
            {
                Assert.Equal("旧进程未能结束", ((TextBlock)window.FindName("StatusText")).Text);
                TextBlock failure = (TextBlock)window.FindName("FailureText");
                Assert.Equal(Visibility.Visible, failure.Visibility);
                Assert.Contains("PID 123", failure.Text);
                Assert.Contains("重启 Windows", failure.Text);
                Assert.Equal(Visibility.Hidden, ((ProgressBar)window.FindName("ClosingProgress")).Visibility);
                Button button = (Button)window.FindName(buttonName);
                Assert.True(button.IsEnabled);
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            });
            Assert.Equal(buttonName switch
            {
                "ForceButton" => SingleInstanceStartupResult.ClosedEarlierInstances,
                "OpenButton" => SingleInstanceStartupResult.OpenAdditional,
                _ => SingleInstanceStartupResult.Cancel,
            }, window.Result);
            Assert.Equal(buttonName == "ForceButton" ? 2 : 1, attempts);
            Assert.False(Application.Current.Dispatcher.HasShutdownStarted);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ClosingOrDirectOpeningDuringWaitCancelsBeforeTheWindowCloses(bool directOpen)
    {
        WpfTestHost.Invoke(() =>
        {
            bool cleanupCompleted = false;
            var window = new SingleInstanceStartupWindow(async (_, token) =>
            {
                try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
                finally { cleanupCompleted = true; }
            }, "123");
            bool cleanupAtClose = false;
            window.Closed += (_, _) => cleanupAtClose = cleanupCompleted;
            ShowAndCheck(window, () =>
            {
                Assert.False(((Button)window.FindName("ForceButton")).IsEnabled);
                if (directOpen)
                    ((Button)window.FindName("OpenButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                else
                    window.Close();
            });
            Assert.True(cleanupAtClose);
            Assert.Equal(directOpen ? SingleInstanceStartupResult.OpenAdditional : SingleInstanceStartupResult.Cancel, window.Result);
        });
    }

    private static void ShowAndCheck(SingleInstanceStartupWindow window, Action check)
    {
        Exception? failure = null;
        var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        timeout.Tick += (_, _) => { failure ??= new TimeoutException("Startup window did not close."); window.Close(); };
        window.Loaded += (_, _) => window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            try { check(); }
            catch (Exception exception) { failure = exception; window.Close(); }
        }));
        Window previousMainWindow = Application.Current.MainWindow;
        try
        {
            timeout.Start();
            window.ShowDialog();
            Assert.Null(failure);
        }
        finally
        {
            timeout.Stop();
            window.Close();
            Application.Current.MainWindow = previousMainWindow;
        }
    }
}
