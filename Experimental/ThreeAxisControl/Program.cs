using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;

namespace ThreeAxisControl
{
    internal static class Program
    {
        private const string InstanceId = "ColorVision.ThreeAxisControl.F017C2F7-BF7F-4A76-8C5A-DA92B08E12C0";
        private const int StartupWaitMilliseconds = 4000;
        private const int WmNcDestroy = 0x0082;
        private const int SwRestore = 9;

        [STAThread]
        private static void Main()
        {
            uint activationMessage = Native.RegisterWindowMessage(InstanceId);
            using (var mutex = new Mutex(false, @"Local\" + InstanceId))
            {
                if (!AcquireOrActivate(mutex, activationMessage))
                    return;

                try
                {
                    var application = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                    var window = new MainWindow();
                    window.SourceInitialized += (_, __) => MarkWindow(window, activationMessage);
                    application.Run(window);
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
        }

        private static bool AcquireOrActivate(Mutex mutex, uint activationMessage)
        {
            var elapsed = Stopwatch.StartNew();
            do
            {
                try
                {
                    if (mutex.WaitOne(0))
                        return true;
                }
                catch (AbandonedMutexException)
                {
                    return true;
                }

                if (RequestActivation(activationMessage))
                    return false;

                // The first process may own the mutex while WPF is still creating its window.
                Thread.Sleep(50);
            }
            while (elapsed.ElapsedMilliseconds < StartupWaitMilliseconds);

            return false;
        }

        private static bool RequestActivation(uint activationMessage)
        {
            if (activationMessage == 0)
                return false;

            IntPtr existingWindow = IntPtr.Zero;
            Native.EnumWindows((handle, _) =>
            {
                if (Native.GetProp(handle, InstanceId) == IntPtr.Zero)
                    return true;

                existingWindow = handle;
                return false;
            }, IntPtr.Zero);
            if (existingWindow == IntPtr.Zero)
                return false;

            Native.GetWindowThreadProcessId(existingWindow, out uint processId);
            if (processId == 0)
                return false;

            Native.AllowSetForegroundWindow(processId);
            return Native.PostMessage(existingWindow, activationMessage, IntPtr.Zero, IntPtr.Zero);
        }

        private static void MarkWindow(Window window, uint activationMessage)
        {
            if (activationMessage == 0 || !(PresentationSource.FromVisual(window) is HwndSource source))
                return;

            source.AddHook((IntPtr handle, int message, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            {
                if (message == WmNcDestroy)
                    Native.RemoveProp(handle, InstanceId);
                else if (message == activationMessage)
                {
                    window.Dispatcher.BeginInvoke(new Action(() => ActivateWindow(handle)));
                    handled = true;
                }

                return IntPtr.Zero;
            });
            Native.SetProp(source.Handle, InstanceId, new IntPtr(1));
        }

        private static void ActivateWindow(IntPtr handle)
        {
            if (!Native.IsWindowVisible(handle))
                return;

            if (Native.IsIconic(handle))
                Native.ShowWindow(handle, SwRestore);

            IntPtr popup = Native.GetLastActivePopup(handle);
            Native.SetForegroundWindow(popup != IntPtr.Zero && Native.IsWindowVisible(popup) ? popup : handle);
        }

        private static class Native
        {
            internal delegate bool EnumWindowsProc(IntPtr handle, IntPtr parameter);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            internal static extern uint RegisterWindowMessage(string name);

            [DllImport("user32.dll")]
            internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            internal static extern IntPtr GetProp(IntPtr handle, string name);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            internal static extern bool SetProp(IntPtr handle, string name, IntPtr data);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            internal static extern IntPtr RemoveProp(IntPtr handle, string name);

            [DllImport("user32.dll")]
            internal static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

            [DllImport("user32.dll")]
            internal static extern bool AllowSetForegroundWindow(uint processId);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            internal static extern bool PostMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll")]
            internal static extern bool IsIconic(IntPtr handle);

            [DllImport("user32.dll")]
            internal static extern bool ShowWindow(IntPtr handle, int command);

            [DllImport("user32.dll")]
            internal static extern IntPtr GetLastActivePopup(IntPtr handle);

            [DllImport("user32.dll")]
            internal static extern bool IsWindowVisible(IntPtr handle);

            [DllImport("user32.dll")]
            internal static extern bool SetForegroundWindow(IntPtr handle);
        }
    }
}
