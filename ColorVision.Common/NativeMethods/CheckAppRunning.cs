using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ColorVision.NativeMethods
{
    //这里备注一下：实现的功能为，检测到软件已经打开，就把软件的窗口置顶，并且闪烁几下，同时会检测软件的版本
    public static class CheckAppRunning
    {

        [DllImport("user32.dll")]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        //private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        private static extern int GetWindowText(IntPtr hWnd, char[] lpString, int nMaxCount);

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



        public static IntPtr Check(string WindowTitle)
        {
            var currentProcess = Process.GetCurrentProcess();
            var processList = Process.GetProcessesByName(currentProcess.ProcessName);
            IntPtr hwnd;
            if (processList.Length > 1)
            {
                // 遍历所有进程窗口，找到需要提示的窗口进行闪烁
                foreach (var process in processList)
                {
                    hwnd = process.MainWindowHandle;
                    // 如果窗口不可见，则继续遍历下一个窗口
                    if (!IsWindowVisible(hwnd))
                    {
                        continue;
                    }

                    //StringBuilder sb = new StringBuilder(256);
                    //GetWindowText(hwnd, sb, sb.Capacity);
                    char[] chars = new char[1024];
                    int size = GetWindowText(hwnd, chars, chars.Length);
                    // 如果窗口标题包含“提示”等关键词，则进行闪烁
                    if (new string(chars, 0, size).Contains(WindowTitle))
                    {
                        string FilenName = process.MainModule?.FileName;
                        var fvi = FileVersionInfo.GetVersionInfo(FilenName ?? "");
                        Version versionrun = new Version(fvi?.FileVersion ?? "");
                        Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                        if (version != versionrun)
                        {
                            continue;
                        }

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
                        return hwnd;
                    }
                }
            }
            return IntPtr.Zero;
        }

    }
}
