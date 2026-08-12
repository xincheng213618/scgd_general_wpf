using ColorVision.UI.Desktop.Operations;
using System.Diagnostics;

namespace ColorVisionOperationsWatchdog;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (!OperationsFailureWatchdogProtocol.TryParseTargetProcessId(args, out int processId))
            return 2;

        string targetPath;
        try
        {
            targetPath = OperationsFailureWatchdogProtocol.ResolveTargetExecutablePath(AppContext.BaseDirectory);
        }
        catch
        {
            return 3;
        }

        try
        {
            using Process targetProcess = Process.GetProcessById(processId);
            string processPath = targetProcess.MainModule?.FileName ?? string.Empty;
            if (!OperationsFailureWatchdogProtocol.IsExpectedTargetExecutable(AppContext.BaseDirectory, processPath))
                return 4;

            DateTimeOffset startedAt = targetProcess.StartTime.ToUniversalTime();
            long startUtcTicks = startedAt.UtcDateTime.Ticks;
            using EventWaitHandle cleanExitEvent = EventWaitHandle.OpenExisting(
                OperationsFailureWatchdogProtocol.CreateCleanExitEventName(processId, startUtcTicks));
            using EventWaitHandle readyEvent = EventWaitHandle.OpenExisting(
                OperationsFailureWatchdogProtocol.CreateReadyEventName(processId, startUtcTicks));
            readyEvent.Set();

            Task cleanExitTask = Task.Run(() => cleanExitEvent.WaitOne());
            Task processExitTask = targetProcess.WaitForExitAsync();
            Task completed = await Task.WhenAny(cleanExitTask, processExitTask).ConfigureAwait(false);
            if (completed == cleanExitTask)
                return 0;

            await processExitTask.ConfigureAwait(false);
            bool cleanExitSignaled = cleanExitEvent.WaitOne(TimeSpan.FromMilliseconds(500));
            if (!OperationsFailureWatchdogProtocol.ShouldRestart(
                    startedAt, DateTimeOffset.UtcNow, cleanExitSignaled))
            {
                return 0;
            }

            await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            if (!File.Exists(targetPath) || IsTargetAlreadyRunning(targetPath))
                return 0;

            ProcessStartInfo startInfo = new(targetPath)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(targetPath)!,
            };
            startInfo.ArgumentList.Add(OperationsFailureWatchdogProtocol.RecoveryRestartArgument);
            using Process? replacement = Process.Start(startInfo);
            return replacement == null ? 6 : 0;
        }
        catch (ArgumentException)
        {
            return 0;
        }
        catch (InvalidOperationException)
        {
            return 5;
        }
        catch (UnauthorizedAccessException)
        {
            return 5;
        }
    }

    private static bool IsTargetAlreadyRunning(string targetPath)
    {
        foreach (Process process in Process.GetProcessesByName("ColorVision"))
        {
            using (process)
            {
                try
                {
                    if (string.Equals(
                            Path.GetFullPath(process.MainModule?.FileName ?? string.Empty),
                            Path.GetFullPath(targetPath),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch (InvalidOperationException)
                {
                }
                catch (System.ComponentModel.Win32Exception)
                {
                }
            }
        }
        return false;
    }
}
