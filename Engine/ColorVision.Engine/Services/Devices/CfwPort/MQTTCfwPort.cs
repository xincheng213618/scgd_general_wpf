using ColorVision.Engine.Messages;
using System.Collections.Generic;
using System;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Threading.Tasks;
using ColorVision.Engine.Services.Devices.CfwPort.Local;

namespace ColorVision.Engine.Services.Devices.CfwPort
{
    public class MQTTCfwPort : MQTTDeviceService<ConfigCfwPort>
    {
        internal DeviceCfwPort? Device { get; init; }
        public override DeviceStatusType DeviceStatus
        {
            get => Device?.CfwBackend.Status ?? base.DeviceStatus;
            set { if (Device == null) base.DeviceStatus = value; else Device.CfwBackend.ObserveService(value); }
        }
        internal void RefreshBackendStatus() => base.DeviceStatus = Device!.CfwBackend.Status;

        internal override MsgRecord PublishAsyncClient(MsgSend message, double timeout = 30000)
        {
            if (Device == null) return base.PublishAsyncClient(message, timeout);
            bool local;
            lock (CfwPortBackendState.Sync)
            {
                local = Device.CfwBackend.OpensLocally;
                if (!local)
                {
                    var config = LocalCfwPortConnection.From(Config);
                    if (message.EventName != "Close") Device.EnsureOtherCfwBackends(false, config);
                    Device.CfwBackend.Begin(false, message.EventName, config.PortName);
                }
            }
            if (local) return Device.RunLocalCfwCommand(message);
            try
            {
                var record = base.PublishAsyncClient(message, timeout);
                int completed = 0;
                void Complete(object? sender, MsgRecordState state)
                {
                    if (state is not (MsgRecordState.Success or MsgRecordState.Fail or MsgRecordState.Timeout) || Interlocked.Exchange(ref completed, 1) != 0) return;
                    record.MsgRecordStateChanged -= Complete;
                    Device.CfwBackend.End(false);
                    if (state == MsgRecordState.Success && message.EventName is "Open" or "Close")
                        DeviceStatus = message.EventName == "Close" ? DeviceStatusType.Closed : DeviceStatusType.Opened;
                }
                record.MsgRecordStateChanged += Complete;
                Complete(record, record.MsgRecordState);
                return record;
            }
            catch { Device.CfwBackend.End(false); throw; }
        }

        private readonly object commandGate = new();
        private int calibrationOwner;
        private int pendingCommands;

        internal async Task<int?> ExecuteLocalAsync(string operation, LocalCfwPortConnection config, int? port, CancellationToken token)
        {
            lock (commandGate)
            {
                if (calibrationOwner != 0 && calibrationOwner != Environment.CurrentManagedThreadId)
                    throw new InvalidOperationException("滤色轮正在用于光谱采集或校零，请等待完成。");
                pendingCommands++;
            }
            try { return await Device!.ExecuteLocalCfwAsync(operation, config, port, true, token).ConfigureAwait(false); }
            finally { lock (commandGate) pendingCommands--; }
        }

        internal IDisposable ReserveForCalibration()
        {
            lock (commandGate)
            {
                if (calibrationOwner != 0 || pendingCommands != 0)
                    throw new InvalidOperationException("滤色轮正在执行其他操作，请稍后再校零。");
                calibrationOwner = Environment.CurrentManagedThreadId;
                return new CalibrationReservation(this);
            }
        }

        private sealed class CalibrationReservation(MQTTCfwPort owner) : IDisposable
        {
            private MQTTCfwPort? owner = owner;
            public void Dispose()
            {
                var current = Interlocked.Exchange(ref owner, null);
                if (current == null) return;
                lock (current.commandGate) current.calibrationOwner = 0;
            }
        }

        private MsgRecord SendCommand(MsgSend msg)
        {
            bool reserved;
            lock (commandGate)
            {
                reserved = calibrationOwner != 0 && calibrationOwner != Environment.CurrentManagedThreadId;
                if (!reserved) pendingCommands++;
            }
            if (reserved)
                return new MsgRecord { MsgSend = msg, MsgReturn = new MsgReturn { Code = -1, Message = "滤色轮正在用于光谱采集或校零，请等待完成。" }, MsgRecordState = MsgRecordState.Fail };
            MsgRecord record;
            try { record = PublishAsyncClient(msg); }
            catch { lock (commandGate) pendingCommands--; throw; }
            int completed = 0;
            void Complete(object? sender, MsgRecordState state)
            {
                if (state is not (MsgRecordState.Success or MsgRecordState.Fail or MsgRecordState.Timeout)
                    || Interlocked.Exchange(ref completed, 1) != 0) return;
                record.MsgRecordStateChanged -= Complete;
                lock (commandGate) pendingCommands--;
            }
            record.MsgRecordStateChanged += Complete;
            Complete(record, record.MsgRecordState);
            return record;
        }

        public MQTTCfwPort(ConfigCfwPort config) : base(config)
        {
            MsgReturnReceived += ProcessingReceived;
            DeviceStatus = DeviceStatusType.Closed;
        }

        private void ProcessingReceived(MsgReturn msg)
        {
            if (!string.IsNullOrEmpty(msg.DeviceCode) && msg.DeviceCode != DeviceCode) return;
            if (msg.Code == 0)
            {
                switch (msg.EventName)
                {
                    case "Open":
                        DeviceStatus = DeviceStatusType.Opened;
                        break;
                    case "SetPort":
                        break;
                    case "GetPort":
                        break;
                    case "Close":
                        DeviceStatus = DeviceStatusType.Closed;
                        break;
                    default:
                        break;
                }
            }
            // A failed move/read/close is not proof that the service released its serial handle.
        }

        public MsgRecord Clsoe()
        {

            MsgSend msg = new MsgSend()
            {
                EventName = "Close",
            };

            return SendCommand(msg);
        }


        public MsgRecord Open()
        {

            MsgSend msg = new MsgSend()
            {
                EventName = "Open",
            };

            return SendCommand(msg);
        }

        public MsgRecord SetPort(int port)
        {

            MsgSend msg = new()
            {
                EventName = "SetPort",
                Params = new Dictionary<string, object>() { { "PortNum", port } }
            };

            MsgRecord record = SendCommand(msg);
            TrackPortCommand(record, port);
            return record;
        }

        private void TrackPortCommand(MsgRecord record, int? requestedPort)
        {
            int completed = 0;
            void Complete(object? sender, MsgRecordState state)
            {
                if (state is not (MsgRecordState.Success or MsgRecordState.Fail or MsgRecordState.Timeout)
                    || Interlocked.Exchange(ref completed, 1) != 0) return;
                record.MsgRecordStateChanged -= Complete;
                if (state != MsgRecordState.Success) return;

                int? port = requestedPort;
                if (record.MsgReturn?.Data is JObject data && data["Port"] is JValue { Type: JTokenType.Integer } value
                    && value.Value<long>() is >= 0 and <= int.MaxValue)
                    port = value.Value<int>();
                if (port.HasValue)
                    DisplayConfigManager.Instance.GetDisplayConfig<DisplayCfwPortConfig>(DeviceCode).PortNum = port;
            }
            record.MsgRecordStateChanged += Complete;
            Complete(record, record.MsgRecordState);
        }

        public MsgRecord GetPort()
        {

            MsgSend msg = new()
            {
                EventName = "GetPort",
                Params = new Dictionary<string, object>() { }
            };

            MsgRecord record = SendCommand(msg);
            TrackPortCommand(record, null);
            return record;
        }

    }
}
