using System.Collections;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Screen = System.Windows.Forms.Screen;

namespace ColorVision.UI.Tests;

/// <summary>Uses isolated windows and the current display configuration; never changes DPI or persistent configuration.</summary>
public sealed class WindowConfigDpiLifecycleTests
{
    [Fact]
    public void ReadingScreenGeometryWithoutAHandleDoesNotCreateOne()
    {
        WithWindow(window =>
        {
            Assert.Equal(IntPtr.Zero, new WindowInteropHelper(window).Handle);
            AssertScreenGeometry(window, Matrix.Identity);
            Assert.Equal(IntPtr.Zero, new WindowInteropHelper(window).Handle);
        });
    }

    [Fact]
    public void SourceInitializedAndEnsureHandleUseTheSameScreenDipsAsTheShownWindow()
    {
        WithWindow(window =>
        {
            ScreenSnapshot[]? initializedScreens = null;
            ScreenSnapshot? initializedPrimary = null;
            Matrix initializedTransform = Matrix.Identity;
            window.SourceInitialized += (_, _) =>
            {
                // This is the lifecycle used by startup HWND precreation: the root visual is not attached yet.
                Assert.Null(PresentationSource.FromVisual(window));
                var source = Assert.IsType<HwndSource>(HwndSource.FromHwnd(new WindowInteropHelper(window).Handle));
                Assert.NotNull(source.CompositionTarget);
                initializedTransform = source.CompositionTarget.TransformFromDevice;
                AssertScreenGeometry(window, initializedTransform);
                initializedScreens = ReadScreens(window);
                initializedPrimary = ReadPrimary(window);
            };

            IntPtr handle = new WindowInteropHelper(window).EnsureHandle();
            Assert.NotEqual(IntPtr.Zero, handle);
            Assert.NotNull(initializedScreens);
            Assert.NotNull(initializedPrimary);
            Assert.Null(PresentationSource.FromVisual(window));
            Assert.Equal(initializedScreens, ReadScreens(window));
            Assert.Equal(initializedPrimary, ReadPrimary(window));

            window.Show();
            DrainLayout();
            Assert.True(window.IsLoaded);
            var shownSource = Assert.IsType<HwndSource>(PresentationSource.FromVisual(window));
            Assert.NotNull(shownSource.CompositionTarget);
            Assert.Equal(initializedTransform, shownSource.CompositionTarget.TransformFromDevice);
            AssertScreenGeometry(window, shownSource.CompositionTarget.TransformFromDevice);
            Assert.Equal(initializedScreens, ReadScreens(window));
            Assert.Equal(initializedPrimary, ReadPrimary(window));
        });
    }

    [Theory]
    [InlineData(WindowState.Normal, true, true)]
    [InlineData(WindowState.Maximized, true, true)]
    [InlineData(WindowState.Normal, false, true)]
    [InlineData(WindowState.Maximized, false, true)]
    [InlineData(WindowState.Normal, true, false)]
    [InlineData(WindowState.Maximized, true, false)]
    [InlineData(WindowState.Normal, false, false)]
    [InlineData(WindowState.Maximized, false, false)]
    public void RestoredNormalBoundsStayInsideTheSameDipWorkAreaBeforeAndAfterShow(WindowState state, bool hasSavedBounds, bool precreateHandle)
    {
        WithWindow(window =>
        {
            var config = new TestWindowConfig
            {
                WindowStates = (int)state,
                ScreenDeviceName = Screen.PrimaryScreen!.DeviceName,
                Left = SystemParameters.WorkArea.Left + 64,
                Top = SystemParameters.WorkArea.Top + 64,
                Width = hasSavedBounds ? 480 : 0,
                Height = hasSavedBounds ? 320 : 0,
            };
            Rect workingArea = Rect.Empty;
            Rect initializedBounds = Rect.Empty;
            int configStateAtSourceInitialized = -1;
            WindowState? restoredStateAtSourceInitialized = null;
            window.SourceInitialized += (_, _) =>
            {
                configStateAtSourceInitialized = config.WindowStates;
                var source = Assert.IsType<HwndSource>(HwndSource.FromHwnd(new WindowInteropHelper(window).Handle));
                workingArea = Transform(Screen.PrimaryScreen!.WorkingArea, source.CompositionTarget.TransformFromDevice);
            };
            config.SetWindow(window);
            window.SourceInitialized += (_, _) =>
            {
                initializedBounds = GetNormalBounds(window);
                restoredStateAtSourceInitialized = window.WindowState;
            };

            if (precreateHandle)
                new WindowInteropHelper(window).EnsureHandle();
            else
                window.Show();
            Assert.True(state == window.WindowState,
                $"Expected {state}; actual {window.WindowState}; config at SourceInitialized entry={(WindowState)configStateAtSourceInitialized}; state immediately after restore={restoredStateAtSourceInitialized}; precreateHandle={precreateHandle}.");
            AssertInsideWorkArea(initializedBounds, workingArea);
            AssertBoundsEqual(initializedBounds, GetNormalBounds(window));

            window.Show();
            DrainLayout();
            Assert.Equal(state, window.WindowState);
            Rect shownBounds = GetNormalBounds(window);
            AssertBoundsEqual(initializedBounds, shownBounds);
            AssertInsideWorkArea(shownBounds, workingArea);
            config.SetConfig(window);
            AssertBoundsEqual(shownBounds, new Rect(config.Left, config.Top, config.Width, config.Height));
            Assert.Equal((int)state, config.WindowStates);
        });
    }

