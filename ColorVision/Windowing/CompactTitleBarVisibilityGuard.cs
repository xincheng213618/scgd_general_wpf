using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Shell;

namespace ColorVision.Windowing
{
    /// <summary>
    /// Suppresses only WindowChromeWorker's temporary WS_VISIBLE clear while it
    /// updates the system menu for a size/state message (dotnet/wpf#3193).
    /// Native show/hide operations, window geometry and animation are not replaced.
    /// </summary>
    internal sealed class CompactTitleBarVisibilityGuard : IDisposable
    {
        private const uint VisibleStyle = 0x10000000;
        private const uint NoSize = 0x0001;
        private const uint HideWindow = 0x0080;
        private readonly Window window;
        private readonly WindowChrome chrome;
        private readonly IntPtr handle;
        private readonly SubclassProcedure callback;
        private WindowState lastMenuState;
        private int callbackDepth;
        private int expectedStyleDepth;
        private int pulseBudget;
        private long hideGeneration;
        private bool enabled;
        private bool attached;
        private bool disposed;

        internal CompactTitleBarVisibilityGuard(Window window, IntPtr handle, WindowChrome chrome)
        {
            this.window = window;
            this.handle = handle;
            this.chrome = chrome;
            callback = WindowProc;
        }

        internal bool TryAttach()
        {
            window.VerifyAccess();
            if (disposed)
                return false;
            if (attached)
                return true;
            attached = SetWindowSubclass(handle, callback, 1, 0);
            if (attached)
                SetEnabled(true);
            return attached;
        }

        internal void SetEnabled(bool value)
        {
            window.VerifyAccess();
            enabled = value && attached && !disposed;
            pulseBudget = 0;
            expectedStyleDepth = 0;
            hideGeneration++;
            // _ApplyNewCustomChrome initializes its menu with Window.WindowState.
            // The host calls this after attaching/resuming that same chrome.
            lastMenuState = window.WindowState;
        }

        private IntPtr WindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, nuint id, nuint data)
        {
            if (message == 0x0082) // WM_NCDESTROY
            {
                Dispose();
                return DefSubclassProc(hwnd, message, wParam, lParam);
            }

            callbackDepth++;
            try
            {
                if (message == 0x007C) // WM_STYLECHANGING
                {
                    PreserveExpectedVisibility(wParam, lParam);
                    return DefSubclassProc(hwnd, message, wParam, lParam);
                }

                bool isHide = message == 0x0018 && wParam == IntPtr.Zero; // WM_SHOWWINDOW(false)
                WindowPosition position = default;
                if (message is 0x0046 or 0x0047) // WM_WINDOWPOSCHANGING / CHANGED
                {
                    position = Marshal.PtrToStructure<WindowPosition>(lParam);
                    isHide |= (position.Flags & HideWindow) != 0;
                }
                if (isHide)
                {
                    // Also revoke an outer size-message budget when application code
                    // performs a real native Hide during a re-entrant callback.
                    hideGeneration++;
                    pulseBudget = 0;
                    expectedStyleDepth = 0;
                }

                int previousBudget = pulseBudget;
                int previousDepth = expectedStyleDepth;
                long entryHideGeneration = hideGeneration;
                // Budgets belong to one direct message dispatch, never to all nested
                // work done by the application while processing that message.
                pulseBudget = ExpectsMenuUpdate(message, wParam, position) && !isHide ? 1 : 0;
                expectedStyleDepth = pulseBudget == 1 ? callbackDepth + 1 : 0;
                try
                {
                    return DefSubclassProc(hwnd, message, wParam, lParam);
                }
                finally
                {
                    bool restore = enabled && !disposed && entryHideGeneration == hideGeneration;
                    pulseBudget = restore ? previousBudget : 0;
                    expectedStyleDepth = restore ? previousDepth : 0;
                }
            }
            finally
            {
                callbackDepth--;
            }
        }

        private bool ExpectsMenuUpdate(uint message, IntPtr wParam, WindowPosition position)
        {
            if (!enabled || disposed || !ReferenceEquals(WindowChrome.GetWindowChrome(window), chrome))
                return false;
            if (message != 0x0005 && (message != 0x0047 || (position.Flags & NoSize) != 0))
                return false;

            bool forcedMaximizeUpdate = message == 0x0005 && wParam.ToInt64() == 2; // SIZE_MAXIMIZED
            var placement = new WindowPlacement { Length = Marshal.SizeOf<WindowPlacement>() };
            if (!GetWindowPlacement(handle, ref placement))
                return false;
            WindowState state = placement.ShowCommand switch
            {
                2 => WindowState.Minimized,
                3 => WindowState.Maximized,
                _ => WindowState.Normal
            };
            bool changed = state != lastMenuState;
            lastMenuState = forcedMaximizeUpdate ? WindowState.Maximized : state;
            // Normal-to-normal resize does not update the menu. An unused blanket
            // budget there could instead intercept a subsequent application Hide.
            return forcedMaximizeUpdate || message == 0x0047 && changed;
        }

        private void PreserveExpectedVisibility(IntPtr styleKind, IntPtr value)
        {
            if (!enabled || disposed || pulseBudget != 1 || callbackDepth != expectedStyleDepth ||
                unchecked((int)styleKind.ToInt64()) != -16 || value == IntPtr.Zero ||
                window.Visibility != Visibility.Visible || !window.IsVisible || !IsWindowVisible(handle) ||
                !ReferenceEquals(WindowChrome.GetWindowChrome(window), chrome))
                return;

            var style = Marshal.PtrToStructure<StyleChange>(value);
            if ((style.OldStyle ^ style.NewStyle) != VisibleStyle || (style.OldStyle & VisibleStyle) == 0)
                return;

            pulseBudget = 0;
            style.NewStyle |= VisibleStyle;
            Marshal.StructureToPtr(style, value, false);
        }

        public void Dispose()
        {
            window.VerifyAccess();
            if (disposed)
                return;
            disposed = true;
            enabled = false;
            pulseBudget = 0;
            expectedStyleDepth = 0;
            hideGeneration++;
            if (attached)
            {
                RemoveWindowSubclass(handle, callback, 1);
                attached = false;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct StyleChange { public uint OldStyle; public uint NewStyle; }
        [StructLayout(LayoutKind.Sequential)]
        private struct WindowPosition { public IntPtr Window; public IntPtr InsertAfter; public int X; public int Y; public int Width; public int Height; public uint Flags; }
        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint { public int X; public int Y; }
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        private struct WindowPlacement { public int Length; public uint Flags; public uint ShowCommand; public NativePoint MinPosition; public NativePoint MaxPosition; public NativeRect NormalPosition; }
        private delegate IntPtr SubclassProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, nuint id, nuint data);
        [DllImport("comctl32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowSubclass(IntPtr hwnd, SubclassProcedure callback, nuint id, nuint data);
        [DllImport("comctl32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RemoveWindowSubclass(IntPtr hwnd, SubclassProcedure callback, nuint id);
        [DllImport("comctl32.dll")]
        private static extern IntPtr DefSubclassProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowPlacement(IntPtr hwnd, ref WindowPlacement placement);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hwnd);
    }
}
