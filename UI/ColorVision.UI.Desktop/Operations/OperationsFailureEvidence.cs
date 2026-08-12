using ColorVision.UI.Desktop.Diagnostics;
using System.Diagnostics;
using System.IO;

namespace ColorVision.UI.Desktop.Operations
{
    public static class OperationsFailureKinds
    {
        public const string ApplicationCrash = "application-crash";
        public const string ApplicationHang = "application-hang";
        public const string ManagedRuntimeFailure = "managed-runtime-failure";
        public const string WindowsErrorReport = "windows-error-report";
    }

    public sealed record OperationsFailureEventObservation(DateTimeOffset OccurredAt, string Kind);

    public sealed record OperationsFailureDumpObservation(DateTimeOffset ModifiedAt);

    public sealed class OperationsFailureEvidenceSnapshot
    {
        public bool Available { get; init; }

        public bool EventLogAvailable { get; init; }

        public bool DumpFolderAvailable { get; init; }

        public bool EventScanLimited { get; init; }

        public bool DumpScanLimited { get; init; }

        public bool HasEvidence { get; init; }

        public int WindowDays { get; init; } = 7;

        public int FailureEventCount { get; init; }

        public int CrashCount { get; init; }

        public int HangCount { get; init; }

        public int ManagedRuntimeFailureCount { get; init; }

        public int WindowsErrorReportCount { get; init; }

        public int DumpCount { get; init; }

        public DateTimeOffset? LatestEventAt { get; init; }

        public DateTimeOffset? LatestDumpAt { get; init; }

        public DateTimeOffset? LatestEvidenceAt { get; init; }

        public DateTimeOffset WindowStartedAt { get; init; }

        public DateTimeOffset ObservedAt { get; init; }

        public string PrivacyNotice { get; init; } =
            "This fixed seven-day snapshot contains only bounded ColorVision failure-category counts and aggregate timestamps. It excludes event messages, replacement strings, sources, file names, paths, dump contents, process identifiers, machine or user identities, stack traces, command lines, application data, and arbitrary log queries.";

        public static OperationsFailureEvidenceSnapshot CreateUnavailable(DateTimeOffset? observedAt = null)
        {
            DateTimeOffset now = observedAt ?? DateTimeOffset.UtcNow;
            return new OperationsFailureEvidenceSnapshot
            {
                ObservedAt = now,
                WindowStartedAt = now.AddDays(-7),
            };
        }
    }

    public static class OperationsFailureEvidenceSnapshotFactory
    {
        private const int MaximumExposedCount = 999;

        public static OperationsFailureEvidenceSnapshot Create(
            IEnumerable<OperationsFailureEventObservation> events,
            IEnumerable<OperationsFailureDumpObservation> dumps,
            bool eventLogAvailable,
            bool dumpFolderAvailable,
            bool eventScanLimited = false,
            bool dumpScanLimited = false,
            DateTimeOffset? observedAt = null)
        {
            ArgumentNullException.ThrowIfNull(events);
            ArgumentNullException.ThrowIfNull(dumps);
            DateTimeOffset now = observedAt ?? DateTimeOffset.UtcNow;
            DateTimeOffset windowStartedAt = now.AddDays(-7);
            OperationsFailureEventObservation[] recentEvents = events
                .Where(item => item.OccurredAt >= windowStartedAt && item.OccurredAt <= now)
                .ToArray();
            OperationsFailureDumpObservation[] recentDumps = dumps
                .Where(item => item.ModifiedAt >= windowStartedAt && item.ModifiedAt <= now)
                .ToArray();
            DateTimeOffset? latestEventAt = recentEvents.Length == 0 ? null : recentEvents.Max(item => item.OccurredAt);
            DateTimeOffset? latestDumpAt = recentDumps.Length == 0 ? null : recentDumps.Max(item => item.ModifiedAt);

            return new OperationsFailureEvidenceSnapshot
            {
                Available = eventLogAvailable || dumpFolderAvailable,
                EventLogAvailable = eventLogAvailable,
                DumpFolderAvailable = dumpFolderAvailable,
                EventScanLimited = eventScanLimited,
                DumpScanLimited = dumpScanLimited,
                HasEvidence = recentEvents.Length > 0 || recentDumps.Length > 0,
                FailureEventCount = Bound(recentEvents.Length),
                CrashCount = Bound(Count(recentEvents, OperationsFailureKinds.ApplicationCrash)),
                HangCount = Bound(Count(recentEvents, OperationsFailureKinds.ApplicationHang)),
                ManagedRuntimeFailureCount = Bound(Count(recentEvents, OperationsFailureKinds.ManagedRuntimeFailure)),
                WindowsErrorReportCount = Bound(Count(recentEvents, OperationsFailureKinds.WindowsErrorReport)),
                DumpCount = Bound(recentDumps.Length),
                LatestEventAt = latestEventAt,
                LatestDumpAt = latestDumpAt,
                LatestEvidenceAt = Latest(latestEventAt, latestDumpAt),
                WindowStartedAt = windowStartedAt,
                ObservedAt = now,
            };
        }

        private static int Count(IEnumerable<OperationsFailureEventObservation> events, string kind) =>
            events.Count(item => item.Kind == kind);

