using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shell;

namespace ColorVision.Common.Utilities
{
    /// <summary>A reversible, monitor-sized window presentation. Nested sessions exit in reverse order.</summary>
    public sealed class WindowFullScreenSession : IDisposable
    {
        private static readonly DependencyPropertyKey IsActivePropertyKey = DependencyProperty.RegisterAttachedReadOnly(
            "IsActive", typeof(bool), typeof(WindowFullScreenSession), new PropertyMetadata(false));
        public static readonly DependencyProperty IsActiveProperty = IsActivePropertyKey.DependencyProperty;
        private static readonly DependencyProperty CurrentProperty = DependencyProperty.RegisterAttached(
            "Current", typeof(WindowFullScreenSession), typeof(WindowFullScreenSession));

        public static bool GetIsActive(Window window) => (bool)window.GetValue(IsActiveProperty);

        private readonly Window window;
        private readonly Action requestExit;
        private readonly WindowFullScreenSession? previous;
        private readonly Rect bounds;
        private readonly WindowState state;
        private readonly WindowStyle style;
        private readonly ResizeMode resizeMode;
        private readonly SizeToContent sizeToContent;
        private readonly WindowChrome? chrome;
        private WindowFullScreenOverlay? overlay;
        private bool disposed;

        public WindowFullScreenSession(Window window, Action requestExit)
        {
            this.window = window ?? throw new ArgumentNullException(nameof(window));
            this.requestExit = requestExit ?? throw new ArgumentNullException(nameof(requestExit));
            window.VerifyAccess();
            IntPtr handle = new WindowInteropHelper(window).EnsureHandle();
            var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(MonitorFromWindow(handle, 2), ref monitor))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            previous = (WindowFullScreenSession?)window.GetValue(CurrentProperty);
            bounds = window.WindowState == WindowState.Normal
                ? new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight)
                : window.RestoreBounds;
            state = window.WindowState;
            style = window.WindowStyle;
            resizeMode = window.ResizeMode;
            sizeToContent = window.SizeToContent;
            chrome = WindowChrome.GetWindowChrome(window);
            previous?.overlay?.Hide();
            window.SetValue(CurrentProperty, this);
            // Notify custom chrome before changing any styles, bounds or content.
            window.SetValue(IsActivePropertyKey, true);
            try
            {
                WindowChrome.SetWindowChrome(window, null);
                window.SetCurrentValue(Window.SizeToContentProperty, SizeToContent.Manual);
                window.SetCurrentValue(Window.WindowStateProperty, WindowState.Normal);
                window.SetCurrentValue(Window.WindowStyleProperty, WindowStyle.None);
                window.SetCurrentValue(Window.ResizeModeProperty, ResizeMode.NoResize);
                NativeRect rect = monitor.Monitor;
                // Use physical monitor bounds, including the taskbar area. Do not force Topmost.
                if (!SetWindowPos(handle, IntPtr.Zero, rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top, 0x0034))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                window.PreviewKeyDown += OnPreviewKeyDown;
                window.Closed += OnClosed;
                overlay = new WindowFullScreenOverlay(window, () => ReferenceEquals(window.GetValue(CurrentProperty), this), requestExit);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Handled || !ReferenceEquals(window.GetValue(CurrentProperty), this) || Keyboard.Modifiers != ModifierKeys.None ||
                (e.Key != Key.F11 && e.Key != Key.Escape))
                return;
            e.Handled = true;
            if (!e.IsRepeat)
                requestExit();
        }

        private void OnClosed(object? sender, EventArgs e) => Release(restoreWindow: false);

        public void Dispose()
        {
            window.VerifyAccess();
            if (disposed)
                return;
            if (!ReferenceEquals(window.GetValue(CurrentProperty), this))
                throw new InvalidOperationException("Exit the active full-screen session before its parent session.");
            Release(restoreWindow: true);
        }

        private void Release(bool restoreWindow)
        {
            if (disposed)
                return;
            disposed = true;
            overlay?.Dispose();
            window.PreviewKeyDown -= OnPreviewKeyDown;
            window.Closed -= OnClosed;
            if (restoreWindow)
            {
                window.SetCurrentValue(Window.WindowStateProperty, WindowState.Normal);
                window.SetCurrentValue(Window.WindowStyleProperty, style);
                window.SetCurrentValue(Window.ResizeModeProperty, resizeMode);
                window.SetCurrentValue(Window.LeftProperty, bounds.Left);
                window.SetCurrentValue(Window.TopProperty, bounds.Top);
                window.SetCurrentValue(Window.WidthProperty, bounds.Width);
                window.SetCurrentValue(Window.HeightProperty, bounds.Height);
                window.SetCurrentValue(Window.SizeToContentProperty, sizeToContent);
                window.SetCurrentValue(Window.WindowStateProperty, state);
                WindowChrome.SetWindowChrome(window, chrome);
            }
            window.SetValue(CurrentProperty, restoreWindow ? previous : null);
            window.SetValue(IsActivePropertyKey, previous != null && restoreWindow);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
    }
}
