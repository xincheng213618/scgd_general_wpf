using ColorVision.Common.MVVM;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Local;
using FlowEngineLib.Base;
using FlowEngineLib.Repeat;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Nodes;

[STNode("Flow_LocalDevices", "本地光谱采集", CategoryOrder = 9900, NodeOrder = 80)]
public sealed class LocalSpectrumNode : LocalDeviceFlowNodeBase, IRepeatParameterNode
{
    // Native acquisition converts float milliseconds to signed integer microseconds.
    private const double MaximumIntegralTime = 2_147_483.5;
    private sealed record ExecutionParameters(DeviceSpectrum Device, string DeviceCode, LocalSpectrumParameters Capture, bool AutoConnect, int ZIndex, string NodeId);
    private float integralTime = 100;
    private int average = 1;
    private bool autoIntegration, autoDark, adaptiveDark;
    private bool isWithNd;
    private bool autoConnect = true;
    private bool eqe;
    private bool useUpstreamVI = true;
    private float voltage = 5, current = 1;
    private double factor = 1;
    [STNodeProperty("积分时间(ms)", "本次测量的积分时间", true)]
    public float IntegralTime { get => integralTime; set { integralTime = value; OnPropertyChanged(); } }
    [STNodeProperty("平均次数", "本次测量的平均次数", true)]
    public int NumberOfAverage { get => average; set { average = value; OnPropertyChanged(); } }
    [STNodeProperty("自动积分", "自动选择积分时间", true)]
    public bool AutoIntegration { get => autoIntegration; set { autoIntegration = value; OnPropertyChanged(); } }
    [STNodeProperty("使用 ND", "按实际 ND 孔位选择标定；自动积分时联动 ND", true)]
    public bool IsWithND { get => isWithNd; set { isWithNd = value; OnPropertyChanged(); } }
    [STNodeProperty("自动校零", "测量前进行暗采集，请确保光路已遮光", true)]
    public bool AutoInitDark { get => autoDark; set { autoDark = value; OnPropertyChanged(); } }
    [STNodeProperty("自适应暗校正", "使用已初始化的自适应暗数据", true)]
    public bool SelfAdaptionInitDark { get => adaptiveDark; set { adaptiveDark = value; OnPropertyChanged(); } }
    [STNodeProperty("自动连接", "未打开时按当前设备配置连接本机光谱仪", true)]
    public bool AutoConnect { get => autoConnect; set { autoConnect = value; OnPropertyChanged(); } }
    [STNodeProperty("光通量/EQE", "默认使用本次流程的上游源表电压、电流测量 EQE；也可关闭上游输入后手动填写。", true)]
    public bool Eqe { get => eqe; set { eqe = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowManualVI)); } }
    [STNodeProperty("使用上游源表电压/流", "默认开启；EQE 必须取得本次流程上游 SMU 的实际测量结果，否则报错。关闭后手动填写 V/A。", true)]
    [PropertyVisibility(nameof(Eqe))]
    public bool UseUpstreamVI { get => useUpstreamVI; set { useUpstreamVI = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowManualVI)); } }
    [Browsable(false), JsonIgnore]
    public bool ShowManualVI => Eqe && !UseUpstreamVI;
    [STNodeProperty("修正系数", "EQE 修正系数，必须大于 0", true)]
    public double AFactor { get => factor; set { factor = value; OnPropertyChanged(); } }
    [STNodeProperty("电压(V)", "EQE 的电压输入", true)]
    [PropertyVisibility(nameof(ShowManualVI))]
    public float Voltage { get => voltage; set { voltage = value; OnPropertyChanged(); } }
    [STNodeProperty("电流(A)", "EQE 的电流输入，必须大于 0", true)]
    [PropertyVisibility(nameof(ShowManualVI))]
    public float Current { get => current; set { current = value; OnPropertyChanged(); } }
    [JsonIgnore, CommandDisplay("本地光谱仪管理", Order = -100), Description("打开当前光谱仪的本地窗口，使用此节点的采集参数")]
    public RelayCommand OpenLocalSpectrumManagerCommand { get; }

    public LocalSpectrumNode() : base("本地光谱采集", "Spectrum", "GetData")
    {
        SelectFirstAvailableDevice<DeviceSpectrum>();
        OpenLocalSpectrumManagerCommand = new RelayCommand(_ =>
        {
            DeviceSpectrum device = FindDevice();
            device.DisplayConfig.IntTime = IntegralTime;
            device.DisplayConfig.AveNum = NumberOfAverage;
            device.DisplayConfig.IsAutoIntTime = AutoIntegration;
            device.DisplayConfig.IsAutoDark = AutoInitDark;
            device.DisplayConfig.IsShutter = SelfAdaptionInitDark;
            device.DisplayConfig.IsLuminousFluxMode = Eqe;
            device.DisplayConfig.Divisor = AFactor;
            device.DisplayConfig.V = Voltage;
            device.DisplayConfig.I = Current;
            device.DisplayConfig.IsWithND = IsWithND;
            device.OpenLocalSpectrumWindow(this);
        });
    }
    private DeviceSpectrum FindDevice() => ServiceManager.Current?.DeviceServices.OfType<DeviceSpectrum>().FirstOrDefault(d => d.Code == DeviceCode)
        ?? throw new InvalidOperationException($"找不到光谱仪设备：{DeviceCode}");

    public IReadOnlyList<RepeatParameterDefinition> GetRepeatParameters()
    {
        bool syncFrequency = ServiceManager.Current?.DeviceServices.OfType<DeviceSpectrum>()
            .FirstOrDefault(device => device.Code == DeviceCode)?.Config.GetDataConfig.IsSyncFrequencyEnabled == true;
        string? syncReason = syncFrequency ? "同步频率模式会调整实际积分时间和平均次数，请先在光谱仪设置中关闭同步频率。" : null;
        return
        [
            new(nameof(IntegralTime), "积分时间", "ms", IntegralTime, minimum: 0.001, maximum: MaximumIntegralTime,
                unavailableReason: syncReason ?? (AutoIntegration ? "自动积分已启用，请先关闭自动积分后设置逐轮积分时间。" : null)),
            new(nameof(NumberOfAverage), "平均次数", "次", NumberOfAverage, isInteger: true, minimum: 1, maximum: int.MaxValue, unavailableReason: syncReason)
        ];
    }

    protected override string GetCompactSummaryValue() => $"{IntegralTime:0.###} ms × {NumberOfAverage}";

    protected override object CaptureExecutionParameters(CVStartCFC action)
    {
        double roundIntegralTime = ResolveRepeatParameter(action, nameof(IntegralTime), double.NaN);
        double roundAverage = ResolveRepeatParameter(action, nameof(NumberOfAverage), double.NaN);
        foreach (var definition in GetRepeatParameters())
        {
            double value = definition.Key == nameof(IntegralTime) ? roundIntegralTime : roundAverage;
            if (!double.IsNaN(value) && !definition.TryValidate(value, out string error))
                throw new InvalidOperationException(error);
        }
        var parameters = new LocalSpectrumParameters
        {
            IntegralTime = double.IsNaN(roundIntegralTime) ? IntegralTime : (float)roundIntegralTime,
            NumberOfAverage = double.IsNaN(roundAverage) ? NumberOfAverage : checked((int)roundAverage), AutoIntegration = AutoIntegration,
            AutoInitDark = AutoInitDark, SelfAdaptionInitDark = SelfAdaptionInitDark,
            IsWithND = IsWithND,
            Eqe = Eqe, AFactor = AFactor, Voltage = Voltage, Current = Current
        };
        if (parameters.Eqe && UseUpstreamVI)
        {
            action.Data.TryGetValue("SMUResult", out object? smu);
            parameters.ApplySmuData(smu);
        }
        parameters.Validate();
        if (parameters.IntegralTime > MaximumIntegralTime)
            throw new InvalidOperationException($"积分时间不能超过 {MaximumIntegralTime:0.###} ms。");
        return new ExecutionParameters(FindDevice(), DeviceCode, parameters, AutoConnect, ZIndex, NodeID);
    }

    protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
    {
        var snapshot = GetExecutionParameters<ExecutionParameters>(action);
        using var execution = new LocalSpectrumFlowExecution(snapshot.Device, snapshot.Capture,
            snapshot.Capture.Eqe ? "EQE.GetData" : "GetData", snapshot.AutoConnect, snapshot.ZIndex, snapshot.NodeId);
        execution.Bind(action);
        execution.Execute();
        return new LocalNodeExecutionResult { Data = execution.Complete(action) };
    }

    protected override string BuildRunPayload(CVStartCFC action)
    {
        var snapshot = GetExecutionParameters<ExecutionParameters>(action);
        var parameters = snapshot.Capture;
        return JsonConvert.SerializeObject(new
        {
            snapshot.DeviceCode, parameters.IntegralTime, parameters.NumberOfAverage, parameters.AutoIntegration,
            parameters.AutoInitDark, parameters.SelfAdaptionInitDark, snapshot.AutoConnect,
            parameters.IsWithND,
            parameters.Eqe, parameters.AFactor, parameters.Voltage, parameters.Current, parameters.SMUData
        });
    }
}
