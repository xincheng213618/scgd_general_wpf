using log4net;
using Microsoft.Win32.SafeHandles;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ColorVision.Solution.Terminal
{
    /// <summary>
    /// Wraps the Windows Pseudo Console (ConPTY) API so that child processes
    /// see a real TTY on stdin/stdout (isatty() == true).
    /// Requires Windows 10 1809+.
    /// </summary>
    internal sealed class ConPtyTerminal : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ConPtyTerminal));

        private IntPtr _hPC;
        private SafeFileHandle? _pipeInWrite;
        private SafeFileHandle? _pipeOutRead;
        private IntPtr _hProcess;
        private IntPtr _hThread;
        private Thread? _readThread;
        private FileStream? _writerStream;
        private bool _disposed;

        public event Action<string>? OutputReceived;
        public event Action<int>? ProcessExited;

        public bool IsRunning =>
            _hProcess != IntPtr.Zero &&
            WaitForSingleObject(_hProcess, 0) != WAIT_OBJECT_0;

        public void Start(string commandLine, string workingDirectory,
                          short cols = 120, short rows = 30)
        {
            // 1. Create two anonymous pipe pairs
            var sa = new SECURITY_ATTRIBUTES
            {
                nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
                bInheritHandle = true
            };

            if (!CreatePipe(out var inRead, out var inWrite, ref sa, 0))
                throw new InvalidOperationException(
                    $"CreatePipe(in) failed: {Marshal.GetLastWin32Error()}");
            if (!CreatePipe(out var outRead, out var outWrite, ref sa, 0))
                throw new InvalidOperationException(
                    $"CreatePipe(out) failed: {Marshal.GetLastWin32Error()}");

            _pipeInWrite = inWrite;
            _pipeOutRead = outRead;

            // 2. Create the pseudo console
            var size = new COORD { X = cols, Y = rows };
            int hr = CreatePseudoConsole(size, inRead, outWrite, 0, out _hPC);
            if (hr != 0)
                throw new InvalidOperationException(
                    $"CreatePseudoConsole failed: 0x{hr:X8}");

            // These sides are now owned by the pseudo console
            inRead.Dispose();
            outWrite.Dispose();

            // 3. Create the child process attached to the pseudo console
            var attrSize = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attrSize);

            var attrList = Marshal.AllocHGlobal(attrSize.ToInt32());
            try
            {
                if (!InitializeProcThreadAttributeList(attrList, 1, 0, ref attrSize))
                    throw new InvalidOperationException(
                        $"InitializeProcThreadAttributeList: {Marshal.GetLastWin32Error()}");

                if (!UpdateProcThreadAttribute(
                        attrList, 0,
                        PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                        _hPC, (IntPtr)IntPtr.Size,
                        IntPtr.Zero, IntPtr.Zero))
                    throw new InvalidOperationException(
                        $"UpdateProcThreadAttribute: {Marshal.GetLastWin32Error()}");

                var si = new STARTUPINFOEX
                {
                    StartupInfo = new STARTUPINFO
                    {
                        cb = Marshal.SizeOf<STARTUPINFOEX>()
                    },
                    lpAttributeList = attrList
                };

                if (!CreateProcessW(
                        null, commandLine,
                        IntPtr.Zero, IntPtr.Zero, false,
                        EXTENDED_STARTUPINFO_PRESENT,
                        IntPtr.Zero, workingDirectory,
                        ref si, out var pi))
                    throw new InvalidOperationException(
                        $"CreateProcess failed: {Marshal.GetLastWin32Error()}");

                _hProcess = pi.hProcess;
                _hThread = pi.hThread;
            }
            finally
            {
                DeleteProcThreadAttributeList(attrList);
                Marshal.FreeHGlobal(attrList);
            }

            // 4. Open the writer stream and start the reader thread
            _writerStream = new FileStream(_pipeInWrite, FileAccess.Write);
            _readThread = new Thread(ReadLoop)
            {
                IsBackground = true,
                Name = "ConPTY-Read"
            };
            _readThread.Start();
        }

        /// <summary>Write raw text (including control chars) to the terminal.</summary>
        public void Write(string text)
        {
            if (_writerStream == null || _disposed) return;
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                _writerStream.Write(bytes, 0, bytes.Length);
                _writerStream.Flush();
            }
            catch (Exception ex)
            {
                log.Debug($"ConPTY write failed: {ex.Message}");
            }
        }

        public void Resize(short cols, short rows)
        {
            if (_hPC != IntPtr.Zero)
                ResizePseudoConsole(_hPC, new COORD { X = cols, Y = rows });
        }

        public void Kill()
        {
            if (_hProcess != IntPtr.Zero && IsRunning)
                TerminateProcess(_hProcess, 1);
        }

        private void ReadLoop()
        {
            var buf = new byte[4096];
            var charBuf = new char[4096];
            var decoder = Encoding.UTF8.GetDecoder();

            try
            {
                using var stream = new FileStream(_pipeOutRead!, FileAccess.Read, 4096);
                while (true)
                {
                    int n = stream.Read(buf, 0, buf.Length);
                    if (n <= 0) break;
                    int chars = decoder.GetChars(buf, 0, n, charBuf, 0, false);
                    OutputReceived?.Invoke(new string(charBuf, 0, chars));
                }
            }
            catch (Exception ex) when (!_disposed)
            {
                log.Debug($"ConPTY read ended: {ex.Message}");
            }

            int exitCode = -1;
            if (_hProcess != IntPtr.Zero)
            {
                WaitForSingleObject(_hProcess, 5000);
                GetExitCodeProcess(_hProcess, out exitCode);
            }
            ProcessExited?.Invoke(exitCode);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try { _writerStream?.Dispose(); } catch { }
            _writerStream = null;

            if (_hPC != IntPtr.Zero)
            {
                ClosePseudoConsole(_hPC);
                _hPC = IntPtr.Zero;
            }

            _readThread?.Join(3000);

            try { _pipeInWrite?.Dispose(); } catch { }
            try { _pipeOutRead?.Dispose(); } catch { }

            if (_hThread != IntPtr.Zero)
            {
                CloseHandle(_hThread);
                _hThread = IntPtr.Zero;
            }
            if (_hProcess != IntPtr.Zero)
            {
                CloseHandle(_hProcess);
                _hProcess = IntPtr.Zero;
            }
        }

        #region Win32 P/Invoke

        private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        private const uint WAIT_OBJECT_0 = 0;
        private static readonly IntPtr PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE =
            (IntPtr)0x00020016;

        [StructLayout(LayoutKind.Sequential)]
        private struct COORD { public short X; public short Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            [MarshalAs(UnmanagedType.Bool)] public bool bInheritHandle;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFO
        {
            public int cb;
            public IntPtr lpReserved, lpDesktop, lpTitle;
            public int dwX, dwY, dwXSize, dwYSize;
            public int dwXCountChars, dwYCountChars;
            public int dwFillAttribute, dwFlags;
            public short wShowWindow, cbReserved2;
            public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFOEX
        {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess, hThread;
            public uint dwProcessId, dwThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CreatePipe(
            out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe,
            ref SECURITY_ATTRIBUTES lpPipeAttributes, int nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int CreatePseudoConsole(
            COORD size, SafeFileHandle hInput, SafeFileHandle hOutput,
            uint dwFlags, out IntPtr phPC);

        [DllImport("kernel32.dll")]
        private static extern void ClosePseudoConsole(IntPtr hPC);

        [DllImport("kernel32.dll")]
        private static extern int ResizePseudoConsole(IntPtr hPC, COORD size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool InitializeProcThreadAttributeList(
            IntPtr lpAttributeList, int dwAttributeCount,
            int dwFlags, ref IntPtr lpSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UpdateProcThreadAttribute(
            IntPtr lpAttributeList, uint dwFlags, IntPtr attribute,
            IntPtr lpValue, IntPtr cbSize,
            IntPtr lpPreviousValue, IntPtr lpReturnSize);

        [DllImport("kernel32.dll")]
        private static extern bool DeleteProcThreadAttributeList(IntPtr lpAttributeList);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcessW(
            string? lpApplicationName, string lpCommandLine,
            IntPtr lpProcessAttributes, IntPtr lpThreadAttributes,
            bool bInheritHandles, uint dwCreationFlags,
            IntPtr lpEnvironment, string lpCurrentDirectory,
            ref STARTUPINFOEX lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll")]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll")]
        private static extern bool GetExitCodeProcess(IntPtr hProcess, out int lpExitCode);

        [DllImport("kernel32.dll")]
        private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        #endregion
    }
}
