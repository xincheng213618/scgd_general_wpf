using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ColorVision.NativeMethods
{
    public static class CheckAppRuning
    {
        [DllImport("user32.dll")]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);


        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;


        [StructLayout(LayoutKind.Sequential)]
        public struct FLASHWINFO
        {
            public UInt32 cbSize;
            public IntPtr hwnd;
            public UInt32 dwFlags;
            public UInt32 uCount;
            public UInt32 dwTimeout;
        }

        private const uint FLASHW_ALL = 0x03;
        private const uint FLASHW_TIMERNOFG = 0x0C;



        public static void Check()
        {
            var currentProcess = Process.GetCurrentProcess();
            var processList = Process.GetProcessesByName(currentProcess.ProcessName);
            if (processList.Length > 1)
            {
                // 遍历所有进程窗口，找到需要提示的窗口进行闪烁
                foreach (var process in processList)
                {
                    IntPtr hwnd = process.MainWindowHandle;

                    // 如果窗口不可见，则继续遍历下一个窗口
                    if (!IsWindowVisible(hwnd))
                    {
                        continue;
                    }

                    StringBuilder sb = new StringBuilder(256);
                    GetWindowText(hwnd, sb, sb.Capacity);

                    // 如果窗口标题包含“提示”等关键词，则进行闪烁
                    if (sb.ToString().Contains("MainWindow"))
                    {
                        SetForegroundWindow(hwnd);
                        FLASHWINFO fLASHWINFO = new FLASHWINFO
                        {
                            cbSize = Convert.ToUInt32(Marshal.SizeOf(typeof(FLASHWINFO))),
                            dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
                            hwnd = hwnd,
                            uCount = UInt32.MaxValue,
                            dwTimeout = 0
                        };
                        //FlashWindowEx(ref fLASHWINFO);
                        if (IsIconic(hwnd))
                        {
                            ShowWindowAsync(hwnd, SW_RESTORE);
                        }
                        break;
                    }
                }
            }
        }

    }
}
