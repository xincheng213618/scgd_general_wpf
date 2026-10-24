using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.CfwPort.Local;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.CfwPort;

// Local SDK commands have no MQTT timer. Consumers must wait for native completion before releasing a calibration reservation.
internal sealed class LocalCfwPortRecord : MsgRecord { }

public partial class DeviceCfwPort
{
    internal LocalCfwPortSession LocalSession { get; }
    internal CfwPortBackendState CfwBackend { get; private set; } = null!;
    private bool localDisposed;
    private bool autoConnectAttempted;

    internal bool TryBeginLocalAutoConnect()
    {
        if (autoConnectAttempted || !Config.IsAutoOpen || !CfwBackend.OpensLocally || CfwBackend.LocalOwned) return false;
        autoConnectAttempted = true;
        return true;
    }

    private void InitializeLocalCfwPort()
    {
        CfwBackend = new(DisplayConfig.UseLocalCfwPort);
        CfwBackend.Changed += RefreshLocalCfwStatus;
        LocalSession.StatusChanged += CfwBackend.SetLocalStatus;
        DisplayConfig.PropertyChanged += LocalCfwPreferenceChanged;
        LocalCfwPortFlowExecution.Register();
    }

    private void LocalCfwPreferenceChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(DisplayCfwPortConfig.UseLocalCfwPort)) return;
        CfwBackend.SetPreference(DisplayConfig.UseLocalCfwPort);
    }

    private void RefreshLocalCfwStatus()
    {
        void Refresh()
        {
            if (localDisposed) return;
            DService?.RefreshBackendStatus();
        }
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess()) dispatcher.BeginInvoke(Refresh);
        else Refresh();
    }

    internal void EnsureOtherCfwBackends(bool local, LocalCfwPortConnection config)
    {
        if (!local) LocalCfwPortSession.EnsureNotLocallyOwned(config.PortName);
        foreach (var other in ServiceManager.Current?.DeviceServices.OfType<DeviceCfwPort>() ?? Enumerable.Empty<DeviceCfwPort>())
        {
            if (ReferenceEquals(other, this) || !string.Equals(other.LocalSession.Endpoint ?? other.CfwBackend.Endpoint ?? LocalCfwPortConnection.From(other.Config).PortName, config.PortName, StringComparison.OrdinalIgnoreCase)) continue;
            other.CfwBackend.EnsureAvailable(local);
            if (local && other.CfwBackend.LocalOwned) throw new InvalidOperationException("另一个本地滤色轮正在使用此串口。");
        }
    }

    internal async Task<int?> ExecuteLocalCfwAsync(string operation, LocalCfwPortConnection config, int? port, bool autoConnect, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(localDisposed, this);
        lock (CfwPortBackendState.Sync)
        {
            if (operation != "Close") EnsureOtherCfwBackends(true, config);
            CfwBackend.Begin(true, operation, config.PortName);
        }
        try { return await LocalSession.ExecuteAsync(operation, config, port, autoConnect, token).ConfigureAwait(false); }
        finally { CfwBackend.End(true); }
    }

    internal MsgRecord RunLocalCfwCommand(MsgSend message)
    {
        message.MsgID ??= Guid.NewGuid().ToString();
        message.DeviceCode ??= Code;
        var record = new LocalCfwPortRecord { MsgID = message.MsgID, MsgSend = message, SendTime = DateTime.Now, MsgRecordState = MsgRecordState.Sended };
        var config = LocalCfwPortConnection.From(Config);
        async Task Run()
        {
            int? port = null;
            Exception? failure = null;
            try
            {
                int? requested = message.Params == null ? null : JObject.FromObject(message.Params).Value<int?>("PortNum");
                port = await ExecuteLocalCfwAsync(message.EventName, config, requested, false, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex) { failure = ex; MQTTServiceBase.log.Error(ex); }
            void Finish()
            {
                record.ReciveTime = DateTime.Now;
                record.MsgReturn = new MsgReturn { MsgID = record.MsgID, DeviceCode = message.DeviceCode, EventName = message.EventName,
                    Code = failure == null ? 0 : -1, Message = failure?.Message ?? "ok", Data = port.HasValue ? new JObject { ["Port"] = port.Value } : new JObject() };
                record.MsgRecordState = failure == null ? MsgRecordState.Success : MsgRecordState.Fail;
                if (!localDisposed) SetMsgRecordChanged(record);
            }
            if (Application.Current?.Dispatcher is { } dispatcher) await dispatcher.InvokeAsync(Finish);
            else Finish();
        }
        _ = Run(); // Enter the session before returning, preserving Open/Close ordering.
        return record;
    }

    internal void UpdateLocalPort(int port)
    {
        void Update() { if (!localDisposed) DisplayConfig.PortNum = port; }
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess()) dispatcher.BeginInvoke(Update);
        else Update();
    }

    protected override void OnConfigChanged()
    {
        base.OnConfigChanged();
        DisplayConfigManager.Instance.Configs[Config.Code] = DisplayConfig;
    }

    public override void RestartRCService()
    {
        if (CfwBackend.OpensLocally || DisplayConfig.UseLocalCfwPort) return;
        base.RestartRCService();
    }

    private void DisposeLocalCfwPort()
    {
        if (localDisposed) return;
        localDisposed = true;
        DisplayConfig.PropertyChanged -= LocalCfwPreferenceChanged;
        CfwBackend.Changed -= RefreshLocalCfwStatus;
        LocalSession.StatusChanged -= CfwBackend.SetLocalStatus;
        _ = LocalSession.DisposeAsync().AsTask().ContinueWith(task => { if (task.Exception != null) MQTTServiceBase.log.Error(task.Exception); }, TaskScheduler.Default);
        DService.Dispose();
    }
}
