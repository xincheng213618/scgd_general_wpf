#pragma warning disable CA1806
using log4net;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ColorVision.Solution.Terminal
{
    /// <summary>Hosts a shell in a Windows pseudoconsole and owns its process tree.</summary>
    internal sealed class ConPtyTerminal : IDisposable
    {
        private const int PipeBufferSize = 16 * 1024;
        private static readonly TimeSpan ProcessExitGrace = TimeSpan.FromSeconds(1);
        private static readonly ILog log = LogManager.GetLogger(typeof(ConPtyTerminal));

        private readonly object _gate = new();
        private readonly Queue<string> _input = new();
        private readonly SemaphoreSlim _geometryChanged = new(0, 1);
        private readonly TaskCompletionSource _stopRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _transportEnded = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _started;
        private volatile bool _disposed;
        private volatile bool _stopping;
        private COORD? _pendingSize;

        public event Action<string>? OutputReceived;
        public event Action<int>? ProcessExited;

        public Task StartAsync(string commandLine, string workingDirectory, short cols = 120, short rows = 30)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cols);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_started)
                    throw new InvalidOperationException("A ConPTY session can only be started once.");
                _started = true;
            }

            var startup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = Task.Run(() => RunSessionAsync(commandLine, workingDirectory, new COORD { X = cols, Y = rows }, startup));
            return startup.Task;
        }

        public bool Write(string text)
        {
            lock (_gate)
            {
                if (!_started || _stopping || text.Length == 0)
                    return false;

                // Anonymous ConPTY pipes are synchronous. The UI only enqueues input.
                _input.Enqueue(text);
                Monitor.Pulse(_gate);
                return true;
            }
        }

        public void Resize(short cols, short rows)
        {
            lock (_gate)
            {
                if (!_started || _stopping || cols <= 0 || rows <= 0)
                    return;

                _pendingSize = new COORD { X = cols, Y = rows };
                SignalGeometryChange();
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;

                _disposed = true;
                RequestStop();
            }
        }

        private void SignalGeometryChange()
        {
            // Callers serialize producers via _gate. Multiple resizes merge into the latest size.
            if (_geometryChanged.CurrentCount == 0)
                _geometryChanged.Release();
        }

        private async Task RunSessionAsync(string commandLine, string workingDirectory, COORD size, TaskCompletionSource startup)
        {
            IntPtr console = IntPtr.Zero;
            SafeFileHandle? input = null;
            SafeFileHandle? output = null;
            SafeWaitHandle? job = null;
            ShellProcess? process = null;
            RegisteredWaitHandle? exitWatch = null;
            Task? reader = null;
            Task? writer = null;
            Task? resize = null;
            int? exitCode = null;

            try
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                (console, input, output) = CreateConsole(size);
                reader = StartPipeWorker(() => ReadOutput(output));
                job = CreateKillOnCloseJob();
                ObjectDisposedException.ThrowIf(_disposed, this);
                process = StartProcess(commandLine, workingDirectory, console, job);
                exitWatch = ThreadPool.RegisterWaitForSingleObject(process, static (state, _) =>
                {
                    var terminal = (ConPtyTerminal)state!;
                    lock (terminal._gate)
                        terminal.RequestStop();
                }, this, Timeout.Infinite, executeOnlyOnce: true);
                if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100))
                {
                    // Let the pipes close when the last console client disconnects. The
                    // HPCON still belongs to this worker and is freed during teardown.
                    int hr = ReleasePseudoConsole(console);
                    if (hr != 0)
                        log.Debug($"ReleasePseudoConsole failed: 0x{hr:X8}");
                }

                writer = StartPipeWorker(() => WriteInput(input));
                resize = Task.Run(() => ApplyResizesAsync(console, size));
                if (_disposed)
                    startup.TrySetCanceled();
                else
                    startup.TrySetResult();

                Task completed = await Task.WhenAny(_stopRequested.Task, _transportEnded.Task).ConfigureAwait(false);
                if (completed == _transportEnded.Task)
                {
                    // Windows can close the pipes before the child signals exit. Give an
                    // exiting process a chance to report its code, or retire a detached one.
                    await Task.WhenAny(_stopRequested.Task, Task.Delay(ProcessExitGrace)).ConfigureAwait(false);
                }

                if (!_disposed)
                    exitCode = process.WaitOne(0) && GetExitCodeProcess(process.SafeWaitHandle, out int code) ? code : -1;
            }
            catch (Exception ex)
            {
                if (_disposed)
                    startup.TrySetCanceled();
                else if (!startup.TrySetException(ex))
                {
                    log.Warn("ConPTY session failed", ex);
                    exitCode = -1;
                }
            }
            finally
            {
                lock (_gate)
                    RequestStop();
                exitWatch?.Unregister(null);
                process?.Dispose();
                // Start terminating the job even if a resize is stuck writing to ConPTY's
                // signal pipe. Keep the HPCON alive until that native call has returned.
                job?.Dispose();
                if (resize != null)
                    await resize.ConfigureAwait(false);

                if (reader == null)
                    output?.Dispose();

                // Older Windows versions wait for ConPTY's final output here. Keep the reader
                // running independently, including when the UI has already detached the session.
                if (console != IntPtr.Zero)
                    ClosePseudoConsole(console);

                if (reader != null)
                    await reader.ConfigureAwait(false);
                if (writer != null)
                    await writer.ConfigureAwait(false);

                input?.Dispose();
                output?.Dispose();
            }

            if (!_disposed && exitCode is int finalCode)
                ProcessExited?.Invoke(finalCode);
        }

        private async Task ApplyResizesAsync(IntPtr console, COORD current)
        {
            try
            {
                while (!_stopping)
                {
                    await _geometryChanged.WaitAsync().ConfigureAwait(false);
                    COORD? nextSize;
                    lock (_gate)
                    {
                        if (_stopping)
                            break;

                        nextSize = _pendingSize;
                        _pendingSize = null;
                    }

                    if (nextSize is COORD requested && (requested.X != current.X || requested.Y != current.Y))
                    {
                        int hr = ResizePseudoConsole(console, requested);
                        if (hr == 0)
                            current = requested;
                        else
                            log.Debug($"ConPTY resize failed: 0x{hr:X8} ({requested.X}x{requested.Y})");
                    }
                }
            }
            catch (Exception ex)
            {
                log.Warn("ConPTY resize ended", ex);
                ReportTransportEnd();
            }
        }

        // Called while holding _gate.
        private void RequestStop()
        {
            StopInputAndResizes();
            _stopRequested.TrySetResult();
        }

        private void ReportTransportEnd()
        {
            lock (_gate)
            {
                StopInputAndResizes();
                _transportEnded.TrySetResult();
            }
        }

        private void StopInputAndResizes()
        {
            if (_stopping)
                return;

            _stopping = true;
            _input.Clear();
            Monitor.Pulse(_gate);
            SignalGeometryChange();
        }

        private string? TakeInput()
        {
            lock (_gate)
            {
                while (!_stopping && _input.Count == 0)
                    Monitor.Wait(_gate);

                return _stopping ? null : _input.Dequeue();
            }
        }

        private static Task StartPipeWorker(Action work)
        {
            return Task.Factory.StartNew(work, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void ReadOutput(SafeFileHandle output)
        {
            try
            {
                using var stream = new FileStream(output, FileAccess.Read, bufferSize: 1);
                var bytes = new byte[PipeBufferSize];
                var chars = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
                Decoder decoder = Encoding.UTF8.GetDecoder();

                while (true)
                {
                    int read = stream.Read(bytes);
                    int decoded = decoder.GetChars(bytes.AsSpan(0, read), chars, flush: read == 0);
                    if (!_disposed && decoded > 0)
                        OutputReceived?.Invoke(new string(chars, 0, decoded));
                    if (read == 0)
                        break;
                }
            }
            catch (Exception ex)
            {
                if (!_disposed)
                    log.Debug("ConPTY read ended", ex);
            }
            finally
            {
                ReportTransportEnd();
            }
        }

        private void WriteInput(SafeFileHandle input)
        {
            try
            {
                using var stream = new FileStream(input, FileAccess.Write, bufferSize: 1);
                var bytes = new byte[PipeBufferSize];
                Encoder encoder = Encoding.UTF8.GetEncoder();

                while (TakeInput() is string text)
                {
                    ReadOnlySpan<char> remaining = text.AsSpan();
                    while (!remaining.IsEmpty && !_stopping)
                    {
                        encoder.Convert(remaining, bytes, flush: true, out int used, out int encoded, out _);
                        stream.Write(bytes.AsSpan(0, encoded));
                        remaining = remaining[used..];
                    }
                }
            }
            catch (Exception ex)
            {
                if (!_stopping)
                    log.Debug("ConPTY write ended", ex);
            }
            finally
            {
                ReportTransportEnd();
            }
        }

        private static (IntPtr Console, SafeFileHandle Input, SafeFileHandle Output) CreateConsole(COORD size)
        {
            if (!CreatePipe(out SafeFileHandle inputRead, out SafeFileHandle inputWrite, IntPtr.Zero, 0))
                throw NativeError("CreatePipe(input)");

            SafeFileHandle? outputRead = null;
            using (inputRead)
            {
                try
                {
                    if (!CreatePipe(out outputRead, out SafeFileHandle outputWrite, IntPtr.Zero, 0))
                        throw NativeError("CreatePipe(output)");

                    using (outputWrite)
                    {
                        int hr = CreatePseudoConsole(size, inputRead, outputWrite, 0, out IntPtr console);
                        if (hr != 0)
                            throw new InvalidOperationException($"CreatePseudoConsole failed: 0x{hr:X8}");

                        return (console, inputWrite, outputRead);
                    }
                }
                catch
                {
                    inputWrite.Dispose();
                    outputRead?.Dispose();
                    throw;
                }
            }
        }

        private static SafeWaitHandle CreateKillOnCloseJob()
        {
            SafeWaitHandle job = CreateJobObject(IntPtr.Zero, null);
            if (job.IsInvalid)
            {
                Win32Exception error = NativeError("CreateJobObject");
                job.Dispose();
                throw error;
            }

            var jobInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
            {
                BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                {
                    LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE,
                },
            };
            if (SetInformationJobObject(job, JobObjectExtendedLimitInformation, ref jobInfo,
                (uint)Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>()))
                return job;

            Win32Exception failure = NativeError("SetInformationJobObject");
            job.Dispose();
            throw failure;
        }

        private ShellProcess StartProcess(string commandLine, string workingDirectory, IntPtr console, SafeWaitHandle job)
        {
            IntPtr attrSize = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attrSize);
            if (attrSize == IntPtr.Zero)
                throw NativeError("InitializeProcThreadAttributeList");

            IntPtr attrList = Marshal.AllocHGlobal(attrSize);
            try
            {
                if (!InitializeProcThreadAttributeList(attrList, 1, 0, ref attrSize))
                    throw NativeError("InitializeProcThreadAttributeList");

                try
                {
                    if (!UpdateProcThreadAttribute(attrList, 0, PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                        console, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
                        throw NativeError("UpdateProcThreadAttribute");

                    var startupInfo = new STARTUPINFOEX
                    {
                        // Explicit null standard handles let ConPTY supply all three even if the host is redirected.
                        StartupInfo = new STARTUPINFO { cb = Marshal.SizeOf<STARTUPINFOEX>(), dwFlags = STARTF_USESTDHANDLES },
                        lpAttributeList = attrList,
                    };
                    if (!CreateProcessW(null, new StringBuilder(commandLine), IntPtr.Zero, IntPtr.Zero, false,
                        EXTENDED_STARTUPINFO_PRESENT | CREATE_SUSPENDED, IntPtr.Zero, workingDirectory, ref startupInfo,
                        out PROCESS_INFORMATION info))
                        throw NativeError("CreateProcess");

                    var process = new ShellProcess(info.hProcess);
                    using var thread = new SafeWaitHandle(info.hThread, ownsHandle: true);
                    try
                    {
                        if (!AssignProcessToJobObject(job, process.SafeWaitHandle))
                            throw NativeError("AssignProcessToJobObject");
                        ObjectDisposedException.ThrowIf(_disposed, this);
                        if (ResumeThread(thread) == uint.MaxValue)
                            throw NativeError("ResumeThread");

                        return process;
                    }
                    catch
                    {
                        // A shell whose assignment failed is suspended and outside the job.
                        TerminateProcess(process.SafeWaitHandle, 1);
                        process.Dispose();
                        throw;
                    }
                }
                finally
                {
                    DeleteProcThreadAttributeList(attrList);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(attrList);
            }
        }

        private static Win32Exception NativeError(string operation)
        {
            int code = Marshal.GetLastWin32Error();
            return new Win32Exception(code, $"{operation} failed ({code}): {new Win32Exception(code).Message}");
        }

        private sealed class ShellProcess : WaitHandle
        {
            internal ShellProcess(IntPtr handle)
            {
                SafeWaitHandle = new SafeWaitHandle(handle, ownsHandle: true);
            }
        }

        #region Win32 P/Invoke

        private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        private const uint CREATE_SUSPENDED = 0x00000004;
        private const int STARTF_USESTDHANDLES = 0x00000100;
        private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;
        private const int JobObjectExtendedLimitInformation = 9;
        private static readonly IntPtr PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE =
            (IntPtr)0x00020016;

        [StructLayout(LayoutKind.Sequential)]
        private struct COORD { public short X; public short Y; }

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

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CreatePipe(
            out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe,
            IntPtr lpPipeAttributes, int nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int CreatePseudoConsole(
            COORD size, SafeFileHandle hInput, SafeFileHandle hOutput,
            uint dwFlags, out IntPtr phPC);

        [DllImport("kernel32.dll")]
        private static extern void ClosePseudoConsole(IntPtr hPC);

        [DllImport("kernel32.dll")]
        private static extern int ReleasePseudoConsole(IntPtr hPC);

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
        private static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern bool CreateProcessW(
            string? lpApplicationName, StringBuilder lpCommandLine,
            IntPtr lpProcessAttributes, IntPtr lpThreadAttributes,
            bool bInheritHandles, uint dwCreationFlags,
            IntPtr lpEnvironment, string lpCurrentDirectory,
            ref STARTUPINFOEX lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern SafeWaitHandle CreateJobObject(
            IntPtr lpJobAttributes,
            string? lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(
            SafeWaitHandle hJob,
            int jobObjectInfoClass,
            ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION lpJobObjectInfo,
            uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(SafeWaitHandle hJob, SafeWaitHandle hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint ResumeThread(SafeWaitHandle hThread);


        [DllImport("kernel32.dll")]
        private static extern bool GetExitCodeProcess(SafeWaitHandle hProcess, out int lpExitCode);

        [DllImport("kernel32.dll")]
        private static extern bool TerminateProcess(SafeWaitHandle hProcess, uint uExitCode);


        #endregion
    }
}
