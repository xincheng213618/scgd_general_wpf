using System;
using System.Runtime.InteropServices;

namespace ColorVision.Common.NativeMethods
{
    public class FileProperties
    {



        [StructLayout(LayoutKind.Sequential)]
        public struct SHELLEXECUTEINFO
        {
            public int cbSize;
            public uint fMask;
            public IntPtr hwnd;
            [MarshalAs(UnmanagedType.LPStr)]
            public string lpVerb;
            [MarshalAs(UnmanagedType.LPStr)]
            public string lpFile;
            [MarshalAs(UnmanagedType.LPStr)]
            public string lpParameters;
            [MarshalAs(UnmanagedType.LPStr)]
            public string lpDirectory;
            public int nShow;
            public IntPtr hInstApp;
            public IntPtr lpIDList;
            [MarshalAs(UnmanagedType.LPStr)]
            public string? lpClass;
            public IntPtr hkeyClass;
            public uint dwHotKey;
            public IntPtr hIcon;
            public IntPtr hProcess;
        }

        private const int SW_SHOW = 5;
        private const uint SEE_MASK_INVOKEIDLIST = 12;

        [DllImport("shell32.dll")]
        static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);
        public static void ShowFolderProperties(string folderPath)
        {
            SHELLEXECUTEINFO info = new SHELLEXECUTEINFO
            {
                cbSize = Marshal.SizeOf(typeof(SHELLEXECUTEINFO)),
                fMask = SEE_MASK_INVOKEIDLIST,
                hwnd = IntPtr.Zero,
                lpVerb = "properties",
                lpFile = folderPath,
                lpParameters = string.Empty,
                lpDirectory = string.Empty,
                nShow = SW_SHOW,
                hInstApp = IntPtr.Zero,
                lpIDList = IntPtr.Zero,
                lpClass = null,
                hkeyClass = IntPtr.Zero,
                dwHotKey = 0,
                hIcon = IntPtr.Zero,
                hProcess = IntPtr.Zero
            };

            ShellExecuteEx(ref info);
        }
        public static void ShowFileProperties(string Filename)
        {
            SHELLEXECUTEINFO info = new();
            info.cbSize = Marshal.SizeOf(info);
            info.lpVerb = "properties";
            info.lpFile = Filename;
            info.nShow = SW_SHOW;
            info.fMask = SEE_MASK_INVOKEIDLIST;
            ShellExecuteEx(ref info);
        }

    }
}
