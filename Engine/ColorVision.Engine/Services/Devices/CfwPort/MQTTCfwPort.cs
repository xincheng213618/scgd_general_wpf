using ColorVision.Engine.Messages;
using System.Collections.Generic;
using ColorVision.Database;
using ColorVision.Engine.MQTT;
using System;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ColorVision.Engine.Services.Devices.CfwPort.Local;

namespace ColorVision.Engine.Services.Devices.CfwPort
{
    public class MQTTCfwPort : MQTTDeviceService<ConfigCfwPort>
    {
        private volatile bool disposed;
        internal bool IsDisposed => disposed;
        internal DeviceCfwPort? Device { get; init; }
        public override DeviceStatusType DeviceStatus
        {
            get => Device?.CfwBackend.Status ?? base.DeviceStatus;
            set { if (Device == null) base.DeviceStatus = value; else Device.CfwBackend.ObserveService(value); }
        }
        internal void RefreshBackendStatus()
        {
            var device = Device!;
            var status = device.CfwBackend.Status;
            if (!DeviceCfwPort.HasOpenConnection(status)) device.ClearConfirmedPort();
            base.DeviceStatus = status;
        }

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
            if (local)
            {
                var ownership = message.EventName == "Open" ? Device.CapturePortCommandOwnership(this) : null;
                var localRecord = Device.RunLocalCfwCommand(message);
                if (ownership != null) TrackPortCommand(localRecord, null, ownership);
                return localRecord;
            }
            try
            {
                var record = base.PublishAsyncClient(message, timeout);
                CompleteAfterTerminalState(record, state =>
                {
                    Device.CfwBackend.End(false);
                    if (state == MsgRecordState.Success && message.EventName is "Open" or "Close")
                        DeviceStatus = message.EventName == "Close" ? DeviceStatusType.Closed : DeviceStatusType.Opened;
                });
                return record;
            }
            catch { Device.CfwBackend.End(false); throw; }
        }

        private readonly object commandGate = new();
        private int calibrationOwner;
        private int pendingCommands;

        internal async Task<int?> ExecuteLocalAsync(string operation, LocalCfwPortConnection config, int? port, CancellationToken token, bool autoConnect = true)
        {
            lock (commandGate)
            {
                if (calibrationOwner != 0 && calibrationOwner != Environment.CurrentManagedThreadId)
                    throw new InvalidOperationException("滤色轮正在用于光谱采集或校零，请等待完成。");
                pendingCommands++;
            }
            try { return await Device!.ExecuteLocalCfwAsync(operation, config, port, autoConnect, token).ConfigureAwait(false); }
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

        private MsgRecord SendCommand(MsgSend msg, CalibrationRoute? route = null)
        {
            bool available;
            lock (commandGate)
            {
                available = calibrationOwner == Environment.CurrentManagedThreadId || (calibrationOwner == 0 && route == null);
                if (available) pendingCommands++;
            }
            if (!available)
                return new MsgRecord(msg.CompleteWithoutDispatcher) { MsgSend = msg, MsgReturn = new MsgReturn { Code = -1, Message = "滤色轮正在用于光谱采集或校零，请等待完成。" }, MsgRecordState = MsgRecordState.Fail };
            MsgRecord record;
            try { record = route == null ? PublishAsyncClient(msg) : PublishCalibrationCommand(msg, route); }
            catch { lock (commandGate) pendingCommands--; throw; }
            CompleteAfterTerminalState(record, _ => { lock (commandGate) pendingCommands--; });
            return record;
        }

        private static void CompleteAfterTerminalState(MsgRecord record, Action<MsgRecordState> onTerminal)
        {
            if (record.WorkerCompletion != null)
            {
                record.BeforeWorkerCompletion(onTerminal);
                return;
            }

            int completed = 0;
            void Complete(object? sender, MsgRecordState state)
            {
                if (state is not (MsgRecordState.Success or MsgRecordState.Fail or MsgRecordState.Timeout)
                    || Interlocked.Exchange(ref completed, 1) != 0) return;
                record.MsgRecordStateChanged -= Complete;
                onTerminal(state);
            }
            record.MsgRecordStateChanged += Complete;
            Complete(record, record.MsgRecordState);
        }

        public MQTTCfwPort(ConfigCfwPort config) : this(config, MQTTControl.GetInstance())
        {
        }

        internal MQTTCfwPort(ConfigCfwPort config, MQTTControl mqttControl) : base(config, mqttControl)
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

            var ownership = Device?.CapturePortCommandOwnership(this);
            MsgRecord record = SendCommand(msg);
            TrackPortCommand(record, port, ownership);
            return record;
        }

