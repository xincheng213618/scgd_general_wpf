using System.IO;

namespace ColorVision.UI.Desktop.Operations
{
    public static class OperationsFailureWatchdogProtocol
    {
        public const string WatchdogDirectoryName = "OperationsWatchdog";
        public const string WatchdogExecutableName = "ColorVisionOperationsWatchdog.exe";
        public const string TargetExecutableName = "ColorVision.exe";
        public const string WatchProcessArgument = "--watch-process";
        public const string RecoveryRestartArgument = "--operations-failure-recovery";
        public static readonly TimeSpan MinimumHealthyLifetime = TimeSpan.FromSeconds(60);

        public static bool TryParseTargetProcessId(IReadOnlyList<string> args, out int processId)
        {
            processId = 0;
            return args.Count == 2
                && string.Equals(args[0], WatchProcessArgument, StringComparison.Ordinal)
                && int.TryParse(args[1], out processId)
                && processId > 0;
        }

        public static string ResolveTargetExecutablePath(string watchdogBaseDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(watchdogBaseDirectory);
            string watchdogDirectory = Path.GetFullPath(watchdogBaseDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(
                    Path.GetFileName(watchdogDirectory),
                    WatchdogDirectoryName,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The watchdog must run from the fixed OperationsWatchdog directory.");
            }

            string applicationDirectory = Directory.GetParent(watchdogDirectory)?.FullName
                ?? throw new InvalidOperationException("Unable to resolve the ColorVision application directory.");
            return Path.Combine(applicationDirectory, TargetExecutableName);
        }

        public static bool IsExpectedTargetExecutable(string watchdogBaseDirectory, string processPath)
        {
            if (string.IsNullOrWhiteSpace(processPath) || !Path.IsPathFullyQualified(processPath))
                return false;
            return string.Equals(
                Path.GetFullPath(processPath),
                Path.GetFullPath(ResolveTargetExecutablePath(watchdogBaseDirectory)),
                StringComparison.OrdinalIgnoreCase);
        }

        public static string CreateCleanExitEventName(int processId, long processStartUtcTicks) =>
            CreateEventName("CleanExit", processId, processStartUtcTicks);

        public static string CreateReadyEventName(int processId, long processStartUtcTicks) =>
            CreateEventName("Ready", processId, processStartUtcTicks);

        public static bool ShouldRestart(DateTimeOffset startedAt, DateTimeOffset exitedAt, bool cleanExitSignaled) =>
            !cleanExitSignaled && exitedAt >= startedAt + MinimumHealthyLifetime;

        private static string CreateEventName(string kind, int processId, long processStartUtcTicks)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processId);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processStartUtcTicks);
            return $"Local\\ColorVision.OperationsWatchdog.{kind}.{processId}.{processStartUtcTicks}";
        }
    }
}
