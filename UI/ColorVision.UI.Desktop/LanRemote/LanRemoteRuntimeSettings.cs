namespace ColorVision.UI.Desktop.LanRemote
{
    internal sealed class LanRemoteRuntimeSettings
    {
        public bool Enabled { get; init; }

        public int Port { get; init; } = LanRemoteControlConfig.DefaultPort;

        public int SecurePort { get; init; } = LanRemoteControlConfig.DefaultSecurePort;

        public string Host { get; init; } = "127.0.0.1";

        public string PairingToken { get; init; } = string.Empty;
    }

    internal sealed class LanRemoteRuntimeSnapshot
    {
        public LanRemoteRuntimeSettings Settings { get; init; } = new();

        public bool IsRunning { get; init; }

        public int RunningPort { get; init; }

        public string StatusMessage { get; init; } = "局域网控制已关闭。";

        public DateTime? StartedAt { get; init; }

        public LanRemoteOperationsHostStatus OperationsStatus { get; init; } = new();
    }
}
