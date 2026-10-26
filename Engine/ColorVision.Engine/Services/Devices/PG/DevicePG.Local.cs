using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.PG.Local;
using ColorVision.Engine.Services.Devices.PG.Templates;
using ColorVision.Engine.Services.Devices.Sensor.Local;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.PG;

public partial class DevicePG
{
    internal SensorBackendState PGBackend { get; private set; } = null!;
    internal LocalSensorSession LocalSession { get; } = new();
    internal string LocalEndpoint => Config.IsNet ? $"tcp:{Config.Addr?.Trim().ToUpperInvariant()}:{Config.Port}" : $"serial:{Config.Addr?.Trim().ToUpperInvariant()}";
    private bool localDisposed;
    private LocalPGConnection? activeConnection;
    public string LastLocalResponse { get => lastLocalResponse; private set { lastLocalResponse = value; OnPropertyChanged(); } }
    private string lastLocalResponse = string.Empty;

    private void InitializeLocalPG()
    {
        PGBackend = new SensorBackendState(DisplayConfig.UseLocalPG, "PG");
        PGBackend.Changed += RefreshLocalPGStatus;
        LocalSession.StatusChanged += LocalPGStatusChanged;
        DisplayConfig.PropertyChanged += LocalPGPreferenceChanged;
        LocalSession.Diagnostics.DeviceCode = Code;
        LocalPGFlowExecution.Register();
    }

