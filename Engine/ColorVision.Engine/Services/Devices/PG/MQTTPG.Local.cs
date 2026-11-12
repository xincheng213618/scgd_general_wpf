using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Sensor.Local;
using System.Threading;

namespace ColorVision.Engine.Services.Devices.PG;

public partial class MQTTPG
{
    internal DevicePG? Device { get; set; }
    public override DeviceStatusType DeviceStatus
    {
        get => Device?.PGBackend.Status ?? base.DeviceStatus;
        set { if (Device == null) base.DeviceStatus = value; else Device.PGBackend.ObserveService(value); }
    }
    internal void RefreshBackendStatus() => base.DeviceStatus = Device?.PGBackend.Status ?? base.DeviceStatus;

    internal override MsgRecord PublishAsyncClient(MsgSend message, double timeout = 30000)
    {
        if (Device == null) return base.PublishAsyncClient(message, timeout);
        lock (Device.PGBackend.Sync)
        {
            if (Device.PGBackend.OpensLocally) return Device.RunLocalPGCommand(message);
            LocalCommunicationOwnership.EnsureAvailable(Device, false, Device.LocalEndpoint);
            Device.PGBackend.BeginServiceCommand(message.EventName);
        }
        try
        {
            MsgRecord record = base.PublishAsyncClient(message, timeout);
            int completed = 0;
            void Complete(object? sender, MsgRecordState state)
            {
                if (state is not (MsgRecordState.Success or MsgRecordState.Fail or MsgRecordState.Timeout) || Interlocked.Exchange(ref completed, 1) != 0) return;
                record.MsgRecordStateChanged -= Complete;
                Device.PGBackend.EndServiceCommand();
                if (state == MsgRecordState.Success && message.EventName is "Open" or "Close") DeviceStatus = message.EventName == "Close" ? DeviceStatusType.Closed : DeviceStatusType.Opened;
            }
            record.MsgRecordStateChanged += Complete;
            Complete(record, record.MsgRecordState);
            return record;
        }
        catch { Device.PGBackend.EndServiceCommand(); throw; }
    }

    public override void Dispose()
    {
        MQTTControl.ApplicationMessageReceivedAsync -= MqttClient_ApplicationMessageReceivedAsync;
        base.Dispose();
        System.GC.SuppressFinalize(this);
    }
}
