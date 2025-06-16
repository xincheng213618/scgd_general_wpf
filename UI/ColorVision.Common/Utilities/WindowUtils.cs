#pragma warning disable CA1707

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace ColorVision.Common.Utilities
{
    public static class WindowUtils
    {
        public static class WindowNotifications
        {
            /// <summary>
            /// Sent to a window when its nonclient area needs to be changed to indicate an active or inactive state.
            /// </summary>
            public const int WM_NCACTIVATE = 0x0086;
            public const uint MF_BYCOMMAND = 0x00000000;
            public const uint MF_GRAYED = 0x00000001;
            public const uint MF_ENABLED = 0x00000000;
            public const uint SC_CLOSE = 0xF060;
            public const int WM_SHOWWINDOW = 0x00000018;
            public const int WM_CLOSE = 0x10;
        }

        public static void DelayClearImage(this Window window, Action action, int delay = 100)
        {
            IntPtr handle = new WindowInteropHelper(window).Handle;
            //这里不知道为什么有时候会是空
            if (handle == IntPtr.Zero) return;

            bool canClose = false;

            // Using a local function for the hook to avoid creating a new delegate each time.
            IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
            {
                if (msg == WindowNotifications.WM_CLOSE)
                {
                    handled = !canClose;
                    if (!canClose)
                    {
                        Task.Run(Run);
                    }
                    else
                    {
                        // Remove the hook here to prevent memory leaks.
                        HwndSource.FromHwnd(handle)?.RemoveHook(WndProc);
                    }
                }
                return IntPtr.Zero;
            }

            HwndSource hwndSource = HwndSource.FromHwnd(handle);
            hwndSource?.AddHook(WndProc);

            // The Run method is asynchronous and does not need to be an async void. 
            // It's better to use async Task when possible to avoid unhandled exceptions.
            async Task Run()
            {
                action.Invoke();
                await Task.Delay(delay);
                canClose = true;

                  _= Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    if (window != null && !window.IsDisposed())
                    {
                        window.Close();
                    }
                });
            }
        }

        // Utilities method to check if a window has been disposed.
        public static bool IsDisposed(this Window window)
        {
            // Check if the window is disposed based on its state.
            return !window.IsLoaded && !window.IsVisible && window.IsInitialized;
        }



    }
}