    private void LocalPGPreferenceChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(DisplayPGConfig.UseLocalPG)) PGBackend.SetPreference(DisplayConfig.UseLocalPG);
    }
    private void LocalPGStatusChanged(DeviceStatusType status)
    {
        lock (PGBackend.Sync)
        {
            if (status == DeviceStatusType.Closed) activeConnection = null;
            PGBackend.SetLocalStatus(status);
        }
    }
    private void RefreshLocalPGStatus() => DService?.RefreshBackendStatus();

    protected override void OnConfigChanged()
    {
        base.OnConfigChanged();
        LocalSession.Diagnostics.DeviceCode = Code;
        DisplayConfigManager.Instance.Configs[Code] = DisplayConfig;
    }

    internal LocalPGConnection SnapshotLocalPGConfig()
    {
        lock (PGBackend.Sync) if (activeConnection != null) return activeConnection;
        var config = JsonConvert.DeserializeObject<ConfigPG>(JsonConvert.SerializeObject(Config))!;
        var display = JsonConvert.DeserializeObject<DisplayPGConfig>(JsonConvert.SerializeObject(DisplayConfig))!;
        LocalPGProtocol.Validate(config, display);
        var transport = new LocalSensorConnectionConfig { IsNet = config.IsNet, Addr = config.Addr, Port = config.Port, ConnectTimeout = display.ConnectTimeout };
        transport.ValidateLocalConnection();
        // GECS has built-in framed commands. Only real user templates override those defaults.
        var commands = config.Category == "GECS.V2.4"
            ? TemplatePGParam.Params.FirstOrDefault(item => item.Key == config.Category)?.Value.ConvertToMap() ?? new Dictionary<string, string>()
            : DService.PGCategoryLib.TryGetValue(config.Category, out var values) ? new Dictionary<string, string>(values) : new Dictionary<string, string>();
        return new LocalPGConnection(transport, new LocalPGProtocol(config, display.CommandTimeout, commands));
    }

    internal async Task<object?> ExecuteLocalPGAsync(string operation, object? parameters, LocalPGConnection? snapshot, CancellationToken token, bool autoConnect)
    {
        ObjectDisposedException.ThrowIf(localDisposed, this);
        // Closing must always work, even after the endpoint/category has been edited.
        LocalSensorCommand[]? commands;
        lock (PGBackend.Sync)
        {
            snapshot = activeConnection ?? snapshot;
            commands = operation is "SetParam" or "ExecCmd" ? snapshot!.Protocol.Build(parameters) : null;
            if (operation != "Close") LocalCommunicationOwnership.EnsureAvailable(this, true, LocalSession.EndpointIdentity ?? LocalSensorSession.EndpointKey(snapshot!.Transport));
            PGBackend.BeginLocalCommand();
            if (operation == "Open" || (autoConnect && commands != null)) activeConnection ??= snapshot;
        }
        try
        {
            switch (operation)
            {
                case "Open": await LocalSession.OpenAsync(snapshot!.Transport, token).ConfigureAwait(false); return null;
                case "Close": await LocalSession.CloseAsync().ConfigureAwait(false); return null;
                case "SetParam":
                case "ExecCmd":
                    if (autoConnect && !LocalSession.IsOpen) await LocalSession.OpenAsync(snapshot!.Transport, token).ConfigureAwait(false);
                    var results = await LocalSession.ExecuteAsync(commands!, token).ConfigureAwait(false);
                    var measurement = results.LastOrDefault(result => result.Name == "POWER.MEASURE");
                    return new
                    {
                        CmdType = measurement == null ? "" : "POWER.MEASURE",
                        ResultData = measurement == null ? "" : LocalPGProtocol.Measurement(LocalPGProtocol.Payload(Convert.FromHexString(measurement.ResponseHex))).ToString(Formatting.None),
                        Message = "ok", Commands = results
                    };
                default: throw new NotSupportedException($"本地 PG 不支持 {operation}。");
            }
        }
        finally
        {
            lock (PGBackend.Sync)
            {
                if (!LocalSession.IsOpen) activeConnection = null;
                PGBackend.EndLocalCommand();
            }
        }
    }

    internal MsgRecord RunLocalPGCommand(MsgSend message)
    {
        message.MsgID ??= Guid.NewGuid().ToString();
        message.DeviceCode ??= Code;
        var record = new MsgRecord { MsgID = message.MsgID, SendTime = DateTime.Now, MsgSend = message, MsgRecordState = MsgRecordState.Sended };
        async Task Run()
        {
            object? result = null;
            Exception? failure = null;
            try
            {
                var snapshot = message.EventName == "Close" ? null : SnapshotLocalPGConfig();
                result = await ExecuteLocalPGAsync(message.EventName, message.Params, snapshot, CancellationToken.None, false).ConfigureAwait(false);
            }
            catch (Exception ex) { failure = ex; MQTTServiceBase.log.Error(ex); }
            void Finish()
            {
                record.ReciveTime = DateTime.Now;
                record.MsgReturn = new MsgReturn { MsgID = record.MsgID, EventName = message.EventName, DeviceCode = Code, Code = failure == null ? 0 : -1, Message = failure?.Message ?? "ok", Data = result! };
                record.MsgRecordState = failure is TimeoutException ? MsgRecordState.Timeout : failure == null ? MsgRecordState.Success : MsgRecordState.Fail;
                PublishLocalPGResult(result, failure);
                SetMsgRecordChanged(record);
            }
            if (Application.Current?.Dispatcher is { } dispatcher) await dispatcher.InvokeAsync(Finish);
            else Finish();
        }
        _ = Run();
        return record;
    }

    internal void PublishLocalPGResult(object? result, Exception? failure = null)
    {
        void Update()
        {
            string? measurement = result == null ? null : JObject.FromObject(result).Value<string>("ResultData");
            LastLocalResponse = failure?.Message ?? (string.IsNullOrEmpty(measurement) ? Properties.Resources.Success : measurement);
        }
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess()) dispatcher.BeginInvoke(Update);
        else Update();
    }

    public override void Dispose()
    {
        if (localDisposed) return;
        localDisposed = true;
        try { LocalSession.Dispose(); }
        finally
        {
            LocalSession.StatusChanged -= LocalPGStatusChanged;
            PGBackend.Changed -= RefreshLocalPGStatus;
            DisplayConfig.PropertyChanged -= LocalPGPreferenceChanged;
            DService.Dispose();
            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}

internal sealed record LocalPGConnection(LocalSensorConnectionConfig Transport, LocalPGProtocol Protocol);
