namespace ColorVision.UI.Desktop.Operations
{
    public static class OperationsServiceIds
    {
        public const string ServiceHost = "colorvision-service-host";
        public const string MqttBroker = "mqtt-broker";
    }

    public sealed class OperationsServiceHealthItem
    {
        public string ServiceId { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Status { get; init; } = "unknown";
        public bool Installed { get; init; }
        public bool Healthy { get; init; }
        public bool MaintenanceSupported { get; init; }
        public string StatusSource { get; init; } = string.Empty;
        public DateTimeOffset ObservedAt { get; init; } = DateTimeOffset.UtcNow;
    }

    public sealed class OperationsServiceHealthReport
    {
        public bool Available { get; init; }
        public bool AllHealthy { get; init; }
        public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;
        public IReadOnlyList<OperationsServiceHealthItem> Services { get; init; } = [];
        public string PrivacyNotice { get; init; } =
            "仅报告固定白名单服务的规范化状态；不返回服务账户、可执行路径、启动参数或机器标识。";

        public static OperationsServiceHealthReport CreateUnavailable() => new();
    }

    public interface IOperationsServiceHealthProvider
    {
        OperationsServiceHealthReport Capture();
    }

    public sealed class UnavailableOperationsServiceHealthProvider : IOperationsServiceHealthProvider
    {
        public static UnavailableOperationsServiceHealthProvider Instance { get; } = new();

        private UnavailableOperationsServiceHealthProvider()
        {
        }

        public OperationsServiceHealthReport Capture() => OperationsServiceHealthReport.CreateUnavailable();
    }
}
