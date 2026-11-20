using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Local;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Spectrum;

public partial class DeviceSpectrum
{
    internal SpectrumBackendState SpectrumBackend { get; private set; } = null!;
    internal LocalSpectrumSession LocalSession { get; }
    private LocalSpectrumWindow? localWindow;
    private volatile bool localDisposed;
    internal bool IsDisposed => localDisposed;
    public event Action<LocalSpectrumCapture, bool>? LocalResultAvailable;
    public event Action<string>? LocalCaptureFailed;
    [Browsable(false)]
    public bool IsLocalSpectrumBackend => SpectrumBackend.OpensLocally;

    [Browsable(false), JsonIgnore]
    public double DisplayCurrent
    {
        get
        {
            var inputs = DisplayConfig.ReadEffectiveElectricalInputs();
            return IsLocalSpectrumBackend ? inputs.NativeCurrentAmps : inputs.LegacyRemoteCurrent;
        }
        set => DisplayConfig.SetManualCurrent(value, IsLocalSpectrumBackend);
    }

    [Browsable(false), JsonIgnore]
    public string DisplayCurrentUnit => IsLocalSpectrumBackend ? "A" : "mA";

    [CommandDisplay("本地光谱仪管理", Order = -2, CategoryOrder = 0), Category("DeviceConnection")]
    public RelayCommand OpenLocalSpectrumManagerCommand { get; private set; } = null!;

    private void InitializeLocalSpectrum()
    {
        SpectrumBackend = new SpectrumBackendState(DisplayConfig.UseLocalSpectrum);
        SpectrumBackend.Changed += RefreshLocalSpectrumStatus;
        DisplayConfig.PropertyChanged += LocalDisplayConfigChanged;
        OpenLocalSpectrumManagerCommand = new RelayCommand(_ => OpenLocalSpectrumWindow());
        LocalSpectrumFlowExecution.Register();
    }

