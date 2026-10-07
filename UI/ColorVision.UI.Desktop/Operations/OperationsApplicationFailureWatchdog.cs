using System.Diagnostics;
using System.IO;

namespace ColorVision.UI.Desktop.Operations;

public static class OperationsApplicationFailureWatchdog
{
    private static readonly FailureWatchdogSession Session = new(CreateCleanExitEvent, StartCore);
    private static int _fatalFailureObserved;
    public static bool Active => Session.Active;

    // Retained for external hosts. The ColorVision UI uses the asynchronous entry point.
    public static bool TryStart() => TryStartAsync().GetAwaiter().GetResult();
    public static Task<bool> TryStartAsync() => Session.StartAsync();

    private static EventWaitHandle CreateCleanExitEvent()
    {
        using Process process = Process.GetCurrentProcess();
        string name = OperationsFailureWatchdogProtocol.CreateCleanExitEventName(
            Environment.ProcessId, process.StartTime.ToUniversalTime().Ticks);
        return new EventWaitHandle(false, EventResetMode.ManualReset, name);
    }

    private static bool StartCore(EventWaitHandle cleanExit)
    {
        if (!string.Equals(Path.GetFileName(Environment.ProcessPath),
            OperationsFailureWatchdogProtocol.TargetExecutableName, StringComparison.OrdinalIgnoreCase))
            return false;
        string watchdogPath = Path.Combine(AppContext.BaseDirectory,
            OperationsFailureWatchdogProtocol.WatchdogDirectoryName,
            OperationsFailureWatchdogProtocol.WatchdogExecutableName);
        if (!File.Exists(watchdogPath))
            return false;

        using Process currentProcess = Process.GetCurrentProcess();
        string readyName = OperationsFailureWatchdogProtocol.CreateReadyEventName(
            Environment.ProcessId, currentProcess.StartTime.ToUniversalTime().Ticks);
        using EventWaitHandle ready = new(false, EventResetMode.ManualReset, readyName);
        if (cleanExit.WaitOne(0))
            return false;
        ProcessStartInfo startInfo = new(watchdogPath)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(watchdogPath)!,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        startInfo.ArgumentList.Add(OperationsFailureWatchdogProtocol.WatchProcessArgument);
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        using Process? watchdogProcess = Process.Start(startInfo);
        return watchdogProcess != null
            && WaitHandle.WaitAny([ready, cleanExit], TimeSpan.FromSeconds(5)) == 0;
    }

    public static void MarkFatalFailureObserved() => Volatile.Write(ref _fatalFailureObserved, 1);
    public static void SignalCleanExit()
    {
        if (Volatile.Read(ref _fatalFailureObserved) == 0)
            Session.SignalCleanExit();
    }
}

/// <summary>Single-flight handshake with a nonblocking exit path, including exit before readiness.</summary>
internal sealed class FailureWatchdogSession(Func<EventWaitHandle> createCleanExit, Func<EventWaitHandle, bool> start)
{
    private readonly object sync = new();
    private EventWaitHandle? cleanExit;
    private Task<bool>? startup;
    private bool exiting;
    private bool active;
    public bool Active { get { lock (sync) return active; } }

    public Task<bool> StartAsync()
    {
        lock (sync)
            return exiting ? Task.FromResult(false) : startup ??= Task.Run(Start);
    }

    private bool Start()
    {
        try
        {
            EventWaitHandle signal;
            lock (sync)
            {
                if (exiting) return false;
                signal = cleanExit = createCleanExit();
            }
            bool ready = start(signal);
            lock (sync)
            {
                active = ready && !exiting;
                if (active) return true;
            }
        }
        catch { /* The host retains Windows restart registration as fallback. */ }
        lock (sync)
        {
            cleanExit?.Set();
            cleanExit?.Dispose();
            cleanExit = null;
            return false;
        }
    }

    public void SignalCleanExit()
    {
        lock (sync)
        {
            exiting = true;
            active = false;
            cleanExit?.Set();
        }
    }
}
