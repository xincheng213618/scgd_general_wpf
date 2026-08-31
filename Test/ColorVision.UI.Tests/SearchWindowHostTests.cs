using ColorVision.Common.MVVM;
using ColorVision.UI.Serach;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class SearchWindowHostTests
{
    [Fact]
    public void SearchUsesANativeOwnedResizableWindowWithoutStartingASearchSession()
    {
        WithSearchWindows((owner, search, control) =>
        {
            nint ownerHandle = new WindowInteropHelper(owner).Handle;
            nint searchHandle = new WindowInteropHelper(search).Handle;
            Assert.NotEqual(nint.Zero, searchHandle);
            Assert.NotEqual(ownerHandle, searchHandle);
            Assert.Equal(ownerHandle, GetWindow(searchHandle, 4)); // GW_OWNER.
            Assert.False(IsChild(ownerHandle, searchHandle));
            long style = GetWindowLongPtr(searchHandle, -16).ToInt64();
            Assert.Equal(0, style & 0x40000000); // Not WS_CHILD.
            Assert.NotEqual(0, style & 0x40000); // WS_THICKFRAME: native resizing.
            long extendedStyle = GetWindowLongPtr(searchHandle, -20).ToInt64();
            Assert.Equal(0, extendedStyle & 0x40000); // Not WS_EX_APPWINDOW.
            Assert.False(search.ShowInTaskbar);
            Assert.Equal(ResizeMode.CanResize, search.ResizeMode);
            Assert.Equal(WindowStyle.SingleBorderWindow, search.WindowStyle);
            Assert.Same(owner, search.Owner);
            Assert.True(owner.IsEnabled); // Show(), not a modal dialog.
            Assert.True(search.IsVisible);
            Assert.False(owner.IsActive);
            Assert.False(search.IsActive);
            Assert.False(control.Model.IsOpen);
            Assert.Empty(control.Model.Results);
        });
    }

    [Fact]
    public void MovingAndResizingTheOwnerDoesNotDismissTheIndependentSearchWindow()
    {
        WithSearchWindows((owner, search, control) =>
        {
            owner.Left -= 40;
            owner.Top -= 30;
            owner.Width += 40;
            owner.Height += 30;
            owner.UpdateLayout();
            Assert.True(search.IsVisible);

            search.Width = 800;
            search.Height = 620;
            search.UpdateLayout();
            Assert.Equal(800, search.Width);
            Assert.Equal(620, search.Height);
            Assert.True(search.IsVisible);
            Assert.False(control.Model.IsOpen);
        });
    }

    [Fact]
    public void ClosingSearchLeavesItsOwnerUsableAndAllowsANewSearchWindow()
    {
        WithSearchWindows((owner, search, control) =>
        {
            int closed = 0;
            search.Closed += (_, _) => closed++;
            search.Close();
            Assert.Equal(1, closed);
            Assert.False(search.IsVisible);
            Assert.False(control.Model.IsOpen);
            Assert.True(owner.IsVisible);
            Assert.True(owner.IsEnabled);
            Assert.Empty(owner.OwnedWindows.Cast<Window>());

            var reopened = CreateNonActivatingSearch(owner);
            try
            {
                reopened.Show();
                Assert.True(reopened.IsVisible);
                Assert.Same(owner, reopened.Owner);
                Assert.False(GetSearchControl(reopened).Model.IsOpen);
            }
            finally { reopened.Close(); }
        });
    }

    [Fact]
    public void ClosingTheOwnerAlsoClosesItsSearchWindow()
    {
        WithSearchWindows((owner, search, control) =>
        {
            int closed = 0;
            search.Closed += (_, _) => closed++;
            owner.Close();
            Assert.Equal(1, closed);
            Assert.False(search.IsVisible);
            Assert.False(owner.IsVisible);
            Assert.False(control.Model.IsOpen);
        });
    }

    [Fact]
    public void SubmittingAResultClosesTheRealSearchWindowBeforeExecutingAndRecordingItOnce()
    {
        WpfTestHost.Invoke(() =>
        {
            var events = new List<string>();
            SearchResultItem item = new(new SearchMeta
            {
                GuidId = "safe-action", Header = "Safe action", Command = new RelayCommand(_ => events.Add("execute"))
            }, "isolated-window-test", "");
            var control = new SearchControl((_, _, _) => Task.FromResult(new SearchQueryResult([item], [], false)),
                _ => events.Add("recent"));
            // Exercise Enter's submission method without injecting a keyboard event
            // or allowing the off-screen fixture to take desktop keyboard focus.
            Assert.IsType<TextBox>(control.FindName("Searchbox")).Focusable = false;
            WithSearchWindows((owner, search, hosted) =>
            {
                Assert.Same(control, hosted);
                int contentClosed = 0;
                control.Closed += (_, _) => contentClosed++;
                search.Closed += (_, _) => events.Add("window-closed");
                search.Open((TextBox)owner.Content);
                Complete(control.Model.PendingSearch);
                Assert.True(control.Model.IsOpen);
                Assert.True(search.IsVisible);
                Assert.True(control.SubmitSelection());
                Assert.Equal(new[] { "window-closed", "execute", "recent" }, events);
                Assert.Equal(1, contentClosed);
                Assert.False(search.IsVisible);
                Assert.False(control.Model.IsOpen);
                Assert.True(owner.IsVisible);
                Assert.False(control.SubmitSelection());
                Assert.Equal(3, events.Count);
                Assert.False(owner.IsActive);
                Assert.False(search.IsActive);
            }, control);
        });
    }

    [Fact]
    public void SystemCloseCancelsAnOpenQueryAndRejectsLateResultsWithoutExecuting()
    {
        WpfTestHost.Invoke(() =>
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var response = new TaskCompletionSource<SearchQueryResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            CancellationToken queryToken = default;
            int executions = 0, recents = 0;
            SearchResultItem item = new(new SearchMeta
            {
                GuidId = "late-action", Header = "Late action", Command = new RelayCommand(_ => executions++)
            }, "isolated-window-test", "");
            var control = new SearchControl((_, _, token) =>
            {
                queryToken = token;
                started.TrySetResult();
                return response.Task;
            }, _ => recents++);
            Assert.IsType<TextBox>(control.FindName("Searchbox")).Focusable = false;
            WithSearchWindows((owner, search, hosted) =>
            {
                Assert.Same(control, hosted);
                int windowClosed = 0, contentClosed = 0;
                search.Closed += (_, _) => windowClosed++;
                control.Closed += (_, _) => contentClosed++;
                search.Open((TextBox)owner.Content);
                Complete(started.Task);
                Task pending = control.Model.PendingSearch;
                Assert.True(control.Model.IsOpen);
                Assert.True(control.Model.IsSearching);
                search.Close();
                Assert.True(queryToken.IsCancellationRequested);
                Assert.Equal(1, windowClosed);
                Assert.Equal(1, contentClosed);
                Assert.False(control.Model.IsOpen);
                Assert.False(search.IsVisible);
                response.SetResult(new SearchQueryResult([item], [], false));
                Complete(pending);
                Assert.Empty(control.Model.Results);
                Assert.False(control.SubmitSelection());
                Assert.Equal(0, executions);
                Assert.Equal(0, recents);
                Assert.Equal(1, windowClosed);
                Assert.Equal(1, contentClosed);
                Assert.False(owner.IsActive);
            }, control);
        });
    }

    private static void WithSearchWindows(Action<Window, SearchWindow, SearchControl> test, SearchControl? injectedControl = null)
    {
        WpfTestHost.Invoke(() =>
        {
            // The real search shell is shown without calling Open: its query delegate
            // stays dormant. No production MainWindow, providers, devices, or input injection.
            var owner = new Window
            {
                Content = new TextBox(), Width = 900, Height = 700, Left = -10000, Top = -10000,
                ShowInTaskbar = false, ShowActivated = false, Opacity = 0, WindowStyle = WindowStyle.None
            };
            SearchWindow? search = null;
            bool ownerClosed = false;
            bool searchClosed = false;
            owner.Closed += (_, _) => ownerClosed = true;
            try
            {
                owner.Show();
                search = CreateNonActivatingSearch(owner, injectedControl);
                search.Closed += (_, _) => searchClosed = true;
                SearchControl control = GetSearchControl(search);
                Assert.False(control.Model.IsOpen);
                search.Show();
                search.UpdateLayout();
                Assert.False(control.Model.IsOpen);
                test(owner, search, control);
            }
            finally
            {
                if (search != null && !searchClosed) search.Close();
                if (!ownerClosed) owner.Close();
            }
        });
    }

    private static SearchWindow CreateNonActivatingSearch(Window owner, SearchControl? control = null)
    {
        SearchWindow window = control == null ? new() : new(control);
        window.Owner = owner;
        window.ShowActivated = false;
        window.Opacity = 0;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -10000;
        window.Top = -10000;
        return window;
    }

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

    private static SearchControl GetSearchControl(SearchWindow window)
        => Assert.IsType<SearchControl>(window.FindName("CommandSearchControl"));

    [DllImport("user32.dll")]
    private static extern nint GetWindow(nint window, uint command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsChild(nint parent, nint child);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint window, int index);
}
