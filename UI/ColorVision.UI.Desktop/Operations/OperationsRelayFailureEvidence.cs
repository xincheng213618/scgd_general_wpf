namespace ColorVision.UI.Desktop.Operations
{
    public sealed class OperationsRelayFailureEvidence
    {
        public string Kind { get; init; } = "failure-evidence-v1";

        public bool EventLogAvailable { get; init; }

        public bool DumpFolderAvailable { get; init; }

        public bool EventScanLimited { get; init; }

        public bool DumpScanLimited { get; init; }

        public bool HasEvidence { get; init; }

        public int WindowDays { get; init; }

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
    }

    public sealed class OperationsRelayFailureEvidenceError
    {
        public string Kind { get; init; } = "failure-evidence-error-v1";

        public string Code { get; init; } = "failure_evidence_unavailable";
    }

    public sealed class OperationsRelayFailureEvidenceResult
    {
        public string Status { get; init; } = "failed";

        public object Evidence { get; init; } = new OperationsRelayFailureEvidenceError();
    }

    public sealed class OperationsRelayFailureEvidenceHandler
    {
        private const int MaximumCount = 999;
        private readonly IOperationsFailureEvidenceProvider _provider;
        private readonly OperationsWorkStore _workStore;

        public OperationsRelayFailureEvidenceHandler(
            IOperationsFailureEvidenceProvider provider,
            OperationsWorkStore workStore)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentNullException.ThrowIfNull(workStore);
            _provider = provider;
            _workStore = workStore;
        }

        public OperationsRelayFailureEvidenceResult Handle(OperationsRelayVerifiedTask task)
        {
            ArgumentNullException.ThrowIfNull(task);
            string status;
            object evidence;
            try
            {
                OperationsFailureEvidenceSnapshot snapshot = _provider.Capture();
                evidence = Project(snapshot);
                status = "completed";
            }
            catch
            {
                evidence = new OperationsRelayFailureEvidenceError();
                status = "failed";
            }

            _workStore.RecordAudit(
                task.Device.DeviceId,
                "device",
                "diagnostics.failure-evidence.read",
                "failure-evidence",
                status,
                task.IdempotencyKey);
            return new OperationsRelayFailureEvidenceResult
            {
                Status = status,
                Evidence = evidence,
            };
        }

        private static OperationsRelayFailureEvidence Project(
            OperationsFailureEvidenceSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (!IsConsistent(snapshot))
                throw new InvalidOperationException("failure_evidence_unavailable");

            return new OperationsRelayFailureEvidence
            {
                EventLogAvailable = snapshot.EventLogAvailable,
                DumpFolderAvailable = snapshot.DumpFolderAvailable,
                EventScanLimited = snapshot.EventScanLimited,
                DumpScanLimited = snapshot.DumpScanLimited,
                HasEvidence = snapshot.HasEvidence,
                WindowDays = snapshot.WindowDays,
                FailureEventCount = snapshot.FailureEventCount,
                CrashCount = snapshot.CrashCount,
                HangCount = snapshot.HangCount,
                ManagedRuntimeFailureCount = snapshot.ManagedRuntimeFailureCount,
                WindowsErrorReportCount = snapshot.WindowsErrorReportCount,
                DumpCount = snapshot.DumpCount,
                LatestEventAt = snapshot.LatestEventAt,
                LatestDumpAt = snapshot.LatestDumpAt,
                LatestEvidenceAt = snapshot.LatestEvidenceAt,
                WindowStartedAt = snapshot.WindowStartedAt,
                ObservedAt = snapshot.ObservedAt,
            };
        }

        private static bool IsConsistent(OperationsFailureEvidenceSnapshot snapshot)
        {
            if (!snapshot.Available
                || snapshot.WindowDays != 7
                || snapshot.WindowStartedAt > snapshot.ObservedAt
                || !ValidCount(snapshot.FailureEventCount)
                || !ValidCount(snapshot.CrashCount)
                || !ValidCount(snapshot.HangCount)
                || !ValidCount(snapshot.ManagedRuntimeFailureCount)
                || !ValidCount(snapshot.WindowsErrorReportCount)
                || !ValidCount(snapshot.DumpCount))
                return false;

            if (snapshot.HasEvidence != (snapshot.FailureEventCount > 0 || snapshot.DumpCount > 0)
                || (snapshot.FailureEventCount == 0) != (snapshot.LatestEventAt == null)
                || (snapshot.DumpCount == 0) != (snapshot.LatestDumpAt == null)
                || (!snapshot.HasEvidence && HasAnyCount(snapshot)))
                return false;

            if (!InsideWindow(snapshot.LatestEventAt, snapshot)
                || !InsideWindow(snapshot.LatestDumpAt, snapshot)
                || !InsideWindow(snapshot.LatestEvidenceAt, snapshot))
                return false;

            DateTimeOffset? expectedLatest = Latest(
                snapshot.LatestEventAt, snapshot.LatestDumpAt);
            return snapshot.LatestEvidenceAt == expectedLatest;
        }

        private static bool ValidCount(int count) => count is >= 0 and <= MaximumCount;

        private static bool HasAnyCount(OperationsFailureEvidenceSnapshot snapshot) =>
            snapshot.FailureEventCount > 0
            || snapshot.CrashCount > 0
            || snapshot.HangCount > 0
            || snapshot.ManagedRuntimeFailureCount > 0
            || snapshot.WindowsErrorReportCount > 0
            || snapshot.DumpCount > 0;

        private static bool InsideWindow(
            DateTimeOffset? timestamp,
            OperationsFailureEvidenceSnapshot snapshot) =>
            timestamp == null
            || timestamp >= snapshot.WindowStartedAt && timestamp <= snapshot.ObservedAt;

        private static DateTimeOffset? Latest(
            DateTimeOffset? first,
            DateTimeOffset? second) =>
            first == null ? second : second == null ? first : first > second ? first : second;
    }
}
