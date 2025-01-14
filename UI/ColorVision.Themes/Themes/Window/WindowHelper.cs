

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ColorVision.Common.NativeMethods;
using static ColorVision.Common.NativeMethods.User32;

namespace ColorVision.Themes.Controls
{
    public static class WindowHelper
    {
        [DllImport("user32.dll")]
        private static extern int MoveWindow(nint hWnd, int x, int y, int nWidth, int nHeight, [MarshalAs(UnmanagedType.Bool)] bool bRepaint);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);






        public enum WindowComposition
        {
            WcaAccentPolicy = 19
        }

        public static void BringToFront(this Window window, bool keep)
        {
            var handle = new WindowInteropHelper(window).Handle;
            keep |= window.Topmost;

            SetWindowPos(handle, HWNDTOPMOST, 0, 0, 0, 0, SWPNOMOVE | SWPNOSIZE | SWPNOACTIVATE);

            if (!keep)
                SetWindowPos(handle, HWNDNOTOPMOST, 0, 0, 0, 0, SWPNOMOVE | SWPNOSIZE | SWPNOACTIVATE);
        }




        public static void MoveWindow(this Window window, double pxLeft, double pxTop, double width, double height)
        {
            var handle = new WindowInteropHelper(window).EnsureHandle();

            // scale the size to the primary display
            TransformToPixels(window, width, height, out var pxWidth, out var pxHeight);

            // Use absolute location and relative size. WPF will scale the size to the target display
            _ = MoveWindow(handle, (int)Math.Round(pxLeft), (int)Math.Round(pxTop), pxWidth, pxHeight, true);
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(nint hwnd, out RECT lpRect);

        public static Rect GetWindowRectInPixel(this Window window)
        {
            var handle = new WindowInteropHelper(window).EnsureHandle();

            GetWindowRect(handle, out RECT nRect);

            return new Rect(new Point(nRect.Left, nRect.Top), new Point(nRect.Right, nRect.Bottom));
        }

        private static void TransformToPixels(this Visual visual, double unitX, double unitY, out int pixelX, out int pixelY)
        {
            Matrix matrix;
            var source = PresentationSource.FromVisual(visual);
            if (source != null)
                matrix = source.CompositionTarget.TransformToDevice;
            else
                using (var src = new HwndSource(new HwndSourceParameters()))
                {
                    matrix = src.CompositionTarget.TransformToDevice;
                }

            pixelX = (int)Math.Round(matrix.M11 * unitX);
            pixelY = (int)Math.Round(matrix.M22 * unitY);
        }

        [DllImport("user32.dll")]
        private static extern nint GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern nint GetWindowThreadProcessId(nint hWnd, out uint processId);
        public static bool IsForegroundWindowBelongToSelf()
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return false;

            GetWindowThreadProcessId(hwnd, out var procId);
            return procId == Environment.ProcessId;
        }

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(nint hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern nint SetWindowLong(nint hWnd, int nIndex, int dwNewLong);

        public static void SetNoactivate(this Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            SetWindowLong(hwnd, GWLEXSTYLE, GetWindowLong(hwnd, GWLEXSTYLE) | WSEXNOACTIVATE);
        }
        public static void RemoveWindowControls(this Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            SetWindowLong(hwnd, GWLSTYLE, GetWindowLong(hwnd, GWLSTYLE) & ~WSSYSMENU);
        }

        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(nint hwnd, ref WindowCompositionAttributeData data);

        public static void EnableBlur(Window window)
        {
            var accent = new AccentPolicy();
            var accentStructSize = Marshal.SizeOf(accent);
            accent.AccentState = AccentState.AccentEnableBlurbehind;
            accent.AccentFlags = 2;

            var accentPtr = Marshal.AllocHGlobal(accentStructSize);
            Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowComposition.WcaAccentPolicy,
                SizeOfData = accentStructSize,
                Data = accentPtr
            };

            _ = SetWindowCompositionAttribute(new WindowInteropHelper(window).EnsureHandle(), ref data);

            Marshal.FreeHGlobal(accentPtr);
        }

        private static void EnableDwmBlur(Window window, bool isDarkTheme, uint dwAttribute, int pvAttribute)
        {
            // Mica will handle the color
            window.Background = Brushes.Transparent;

            var hwnd = new WindowInteropHelper(window).EnsureHandle();

            var isDarkThemeInt = isDarkTheme ? 1 : 0;
            Dwmapi.SetWindowAttribute(hwnd, (uint)Dwmapi.Window.MicaEffect, ref isDarkThemeInt, Marshal.SizeOf(typeof(bool)));

            var margins = new Dwmapi.Margins(-1, -1, -1, -1);
            Dwmapi.ExtendFrameIntoClientArea(hwnd, ref margins);

            var val = pvAttribute;
            Dwmapi.SetWindowAttribute(hwnd, dwAttribute, ref val, Marshal.SizeOf(typeof(int)));
        }

        public static void EnableMicaBlur(Window window, bool isDarkTheme)
        {
            EnableDwmBlur(window, isDarkTheme, (uint)Dwmapi.Window.MicaEffect, 1);
        }

        public static void EnableBackdropMicaBlur(Window window, bool isDarkTheme)
        {
            EnableDwmBlur(window, isDarkTheme, (uint)Dwmapi.Window.SystembackdropType, (int)Dwmapi.SystembackdropType.MainWindow);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WindowCompositionAttributeData
        {
            public WindowComposition Attribute;
            public nint Data;
            public int SizeOfData;
        }

        private enum AccentState
        {
            AccentDisabled = 0,
            AccentEnableGradient = 1,
            AccentEnableTransparentgradient = 2,
            AccentEnableBlurbehind = 3,
            AccentInvalidState = 4
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public uint GradientColor;
            public readonly int AnimationId;
        }
    }
}