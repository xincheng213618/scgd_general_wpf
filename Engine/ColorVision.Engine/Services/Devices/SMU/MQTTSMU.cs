#pragma warning disable CA1863,CS8602
using ColorVision.Database;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.Messages;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.Devices.SMU.Configs;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.SMU.Views;
using ColorVision.Engine.Services.Devices.Spectrum;
using cvColorVision;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.SMU
{
    public class MQTTSMU : MQTTDeviceService<ConfigSMU>
    {
        private static readonly Version FirstPersistedScanVersion = new(4, 0, 2, 115);
        private readonly Func<IEnumerable<DeviceSpectrum>> _spectrumRecipients;
        private readonly Func<Version> _serviceVersion;
        private readonly Action<DeviceSMU, Func<ViewResultSMU>>? _presentResult;
        private readonly DisplaySMUConfig _spectrumDisplayConfig;
        private SMUChannelType _spectrumChannel;
        private long _spectrumSelectionGeneration;
        private readonly object _resultQueueLock = new();
        private readonly Queue<ResultNotice> _resultQueue = new();
        private bool _drainingResults;
        private int _retired;

        public DeviceSMU Device { get; set; }
        public override DeviceStatusType DeviceStatus
        {
            get => Device?.SmuBackend?.Status ?? base.DeviceStatus;
            set { if (Device?.SmuBackend == null) base.DeviceStatus = value; else Device.SmuBackend.ObserveService(value); }
        }
        internal void RefreshBackendStatus() => base.DeviceStatus = Device.SmuBackend.Status;

        internal override MsgRecord PublishAsyncClient(MsgSend message, double timeout = 30000)
        {
            DeviceSMU owner = Device;
            lock (owner.SmuBackend.Sync)
            {
                if (owner.SmuBackend.OpensLocally) return owner.RunLocalSmuCommand(message);
                if (IsMeasurement(message.EventName))
                {
                    message.CompleteWithoutDispatcher = true;
                    var source = CaptureRemoteOutputRoute(owner, allowUnboundDatabase: true);
                    if (!source.IsCurrent() || (!string.IsNullOrEmpty(message.DeviceCode)
                        && !string.Equals(source.Route.DeviceCode, message.DeviceCode, StringComparison.Ordinal))) return RejectMeasurement(message);
                    try { return PublishOwnedOutputCommand(source, message, TimeSpan.FromMilliseconds(timeout), requireGeneration: false).Record; }
                    catch (InvalidOperationException) when (!source.IsCurrent()) { return RejectMeasurement(message); }
                }
                owner.EnsureOtherSmuBackends(false, Local.LocalSmuConnection.From(Config));
                owner.SmuBackend.BeginServiceCommand(message.EventName);
            }
            try
            {
                MsgRecord record = base.PublishAsyncClient(message, timeout);
                int completed = 0;
                void Complete(object? sender, MsgRecordState state)
                {
                    if (state is not (MsgRecordState.Success or MsgRecordState.Fail or MsgRecordState.Timeout)
                        || System.Threading.Interlocked.Exchange(ref completed, 1) != 0) return;
                    record.MsgRecordStateChanged -= Complete;
                    owner.SmuBackend.EndServiceCommand();
                    if (state == MsgRecordState.Success && message.EventName is "Open" or "Close" or "Reopen")
                        DeviceStatus = message.EventName == "Close" ? DeviceStatusType.Closed : DeviceStatusType.Opened;
                }
                record.MsgRecordStateChanged += Complete;
                Complete(record, record.MsgRecordState);
                return record;
            }
            catch { owner.SmuBackend.EndServiceCommand(); throw; }
        }

        public override void Dispose()
        {
            if (Interlocked.Exchange(ref _retired, 1) != 0) return;
            _spectrumDisplayConfig.PropertyChanged -= SpectrumChannelChanged;
            lock (_resultQueueLock) _resultQueue.Clear();
            base.Dispose();
            GC.SuppressFinalize(this);
        }
        public MQTTSMU(DeviceSMU deviceSMU) : this(deviceSMU, MQTTControl.GetInstance())
        {
        }

        internal MQTTSMU(DeviceSMU device, MQTTControl mqttControl, Func<IEnumerable<DeviceSpectrum>>? spectrumRecipients = null,
            Func<Version>? serviceVersion = null, Action<DeviceSMU, Func<ViewResultSMU>>? presentResult = null)
            : base(device.Config, mqttControl)
        {
            Device = device;
            _spectrumRecipients = spectrumRecipients ??
                (static () => ServiceManager.Current?.DeviceServices.OfType<DeviceSpectrum>() ?? Enumerable.Empty<DeviceSpectrum>());
            _serviceVersion = serviceVersion ?? (static () => FlowEngineManager.GetInstance().ServiceVersion);
            _presentResult = presentResult;
            _spectrumDisplayConfig = device.DisplayConfig;
            _spectrumChannel = _spectrumDisplayConfig.Channel;
            _spectrumDisplayConfig.PropertyChanged += SpectrumChannelChanged;
            MQTTControl.SubscribeCache(SubscribeTopic);
        }

        internal bool OwnsRemoteDevice(DeviceSMU owner) => Volatile.Read(ref _retired) == 0 && !owner.IsDisposed
            && ReferenceEquals(owner, Device) && ReferenceEquals(owner.DService, this) && !owner.SmuBackend.OpensLocally;

        private bool OwnsRemoteResult(DeviceSMU owner, MySqlConnectedEndpoint? endpoint) => OwnsRemoteDevice(owner)
            && endpoint?.IsCurrent == true && ReferenceEquals(owner.ResultDatabaseEndpoint, endpoint);

        internal bool UsesPersistedScanResults => _serviceVersion() >= FirstPersistedScanVersion;

        internal readonly record struct ScanResultParameters(bool IsSourceV, double EndValue);

        private static bool IsMeasurement(string? eventName) => eventName is "GetData" or "Scan";

        internal void EnqueueOwnedResult(DeviceSMU owner, string eventName, MySqlConnectedEndpoint? endpoint,
            JObject data, Func<bool> ownsSource, SMUChannelType? channel = null, bool persistedScan = true,
            ScanResultParameters? scanParameters = null, (long A, long B)? readoutResets = null)
        {
            if (!IsMeasurement(eventName)) return;

            var route = CaptureMqttRoute(pinConnection: true);
            var connection = Local.LocalSmuConnection.From(Config);
            int masterId = 0;
            JObject? inline = null;
            if (eventName == "Scan" && !persistedScan)
            {
                if (scanParameters == null || data["VList"] is not JArray || data["IList"] is not JArray) return;
                inline = data;
            }
            else if (!OwnsRemoteResult(owner, endpoint) || !MsgReturn.TryReadInteger(data["MasterId"], out masterId) || masterId <= 0)
                return;

            var notice = new ResultNotice(owner, eventName, endpoint, masterId, inline, scanParameters, channel, ownsSource,
                readoutResets ?? owner.SmuBackend.CaptureReadoutResets(), route, connection);
            if (!CanPresent(notice)) return;
            lock (_resultQueueLock)
            {
                if (Volatile.Read(ref _retired) != 0) return;
                _resultQueue.Enqueue(notice);
                if (_drainingResults) return;
                _drainingResults = true;
            }
            _ = Task.Run(DrainResultsAsync);
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
                        _drainingResults = false;
                        return;
                    }
                }

                try
                {
                    if (!CanPresent(notice)) continue;
                    var present = CaptureResultPresentation(notice.Owner);
                    if (notice.EventName == "GetData")
                        await ReadPointResultAsync(notice, present).ConfigureAwait(false);
                    else if (present != null && ReadScanResult(notice) is { } scan)
                        await PresentIfCurrentAsync(notice, () => present(scan)).ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    if (Volatile.Read(ref _retired) == 0)
                        log.Warn($"Unable to present SMU result. Device={notice.Route.DeviceCode}, Event={notice.EventName}, MasterId={notice.MasterId}", error);
                }
            }
        }

        private bool CanPresent(ResultNotice notice) => OwnsRemoteDevice(notice.Owner)
            && (notice.Inline != null || OwnsRemoteResult(notice.Owner, notice.Endpoint)) && notice.OwnsSource();

        private Task ReadPointResultAsync(ResultNotice notice, Action<ViewResultSMU>? present)
        {
            SMUResultModel? model;
            using (var db = notice.Endpoint!.CreateDbClient())
                model = db.Queryable<SMUResultModel>().First(row => row.Id == notice.MasterId);
            if (model == null || !MatchesResult(notice, model.DeviceCode, model.ChannelType) || !CanPresent(notice)) return Task.CompletedTask;
            var view = present == null ? null : new ViewResultSMU(model);
            return PresentIfCurrentAsync(notice, () =>
            {
                if (view != null) present!(view);
                ApplyPointReadout(notice, model);
            });
        }

        private ViewResultSMU? ReadScanResult(ResultNotice notice)
        {
            if (notice.Inline is { } inline && notice.ScanParameters is { } parameters)
            {
                if (inline.ToObject<SMUScanResultData>() is not { VList: not null, IList: not null } data) return null;
                return new ViewResultSMU(parameters.IsSourceV ? MeasurementType.Voltage : MeasurementType.Current,
                    (float)parameters.EndValue, data.VList, data.IList)
                {
                    CreateTime = DateTime.Now, Id = -1, ChannelType = notice.Channel ?? SMUChannelType.A,
                };
            }

            SmuScanModel? model;
            using (var db = notice.Endpoint!.CreateDbClient())
                model = db.Queryable<SmuScanModel>().First(row => row.Id == notice.MasterId);
            return model != null && MatchesResult(notice, model.DeviceCode, model.ChannelType) && CanPresent(notice)
                ? new ViewResultSMU(model) : null;
        }

        private static bool MatchesResult(ResultNotice notice, string? deviceCode, SMUChannelType channel) =>
            (string.IsNullOrWhiteSpace(deviceCode) || string.Equals(deviceCode, notice.Route.DeviceCode, StringComparison.OrdinalIgnoreCase))
            && (notice.Channel == null || notice.Channel == channel);

        private void ApplyPointReadout(ResultNotice notice, SMUResultModel model)
        {
            var owner = notice.Owner;
            var endpoint = notice.Endpoint!;
            bool CanApplyReadout() => CanPresent(notice) && owner.SmuBackend.IsReadoutCurrent(model.ChannelType, notice.ReadoutResets);
            if (!CanApplyReadout()) return;
            var config = owner.DisplayConfig;
            var channel = model.ChannelType switch
            {
                SMUChannelType.A => config.ChannelA,
                SMUChannelType.B => config.ChannelB,
                _ => null,
            };
            if (channel != null)
            {
                channel.V = model.VResult;
                if (!CanApplyReadout()) return;
                channel.I = model.IResult;
            }
            if (model.ChannelType != config.Channel || !CanApplyReadout()) return;

            config.V = model.VResult;
            if (model.ChannelType != config.Channel || !CanApplyReadout()) return;
            config.I = model.IResult;
            if (!CanApplyReadout()) return;
            Func<bool> sourceStillCurrent = CreateSourceValidator(owner, endpoint, notice.Route, notice.Connection, model.ChannelType, notice.ReadoutResets);
            SyncSpectrumReadings(owner, endpoint, model.ChannelType, model.VResult, model.IResult, sourceStillCurrent);
        }

        internal void SyncSpectrumReadings(DeviceSMU owner, MySqlConnectedEndpoint? endpoint, SMUChannelType channel,
            double? voltage, double? currentMilliamps, Func<bool> sourceStillCurrent)
        {
            Func<bool> isCurrent = CaptureSpectrumSelection(owner, channel, sourceStillCurrent);
            ApplySpectrumReadings(owner, endpoint, channel, voltage, currentMilliamps, isCurrent);
        }

        private Func<bool> CaptureSpectrumSelection(DeviceSMU owner, SMUChannelType channel, Func<bool> sourceStillCurrent)
        {
            long generation = Volatile.Read(ref _spectrumSelectionGeneration);
            var device = new WeakReference<DeviceSMU>(owner);
            var service = new WeakReference<MQTTSMU>(this);
            return () => device.TryGetTarget(out var presentDevice) && service.TryGetTarget(out var presentService)
                && presentService.OwnsSpectrumSelection(presentDevice, channel, generation) && sourceStillCurrent();
        }

        private bool OwnsSpectrumSelection(DeviceSMU owner, SMUChannelType channel, long generation) => Volatile.Read(ref _retired) == 0
            && !owner.IsDisposed && ReferenceEquals(Device, owner) && ReferenceEquals(owner.DService, this)
            && ReferenceEquals(owner.DisplayConfig, _spectrumDisplayConfig) && _spectrumDisplayConfig.Channel == channel
            && generation == Volatile.Read(ref _spectrumSelectionGeneration);

        private void SpectrumChannelChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName != nameof(DisplaySMUConfig.Channel)) return;
            SMUChannelType selected = _spectrumDisplayConfig.Channel;
            SMUChannelType previous = _spectrumChannel;
            if (selected == previous) return;

            _spectrumChannel = selected;
            long generation = Interlocked.Increment(ref _spectrumSelectionGeneration);
            DeviceSMU owner = Device;
            ApplySpectrumReadings(owner, owner.ResultDatabaseEndpoint, previous, null, null,
                () => OwnsSpectrumSelection(owner, selected, generation));
        }

        private void ApplySpectrumReadings(DeviceSMU owner, MySqlConnectedEndpoint? endpoint, SMUChannelType channel,
            double? voltage, double? currentMilliamps, Func<bool> isCurrent)
        {
            foreach (var spectrum in _spectrumRecipients())
            {
                if (!isCurrent()) return;
                if (!spectrum.IsDisposed && ReferenceEquals(spectrum.ResultDatabaseEndpoint, endpoint))
                    spectrum.ApplySmuReadings(endpoint, voltage, currentMilliamps, isCurrent, sourceDevice: owner, sourceChannel: channel);
            }
        }

        private Func<bool> CreateSourceValidator(DeviceSMU owner, MySqlConnectedEndpoint endpoint, MqttServiceRoute route,
            Local.LocalSmuConnection connection, SMUChannelType channel, (long A, long B) readoutResets)
        {
            var device = new WeakReference<DeviceSMU>(owner);
            var service = new WeakReference<MQTTSMU>(this);
            return () => device.TryGetTarget(out var presentDevice) && service.TryGetTarget(out var presentService)
                && presentService.OwnsRemoteResult(presentDevice, endpoint) && presentService.IsMqttRouteUsable(route)
                && route.HasSameDestination(presentService.CaptureMqttRoute(pinConnection: true))
                && Local.LocalSmuConnection.From(presentDevice.Config) == connection
                && presentDevice.SmuBackend.IsReadoutCurrent(channel, readoutResets);
        }

        private Action<ViewResultSMU>? CaptureResultPresentation(DeviceSMU owner)
        {
            if (_presentResult is { } present) return result => present(owner, () => result);
            if (owner.ExistingView is not { } view || !owner.ViewRegistration.IsVisible) return null;
            var target = new WeakReference<ViewSMU>(view);
            return result =>
            {
                if (target.TryGetTarget(out var currentView) && ReferenceEquals(currentView, owner.ExistingView) && owner.ViewRegistration.IsVisible)
                    currentView.AddViewResultSMU(result);
            };
        }

        private Task PresentIfCurrentAsync(ResultNotice notice, Action present)
        {
            if (!CanPresent(notice) || Application.Current?.Dispatcher is not { } dispatcher) return Task.CompletedTask;
            return dispatcher.InvokeAsync(() =>
            {
                if (CanPresent(notice)) present();
            }).Task;
        }

        private readonly record struct ResultNotice(DeviceSMU Owner, string EventName, MySqlConnectedEndpoint? Endpoint, int MasterId,
            JObject? Inline, ScanResultParameters? ScanParameters, SMUChannelType? Channel, Func<bool> OwnsSource,
            (long A, long B) ReadoutResets, MqttServiceRoute Route, Local.LocalSmuConnection Connection);

        public bool SetParam()
        {
            MsgSend msg = new()
            {
                EventName = "SetParam",
            };
            PublishAsyncClient(msg);
            return true;
        }

        public MsgRecord Open(bool isNet, string devName)
        {
            var Params = new Dictionary<string, object>();

            MsgSend msg = new()
            {
                EventName = "Open",
                Params = Params
            };
            Params.Add("DevName",devName);
            Params.Add("IsNet", isNet);
            Params.Add("Channel", Device.DisplayConfig.Channel);
            return PublishAsyncClient(msg);
        }


        public MsgRecord? GetData(bool isSourceV, double measureVal, double lmtVal, SMUChannelType channel)
        {
            if (!Device.SmuBackend.OpensLocally && Device.DisplayConfig.IsUseLimitSigned)
            {
                double V = isSourceV ? measureVal : lmtVal;
                double I = isSourceV ? lmtVal : measureVal;
                if (!IsLimit(V, I)) return null;
            }

            var Params = new Dictionary<string, object>();
            MsgSend msg = new()
            {
                EventName = "GetData",
                Params = Params
            };

            Params.Add("IsSourceV", isSourceV);
            Params.Add("MeasureValue", measureVal);
            Params.Add("LimitValue", lmtVal);
            Params.Add("Channel", channel);
            return PublishAsyncClient(msg);
        }


        public MsgRecord Close()
        {
            MsgSend msg = new()
            {
                EventName = "Close",
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord? StepData(bool isSourceV, double measureVal, double lmtVal, SMUChannelType channel)
        {
            if (!Device.SmuBackend.OpensLocally) return GetData(isSourceV, measureVal, lmtVal, channel);
            return PublishAsyncClient(new MsgSend { EventName = "StepData", Params = new Local.LocalSmuParameters
                { IsSourceV = isSourceV, MeasureValue = measureVal, LimitValue = lmtVal, Channel = channel } });
        }

        public class SMUScanParam
        {
            public bool IsSourceV { set; get; }
            public double BeginValue { set; get; }
            public double EndValue { set; get; }
            public double LimitValue { set; get; }
            public int Points { set; get; }

            public SMUChannelType Channel { get; set; }

        }
        public MsgRecord? Scan(SMUParam smuParam, SMUChannelType channel)
        {
            if (Device.SmuBackend.OpensLocally)
                return PublishAsyncClient(new MsgSend { EventName = "Scan", Params = new
                    { DeviceParam = Local.LocalSmuParameters.FromTemplate(smuParam, channel, true), IsCloseOutput = true } });

            SMUScanParam? parameters = CreateRemoteScanParameters(smuParam, channel);
            if (parameters == null) return null;
            return PublishAsyncClient(CreateRemoteScanMessage(parameters));
        }

        internal (MsgRecord? Record, Task? Completion) ScanFromPanel(SMUParam smuParam, SMUChannelType channel, Func<Task> waitBeforeClose)
        {
            DeviceSMU owner = Device;
            lock (owner.SmuBackend.Sync)
            {
                if (owner.SmuBackend.OpensLocally) return (Scan(smuParam, channel), null);
            }

            // Limit validation can show a modal dialog. Keep it outside backend admission.
            SMUScanParam? parameters = CreateRemoteScanParameters(smuParam, channel);
            if (parameters == null) return (null, null);

            lock (owner.SmuBackend.Sync)
            {
                RemoteOutputRoute source = CaptureRemoteOutputRoute(owner);
                RequireCurrentOutputRoute(source);
                MsgSend message = CreateRemoteScanMessage(parameters, completeWithoutDispatcher: true);
                var timeout = TimeSpan.FromSeconds(30);
                var (record, generation) = PublishOwnedOutputCommand(source, message, timeout);
                return (record, FinishPanelScanAsync(source, channel, record, generation, waitBeforeClose, timeout));
            }
        }

        private SMUScanParam? CreateRemoteScanParameters(SMUParam smuParam, SMUChannelType channel)
        {
            bool isSourceV = smuParam.IsSourceV;
            double startMeasureVal = smuParam.StartMeasureVal;
            double stopMeasureVal = smuParam.StopMeasureVal;
            double lmtVal = smuParam.LmtVal;
            int number = smuParam.Number;

            if (Device.DisplayConfig.IsUseLimitSigned)
            {
                double V = isSourceV ? stopMeasureVal : lmtVal;
                double I = isSourceV ? lmtVal : stopMeasureVal;
                if (!IsLimit(V, I)) return null;
            }

            return new SMUScanParam { IsSourceV = isSourceV, BeginValue = startMeasureVal, EndValue = stopMeasureVal, LimitValue = lmtVal, Points = number, Channel = channel };
        }

        private static MsgSend CreateRemoteScanMessage(SMUScanParam parameters, bool completeWithoutDispatcher = false) => new()
        {
            EventName = "Scan",
            Params = new Dictionary<string, object> { ["DeviceParam"] = parameters },
            CompleteWithoutDispatcher = completeWithoutDispatcher,
        };

        public bool IsLimit(double V ,double I)
        {
            I /= 1000;

            V = Math.Abs(V);
            I = Math.Abs(I);

            if (Device.Config.DevType == Pss_Type.Keithley_2400)
            {
                if (V > 200)
                {
                    MessageBox.Show(string.Format(Properties.Resources.SMUKeithley2450MaxVoltage, 200), Properties.Resources.Hint, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                if ((V > 20 && V <= 200) && (I > 0.1))
                {
                    MessageBox.Show(string.Format(Properties.Resources.SMUKeithley2450MaxCurrentAboveThreshold, 20, 100), Properties.Resources.Hint, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                if (V <= 20 && I > 1)
                {
                    MessageBox.Show(string.Format(Properties.Resources.SMUKeithley2450MaxCurrentBelowThreshold, 20, 1), Properties.Resources.Hint, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
            else if (Device.Config.DevType == Pss_Type.Keithley_2600)
            {
                if (V > 40)
                {
                    MessageBox.Show(string.Format(Properties.Resources.SMUKeithley2600MaxVoltage, 40), Properties.Resources.Hint, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                if ((V > 6 && V <= 40) && (I > 1))
                {
                    MessageBox.Show(string.Format(Properties.Resources.SMUKeithley2600MaxCurrentAboveThreshold, 6, 1), Properties.Resources.Hint, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                if (V <= 6 && I > 3)
                {
                    MessageBox.Show(string.Format(Properties.Resources.SMUKeithley2600MaxCurrentBelowThreshold, 6, 3), Properties.Resources.Hint, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
            else if (Device.Config.DevType == Pss_Type.Precise_S100)
            {
                if (V > 30)
                {
                    MessageBox.Show(string.Format(Properties.Resources.SMUPreciseS100MaxVoltage, 30), Properties.Resources.Hint, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                if (V <= 30 && I > 1)
                {
                    MessageBox.Show(string.Format(Properties.Resources.SMUPreciseS100MaxCurrentBelowThreshold, 30, 1), Properties.Resources.Hint, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
            return true;
        }

        public MsgRecord CloseOutput() => CloseOutput(Device.DisplayConfig.Channel);

        public MsgRecord CloseOutput(SMUChannelType channel) => PublishAsyncClient(CreateCloseOutputMessage(channel));

        private static MsgSend CreateCloseOutputMessage(SMUChannelType channel, bool completeWithoutDispatcher = false) => new()
        {
            EventName = "CloseOutput",
            Params = new Dictionary<string, object> { ["Channel"] = channel },
            CompleteWithoutDispatcher = completeWithoutDispatcher
        };

        internal Func<Task> CapturePostProcessCloseOutput(SMUChannelType channel, TimeSpan confirmDelay, TimeSpan timeout)
        {
            RemoteOutputRoute source = CaptureRemoteOutputRoute(Device);
            var backend = source.Owner.SmuBackend;
            long capturedGeneration;
            lock (backend.Sync)
            {
                RequireCurrentOutputRoute(source);
                capturedGeneration = backend.CommandGeneration;
            }

            return async () =>
            {
                long firstGeneration = await SendAndConfirmAsync(capturedGeneration);
                await Task.Delay(confirmDelay);
                long closedGeneration = await SendAndConfirmAsync(firstGeneration);
                ClearOwnedReadings(source, channel, closedGeneration);
            };

            async Task<long> SendAndConfirmAsync(long expectedGeneration)
            {
                var (record, generation) = PublishOwnedOutputCommand(source, CreateCloseOutputMessage(channel, true), timeout, expectedGeneration);
                await ConfirmOutputCommandAsync(record, channel).ConfigureAwait(false);
                lock (backend.Sync)
                {
                    RequireCurrentOutputRoute(source);
                    if (backend.CommandGeneration != generation)
                        throw new InvalidOperationException("源表已接受新命令，后处理关闭输出结果已失效。");
                }
                return generation;
            }
        }

        private async Task FinishPanelScanAsync(RemoteOutputRoute source, SMUChannelType channel, MsgRecord scan,
            long scanGeneration, Func<Task> waitBeforeClose, TimeSpan timeout)
        {
            await ConfirmOutputCommandAsync(scan, channel);
            await waitBeforeClose();

            var backend = source.Owner.SmuBackend;
            MsgRecord close;
            long closeGeneration;
            lock (backend.Sync)
            {
                if (!source.IsCurrent() || backend.CommandGeneration != scanGeneration || backend.HasPendingServiceCommands)
                {
                    log.Info($"源表 {source.Route.DeviceCode} 通道 {channel} 已跳过扫描后的关闭输出：设备、连接或命令已变更。输出状态待确认。");
                    return;
                }

                (close, closeGeneration) = PublishOwnedOutputCommand(source, CreateCloseOutputMessage(channel, true), timeout, scanGeneration);
            }

            await ConfirmOutputCommandAsync(close, channel);
            ClearOwnedReadings(source, channel, closeGeneration);
        }

        private RemoteOutputRoute CaptureRemoteOutputRoute(DeviceSMU owner, bool allowUnboundDatabase = false)
        {
            ConfigSMU config = owner.Config;
            bool unbound = allowUnboundDatabase && owner.ResultDatabaseEndpoint == null;
            var target = owner.CaptureConfigurationTarget();
            var connection = Local.LocalSmuConnection.From(Config);
            var route = CaptureMqttRoute(pinConnection: true);
            bool IsResultCurrent() => OwnsRemoteDevice(owner) && ReferenceEquals(owner.Config, config) && ReferenceEquals(Config, config) && IsMqttRouteUsable(route)
                && (unbound ? target.IsCurrentResource(owner) : target.IsCurrent(owner))
                && string.Equals(owner.Code, route.DeviceCode, StringComparison.Ordinal)
                && route.HasSameDestination(CaptureMqttRoute(pinConnection: true)) && Local.LocalSmuConnection.From(Config) == connection;
            bool IsCurrent() => IsResultCurrent() && string.Equals(route.ServiceToken, ServiceToken, StringComparison.Ordinal);
            return new RemoteOutputRoute(owner, owner.DisplayConfig, route, connection, IsCurrent, IsResultCurrent);
        }

        private static void RequireCurrentOutputRoute(RemoteOutputRoute source)
        {
            if (!source.IsCurrent())
                throw new InvalidOperationException("原源表服务或 MQTT 路由已不可用，输出状态待确认。");
        }

        private (MsgRecord Record, long Generation) PublishOwnedOutputCommand(RemoteOutputRoute source, MsgSend message,
            TimeSpan timeout, long? expectedGeneration = null, bool requireGeneration = true)
        {
            var backend = source.Owner.SmuBackend;
            lock (backend.Sync)
            {
                RequireCurrentOutputRoute(source);
                if (expectedGeneration.HasValue && (backend.CommandGeneration != expectedGeneration.Value || backend.HasPendingServiceCommands))
                    throw new InvalidOperationException("源表正在执行其它命令或已接受新命令，关闭输出未发送。");

                string eventName = message.EventName;
                bool measurement = IsMeasurement(eventName);
                string? requestSerial = message.SerialNumber;
                var endpoint = source.Owner.ResultDatabaseEndpoint;
                var readoutResets = backend.CaptureReadoutResets();
                bool persistedScan = eventName != "Scan" || UsesPersistedScanResults;
                var (channel, scanParameters) = measurement ? ReadMeasurementParameters(message) : default;

                source.Owner.EnsureOtherSmuBackends(false, source.Connection);
                backend.BeginServiceCommand(message.EventName);
                long generation = backend.CommandGeneration;
                try
                {
                    bool CanDispatch() => (!requireGeneration || backend.CommandGeneration == generation) && source.IsCurrent()
                        && (!measurement || (ReferenceEquals(source.Owner.ResultDatabaseEndpoint, endpoint) && (endpoint == null || endpoint.IsCurrent)));
                    MsgRecord record = PublishOnMqttRoute(message, source.Route, timeout.TotalMilliseconds, CanDispatch, backend.Sync,
                        acceptsReply: measurement ? reply => AcceptsMeasurementReply(reply, source.Route.DeviceCode, eventName, requestSerial, persistedScan) : null,
                        beforeDispatch: measurement ? ObserveResult : null);
                    record.BeforeWorkerCompletion(_ => backend.EndServiceCommand());
                    return (record, generation);
                }
                catch
                {
                    backend.EndServiceCommand();
                    throw;
                }

                void ObserveResult(MsgRecord record)
                {
                    record.TerminalStateCommitted += OnTerminal;
                    void OnTerminal(MsgRecordState state)
                    {
                        record.TerminalStateCommitted -= OnTerminal;
                        if (state != MsgRecordState.Success || record.MsgReturn is not { HasCode: true, Code: 0 } reply || reply.Data is not JObject data) return;
                        EnqueueOwnedResult(source.Owner, eventName, endpoint, data, OwnsResult, channel, persistedScan, scanParameters, readoutResets);
                    }
                }

                bool OwnsResult() => source.IsResultCurrent() && ReferenceEquals(source.Owner.ResultDatabaseEndpoint, endpoint)
                    && (endpoint == null || endpoint.IsCurrent);
            }
        }

        private static bool AcceptsMeasurementReply(MsgReturn reply, string deviceCode, string eventName, string? serialNumber, bool persistedScan)
        {
            if (!reply.HasCode || (!string.IsNullOrEmpty(reply.DeviceCode) && !string.Equals(deviceCode, reply.DeviceCode, StringComparison.Ordinal))
                || (!string.IsNullOrEmpty(reply.EventName) && !string.Equals(eventName, reply.EventName, StringComparison.Ordinal))
                || (!string.IsNullOrWhiteSpace(serialNumber) && !string.IsNullOrEmpty(reply.SerialNumber)
                    && !string.Equals(serialNumber, reply.SerialNumber, StringComparison.Ordinal))) return false;

            return reply.Code != 0 || !persistedScan || reply.Data is not JObject data
                || data["MasterId"] is not { Type: not (JTokenType.Null or JTokenType.Undefined) } id || MsgReturn.TryReadInteger(id, out _);
        }

        private static (SMUChannelType? Channel, ScanResultParameters? ScanParameters) ReadMeasurementParameters(MsgSend message)
        {
            object? parameters = message.Params;
            if (message.EventName == "GetData") return (ReadChannel(ReadParameter(parameters, "Channel")), null);

            object? scan = ReadParameter(parameters, "DeviceParam");
            if (scan is SMUScanParam value) return (value.Channel, new(value.IsSourceV, value.EndValue));
            if (scan is JObject fields && fields["IsSourceV"] is JValue { Type: JTokenType.Boolean } isSourceV
                && fields["EndValue"] is JValue { Type: JTokenType.Integer or JTokenType.Float } endValue)
                return (ReadChannel(fields["Channel"]), new(isSourceV.Value<bool>(), endValue.Value<double>()));
            return (null, null);

            static object? ReadParameter(object? source, string name) => source switch
            {
                IDictionary<string, object> values when values.TryGetValue(name, out var value) => value,
                JObject fields => fields[name],
                _ => null,
            };

            static SMUChannelType? ReadChannel(object? channel) => channel switch
            {
                SMUChannelType value when value is SMUChannelType.A or SMUChannelType.B => value,
                int value when value is 0 or 1 => (SMUChannelType)value,
                JToken token when MsgReturn.TryReadInteger(token, out int number) && number is 0 or 1 => (SMUChannelType)number,
                _ => null,
            };
        }

        private MsgRecord RejectMeasurement(MsgSend message)
        {
            message.MsgID ??= Guid.NewGuid().ToString();
            var record = new MsgRecord(completeWithoutDispatcher: true) { MsgSend = message, MsgID = message.MsgID, SendTime = DateTime.Now };
            record.CompleteWithResponse(new MsgReturn
            {
                MsgID = record.MsgID, EventName = message.EventName, Code = -1,
                Message = EngineLocalization.Get("原源表服务或 MQTT 路由已不可用。"),
            });
            return record;
        }

        private static async Task ConfirmOutputCommandAsync(MsgRecord record, SMUChannelType channel)
        {
            var state = await record.WorkerCompletion!.ConfigureAwait(false);
            MsgSend command = record.MsgSend;
            string operation = command.EventName == "Scan" ? "扫描" : "关闭输出";
            string source = $"源表 {command.DeviceCode} 通道 {channel}";
            if (state == MsgRecordState.Timeout)
                throw new TimeoutException($"{source} {operation}超时，输出状态待确认。");

            MsgReturn? reply = record.MsgReturn;
            if (state != MsgRecordState.Success || reply is not { Code: 0 })
                throw new InvalidOperationException($"{source} {operation}失败，输出状态待确认。{reply?.Message}");
            if ((!string.IsNullOrEmpty(reply.DeviceCode) && !string.Equals(reply.DeviceCode, command.DeviceCode, StringComparison.Ordinal))
                || (!string.IsNullOrEmpty(reply.EventName) && !string.Equals(reply.EventName, command.EventName, StringComparison.Ordinal)))
                throw new InvalidOperationException($"{source} 收到不匹配的{operation}回执，输出状态待确认。");
        }

        private static void ClearOwnedReadings(RemoteOutputRoute source, SMUChannelType channel, long generation)
        {
            var backend = source.Owner.SmuBackend;
            lock (backend.Sync)
            {
                var display = source.Display;
                if (backend.CommandGeneration != generation || !source.IsCurrent() || !ReferenceEquals(source.Owner.DisplayConfig, display)) return;
                backend.MarkReadoutReset(channel);
                var readings = channel == SMUChannelType.A ? display.ChannelA : display.ChannelB;
                readings.V = null;
                readings.I = null;
                if (display.Channel == channel) { display.V = null; display.I = null; }
            }
        }

        private sealed record RemoteOutputRoute(DeviceSMU Owner, DisplaySMUConfig Display, MqttServiceRoute Route,
            Local.LocalSmuConnection Connection, Func<bool> IsCurrent, Func<bool> IsResultCurrent);
    }
}
