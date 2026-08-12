using System.Runtime.InteropServices;

namespace ColorVision.UI.Desktop.Operations
{
    public sealed class OperationsApplicationRecoveryStatus
    {
        public bool Supported { get; init; }

        public bool Registered { get; init; }

        public bool RestartedAfterFailure { get; init; }

        public string Mode { get; init; } = "current-application-after-failure";
    }

    /// <summary>
    /// Registers only the current ColorVision process for Windows-managed restart after a
    /// crash or hang. The registered command line is fixed and never accepts a path,
    /// executable, command, or argument from an operations client.
    /// </summary>
    public static class WindowsApplicationRestartRegistration
    {
        public const string RecoveryRestartArgument = "--operations-failure-recovery";

        private const uint RestartNoPatch = 0x4;
        private const uint RestartNoReboot = 0x8;
        private const uint RecoveryRestartFlags = RestartNoPatch | RestartNoReboot;
        private static int _registered;
        private static int _restartedAfterFailure;
        private static int _fatalFailureObserved;

        public static bool RestartedAfterFailure =>
            Volatile.Read(ref _restartedAfterFailure) != 0;

        public static string[] CaptureAndRemoveRecoveryArguments(string[] args)
        {
            ArgumentNullException.ThrowIfNull(args);
            List<string> applicationArguments = new(args.Length);
            bool recovered = false;
            foreach (string argument in args)
            {
                if (string.Equals(argument, RecoveryRestartArgument, StringComparison.OrdinalIgnoreCase))
                {
                    recovered = true;
                    continue;
                }

                applicationArguments.Add(argument);
            }

            if (recovered)
                Volatile.Write(ref _restartedAfterFailure, 1);
            return applicationArguments.ToArray();
        }

        public static bool TryRegister()
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(6))
                return false;

            try
            {
                int result = RegisterApplicationRestart(RecoveryRestartArgument, RecoveryRestartFlags);
                bool registered = result >= 0;
                Volatile.Write(ref _registered, registered ? 1 : 0);
                return registered;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }

        public static void MarkFatalFailureObserved() =>
            Volatile.Write(ref _fatalFailureObserved, 1);

        public static bool TryUnregisterForCleanExit()
        {
            if (Volatile.Read(ref _fatalFailureObserved) != 0)
                return false;
            if (Volatile.Read(ref _registered) == 0)
                return true;

            try
            {
                bool unregistered = UnregisterApplicationRestart() >= 0;
                if (unregistered)
                    Volatile.Write(ref _registered, 0);
                return unregistered;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }

        public static OperationsApplicationRecoveryStatus CaptureStatus() => new()
        {
            Supported = OperatingSystem.IsWindowsVersionAtLeast(6),
            Registered = Volatile.Read(ref _registered) != 0,
            RestartedAfterFailure = RestartedAfterFailure,
        };

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegisterApplicationRestart(string commandLine, uint flags);

        [DllImport("kernel32.dll")]
        private static extern int UnregisterApplicationRestart();
    }
}
