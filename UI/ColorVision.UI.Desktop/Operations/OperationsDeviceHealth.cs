namespace ColorVision.UI.Desktop.Operations
{
    public static class OperationsDeviceCategories
    {
        public const string Camera = "camera";
        public const string Algorithm = "algorithm";
        public const string Spectrum = "spectrum";
        public const string Instrument = "instrument";
        public const string Motion = "motion";
        public const string Calibration = "calibration";
        public const string Other = "other";

        internal static string Normalize(string category) => category switch
        {
            Camera or Algorithm or Spectrum or Instrument or Motion or Calibration => category,
            _ => Other,
        };
    }

    public static class OperationsDeviceStates
    {
        public const string Ready = "ready";
        public const string Busy = "busy";
        public const string Transitioning = "transitioning";
        public const string Closed = "closed";
        public const string Unavailable = "unavailable";
        public const string Unknown = "unknown";

        internal static string Normalize(string state) => state switch
        {
            Ready or Busy or Transitioning or Closed or Unavailable => state,
            _ => Unknown,
        };
    }

    public static class OperationsDeviceUnavailableReasons
    {
        public const string None = "none";
        public const string Offline = "offline";
        public const string Uninitialized = "uninitialized";
        public const string Unauthorized = "unauthorized";
        public const string Unclassified = "unclassified";

        internal static string Normalize(string reason, string state) => state != OperationsDeviceStates.Unavailable
            ? None
            : reason switch
            {
                Offline or Uninitialized or Unauthorized => reason,
                _ => Unclassified,
            };
    }

    public sealed record OperationsDeviceHealthObservation(
        string Category,
        string State,
        string UnavailableReason = OperationsDeviceUnavailableReasons.None);

    public sealed class OperationsDeviceHealthGroup
    {
        public string Category { get; init; } = OperationsDeviceCategories.Other;

        public int TotalCount { get; init; }

        public int ReadyCount { get; init; }

        public int BusyCount { get; init; }

        public int TransitioningCount { get; init; }

        public int ClosedCount { get; init; }

        public int UnavailableCount { get; init; }

        public int UnknownCount { get; init; }

        public int AttentionCount { get; init; }

        public int OfflineCount { get; init; }

        public int UninitializedCount { get; init; }

        public int UnauthorizedCount { get; init; }

        public int UnclassifiedUnavailableCount { get; init; }
    }

    public sealed class OperationsDeviceHealthSnapshot
    {
        public bool Available { get; init; }

        public bool HasConfiguredDevices { get; init; }

        public bool AllHealthy { get; init; }

        public int TotalCount { get; init; }

        public int ReadyCount { get; init; }

        public int BusyCount { get; init; }

        public int TransitioningCount { get; init; }

        public int ClosedCount { get; init; }

        public int UnavailableCount { get; init; }

        public int UnknownCount { get; init; }

        public int AttentionCount { get; init; }

        public int OfflineCount { get; init; }

        public int UninitializedCount { get; init; }

        public int UnauthorizedCount { get; init; }

        public int UnclassifiedUnavailableCount { get; init; }

        public IReadOnlyList<OperationsDeviceHealthGroup> Categories { get; init; } = [];

        public DateTimeOffset ObservedAt { get; init; } = DateTimeOffset.UtcNow;

        public string PrivacyNotice { get; init; } =
            "This snapshot contains configured device counts, normalized runtime states, and fixed unavailability-reason counts grouped into fixed coarse categories only. It excludes device names, codes, identifiers, addresses, topics, configuration, raw status payloads, device activity timestamps, and measurement data.";

        public static OperationsDeviceHealthSnapshot CreateUnavailable(DateTimeOffset? observedAt = null) => new()
        {
            ObservedAt = observedAt ?? DateTimeOffset.UtcNow,
        };
    }

    public static class OperationsDeviceHealthSnapshotFactory
    {
        public static OperationsDeviceHealthSnapshot Create(
            IEnumerable<OperationsDeviceHealthObservation> observations,
            DateTimeOffset? observedAt = null)
        {
            ArgumentNullException.ThrowIfNull(observations);
            OperationsDeviceHealthObservation[] current = observations.Select(item =>
            {
                string state = OperationsDeviceStates.Normalize(item.State);
                return new OperationsDeviceHealthObservation(
                    OperationsDeviceCategories.Normalize(item.Category),
                    state,
                    OperationsDeviceUnavailableReasons.Normalize(item.UnavailableReason, state));
            }).ToArray();
            OperationsDeviceHealthGroup[] categories = current
                .GroupBy(item => item.Category, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new OperationsDeviceHealthGroup
                {
                    Category = group.Key,
                    TotalCount = group.Count(),
                    ReadyCount = Count(group, OperationsDeviceStates.Ready),
                    BusyCount = Count(group, OperationsDeviceStates.Busy),
                    TransitioningCount = Count(group, OperationsDeviceStates.Transitioning),
                    ClosedCount = Count(group, OperationsDeviceStates.Closed),
                    UnavailableCount = Count(group, OperationsDeviceStates.Unavailable),
                    UnknownCount = Count(group, OperationsDeviceStates.Unknown),
                    AttentionCount = group.Count(item => item.State is
                        OperationsDeviceStates.Unavailable or OperationsDeviceStates.Unknown),
                    OfflineCount = CountReason(group, OperationsDeviceUnavailableReasons.Offline),
                    UninitializedCount = CountReason(group, OperationsDeviceUnavailableReasons.Uninitialized),
                    UnauthorizedCount = CountReason(group, OperationsDeviceUnavailableReasons.Unauthorized),
                    UnclassifiedUnavailableCount = CountReason(
                        group, OperationsDeviceUnavailableReasons.Unclassified),
                })
                .ToArray();
            int readyCount = Count(current, OperationsDeviceStates.Ready);
            int busyCount = Count(current, OperationsDeviceStates.Busy);
            int transitioningCount = Count(current, OperationsDeviceStates.Transitioning);
            int closedCount = Count(current, OperationsDeviceStates.Closed);
            int unavailableCount = Count(current, OperationsDeviceStates.Unavailable);
            int unknownCount = Count(current, OperationsDeviceStates.Unknown);
            int attentionCount = unavailableCount + unknownCount;
            return new OperationsDeviceHealthSnapshot
            {
                Available = true,
                HasConfiguredDevices = current.Length > 0,
                AllHealthy = current.Length > 0 && attentionCount == 0,
                TotalCount = current.Length,
                ReadyCount = readyCount,
                BusyCount = busyCount,
                TransitioningCount = transitioningCount,
                ClosedCount = closedCount,
                UnavailableCount = unavailableCount,
                UnknownCount = unknownCount,
                AttentionCount = attentionCount,
                OfflineCount = CountReason(current, OperationsDeviceUnavailableReasons.Offline),
                UninitializedCount = CountReason(current, OperationsDeviceUnavailableReasons.Uninitialized),
                UnauthorizedCount = CountReason(current, OperationsDeviceUnavailableReasons.Unauthorized),
                UnclassifiedUnavailableCount = CountReason(current, OperationsDeviceUnavailableReasons.Unclassified),
                Categories = categories,
                ObservedAt = observedAt ?? DateTimeOffset.UtcNow,
            };
        }

        private static int Count(
            IEnumerable<OperationsDeviceHealthObservation> observations,
            string state) => observations.Count(item => item.State == state);

        private static int CountReason(
            IEnumerable<OperationsDeviceHealthObservation> observations,
            string reason) => observations.Count(item => item.UnavailableReason == reason);
    }

    public interface IOperationsDeviceHealthProvider
    {
        OperationsDeviceHealthSnapshot Capture();
    }

    public sealed class UnavailableOperationsDeviceHealthProvider : IOperationsDeviceHealthProvider
    {
        public static UnavailableOperationsDeviceHealthProvider Instance { get; } = new();

        private UnavailableOperationsDeviceHealthProvider()
        {
        }

        public OperationsDeviceHealthSnapshot Capture() => OperationsDeviceHealthSnapshot.CreateUnavailable();
    }
}
