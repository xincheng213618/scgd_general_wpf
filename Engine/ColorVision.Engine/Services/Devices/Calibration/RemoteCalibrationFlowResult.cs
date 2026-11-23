using ColorVision.Database;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.Services.Results;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Start;
using Newtonsoft.Json.Linq;
using System;
using System.Runtime.CompilerServices;

namespace ColorVision.Engine.Services.Devices.Calibration;

internal static class RemoteCalibrationFlowResult
{
    [ModuleInitializer]
    internal static void Register() => CalibrationFlowResultTracking.BindResult = CreateBinder(LocalCalibrationFlowRouting.Find);

    internal static CalibrationResultBinder CreateBinder(Func<string, DeviceCalibration?> findDevice) =>
        (flow, start, request, sendTopic, replyTopic) =>
        {
            string deviceCode = request.DeviceCode;
            if (findDevice(deviceCode) is not { } owner || owner.ResultDatabaseEndpoint is not { } endpoint
                || string.IsNullOrWhiteSpace(sendTopic) || string.IsNullOrWhiteSpace(replyTopic))
                return null!;
            if (FlowMeasurementBatch.TryGetBoundBatchEndpoint(flow, out MySqlConnectedEndpoint? batchEndpoint)
                && !ReferenceEquals(endpoint, batchEndpoint))
                return null!;

            MQTTCalibration service = owner.DService;
            DeviceConfigurationTarget target = owner.CaptureConfigurationTarget();
            if (!IsCurrent()) return null!;

            string eventName = request.EventName;
            string serialNumber = request.SerialNumber;
            string nodeId = request.DeviceNodeCode;
            int zIndex = request.ZIndex;
            return response =>
            {
                if (!IsCurrent() || (object?)response.Data is not JObject data
                    || !MQTTCalibration.TryReadPositiveInt(data["MasterId"], out int masterId)) return;
                int resultType = MQTTCalibration.TryReadPositiveInt(data["MasterResultType"], out int reportedType)
                    ? reportedType : (int)ViewResultAlgType.Calibration;
                ResultMessageBus.Default.PublishPersisted(ResultRoutes.Calibration, ResultKinds.Image, deviceCode,
                    eventName, serialNumber, nodeId, zIndex, masterId, resultType, databaseEndpoint: endpoint, sourceDevice: owner);
            };

            bool IsCurrent() => !owner.IsDisposed && !service.IsDisposed && ReferenceEquals(owner.DService, service)
                && string.Equals(owner.Code, deviceCode, StringComparison.Ordinal)
                && string.Equals(service.DeviceCode, deviceCode, StringComparison.Ordinal)
                && string.Equals(service.SendTopic, sendTopic, StringComparison.Ordinal)
                && string.Equals(service.SubscribeTopic, replyTopic, StringComparison.Ordinal)
                && ReferenceEquals(owner.ResultDatabaseEndpoint, endpoint) && endpoint.IsCurrent && target.IsCurrent(owner)
                && (start is not MQTTStartNode mqtt || mqtt.MatchesConnectedBroker(service.MQTTControl.MQTTClient.Options));
        };
}
