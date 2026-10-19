using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.SMU;
using ColorVision.Engine.Services.Devices.SMU.Local;
using ColorVision.Engine.Services.Results;
using ColorVision.Themes.Controls;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.Repeat;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using SmuChannel = ColorVision.Engine.Services.Devices.SMU.Dao.SMUChannelType;
using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.Windows;
using ColorVision.Themes;
using System.Text;

namespace ColorVision.Engine.FlowProcessing.Nodes;

[STNode("Flow_LocalDevices", "源表", CategoryOrder = 9900, NodeOrder = 20)]
public sealed class LocalSmuNode : LocalDeviceFlowNodeBase, IRepeatParameterNode
{
    private SourceType source = SourceType.Voltage_V;
    private SMUChannelType channel;
    private double measureValue = 1, limitValue = 10, srcRng, lmtRng, measureDelayMs = 100;
    private bool autoConnect = true, isAutoRng = true, closeOutputOnFinish = true, useDeviceDelay = true;
    private LocalSmuSettings? localSettings;
    [Browsable(false)]
    public LocalSmuSettings? LocalSettings
    {
        get => localSettings;
        set { localSettings = value; OnPropertyChanged(nameof(UseWindowSettings)); OnPropertyChanged(nameof(ShowMeasureDelay)); }
    }
    [Category("本地源表"), STNodeProperty("使用窗口设置", "启用窗口中的测量、扫描和脉冲参数；关闭后恢复普通点测与重复框自动合批", true)]
    public bool UseWindowSettings
    {
        get => LocalSettings != null;
        set => LocalSettings = value ? LocalSettings ?? new LocalSmuSettings { Channel = (SmuChannel)(int)Channel,
            IsSourceV = Source == SourceType.Voltage_V, SourceValue = MeasureValue, Limit = LimitValue,
            SourceRange = IsAutoRng ? 0 : SrcRng, MeasureRange = IsAutoRng ? 0 : LmtRng } : null;
    }
    [JsonIgnore, CommandDisplay("本地源表", Order = -100), Description("打开本地源表窗口，编辑测量、扫描、脉冲和接线参数并应用到此节点。")]
    public RelayCommand OpenLocalSmuManagerCommand { get; }
    protected override void OnSaveNode(Dictionary<string, byte[]> data)
    {
        base.OnSaveNode(data);
        if (LocalSettings != null) data["LocalSmuSettings.v1"] = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(LocalSettings));
    }
    public override void OnLoadNode(Dictionary<string, byte[]> data)
    {
        base.OnLoadNode(data);
        LocalSettings = data.TryGetValue("LocalSmuSettings.v1", out var bytes)
            ? JsonConvert.DeserializeObject<LocalSmuSettings>(Encoding.UTF8.GetString(bytes)) ?? throw new InvalidOperationException("本地源表参数为空。") : null;
    }

    [Category("本地源表"), STNodeProperty("电(压/流)源", "源值使用 V 或 mA，保护限值使用 mA 或 V；源类型在重复执行期间固定", true)]
    public SourceType Source { get => source; set { source = value; OnPropertyChanged(); } }

    [Category("本地源表"), STNodeProperty("通道", "本次流程使用的源表通道，重复执行期间固定", true)]
    public SMUChannelType Channel { get => channel; set { channel = value; OnPropertyChanged(); } }

    [Category("本地源表"), STNodeProperty("设定电压 (V)", "电压源设置电压（V），电流源设置电流（mA）；支持重复框逐轮设置", true)]
    [PropertyEditorType(typeof(LocalSmuValuePropertiesEditor))]
    public double MeasureValue { get => measureValue; set { measureValue = value; OnPropertyChanged(); } }

    [Category("本地源表"), STNodeProperty("电流上限 (mA)", "电压源设置电流上限（mA），电流源设置电压上限（V）；支持重复框逐轮设置", true)]
    [PropertyEditorType(typeof(LocalSmuValuePropertiesEditor))]
    public double LimitValue { get => limitValue; set { limitValue = value; OnPropertyChanged(); } }

    [Category("本地源表"), STNodeProperty("自动量程", "按当前设备能力自动选择量程", true)]
    [PropertyVisibility(nameof(UseWindowSettings), true)]
    public bool IsAutoRng { get => isAutoRng; set { isAutoRng = value; OnPropertyChanged(); } }

    [Category("本地源表"), STNodeProperty("源量程", "手动量程沿用设备 SDK 的量程单位与支持范围", true)]
    [PropertyEditorType(typeof(SmuRangePropertiesEditor))]
    [PropertyVisibility(nameof(UseWindowSettings), true)]
    public double SrcRng { get => srcRng; set { srcRng = value; OnPropertyChanged(); } }

    [Category("本地源表"), STNodeProperty("限量程", "手动量程沿用设备 SDK 的量程单位与支持范围", true)]
    [PropertyEditorType(typeof(SmuRangePropertiesEditor))]
    [PropertyVisibility(nameof(UseWindowSettings), true)]
    public double LmtRng { get => lmtRng; set { lmtRng = value; OnPropertyChanged(); } }

    [Category("本地源表"), STNodeProperty("沿用设备延时", "使用设备配置中的测量延时；关闭后可单独设置此节点的等待时间", true)]
    [PropertyVisibility(nameof(UseWindowSettings), true)]
    public bool UseDeviceDelay
    {
        get => useDeviceDelay;
        set { useDeviceDelay = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowMeasureDelay)); }
    }

    [Browsable(false)]
    public bool ShowMeasureDelay => !UseWindowSettings && !UseDeviceDelay;

    [Category("本地源表"), STNodeProperty("测量延时 (ms)", "输出设置后、源表采样前的等待时间。0 表示不额外等待；在这里等待灯响应后，下游无需再添加同用途的延时。扫描时每点均使用此延时", true)]
    [PropertyVisibility(nameof(ShowMeasureDelay))]
    public double MeasureDelayMs { get => measureDelayMs; set { measureDelayMs = value; OnPropertyChanged(); } }

    [Category("本地源表"), STNodeProperty("自动连接", "源表未连接时，按所选设备的本地连接配置打开", true)]
    public bool AutoConnect { get => autoConnect; set { autoConnect = value; OnPropertyChanged(); } }

    [Category("本地源表"), STNodeProperty("结束关闭输出", "设置后保持输出供相机或光谱仪测量，流程结束时关闭；失败或取消始终尝试关闭", true)]
    public bool CloseOutputOnFinish { get => closeOutputOnFinish; set { closeOutputOnFinish = value; OnPropertyChanged(); } }

    public LocalSmuNode() : base("源表", "SMU", "StepData")
    {
        SelectFirstAvailableDevice<DeviceSMU>();
        OpenLocalSmuManagerCommand = new RelayCommand(_ =>
        {
            DeviceSMU? device = ServiceManager.GetInstance().DeviceServices.OfType<DeviceSMU>().FirstOrDefault(item => item.Code == DeviceCode);
            if (device == null) { MessageBox1.Show(Application.Current.GetActiveWindow(), $"找不到本地源表设备：{DeviceCode}", "ColorVision"); return; }
            device.OpenLocalSmuWindow(this);
        });
    }

    public IReadOnlyList<RepeatParameterDefinition> GetRepeatParameters() =>
    [
        new(nameof(MeasureValue), Source == SourceType.Voltage_V ? "设定电压" : "设定电流", Source == SourceType.Voltage_V ? "V" : "mA", MeasureValue),
        new(nameof(LimitValue), Source == SourceType.Voltage_V ? "电流上限" : "电压上限", Source == SourceType.Voltage_V ? "mA" : "V", LimitValue,
            validateValue: value => value == 0 ? "源表保护限值必须是非零有限值。" : string.Empty)
    ];

    protected override string GetCompactSummaryValue() => $"{Channel}: {MeasureValue:0.###} {(Source == SourceType.Voltage_V ? "V" : "mA")}";

    private sealed record ExecutionParameters(DeviceSMU Device, string DeviceCode, LocalSmuConnection Connection, LocalSmuParameters Parameters,
        LocalSmuParameters? ScanParameters, bool AutoConnect, bool CheckLimits, bool CloseOutputOnFinish, int ZIndex, string NodeId);

    protected override object CaptureExecutionParameters(CVStartCFC action)
    {
        DeviceSMU device = ServiceManager.GetInstance().DeviceServices.OfType<DeviceSMU>()
            .FirstOrDefault(item => string.Equals(item.Code, DeviceCode, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"找不到本地源表设备：{DeviceCode}");
        if (!Enum.IsDefined(Source)) throw new InvalidOperationException("源表源类型无效。");
        var connection = LocalSmuConnection.From(device.Config, (int)device.DisplayConfig.LocalBaudRate);
        var parameters = new LocalSmuParameters
        {
            Channel = (SmuChannel)(int)Channel, IsSourceV = Source == SourceType.Voltage_V,
            MeasureValue = ResolveRepeatParameter(action, nameof(MeasureValue), MeasureValue),
            LimitValue = ResolveRepeatParameter(action, nameof(LimitValue), LimitValue),
            IsAutoRng = IsAutoRng, SrcRng = SrcRng, LmtRng = LmtRng,
            MeasureDelayMs = !UseWindowSettings && !UseDeviceDelay ? MeasureDelayMs : null
        };
        connection.Validate();
        if (LocalSettings != null)
        {
            var settings = LocalSettings.Clone();
            settings.Channel = parameters.Channel; settings.IsSourceV = parameters.IsSourceV;
            settings.SourceValue = parameters.MeasureValue; settings.Limit = parameters.LimitValue;
            // Output ownership and normal finish are managed by the entire flow.
            settings.CloseOutput = false;
            settings.Validate(connection.DeviceType, device.DisplayConfig.IsUseLimitSigned);
            if (TryGetSingleNodeRepeatParameters(action, out int repeatCount, out var repeatValues))
            {
                repeatValues.TryGetValue(nameof(MeasureValue), out double[]? repeatSources);
                repeatValues.TryGetValue(nameof(LimitValue), out double[]? repeatLimits);
                double scanMaximum = settings.SourceValues().Max(value => Math.Abs(value));
                for (int index = 0; index < repeatCount; index++)
                    (parameters with { MeasureValue = settings.Sweep == SmuSweep.Fixed ? repeatSources?[index] ?? settings.SourceValue : scanMaximum,
                        LimitValue = repeatLimits?[index] ?? settings.Limit }).Validate(connection.DeviceType, false, device.DisplayConfig.IsUseLimitSigned);
            }
            return new ExecutionParameters(device, DeviceCode, connection, parameters with { AdvancedSettings = settings }, null, AutoConnect,
                device.DisplayConfig.IsUseLimitSigned, CloseOutputOnFinish, ZIndex, NodeID);
        }
        parameters.Validate(connection.DeviceType, false, device.DisplayConfig.IsUseLimitSigned);
        LocalSmuParameters? scanParameters = null;
        if (TryGetSingleNodeRepeatParameters(action, out int count, out var values) && count <= 100000)
        {
            double[] sources = values.TryGetValue(nameof(MeasureValue), out double[]? sequence)
                ? sequence.ToArray() : Enumerable.Repeat(parameters.MeasureValue, count).ToArray();
            values.TryGetValue(nameof(LimitValue), out double[]? limits);
            // Validate every row before opening/output, including a later invalid row in point fallback.
            for (int index = 0; index < count; index++)
                (parameters with { MeasureValue = sources[index], LimitValue = limits?[index] ?? parameters.LimitValue })
                    .Validate(connection.DeviceType, false, device.DisplayConfig.IsUseLimitSigned);
            if (limits == null || limits.All(value => value == parameters.LimitValue))
            {
                scanParameters = parameters with { BeginValue = sources[0], EndValue = sources[^1], Points = count,
                    SourceValues = sources, IsListSweep = !IsIncreasingLinear(sources) };
                scanParameters.Validate(connection.DeviceType, true, device.DisplayConfig.IsUseLimitSigned);
            }
        }
        return new ExecutionParameters(device, DeviceCode, connection, parameters, scanParameters, AutoConnect,
            device.DisplayConfig.IsUseLimitSigned, CloseOutputOnFinish, ZIndex, NodeID);
    }

    private static bool IsIncreasingLinear(double[] values)
    {
        double step = (values[^1] - values[0]) / (values.Length - 1);
        if (!(step > 0) || !double.IsFinite(step)) return false;
        const double roundingTolerance = 8 * 2.2204460492503131e-16;
        double scale = Math.Max(Math.Abs(values[0]), Math.Abs(values[^1]));
        for (int index = 1; index < values.Length - 1; index++)
            if (Math.Abs(values[index] - (values[0] + step * index)) > scale * roundingTolerance) return false;
        return true;
    }

    protected override string BuildRunPayload(CVStartCFC action)
    {
        var run = GetExecutionParameters<ExecutionParameters>(action);
        return JsonConvert.SerializeObject(new { ServiceName = NodeName, run.DeviceCode, EventName = OperatorCode,
            action.SerialNumber, run.Parameters, run.ScanParameters, RequestedExecutionMode = "Auto",
            run.AutoConnect, run.CloseOutputOnFinish });
    }

    protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
    {
        var run = GetExecutionParameters<ExecutionParameters>(action);
        var owner = action.RuntimeResources;
        string key = "LocalSmuFlow:" + run.DeviceCode;
        if (!owner.TryGet<LocalSmuFlowLease>(key, out var lease))
        {
            lease = run.Device.LocalSession.AcquireFlowLeaseAsync(owner, () => action.FinishedStatus != StatusTypeEnum.Completed, owner.StopToken).GetAwaiter().GetResult();
            owner.Set(key, lease);
        }
        lease.ConfigureChannel(run.Parameters.Channel, run.CloseOutputOnFinish);
        if (run.ScanParameters != null)
        {
            if (run.AutoConnect && !run.Device.LocalSession.IsOpen)
                run.Device.ExecuteLocalSmuAsync("Open", run.Connection, run.Parameters, true, run.CheckLimits, owner.StopToken, owner).GetAwaiter().GetResult();
            int required = run.ScanParameters.IsListSweep ? 2 : 1;
            if ((run.Device.LocalSession.SweepCapabilities & required) != 0)
                return ExecuteScan(action, run);
        }
        LocalSmuCapture capture = run.Device.ExecuteLocalSmuAsync(run.Parameters.AdvancedSettings == null ? "StepData" : "Advanced", run.Connection, run.Parameters, run.AutoConnect,
            run.CheckLimits, owner.StopToken, owner).GetAwaiter().GetResult()!;
        owner.StopToken.ThrowIfCancellationRequested();
        action.Data.Remove("LocalSmuScan");
        action.Data.Remove("SMUScanResult");
        if (capture.Details != null)
        {
            int advancedId;
            if (capture.IsScan)
            {
                advancedId = LocalSmuResultService.Save(capture, run.DeviceCode, action, run.ZIndex);
                var point = capture with { Voltages = [capture.V], Currents = [capture.I], IsScan = false, TotalTime = 0,
                    Details = capture.Details with { SourceValues = [capture.Details.SourceValues[^1]],
                        Resistance = [capture.Details.Resistance[^1]], Status = [capture.Details.Status[^1]] } };
                SavePoint(action, run, point, publishView: false);
            }
            else if (capture.HasMeasurements) advancedId = SavePoint(action, run, capture, publishView: false);
            else
            {
                advancedId = 0; action.MasterValue(null, 0, LocalSmuResultService.ResultType);
                action.Data["LocalSmu"] = capture; action.Data.Remove("SMUResult");
            }
            if (capture.IsScan) { action.Data["LocalSmuScan"] = capture; action.Data["SMUScanResult"] = LocalSmuResultService.Response(capture, advancedId); }
            run.Device.PublishLocalSmu(capture, advancedId);
            if (capture.IsScan && advancedId > 0) ResultMessageBus.Default.PublishPersisted(ResultRoutes.Smu, ResultKinds.Smu, run.DeviceCode, "Scan",
                action.SerialNumber, run.NodeId, run.ZIndex, advancedId, LocalSmuResultService.ResultType);
            var advancedResponse = LocalSmuResultService.Response(capture, advancedId);
            advancedResponse["ExecutionMode"] = capture.Details.TimingMode;
            return new LocalNodeExecutionResult { Data = advancedResponse };
        }
        int id = SavePoint(action, run, capture, publishView: true);
        var response = LocalSmuResultService.Response(capture, id);
        response["ExecutionMode"] = "Point";
        return new LocalNodeExecutionResult { Data = response };
    }

    private LocalNodeExecutionResult ExecuteScan(CVStartCFC action, ExecutionParameters run)
    {
        var owner = action.RuntimeResources;
        var scan = run.ScanParameters!;
        // Once a scan is started, errors must propagate; retrying point-by-point would energize the sample again.
        LocalSmuCapture capture = run.Device.ExecuteLocalSmuAsync("Scan", run.Connection, scan, run.AutoConnect,
            run.CheckLimits, owner.StopToken, owner).GetAwaiter().GetResult()!;
        owner.StopToken.ThrowIfCancellationRequested();
        int scanId = LocalSmuResultService.Save(capture, run.DeviceCode, action, run.ZIndex);
        for (int index = 0; index < scan.Points; index++)
        {
            owner.StopToken.ThrowIfCancellationRequested();
            var point = new LocalSmuCapture(run.Parameters with { MeasureValue = scan.SourceValues![index] },
                [capture.Voltages[index]], [capture.Currents[index]], 0, false) { Generation = capture.Generation, CapturedAt = capture.CapturedAt };
            SavePoint(action, run, point, publishView: false);
        }
        action.Data["LocalSmuScan"] = capture;
        var response = LocalSmuResultService.Response(capture, scanId);
        response["ExecutionMode"] = scan.IsListSweep ? "ListScan" : "LinearScan";
        action.Data["SMUScanResult"] = response;
        run.Device.PublishLocalSmu(capture, scanId);
        if (scanId > 0) ResultMessageBus.Default.PublishPersisted(ResultRoutes.Smu, ResultKinds.Smu, run.DeviceCode, "Scan",
            action.SerialNumber, run.NodeId, run.ZIndex, scanId, LocalSmuResultService.ResultType);
        owner.StopToken.ThrowIfCancellationRequested();
        CompleteRepeatBatch(action, scan.Points);
        return new LocalNodeExecutionResult { Data = response };
    }

    private static int SavePoint(CVStartCFC action, ExecutionParameters run, LocalSmuCapture capture, bool publishView)
    {
        int id = LocalSmuResultService.Save(capture, run.DeviceCode, action, run.ZIndex);
        action.MasterValue(null, id, LocalSmuResultService.ResultType);
        action.Data["LocalSmu"] = capture;
        action.Data["SMUResult"] = new SMUResultData((SMUChannelType)(int)capture.Parameters.Channel,
            capture.V, capture.I, id, LocalSmuResultService.ResultType);
        if (publishView) run.Device.PublishLocalSmu(capture, id);
        if (id > 0) ResultMessageBus.Default.PublishPersisted(ResultRoutes.Smu, ResultKinds.Smu, run.DeviceCode, "StepData",
            action.SerialNumber, run.NodeId, run.ZIndex, id, LocalSmuResultService.ResultType);
        return id;
    }
}
