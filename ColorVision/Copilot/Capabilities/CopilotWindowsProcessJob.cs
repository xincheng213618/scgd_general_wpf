using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    /// <summary>
    /// Places one shell process and its descendants in a disposable Windows Job Object.
    /// Closing the job is a final safety net; callers should terminate it explicitly before
    /// draining redirected streams so background descendants cannot keep pipe handles open.
    /// </summary>
    internal sealed class CopilotWindowsProcessJob : IDisposable
    {
        private const uint JobObjectLimitKillOnJobClose = 0x00002000;
        private SafeFileHandle? _handle;

        private CopilotWindowsProcessJob(SafeFileHandle handle)
        {
            _handle = handle;
        }

        public static CopilotWindowsProcessJob? TryAssign(Process process)
        {
            ArgumentNullException.ThrowIfNull(process);
            if (!OperatingSystem.IsWindows())
                return null;

            SafeFileHandle? handle = null;
            try
            {
                handle = CreateJobObject(IntPtr.Zero, null);
                if (handle.IsInvalid)
                {
                    handle.Dispose();
                    return null;
                }

                var information = new JobObjectExtendedLimitInformation
                {
                    BasicLimitInformation = new JobObjectBasicLimitInformation
                    {
                        LimitFlags = JobObjectLimitKillOnJobClose,
                    },
                };
                if (!SetInformationJobObject(
                    handle,
                    JobObjectInformationClass.ExtendedLimitInformation,
                    ref information,
                    (uint)Marshal.SizeOf<JobObjectExtendedLimitInformation>()))
                {
                    handle.Dispose();
                    return null;
                }

                if (!AssignProcessToJobObject(handle, process.Handle))
                {
                    handle.Dispose();
                    return null;
                }

                return new CopilotWindowsProcessJob(handle);
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or ObjectDisposedException)
            {
                handle?.Dispose();
                return null;
            }
        }

        public bool TryTerminate()
        {
            var handle = _handle;
            if (handle == null || handle.IsClosed || handle.IsInvalid)
                return false;

            try
            {
                return TerminateJobObject(handle, 1);
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        public async Task<bool> TryWaitForExitAsync(TimeSpan timeout)
        {
            var handle = _handle;
            if (handle == null || handle.IsClosed || handle.IsInvalid)
                return false;

            var boundedTimeout = timeout < TimeSpan.Zero ? TimeSpan.Zero : timeout;
            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                if (!TryGetActiveProcessCount(handle, out var activeProcesses))
                    return false;
                if (activeProcesses == 0)
                    return true;
                if (stopwatch.Elapsed >= boundedTimeout)
                    return false;
                await Task.Delay(10).ConfigureAwait(false);
            }
        }

        private static bool TryGetActiveProcessCount(SafeFileHandle handle, out uint activeProcesses)
        {
            activeProcesses = 0;
            try
            {
                if (!QueryInformationJobObject(
                    handle,
                    JobObjectInformationClass.BasicAccountingInformation,
                    out var information,
                    (uint)Marshal.SizeOf<JobObjectBasicAccountingInformation>(),
                    out _))
                {
                    return false;
                }

                activeProcesses = information.ActiveProcesses;
                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        public void Dispose()
        {
            _handle?.Dispose();
            _handle = null;
        }

        private enum JobObjectInformationClass
        {
            BasicAccountingInformation = 1,
            ExtendedLimitInformation = 9,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectBasicAccountingInformation
        {
            public long TotalUserTime;
            public long TotalKernelTime;
            public long ThisPeriodTotalUserTime;
            public long ThisPeriodTotalKernelTime;
            public uint TotalPageFaultCount;
            public uint TotalProcesses;
            public uint ActiveProcesses;
            public uint TotalTerminatedProcesses;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectBasicLimitInformation
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
        private struct IoCounters
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectExtendedLimitInformation
        {
            public JobObjectBasicLimitInformation BasicLimitInformation;
            public IoCounters IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern SafeFileHandle CreateJobObject(IntPtr jobAttributes, string? name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(
            SafeFileHandle job,
            JobObjectInformationClass informationClass,
            ref JobObjectExtendedLimitInformation information,
            uint informationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryInformationJobObject(
            SafeFileHandle job,
            JobObjectInformationClass informationClass,
            out JobObjectBasicAccountingInformation information,
            uint informationLength,
            out uint returnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateJobObject(SafeFileHandle job, uint exitCode);
    }
}
