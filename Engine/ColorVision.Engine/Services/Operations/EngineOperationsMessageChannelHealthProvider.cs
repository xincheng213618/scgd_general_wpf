using ColorVision.Engine.MQTT;
using ColorVision.UI.Desktop.Operations;
using System;
using System.Threading;

namespace ColorVision.Engine.Services.Operations
{
    public sealed class EngineOperationsMessageChannelHealthProvider :
        IOperationsMessageChannelHealthProvider,
        IOperationsMessageChannelRecoveryController
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

        public OperationsMessageChannelRecoveryResult Recover()
        {
            OperationsMessageChannelHealthSnapshot before = Capture();
            if (!before.Available || !before.Configured)
                return new(false, "message_channel:unconfigured");
            if (before.Connected && before.SubscriptionReady)
                return new(true, "message_channel:already_ready");

            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));
            try
            {
                bool recovered = MQTTControl.GetInstance().RecoverConnectionAsync(timeout.Token)
                    .GetAwaiter().GetResult();
                OperationsMessageChannelHealthSnapshot after = Capture();
                bool ready = recovered && after.Connected && after.SubscriptionReady;
                return new(ready, ready
                    ? "message_channel:recovered"
                    : timeout.IsCancellationRequested
                        ? "message_channel:recovery_timeout"
                        : "message_channel:recovery_failed");
            }
            catch (OperationCanceledException)
            {
                return new(false, "message_channel:recovery_timeout");
            }
            catch
            {
                return new(false, "message_channel:recovery_failed");
            }
        }
    }
}
