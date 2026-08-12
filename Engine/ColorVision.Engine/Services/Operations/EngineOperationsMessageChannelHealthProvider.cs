using ColorVision.Engine.MQTT;
using ColorVision.UI.Desktop.Operations;

namespace ColorVision.Engine.Services.Operations
{
    public sealed class EngineOperationsMessageChannelHealthProvider : IOperationsMessageChannelHealthProvider
    {
        public OperationsMessageChannelHealthSnapshot Capture()
        {
            MqttRuntimeDiagnostics diagnostics = MQTTControl.GetInstance().CaptureRuntimeDiagnostics();
            return OperationsMessageChannelHealthSnapshotFactory.Create(
                new OperationsMessageChannelObservation(
                    diagnostics.Configured,
                    diagnostics.Connected,
                    diagnostics.RegisteredSubscriptionCount,
                    diagnostics.ActiveSubscriptionCount,
                    diagnostics.LastConnectedAt,
                    diagnostics.LastDisconnectedAt,
                    diagnostics.LastInboundActivityAt,
                    diagnostics.LastOutboundActivityAt));
        }
    }
}
