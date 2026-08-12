namespace ColorVision.UI.Desktop.Operations
{
    public static class OperationsMessageChannelStates
    {
        public const string Connected = "connected";
        public const string Degraded = "degraded";
        public const string Disconnected = "disconnected";
        public const string Unconfigured = "unconfigured";
        public const string Unavailable = "unavailable";
    }

    public sealed record OperationsMessageChannelObservation(
        bool Configured,
        bool Connected,
        int RegisteredSubscriptionCount,
        int ActiveSubscriptionCount,
        DateTimeOffset? LastConnectedAt = null,
        DateTimeOffset? LastDisconnectedAt = null,
        DateTimeOffset? LastInboundActivityAt = null,
        DateTimeOffset? LastOutboundActivityAt = null);

    public sealed class OperationsMessageChannelHealthSnapshot
    {
        public bool Available { get; init; }

        public bool Configured { get; init; }

        public string State { get; init; } = OperationsMessageChannelStates.Unavailable;

        public bool Connected { get; init; }

        public bool SubscriptionReady { get; init; }

        public int RegisteredSubscriptionCount { get; init; }

        public int ActiveSubscriptionCount { get; init; }

        public DateTimeOffset? LastConnectedAt { get; init; }

        public DateTimeOffset? LastDisconnectedAt { get; init; }

        public DateTimeOffset? LastInboundActivityAt { get; init; }

        public DateTimeOffset? LastOutboundActivityAt { get; init; }

        public DateTimeOffset ObservedAt { get; init; } = DateTimeOffset.UtcNow;

        public bool AttentionRequired { get; init; }

        public string PrivacyNotice { get; init; } =
            "This snapshot contains normalized ColorVision message-channel connection state, subscription counts, and aggregate activity times only. It excludes hosts, ports, endpoints, topics, payloads, client or device identifiers, configuration, credentials, certificates, and raw logs.";

        public static OperationsMessageChannelHealthSnapshot CreateUnavailable(DateTimeOffset? observedAt = null) => new()
        {
            ObservedAt = observedAt ?? DateTimeOffset.UtcNow,
        };
    }

    public static class OperationsMessageChannelHealthSnapshotFactory
    {
        public static OperationsMessageChannelHealthSnapshot Create(
            OperationsMessageChannelObservation observation,
            DateTimeOffset? observedAt = null)
        {
            ArgumentNullException.ThrowIfNull(observation);
            int registeredCount = Math.Max(0, observation.RegisteredSubscriptionCount);
            int activeCount = Math.Max(0, observation.ActiveSubscriptionCount);
            bool connected = observation.Configured && observation.Connected;
            bool subscriptionReady = connected && activeCount >= registeredCount;
            string state = !observation.Configured
                ? OperationsMessageChannelStates.Unconfigured
                : !connected
                    ? OperationsMessageChannelStates.Disconnected
                    : subscriptionReady
                        ? OperationsMessageChannelStates.Connected
                        : OperationsMessageChannelStates.Degraded;
            return new OperationsMessageChannelHealthSnapshot
            {
                Available = true,
                Configured = observation.Configured,
                State = state,
                Connected = connected,
                SubscriptionReady = subscriptionReady,
                RegisteredSubscriptionCount = registeredCount,
                ActiveSubscriptionCount = activeCount,
                LastConnectedAt = observation.LastConnectedAt,
                LastDisconnectedAt = observation.LastDisconnectedAt,
                LastInboundActivityAt = observation.LastInboundActivityAt,
                LastOutboundActivityAt = observation.LastOutboundActivityAt,
                ObservedAt = observedAt ?? DateTimeOffset.UtcNow,
                AttentionRequired = state != OperationsMessageChannelStates.Connected,
            };
        }
    }

    public interface IOperationsMessageChannelHealthProvider
    {
        OperationsMessageChannelHealthSnapshot Capture();
    }

    public sealed class UnavailableOperationsMessageChannelHealthProvider : IOperationsMessageChannelHealthProvider
    {
        public static UnavailableOperationsMessageChannelHealthProvider Instance { get; } = new();

        private UnavailableOperationsMessageChannelHealthProvider()
        {
        }

        public OperationsMessageChannelHealthSnapshot Capture() =>
            OperationsMessageChannelHealthSnapshot.CreateUnavailable();
    }
}
