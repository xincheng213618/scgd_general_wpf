using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Local;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.UI;
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
    internal LocalSpectrumSession LocalSession { get; } = new();
    private LocalSpectrumWindow? localWindow;
    private readonly object continuousSync = new();
    private CancellationTokenSource? continuousCancellation;
    private Task? continuousTask;
    private bool localDisposed;
    public event Action<LocalSpectrumCapture, bool>? LocalResultAvailable;
    public event Action<string>? LocalCaptureFailed;
    [CommandDisplay("本地光谱仪管理", Order = -2, CategoryOrder = 0), Category("DeviceConnection")]
    public RelayCommand OpenLocalSpectrumManagerCommand { get; private set; } = null!;

    private void InitializeLocalSpectrum()
    {
        if (SysResourceDao.IsLocalId(SysResourceModel.Id)) DisplayConfig.UseLocalSpectrum = true;
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
    private void RefreshLocalSpectrumStatus() => DService?.RefreshBackendStatus();

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

    internal LocalSpectrumCapture CaptureLocalSpectrum(LocalSpectrumParameters parameters, ConfigSpectrum config, bool autoConnect, bool continuous = false)
    {
        EnsureLocalSpectrumConnected(autoConnect, config);
        if (!continuous) SpectrumBackend.SetLocalStatus(DeviceStatusType.Busy);
        try
        {
            LocalSpectrumCapture result = LocalSession.Capture(parameters, config);
            result.DeviceCode = Code;
            return result;
        }
        finally { if (!continuous) SpectrumBackend.SetLocalStatus(DeviceStatusType.Opened); }
    }

    internal void PublishLocalSpectrum(LocalSpectrumCapture result)
    {
        Application.Current?.Dispatcher.BeginInvoke(() => DisplayLocalSpectrum(result, false));
    }

    private void DisplayLocalSpectrum(LocalSpectrumCapture result, bool continuous)
    {
        if (localDisposed) return;
        DisplayConfig.IntTime = result.IntegralTime;
        if (continuous) View.SetLocalPreview(CreateLocalSpectrumView(result));
        else View.AddViewResultSpectrum(CreateLocalSpectrumView(result));
        LocalResultAvailable?.Invoke(result, continuous);
    }

    internal static ViewResultSpectrum CreateLocalSpectrumView(LocalSpectrumCapture result)
    {
        var view = new ViewResultSpectrum(result.Data) { CreateTime = result.CapturedAt };
        if (result.EqeData is { } eqe)
        {
            view.Eqe = eqe.dEqe; view.V = (float)eqe.dVoltage; view.I = (float)eqe.dCurrent;
            view.LuminousFlux = eqe.dIm; view.RadiantFlux = eqe.dW;
        }
        return view;
    }

    internal MsgRecord RunLocalSpectrumCommand(string eventName)
    {
        // Snapshot UI settings before leaving the dispatcher; continuous runs keep their starting parameters.
        LocalSpectrumParameters parameters = BuildLocalSpectrumParameters(eventName.StartsWith("EQE", StringComparison.Ordinal) ? true : null);
        ConfigSpectrum config = SnapshotLocalConfiguration();
        if (eventName == "GetDataAutoStop" || eventName == "Close") return FinishLocalContinuousCommand(eventName);
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
            else if (eventName == "InitAutoDark") LocalSession.InitAutoDark(config);
            else throw new NotSupportedException($"本地光谱仪暂不支持 {eventName} 操作。");
            return null;
        });
    }

    private MsgRecord RunLocalSpectrumWork(string eventName, Func<object?> work)
    {
        Exception? reservationError = null;
        try { SpectrumBackend.BeginLocalCommand(); }
        catch (Exception ex) { reservationError = ex; }
        return StartLocalRecord(eventName, async () =>
        {
            if (reservationError != null) throw reservationError;
            try { return await Task.Run(() => { ObjectDisposedException.ThrowIf(localDisposed, this); return work(); }); }
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
            try { data = await work(); }
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
        var cancellation = new CancellationTokenSource();
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (continuousSync)
        {
            continuousCancellation = cancellation;
            continuousTask = stopped.Task;
        }
        void Release()
        {
            SpectrumBackend.EndLocalCommand();
            if (!localDisposed) SpectrumBackend.SetLocalStatus(LocalSession.IsOpen ? DeviceStatusType.Opened : DeviceStatusType.Closed);
            lock (continuousSync)
            {
                if (ReferenceEquals(continuousCancellation, cancellation)) continuousCancellation = null;
            }
            cancellation.Dispose();
            stopped.TrySetResult();
        }
        return StartLocalRecord("GetDataAuto", async () =>
        {
            try
            {
                await Task.Run(() => EnsureLocalSpectrumConnected(false, config));
                cancellation.Token.ThrowIfCancellationRequested();
                SpectrumBackend.SetLocalStatus(DeviceStatusType.SP_Continuous_Mode);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (!cancellation.IsCancellationRequested)
                        {
                            var result = CaptureLocalSpectrum(parameters, config, false, continuous: true);
                            if (!cancellation.IsCancellationRequested && Application.Current?.Dispatcher is { } dispatcher)
                                await dispatcher.InvokeAsync(() => { if (!cancellation.IsCancellationRequested) DisplayLocalSpectrum(result, true); });
                            await Task.Delay(100, cancellation.Token);
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

    private MsgRecord FinishLocalContinuousCommand(string eventName) => StartLocalRecord(eventName, async () =>
    {
        Task? task;
        lock (continuousSync) { continuousCancellation?.Cancel(); task = continuousTask; }
        if (task != null) await task;
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
        DisplayConfig.PropertyChanged -= LocalDisplayConfigChanged;
        SpectrumBackend.Changed -= RefreshLocalSpectrumStatus;
        lock (continuousSync) continuousCancellation?.Cancel();
        try { LocalSession.Dispose(); }
        catch (Exception ex) { log.Error(ex); }
        DService.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
