using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.EditorTools.FullScreen;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.EditorTools;
using ColorVision.Windowing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;

namespace ColorVision.UI.Tests;

public sealed class WindowFullScreenTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ImageFullScreenFitsViewportThenRestoresZoomToolAndDrawingScale(bool compact, bool shellShortcutsEnabled)
    {
        WpfTestHost.Invoke(() =>
        {
            Application.Current.Resources["TextBox.Small"] = new Style(typeof(TextBox));
            Application.Current.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
            Application.Current.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
            Application.Current.Resources["ToolBarImage"] = new Style(typeof(Image));
            Application.Current.Resources["BaseStyle"] = new Style(typeof(Control));
            Application.Current.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
            Application.Current.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
            using var host = new Host(compact);
            var config = new Config();
            if (shellShortcutsEnabled)
                host.Window.SetWindowFull(config);
            using var view = new ImageView();
            Grid.SetRow(view, 1);
            host.Root.Children.Add(view);
            view.SetImageSource(BitmapSource.Create(256, 256, 96, 96, PixelFormats.Bgra32, null, new byte[256 * 256 * 4], 256 * 4));
            host.Window.UpdateLayout();
            view.Zoombox1.ZoomUniform();
            view.Zoombox1.Zoom(1.5);
            Matrix original = view.Zoombox1.ContentMatrix;
            var tool = new ZoomRatioEditorTool(view.EditorContext.DrawEditorContext);
            double displayedRatio = tool.ZoomRatio;
            tool.PropertyChanged += (_, _) => displayedRatio = tool.ZoomRatio;

            view.ToggleFullScreen();
            AssertFullScreen(host.Window);
            Assert.Equal(Math.Min(view.Zoombox1.ActualWidth, view.Zoombox1.ActualHeight) / 256, displayedRatio);
            RaiseKey(host.Window, Key.Escape);
            Assert.Equal(original, view.Zoombox1.ContentMatrix);
            Assert.Equal(original.M11, displayedRatio);
            Assert.Equal(1 / original.M11, view.EditorContext.DrawEditorContext.DrawCanvas.Scale);

            // Preview and bubble routing must preserve the image shortcut even
            // when the shell registered its own F11 handler before the view loaded.
            for (int i = 0; i < 2; i++)
            {
                RaiseKey(host.Window, Key.F11, view.Zoombox1);
                Assert.False(config.IsFull);
                AssertFullScreen(host.Window);
                Assert.Same(view.ImageContentGrid, host.Window.Content);
                RaiseKey(host.Window, Key.F11, view.Zoombox1);
                Assert.False(config.IsFull);
                Assert.False(WindowFullScreenSession.GetIsActive(host.Window));
                Assert.Same(host.Root, host.Window.Content);
                Assert.Equal(original, view.Zoombox1.ContentMatrix);
            }
        });
    }

    [Theory]
    [InlineData(false, WindowState.Normal, Key.F11)]
    [InlineData(false, WindowState.Maximized, Key.Escape)]
    [InlineData(true, WindowState.Normal, Key.Escape)]
    [InlineData(true, WindowState.Maximized, Key.F11)]
    public void ShellFullScreenCoversMonitorAndRestoresPlacement(bool compact, WindowState initialState, Key exitKey)
    {
        WpfTestHost.Invoke(() =>
        {
            using var host = new Host(compact);
            host.Window.WindowState = initialState;
            Rect original = host.Window.RestoreBounds;
            WindowChrome? chrome = WindowChrome.GetWindowChrome(host.Window);
            var config = new Config();
            host.Window.SetWindowFull(config);
            host.Window.SetWindowFull(config); // Repeated registration must not toggle twice.

            for (int i = 0; i < 2; i++)
            {
                RaiseKey(host.Window, Key.F11);
                Assert.True(config.IsFull);
                AssertFullScreen(host.Window);
                Assert.Same(host.Root, host.Window.Content);
                config.IsFull = true; // Duplicate notifications must not replace the restore snapshot.
                RaiseKey(host.Window, exitKey);
                Assert.False(config.IsFull);
                Assert.False(WindowFullScreenSession.GetIsActive(host.Window));
                Assert.Same(chrome, WindowChrome.GetWindowChrome(host.Window));
                Assert.Equal(initialState, host.Window.WindowState);
                Assert.Equal(original, host.Window.RestoreBounds);
                Assert.Equal(ResizeMode.CanResize, host.Window.ResizeMode);
                Assert.Equal(WindowStyle.SingleBorderWindow, host.Window.WindowStyle);
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImageToolbarAndEscapeRestoreParentOrderAndClientHitTesting(bool contentControl)
    {
        WpfTestHost.Invoke(() =>
        {
            using var host = new Host(compact: true);
            var image = new Grid { Background = Brushes.DarkGreen };
            var button = new Button { Content = "Full screen", Width = 110, Height = 28, VerticalAlignment = VerticalAlignment.Top };
            image.Children.Add(button);
            var container = new Grid();
            var before = new Border();
            var after = new Border();
            ContentControl? content = contentControl ? new ContentControl { Content = image } : null;
            container.Children.Add(before);
            container.Children.Add(content ?? (UIElement)image);
            container.Children.Add(after);
            Grid.SetRow(container, 1);
            host.Root.Children.Add(container);
            var mode = new ImageFullScreenMode(image);
            button.Click += (_, _) => mode.ToggleFullScreen();
            host.Window.UpdateLayout();
            WindowChrome? chrome = WindowChrome.GetWindowChrome(host.Window);

            foreach (bool escape in new[] { false, true })
            {
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.True(mode.IsMax);
                AssertFullScreen(host.Window);
                host.Window.UpdateLayout();
                Assert.Same(image, host.Window.Content);
                Assert.Same(Brushes.Beige, host.Window.Background);
                Point point = button.PointToScreen(new Point(button.ActualWidth / 2, button.ActualHeight / 2));
                long coordinates = ((long)(ushort)(short)point.Y << 16) | (ushort)(short)point.X;
                Assert.Equal(new IntPtr(1), SendMessage(new WindowInteropHelper(host.Window).Handle, 0x84, IntPtr.Zero, new IntPtr(coordinates)));
                if (escape) RaiseKey(host.Window, Key.Escape);
                else button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.False(mode.IsMax);
                Assert.Same(host.Root, host.Window.Content);
                Assert.Same(before, container.Children[0]);
                Assert.Same(content ?? (UIElement)image, container.Children[1]);
                Assert.Same(after, container.Children[2]);
                if (content != null) Assert.Same(image, content.Content);
                Assert.Same(chrome, WindowChrome.GetWindowChrome(host.Window));
            }
        });
    }

    [Fact]
    public void NestedImageExitKeepsShellFullScreenUntilSecondKey()
    {
        WpfTestHost.Invoke(() =>
        {
            using var host = new Host(compact: true);
            var config = new Config();
            host.Window.SetWindowFull(config);
            var image = new Grid();
            host.Root.Children.Add(image);
            var mode = new ImageFullScreenMode(image);
            WindowChrome? chrome = WindowChrome.GetWindowChrome(host.Window);
            RaiseKey(host.Window, Key.F11);
            mode.ToggleFullScreen();
            AssertFullScreen(host.Window);
            RaiseKey(host.Window, Key.F11);
            Assert.False(mode.IsMax);
            Assert.True(config.IsFull);
            Assert.Same(host.Root, host.Window.Content);
            AssertFullScreen(host.Window);
            RaiseKey(host.Window, Key.F11);
            Assert.False(config.IsFull);
            Assert.False(WindowFullScreenSession.GetIsActive(host.Window));
            Assert.Same(chrome, WindowChrome.GetWindowChrome(host.Window));
        });
    }

    [Fact]
    public void FullScreenDoesNotOverwriteStartupPlacementAndCloseUnsubscribesConfig()
    {
        WpfTestHost.Invoke(() =>
        {
            using var host = new Host(compact: false);
            var placement = new PlacementConfig();
            placement.SetWindow(host.Window);
            placement.SetConfig(host.Window);
            Rect original = new(placement.Left, placement.Top, placement.Width, placement.Height);
            var config = new Config();
            host.Window.SetWindowFull(config);
            RaiseKey(host.Window, Key.F11);
            placement.SetConfig(host.Window);
            Assert.Equal(original, new Rect(placement.Left, placement.Top, placement.Width, placement.Height));
            host.Window.Close();
            config.IsFull = false; // A closed window must no longer be touched.
            Assert.Equal(original, new Rect(placement.Left, placement.Top, placement.Width, placement.Height));
        });
    }

    private static void RaiseKey(Window window, Key key, UIElement? source = null)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, key)
        { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        (source ?? window).RaiseEvent(args);
        if (!args.Handled)
        {
            args.RoutedEvent = Keyboard.KeyDownEvent;
            (source ?? window).RaiseEvent(args);
        }
        Assert.True(args.Handled);
    }

    private static void AssertFullScreen(Window window)
    {
        Assert.True(WindowFullScreenSession.GetIsActive(window));
        Assert.Equal(WindowStyle.None, window.WindowStyle);
        Assert.Equal(WindowState.Normal, window.WindowState);
        Assert.Equal(ResizeMode.NoResize, window.ResizeMode);
        Assert.Null(WindowChrome.GetWindowChrome(window));
        Assert.False(window.Topmost);
        IntPtr handle = new WindowInteropHelper(window).Handle;
        var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        Assert.True(GetMonitorInfo(MonitorFromWindow(handle, 2), ref monitor));
        Assert.True(GetWindowRect(handle, out NativeRect actual));
        Assert.Equal(monitor.Monitor, actual);
    }

    private sealed class Config : ViewModelBase, IFullScreenState
    {
        private bool full;
        public bool IsFull { get => full; set { full = value; OnPropertyChanged(); } }
    }
    private sealed class PlacementConfig : ColorVision.UI.WindowConfig { }

    private sealed class Host : IDisposable
    {
        public Grid Root { get; } = new();
        public Window Window { get; }
        private readonly CompactTitleBarChrome? controller;
        public Host(bool compact)
        {
            Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Root.RowDefinitions.Add(new RowDefinition());
            var title = new Grid { Height = 36 };
            var caption = new Border();
            title.Children.Add(caption);
            Root.Children.Add(title);
            Window = new Window
            {
                Content = Root, Width = 900, Height = 600, Left = 120, Top = 140,
                Background = Brushes.Beige, ShowActivated = false, ShowInTaskbar = false
            };
            Window.Show();
            Window.UpdateLayout();
            if (compact && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            {
                controller = new CompactTitleBarChrome(Window, title, caption, Root);
                Assert.True(controller.TryAttach());
                Window.UpdateLayout();
            }
        }
        public void Dispose() { controller?.Dispose(); Window.Close(); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
}
