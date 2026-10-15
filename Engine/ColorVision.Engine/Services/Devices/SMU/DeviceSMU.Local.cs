using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.SMU.Local;
using ColorVision.Engine.Services.Devices.Spectrum;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.SMU;

public partial class DeviceSMU
{
    internal SmuBackendState SmuBackend { get; private set; } = null!;
    internal LocalSmuSession LocalSession { get; }
    private bool localDisposed;
    public string LocalDeviceIdentity { get => localDeviceIdentity; private set { localDeviceIdentity = value; OnPropertyChanged(); } }
    private string localDeviceIdentity = string.Empty;

    private void InitializeLocalSmu()
    {
        if (SysResourceDao.IsLocalId(SysResourceModel.Id)) DisplayConfig.UseLocalSmu = true;
        SmuBackend = new SmuBackendState(DisplayConfig.UseLocalSmu);
        SmuBackend.Changed += RefreshSmuBackendStatus;
        LocalSession.StatusChanged += LocalSmuStatusChanged;
        DisplayConfig.PropertyChanged += LocalSmuPreferenceChanged;
        LocalSmuFlowExecution.Register();
    }
    private void LocalSmuPreferenceChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(DisplaySMUConfig.UseLocalSmu)) SmuBackend.SetPreference(DisplayConfig.UseLocalSmu);
    }
    protected override void OnConfigChanged()
    {
        base.OnConfigChanged();
        DisplayConfigManager.Instance.Configs[Config.Code] = DisplayConfig;
    }
    private void LocalSmuStatusChanged(DeviceStatusType status) => SmuBackend.SetLocalStatus(status);
    private void RefreshSmuBackendStatus()
    {
        void Refresh() { if (!localDisposed) { LocalDeviceIdentity = LocalSession.Idn; DService?.RefreshBackendStatus(); } }
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess()) dispatcher.BeginInvoke(Refresh);
        else Refresh();
    }
    internal void EnsureOtherSmuBackends(bool local, LocalSmuConnection config)
    {
        foreach (var other in ServiceManager.Current?.DeviceServices.OfType<DeviceSMU>() ?? Enumerable.Empty<DeviceSMU>())
        {
            if (ReferenceEquals(other, this) || (other.LocalSession.Endpoint ?? LocalSmuConnection.From(other.Config).Endpoint) != config.Endpoint) continue;
            if (local)
            {
                if (other.SmuBackend.LocalOwned) throw new InvalidOperationException("另一个本地源表正在使用此连接。");
                other.SmuBackend.EnsureLocalAvailable();
            }
            else other.SmuBackend.EnsureServiceAvailable();
        }
    }
    internal LocalSmuParameters ResolveLocalSmuParameters(string operation, JObject data)
    {
        bool scan = operation == "Scan";
        JObject? template = data["TemplateParam"] as JObject;
        if (template != null && data["DeviceParam"] is not JObject)
        {
            LocalSmuParameters Resolve()
            {
                int id = template.Value<int?>("ID") ?? -1;
                string? name = template.Value<string>("Name");
                var matches = TemplateSMUParam.Params.Where(item => id > 0 ? item.Id == id : item.Key == name).ToArray();
                if (matches.Length != 1) throw new InvalidOperationException($"未找到唯一的源表模板：{name ?? id.ToString()}");
                return LocalSmuParameters.FromTemplate(matches[0].Value, matches[0].Value.Channel, data.Value<bool>("IsCloseOutput"));
            }
            return Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess() ? dispatcher.Invoke(Resolve) : Resolve();
        }
        var parameters = (data["DeviceParam"] as JObject ?? data).ToObject<LocalSmuParameters>()!;
        return scan ? parameters with { IsCloseOutput = data.Value<bool>("IsCloseOutput") } : parameters;
    }

    internal async Task<LocalSmuCapture?> ExecuteLocalSmuAsync(string operation, LocalSmuConnection config, LocalSmuParameters parameters,
        bool autoConnect, bool checkLimits, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(localDisposed, this);
        lock (SmuBackend.Sync) { EnsureOtherSmuBackends(true, config); SmuBackend.BeginLocalCommand(); }
        try
        {
            if (operation == "Close") { await LocalSession.CloseAsync().ConfigureAwait(false); return null; }
            if (operation == "Reopen") await LocalSession.CloseAsync().ConfigureAwait(false);
            if (operation is "Open" or "Reopen" || (autoConnect && !LocalSession.IsOpen))
                await LocalSession.OpenAsync(config, token).ConfigureAwait(false);
            if (operation is "Open" or "Reopen") return null;
            if (operation == "CloseOutput") { await LocalSession.CloseOutputAsync(parameters.Channel, token).ConfigureAwait(false); return null; }
            if (operation is not ("GetData" or "StepData" or "Scan" or "SMU.MeasureResult")) throw new NotSupportedException($"本地源表暂不支持 {operation}。");
            return await LocalSession.CaptureAsync(config, parameters, operation == "Scan", operation != "GetData", checkLimits,
                read: operation == "SMU.MeasureResult", token: token).ConfigureAwait(false);
        }
        finally { SmuBackend.EndLocalCommand(); }
    }

    internal MsgRecord RunLocalSmuCommand(MsgSend message)
    {
        message.MsgID ??= Guid.NewGuid().ToString(); message.DeviceCode ??= Code;
        var record = new MsgRecord { MsgID = message.MsgID, SendTime = DateTime.Now, MsgSend = message, MsgRecordState = MsgRecordState.Sended };
        var config = LocalSmuConnection.From(Config);
        var data = message.Params == null ? new JObject() : JObject.FromObject(message.Params);
        if (message.EventName == "Open") config = config with { IsNet = data.Value<bool?>("IsNet") ?? config.IsNet, DeviceName = data.Value<string>("DevName")?.Trim() ?? config.DeviceName };
        bool checkLimits = DisplayConfig.IsUseLimitSigned;
        LocalSmuParameters? parameters = null;
        Exception? setupFailure = null;
        try { parameters = ResolveLocalSmuParameters(message.EventName, data); }
        catch (Exception ex) { setupFailure = ex; }
        async Task Run()
        {
            LocalSmuCapture? capture = null;
            object? result = null;
            Exception? failure = setupFailure;
            int id = 0;
            try
            {
                if (failure != null) throw failure;
                capture = await ExecuteLocalSmuAsync(message.EventName, config, parameters!, false, checkLimits, CancellationToken.None).ConfigureAwait(false);
                if (capture != null)
                {
                    id = await Task.Run(() => LocalSmuResultService.Save(capture, Code)).ConfigureAwait(false);
                    result = LocalSmuResultService.Response(capture, id);
                }
            }
            catch (Exception ex) { failure = ex; MQTTServiceBase.log.Error(ex); }
            void Finish()
            {
                if (!localDisposed && capture != null) PublishLocalSmu(capture, id);
                if (!localDisposed && failure == null && message.EventName is "Close" or "CloseOutput") ClearLocalSmuReadings(parameters!.Channel, message.EventName == "Close");
                record.ReciveTime = DateTime.Now;
                record.MsgReturn = new MsgReturn { MsgID = record.MsgID, EventName = message.EventName, DeviceCode = Code,
                    Code = failure == null ? 0 : -1, Message = failure?.Message ?? "ok", Data = result! };
                record.MsgRecordState = failure == null ? MsgRecordState.Success : MsgRecordState.Fail;
                if (!localDisposed) SetMsgRecordChanged(record);
            }
            if (Application.Current?.Dispatcher is { } dispatcher) await dispatcher.InvokeAsync(Finish);
            else Finish();
        }
        // Reserve/enter the session before returning so a queued Open cannot overtake Close.
        _ = Run();
        return record;
    }
    internal void PublishLocalSmu(LocalSmuCapture capture, int id)
    {
        void Update()
        {
            if (localDisposed) return;
            View.AddViewResultSMU(LocalSmuResultService.CreateView(capture, Code, id));
            if (!capture.IsScan)
            {
                var channel = capture.Parameters.Channel == SMUChannelType.A ? DisplayConfig.ChannelA : DisplayConfig.ChannelB;
                channel.V = capture.V; channel.I = capture.I;
                if (capture.Parameters.Channel == DisplayConfig.Channel)
                {
                    DisplayConfig.V = capture.V; DisplayConfig.I = capture.I;
                    foreach (var spectrum in ServiceManager.Current?.DeviceServices.OfType<DeviceSpectrum>() ?? Enumerable.Empty<DeviceSpectrum>())
                    { spectrum.DisplayConfig.V = capture.V; spectrum.DisplayConfig.I = capture.I; }
                }
            }
            if (capture.Parameters.IsCloseOutput) ClearLocalSmuReadings(capture.Parameters.Channel, false);
        }
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess()) dispatcher.BeginInvoke(Update);
        else Update();
    }
    private void ClearLocalSmuReadings(SMUChannelType channel, bool all)
    {
        if (all || channel == SMUChannelType.A) { DisplayConfig.ChannelA.V = null; DisplayConfig.ChannelA.I = null; }
        if (all || channel == SMUChannelType.B) { DisplayConfig.ChannelB.V = null; DisplayConfig.ChannelB.I = null; }
        DisplayConfig.V = DisplayConfig.CurrentChannelConfig.V; DisplayConfig.I = DisplayConfig.CurrentChannelConfig.I;
    }
    public override void RestartRCService()
    {
        if (SmuBackend.OpensLocally) return;
        base.RestartRCService();
    }
    public override void Dispose()
    {
        if (localDisposed) return;
        localDisposed = true;
        DisplayConfig.PropertyChanged -= LocalSmuPreferenceChanged;
        SmuBackend.Changed -= RefreshSmuBackendStatus;
        LocalSession.StatusChanged -= LocalSmuStatusChanged;
        _ = LocalSession.DisposeAsync().AsTask().ContinueWith(task => { if (task.Exception != null) MQTTServiceBase.log.Error(task.Exception); }, TaskScheduler.Default);
        DService.Dispose(); base.Dispose(); GC.SuppressFinalize(this);
    }
}
