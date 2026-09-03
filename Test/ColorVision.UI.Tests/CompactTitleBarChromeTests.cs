using ColorVision.Windowing;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

/// <summary>Exercises isolated synthetic windows; never constructs MainWindow or loads device/workspace configuration.</summary>
public sealed class CompactTitleBarChromeTests
{
    [Fact]
    public void AttachBeforeHandleCreationDoesNotCreateAHandleOrChangeTheWindow()
    {
        WpfTestHost.Invoke(() =>
        {
            using var host = new ChromeHost();
            using var controller = host.CreateController();

            controller.ApplyTheme(true);
            controller.SetFullScreen(true);
            Assert.False(controller.TryAttach());
            Assert.False(controller.IsAttached);
            Assert.Equal(IntPtr.Zero, new WindowInteropHelper(host.Window).Handle);
            host.AssertOriginalLayout();
            Assert.Null(WindowChrome.GetWindowChrome(host.Window));
        });
    }

    [Fact]
    public void ExistingWindowChromeIsNotReplaced()
    {
        WpfTestHost.Invoke(() =>
        {
            using var host = new ChromeHost();
            var existing = new WindowChrome { CaptionHeight = 25 };
            WindowChrome.SetWindowChrome(host.Window, existing);
            new WindowInteropHelper(host.Window).EnsureHandle();
            using var controller = host.CreateController();

            Assert.False(controller.TryAttach());
            Assert.False(controller.IsAttached);
            Assert.Same(existing, WindowChrome.GetWindowChrome(host.Window));
            host.AssertOriginalLayout();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BorderlessOrLayeredWindowsAreNotConverted(bool allowsTransparency)
    {
        WpfTestHost.Invoke(() =>
        {
            using var host = new ChromeHost();
            host.Window.WindowStyle = WindowStyle.None;
            host.Window.AllowsTransparency = allowsTransparency;
            new WindowInteropHelper(host.Window).EnsureHandle();
            using var controller = host.CreateController();

            Assert.False(controller.TryAttach());
            Assert.False(controller.IsAttached);
            Assert.Null(WindowChrome.GetWindowChrome(host.Window));
            Assert.Equal(WindowStyle.None, host.Window.WindowStyle);
            Assert.Equal(allowsTransparency, host.Window.AllowsTransparency);
            host.AssertOriginalLayout();
        });
    }

    [Fact]
    public void AttachingRetainsStandardWindowCapabilitiesAndReusesChromeAcrossThemeChanges()
    {
        WpfTestHost.Invoke(() =>
        {
            using var host = new ChromeHost();
            new WindowInteropHelper(host.Window).EnsureHandle();
            using var controller = host.CreateController();
            if (!AttachForCurrentSystem(host, controller))
                return;

            var chrome = Assert.IsType<WindowChrome>(WindowChrome.GetWindowChrome(host.Window));
            Assert.True(chrome.UseAeroCaptionButtons);
            Assert.True(chrome.CaptionHeight > 0);
            Assert.True(chrome.GlassFrameThickness.Top > 0);
            Assert.True(chrome.ResizeBorderThickness.Left > 1);
            Assert.True(host.CaptionButtonsPlaceholder.Width > 0);
            Assert.Equal(WindowStyle.SingleBorderWindow, host.Window.WindowStyle);
            Assert.Equal(ResizeMode.CanResize, host.Window.ResizeMode);
            Assert.False(host.Window.AllowsTransparency);
            Assert.Equal(1, host.Window.Opacity);
            IntPtr handle = new WindowInteropHelper(host.Window).Handle;
            long nativeStyle = GetWindowLongPtr(handle, -16).ToInt64();
            const long standardFrameCapabilities = 0x00C00000 | 0x00040000 | 0x00020000 | 0x00010000 | 0x00080000;
            Assert.Equal(standardFrameCapabilities, nativeStyle & standardFrameCapabilities);
            Assert.Equal(0L, GetWindowLongPtr(handle, -20).ToInt64() & 0x00080000); // WS_EX_LAYERED

            foreach (bool isDark in new[] { true, false, true })
            {
                controller.ApplyTheme(isDark);
                Assert.True(controller.TryAttach());
                Assert.Same(chrome, WindowChrome.GetWindowChrome(host.Window));
                Assert.Equal(WindowStyle.SingleBorderWindow, host.Window.WindowStyle);
                Assert.False(host.Window.AllowsTransparency);
            }
        });
    }

    [Fact]
    public void MaximizeRestoreKeepsChromeMetricsStableAndPreservesCaptionBodyAndNativeHitTargets()
    {
        WpfTestHost.Invoke(() =>
        {
            using var host = new ChromeHost();
            host.TitleBar.Children.Remove(host.CaptionButtonsPlaceholder);
            host.ContentRoot.Children.Clear();
            host.TitleBar.Background = Brushes.Beige;
            var topBar = new Grid();
            topBar.ColumnDefinitions.Add(new ColumnDefinition());
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topBar.Children.Add(host.TitleBar);
            Grid.SetColumn(host.CaptionButtonsPlaceholder, 1);
            host.CaptionButtonsPlaceholder.IsHitTestVisible = false;
            topBar.Children.Add(host.CaptionButtonsPlaceholder);
            host.ContentRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            host.ContentRoot.RowDefinitions.Add(new RowDefinition());
            host.ContentRoot.Children.Add(topBar);
            var body = new Border { Background = Brushes.White };
            Grid.SetRow(body, 1);
            host.ContentRoot.Children.Add(body);
            host.Window.Show();
            IntPtr handle = new WindowInteropHelper(host.Window).Handle;
            using var controller = host.CreateController();
            if (!AttachForCurrentSystem(host, controller))
                return;

            void SettleWindow()
            {
                // Keep this synthetic HWND behind user windows; no desktop input or production window is used.
                Assert.True(SetWindowPos(handle, new IntPtr(1), 0, 0, 0, 0, 0x0013)); // HWND_BOTTOM; NOSIZE|NOMOVE|NOACTIVATE
                host.Window.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                host.Window.UpdateLayout();
            }

            SettleWindow();
            WindowChrome chrome = WindowChrome.GetWindowChrome(host.Window);
            double captionHeight = chrome.CaptionHeight;
            Thickness glass = chrome.GlassFrameThickness;
            Thickness resizeBorder = chrome.ResizeBorderThickness;
            void AssertHitTargets()
            {
                Point captionPoint = host.TitleBar.PointToScreen(new Point(host.TitleBar.ActualWidth * .35, host.TitleBar.ActualHeight - 1));
                Point bodyPoint = body.PointToScreen(new Point(body.ActualWidth * .35, 1));
                Assert.Equal(2, NativeHitTest(handle, captionPoint)); // HTCAPTION
                Assert.Equal(1, NativeHitTest(handle, bodyPoint)); // HTCLIENT
                Assert.True(GetWindowRect(handle, out NativeRect windowBounds));
                if (host.Window.WindowState == WindowState.Normal)
                {
                    Assert.Equal(10, NativeHitTest(handle, new Point(windowBounds.Left + 1, bodyPoint.Y))); // HTLEFT
                    Assert.Equal(11, NativeHitTest(handle, new Point(windowBounds.Right - 1, bodyPoint.Y))); // HTRIGHT
                }
                Assert.Equal(0, DwmGetWindowAttribute(handle, 5, out NativeRect captionButtons, Marshal.SizeOf<NativeRect>()));
                Assert.True(captionButtons.Right > captionButtons.Left && captionButtons.Bottom > captionButtons.Top);
                double buttonWidth = (captionButtons.Right - captionButtons.Left) / 3.0;
                double buttonY = windowBounds.Top + (captionButtons.Top + captionButtons.Bottom) / 2.0;
                foreach ((int index, int expected) in new[] { (0, 20), (1, 9), (2, 8) }) // close/maximize/minimize
                {
                    double buttonX = windowBounds.Left + captionButtons.Right - buttonWidth * (index + .5);
                    Assert.Equal(expected, NativeHitTest(handle, new Point(buttonX, buttonY)));
                }
            }

            foreach (WindowState state in new[] { WindowState.Normal, WindowState.Maximized, WindowState.Normal, WindowState.Maximized, WindowState.Normal })
            {
                host.Window.WindowState = state;
                SettleWindow();
                Assert.Same(chrome, WindowChrome.GetWindowChrome(host.Window));
                Assert.Equal(captionHeight, chrome.CaptionHeight);
                Assert.Equal(glass, chrome.GlassFrameThickness);
                Assert.Equal(resizeBorder, chrome.ResizeBorderThickness);
                AssertHitTargets();
            }

            controller.SetFullScreen(true);
            controller.SetFullScreen(false);
            SettleWindow();
            Assert.Same(chrome, WindowChrome.GetWindowChrome(host.Window));
            AssertHitTargets();
        });
    }

    [Fact]
    public void SystemMaximizeRestoreNeverClearsVisibilityAndKeepsNativeMenuCommandsCorrect()
    {
        WpfTestHost.Invoke(() =>
        {
            using var host = new ChromeHost();
            // Match a real auto-height title row; stretching it over the whole Window
            // would intentionally change CaptionHeight and measure unrelated frame refreshes.
            host.TitleBar.VerticalAlignment = VerticalAlignment.Top;
            host.Window.Show();
            using var controller = host.CreateController();
            if (!AttachForCurrentSystem(host, controller))
                return;

            IntPtr handle = new WindowInteropHelper(host.Window).Handle;
            HwndSource source = HwndSource.FromHwnd(handle);
            int hiddenTransitions = 0;
            int frameChanges = 0;
            IntPtr ObserveStyle(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
            {
                if (message == 0x007D && wParam.ToInt64() == -16) // WM_STYLECHANGED / GWL_STYLE
                {
                    NativeStyle style = Marshal.PtrToStructure<NativeStyle>(lParam);
                    if ((style.Old & 0x10000000) != 0 && (style.New & 0x10000000) == 0)
                        hiddenTransitions++;
                }
                if (message == 0x0046 && (Marshal.PtrToStructure<NativeWindowPosition>(lParam).Flags & 0x20) != 0)
                    frameChanges++;
                return IntPtr.Zero;
            }

            source.AddHook(ObserveStyle);
            try
            {
                foreach (bool resumeChrome in new[] { false, true })
                {
                    if (resumeChrome)
                    {
                        controller.SetFullScreen(true);
                        controller.SetFullScreen(false);
                    }
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    hiddenTransitions = 0;
                    for (int iteration = 0; iteration < 3; iteration++)
                    {
                        foreach (bool maximize in new[] { true, false })
                        {
                            frameChanges = 0;
                            SendMessage(handle, 0x0112, new IntPtr(maximize ? 0xF030 : 0xF120), IntPtr.Zero);
                            Assert.True(SetWindowPos(handle, new IntPtr(1), 0, 0, 0, 0, 0x0013));
                            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                            Assert.Equal(maximize ? WindowState.Maximized : WindowState.Normal, host.Window.WindowState);
                            Assert.True(IsWindowVisible(handle));
                            Assert.Equal(0, hiddenTransitions);
                            Assert.Equal(1, frameChanges); // Initial/resumed frame setup must not repeat on ordinary state changes.
                            IntPtr menu = GetSystemMenu(handle, false);
                            uint maximizeState = GetMenuState(menu, 0xF030, 0);
                            uint restoreState = GetMenuState(menu, 0xF120, 0);
                            Assert.NotEqual(uint.MaxValue, maximizeState);
                            Assert.NotEqual(uint.MaxValue, restoreState);
                            Assert.Equal(maximize, (maximizeState & 3) != 0);
                            Assert.Equal(!maximize, (restoreState & 3) != 0);
                        }
                    }
                }
            }
            finally { source.RemoveHook(ObserveStyle); }
        });
    }

    [Fact]
    public void VisibilityGuardPreservesRealWpfAndNativeHideShowAndMinimize()
    {
        WpfTestHost.Invoke(() =>
        {
            using var host = new ChromeHost();
            host.Window.Show();
            using var controller = host.CreateController();
            if (!AttachForCurrentSystem(host, controller))
                return;
            IntPtr handle = new WindowInteropHelper(host.Window).Handle;

            host.Window.Hide();
            Assert.False(IsWindowVisible(handle));
            Assert.Equal(Visibility.Hidden, host.Window.Visibility);
            host.Window.Show();
            Assert.True(IsWindowVisible(handle));
            ShowWindow(handle, 0); // An actual native hide is not WindowChrome's SetWindowLong trick.
            Assert.False(IsWindowVisible(handle));
            ShowWindow(handle, 4); // SW_SHOWNOACTIVATE
            Assert.True(IsWindowVisible(handle));
            SendMessage(handle, 0x0112, new IntPtr(0xF020), IntPtr.Zero);
            Assert.Equal(WindowState.Minimized, host.Window.WindowState);
            SendMessage(handle, 0x0112, new IntPtr(0xF120), IntPtr.Zero);
            Assert.Equal(WindowState.Normal, host.Window.WindowState);
            Assert.True(IsWindowVisible(handle));

            controller.Dispose();
            ShowWindow(handle, 0);
            Assert.False(IsWindowVisible(handle));
            ShowWindow(handle, 4);
            Assert.True(IsWindowVisible(handle));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ApplicationCanIntentionallyHideFromASizeChangedHandler(bool maximize)
    {
        WpfTestHost.Invoke(() =>
        {
            using var host = new ChromeHost();
            host.Window.Show();
            using var controller = host.CreateController();
            if (!AttachForCurrentSystem(host, controller))
                return;
            IntPtr handle = new WindowInteropHelper(host.Window).Handle;
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            bool requestedHide = false;
            void HideOnResize(object sender, SizeChangedEventArgs args)
            {
                if (requestedHide)
                    return;
                requestedHide = true;
                ShowWindow(handle, 0);
            }
            host.Window.SizeChanged += HideOnResize;
            try
            {
                if (maximize)
                    SendMessage(handle, 0x0112, new IntPtr(0xF030), IntPtr.Zero);
                else
                    host.Window.Width += 100;
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.True(requestedHide);
                Assert.False(IsWindowVisible(handle));
            }
            finally { host.Window.SizeChanged -= HideOnResize; }
        });
    }

    [Theory]
    [InlineData(0x0005)] // WM_SIZE
    [InlineData(0x0047)] // WM_WINDOWPOSCHANGED
    public void NativeHideInsideStateMessageRevokesThePendingVisibilityGuard(int targetMessage)
    {
        WpfTestHost.Invoke(() =>
        {
            using var host = new ChromeHost();
            host.Window.Show();
            using var controller = host.CreateController();
            if (!AttachForCurrentSystem(host, controller))
                return;
            IntPtr handle = new WindowInteropHelper(host.Window).Handle;
            HwndSource source = HwndSource.FromHwnd(handle);
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            bool hideRequested = false;
            bool hiddenInsideMessage = false;
            IntPtr HideBeforeChrome(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
            {
                if (!hideRequested && message == targetMessage && (GetWindowLongPtr(handle, -16).ToInt64() & 0x01000000) != 0)
                {
                    hideRequested = true;
                    ShowWindow(handle, 0);
                    hiddenInsideMessage = !IsWindowVisible(handle);
                }
                return IntPtr.Zero;
            }
            source.AddHook(HideBeforeChrome);
            try
            {
                SendMessage(handle, 0x0112, new IntPtr(0xF030), IntPtr.Zero);
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.True(hideRequested);
                Assert.True(hiddenInsideMessage);
                Assert.False(IsWindowVisible(handle));
            }
            finally { source.RemoveHook(HideBeforeChrome); }
        });
    }

    [Fact]
    public void DisposeRestoresOriginalLayoutAndPreservesTheBackgroundResourceReference()
    {
        WpfTestHost.Invoke(() =>
        {
            using var host = new ChromeHost();
            new WindowInteropHelper(host.Window).EnsureHandle();
            var controller = host.CreateController();
            try
            {
                AttachForCurrentSystem(host, controller);
                controller.Dispose();
                controller.Dispose();

                Assert.False(controller.IsAttached);
                Assert.Null(WindowChrome.GetWindowChrome(host.Window));
                host.AssertOriginalLayout();
                host.Window.Resources[ChromeHost.BackgroundResourceKey] = Brushes.LightBlue;
                Assert.Same(Brushes.LightBlue, host.Window.Background);
            }
            finally { controller.Dispose(); }
        });
    }

    [Fact]
    public void ThemeResourceChangesDoNotCoverNativeButtonsAndRollbackUsesTheCurrentTheme()
    {
        WpfTestHost.Invoke(() =>
        {
            using var host = new ChromeHost();
            new WindowInteropHelper(host.Window).EnsureHandle();
            using var controller = host.CreateController();
            if (!AttachForCurrentSystem(host, controller))
                return;

            host.Window.Resources[ChromeHost.BackgroundResourceKey] = Brushes.Black;
            controller.ApplyTheme(true);
            Assert.Same(Brushes.Transparent, host.Window.Background);

            controller.SetFullScreen(true);
            Assert.Same(Brushes.Black, host.Window.Background);
            controller.SetFullScreen(false);
            Assert.Same(Brushes.Transparent, host.Window.Background);

            controller.Dispose();
            Assert.Same(Brushes.Black, host.Window.Background);
            host.Window.Resources[ChromeHost.BackgroundResourceKey] = Brushes.White;
            Assert.Same(Brushes.White, host.Window.Background);
        });
    }

    [Fact]
    public void ResourceRefreshWithoutAThemeChangedEventKeepsNativeCaptionButtonsUncovered()
    {
        WpfTestHost.Invoke(() =>
        {
            using var host = new ChromeHost();
            host.TitleBar.VerticalAlignment = VerticalAlignment.Top;
            host.Window.Resources.Remove(ChromeHost.BackgroundResourceKey);
            host.Window.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                [ChromeHost.BackgroundResourceKey] = Brushes.Beige
            });
            // A real Loaded visual tree is needed for WPF's dynamic-resource invalidation walk.
            host.Window.Show();
            using var controller = host.CreateController();
            if (!AttachForCurrentSystem(host, controller))
                return;
            WindowChrome chrome = WindowChrome.GetWindowChrome(host.Window);

            // ForceApplyTheme can reload dictionaries without raising CurrentUIThemeChanged.
            // Do not explicitly call controller.ApplyTheme here: the frame must remain transparent.
            host.Window.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                [ChromeHost.BackgroundResourceKey] = Brushes.Black
            });
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

            Assert.Same(Brushes.Transparent, host.Window.Background);
            Assert.Same(chrome, WindowChrome.GetWindowChrome(host.Window));

            controller.SetFullScreen(true);
            Assert.Same(Brushes.Black, host.Window.Background);
            host.Window.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                [ChromeHost.BackgroundResourceKey] = Brushes.White
            });
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.Same(Brushes.White, host.Window.Background);

            controller.SetFullScreen(false);
            Assert.Same(Brushes.Transparent, host.Window.Background);
            controller.Dispose();
            Assert.Same(Brushes.White, host.Window.Background);
        });
    }

    [Fact]
    public void FullScreenTemporarilyRemovesChromeAndRestoresTheSameInstanceAfterExit()
    {
        WpfTestHost.Invoke(() =>
        {
            using var host = new ChromeHost();
            new WindowInteropHelper(host.Window).EnsureHandle();
            using var controller = host.CreateController();
            if (!AttachForCurrentSystem(host, controller))
                return;

            WindowChrome chrome = WindowChrome.GetWindowChrome(host.Window);
            controller.SetFullScreen(true);
            Assert.True(controller.IsAttached);
            Assert.Null(WindowChrome.GetWindowChrome(host.Window));
            host.AssertOriginalLayout();

            controller.ApplyTheme(true);
            Assert.Null(WindowChrome.GetWindowChrome(host.Window));
            controller.SetFullScreen(false);

            Assert.True(controller.IsAttached);
            Assert.Same(chrome, WindowChrome.GetWindowChrome(host.Window));
            Assert.True(host.CaptionButtonsPlaceholder.Width > 0);
        });
    }

    [Fact]
    public void CancelledCloseKeepsChromeUntilTheWindowActuallyCloses()
    {
        WpfTestHost.Invoke(() =>
        {
            using var host = new ChromeHost();
            new WindowInteropHelper(host.Window).EnsureHandle();
            using var controller = host.CreateController();
            if (!AttachForCurrentSystem(host, controller))
                return;

            WindowChrome chrome = WindowChrome.GetWindowChrome(host.Window);
            CancelEventHandler cancelClose = (_, args) => args.Cancel = true;
            host.Window.Closing += cancelClose;
            try
            {
                host.Window.Close();
                Assert.True(controller.IsAttached);
                Assert.Same(chrome, WindowChrome.GetWindowChrome(host.Window));
            }
            finally { host.Window.Closing -= cancelClose; }

            host.Window.Close();
            Assert.False(controller.IsAttached);
        });
    }

    [Fact]
    public void HostClosedHandlerCanDisposeBeforeTheControllerReceivesClosed()
    {
        WpfTestHost.Invoke(() =>
        {
            using var host = new ChromeHost();
            using var controller = host.CreateController();
            // MainWindow registers its cleanup before SourceInitialized attaches the controller.
            host.Window.Closed += (_, _) => controller.Dispose();
            new WindowInteropHelper(host.Window).EnsureHandle();
            AttachForCurrentSystem(host, controller);

            host.Window.Close();

            Assert.False(controller.IsAttached);
        });
    }

    [Fact]
    public void DisposedControllerCanBeCollectedWhileItsWindowRemainsAlive()
    {
        ChromeHost host = WpfTestHost.Invoke(() => new ChromeHost());
        try
        {
            WeakReference reference = WpfTestHost.Invoke(() => CreateDisposedControllerReference(host));
            WpfTestHost.Invoke(() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle));
            for (int index = 0; index < 3 && reference.IsAlive; index++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            Assert.False(reference.IsAlive);
            GC.KeepAlive(host);
        }
        finally { WpfTestHost.Invoke(host.Dispose); }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateDisposedControllerReference(ChromeHost host)
    {
        new WindowInteropHelper(host.Window).EnsureHandle();
        var controller = host.CreateController();
        AttachForCurrentSystem(host, controller);
        controller.Dispose();
        return new WeakReference(controller);
    }

    private static bool AttachForCurrentSystem(ChromeHost host, CompactTitleBarChrome controller)
    {
        bool supported = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)
            && DwmIsCompositionEnabled(out bool compositionEnabled) == 0 && compositionEnabled;
        bool attached = controller.TryAttach();
        Assert.Equal(supported, attached);
        Assert.Equal(attached, controller.IsAttached);
        if (!attached)
        {
            Assert.Null(WindowChrome.GetWindowChrome(host.Window));
            host.AssertOriginalLayout();
        }
        return attached;
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool enabled);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    private static int NativeHitTest(IntPtr handle, Point point)
    {
        int x = (int)Math.Round(point.X);
        int y = (int)Math.Round(point.Y);
        return SendMessage(handle, 0x0084, IntPtr.Zero, new IntPtr((y << 16) | (x & 0xFFFF))).ToInt32();
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hwnd, int command);

    [DllImport("user32.dll")]
    private static extern IntPtr GetSystemMenu(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool revert);

    [DllImport("user32.dll")]
    private static extern uint GetMenuState(IntPtr menu, uint item, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeStyle
    {
        public uint Old;
        public uint New;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeWindowPosition
    {
        public IntPtr Window;
        public IntPtr InsertAfter;
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect bounds);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out NativeRect value, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private sealed class ChromeHost : IDisposable
    {
        internal const string BackgroundResourceKey = "SyntheticWindowBackground";
        private static readonly Thickness OriginalMargin = new(2, 3, 4, 5);
        internal Window Window { get; }
        internal Grid ContentRoot { get; } = new() { Margin = OriginalMargin };
        internal Grid TitleBar { get; } = new() { MinHeight = 17 };
        internal Border CaptionButtonsPlaceholder { get; } = new() { Width = 19 };

        internal ChromeHost()
        {
            TitleBar.Children.Add(CaptionButtonsPlaceholder);
            ContentRoot.Children.Add(TitleBar);
            Window = new Window
            {
                Content = ContentRoot,
                Width = 1000,
                Height = 600,
                Left = -10000,
                Top = -10000,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                WindowStyle = WindowStyle.SingleBorderWindow,
                ResizeMode = ResizeMode.CanResize,
            };
            // Rollback must restore the actual resource key, not assume the application's palette key.
            Window.Resources["GlobalBackground"] = Brushes.Magenta;
            Window.Resources[BackgroundResourceKey] = Brushes.Beige;
            Window.SetResourceReference(Control.BackgroundProperty, BackgroundResourceKey);
        }

        internal CompactTitleBarChrome CreateController() => new(Window, TitleBar, CaptionButtonsPlaceholder, ContentRoot);

        internal void AssertOriginalLayout()
        {
            Assert.Same(Brushes.Beige, Window.Background);
            Assert.Equal(17, TitleBar.MinHeight);
            Assert.Equal(19, CaptionButtonsPlaceholder.Width);
            Assert.Equal(OriginalMargin, ContentRoot.Margin);
        }

        public void Dispose() => Window.Close();
    }
}
