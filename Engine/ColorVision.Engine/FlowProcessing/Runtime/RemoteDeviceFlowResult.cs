using ColorVision.Database;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.SMU;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.SMU.Local;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Results;
using FlowEngineLib.Base;
using FlowEngineLib.Node.SMU;
using FlowEngineLib.SMU;
using MQTTMessageLib.Camera;
using MQTTnet;
using Newtonsoft.Json.Linq;
using System;
using System.Runtime.CompilerServices;
using CVServerResponse = FlowEngineLib.CVServerResponse;

namespace ColorVision.Engine.FlowProcessing;

internal static class RemoteDeviceFlowResult
{
    private const int CameraMasterResultType = 100;

    [ModuleInitializer]
    internal static void Register() => RemoteFlowResultTracking.BindRegisteredResult = CreateBinder();

    internal static RemoteFlowResultBinder CreateBinder(Func<string, DeviceService?>? findDevice = null) =>
        (flow, request, sendTopic, replyTopic, matchesBroker) =>
        {
            string deviceCode = request.DeviceCode;
            string eventName = request.EventName;
            if (string.IsNullOrWhiteSpace(deviceCode) || string.IsNullOrWhiteSpace(sendTopic) || string.IsNullOrWhiteSpace(replyTopic)
                || FindSource() is not { } owner || !SupportsEvent(owner, eventName)
                || owner.GetMQTTService() is not { } service) return null!;
            var endpoint = owner.ResultDatabaseEndpoint;
            if (FlowMeasurementBatch.TryGetBoundBatchEndpoint(flow, out MySqlConnectedEndpoint? batchEndpoint)
                && !ReferenceEquals(endpoint, batchEndpoint)) return null!;

            if (!IsActive()) return null!;

            var route = service.CaptureMqttRoute(pinConnection: true);
            var target = owner.CaptureConfigurationTarget();
            if (!IsTargetCurrent() || !string.Equals(owner.Code, deviceCode, StringComparison.Ordinal)
                || !string.Equals(route.DeviceCode, deviceCode, StringComparison.Ordinal)
                || !string.Equals(route.SendTopic, sendTopic, StringComparison.Ordinal)
                || !string.Equals(route.ReplyTopic, replyTopic, StringComparison.Ordinal)) return null!;

            if (owner is DeviceSMU smu && service is MQTTSMU mqttSmu)
                return BindSmuResult(smu, mqttSmu, request, endpoint, route, matchesBroker, OwnsSource)!;
            if (endpoint?.IsCurrent != true) return null!;

            string serialNumber = request.SerialNumber;
            string nodeId = request.DeviceNodeCode;
            int zIndex = request.ZIndex;
            return response =>
            {
                if (!OwnsSource() || !matchesBroker(route.ConnectionOptions!) || (object?)response.Data is not JObject data)
                    return;

                if (owner is DeviceSpectrum spectrum && service is MQTTSpectrum mqttSpectrum)
                {
                    // The reply has arrived on the original connection. The queued result can outlive the flow.
                    if (MQTTSpectrum.ReadServiceResultId(data) is int resultId)
                        mqttSpectrum.EnqueueOwnedResult(spectrum, eventName, resultId, endpoint, OwnsSource);
                    return;
                }

                if (!MsgReturn.TryReadInteger(data["MasterId"], out int masterId) || masterId <= 0) return;
                (string routeName, string kind, int defaultType) = service is MQTTCamera
                    ? (ResultRoutes.Camera, ResultKinds.Image, CameraMasterResultType)
                    : (ResultRoutes.Algorithm, ResultKinds.Algorithm, (int)ViewResultAlgType.None);

                int resultType = MsgReturn.TryReadInteger(data["MasterResultType"], out int reportedType)
                    ? reportedType : defaultType;
                ResultMessageBus.Default.PublishPersisted(routeName, kind, deviceCode,
                    eventName, serialNumber, nodeId, zIndex, masterId, resultType, databaseEndpoint: endpoint,
                    sourceDevice: owner, sourceService: service is MQTTCamera ? service : null);
            };

            DeviceService? FindSource() => findDevice != null ? findDevice(deviceCode) : FindRegisteredDevice(deviceCode, eventName, sendTopic, replyTopic);

            bool IsTargetCurrent() => owner is DeviceSMU && endpoint == null ? target.IsCurrentResource(owner) : target.IsCurrent(owner);

            bool OwnsSource() => IsActive() && ReferenceEquals(FindSource(), owner) && ReferenceEquals(owner.GetMQTTService(), service)
                && IsTargetCurrent() && ReferenceEquals(owner.ResultDatabaseEndpoint, endpoint) && (endpoint == null || endpoint.IsCurrent)
                && route.HasSameDestination(service.CaptureMqttRoute(pinConnection: true));

            bool IsActive() => (owner, service) switch
            {
                (DeviceAlgorithm algorithm, MQTTAlgorithm) => !algorithm.IsDisposed,
                (DeviceCamera camera, MQTTCamera mqttCamera) => mqttCamera.CanPresentFeedback(camera, serviceOnly: true),
                (DeviceSMU smu, MQTTSMU mqttSmu) => mqttSmu.OwnsRemoteDevice(smu),
                (DeviceSpectrum spectrum, MQTTSpectrum mqttSpectrum) => mqttSpectrum.OwnsDevice(spectrum) && !spectrum.SpectrumBackend.OpensLocally,
                _ => false,
            };
        };

