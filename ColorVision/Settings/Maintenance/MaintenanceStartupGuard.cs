using System;
using System.Diagnostics;

namespace ColorVision.Settings.Maintenance;

internal static class MaintenanceStartupGuard
{
    // Configuration is loaded before the ordinary single-instance handoff. Another instance
    // may still save its old objects on exit, so a reset must wait for a sole-process startup.
    public static bool CanApplyReset()
    {
        using Process current = Process.GetCurrentProcess();
        Process[] processes = Process.GetProcessesByName(current.ProcessName);
        try
        {
            foreach (Process process in processes)
                if (process.Id != current.Id && !process.HasExited) return false;
            return true;
        }
        finally
        {
            foreach (Process process in processes) process.Dispose();
        }
    }
}