        internal sealed record CalibrationRoute(MQTTCfwPort Service, DeviceCfwPort? Device, DeviceConfigurationTarget? DeviceTarget,
            MySqlConnectedEndpoint? Database, LocalCfwPortConnection Connection, MqttServiceRoute MqttRoute);

        internal CalibrationRoute CaptureCalibrationRoute()
        {
            DeviceCfwPort? device = Device;
            var route = new CalibrationRoute(this, device, device?.CaptureConfigurationTarget(), device?.ResultDatabaseEndpoint,
                LocalCfwPortConnection.From(Config), CaptureMqttRoute(pinConnection: true));
            RequireCalibrationRoute(route);
            return route;
        }

        internal bool IsCalibrationRouteUsable(CalibrationRoute route) => !disposed && ReferenceEquals(route.Service, this)
            && ReferenceEquals(Device, route.Device) && IsMqttRouteUsable(route.MqttRoute)
            && (route.Device is not { } device || (ReferenceEquals(device.DService, this)
                && ReferenceEquals(device.ResultDatabaseEndpoint, route.Database) && route.DeviceTarget?.IsCurrent(device) == true));

        private void RequireCalibrationRoute(CalibrationRoute route)
        {
            if (!IsCalibrationRouteUsable(route)) throw new InvalidOperationException("原校零滤色轮服务已不可用，请检查光路。");
        }

        private MsgRecord PublishCalibrationCommand(MsgSend message, CalibrationRoute route)
        {
            RequireCalibrationRoute(route);
            if (route.Device is not { } owner) return PublishOnMqttRoute(message, route.MqttRoute);
            lock (CfwPortBackendState.Sync)
            {
                owner.EnsureOtherCfwBackends(false, route.Connection);
                owner.CfwBackend.Begin(false, message.EventName, route.Connection.PortName);
            }
            try
            {
                RequireCalibrationRoute(route);
                var record = PublishOnMqttRoute(message, route.MqttRoute);
                CompleteAfterTerminalState(record, state =>
                {
                    owner.CfwBackend.End(false);
                    if (state == MsgRecordState.Success && message.EventName == "Open"
                        && IsCalibrationRouteUsable(route) && CaptureMqttRoute(pinConnection: true) == route.MqttRoute)
                        owner.CfwBackend.ObserveService(DeviceStatusType.Opened);
                });
                return record;
            }
            catch { owner.CfwBackend.End(false); throw; }
        }

        internal MsgRecord SendCalibrationCommand(string operation, int? port = null) =>
            SendCommand(CreateCalibrationMessage(operation, port));

        internal MsgRecord SendCalibrationCommand(CalibrationRoute route, string operation, int? port = null) =>
            SendCommand(CreateCalibrationMessage(operation, port), route);

        private static MsgSend CreateCalibrationMessage(string operation, int? port)
        {
            var message = new MsgSend { EventName = operation, CompleteWithoutDispatcher = true };
            if (operation != "Open")
                message.Params = port.HasValue ? new Dictionary<string, object> { ["PortNum"] = port.Value } : new Dictionary<string, object>();
            return message;
        }

        internal static int? ReadPortReply(MsgRecord record, int? requestedPort)
        {
            if (record.MsgReturn?.Data is JObject data && data["Port"] is JValue { Type: JTokenType.Integer } value
                && value.Value<long>() is >= 0 and <= int.MaxValue)
                return value.Value<int>();
            return requestedPort;
        }

        private void TrackPortCommand(MsgRecord record, int? requestedPort, Func<bool>? ownership)
        {
            if (Device is not { } owner || ownership == null) return;
            int completed = 0;
            void Complete(object? sender, MsgRecordState state)
            {
                if (state is not (MsgRecordState.Success or MsgRecordState.Fail or MsgRecordState.Timeout)
                    || Interlocked.Exchange(ref completed, 1) != 0) return;
                record.MsgRecordStateChanged -= Complete;
                if (state != MsgRecordState.Success) return;

                void Apply()
                {
                    if (ownership() && ReadPortReply(record, requestedPort) is int port)
                        owner.UpdateConfirmedPort(port);
                }
                if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
                    dispatcher.BeginInvoke(Apply);
                else Apply();
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

            var ownership = Device?.CapturePortCommandOwnership(this);
            MsgRecord record = SendCommand(msg);
            TrackPortCommand(record, null, ownership);
            return record;
        }

        public override void Dispose()
        {
            disposed = true;
            base.Dispose();
        }

    }
}
