using ColorVision.UI.Desktop.Operations;

namespace ColorVision.UI.Desktop.LanRemote
{
    internal interface ILanRemoteOperationsHost
    {
        event EventHandler? StateChanged;

        bool IsRunning { get; }

        int RunningPort { get; }

        string LastStatusMessage { get; }

        OperationsSecureHostService? PublicService { get; }

        void Start(int port, Func<object> snapshotProvider);

        void Stop();

        string CreatePairingPayload(string endpoint);

        LanRemoteOperationsHostStatus CaptureStatus();
    }

    internal sealed class LanRemoteOperationsHost : ILanRemoteOperationsHost
    {
        private readonly OperationsSecureHostService _service;

        public LanRemoteOperationsHost(OperationsSecureHostService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public event EventHandler? StateChanged
        {
            add => _service.StateChanged += value;
            remove => _service.StateChanged -= value;
        }

        public bool IsRunning => _service.IsRunning;

        public int RunningPort => _service.RunningPort;

        public string LastStatusMessage => _service.LastStatusMessage;

        public OperationsSecureHostService PublicService => _service;

        public void Start(int port, Func<object> snapshotProvider) => _service.Start(port, snapshotProvider);

        public void Stop() => _service.Stop();

        public string CreatePairingPayload(string endpoint)
        {
            OperationsPairingChallenge challenge = _service.CreatePairingChallenge(endpoint);
            return _service.Pairing.BuildQrPayload(challenge);
        }

        public LanRemoteOperationsHostStatus CaptureStatus()
        {
            return new LanRemoteOperationsHostStatus
            {
                IsRunning = _service.IsRunning,
                PairedDeviceCount = _service.Registry.GetAll().Count(item => item.IsActive),
                RelayConfigured = _service.Relay.IsConfigured,
                RelayRunning = _service.Relay.IsRunning,
                RelayLastHeartbeatAt = _service.Relay.LastHeartbeatAt,
                RelayStatus = _service.Relay.LastStatusMessage,
            };
        }
    }

    internal sealed class LanRemoteOperationsHostStatus
    {
        public bool IsRunning { get; init; }

        public int PairedDeviceCount { get; init; }

        public bool RelayConfigured { get; init; }

        public bool RelayRunning { get; init; }

        public DateTimeOffset? RelayLastHeartbeatAt { get; init; }

        public string RelayStatus { get; init; } = string.Empty;
    }
}
