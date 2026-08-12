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

    public sealed record OperationsDeviceHealthObservation(string Category, bool IsOnline);

    public sealed class OperationsDeviceHealthGroup
    {
        public string Category { get; init; } = OperationsDeviceCategories.Other;

        public int TotalCount { get; init; }

        public int OnlineCount { get; init; }

        public int OfflineCount { get; init; }
    }

    public sealed class OperationsDeviceHealthSnapshot
    {
        public bool Available { get; init; }

        public bool HasConfiguredDevices { get; init; }

        public bool AllOnline { get; init; }

        public int TotalCount { get; init; }

        public int OnlineCount { get; init; }

        public int OfflineCount { get; init; }

        public IReadOnlyList<OperationsDeviceHealthGroup> Categories { get; init; } = [];

        public DateTimeOffset ObservedAt { get; init; } = DateTimeOffset.UtcNow;

        public string PrivacyNotice { get; init; } =
            "This snapshot contains configured device counts grouped into fixed coarse categories and current online/offline flags only. It excludes device names, codes, identifiers, addresses, topics, configuration, heartbeat timestamps, and measurement data.";

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
            OperationsDeviceHealthObservation[] current = observations
                .Select(item => new OperationsDeviceHealthObservation(
                    OperationsDeviceCategories.Normalize(item.Category), item.IsOnline))
                .ToArray();
            OperationsDeviceHealthGroup[] categories = current
                .GroupBy(item => item.Category, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new OperationsDeviceHealthGroup
                {
                    Category = group.Key,
                    TotalCount = group.Count(),
                    OnlineCount = group.Count(item => item.IsOnline),
                    OfflineCount = group.Count(item => !item.IsOnline),
                })
                .ToArray();
            int onlineCount = current.Count(item => item.IsOnline);
            int offlineCount = current.Length - onlineCount;
            return new OperationsDeviceHealthSnapshot
            {
                Available = true,
                HasConfiguredDevices = current.Length > 0,
                AllOnline = current.Length > 0 && offlineCount == 0,
                TotalCount = current.Length,
                OnlineCount = onlineCount,
                OfflineCount = offlineCount,
                Categories = categories,
                ObservedAt = observedAt ?? DateTimeOffset.UtcNow,
            };
        }
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
