using log4net;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace ColorVision.Update
{
    public enum SingleInstanceCloseRequestResult
    {
        Accepted,
        Rejected,
        Unavailable,
        Indeterminate,
        TimedOut,
    }

    /// <summary>
    /// Closes every running application process that belongs to the current installation
    /// before an external updater starts replacing files.
    /// </summary>
    public static partial class ApplicationUpdateProcessCoordinator
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ApplicationUpdateProcessCoordinator));
        private static readonly TimeSpan DefaultGracefulShutdownTimeout = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan DefaultForcedShutdownTimeout = TimeSpan.FromSeconds(5);
        private const string ReplacementSignalPrefix = @"Local\ColorVision.SingleInstanceReplacement.";
        private const uint ProcessQueryLimitedInformation = 0x1000;
        private const int MaximumExecutablePathLength = 32768;

        public static int CloseOtherApplicationProcesses()
        {
            try
            {
                return Environment.ProcessPath is string executablePath
                    ? CloseOtherApplicationProcesses(executablePath, Environment.ProcessId, DefaultForcedShutdownTimeout)
                    : 0;
            }
            catch (Exception ex)
            {
                log.Warn("Unable to coordinate running ColorVision processes; continuing the update.", ex);
                return 0;
            }
        }

        public static int CloseEarlierApplicationProcesses(
            Func<int, SingleInstanceCloseRequestResult> requestClose) =>
            CloseEarlierApplicationProcesses(requestClose, confirmTermination: null);

        // Retain the original public entry point for callers that only allow graceful replacement.
        public static int CloseEarlierApplicationProcesses(
            Func<int, SingleInstanceCloseRequestResult> requestClose,
            Func<int, bool>? confirmTermination)
        {
            ArgumentNullException.ThrowIfNull(requestClose);
            string executablePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("Unable to resolve the current ColorVision executable path.");
            using Process currentProcess = Process.GetCurrentProcess();
            return CloseEarlierApplicationProcesses(
                executablePath,
                currentProcess.Id,
                currentProcess.SessionId,
                currentProcess.StartTime.ToUniversalTime(),
                DefaultGracefulShutdownTimeout,
                requestClose,
                confirmTermination);
        }

        internal static int CloseOtherApplicationProcesses(
            string executablePath,
            int currentProcessId,
            TimeSpan forcedShutdownTimeout)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
            ArgumentOutOfRangeException.ThrowIfLessThan(forcedShutdownTimeout, TimeSpan.Zero);

            string normalizedExecutablePath = Path.GetFullPath(executablePath);
            string processName = Path.GetFileNameWithoutExtension(normalizedExecutablePath);
            if (string.IsNullOrWhiteSpace(processName))
                throw new InvalidOperationException("Unable to resolve the current ColorVision process name.");

            var targetProcesses = new List<Process>();
            foreach (Process process in Process.GetProcessesByName(processName))
            {
                bool keepProcess = false;
                try
                {
                    if (process.Id == currentProcessId || process.HasExited)
                        continue;

                    if (!TryGetExecutablePath(process, out string candidateExecutablePath))
                        continue;

                    if (!string.Equals(
                        Path.GetFullPath(candidateExecutablePath),
                        normalizedExecutablePath,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    targetProcesses.Add(process);
                    keepProcess = true;
                }
                catch (InvalidOperationException)
                {
                    // The process exited while it was being inspected.
                }
                catch (ArgumentException)
                {
                    // The process exited while it was being inspected.
                }
                catch (Win32Exception ex)
                {
                    if (IsRunning(process))
                        log.Warn($"Unable to inspect ColorVision process {process.Id}: {ex.Message}");
                }
                finally
                {
                    if (!keepProcess)
                        process.Dispose();
                }
            }

            try
            {
                if (targetProcesses.Count == 0)
                    return 0;

                log.Info(
                    $"Terminating {targetProcesses.Count} existing ColorVision process(es) from '{normalizedExecutablePath}': " +
                    string.Join(", ", targetProcesses.Select(process => process.Id)));

                foreach (Process process in targetProcesses)
                {
                    try
                    {
                        if (!process.HasExited)
                            process.Kill(entireProcessTree: true);
                    }
                    catch (InvalidOperationException)
                    {
                    }
                    catch (Win32Exception ex)
                    {
                        log.Warn($"Unable to terminate ColorVision process {process.Id}: {ex.Message}");
                    }
                }

                List<Process> remainingProcesses = WaitForExit(targetProcesses, forcedShutdownTimeout);
                if (remainingProcesses.Count > 0)
                {
                    log.Warn(
                        $"ColorVision process(es) {string.Join(", ", remainingProcesses.Select(process => process.Id))} remain running; continuing the update.");
                }

                return targetProcesses.Count - remainingProcesses.Count;
            }
            finally
            {
                DisposeProcesses(targetProcesses);
            }
        }

        internal static int CloseEarlierApplicationProcesses(
            string executablePath,
            int currentProcessId,
            int currentProcessSessionId,
            DateTime currentProcessStartTimeUtc,
            TimeSpan gracefulShutdownTimeout,
            Func<int, SingleInstanceCloseRequestResult> requestClose,
            Func<int, bool>? confirmTermination = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
            ArgumentOutOfRangeException.ThrowIfLessThan(gracefulShutdownTimeout, TimeSpan.Zero);
            ArgumentNullException.ThrowIfNull(requestClose);

            string normalizedExecutablePath = Path.GetFullPath(executablePath);
            var targetProcesses = FindEarlierApplicationProcesses(
                normalizedExecutablePath, currentProcessId, currentProcessSessionId, currentProcessStartTimeUtc);

            try
            {
                if (targetProcesses.Count == 0)
                    return 0;

                foreach (Process process in targetProcesses)
                {
                    if (!IsRunning(process))
                        continue;
                    using var replacementSignal = new EventWaitHandle(false, EventResetMode.ManualReset, CreateReplacementSignalName(process.Id));
                    log.Info($"Requesting replacement shutdown from earlier ColorVision process {process.Id}.");
                    SingleInstanceCloseRequestResult closeResult = requestClose(process.Id);
                    log.Info($"Earlier ColorVision process {process.Id} shutdown response: {closeResult}.");
                    if (closeResult == SingleInstanceCloseRequestResult.Rejected)
                        throw new InvalidOperationException($"Earlier ColorVision process {process.Id} declined the shutdown request.");

                    if (closeResult == SingleInstanceCloseRequestResult.Unavailable && !RequestWindowClose(process) && IsRunning(process))
                    {
                        RecoverUnresponsiveProcess(process, confirmTermination, $"Earlier ColorVision process {process.Id} has no safe close endpoint.");
                        continue;
                    }

                    if (closeResult == SingleInstanceCloseRequestResult.TimedOut)
                    {
                        RecoverUnresponsiveProcess(process, confirmTermination, $"Earlier ColorVision process {process.Id} did not answer the shutdown request.");
                        continue;
                    }

                    if (WaitForExit([process], gracefulShutdownTimeout).Count > 0)
                        RecoverUnresponsiveProcess(process, confirmTermination, $"Earlier ColorVision process {process.Id} did not exit after the safe shutdown request.");
                }

                log.Info($"Closed {targetProcesses.Count} earlier ColorVision process(es) from '{normalizedExecutablePath}'.");
                return targetProcesses.Count;
            }
            finally
            {
                DisposeProcesses(targetProcesses);
            }
        }

        private static List<Process> FindEarlierApplicationProcesses(
            string normalizedExecutablePath, int currentProcessId, int currentProcessSessionId, DateTime currentProcessStartTimeUtc)
        {
            string processName = Path.GetFileNameWithoutExtension(normalizedExecutablePath);
            if (string.IsNullOrWhiteSpace(processName))
                throw new InvalidOperationException("Unable to resolve the current ColorVision process name.");

            var targetProcesses = new List<Process>();
            var unresolvedProcessIds = new List<int>();
            foreach (Process process in Process.GetProcessesByName(processName))
            {
                bool keepProcess = false;
                try
                {
                    if (process.Id == currentProcessId
                        || process.HasExited
                        || process.SessionId != currentProcessSessionId)
                    {
                        continue;
                    }

                    if (!TryGetExecutablePath(process, out string candidateExecutablePath))
                    {
                        if (IsRunning(process))
                            unresolvedProcessIds.Add(process.Id);
                        continue;
                    }

                    if (!string.Equals(
                        Path.GetFullPath(candidateExecutablePath),
                        normalizedExecutablePath,
                        StringComparison.OrdinalIgnoreCase)
                        || !IsEarlierProcess(process, currentProcessStartTimeUtc, currentProcessId))
                    {
                        continue;
                    }

                    targetProcesses.Add(process);
                    keepProcess = true;
                }
                catch (InvalidOperationException)
                {
                }
                catch (ArgumentException)
                {
                }
                catch (Win32Exception ex)
                {
                    if (IsRunning(process))
                    {
                        unresolvedProcessIds.Add(process.Id);
                        log.Warn($"Unable to inspect ColorVision process {process.Id}: {ex.Message}");
                    }
                }
                finally
                {
                    if (!keepProcess)
                        process.Dispose();
                }
            }

            if (unresolvedProcessIds.Count > 0)
            {
                DisposeProcesses(targetProcesses);
                throw new InvalidOperationException(
                    $"Unable to verify earlier ColorVision process(es): {string.Join(", ", unresolvedProcessIds)}. Close them manually and retry.");
            }

            targetProcesses.Sort(CompareProcessStartOrder);
            return targetProcesses;
        }

        private static bool RequestWindowClose(Process process)
        {
            try { return process.HasExited || process.CloseMainWindow(); }
            catch (InvalidOperationException) { return true; }
        }

        private static void RecoverUnresponsiveProcess(Process process, Func<int, bool>? confirmTermination, string failureMessage)
        {
            if (!IsRunning(process))
                return;
            if (confirmTermination == null)
                throw new InvalidOperationException(failureMessage);

            if (!PinEarlierProcess(process))
                return;
            if (!confirmTermination(process.Id))
                throw new OperationCanceledException($"Recovery of earlier ColorVision process {process.Id} was canceled.");

            try
            {
                if (!process.HasExited)
                {
                    log.Warn($"User confirmed termination of earlier ColorVision process {process.Id}. {failureMessage}");
                    process.Kill();
                }
            }
            catch (InvalidOperationException) when (!IsRunning(process))
            {
            }

            if (WaitForExit([process], DefaultForcedShutdownTimeout).Count > 0)
                throw new InvalidOperationException($"Earlier ColorVision process {process.Id} did not exit after confirmed termination.");
        }

        private static bool PinEarlierProcess(Process process)
        {
            // StartTime was captured during discovery. Pin that process before checking PID reuse.
            DateTime expectedStartTimeUtc = process.StartTime.ToUniversalTime();
            try
            {
                _ = process.SafeHandle;
                if (process.HasExited)
                    return false;
                using Process identity = Process.GetProcessById(process.Id);
                if (identity.StartTime.ToUniversalTime() != expectedStartTimeUtc)
                    throw new InvalidOperationException($"Earlier ColorVision process {process.Id} changed identity before recovery.");
            }
            catch (Exception ex) when ((ex is InvalidOperationException or ArgumentException) && !IsRunning(process))
            {
                return false;
            }
            return true;
        }

        public static bool IsSingleInstanceReplacementRequested(int processId)
        {
            try
            {
                if (!EventWaitHandle.TryOpenExisting(
                    CreateReplacementSignalName(processId),
                    out EventWaitHandle? signal))
                {
                    return false;
                }

                signal.Dispose();
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static string CreateReplacementSignalName(int processId) =>
            ReplacementSignalPrefix + processId;

        private static bool IsEarlierProcess(
            Process process,
            DateTime currentProcessStartTimeUtc,
            int currentProcessId)
        {
            int startTimeComparison = DateTime.Compare(
                process.StartTime.ToUniversalTime(),
                currentProcessStartTimeUtc);
            return startTimeComparison < 0
                || (startTimeComparison == 0 && process.Id < currentProcessId);
        }

        private static int CompareProcessStartOrder(Process left, Process right)
        {
            try
            {
                int startTimeComparison = DateTime.Compare(
                    left.StartTime.ToUniversalTime(),
                    right.StartTime.ToUniversalTime());
                if (startTimeComparison != 0)
                    return startTimeComparison;
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
            }

            return left.Id.CompareTo(right.Id);
        }

        private static bool TryGetExecutablePath(Process process, out string executablePath)
        {
            executablePath = string.Empty;
            try
            {
                string? mainModulePath = process.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(mainModulePath))
                {
                    executablePath = mainModulePath;
                    return true;
                }
            }
            catch (Win32Exception)
            {
                // QueryFullProcessImageName only needs limited query access and also works
                // when MainModule cannot be read across integrity levels.
            }

            using SafeProcessHandle processHandle = OpenProcess(
                ProcessQueryLimitedInformation,
                inheritHandle: false,
                process.Id);
            if (processHandle.IsInvalid)
                return false;

            var pathBuffer = new char[MaximumExecutablePathLength];
            int pathLength = pathBuffer.Length;
            if (!QueryFullProcessImageName(processHandle, flags: 0, pathBuffer, ref pathLength))
                return false;

            executablePath = new string(pathBuffer, 0, pathLength);
            return !string.IsNullOrWhiteSpace(executablePath);
        }

        private static List<Process> WaitForExit(IReadOnlyList<Process> processes, TimeSpan timeout)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            List<Process> remainingProcesses = GetRunningProcesses(processes);
            while (remainingProcesses.Count > 0 && stopwatch.Elapsed < timeout)
            {
                Thread.Sleep(100);
                remainingProcesses = GetRunningProcesses(remainingProcesses);
            }
            return remainingProcesses;
        }

        private static List<Process> GetRunningProcesses(IEnumerable<Process> processes)
        {
            var runningProcesses = new List<Process>();
            foreach (Process process in processes)
            {
                if (IsRunning(process))
                    runningProcesses.Add(process);
            }
            return runningProcesses;
        }

        private static bool IsRunning(Process process)
        {
            try
            {
                return !process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (Win32Exception)
            {
                // If exit state cannot be queried because access is denied, keep treating
                // the process as running so the update cannot replace locked files.
                return true;
            }
        }

        private static void DisposeProcesses(IEnumerable<Process> processes)
        {
            foreach (Process process in processes)
                process.Dispose();
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeProcessHandle OpenProcess(
            uint desiredAccess,
            bool inheritHandle,
            int processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool QueryFullProcessImageName(
            SafeProcessHandle processHandle,
            uint flags,
            [Out] char[] executablePath,
            ref int pathLength);
    }
}
