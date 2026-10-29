using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Local;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Engine.Services.PhyCameras.Licenses;
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
    private bool localDisposed;
    public event Action<LocalSpectrumCapture, bool>? LocalResultAvailable;
    public event Action<string>? LocalCaptureFailed;
    [Browsable(false)]
    public bool IsLocalSpectrumBackend => SpectrumBackend.OpensLocally;
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
    }
    private void RefreshLocalSpectrumStatus()
    {
        DService?.RefreshBackendStatus();
        OnPropertyChanged(nameof(IsLocalSpectrumBackend));
    }

    public void OpenLocalSpectrumWindow(ColorVision.Engine.FlowProcessing.Nodes.LocalSpectrumNode? node = null)
    {
        if (localWindow != null) { localWindow.AttachNode(node); localWindow.Activate(); return; }
        localWindow = new LocalSpectrumWindow(this) { Owner = Application.Current.GetActiveWindow() };
        localWindow.AttachNode(node);
        localWindow.Closed += (_, _) => localWindow = null;
        localWindow.Show();
    }

    internal LocalSpectrumParameters BuildLocalSpectrumParameters(bool? eqe = null) => new()
    {
        IntegralTime = (float)DisplayConfig.IntTime, NumberOfAverage = DisplayConfig.AveNum,
        AutoIntegration = DisplayConfig.IsAutoIntTime, AutoInitDark = DisplayConfig.IsAutoDark,
        SelfAdaptionInitDark = DisplayConfig.IsShutter, IsWithND = DisplayConfig.IsWithND,
        Eqe = eqe ?? DisplayConfig.IsLuminousFluxMode, AFactor = DisplayConfig.Divisor,
        Voltage = (float)DisplayConfig.V, Current = (float)DisplayConfig.I
    };

    internal ConfigSpectrum SnapshotLocalConfiguration() => JsonConvert.DeserializeObject<ConfigSpectrum>(JsonConvert.SerializeObject(Config))!;

    internal void EnsureOtherSpectrumBackends(bool local)
    {
        foreach (var other in ServiceManager.Current?.DeviceServices.OfType<DeviceSpectrum>() ?? Enumerable.Empty<DeviceSpectrum>())
        {
            if (ReferenceEquals(other, this) || !string.Equals(other.Config.SN, Config.SN, StringComparison.OrdinalIgnoreCase)) continue;
            if (local) other.SpectrumBackend.EnsureLocalAvailable();
            else other.SpectrumBackend.EnsureServiceAvailable();
        }
    }

    private void EnsureLocalSpectrumLicense(ConfigSpectrum config)
    {
        if (ExperimentalFeaturesConfig.Instance.IsEnabled) return;
        LicenseModel? license = PhyLicenseDao.Instance.GetByMAC(config.SN, SysResourceDao.IsLocalId(SysResourceModel.Id) || !ColorVision.Database.MySqlSetting.IsConnect);
        LocalSpectrumLicense.EnsureNotExpired(license?.LiceType == 1 ? license.ExpiryDate : null, skipValidation: false, DateTime.Now);
    }

    // Callers reserve the backend before scheduling native work. The session itself serializes all handle access.
    internal void EnsureLocalSpectrumConnected(bool autoConnect, ConfigSpectrum config)
    {
        ObjectDisposedException.ThrowIf(localDisposed, this);
        if (LocalSession.IsOpen) return;
        if (!autoConnect) throw new InvalidOperationException("请先打开本地光谱仪。");
        lock (SpectrumBackend.Sync)
        {
            EnsureOtherSpectrumBackends(true);
            SpectrumBackend.SetLocalStatus(DeviceStatusType.Opening);
        }
        try
        {
            LocalSpectrumLicense.EnsureAvailable(config.SN, SysResourceDao.IsLocalId(SysResourceModel.Id));
            LocalSession.Open(config);
            SpectrumBackend.SetLocalStatus(DeviceStatusType.Opened);
        }
        catch { SpectrumBackend.SetLocalStatus(LocalSession.IsQuarantined ? DeviceStatusType.Unknown : DeviceStatusType.Closed); throw; }
    }

    internal LocalSpectrumCapture CaptureLocalSpectrum(LocalSpectrumParameters parameters, ConfigSpectrum config, bool autoConnect, CancellationToken cancellationToken = default)
    {
        EnsureLocalSpectrumConnected(autoConnect, config);
        CancellationToken token = cancellationToken.CanBeCanceled ? cancellationToken : SpectrumBackend.LocalCancellationToken;
        LocalSpectrumCapture result = LocalSession.Capture(parameters, config, token);
        result.DeviceCode = Code;
        return result;
    }

    internal void PublishLocalSpectrum(LocalSpectrumCapture result)
    {
        Application.Current?.Dispatcher.BeginInvoke(() => DisplayLocalSpectrum(result, false));
    }

    private void DisplayLocalSpectrum(LocalSpectrumCapture result, bool continuous)
    {
        if (localDisposed) return;
        if (result.NDPort.HasValue) ApplyLocalNdState(result.NDPort.Value, result.NdCalibration);
        DisplayConfig.IntTime = result.IntegralTime;
        PresentResult(() => CreateLocalSpectrumView(result));
        LocalResultAvailable?.Invoke(result, continuous);
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
        // Snapshot UI settings before leaving the dispatcher; continuous runs keep their starting parameters.
        LocalSpectrumParameters parameters = BuildLocalSpectrumParameters(eventName.StartsWith("EQE", StringComparison.Ordinal) ? true : null);
        ConfigSpectrum config = SnapshotLocalConfiguration();
        if (eventName == "GetDataAutoStop" || eventName == "Close") return FinishLocalCommand(eventName);
        if (eventName is "GetDataAuto" or "EQE.GetDataAuto") return StartLocalContinuous(parameters, config);
        return RunLocalSpectrumWork(eventName, () =>
        {
            if (eventName == "Open") { EnsureLocalSpectrumConnected(true, config); return null; }
            if (eventName is "GetData" or "EQE.GetData")
            {
                var result = CaptureLocalSpectrum(parameters, config, false);
                var model = LocalSpectrumResultService.Save(result, parameters);
                PublishLocalSpectrum(result);
                return new LocalSpectrumCommandResult(result, model);
            }
            EnsureLocalSpectrumConnected(false, config);
            if (eventName == "InitDark") LocalSession.Dark(parameters, config);
            else if (eventName == "ResetDark") LocalSession.ResetDark(config);
            else if (eventName == "InitAutoDark") LocalSession.InitAutoDark(config);
            else if (eventName is "ShutterConnect" or "ShutterDisconnect" or "ShutterDoOpen" or "ShutterDoClose") LocalSession.ControlShutter(eventName, config);
            else throw new NotSupportedException($"本地光谱仪暂不支持 {eventName} 操作。");
            return null;
        });
    }

    internal bool UsesLocalNd => SpectrumBackend.OpensLocally || (Config.NDConfig.IsBingNDDevice && GetBoundCfwPort()?.CfwBackend.OpensLocally == true);

    internal MsgRecord RunLocalNdCommand(string eventName, int? port)
    {
        ConfigSpectrum config = SnapshotLocalConfiguration();
        bool reserveSpectrum = SpectrumBackend.OpensLocally;
        Exception? reservationError = null;
        try
        {
            if (!config.NDConfig.IsNDPort) throw new InvalidOperationException("请先在光谱仪配置中启用 ND。");
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
                var result = await Task.Run(() => ExecuteLocalNd(config, port, reserveSpectrum ? SpectrumBackend.LocalCancellationToken : CancellationToken.None));
                if (Application.Current?.Dispatcher is { } dispatcher) await dispatcher.InvokeAsync(() => ApplyLocalNdState(result.Port, result.Calibration));
                else ApplyLocalNdState(result.Port, result.Calibration);
                return new { result.Port };
            }
            finally { if (reserveSpectrum) SpectrumBackend.EndLocalCommand(); }
        });
    }

    internal (int Port, LocalSpectrumNd.Calibration Calibration) ExecuteLocalNd(ConfigSpectrum config, int? port, CancellationToken token)
    {
        if (!config.NDConfig.IsNDPort) throw new InvalidOperationException("请先在光谱仪配置中启用 ND。");
        // Validate the destination before moving, then commit files only after the SDK confirms its position.
        LocalSpectrumNd.Calibration? calibration = port.HasValue ? LocalSpectrumNd.ResolveCalibration(config, port.Value) : null;
        int actual = LocalSession.ControlNd(config, port, token);
        return (actual, calibration ?? LocalSpectrumNd.ResolveCalibration(config, actual));
    }

    internal void ApplyLocalNdState(int port, LocalSpectrumNd.Calibration? calibration)
    {
        if (localDisposed) return;
        DisplayConfig.PortNum = port;
        if (calibration == null) return;
        if (calibration.GroupName is { } name)
            ApplyCalibrationGroup(new SpectrumCalibrationGroup { GroupName = name, NDHoleIndex = port,
                WavelengthFile = calibration.WavelengthFile, MaguideFile = calibration.MagnitudeFile }, true);
        else
        {
            if (string.Equals(Config.WavelengthFile, calibration.WavelengthFile, StringComparison.Ordinal)
                && string.Equals(Config.MaguideFile, calibration.MagnitudeFile, StringComparison.Ordinal)) return;
            using var scope = CalibrationGroupChangeGuard.EnterApply();
            Config.WavelengthFile = calibration.WavelengthFile;
            Config.MaguideFile = calibration.MagnitudeFile;
            SaveConfig();
        }
    }

    private MsgRecord RunLocalSpectrumWork(string eventName, Func<object?> work)
    {
        Exception? reservationError = null;
        try { SpectrumBackend.BeginLocalCommand(); }
        catch (Exception ex) { reservationError = ex; }
        return StartLocalRecord(eventName, async () =>
        {
            if (reservationError != null) throw reservationError;
            try { return await Task.Run(() => { ObjectDisposedException.ThrowIf(localDisposed, this); SpectrumBackend.LocalCancellationToken.ThrowIfCancellationRequested(); return work(); }); }
            finally { SpectrumBackend.EndLocalCommand(); }
        });
    }

    private MsgRecord StartLocalRecord(string eventName, Func<Task<object?>> work)
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
            catch (OperationCanceledException ex) { failure = new OperationCanceledException("光谱仪操作已停止。", ex); LocalCaptureFailed?.Invoke(failure.Message); }
            catch (Exception ex) { failure = ex; log.Error(ex); LocalCaptureFailed?.Invoke(ex.Message); }
            record.ReciveTime = DateTime.Now;
            record.MsgReturn = new MsgReturn { MsgID = record.MsgID, EventName = eventName, DeviceCode = Code,
                Code = failure == null ? 0 : -1, Message = failure?.Message ?? "ok", Data = data! };
            record.MsgRecordState = failure == null ? MsgRecordState.Success : MsgRecordState.Fail;
        }
        if (Application.Current?.Dispatcher is { } dispatcher) dispatcher.BeginInvoke(async () => await Complete());
        else _ = Task.Run(Complete);
        return record;
    }

    private MsgRecord StartLocalContinuous(LocalSpectrumParameters parameters, ConfigSpectrum config)
    {
        Exception? reservationError = null;
        try { parameters.Validate(); SpectrumBackend.BeginLocalCommand(); }
        catch (Exception ex) { reservationError = ex; }
        if (reservationError != null) return StartLocalRecord("GetDataAuto", () => Task.FromException<object?>(reservationError));
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
                await Task.Run(() => { cancellation.ThrowIfCancellationRequested(); EnsureLocalSpectrumConnected(false, config); });
                cancellation.ThrowIfCancellationRequested();
                SpectrumBackend.SetLocalStatus(DeviceStatusType.SP_Continuous_Mode);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (!cancellation.IsCancellationRequested)
                        {
                            var result = CaptureLocalSpectrum(parameters, config, false);
                            LocalSpectrumResultService.Save(result, parameters);
                            // A completed capture is a result even if stop was requested while the SDK was running.
                            if (Application.Current?.Dispatcher is { } dispatcher)
                                await dispatcher.InvokeAsync(() => DisplayLocalSpectrum(result, true));
                            await Task.Delay(100, cancellation);
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex) { log.Error(ex); Application.Current?.Dispatcher.BeginInvoke(() => LocalCaptureFailed?.Invoke(ex.Message)); }
                    finally { Release(); }
                });
                return null;
            }
            catch { Release(); throw; }
        });
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
        _view.Dispose();
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