    private void LocalDisplayConfigChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DisplaySpectrumConfig.UseLocalSpectrum)) SpectrumBackend.SetPreference(DisplayConfig.UseLocalSpectrum);
        else if (e.PropertyName == nameof(DisplaySpectrumConfig.I)) OnPropertyChanged(nameof(DisplayCurrent));
    }
    private void RefreshLocalSpectrumStatus()
    {
        DService?.RefreshBackendStatus();
        OnPropertyChanged(nameof(IsLocalSpectrumBackend));
        OnPropertyChanged(nameof(DisplayCurrent));
        OnPropertyChanged(nameof(DisplayCurrentUnit));
    }

    public void OpenLocalSpectrumWindow(ColorVision.Engine.FlowProcessing.Nodes.LocalSpectrumNode? node = null)
    {
        if (localDisposed) return;
        if (localWindow is { } existing) { existing.AttachNode(node); existing.Activate(); return; }
        var window = new LocalSpectrumWindow(this) { Owner = Application.Current.GetActiveWindow() };
        localWindow = window;
        window.Closed += (_, _) => Interlocked.CompareExchange(ref localWindow, null, window);
        if (localDisposed)
        {
            window.Close();
            return;
        }
        window.AttachNode(node);
        window.Show();
    }

    internal LocalSpectrumParameters BuildLocalSpectrumParameters(bool? eqe = null)
    {
        var settings = DisplayConfig;
        var electricalInputs = settings.ReadEffectiveElectricalInputs();
        return new()
        {
            IntegralTime = (float)settings.IntTime, NumberOfAverage = settings.AveNum,
            AutoIntegration = settings.IsAutoIntTime, AutoInitDark = settings.IsAutoDark,
            SelfAdaptionInitDark = settings.IsShutter, IsWithND = settings.IsWithND,
            Eqe = eqe ?? settings.IsLuminousFluxMode, AFactor = settings.Divisor,
            Voltage = (float)electricalInputs.Voltage, Current = (float)electricalInputs.NativeCurrentAmps
        };
    }

    internal ConfigSpectrum SnapshotLocalConfiguration() => JsonConvert.DeserializeObject<ConfigSpectrum>(JsonConvert.SerializeObject(Config))!;

    internal LocalSpectrumOpticalScope SnapshotLocalOptics(string operation)
    {
        ConfigSpectrum config = SnapshotLocalConfiguration();
        bool measures = operation is "GetData" or "EQE.GetData" or "GetDataAuto" or "EQE.GetDataAuto" or "InitDark" or "InitAutoDark";
        return LocalSpectrumOpticalScope.Capture(this, config,
            includeNd: config.NDConfig.IsNDPort && (measures || operation == "Open"),
            includeShutter: measures || operation is "ShutterConnect" or "ShutterDoOpen" or "ShutterDoClose");
    }

    internal LocalSpectrumOpticalScope SnapshotNdCommandOptics() =>
        LocalSpectrumOpticalScope.Capture(this, SnapshotLocalConfiguration(), includeNd: true, includeShutter: false);

    internal MsgRecord SetShutterTransmitting(bool transmitting)
    {
        lock (SpectrumBackend.Sync)
        {
            bool open = SpectrumBackend.OpensLocally ? transmitting != Config.ShutterCfg.OpenForDark : transmitting;
            return open ? DService.ShutterDoopen() : DService.ShutterDoclose();
        }
    }

    internal void EnsureOtherSpectrumBackends(bool local)
    {
        foreach (var other in ServiceManager.Current?.DeviceServices.OfType<DeviceSpectrum>() ?? Enumerable.Empty<DeviceSpectrum>())
        {
            if (ReferenceEquals(other, this) || !string.Equals(other.Config.SN, Config.SN, StringComparison.OrdinalIgnoreCase)) continue;
            if (local) other.SpectrumBackend.EnsureLocalAvailable();
            else other.SpectrumBackend.EnsureServiceAvailable();
        }
    }

    internal LocalSpectrumLicenseSource CaptureOpenLicense(LocalSpectrumOpticalScope optics) =>
        LocalSpectrumLicense.Capture(this, () => optics.IsDeviceCurrent);

    // Callers reserve the backend before scheduling native work. The session itself serializes all handle access.
    internal void EnsureLocalSpectrumConnected(LocalSpectrumOpticalScope optics, LocalSpectrumLicenseSource? licenseSource = null)
    {
        ObjectDisposedException.ThrowIf(localDisposed, this);
        if (LocalSession.IsOpen) return;
        if (licenseSource == null) throw new InvalidOperationException("请先打开本地光谱仪。");
        ConfigSpectrum config = optics.Configuration;
        if (config.NDConfig.IsNDPort) optics.RequireNdCurrent();
        lock (SpectrumBackend.Sync)
        {
            EnsureOtherSpectrumBackends(true);
            SpectrumBackend.SetLocalStatus(DeviceStatusType.Opening);
        }
        try
        {
            LocalSession.Open(config, opticalScope: optics, licenseSource: licenseSource);
            SpectrumBackend.SetLocalStatus(DeviceStatusType.Opened);
        }
        catch { SpectrumBackend.SetLocalStatus(LocalSession.IsQuarantined ? DeviceStatusType.Unknown : DeviceStatusType.Closed); throw; }
    }

    internal LocalSpectrumCapture CaptureLocalSpectrum(LocalSpectrumParameters parameters, LocalSpectrumOpticalScope optics, CancellationToken cancellationToken = default)
    {
        EnsureLocalSpectrumConnected(optics);
        CancellationToken token = cancellationToken.CanBeCanceled ? cancellationToken : SpectrumBackend.LocalCancellationToken;
        LocalSpectrumCapture result = LocalSession.Capture(parameters, optics.Configuration, token, opticalScope: optics);
        result.DeviceCode = Code;
        return result;
    }

    internal void PublishLocalSpectrum(LocalSpectrumCapture result, LocalSpectrumOpticalScope optics, MySqlConnectedEndpoint? resultDatabase)
    {
        Application.Current?.Dispatcher.BeginInvoke(() => DisplayLocalSpectrum(result, false, optics.ConfigurationTarget, resultDatabase, optics));
    }

    internal void DisplayLocalSpectrum(LocalSpectrumCapture result, bool continuous, DeviceConfigurationTarget configurationTarget,
        MySqlConnectedEndpoint? resultDatabase, LocalSpectrumOpticalScope? optics = null)
    {
        bool CanPresent() => !localDisposed
            && (string.IsNullOrWhiteSpace(result.DeviceCode) || string.Equals(Code, result.DeviceCode, StringComparison.Ordinal))
            && (result.MeasurementId <= 0 || OwnsLocalSpectrumResultDatabase(resultDatabase));
        if (!CanPresent()) return;
        if (result.NDPort is int port && (optics?.IsNdCurrent ?? true) && IsConfigurationTargetCurrent(configurationTarget))
        {
            try
            {
                if (!ApplyLocalNdState(port, result.NdCalibration, configurationTarget, optics) && IsConfigurationTargetCurrent(configurationTarget))
                    log.Warn($"本地光谱结果有效，但未能同步 ND 配置。Device={result.DeviceCode}, Port={port}");
            }
            catch (Exception error)
            {
                log.Warn($"本地光谱结果有效，但同步 ND 配置失败。Device={result.DeviceCode}, Port={port}", error);
            }
        }
        // Feeding back a synchronized duration can quantize it again on the next request.
        if (CanPresent() && !result.UsedSyncFrequency && IsConfigurationTargetCurrent(configurationTarget))
            DisplayConfig.IntTime = result.IntegralTime;
        if (!CanPresent()) return;
        PresentResult(() => CreateLocalSpectrumView(result));
        if (CanPresent()) LocalResultAvailable?.Invoke(result, continuous);
    }

    private bool OwnsLocalSpectrumResultDatabase(MySqlConnectedEndpoint? endpoint) => endpoint?.IsCurrent == true
        && (SysResourceDao.IsLocalId(SysResourceModel.Id) || ReferenceEquals(ResultDatabaseEndpoint, endpoint));

    internal MySqlConnectedEndpoint? CaptureLocalSpectrumResultDatabase()
    {
        MySqlConnectedEndpoint? endpoint = ResultDatabaseEndpoint;
        if (OwnsLocalSpectrumResultDatabase(endpoint)) return endpoint;

        var active = MySqlControl.GetInstance().ConnectedEndpoint;
        if (active?.IsCurrent != true) return null;
        if (OwnsLocalSpectrumResultDatabase(active)) return active;
        throw new InvalidOperationException("本地光谱仪未绑定当前结果数据库，请重新选择设备后采集。");
    }

    internal static ViewResultSpectrum CreateLocalSpectrumView(LocalSpectrumCapture result)
    {
        var view = new ViewResultSpectrum(result.Data)
        {
            CreateTime = result.CapturedAt, IntTime = result.IntegralTime, AcquisitionFluxMode = result.EqeData.HasValue,
            AcquisitionDeviceCode = result.DeviceCode, AcquisitionParametersJson = result.AcquisitionParametersJson
        };
        if (result.MeasurementId > 0) view.Id = result.MeasurementId;
        if (result.EqeData is { } eqe)
        {
            view.Eqe = eqe.dEqe; view.V = (float)eqe.dVoltage; view.I = (float)(eqe.dCurrent * 1000.0);
            view.LuminousFlux = eqe.dIm; view.RadiantFlux = eqe.dW;
        }
        return view;
    }

    internal MsgRecord RunLocalSpectrumCommand(string eventName)
    {
        if (eventName is "SetPort" or "GetPort")
            return RunLocalNdCommand(eventName, eventName == "SetPort" ? DisplayConfig.PortNum : null);
        if (eventName is "GetDataAutoStop" or "Close") return FinishLocalCommand(eventName);
        // Snapshot UI settings before leaving the dispatcher; continuous runs keep their starting parameters.
        LocalSpectrumParameters parameters = BuildLocalSpectrumParameters(eventName.StartsWith("EQE", StringComparison.Ordinal) ? true : null);
        LocalSpectrumOpticalScope optics;
        LocalSpectrumLicenseSource? licenseSource;
        try
        {
            optics = SnapshotLocalOptics(eventName);
            licenseSource = eventName == "Open" ? CaptureOpenLicense(optics) : null;
        }
        catch (Exception ex) { return StartLocalRecord(eventName, () => Task.FromException<object?>(ex)); }
        ConfigSpectrum config = optics.Configuration;
        if (eventName is "GetData" or "EQE.GetData" or "GetDataAuto" or "EQE.GetDataAuto")
        {
            string deviceCode = Code;
            MySqlConnectedEndpoint? resultDatabase;
            try
            {
                resultDatabase = CaptureLocalSpectrumResultDatabase();
            }
            catch (Exception ex)
            {
                return StartLocalRecord(eventName, () => Task.FromException<object?>(ex));
            }

            if (eventName is "GetDataAuto" or "EQE.GetDataAuto")
                return StartLocalContinuous(parameters, optics, resultDatabase, deviceCode);

            return RunLocalSpectrumWork(eventName, () =>
            {
                var result = CaptureLocalSpectrum(parameters, optics);
                result.DeviceCode = deviceCode;
                var model = LocalSpectrumResultService.Save(result, parameters, resultDatabase);
                PublishLocalSpectrum(result, optics, resultDatabase);
                return new LocalSpectrumCommandResult(result, model);
            }, () => string.Equals(Code, deviceCode, StringComparison.Ordinal)
                && (resultDatabase == null || OwnsLocalSpectrumResultDatabase(resultDatabase)));
        }
        return RunLocalSpectrumWork(eventName, () =>
        {
            EnsureLocalSpectrumConnected(optics, licenseSource);
            if (eventName == "Open") return null;
            if (eventName == "InitDark") LocalSession.Dark(parameters, config, opticalScope: optics);
            else if (eventName == "ResetDark") LocalSession.ResetDark(config);
            else if (eventName == "InitAutoDark") LocalSession.InitAutoDark(config, opticalScope: optics);
            else if (eventName is "ShutterConnect" or "ShutterDisconnect" or "ShutterDoOpen" or "ShutterDoClose") LocalSession.ControlShutter(eventName, config, opticalScope: optics);
            else throw new NotSupportedException($"本地光谱仪暂不支持 {eventName} 操作。");
            return null;
        });
    }

    internal bool UsesLocalNd => SpectrumBackend.OpensLocally || (Config.NDConfig.IsBingNDDevice && GetBoundCfwPort()?.CfwBackend.OpensLocally == true);

    internal const string NdConfigurationChangedMessage = "光谱仪配置来源已更改，无法应用本次 ND 结果。";

    internal MsgRecord RunLocalNdCommand(string eventName, int? port, LocalSpectrumOpticalScope? optics = null)
    {
        DeviceConfigurationTarget configurationTarget = optics?.ConfigurationTarget ?? CaptureConfigurationTarget();
        bool reserveSpectrum = SpectrumBackend.OpensLocally;
        Exception? reservationError = null;
        try
        {
            if (!IsConfigurationTargetCurrent(configurationTarget)) throw new InvalidOperationException(NdConfigurationChangedMessage);
            if (!Config.NDConfig.IsNDPort) throw new InvalidOperationException("请先在光谱仪配置中启用 ND。");
            optics ??= SnapshotNdCommandOptics();
            optics.RequireNdCurrent();
            if (reserveSpectrum) SpectrumBackend.BeginLocalCommand();
            else if (DService.DeviceStatus is DeviceStatusType.Busy or DeviceStatusType.SP_Continuous_Mode)
                throw new InvalidOperationException("光谱仪正在采集，请完成或停止后再切换 ND。");
        }
        catch (Exception ex) { reservationError = ex; }
        return StartLocalRecord(eventName, async () =>
        {
            if (reservationError != null) throw reservationError;
            try
            {
                var result = await Task.Run(() =>
                {
                    if (!IsConfigurationTargetCurrent(configurationTarget)) throw new InvalidOperationException(NdConfigurationChangedMessage);
                    return ExecuteLocalNd(optics!, port, reserveSpectrum ? SpectrumBackend.LocalCancellationToken : CancellationToken.None);
                });
                bool Apply() => ApplyLocalNdState(result.Port, result.Calibration, configurationTarget, optics);
                bool accepted = Application.Current?.Dispatcher is { } dispatcher ? await dispatcher.InvokeAsync(Apply) : Apply();
                if (!accepted) throw new InvalidOperationException(NdConfigurationChangedMessage);
                return new { result.Port };
            }
            finally { if (reserveSpectrum) SpectrumBackend.EndLocalCommand(); }
        }, () => IsConfigurationTargetCurrent(configurationTarget));
    }

    internal (int Port, LocalSpectrumNd.Calibration Calibration) ExecuteLocalNd(LocalSpectrumOpticalScope optics, int? port, CancellationToken token)
    {
        ConfigSpectrum config = optics.Configuration;
        if (!config.NDConfig.IsNDPort) throw new InvalidOperationException("请先在光谱仪配置中启用 ND。");
        optics.RequireNdCurrent();
        // Validate the destination before moving, then commit files only after the SDK confirms its position.
        LocalSpectrumNd.Calibration? calibration = port.HasValue ? LocalSpectrumNd.ResolveCalibration(config, port.Value, optics) : null;
        int actual = LocalSession.ControlNd(config, port, token, opticalScope: optics);
        return (actual, calibration ?? LocalSpectrumNd.ResolveCalibration(config, actual, optics));
    }

    internal bool ApplyLocalNdState(int port, LocalSpectrumNd.Calibration? calibration, DeviceConfigurationTarget target, LocalSpectrumOpticalScope? optics = null)
    {
        bool OwnsNd() => (optics?.IsNdCurrent ?? true) && IsConfigurationTargetCurrent(target);
        if (!OwnsNd()) return false;
        if (calibration != null && !TryApplyCalibrationSelection(target, calibration.GroupName, calibration.WavelengthFile,
            calibration.MagnitudeFile, calibration.GroupName != null, false, false, out _)) return false;
        if (!OwnsNd()) return false;
        DisplayConfig.PortNum = port;
        return true;
    }

    private MsgRecord RunLocalSpectrumWork(string eventName, Func<object?> work, Func<bool>? ownsResult = null)
    {
        Exception? reservationError = null;
        try { SpectrumBackend.BeginLocalCommand(); }
        catch (Exception ex) { reservationError = ex; }
        return StartLocalRecord(eventName, async () =>
        {
            if (reservationError != null) throw reservationError;
            try { return await Task.Run(() => { ObjectDisposedException.ThrowIf(localDisposed, this); SpectrumBackend.LocalCancellationToken.ThrowIfCancellationRequested(); return work(); }); }
            finally { SpectrumBackend.EndLocalCommand(); }
        }, ownsResult);
    }

    private MsgRecord StartLocalRecord(string eventName, Func<Task<object?>> work, Func<bool>? ownsResult = null)
    {
        var record = new MsgRecord { MsgID = Guid.NewGuid().ToString(), SendTime = DateTime.Now,
            MsgSend = new MsgSend { EventName = eventName, DeviceCode = Code }, MsgRecordState = MsgRecordState.Sended };
        async Task Complete()
        {
            object? data = null;
            Exception? failure = null;
            try
            {
                data = await work();
                if (eventName == "ResetDark")
                {
                    DisplayConfig.IsAutoDark = false;
                    DisplayConfig.IsShutter = false;
                }
            }
            catch (OperationCanceledException ex) { failure = new OperationCanceledException("光谱仪操作已停止。", ex); }
            catch (Exception ex) { failure = ex; log.Error(ex); }
            record.ReciveTime = DateTime.Now;
            record.MsgReturn = new MsgReturn { MsgID = record.MsgID, EventName = eventName, DeviceCode = Code,
                Code = failure == null ? 0 : -1, Message = failure?.Message ?? "ok", Data = data! };
            record.MsgRecordState = failure == null ? MsgRecordState.Success : MsgRecordState.Fail;
            if (failure != null && !localDisposed && (ownsResult?.Invoke() ?? true))
                LocalCaptureFailed?.Invoke(failure.Message);
        }
        if (Application.Current?.Dispatcher is { } dispatcher) dispatcher.BeginInvoke(async () => await Complete());
        else _ = Task.Run(Complete);
        return record;
    }

    private MsgRecord StartLocalContinuous(LocalSpectrumParameters parameters, LocalSpectrumOpticalScope optics,
        MySqlConnectedEndpoint? resultDatabase, string deviceCode)
    {
        bool OwnsResult() => string.Equals(Code, deviceCode, StringComparison.Ordinal)
            && (resultDatabase == null || OwnsLocalSpectrumResultDatabase(resultDatabase));
        Exception? reservationError = null;
        try { parameters.Validate(); SpectrumBackend.BeginLocalCommand(); }
        catch (Exception ex) { reservationError = ex; }
        if (reservationError != null) return StartLocalRecord("GetDataAuto", () => Task.FromException<object?>(reservationError), OwnsResult);
        CancellationToken cancellation = SpectrumBackend.LocalCancellationToken;
        void Release()
        {
            try { if (!localDisposed) SpectrumBackend.SetLocalStatus(LocalSession.IsOpen ? DeviceStatusType.Opened : DeviceStatusType.Closed); }
            finally { SpectrumBackend.EndLocalCommand(); }
        }
        return StartLocalRecord("GetDataAuto", async () =>
        {
            try
            {
                await Task.Run(() => { cancellation.ThrowIfCancellationRequested(); EnsureLocalSpectrumConnected(optics); optics.EnsureCurrent(); });
                cancellation.ThrowIfCancellationRequested();
                if (!OwnsResult()) throw new InvalidOperationException("本地光谱连续采集的结果数据库已更改。");
                SpectrumBackend.SetLocalStatus(DeviceStatusType.SP_Continuous_Mode);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (!cancellation.IsCancellationRequested && !localDisposed && OwnsResult())
                        {
                            var result = CaptureLocalSpectrum(parameters, optics);
                            result.DeviceCode = deviceCode;
                            LocalSpectrumResultService.Save(result, parameters, resultDatabase);
                            // A completed capture is a result even if stop was requested while the SDK was running.
                            if (Application.Current?.Dispatcher is { } dispatcher)
                                await dispatcher.InvokeAsync(() => DisplayLocalSpectrum(result, true, optics.ConfigurationTarget, resultDatabase, optics));
                            await Task.Delay(100, cancellation);
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        log.Error(ex);
                        Application.Current?.Dispatcher.BeginInvoke(() =>
                        {
                            if (!localDisposed && OwnsResult()) LocalCaptureFailed?.Invoke(ex.Message);
                        });
                    }
                    finally { Release(); }
                });
                return null;
            }
            catch { Release(); throw; }
        }, OwnsResult);
    }

    private MsgRecord FinishLocalCommand(string eventName) => StartLocalRecord(eventName, async () =>
    {
        await SpectrumBackend.StopLocalCommandAsync();
        if (eventName == "Close")
        {
            SpectrumBackend.BeginLocalCommand();
            try
            {
                try { await Task.Run(LocalSession.Close); }
                finally { SpectrumBackend.SetLocalStatus(LocalSession.IsQuarantined ? DeviceStatusType.Unknown : DeviceStatusType.Closed); }
            }
            finally { SpectrumBackend.EndLocalCommand(); }
        }
        return null;
    });

    public override void RestartRCService()
    {
        // Calibration and capture settings are read again on the next local measurement.
        if (SpectrumBackend?.OpensLocally == true) return;
        base.RestartRCService();
    }

    public override void Dispose()
    {
        if (localDisposed) return;
        localDisposed = true;
        if (Interlocked.Exchange(ref localWindow, null) is { } window && !window.Dispatcher.HasShutdownStarted)
            _ = window.Dispatcher.BeginInvoke(new Action(window.CloseForDeviceRetirement));
        CancellationTokenSource? pendingRestart;
        lock (calibrationRestartSync)
        {
            pendingRestart = calibrationRestartCts;
            calibrationRestartCts = null;
        }
        pendingRestart?.Cancel();
        pendingRestart?.Dispose();
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            if (!dispatcher.HasShutdownStarted) _ = dispatcher.BeginInvoke(new Action(_view.Dispose));
        }
        else _view.Dispose();
        DisplayConfig.PropertyChanged -= LocalDisplayConfigChanged;
        SpectrumBackend.Changed -= RefreshLocalSpectrumStatus;
        _ = SpectrumBackend.StopLocalCommandAsync();
        try { LocalSession.Dispose(); }
        catch (Exception ex) { log.Error(ex); }
        DService.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
