using ColorVision.Engine.Messages;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Services.Results;
using MQTTMessageLib;
using MQTTMessageLib.Calibration;
using MQTTMessageLib.FileServer;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.Devices.Calibration
{
    public class MQTTCalibration : MQTTDeviceService<ConfigCalibration>
    {
        private readonly DeviceCalibration? owner;
        private volatile bool _isDisposed;
        internal bool IsDisposed => _isDisposed;

        public MQTTCalibration(ConfigCalibration config) : this(config, MQTTControl.GetInstance()) { }

        internal MQTTCalibration(DeviceCalibration owner, MQTTControl? control = null)
            : this(owner.Config, control ?? MQTTControl.GetInstance())
        {
            this.owner = owner;
        }

        private MQTTCalibration(ConfigCalibration config, MQTTControl control) : base(config, control)
        {
            DeviceStatus = DeviceStatusType.Unknown;
        }

        public MsgRecord Calibration(CalibrationParam item, string fileName, FileExtType fileExtType, int pid, string tempName, string serialNumber, float R, float G, float B)
        {
            string sn = string.IsNullOrWhiteSpace(serialNumber)
                ? DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff") : serialNumber;
            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = pid, Name = tempName });
            Params.Add("DeviceParam", new DeviceParamCalibration() { exp = new float[] { R, G, B }, gain = 1, });

            MsgSend msg = new()
            {
                EventName = MQTTCalibrationEventEnum.Event_GetData,
                SerialNumber = sn,
                Params = Params,
                CompleteWithoutDispatcher = owner != null,
            };
            if (owner == null) return PublishAsyncClient(msg);

            var target = owner.CaptureConfigurationTarget();
            var endpoint = owner.ResultDatabaseEndpoint is { IsCurrent: true } currentEndpoint ? currentEndpoint : null;
            var route = CaptureMqttRoute(pinConnection: true);
            bool OwnsRequest() => !_isDisposed && !owner.IsDisposed && ReferenceEquals(owner.DService, this)
                && target.IsCurrent(owner) && CaptureMqttRoute(pinConnection: true) == route && IsMqttRouteUsable(route);

            if (!OwnsRequest())
                throw new InvalidOperationException(EngineLocalization.Get("原校正设备或 MQTT 连接已失效，请重新选择设备后重试。"));

            MsgRecord record = PublishOnMqttRoute(msg, route, canDispatch: OwnsRequest,
                acceptsReply: reply => AcceptsCalibrationReply(reply, route.DeviceCode, sn));
            record.BeforeWorkerCompletion(state =>
            {
                if (state != MsgRecordState.Success || endpoint?.IsCurrent != true
                    || !ReferenceEquals(owner.ResultDatabaseEndpoint, endpoint) || !OwnsRequest()
                    || record.MsgReturn?.Data is not JObject data || !TryReadPositiveInt(data["MasterId"], out int masterId)) return;

                int resultType = TryReadPositiveInt(data["MasterResultType"], out int reportedType)
                    ? reportedType : (int)ViewResultAlgType.Calibration;
                ResultMessageBus.Default.PublishPersisted(ResultRoutes.Calibration, ResultKinds.Image, route.DeviceCode,
                    MQTTCalibrationEventEnum.Event_GetData, sn, string.Empty, -1, masterId, resultType, databaseEndpoint: endpoint, sourceDevice: owner);
            });
            return record;
        }

        private static bool AcceptsCalibrationReply(MsgReturn reply, string deviceCode, string serialNumber)
        {
            if (!reply.HasCode || (!string.IsNullOrEmpty(reply.DeviceCode) && !string.Equals(reply.DeviceCode, deviceCode, StringComparison.Ordinal))
                || (!string.IsNullOrEmpty(reply.SerialNumber) && !string.Equals(reply.SerialNumber, serialNumber, StringComparison.Ordinal))) return false;

            bool matchesEvent = string.Equals(reply.EventName, MQTTCalibrationEventEnum.Event_GetData, StringComparison.Ordinal);
            if (reply.Code != 0) return matchesEvent || string.IsNullOrEmpty(reply.EventName);
            return matchesEvent && reply.Data is JObject data && TryReadPositiveInt(data["MasterId"], out _);
        }

        internal static bool TryReadPositiveInt(JToken? token, out int value) => MsgReturn.TryReadInteger(token, out value) && value > 0;

        public MsgRecord CacheClear()
        {
            MsgSend msg = new()
            {
                EventName = MQTTCalibrationEventEnum.Event_Delete_Data,
                Params = new Dictionary<string, object> { }
            };
            return PublishAsyncClient(msg);
        }

        public override void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            MsgReturnReceived = null!;
            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
