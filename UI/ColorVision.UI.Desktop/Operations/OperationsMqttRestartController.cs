namespace ColorVision.UI.Desktop.Operations
{
    public sealed record OperationsMqttRestartResult(bool Success, string EvidenceId);

    public interface IOperationsMqttRestartController
    {
        OperationsMqttRestartResult Restart();
    }

    public sealed class UnavailableOperationsMqttRestartController : IOperationsMqttRestartController
    {
        public static UnavailableOperationsMqttRestartController Instance { get; } = new();

        private UnavailableOperationsMqttRestartController()
        {
        }

        public OperationsMqttRestartResult Restart() =>
            new(false, "mqtt_restart_controller_unavailable");
    }
}
