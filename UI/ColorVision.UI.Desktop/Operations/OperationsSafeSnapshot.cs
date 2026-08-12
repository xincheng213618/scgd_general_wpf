using System.Text.Json;

namespace ColorVision.UI.Desktop.Operations
{
    public sealed class OperationsSafeProcessSnapshot
    {
        public double MemoryMb { get; init; }
    }

    public sealed class OperationsSafeWindowSnapshot
    {
        public bool Exists { get; init; }

        public string State { get; init; } = "Unknown";

        public bool IsVisible { get; init; }
    }

    public sealed class OperationsSafeChannelSnapshot
    {
        public bool IsRunning { get; init; }

        public long PairedDeviceCount { get; init; }

        public bool RelayConfigured { get; init; }

        public bool RelayRunning { get; init; }
    }

    public class OperationsSafeSnapshot
    {
        public string Application { get; init; } = "ColorVision";

        public string Version { get; init; } = "unknown";

        public bool IsRunning { get; init; }

        public long UptimeSeconds { get; init; }

        public DateTimeOffset CapturedAt { get; init; }

        public OperationsSafeProcessSnapshot Process { get; init; } = new();

        public OperationsSafeWindowSnapshot MainWindow { get; init; } = new();

        public OperationsSafeChannelSnapshot SecureOperations { get; init; } = new();
    }

    public sealed class OperationsRelaySnapshot : OperationsSafeSnapshot
    {
        public OperationsLiveMonitorSnapshot? Monitor { get; init; }
    }

    public static class OperationsRelaySnapshotFactory
    {
        public static OperationsRelaySnapshot Create(
            object snapshot,
            OperationsLiveMonitorSnapshot? monitor,
            DateTimeOffset? capturedAt = null)
        {
            OperationsSafeSnapshot safe = OperationsSafeSnapshotFactory.Create(snapshot, capturedAt);
            return new OperationsRelaySnapshot
            {
                Application = safe.Application,
                Version = safe.Version,
                IsRunning = safe.IsRunning,
                UptimeSeconds = safe.UptimeSeconds,
                CapturedAt = safe.CapturedAt,
                Process = safe.Process,
                MainWindow = safe.MainWindow,
                SecureOperations = safe.SecureOperations,
                Monitor = monitor,
            };
        }
    }

    public static class OperationsSafeSnapshotFactory
    {
        public static OperationsSafeSnapshot Create(object snapshot, DateTimeOffset? capturedAt = null)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            JsonElement root = JsonSerializer.SerializeToElement(snapshot, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });
            JsonElement process = Object(root, "process");
            JsonElement window = Object(root, "mainWindow");
            JsonElement secure = Object(root, "secureOperations");
            return new OperationsSafeSnapshot
            {
                Application = Text(root, "app", "ColorVision"),
                Version = Text(root, "version", "unknown"),
                IsRunning = Boolean(root, "isRunning"),
                UptimeSeconds = Integer(root, "uptimeSeconds"),
                CapturedAt = capturedAt ?? DateTimeOffset.UtcNow,
                Process = new OperationsSafeProcessSnapshot { MemoryMb = Number(process, "memoryMb") },
                MainWindow = new OperationsSafeWindowSnapshot
                {
                    Exists = Boolean(window, "exists"),
                    State = Text(window, "state", "Unknown"),
                    IsVisible = Boolean(window, "isVisible"),
                },
                SecureOperations = new OperationsSafeChannelSnapshot
                {
                    IsRunning = Boolean(secure, "isRunning"),
                    PairedDeviceCount = Integer(secure, "pairedDeviceCount"),
                    RelayConfigured = Boolean(secure, "relayConfigured"),
                    RelayRunning = Boolean(secure, "relayRunning"),
                },
            };
        }

        private static JsonElement Object(JsonElement parent, string name) =>
            parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.Object
                ? value : default;

        private static string Text(JsonElement parent, string name, string fallback) =>
            parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? fallback : fallback;

        private static bool Boolean(JsonElement parent, string name) =>
            parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement value)
            && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            && value.GetBoolean();

        private static long Integer(JsonElement parent, string name) =>
            parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement value)
            && value.TryGetInt64(out long parsed)
                ? parsed : 0;

        private static double Number(JsonElement parent, string name) =>
            parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement value)
            && value.TryGetDouble(out double parsed)
                ? parsed : 0;
    }
}