        private static int Bound(int count) => Math.Clamp(count, 0, MaximumExposedCount);

        private static DateTimeOffset? Latest(DateTimeOffset? first, DateTimeOffset? second) =>
            first == null ? second : second == null ? first : first > second ? first : second;
    }

    public interface IOperationsFailureEvidenceProvider
    {
        OperationsFailureEvidenceSnapshot Capture();
    }

    public sealed class UnavailableOperationsFailureEvidenceProvider : IOperationsFailureEvidenceProvider
    {
        public static UnavailableOperationsFailureEvidenceProvider Instance { get; } = new();

        private UnavailableOperationsFailureEvidenceProvider()
        {
        }

        public OperationsFailureEvidenceSnapshot Capture() => OperationsFailureEvidenceSnapshot.CreateUnavailable();
    }

    public sealed class WindowsOperationsFailureEvidenceService : IOperationsFailureEvidenceProvider
    {
        private const int MaximumEventEntriesInspected = 2000;
        private const int MaximumDumpFilesInspected = 1000;

        public OperationsFailureEvidenceSnapshot Capture()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateTimeOffset windowStartedAt = now.AddDays(-7);
            List<OperationsFailureEventObservation> events = [];
            List<OperationsFailureDumpObservation> dumps = [];
            bool eventLogAvailable = TryReadEvents(windowStartedAt, events, out bool eventScanLimited);
            bool dumpFolderAvailable = TryReadDumps(windowStartedAt, dumps, out bool dumpScanLimited);
            return OperationsFailureEvidenceSnapshotFactory.Create(
                events, dumps, eventLogAvailable, dumpFolderAvailable,
                eventScanLimited, dumpScanLimited, now);
        }

        private static bool TryReadEvents(
            DateTimeOffset windowStartedAt,
            List<OperationsFailureEventObservation> observations,
            out bool scanLimited)
        {
            scanLimited = false;
            try
            {
                using EventLog eventLog = new("Application");
                int inspected = 0;
                for (int index = eventLog.Entries.Count - 1; index >= 0; index--)
                {
                    if (inspected >= MaximumEventEntriesInspected)
                    {
                        scanLimited = true;
                        break;
                    }

                    EventLogEntry entry = eventLog.Entries[index];
                    inspected++;
                    DateTimeOffset occurredAt = new(entry.TimeGenerated);
                    if (occurredAt < windowStartedAt)
                        break;
                    if (entry.EntryType is not (EventLogEntryType.Error or EventLogEntryType.Warning))
                        continue;
                    if (!TryClassify(entry.Source, unchecked((int)(entry.InstanceId & 0xffff)), out string kind))
                        continue;
                    if (!ContainsColorVisionExecutable(entry.ReplacementStrings))
                        continue;

                    observations.Add(new OperationsFailureEventObservation(occurredAt, kind));
                }
                return true;
            }
            catch
            {
                observations.Clear();
                scanLimited = false;
                return false;
            }
        }

        private static bool TryReadDumps(
            DateTimeOffset windowStartedAt,
            List<OperationsFailureDumpObservation> observations,
            out bool scanLimited)
        {
            scanLimited = false;
            try
            {
                string dumpFolder = new CrashDumpConfiguration("ColorVision.exe").DumpFolder;
                if (!Directory.Exists(dumpFolder))
                    return true;

                int inspected = 0;
                foreach (string filePath in Directory.EnumerateFiles(dumpFolder, "*.dmp", SearchOption.TopDirectoryOnly))
                {
                    string fileName = Path.GetFileName(filePath);
                    if (!fileName.StartsWith("ColorVision", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (inspected >= MaximumDumpFilesInspected)
                    {
                        scanLimited = true;
                        break;
                    }

                    inspected++;
                    FileInfo file = new(filePath);
                    DateTimeOffset modifiedAt = new(file.LastWriteTime);
                    if (modifiedAt >= windowStartedAt)
                        observations.Add(new OperationsFailureDumpObservation(modifiedAt));
                }
                return true;
            }
            catch
            {
                observations.Clear();
                scanLimited = false;
                return false;
            }
        }

        internal static bool TryClassify(string source, int eventId, out string kind)
        {
            kind = string.Empty;
            if (source.Equals("Application Error", StringComparison.OrdinalIgnoreCase) && eventId == 1000)
                kind = OperationsFailureKinds.ApplicationCrash;
            else if (source.Equals("Application Hang", StringComparison.OrdinalIgnoreCase) && eventId == 1002)
                kind = OperationsFailureKinds.ApplicationHang;
            else if (source.Equals(".NET Runtime", StringComparison.OrdinalIgnoreCase) && eventId == 1026)
                kind = OperationsFailureKinds.ManagedRuntimeFailure;
            else if (source.Equals("Windows Error Reporting", StringComparison.OrdinalIgnoreCase) && eventId == 1001)
                kind = OperationsFailureKinds.WindowsErrorReport;
            return kind.Length > 0;
        }

        internal static bool ContainsColorVisionExecutable(IEnumerable<string> replacementStrings) =>
            replacementStrings.Any(value => value?.Contains("ColorVision.exe", StringComparison.OrdinalIgnoreCase) == true);
    }
}
