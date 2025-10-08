using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ColorVision.Solution.NativeMethods
{
    /// <summary>
    /// PInvoke signatures for Windows ConPTY API
    /// </summary>
    internal static class ConPtyNativeMethods
    {
        private const string Kernel32 = "kernel32.dll";

        [DllImport(Kernel32, SetLastError = true)]
        internal static extern int CreatePseudoConsole(
            COORD size,
            SafeFileHandle hInput,
            SafeFileHandle hOutput,
            uint dwFlags,
            out IntPtr phPC);

        [DllImport(Kernel32, SetLastError = true)]
        internal static extern int ResizePseudoConsole(IntPtr hPC, COORD size);

        [DllImport(Kernel32, SetLastError = true)]
        internal static extern void ClosePseudoConsole(IntPtr hPC);

        [DllImport(Kernel32, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreatePipe(
            out SafeFileHandle hReadPipe,
            out SafeFileHandle hWritePipe,
            IntPtr lpPipeAttributes,
            uint nSize);

        [DllImport(Kernel32, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr hObject);

        [DllImport(Kernel32, CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreateProcess(
            string lpApplicationName,
            StringBuilder lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            [In] ref STARTUPINFOEX lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [StructLayout(LayoutKind.Sequential)]
        internal struct COORD
        {
            public short X;
            public short Y;

            public COORD(short x, short y)
            {
                X = x;
                Y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct STARTUPINFOEX
        {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [DllImport(Kernel32, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool InitializeProcThreadAttributeList(
            IntPtr lpAttributeList,
            int dwAttributeCount,
            int dwFlags,
            ref IntPtr lpSize);

        [DllImport(Kernel32, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UpdateProcThreadAttribute(
            IntPtr lpAttributeList,
            uint dwFlags,
            IntPtr attribute,
            IntPtr lpValue,
            IntPtr cbSize,
            IntPtr lpPreviousValue,
            IntPtr lpReturnSize);

        [DllImport(Kernel32, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteProcThreadAttributeList(IntPtr lpAttributeList);

        internal const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        internal const uint CREATE_NO_WINDOW = 0x08000000;
        internal const int PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;
        internal const int STARTF_USESTDHANDLES = 0x00000100;
    }
}
