using System;
using System.ComponentModel;
using System.Diagnostics;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    internal enum FlowOwnerProcessState
    {
        Unknown,
        Alive,
        NotRunning,
        StartTimeMismatch,
    }

    internal interface IFlowProcessProbe
    {
        FlowOwnerProcessState GetState(int processId, DateTime expectedStartedUtc);
    }

    internal sealed record FlowExecutionOwnerIdentity(
        string InstanceId,
        string MachineName,
        int ProcessId,
        DateTime ProcessStartedUtc)
    {
        private static readonly Lazy<FlowExecutionOwnerIdentity> CurrentIdentity =
            new(CreateCurrentCore);

        public static FlowExecutionOwnerIdentity CreateCurrent()
        {
            return CurrentIdentity.Value;
        }

        private static FlowExecutionOwnerIdentity CreateCurrentCore()
        {
            using Process process = Process.GetCurrentProcess();
            return new FlowExecutionOwnerIdentity(
                Guid.NewGuid().ToString("N"),
                Environment.MachineName,
                process.Id,
                process.StartTime.ToUniversalTime());
        }
    }

    internal sealed record FlowRunRecoveryResult(
        FlowRunRecord Run,
        FlowExecutionEvent Event,
        FlowIncident Incident);

    internal sealed class SystemFlowProcessProbe : IFlowProcessProbe
    {
        // SQLite providers may round DateTime values during a persistence
        // round-trip. A one-second window is still narrow enough to detect PID
        // reuse while avoiding recovery of the process that actually owns the
        // run.
        internal static readonly TimeSpan StartTimeTolerance = TimeSpan.FromSeconds(1);

        public FlowOwnerProcessState GetState(int processId, DateTime expectedStartedUtc)
        {
            try
            {
                using Process process = Process.GetProcessById(processId);
                if (process.HasExited)
                    return FlowOwnerProcessState.NotRunning;

                DateTime actualStartedUtc = process.StartTime.ToUniversalTime();
                DateTime expectedUtc = NormalizeUtc(expectedStartedUtc);
                return (actualStartedUtc - expectedUtc).Duration() <= StartTimeTolerance
                    ? FlowOwnerProcessState.Alive
                    : FlowOwnerProcessState.StartTimeMismatch;
            }
            catch (ArgumentException)
            {
                return FlowOwnerProcessState.NotRunning;
            }
            catch (InvalidOperationException)
            {
                return FlowOwnerProcessState.NotRunning;
            }
            catch (Win32Exception)
            {
                return FlowOwnerProcessState.Unknown;
            }
            catch (PlatformNotSupportedException)
            {
                return FlowOwnerProcessState.Unknown;
            }
            catch (NotSupportedException)
            {
                return FlowOwnerProcessState.Unknown;
            }
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            };
        }
    }
}
