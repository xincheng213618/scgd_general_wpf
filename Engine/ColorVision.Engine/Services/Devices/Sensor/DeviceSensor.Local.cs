using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Sensor.Local;
using ColorVision.Engine.Services.Devices.Sensor.Templates;
using ColorVision.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Sensor;

public sealed class DisplaySensorConfig : IDisplayConfigBase
{
    [Category("AcquisitionDisplay"), DisplayName("使用本地通用传感器")]
    [Description("下次打开时直接连接 TCP 或串口。已打开的连接在关闭前保持当前模式。")]
    public bool UseLocalSensor { get => useLocal; set { if (useLocal == value) return; useLocal = value; OnPropertyChanged(); } }
    private bool useLocal;

    [Category("本地连接"), DisplayName("连接超时(ms)"), Description("建立本地连接的最长等待时间；指令超时仍使用原模板设置。")]
    public int ConnectTimeout { get; set; } = 3000;
    [Category("本地串口"), DisplayName("数据位")]
    public int DataBits { get; set; } = 8;
    [Category("本地串口"), DisplayName("校验位")]
    public System.IO.Ports.Parity Parity { get; set; }
    [Category("本地串口"), DisplayName("停止位")]
    public System.IO.Ports.StopBits StopBits { get; set; } = System.IO.Ports.StopBits.One;
    [Category("本地串口"), DisplayName("DTR")]
    public bool DtrEnable { get; set; }
    [Category("本地串口"), DisplayName("RTS")]
    public bool RtsEnable { get; set; }
}

public partial class DeviceSensor
{
    internal SensorBackendState SensorBackend { get; private set; } = null!;
    internal LocalSensorSession LocalSession { get; } = new();
    private bool localDisposed;
    public string LastLocalResponse { get => lastLocalResponse; private set { lastLocalResponse = value; OnPropertyChanged(); } }
    private string lastLocalResponse = string.Empty;
    [CommandDisplay("EditDisplayConfig", Order = -1, CategoryOrder = 2)]
    [Category("AcquisitionDisplay"), Description("CommandDisplayConfigHint")]
    public RelayCommand EditDisplayConfigCommand { get; private set; } = null!;

