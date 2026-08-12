using System.Diagnostics;
using System.IO;

namespace ColorVision.UI.Desktop.Operations
{
    public static class OperationsApplicationFailureWatchdog
    {
        private static readonly object Sync = new();
        private static EventWaitHandle? _cleanExitEvent;
        private static int _active;
        private static int _fatalFailureObserved;

        public static bool Active => Volatile.Read(ref _active) != 0;

        public static bool TryStart()
        {
            lock (Sync)
            {
                if (Active)
                    return true;
                if (!string.Equals(
                        Path.GetFileName(Environment.ProcessPath),
                        OperationsFailureWatchdogProtocol.TargetExecutableName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                string watchdogPath = Path.Combine(
                    AppContext.BaseDirectory,
                    OperationsFailureWatchdogProtocol.WatchdogDirectoryName,
                    OperationsFailureWatchdogProtocol.WatchdogExecutableName);
                if (!File.Exists(watchdogPath))
                    return false;

                using Process currentProcess = Process.GetCurrentProcess();
                long startUtcTicks = currentProcess.StartTime.ToUniversalTime().Ticks;
                string cleanExitEventName = OperationsFailureWatchdogProtocol.CreateCleanExitEventName(
                    Environment.ProcessId, startUtcTicks);
                string readyEventName = OperationsFailureWatchdogProtocol.CreateReadyEventName(
                    Environment.ProcessId, startUtcTicks);
                EventWaitHandle cleanExitEvent = new(false, EventResetMode.ManualReset, cleanExitEventName);
                using EventWaitHandle readyEvent = new(false, EventResetMode.ManualReset, readyEventName);
                Process? watchdogProcess = null;
                try
                {
                    ProcessStartInfo startInfo = new(watchdogPath)
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WorkingDirectory = Path.GetDirectoryName(watchdogPath)!,
                        WindowStyle = ProcessWindowStyle.Hidden,
                    };
                    startInfo.ArgumentList.Add(OperationsFailureWatchdogProtocol.WatchProcessArgument);
                    startInfo.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    watchdogProcess = Process.Start(startInfo);
                    if (watchdogProcess == null || !readyEvent.WaitOne(TimeSpan.FromSeconds(5)))
                    {
                        cleanExitEvent.Set();
                        cleanExitEvent.Dispose();
                        return false;
                    }

                    _cleanExitEvent = cleanExitEvent;
                    Volatile.Write(ref _active, 1);
                    return true;
                }
                catch
                {
                    cleanExitEvent.Set();
                    cleanExitEvent.Dispose();
                    return false;
                }
                finally
                {
                    watchdogProcess?.Dispose();
                }
            }
        }

        public static void MarkFatalFailureObserved() =>
            Volatile.Write(ref _fatalFailureObserved, 1);

        public static void SignalCleanExit()
        {
            lock (Sync)
            {
                if (Volatile.Read(ref _fatalFailureObserved) != 0)
                    return;
                _cleanExitEvent?.Set();
                Volatile.Write(ref _active, 0);
            }
        }
    }
}
