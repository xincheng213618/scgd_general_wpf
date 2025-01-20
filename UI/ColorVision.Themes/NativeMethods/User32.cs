using System;
using System.Runtime.InteropServices;

namespace ColorVision.Themes.NativeMethods
{
    public static class User32
    {
        public delegate int KeyboardHookProc(int code, int wParam, ref KeyboardHookStruct lParam);

        public enum MonitorDefaults
        {
            TONULL = 0,
            TOPRIMARY = 1,
            TONEAREST = 2
        }

        public enum DeviceCap
        {
            /// <summary>
            ///     Logical pixels inch in X
            /// </summary>
            LOGPIXELSX = 88,
            /// <summary>
            ///     Logical pixels inch in Y
            /// </summary>
            LOGPIXELSY = 90
        }

        public struct KeyboardHookStruct
        {
            public int vkCode { get; set; }
            public int scanCode { get; set; }
            public int flags { get; set; }
            public int time { get; set; }
            public int dwExtraInfo { get; set; }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFOEX
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct DISPLAYDEVICE
        {
            public uint cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public uint StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        // ReSharper disable InconsistentNaming
        public static readonly nint HWNDTOPMOST = new IntPtr(-1);
        public static readonly nint HWNDNOTOPMOST = new IntPtr(-2);
        public static readonly nint HWNDTOP = IntPtr.Zero;
        public static readonly nint HWNDBOTTOM = new IntPtr(1);
        public const uint SWPNOSIZE = 0x0001;
        public const uint SWPNOMOVE = 0x0002;
        public const uint SWPNOZORDER = 0x0004;
        public const uint SWPNOREDRAW = 0x0008;
        public const uint SWPNOACTIVATE = 0x0010;
        public const uint SWPDRAWFRAME = 0x0020;
        public const uint SWPFRAMECHANGED = 0x0020;
        public const uint SWPSHOWWINDOW = 0x0040;
        public const uint SWPHIDEWINDOW = 0x0080;
        public const uint SWPNOCOPYBITS = 0x0100;
        public const uint SWPNOOWNERZORDER = 0x0200;
        public const uint SWPNOREPOSITION = 0x0200;
        public const uint SWPNOSENDCHANGING = 0x0400;
        public const uint SWPDEFERERASE = 0x2000;
        public const uint SWPASYNCWINDOWPOS = 0x4000;

        public const int WHKEYBOARDLL = 13;
        public const int WMKEYDOWN = 0x100;
        public const int WMKEYUP = 0x101;
        public const int WMSYSKEYDOWN = 0x104;
        public const int WMSYSKEYUP = 0x105;
        public const int GWLSTYLE = -16;
        public const int GWLEXSTYLE = -20;
        public const int WSSYSMENU = 0x00080000;
        public const int WSMINIMIZEBOX = 0x00020000;
        public const int WSMAXIMIZEBOX = 0x00010000;
        public const int WSEXNOACTIVATE = 0x08000000;

        public const uint GAPARENT = 1;
        public const uint GAROOT = 2;
        public const uint GAROOTOWNER = 3;
        // ReSharper restore InconsistentNaming
    }
}