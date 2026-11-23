#pragma warning disable CA1507,CS8602
using ColorVision.Database;
using ColorVision.Engine.Messages;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Local;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Engine.Services.RC;
using MQTTMessageLib;
using MQTTMessageLib.Spectrum;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Spectrum
{
    public class GetDataParam
    {
        [JsonProperty("IntegralTime")]
        public float IntTime { get; set; }
        [JsonProperty("NumberOfAverage")]
        public int AveNum { get; set; }
        [JsonProperty("AutoIntegration")]
        public bool BUseAutoIntTime { get; set; }
        [JsonProperty("SelfAdaptionInitDark")]
        public bool SelfAdaptionInitDark { get; set; }

        public bool AutoInitDark { get; set; }

        public bool IsWithND { get; set; }
    }

    public class MQTTSpectrum : MQTTDeviceService<ConfigSpectrum>
    {
        private static readonly Version LastInlineResultVersion = new(4, 0, 1, 104);
        private const int ResultBacklogWarningThreshold = 256;
        private readonly object _resultQueueLock = new();
        private readonly Queue<ResultNotice> _resultQueue = new();
        private readonly Func<MySqlConnectedEndpoint, int, SpectumResultEntity?> _lookupResult;
        private readonly Func<Version> _serviceVersion;
        private readonly Func<DeviceSpectrum, Action<ViewResultSpectrum>?> _capturePresentation;
        private readonly Func<SpectumResultEntity, ViewResultSpectrum> _materializePersistedResult;
        private bool _resultDrainScheduled;
        private bool _resultBacklogReported;
        private volatile bool _retired;

        public DeviceSpectrum Device { get; set; }

        public override DeviceStatusType DeviceStatus
        {
            get => Device?.SpectrumBackend.Status ?? base.DeviceStatus;
            set
            {
                if (Device == null) { base.DeviceStatus = value; return; }
                Device.SpectrumBackend.ObserveService(value);
                if (!Device.SpectrumBackend.OpensLocally) Device.ObserveSpectrumDeviceStatus(value);
            }
        }

        internal void RefreshBackendStatus() => base.DeviceStatus = Device.SpectrumBackend.Status;

        internal override MsgRecord PublishAsyncClient(MsgSend msg, double timeout = 30000)
        {
            DeviceSpectrum owner = Device;
            return PublishSpectrumCommand(owner, msg, timeout, resultEndpoint: owner.ResultDatabaseEndpoint);
        }

        private MsgRecord PublishSpectrumCommand(DeviceSpectrum owner, MsgSend msg, double timeout, Func<string?>? getRejection = null,
            LocalSpectrumOpticalScope? ndOptics = null, MySqlConnectedEndpoint? resultEndpoint = null, MeasurementResultCapture? resultCapture = null)
        {
            string? GetRejection() => ndOptics != null && (!OwnsDevice(owner) || !ndOptics.IsNdCurrent)
                ? DeviceSpectrum.NdConfigurationChangedMessage : getRejection?.Invoke();
            lock (owner.SpectrumBackend.Sync)
            {
                if (GetRejection() is { } rejection) return CreateRejectedMeasurementRecord(msg.EventName, rejection);
                if (msg.EventName is "SetPort" or "GetPort")
                {
                    bool localNd = ndOptics == null ? owner.UsesLocalNd
                        : owner.SpectrumBackend.OpensLocally || ndOptics.NdDevice?.CfwBackend.OpensLocally == true;
                    if (localNd) return owner.RunLocalNdCommand(msg.EventName,
                        msg.EventName == "SetPort" ? JObject.FromObject(msg.Params).Value<int>("PortNum") : null, ndOptics);
                }
                if (owner.SpectrumBackend.OpensLocally) return owner.RunLocalSpectrumCommand(msg.EventName);
                if (Config.IsShutterEnable && Config.ShutterCfg.UseFilterWheel && msg.EventName is not ("Close" or "GetDataAutoStop"))
                    return CreateRejectedMeasurementRecord(msg.EventName, "滤色轮校零仅支持本地光谱模式，请启用使用本地光谱。");
                owner.EnsureOtherSpectrumBackends(false);
                owner.SpectrumBackend.BeginServiceCommand(msg.EventName);
            }
            try
            {
                if (GetRejection() is { } rejection)
                {
                    MsgRecord rejected = CreateRejectedMeasurementRecord(msg.EventName, rejection);
                    owner.SpectrumBackend.EndServiceCommand();
                    return rejected;
                }
                if (IsSingleMeasurement(msg.EventName))
                {
                    if (PublishOwnedSingleMeasurement(owner, msg, timeout, resultEndpoint, GetRejection, resultCapture) is { } measurement)
                        return measurement;

                    MsgRecord rejected = CreateRejectedMeasurementRecord(msg.EventName, GetRejection()
                        ?? EngineLocalization.Get("原光谱仪或 MQTT 连接已失效，请重新选择设备后采集。"));
                    owner.SpectrumBackend.EndServiceCommand();
                    return rejected;
                }

                MsgRecord record = base.PublishAsyncClient(msg, timeout);
                int completed = 0;
                void Complete(object? sender, MsgRecordState state)
                {
                    if (state is not (MsgRecordState.Success or MsgRecordState.Fail or MsgRecordState.Timeout)
                        || System.Threading.Interlocked.Exchange(ref completed, 1) != 0) return;
                    record.MsgRecordStateChanged -= Complete;
                    owner.SpectrumBackend.EndServiceCommand();
                    if (state == MsgRecordState.Success && ReferenceEquals(Device, owner) && msg.EventName is "Open" or "Close")
                        DeviceStatus = msg.EventName == "Close" ? DeviceStatusType.Closed : DeviceStatusType.Opened;
                }
                record.MsgRecordStateChanged += Complete;
                Complete(record, record.MsgRecordState);
                return record;
            }
            catch { owner.SpectrumBackend.EndServiceCommand(); throw; }
        }

        private static bool IsSingleMeasurement(string? eventName) => eventName is "GetData" or "EQE.GetData";

        private MsgRecord? PublishOwnedSingleMeasurement(DeviceSpectrum owner, MsgSend msg, double timeout,
            MySqlConnectedEndpoint? endpoint, Func<string?> getRejection, MeasurementResultCapture? resultCapture)
        {
            var route = CaptureMqttRoute(pinConnection: true);
            var resource = owner.SysResourceModel;
            var target = endpoint != null ? owner.CaptureConfigurationTarget() : null;
            ConfigSpectrum config = owner.Config;
            string physicalSerial = config.SN;
            string eventName = msg.EventName;
            string? requestSerial = msg.SerialNumber;

            bool OwnsSource(bool beforeDispatch)
            {
                var currentRoute = CaptureMqttRoute(pinConnection: true);
                return CanPresentRemoteResult(owner) && ReferenceEquals(owner.Config, config)
                    && ReferenceEquals(owner.SysResourceModel, resource) && !resource.IsDelete
                    && string.Equals(owner.Code, route.DeviceCode, StringComparison.Ordinal)
                    && string.Equals(config.SN, physicalSerial, StringComparison.Ordinal)
                    && ReferenceEquals(owner.ResultDatabaseEndpoint, endpoint)
                    && (endpoint == null || (endpoint.IsCurrent && owner.IsConfigurationTargetCurrent(target!)))
                    && route.HasSameDestination(currentRoute) && IsMqttRouteUsable(route) && getRejection() == null
                    && (!beforeDispatch || string.Equals(route.ServiceToken, currentRoute.ServiceToken, StringComparison.Ordinal));
            }

            if ((!string.IsNullOrEmpty(msg.DeviceCode) && !string.Equals(msg.DeviceCode, route.DeviceCode, StringComparison.Ordinal))
                || !OwnsSource(beforeDispatch: true)) return null;

            msg.CompleteWithoutDispatcher = true;
            try
            {
                return PublishOnMqttRoute(msg, route, timeout, canDispatch: () => OwnsSource(beforeDispatch: true),
                    dispatchLock: owner.SpectrumBackend.Sync,
                    acceptsReply: reply => AcceptsSingleMeasurementReply(reply, route.DeviceCode, eventName, requestSerial),
                    beforeDispatch: PrepareRecord);
            }
            catch (InvalidOperationException) when (!OwnsSource(beforeDispatch: true)) { return null; }

            void PrepareRecord(MsgRecord record)
            {
                record.TerminalStateCommitted += PresentMeasurement;

                void PresentMeasurement(MsgRecordState state)
                {
                    record.TerminalStateCommitted -= PresentMeasurement;
                    owner.SpectrumBackend.EndServiceCommand();
                    if (state != MsgRecordState.Success || record.MsgReturn is not { HasCode: true, Code: 0 } reply) return;

                    bool OwnsResult() => OwnsSource(beforeDispatch: false);
                    if (resultCapture != null) resultCapture.OwnsRemoteSource = OwnsResult;
                    if (endpoint == null || reply.Data is not JObject data || ReadServiceResultId(data) is not int masterId) return;

                    // Programmatic capture and live presentation consume the same original database read.
                    Lazy<Task<SpectumResultEntity?>>? lookup = null;
                    if (resultCapture != null)
                    {
                        lookup = new(() => Task.Run(() => OwnsResult() ? _lookupResult(endpoint, masterId) : null));
                        resultCapture.RemoteLookup = lookup;
                    }
                    EnqueueOwnedResult(owner, eventName, masterId, endpoint, OwnsResult, lookup);
                }
            }
        }

        private static bool AcceptsSingleMeasurementReply(MsgReturn reply, string deviceCode, string eventName, string? serialNumber)
        {
            if (!reply.HasCode || (!string.IsNullOrEmpty(reply.DeviceCode) && !string.Equals(reply.DeviceCode, deviceCode, StringComparison.Ordinal))
                || (!string.IsNullOrEmpty(reply.EventName) && !string.Equals(reply.EventName, eventName, StringComparison.Ordinal))
                || (!string.IsNullOrWhiteSpace(serialNumber) && !string.IsNullOrEmpty(reply.SerialNumber)
                    && !string.Equals(reply.SerialNumber, serialNumber, StringComparison.Ordinal))) return false;

            // A successful acknowledgement can omit a persisted result, especially without a result database.
            return reply.Code != 0 || reply.Data is not JObject data || ReadServiceResultIdToken(data) is not { } resultId
                || MsgReturn.TryReadInteger(resultId, out _);
        }

        public override void Dispose()
        {
            lock (_resultQueueLock)
            {
                if (_retired) return;
                _retired = true;
                _resultQueue.Clear();
            }
            MsgReturnReceived -= ReceiveRemoteNotification;
            base.Dispose();
            GC.SuppressFinalize(this);
        }

        public MQTTSpectrum(DeviceSpectrum DeviceSpectrum)
            : this(DeviceSpectrum, MQTTControl.GetInstance(), LookupResult, ReadServiceVersion, static device => device.CaptureLiveResultPresentation())
        {
        }

        internal MQTTSpectrum(DeviceSpectrum device, MQTTControl mqttControl, Func<MySqlConnectedEndpoint, int, SpectumResultEntity?> lookupResult,
            Func<Version> serviceVersion, Func<DeviceSpectrum, Action<ViewResultSpectrum>?> capturePresentation,
            Func<SpectumResultEntity, ViewResultSpectrum>? materializePersistedResult = null)
            : base(device.Config, mqttControl)
        {
            Device = device;
            _lookupResult = lookupResult;
            _serviceVersion = serviceVersion;
            _capturePresentation = capturePresentation;
            _materializePersistedResult = materializePersistedResult ?? (static model => new ViewResultSpectrum(model));
            MQTTControl.SubscribeCache(SubscribeTopic);
            MsgReturnReceived += ReceiveRemoteNotification;
        }

        private static Version ReadServiceVersion() =>
            Version.TryParse(ServiceConfig.Instance.RegistrationCenterServiceInfo.FileVersion, out Version? version) ? version : new Version();

        private static JToken? ReadServiceResultIdToken(JObject? notificationData) =>
            notificationData?["MasterId"] is { Type: not (JTokenType.Null or JTokenType.Undefined) } current ? current
                : notificationData?["masterId"] is { Type: not (JTokenType.Null or JTokenType.Undefined) } legacy ? legacy : null;

        internal static int? ReadServiceResultId(JObject? notificationData) =>
            MsgReturn.TryReadInteger(ReadServiceResultIdToken(notificationData), out int resultId) && resultId > 0 ? resultId : null;

        internal static SpectumResultEntity? ReadServiceResult(ISqlSugarClient db, int resultId) =>
            // Other devices and subsequent samples can write before this notification is handled.
            db.Queryable<SpectumResultEntity>().First(result => result.Id == resultId);

        private static SpectumResultEntity? LookupResult(MySqlConnectedEndpoint endpoint, int resultId)
        {
            using var db = endpoint.CreateDbClient();
            return ReadServiceResult(db, resultId);
        }

        internal bool OwnsDevice(DeviceSpectrum owner) => !_retired && !owner.IsDisposed
            && ReferenceEquals(owner, Device) && ReferenceEquals(owner.DService, this);

        private bool CanPresentRemoteResult(DeviceSpectrum owner) => OwnsDevice(owner) && !owner.SpectrumBackend.OpensLocally;

        private void EnqueueStreamResult(DeviceSpectrum owner, string eventName, JObject? data)
        {
            MySqlConnectedEndpoint? endpoint = owner.ResultDatabaseEndpoint;
            if (endpoint?.IsCurrent == true && MsgReturn.TryReadInteger(data?["MasterId"], out int id) && id > 0)
                EnqueueResult(new(owner, eventName, id, null, endpoint));
        }

        internal void EnqueueOwnedResult(DeviceSpectrum owner, string eventName, int masterId,
            MySqlConnectedEndpoint endpoint, Func<bool> ownsSource, Lazy<Task<SpectumResultEntity?>>? lookup = null)
        {
            var notice = new ResultNotice(owner, eventName, masterId, null, endpoint, ownsSource, lookup);
            if (IsSingleMeasurement(eventName) && masterId > 0 && CanPresentRemoteResult(notice)) EnqueueResult(notice);
        }

        private void EnqueueResult(ResultNotice notice)
        {
            bool startDrain;
            bool reportBacklog;
            lock (_resultQueueLock)
            {
                if (_retired) return;
                _resultQueue.Enqueue(notice);
                startDrain = !_resultDrainScheduled;
                _resultDrainScheduled = true;
                reportBacklog = !_resultBacklogReported && _resultQueue.Count >= ResultBacklogWarningThreshold;
                _resultBacklogReported |= reportBacklog;
            }

            if (startDrain) _ = Task.Run(DrainResultsAsync);
            if (reportBacklog)
                log.Warn($"Spectrum result backlog reached {ResultBacklogWarningThreshold} pending notifications. Device={notice.Owner.Config.Code}");
        }

        private async Task DrainResultsAsync()
        {
            while (true)
            {
                ResultNotice notice;
                lock (_resultQueueLock)
                {
                    if (!_resultQueue.TryDequeue(out notice))
                    {
                        _resultDrainScheduled = false;
                        _resultBacklogReported = false;
                        return;
                    }
                }

                try
                {
                    if (!CanPresentRemoteResult(notice)) continue;
                    if (notice.InlineResult is { } inline)
                    {
                        await PresentResultAsync(notice, () => new ViewResultSpectrum(inline.Data), null).ConfigureAwait(false);
                        continue;
                    }

                    SpectumResultEntity? model = notice.Lookup is { } lookup
                        ? await lookup.Value.ConfigureAwait(false) : _lookupResult(notice.Endpoint!, notice.MasterId);
                    if (model == null || (notice.OwnsSource != null && !MatchesResultDevice(model, notice.Owner.Code))) continue;
                    log.Info($"{notice.EventName} MasterId:{model.Id}");
                    await PresentResultAsync(notice, () => _materializePersistedResult(model), model.IntTime ?? 0).ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    if (!_retired)
                        log.Error($"Unable to present spectrum result. Event={notice.EventName}, MasterId={notice.MasterId}, Error={error.GetType().Name}");
                }
            }
        }

        private bool CanPresentRemoteResult(ResultNotice notice) => CanPresentRemoteResult(notice.Owner)
            && (notice.InlineResult != null || (notice.Endpoint?.IsCurrent == true && ReferenceEquals(notice.Owner.ResultDatabaseEndpoint, notice.Endpoint)))
            && notice.OwnsSource?.Invoke() != false;

        private static bool MatchesResultDevice(SpectumResultEntity model, string deviceCode) =>
            string.IsNullOrWhiteSpace(model.DeviceCode) || string.Equals(deviceCode, model.DeviceCode, StringComparison.OrdinalIgnoreCase);

        private Task PresentResultAsync(ResultNotice notice, Func<ViewResultSpectrum> createResult, float? integralTime)
        {
            if (!CanPresentRemoteResult(notice) || Application.Current?.Dispatcher is not { } dispatcher)
                return Task.CompletedTask;

            // Resolve the existing view before opening external spectrum files on the queue worker.
            var present = _capturePresentation(notice.Owner);
            if (present == null && !integralTime.HasValue) return Task.CompletedTask;
            ViewResultSpectrum? result = present == null ? null : createResult();
            if (!CanPresentRemoteResult(notice)) return Task.CompletedTask;

            return dispatcher.InvokeAsync(() =>
            {
                if (!CanPresentRemoteResult(notice)) return;
                if (result != null) present!(result);
                if (integralTime is { } time && CanPresentRemoteResult(notice))
                    notice.Owner.DisplayConfig.IntTime = time;
            }).Task;
        }

        private readonly record struct ResultNotice(DeviceSpectrum Owner, string EventName, int MasterId, SpectrumData? InlineResult,
            MySqlConnectedEndpoint? Endpoint, Func<bool>? OwnsSource = null, Lazy<Task<SpectumResultEntity?>>? Lookup = null);

        private void ReceiveRemoteNotification(MsgReturn reply)
        {
            DeviceSpectrum owner = Device;
            if (!CanPresentRemoteResult(owner) || (Config.Code != null && reply.DeviceCode != Config.Code) || reply.Code is not (0 or 102)) return;

            try
            {
                switch (reply.EventName)
                {
                    case "GetDataAuto":
                    case "EQE.GetDataAuto":
                        if (_serviceVersion() > LastInlineResultVersion)
                            EnqueueStreamResult(owner, reply.EventName, (object?)reply.Data as JObject);
                        else if (reply.Data is JObject inlineData && inlineData.ToObject<SpectrumData>() is { } inlineResult)
                            EnqueueResult(new(owner, reply.EventName, 0, inlineResult, null));
                        break;
                    case "GetParam":
                        AutoIntTimeParam? parameters = JsonConvert.DeserializeObject<AutoIntTimeParam>(JsonConvert.SerializeObject(reply.Data));
                        if (parameters != null)
                        {
                            Application.Current?.Dispatcher.BeginInvoke(() =>
                            {
                                if (!CanPresentRemoteResult(owner)) return;
                                owner.Config.BeginIntegralTime = parameters.fTimeB;
                                owner.Config.MaxIntegralTime = parameters.iLimitTime;
                            });
                        }
                        else
                        {
                            log.Info("GetParam is null");
                        }
                        break;
                }
            }
            catch (Exception error)
            {
                log.Error(error);
            }
        }

        public MsgRecord GetAllSnID() => PublishAsyncClient(new MsgSend { EventName = "CM_GetAllSnID" });
        public MsgRecord GetCameraID() => PublishAsyncClient(new MsgSend { EventName = "CM_GetSnID" });

        public void GetParam()
        {
            MsgSend msg = new()
            {
                EventName = "GetParam",
                ServiceName = Config.Code,
            };
            PublishAsyncClient(msg);
        }

        public bool SetParam(int iLimitTime, float fTimeB)
        {
            MsgSend msg = new()
            {
                EventName = "SetParam",
                ServiceName = Config.Code,
                Params = new Dictionary<string, object>() { { "IntegralTime", iLimitTime }, { "NumberOfAverage", fTimeB } }
            };
            PublishAsyncClient(msg);
            return true;
        }


        public MsgRecord GetEqe()
        {
            DeviceSpectrum owner = Device;
            MySqlConnectedEndpoint? endpoint = owner.ResultDatabaseEndpoint;
            if (owner.SpectrumBackend.OpensLocally) return owner.RunLocalSpectrumCommand("EQE.GetData");
            if (!owner.TryEnterSpectrumMeasurement(out string rejectionReason))
                return CreateRejectedMeasurementRecord("EQE.GetData", rejectionReason);

            try
            {
                var Param = new Dictionary<string, object>();
                MsgSend msg = new()
                {
                    EventName = "EQE.GetData",
                    Params = Param
                };
                var settings = owner.DisplayConfig;
                Param.Add("IntegralTime", settings.IntTime);
                Param.Add("NumberOfAverage", settings.AveNum);
                Param.Add("AutoInitDark", settings.IsAutoDark);
                Param.Add("SelfAdaptionInitDark", settings.IsShutter);
                Param.Add("AutoIntegration", settings.IsAutoIntTime);
                MsgRecord msgRecord = PublishRemoteEqe(owner, endpoint, msg, Param, settings, 30000);
                owner.ReleaseSpectrumMeasurementWhenTerminal(msgRecord);
                return msgRecord;
            }
            catch
            {
                owner.ReleaseSpectrumMeasurementLease();
                throw;
            }
        }

        public MsgRecord Open()
        {
            var Params = new Dictionary<string, object>() { };

            MsgSend msg = new()
            {
                EventName = "Open",
                ServiceName = Config.Code,
                Params = Params
            };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = -1, Name = string.Empty });
            return PublishAsyncClient(msg);
        }

        public MsgRecord GetData(double timeoutMilliseconds = 30000)
        {
            DeviceSpectrum owner = Device;
            return GetSingleMeasurement(owner, owner.ResultDatabaseEndpoint, timeoutMilliseconds);
        }

        /// <summary>Runs one normal measurement and returns its exact result when available. A successful acknowledgement can have no result.</summary>
        public async Task<(bool Succeeded, SpectumResultEntity? Result)> GetDataResultAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeviceSpectrum owner = Device;
            ConfigSpectrum config = owner.Config;
            var resource = owner.SysResourceModel;
            int resourceId = resource.Id;
            string deviceCode = owner.Code;
            string physicalSerial = config.SN;
            bool local = owner.SpectrumBackend.OpensLocally;
            MySqlConnectedEndpoint? endpoint = owner.ResultDatabaseEndpoint;
            MqttServiceRoute? route = local ? null : CaptureMqttRoute(pinConnection: true);
            var capture = new MeasurementResultCapture(OwnsCapture);
            MsgRecord record = GetSingleMeasurement(owner, endpoint, timeoutMilliseconds: 30000, resultCapture: capture);

            MsgRecordState state = await ScheduledDeviceJobHelper.GetTerminalCompletion(record).WaitAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (state != MsgRecordState.Success)
                return RejectResult($"State={state}, Code={record.MsgReturn?.Code}, Message={record.MsgReturn?.Message}");
            if (!OwnsCapture()) return RejectResult("The original measurement source changed.");

            if (local)
                return CompleteResult((record.MsgReturn?.Data as LocalSpectrumCommandResult)?.Model);

            if (capture.OwnsRemoteSource is not { } ownsRemoteSource || !ownsRemoteSource())
                return RejectResult("The original measurement source changed.");
            SpectumResultEntity? result = capture.RemoteLookup is { } lookup
                ? await lookup.Value.WaitAsync(cancellationToken).ConfigureAwait(false) : null;
            cancellationToken.ThrowIfCancellationRequested();
            return ownsRemoteSource() ? CompleteResult(result) : RejectResult("The original measurement source changed during result lookup.");

            (bool Succeeded, SpectumResultEntity? Result) CompleteResult(SpectumResultEntity? model) =>
                model == null || MatchesResultDevice(model, deviceCode)
                    ? (true, model) : RejectResult($"ResultId={model.Id}, ResultDevice={model.DeviceCode}");

            (bool Succeeded, SpectumResultEntity? Result) RejectResult(string reason)
            {
                log.Warn($"Spectrum measurement result rejected. Device={deviceCode}, Event={record.MsgSend?.EventName}, MsgID={record.MsgID}, {reason}");
                return (false, null);
            }

            bool OwnsCapture() => OwnsDevice(owner) && ReferenceEquals(owner.Config, config)
                && ReferenceEquals(owner.SysResourceModel, resource) && resource.Id == resourceId && !resource.IsDelete
                && string.Equals(owner.Code, deviceCode, StringComparison.Ordinal)
                && string.Equals(config.SN, physicalSerial, StringComparison.Ordinal) && owner.SpectrumBackend.OpensLocally == local
                && (route == null || (ReferenceEquals(owner.ResultDatabaseEndpoint, endpoint) && (endpoint == null || endpoint.IsCurrent)
                    && route.HasSameDestination(CaptureMqttRoute(pinConnection: true)) && IsMqttRouteUsable(route)));
        }

        private sealed class MeasurementResultCapture(Func<bool> ownsCapture)
        {
            internal bool IsCurrent() => ownsCapture();
            internal Func<bool>? OwnsRemoteSource { get; set; }
            internal Lazy<Task<SpectumResultEntity?>>? RemoteLookup { get; set; }
        }

        internal MsgRecord GetDataForCorrection(DeviceSpectrum owner, MySqlConnectedEndpoint? endpoint, double timeoutMilliseconds,
            Func<bool> ownsCapture) => GetSingleMeasurement(owner, endpoint, timeoutMilliseconds, ownsCapture);

        private MsgRecord GetSingleMeasurement(DeviceSpectrum owner, MySqlConnectedEndpoint? endpoint, double timeoutMilliseconds,
            Func<bool>? ownsCorrectionCapture = null, MeasurementResultCapture? resultCapture = null)
        {
            bool OwnsCapture() => ownsCorrectionCapture?.Invoke() != false && resultCapture?.IsCurrent() != false;
            Func<bool>? captureGuard = ownsCorrectionCapture != null || resultCapture != null ? OwnsCapture : null;
            var settings = owner.DisplayConfig;
            string eventName = settings.IsLuminousFluxMode ? "EQE.GetData" : "GetData";
            lock (owner.SpectrumBackend.Sync)
            {
                if (!OwnsCapture())
                    return CreateRejectedMeasurementRecord(eventName, DeviceSpectrum.CorrectionCaptureSourceChangedMessage);
                if (owner.SpectrumBackend.OpensLocally) return owner.RunLocalSpectrumCommand(eventName);
            }
            if (!owner.TryValidateMeasurementCalibrationFiles(out string calibrationError))
                return CreateRejectedMeasurementRecord(eventName, calibrationError);
            bool acquireLease = ownsCorrectionCapture == null;
            if (acquireLease && !owner.TryEnterSpectrumMeasurement(out string rejectionReason))
                return CreateRejectedMeasurementRecord(eventName, rejectionReason);

            try
            {
                var Param = new Dictionary<string, object>();
                MsgSend msg = new()
                {
                    EventName = eventName,
                    Params = Param
                };
                Param.Add("IntegralTime", settings.IntTime);
                Param.Add("NumberOfAverage", settings.AveNum);
                Param.Add("AutoInitDark", settings.IsAutoDark);
                Param.Add("SelfAdaptionInitDark", settings.IsShutter);
                Param.Add("AutoIntegration", settings.IsAutoIntTime);
                Param.Add("IsWithND", settings.IsWithND);
                MsgRecord msgRecord = msg.EventName == "EQE.GetData"
                    ? PublishRemoteEqe(owner, endpoint, msg, Param, settings, timeoutMilliseconds, captureGuard, resultCapture)
                    : PublishSpectrumCommand(owner, msg, timeoutMilliseconds, captureGuard == null ? null
                        : () => captureGuard() ? null : DeviceSpectrum.CorrectionCaptureSourceChangedMessage,
                        resultEndpoint: endpoint, resultCapture: resultCapture);
                if (acquireLease) owner.ReleaseSpectrumMeasurementWhenTerminal(msgRecord);
                return msgRecord;
            }
            catch
            {
                if (acquireLease) owner.ReleaseSpectrumMeasurementLease();
                throw;
            }
        }
        public MsgRecord Close()
        {
            MsgSend msg = new()
            {
                EventName = "Close",
            };
            return PublishAsyncClient(msg);
        }
        public MsgRecord SelfAdaptionInitDark()
        {
            MsgSend msg = new()
            {
                EventName = "InitAutoDark",
                Params = Config.SelfAdaptionInitDark
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord SetPort() => SendNdCommand(Device.DisplayConfig.PortNum);
        public MsgRecord GetPort() => SendNdCommand(null);

        internal MsgRecord SendNdCommand(int? port, LocalSpectrumOpticalScope? optics = null)
        {
            DeviceSpectrum owner = Device;
            MsgSend msg = new()
            {
                EventName = port.HasValue ? "SetPort" : "GetPort",
            };
            if (port.HasValue) msg.Params = new Dictionary<string, object> { ["PortNum"] = port.Value };
            return optics == null ? PublishAsyncClient(msg) : PublishSpectrumCommand(owner, msg, 30000, ndOptics: optics);
        }


        public MsgRecord InitDark()
        {
            var Param = new Dictionary<string, object>();
            MsgSend msg = new()
            {
                EventName = "InitDark",
                Params = Param
            };
            Param.Add("IntegralTime", Device.DisplayConfig.IntTime);
            Param.Add("NumberOfAverage", Device.DisplayConfig.AveNum);
            Param.Add("AutoInitDark", Device.DisplayConfig.IsAutoDark);
            return PublishAsyncClient(msg);
        }

        public MsgRecord GetDataAuto()
        {
            DeviceSpectrum owner = Device;
            MySqlConnectedEndpoint? endpoint = owner.ResultDatabaseEndpoint;
            var settings = owner.DisplayConfig;
            if (owner.SpectrumBackend.OpensLocally) return owner.RunLocalSpectrumCommand(settings.IsLuminousFluxMode ? "EQE.GetDataAuto" : "GetDataAuto");
            if (!owner.TryEnterSpectrumContinuousMeasurement(out string rejectionReason))
                return CreateRejectedMeasurementRecord("GetDataAuto", rejectionReason);

            try
            {
                var Param = new Dictionary<string, object>();
                MsgSend msg = new()
                {
                    EventName = "GetDataAuto",
                    Params = Param
                };
                Param.Add("IntegralTime", settings.IntTime);
                Param.Add("NumberOfAverage", settings.AveNum);
                Param.Add("AutoInitDark", settings.IsAutoDark);
                Param.Add("SelfAdaptionInitDark", settings.IsShutter);
                Param.Add("AutoIntegration", settings.IsAutoIntTime);
                Param.Add("IsWithND", settings.IsWithND);
                if (settings.IsLuminousFluxMode) msg.EventName = "EQE.GetDataAuto";
                MsgRecord msgRecord = msg.EventName == "EQE.GetDataAuto"
                    ? PublishRemoteEqe(owner, endpoint, msg, Param, settings, 30000)
                    : PublishSpectrumCommand(owner, msg, 30000);
                owner.ReleaseSpectrumContinuousStartWhenTerminal(msgRecord);
                return msgRecord;
            }
            catch
            {
                owner.ReleaseSpectrumContinuousMeasurementLease();
                throw;
            }
        }

        private MsgRecord PublishRemoteEqe(DeviceSpectrum owner, MySqlConnectedEndpoint? endpoint, MsgSend message,
            Dictionary<string, object> parameters, DisplaySpectrumConfig settings, double timeout, Func<bool>? ownsCapture = null,
            MeasurementResultCapture? resultCapture = null)
        {
            bool CanPublish() => endpoint?.IsCurrent == true && ReferenceEquals(owner.ResultDatabaseEndpoint, endpoint)
                && CanPresentRemoteResult(owner) && (ownsCapture?.Invoke() ?? true);

            if (endpoint == null || !CanPublish()) return RejectEqeDatabase(message.EventName);

            var electricalInputs = settings.ReadEffectiveElectricalInputs();
            double voltage = electricalInputs.Voltage;
            double current = electricalInputs.LegacyRemoteCurrent;
            int? masterId = SaveEqeElectricalInputs(endpoint, voltage, current, CanPublish);
            if (masterId == null) return RejectEqeDatabase(message.EventName);

            parameters.Add("AFactor", settings.Divisor);
            parameters.Add("OutputDataFilename", "EQEData.json");
            parameters.Add("SMUData", new Dictionary<string, object>
            {
                ["V"] = voltage, ["I"] = current, ["Channel"] = 0, ["MasterId"] = masterId.Value, ["MasterResultType"] = 200,
            });
            return PublishSpectrumCommand(owner, message, timeout, () => CanPublish() ? null : EqeDatabaseChangedMessage,
                resultEndpoint: endpoint, resultCapture: resultCapture);
        }

        private static int? SaveEqeElectricalInputs(MySqlConnectedEndpoint endpoint, double voltage, double current, Func<bool> canPublish)
        {
            using var db = endpoint.CreateDbClient();
            if (!canPublish()) return null;

            db.Ado.BeginTran();
            bool committed = false;
            try
            {
                var result = new SMUResultModel { VResult = (float)voltage, IResult = (float)current, CreateDate = DateTime.Now };
                int id = db.Insertable(result).ExecuteReturnIdentity();
                if (id <= 0) throw new InvalidOperationException("保存 EQE 的源表关联记录失败。");
                if (!canPublish()) return null;
                db.Ado.CommitTran();
                committed = true;
                return id;
            }
            finally
            {
                if (!committed) db.Ado.RollbackTran();
            }
        }

        private const string EqeDatabaseChangedMessage = "光谱 EQE 需要当前设备所属的已验证数据库，连接已更改或不可用。";

        private MsgRecord RejectEqeDatabase(string eventName) => CreateRejectedMeasurementRecord(eventName, EqeDatabaseChangedMessage);

        private MsgRecord CreateRejectedMeasurementRecord(string eventName, string message)
        {
            var record = new MsgRecord
            {
                MsgID = Guid.NewGuid().ToString(),
                SendTime = DateTime.Now,
                MsgSend = new MsgSend { EventName = eventName, ServiceName = Config.Code },
                MsgReturn = new MsgReturn
                {
                    EventName = eventName,
                    DeviceCode = Config.Code,
                    Code = -1,
                    Message = message,
                },
            };
            if (Application.Current?.Dispatcher is { } dispatcher)
                dispatcher.BeginInvoke(() => record.MsgRecordState = MsgRecordState.Fail);
            else
                record.MsgRecordState = MsgRecordState.Fail;
            return record;
        }

        public MsgRecord GetDataAutoStop()
        {
            if (Device.SpectrumBackend.OpensLocally) return Device.RunLocalSpectrumCommand("GetDataAutoStop");
            MsgSend msg = new()
            {
                EventName = "GetDataAutoStop",
                ServiceName = Config.Code,
            };
            MsgRecord msgRecord = PublishAsyncClient(msg);
            Device.ReleaseSpectrumContinuousStopWhenTerminal(msgRecord);
            return msgRecord;
        }



        public MsgRecord ShutterConnect()
        {
            MsgSend msg = new()
            {
                EventName = MQTTSpectrumEventEnum.Event_Shutter_Connect,
                ServiceName = Config.Code,
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord ShutterDisconnect()
        {
            MsgSend msg = new()
            {
                EventName = MQTTSpectrumEventEnum.Event_Shutter_Disconnect,
                ServiceName = Config.Code,
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord ShutterDoopen()
        {
            MsgSend msg = new()
            {
                EventName = MQTTSpectrumEventEnum.Event_Shutter_Doopen,
                ServiceName = Config.Code,
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord ShutterDoclose()
        {
            MsgSend msg = new()
            {
                EventName = MQTTSpectrumEventEnum.Event_Shutter_Doclose,
                ServiceName = Config.Code,
            };
            return PublishAsyncClient(msg);
        }
    }
}
