using ColorVision.Solution;
using ColorVision.Solution.Editor;
using ColorVision.UI;
using ColorVision.UI.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace ColorVision
{
    public partial class IntegratedMainWindow
    {
        private const int WM_NCHITTEST = 0x0084;
        private const int WM_NCMOUSEMOVE = 0x00A0;
        private const int WM_NCLBUTTONDOWN = 0x00A1;
        private const int WM_NCLBUTTONUP = 0x00A2;
        private const int WM_CAPTURECHANGED = 0x0215;
        private const int WM_NCMOUSELEAVE = 0x02A2;
        private const int HTMAXBUTTON = 9;
        private const int GWLP_WNDPROC = -4;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWCP_ROUND = 2;
        private const uint TME_LEAVE = 0x00000002;
        private const uint TME_NONCLIENT = 0x00000010;

        private delegate IntPtr WndProcDelegate(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        private HwndSource? _mainWindowHwndSource;
        private IntPtr _mainWindowHwnd;
        private IntPtr _previousWndProc;
        private WndProcDelegate? _subclassWndProc;
        private bool _isTrackingMaximizeButtonLeave;
        private bool _isMaximizeButtonHot;
        private bool _isMaximizeButtonPressed;

        [StructLayout(LayoutKind.Sequential)]
        private struct TrackMouseEventOptions
        {
            public int cbSize;
            public uint dwFlags;
            public IntPtr hwndTrack;
            public uint dwHoverTime;
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool TrackMouseEvent(ref TrackMouseEventOptions lpEventTrack);

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            _mainWindowHwnd = new WindowInteropHelper(this).Handle;
            _mainWindowHwndSource = HwndSource.FromHwnd(_mainWindowHwnd);
            _mainWindowHwndSource?.AddHook(MainWindowWndProc);

            _subclassWndProc = MainWindowSubclassWndProc;
            IntPtr previousWndProc = SetWindowLongPtr(
                _mainWindowHwnd,
                GWLP_WNDPROC,
                Marshal.GetFunctionPointerForDelegate(_subclassWndProc));
            if (previousWndProc != IntPtr.Zero)
                _previousWndProc = previousWndProc;
            else
                _subclassWndProc = null;

            ApplyMainWindowDwmAttributes();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_mainWindowHwndSource != null)
            {
                _mainWindowHwndSource.RemoveHook(MainWindowWndProc);
                _mainWindowHwndSource = null;
            }

            if (_mainWindowHwnd != IntPtr.Zero && _previousWndProc != IntPtr.Zero)
            {
                SetWindowLongPtr(_mainWindowHwnd, GWLP_WNDPROC, _previousWndProc);
                _previousWndProc = IntPtr.Zero;
            }

            _subclassWndProc = null;
            base.OnClosed(e);
        }

        private IntPtr MainWindowSubclassWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_NCHITTEST)
            {
                if (IsPointOverMaximizeOrRestoreButton(lParam))
                    return new IntPtr(HTMAXBUTTON);

                SetMaximizeRestoreButtonChromeState(false, false);
            }

            if (msg == WM_NCMOUSEMOVE && wParam.ToInt32() == HTMAXBUTTON)
            {
                SetMaximizeRestoreButtonChromeState(true, _isMaximizeButtonPressed);
                TrackNonClientMouseLeave(hwnd);
            }

            if (msg == WM_NCLBUTTONDOWN && wParam.ToInt32() == HTMAXBUTTON && IsPointOverMaximizeOrRestoreButton(lParam))
            {
                _isMaximizeButtonPressed = true;
                SetMaximizeRestoreButtonChromeState(true, true);
                return IntPtr.Zero;
            }

            if (msg == WM_NCLBUTTONUP && _isMaximizeButtonPressed)
            {
                _isMaximizeButtonPressed = false;
                if (IsPointOverMaximizeOrRestoreButton(lParam))
                    ToggleMaximizeRestoreFromChrome();
                SetMaximizeRestoreButtonChromeState(true, false);
                return IntPtr.Zero;
            }

            if (msg == WM_CAPTURECHANGED)
            {
                _isMaximizeButtonPressed = false;
                SetMaximizeRestoreButtonChromeState(_isMaximizeButtonHot, false);
            }

            if (msg == WM_NCMOUSELEAVE)
            {
                _isTrackingMaximizeButtonLeave = false;
                SetMaximizeRestoreButtonChromeState(false, false);
            }

            return _previousWndProc != IntPtr.Zero
                ? CallWindowProc(_previousWndProc, hwnd, msg, wParam, lParam)
                : IntPtr.Zero;
        }

        private IntPtr MainWindowWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_NCHITTEST && IsPointOverMaximizeOrRestoreButton(lParam))
            {
                handled = true;
                return new IntPtr(HTMAXBUTTON);
            }

            if (msg == SingleInstanceCommandLineTransport.MessageId
                && SingleInstanceCommandLineTransport.TryReceive(lParam, out string[] parsedArgs))
            {
                try
                {
                    ForwardedCommandLineHandler.Handle(parsedArgs);
                    if (WindowState == WindowState.Minimized)
                        WindowState = WindowState.Normal;
                    Activate();
                    handled = true;
                    return new IntPtr(1);
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
            }

            return IntPtr.Zero;
        }

        private void ApplyMainWindowDwmAttributes()
        {
            if (_mainWindowHwnd == IntPtr.Zero)
                return;

            try
            {
                int cornerPreference = DWMWCP_ROUND;
                _ = DwmSetWindowAttribute(
                    _mainWindowHwnd,
                    DWMWA_WINDOW_CORNER_PREFERENCE,
                    ref cornerPreference,
                    Marshal.SizeOf<int>());

                if (TryGetDwmColor(IsActive ? "StatusBarBackgroundBrush" : "GlobalBorderBrush", out int borderColor))
                {
                    _ = DwmSetWindowAttribute(
                        _mainWindowHwnd,
                        DWMWA_BORDER_COLOR,
                        ref borderColor,
                        Marshal.SizeOf<int>());
                }
            }
            catch
            {
            }
        }

        private void ToggleMaximizeRestoreFromChrome()
        {
            if (!CanResizeWindow())
                return;

            if (WindowState == WindowState.Maximized)
                SystemCommands.RestoreWindow(this);
            else
                SystemCommands.MaximizeWindow(this);

            UpdateWindowCommandButtonState();
            SetMaximizeRestoreButtonChromeState(_isMaximizeButtonHot, false);
        }

        private bool IsPointOverMaximizeOrRestoreButton(IntPtr lParam)
        {
            Button? button = WindowState == WindowState.Maximized ? RestoreButton : MaximizeButton;
            if (button == null || !button.IsVisible || !button.IsEnabled || button.ActualWidth <= 0 || button.ActualHeight <= 0)
                return false;

            Point screenPoint = new(GetSignedLoWord(lParam), GetSignedHiWord(lParam));
            Point buttonPoint = button.PointFromScreen(screenPoint);
            Rect buttonRect = new(0, 0, button.ActualWidth, button.ActualHeight);
            return buttonRect.Contains(buttonPoint);
        }

        private void SetMaximizeRestoreButtonChromeState(bool isHot, bool isPressed)
        {
            _isMaximizeButtonHot = isHot;
            string? tag = isPressed ? "ChromePressed" : isHot ? "ChromeHover" : null;

            if (MaximizeButton != null)
                MaximizeButton.Tag = MaximizeButton.IsVisible ? tag : null;

            if (RestoreButton != null)
                RestoreButton.Tag = RestoreButton.IsVisible ? tag : null;
        }

        private void TrackNonClientMouseLeave(IntPtr hwnd)
        {
            if (_isTrackingMaximizeButtonLeave)
                return;

            TrackMouseEventOptions trackMouseEvent = new()
            {
                cbSize = Marshal.SizeOf<TrackMouseEventOptions>(),
                dwFlags = TME_LEAVE | TME_NONCLIENT,
                hwndTrack = hwnd,
                dwHoverTime = 0
            };

            _isTrackingMaximizeButtonLeave = TrackMouseEvent(ref trackMouseEvent);
        }

        private bool TryGetDwmColor(string resourceKey, out int colorRef)
        {
            if (TryFindResource(resourceKey) is SolidColorBrush brush)
            {
                Color color = brush.Color;
                colorRef = color.R | (color.G << 8) | (color.B << 16);
                return true;
            }

            colorRef = 0;
            return false;
        }

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
                : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        private static int GetSignedLoWord(IntPtr value) => unchecked((short)((long)value & 0xffff));

        private static int GetSignedHiWord(IntPtr value) => unchecked((short)(((long)value >> 16) & 0xffff));
    }
}