    private static Action<CVServerResponse>? BindSmuResult(DeviceSMU owner, MQTTSMU service, CVMQTTRequest request,
        MySqlConnectedEndpoint? endpoint, MqttServiceRoute route, Func<MqttClientOptions, bool> matchesBroker, Func<bool> ownsSource)
    {
        string eventName = request.EventName;
        bool persistedScan = eventName != "Scan" || service.UsesPersistedScanResults;
        var sweep = (request.Data as SMUSweepParam)?.DeviceParam;
        MQTTSMU.ScanResultParameters? inlineScan = sweep == null ? null : new(sweep.IsSourceV, sweep.EndValue);
        if ((persistedScan && endpoint?.IsCurrent != true) || (!persistedScan && inlineScan == null)
            || !service.IsMqttRouteUsable(route)) return null;

        var channel = (request.Data as SMUData)?.Channel ?? sweep?.Channel;
        SMUChannelType? resultChannel = channel.HasValue ? (SMUChannelType)channel.Value : null;
        (long A, long B)? readoutResets = eventName == "GetData" ? owner.SmuBackend.CaptureReadoutResets() : null;
        var config = owner.Config;
        var deviceAddress = LocalSmuConnection.From(config);
        bool OwnsResult() => ownsSource() && service.IsMqttRouteUsable(route) && ReferenceEquals(owner.Config, config)
            && LocalSmuConnection.From(config) == deviceAddress;

        return response =>
        {
            if (OwnsResult() && matchesBroker(route.ConnectionOptions!) && (object?)response.Data is JObject data)
                service.EnqueueOwnedResult(owner, eventName, endpoint, data, OwnsResult, resultChannel, persistedScan, inlineScan,
                    readoutResets: readoutResets);
        };
    }

    private static DeviceService? FindRegisteredDevice(string deviceCode, string eventName, string sendTopic, string replyTopic)
    {
        if (ServiceManager.Current?.DeviceServices is not { } devices) return null;
        DeviceService? owner = null;
        foreach (DeviceService candidate in devices)
        {
            if (!string.Equals(candidate.Code, deviceCode, StringComparison.Ordinal) || !SupportsEvent(candidate, eventName)
                || candidate.GetMQTTService() is not { } service
                || !string.Equals(service.SendTopic, sendTopic, StringComparison.Ordinal)
                || !string.Equals(service.SubscribeTopic, replyTopic, StringComparison.Ordinal)) continue;
            if (owner != null && !ReferenceEquals(owner, candidate)) return null;
            owner = candidate;
        }
        return owner;
    }

    private static bool SupportsEvent(DeviceService device, string eventName) => device switch
    {
        DeviceAlgorithm => true,
        DeviceCamera => eventName == MQTTCameraEventEnum.Event_GetData,
        DeviceSMU => eventName is "GetData" or "Scan",
        DeviceSpectrum => eventName is "GetData" or "EQE.GetData",
        _ => false,
    };
}
