using System.Diagnostics;
using System.IO;
using System;
using System.Runtime.InteropServices;

namespace ColorVision.Common.NativeMethods
{
    public class DumpHelper
    {

        [DllImport("Dbghelp.dll")]
        private static extern bool MiniDumpWriteDump(
            IntPtr hProcess,
            uint processId,
            IntPtr hFile,
            int dumpType,
            IntPtr exceptionParam,
            IntPtr userStreamParam,
            IntPtr callbackParam);

        public static void WriteMiniDump(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var process = Process.GetCurrentProcess();
                var processHandle = process.Handle;
                var processId = (uint)process.Id;

                const int MiniDumpWithFullMemory = 0x00000002;

                MiniDumpWriteDump(processHandle, processId, fs.SafeFileHandle.DangerousGetHandle(), MiniDumpWithFullMemory, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            }
        }
    }
}
