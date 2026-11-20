using ColorVision.Engine.Messages;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.Results;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    public class MQTTAlgorithm : MQTTDeviceService<ConfigAlgorithm>
    {
        private readonly DeviceAlgorithm? owner;

        public MQTTAlgorithm(ConfigAlgorithm config) : this(config, MQTTControl.GetInstance()) { }

        internal MQTTAlgorithm(DeviceAlgorithm owner, MQTTControl? control = null)
            : this(owner.Config, control ?? MQTTControl.GetInstance())
        {
            this.owner = owner;
        }

        private MQTTAlgorithm(ConfigAlgorithm config, MQTTControl control) : base(config, control)
        {
            DeviceStatus = DeviceStatusType.Unknown;
        }

        internal override MsgRecord PublishAsyncClient(MsgSend message, double timeout = 30000)
        {
            if (owner == null || !IsCalculation(message)) return base.PublishAsyncClient(message, timeout);

            var route = CaptureMqttRoute(pinConnection: true);
            if (!string.IsNullOrEmpty(message.DeviceCode) && !string.Equals(message.DeviceCode, route.DeviceCode, StringComparison.Ordinal))
                return base.PublishAsyncClient(message, timeout);

            var target = owner.CaptureConfigurationTarget();
            var endpoint = owner.ResultDatabaseEndpoint is { IsCurrent: true } currentEndpoint ? currentEndpoint : null;
            string eventName = message.EventName;
            string serialNumber = message.SerialNumber;
            bool OwnsRequest(bool beforeDispatch)
            {
                var current = CaptureMqttRoute(pinConnection: true);
                return !owner.IsDisposed && ReferenceEquals(owner.DService, this) && target.IsCurrent(owner)
                    && route.HasSameDestination(current) && IsMqttRouteUsable(route)
                    && (!beforeDispatch || string.Equals(route.ServiceToken, current.ServiceToken, StringComparison.Ordinal));
            }

            if (!OwnsRequest(beforeDispatch: true))
                throw new InvalidOperationException(EngineLocalization.Get("原算法设备或 MQTT 连接已失效，请重新选择设备后重试。"));

            message.CompleteWithoutDispatcher = true;
            MsgRecord record = PublishOnMqttRoute(message, route, timeout, canDispatch: () => OwnsRequest(beforeDispatch: true),
                acceptsReply: reply => AcceptsCalculationReply(reply, route.DeviceCode, eventName, serialNumber));
            record.BeforeWorkerCompletion(state =>
            {
                if (state != MsgRecordState.Success || record.MsgReturn is not { HasCode: true, Code: 0 } reply
                    || reply.Data is not JObject data || !MsgReturn.TryReadInteger(data["MasterId"], out int masterId) || masterId <= 0
                    || endpoint?.IsCurrent != true || !ReferenceEquals(owner.ResultDatabaseEndpoint, endpoint) || !OwnsRequest(beforeDispatch: false)) return;

                int resultType = MsgReturn.TryReadInteger(data["MasterResultType"], out int reportedType) ? reportedType : (int)ViewResultAlgType.None;
                ResultMessageBus.Default.PublishPersisted(ResultRoutes.Algorithm, ResultKinds.Algorithm, route.DeviceCode,
                    eventName, string.IsNullOrWhiteSpace(serialNumber) ? reply.SerialNumber : serialNumber, string.Empty,
                    reply.ZIndex, masterId, resultType, databaseEndpoint: endpoint, sourceDevice: owner);
            });
            return record;
        }

        // The manual templates submit the image and template together. Service control commands have a separate payload.
        private static bool IsCalculation(MsgSend message) => (object?)message.Params switch
        {
            IDictionary<string, object> parameters => parameters.ContainsKey("ImgFileName") && parameters.ContainsKey("TemplateParam"),
            JObject parameters => parameters.ContainsKey("ImgFileName") && parameters.ContainsKey("TemplateParam"),
            _ => false,
        };

        private static bool AcceptsCalculationReply(MsgReturn reply, string deviceCode, string eventName, string serialNumber)
        {
            if (!reply.HasCode || (!string.IsNullOrEmpty(reply.DeviceCode) && !string.Equals(reply.DeviceCode, deviceCode, StringComparison.Ordinal))
                || (!string.IsNullOrEmpty(reply.EventName) && !string.Equals(reply.EventName, eventName, StringComparison.Ordinal))
                || (!string.IsNullOrWhiteSpace(serialNumber) && !string.IsNullOrEmpty(reply.SerialNumber)
                    && !string.Equals(reply.SerialNumber, serialNumber, StringComparison.Ordinal))) return false;

            // Some older replies acknowledge a successful calculation without a persisted record.
            if (reply.Code == 0 && reply.Data is JObject data && data["MasterId"] is { Type: not (JTokenType.Null or JTokenType.Undefined) } id)
                return MsgReturn.TryReadInteger(id, out _);

            return true;
        }

    }
}
