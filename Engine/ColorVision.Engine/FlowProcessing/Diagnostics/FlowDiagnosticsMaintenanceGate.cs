using System;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    /// <summary>
    /// Serializes every FlowNodeRecords.db write with exclusive maintenance.
    /// Reads remain independent and continue to use SQLite WAL snapshots.
    /// </summary>
    internal static class FlowDiagnosticsMaintenanceGate
    {
        internal static object SyncRoot { get; } = new object();

        internal static void RunExclusive(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            lock (SyncRoot)
                action();
        }

        internal static T RunExclusive<T>(Func<T> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            lock (SyncRoot)
                return action();
        }
    }
}
