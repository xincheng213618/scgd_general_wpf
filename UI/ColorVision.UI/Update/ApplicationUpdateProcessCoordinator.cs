using log4net;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace ColorVision.Update
{
    /// <summary>
    /// Closes every running application process that belongs to the current installation
    /// before an external updater starts replacing files.
    /// </summary>
    public static class ApplicationUpdateProcessCoordinator
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ApplicationUpdateProcessCoordinator));
        private static readonly TimeSpan DefaultGracefulShutdownTimeout = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan DefaultForcedShutdownTimeout = TimeSpan.FromSeconds(5);
        private const uint ProcessQueryLimitedInformation = 0x1000;
        private const int MaximumExecutablePathLength = 32768;

        public static int CloseOtherApplicationProcesses()
        {
            string executablePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("Unable to resolve the current ColorVision executable path.");
            return CloseOtherApplicationProcesses(
                executablePath,
                Environment.ProcessId,
                DefaultGracefulShutdownTimeout,
                DefaultForcedShutdownTimeout);
        }

        internal static int CloseOtherApplicationProcesses(
            string executablePath,
            int currentProcessId,
            TimeSpan gracefulShutdownTimeout,
            TimeSpan forcedShutdownTimeout)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
            ArgumentOutOfRangeException.ThrowIfLessThan(gracefulShutdownTimeout, TimeSpan.Zero);
            ArgumentOutOfRangeException.ThrowIfLessThan(forcedShutdownTimeout, TimeSpan.Zero);

            string normalizedExecutablePath = Path.GetFullPath(executablePath);
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
                    if (process.Id == currentProcessId || process.HasExited)
                        continue;

                    if (!TryGetExecutablePath(process, out string candidateExecutablePath))
                    {
                        if (IsRunning(process))
                            unresolvedProcessIds.Add(process.Id);
                        continue;
                    }

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
                    $"Unable to verify running ColorVision process(es): {string.Join(", ", unresolvedProcessIds)}. Close them manually and retry the update.");
            }

            try
            {
                if (targetProcesses.Count == 0)
                    return 0;

                log.Info(
                    $"Closing {targetProcesses.Count} existing ColorVision process(es) from '{normalizedExecutablePath}': " +
                    string.Join(", ", targetProcesses.Select(process => process.Id)));

                foreach (Process process in targetProcesses)
                {
                    try
                    {
                        if (!process.HasExited && !process.CloseMainWindow())
                            log.Info($"ColorVision process {process.Id} has no closeable main window; it will be stopped after the graceful timeout.");
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }

                List<Process> remainingProcesses = WaitForExit(targetProcesses, gracefulShutdownTimeout);
                foreach (Process process in remainingProcesses)
                {
                    try
                    {
                        log.Warn($"ColorVision process {process.Id} did not exit gracefully; forcing termination.");
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

                remainingProcesses = WaitForExit(remainingProcesses, forcedShutdownTimeout);
                if (remainingProcesses.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"Unable to close running ColorVision process(es): {string.Join(", ", remainingProcesses.Select(process => process.Id))}.");
                }

                log.Info("All existing ColorVision processes from the current installation have exited.");
                return targetProcesses.Count;
            }
            finally
            {
                DisposeProcesses(targetProcesses);
            }
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
