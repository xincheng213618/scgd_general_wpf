#pragma warning disable CA1707,CA1711,CA1712,CA1401,CA1051,CA2101,CA1838,CA1806
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace ColorVision.Common.NativeMethods
{
    public class DumpHelper
    {

        [DllImport("Dbghelp.dll", SetLastError = true)]
        private static extern bool MiniDumpWriteDump(
            IntPtr hProcess,
            uint processId,
            IntPtr hFile,
            int dumpType,
            IntPtr exceptionParam,
            IntPtr userStreamParam,
            IntPtr callbackParam);

        public static void WriteMiniDump(string filePath, int dumpType = 0x00000002)
        {
            string fullPath = Path.GetFullPath(filePath);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var process = Process.GetCurrentProcess();
            bool succeeded = MiniDumpWriteDump(
                process.Handle,
                (uint)process.Id,
                fileStream.SafeFileHandle.DangerousGetHandle(),
                dumpType,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);

            if (!succeeded)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "MiniDumpWriteDump failed.");
        }
    }
}