    private void InitializeLocalSensor()
    {
        if (SysResourceDao.IsLocalId(SysResourceModel.Id)) DisplayConfig.UseLocalSensor = true;
        SensorBackend = new SensorBackendState(DisplayConfig.UseLocalSensor);
        SensorBackend.Changed += RefreshLocalSensorStatus;
        LocalSession.StatusChanged += LocalSensorStatusChanged;
        DisplayConfig.PropertyChanged += LocalSensorPreferenceChanged;
        EditDisplayConfigCommand = new RelayCommand(_ =>
        {
            new PropertyEditorWindow(DisplayConfig)
            { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            ConfigHandler.GetInstance().Save<DisplayConfigManager>();
        });
        LocalSensorFlowExecution.Register();
    }

    private void LocalSensorPreferenceChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(DisplaySensorConfig.UseLocalSensor)) SensorBackend.SetPreference(DisplayConfig.UseLocalSensor);
    }
    private void LocalSensorStatusChanged(DeviceStatusType status) => SensorBackend.SetLocalStatus(status);
    private void RefreshLocalSensorStatus() => DService?.RefreshBackendStatus();

    protected override void OnConfigChanged()
    {
        base.OnConfigChanged();
        DisplayConfigManager.Instance.Configs[Config.Code] = DisplayConfig;
    }

    internal void EnsureOtherSensorBackends(bool local, string endpoint)
    {
        foreach (var other in ServiceManager.Current?.DeviceServices.OfType<DeviceSensor>() ?? Enumerable.Empty<DeviceSensor>())
        {
            string otherEndpoint = other.LocalSession.EndpointIdentity ?? LocalSensorSession.EndpointKey(other.Config);
            if (ReferenceEquals(other, this) || otherEndpoint != endpoint) continue;
            if (local)
            {
                if (other.SensorBackend.LocalOwned) throw new InvalidOperationException("另一个本地传感器正在使用此连接。");
                other.SensorBackend.EnsureLocalAvailable();
            }
            else other.SensorBackend.EnsureServiceAvailable();
        }
    }

    internal IReadOnlyList<LocalSensorCommand> ResolveLocalSensorCommands(object? parameters, ConfigSensor config)
    {
        JObject data = parameters == null ? new JObject() : JObject.FromObject(parameters);
        if (data["Cmd"] is JObject cmd && cmd.Value<int>("CmdType") != 0 && !string.IsNullOrEmpty(cmd.Value<string>("Request")))
            return [cmd.ToObject<LocalSensorCommand>()!];
        JObject template = data["TemplateParam"] as JObject ?? throw new ArgumentException("请选择传感器指令模板。");
        int id = template.Value<int?>("ID") ?? -1;
        return SensorTemplateRepository.ReadCommands(config.Category, id, template.Value<string>("Name"));
    }

    internal async Task<object?> ExecuteLocalSensorAsync(string operation, IReadOnlyList<LocalSensorCommand>? commands, LocalSensorConnectionConfig config, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(localDisposed, this);
        lock (SensorBackend.Sync)
        {
            if (operation != "Close") EnsureOtherSensorBackends(true, operation is "Open" or "Reopen"
                ? LocalSensorSession.EndpointKey(config) : LocalSession.EndpointIdentity ?? LocalSensorSession.EndpointKey(config));
            SensorBackend.BeginLocalCommand();
        }
        try
        {
            switch (operation)
            {
                case "Open": await LocalSession.OpenAsync(config, token).ConfigureAwait(false); return null;
                case "Close": await LocalSession.CloseAsync().ConfigureAwait(false); return null;
                case "Reopen": await LocalSession.ReopenAsync(config, token).ConfigureAwait(false); return null;
                case "ExecCmd": return await LocalSession.ExecuteAsync(commands ?? throw new ArgumentException("缺少传感器指令。"), token).ConfigureAwait(false);
                default: throw new NotSupportedException($"本地传感器不支持 {operation}。");
            }
        }
        finally { SensorBackend.EndLocalCommand(); }
    }

    internal LocalSensorConnectionConfig SnapshotLocalSensorConfig()
    {
        var snapshot = JsonConvert.DeserializeObject<LocalSensorConnectionConfig>(JsonConvert.SerializeObject(Config))!;
        snapshot.ConnectTimeout = DisplayConfig.ConnectTimeout;
        snapshot.DataBits = DisplayConfig.DataBits;
        snapshot.Parity = DisplayConfig.Parity;
        snapshot.StopBits = DisplayConfig.StopBits;
        snapshot.DtrEnable = DisplayConfig.DtrEnable;
        snapshot.RtsEnable = DisplayConfig.RtsEnable;
        return snapshot;
    }

    internal MsgRecord RunLocalSensorCommand(MsgSend message)
    {
        message.MsgID ??= Guid.NewGuid().ToString();
        message.DeviceCode ??= Code;
        var record = new MsgRecord { MsgID = message.MsgID, SendTime = DateTime.Now, MsgSend = message, MsgRecordState = MsgRecordState.Sended };
        LocalSensorConnectionConfig config = SnapshotLocalSensorConfig();
        var parameters = message.Params == null ? new JObject() : JObject.FromObject(message.Params);
        async Task Run()
        {
            object? result = null;
            Exception? failure = null;
            try
            {
                var commands = message.EventName == "ExecCmd"
                    ? await Task.Run(() => ResolveLocalSensorCommands(parameters, config)).ConfigureAwait(false) : null;
                result = await ExecuteLocalSensorAsync(message.EventName, commands, config, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex) { failure = ex; MQTTServiceBase.log.Error(ex); }
            void Finish()
            {
                record.ReciveTime = DateTime.Now;
                record.MsgReturn = new MsgReturn { MsgID = record.MsgID, EventName = message.EventName, DeviceCode = Code,
                    Code = failure == null ? 0 : -1, Message = failure?.Message ?? "ok", Data = result! };
                record.MsgRecordState = failure is TimeoutException ? MsgRecordState.Timeout : failure == null ? MsgRecordState.Success : MsgRecordState.Fail;
                PublishLocalSensorResult(result, failure);
                SetMsgRecordChanged(record);
            }
            if (Application.Current?.Dispatcher is { } dispatcher) await dispatcher.InvokeAsync(Finish);
            else Finish();
        }
        // Enter lifecycle operations before returning so a later Close cannot be overtaken by a queued Open.
        _ = Run();
        return record;
    }

    internal void PublishLocalSensorResult(object? result, Exception? failure = null)
    {
        void Update() => LastLocalResponse = failure?.Message ?? (result is IReadOnlyList<LocalSensorCommandResult> commands
            ? string.Join(Environment.NewLine, commands.Select(command => $"{command.Name}: {command.ResponseText} ({command.ElapsedMilliseconds} ms)")) : Properties.Resources.Success);
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
            LocalSession.StatusChanged -= LocalSensorStatusChanged;
            SensorBackend.Changed -= RefreshLocalSensorStatus;
            DisplayConfig.PropertyChanged -= LocalSensorPreferenceChanged;
            DService.Dispose();
            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