    private static void WithWindow(Action<Window> inspect)
    {
        WpfTestHost.Invoke(() =>
        {
            Window? previousMainWindow = Application.Current.MainWindow;
            var window = new Window
            {
                Title = "WindowConfig DPI lifecycle test",
                Width = 320,
                Height = 240,
                Left = SystemParameters.WorkArea.Left + 64,
                Top = SystemParameters.WorkArea.Top + 64,
                WindowStartupLocation = WindowStartupLocation.Manual,
                ShowActivated = false,
                ShowInTaskbar = false,
                Opacity = 0,
            };
            try
            {
                inspect(window);
            }
            finally
            {
                window.Close();
                Application.Current.MainWindow = previousMainWindow;
            }
        });
    }

    private static void AssertScreenGeometry(Window window, Matrix transform)
    {
        ScreenSnapshot[] expected = Screen.AllScreens.Select(screen => Snapshot(screen, transform)).OrderBy(screen => screen.Name, StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, ReadScreens(window));
        Assert.Equal(Snapshot(Screen.PrimaryScreen!, transform), ReadPrimary(window));
    }

    private static ScreenSnapshot[] ReadScreens(Window window)
    {
        var screens = (IEnumerable)typeof(WindowConfig).GetMethod("GetDipScreens", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [window])!;
        return screens.Cast<object>().Select(Snapshot).OrderBy(screen => screen.Name, StringComparer.Ordinal).ToArray();
    }

    private static ScreenSnapshot ReadPrimary(Window window) => Snapshot(typeof(WindowConfig)
        .GetMethod("GetPrimaryDipScreen", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [window])!);

    private static ScreenSnapshot Snapshot(object screen) => new(
        (string)screen.GetType().GetProperty("Name")!.GetValue(screen)!,
        (Rect)screen.GetType().GetProperty("WorkingArea")!.GetValue(screen)!,
        (Rect)screen.GetType().GetProperty("Bounds")!.GetValue(screen)!);

    private static ScreenSnapshot Snapshot(Screen screen, Matrix transform) => new(
        screen.DeviceName, Transform(screen.WorkingArea, transform), Transform(screen.Bounds, transform));

    private static Rect Transform(System.Drawing.Rectangle rect, Matrix matrix) => new(
        matrix.Transform(new Point(rect.Left, rect.Top)), matrix.Transform(new Point(rect.Right, rect.Bottom)));

    private static Rect GetNormalBounds(Window window) => window.WindowState == WindowState.Normal
        ? new Rect(window.Left, window.Top, window.Width, window.Height)
        : window.RestoreBounds;

    private static void AssertInsideWorkArea(Rect bounds, Rect area)
    {
        Assert.False(bounds.IsEmpty);
        Assert.True(bounds.Width > 0 && bounds.Height > 0);
        Assert.True(bounds.Left >= area.Left + 15 && bounds.Top >= area.Top + 15, $"{bounds} must retain the work-area inset in {area}.");
        Assert.True(bounds.Right <= area.Right - 15 && bounds.Bottom <= area.Bottom - 15, $"{bounds} must fit within {area}.");
    }

    private static void AssertBoundsEqual(Rect expected, Rect actual)
    {
        Assert.InRange(Math.Abs(expected.Left - actual.Left), 0, 1);
        Assert.InRange(Math.Abs(expected.Top - actual.Top), 0, 1);
        Assert.InRange(Math.Abs(expected.Width - actual.Width), 0, 1);
        Assert.InRange(Math.Abs(expected.Height - actual.Height), 0, 1);
    }

    private static void DrainLayout() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private sealed record ScreenSnapshot(string Name, Rect WorkingArea, Rect Bounds);
    private sealed class TestWindowConfig : WindowConfig { }
}
