using System.Diagnostics;
using System.Management;
using Newtonsoft.Json.Linq;

namespace ColorVisionServiceHost;

internal static class ProcessCommandService
{
    public static ServiceHostResponse Terminate(ServiceHostRequest request)
    {
        int processId = request.Data?.Value<int?>("processId") ?? 0;
        long startTimeUtcTicks = request.Data?.Value<long?>("startTimeUtcTicks") ?? 0;
        string executablePath = request.Data?.Value<string>("executablePath") ?? string.Empty;
        long notAfterUtcTicks = request.Data?.Value<long?>("notAfterUtcTicks") ?? 0;
        if (processId <= 0 || startTimeUtcTicks <= 0 || !Path.IsPathFullyQualified(executablePath) || processId == Environment.ProcessId)
            return Failure("process_target_invalid");
        if (!DeadlineIsValid(notAfterUtcTicks))
            return Failure("process_request_expired");

        if (!IsAllowedProcessPath(executablePath))
            return Failure("process_target_not_allowed");

        Process target;
        try { target = Process.GetProcessById(processId); }
        catch (ArgumentException) { return Success(); }
        using (target)
        {
            if (target.HasExited)
                return Success();
            // Pin the inspected object so PID reuse cannot redirect the termination.
            _ = target.SafeHandle;
            if (target.StartTime.ToUniversalTime().Ticks != startTimeUtcTicks
                || !string.Equals(target.MainModule?.FileName, Path.GetFullPath(executablePath), StringComparison.OrdinalIgnoreCase))
                return Failure("process_identity_mismatch");
            // A timed-out client must not leave a queued termination that can execute much later.
            if (!DeadlineIsValid(notAfterUtcTicks))
                return Failure("process_request_expired");
            try
            {
                return TerminateProcess(target, entireProcessTree: false, timeoutMilliseconds: 5000) ? Success() : Failure("process_still_running");
            }
            catch (Exception) when (target.HasExited) { return Success(); }
        }

        ServiceHostResponse Failure(string message) => ServiceHostResponse.FromObject(request.RequestId, false, message, new { processId });
        ServiceHostResponse Success() => ServiceHostResponse.FromObject(request.RequestId, true, "process_exited", new { processId });
    }

    // Compatibility adapter: names are allowlisted and target identity comes from Windows.
    public static ServiceHostResponse TerminateService(ServiceHostRequest request)
    {
        string serviceName = request.Data?.Value<string>("serviceName") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(serviceName) || serviceName.IndexOfAny(['\\', '/']) >= 0)
            return ServiceHostResponse.FromObject(request.RequestId, false, "Missing or invalid service name.");
        if (!ServiceHostCommandHandler.IsAllowedServiceName(serviceName))
            return ServiceHostResponse.FromObject(request.RequestId, false, "service_target_not_allowed");
        string? registeredPath = ServiceHostCommandHandler.GetServiceInstallPath(serviceName);
        string executablePath = registeredPath ?? request.Data?.Value<string>("executablePath") ?? string.Empty;
        if (!Path.IsPathFullyQualified(executablePath))
            return ServiceHostResponse.FromObject(request.RequestId, false, "The service executable path could not be resolved.");
        executablePath = Path.GetFullPath(executablePath);
        if (!ServiceHostCommandHandler.IsAllowedServiceExecutable(serviceName, executablePath))
            return ServiceHostResponse.FromObject(request.RequestId, false, "service_target_not_allowed");
        int timeoutMilliseconds = Math.Clamp(request.Data?.Value<int?>("timeoutSeconds") ?? 20, 5, 180) * 1000;
        List<int> terminated;
        List<int> remaining;
        Process[] candidates = registeredPath == null
            ? Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executablePath))
            : FindRegisteredServiceProcess(serviceName);
        try
        {
            (terminated, remaining) = TerminateServiceProcesses(candidates, executablePath, timeoutMilliseconds);
        }
        finally
        {
            foreach (Process target in candidates) target.Dispose();
        }
        bool success = remaining.Count == 0 && !ServiceHostCommandHandler.IsServiceRunning(serviceName);
        return ServiceHostResponse.FromObject(request.RequestId, success,
            success ? "service terminated" : "service terminate incomplete", new { serviceName, executablePath, terminated, remaining });
    }

    internal static (List<int> Terminated, List<int> Remaining) TerminateServiceProcesses(IEnumerable<Process> candidates, string executablePath, int timeoutMilliseconds)
    {
        List<int> terminated = [];
        List<int> remaining = [];
        foreach (Process target in candidates)
        {
            if (target.HasExited || target.Id == Environment.ProcessId)
                continue;
            _ = target.SafeHandle;
            if (!string.Equals(target.MainModule?.FileName, executablePath, StringComparison.OrdinalIgnoreCase))
                continue;
            (TerminateProcess(target, entireProcessTree: true, timeoutMilliseconds) ? terminated : remaining).Add(target.Id);
        }
        return (terminated, remaining);
    }

    private static Process[] FindRegisteredServiceProcess(string serviceName)
    {
        string escapedName = serviceName.Replace("'", "\\'", StringComparison.Ordinal);
        using ManagementObjectSearcher searcher = new($"SELECT ProcessId FROM Win32_Service WHERE Name='{escapedName}'");
        using ManagementObjectCollection services = searcher.Get();
        foreach (ManagementObject service in services)
        {
            using (service)
            {
                int processId = Convert.ToInt32(service["ProcessId"]);
                if (processId == 0) return [];
                try { return [Process.GetProcessById(processId)]; }
                catch (ArgumentException) { return []; }
            }
        }
        return [];
    }

    internal static bool TerminateProcess(Process process, bool entireProcessTree, int timeoutMilliseconds)
    {
        if (process.Id == Environment.ProcessId)
            throw new InvalidOperationException("The privilege broker cannot terminate itself.");
        try
        {
            if (!process.HasExited)
            {
                _ = process.SafeHandle;
                if (!IsAllowedProcessPath(process.MainModule?.FileName))
                    throw new InvalidOperationException("process_target_not_allowed");
                process.Kill(entireProcessTree);
            }
            return process.WaitForExit(timeoutMilliseconds);
        }
        catch (Exception) when (process.HasExited) { return true; }
    }

    internal static bool IsAllowedProcessPath(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !Path.IsPathFullyQualified(executablePath))
            return false;
        return ServiceHostCommandHandler.IsAllowedProcessExecutableName(Path.GetFileName(executablePath));
    }

    private static bool DeadlineIsValid(long notAfterUtcTicks)
    {
        long now = DateTime.UtcNow.Ticks;
        return notAfterUtcTicks > now && notAfterUtcTicks <= now + TimeSpan.FromSeconds(30).Ticks;
    }
}
