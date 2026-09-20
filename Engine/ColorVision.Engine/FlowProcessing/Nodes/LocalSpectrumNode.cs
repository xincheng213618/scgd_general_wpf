using ColorVision.Common.MVVM;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Local;
using ColorVision.UI;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.ComponentModel;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Nodes;

[STNode("Flow_CustomNodes", "本地光谱采集")]
public sealed class LocalSpectrumNode : LocalDeviceFlowNodeBase
{
    private float integralTime = 100;
    private int average = 1;
    private bool autoIntegration, autoDark, adaptiveDark;
    private bool autoConnect = true;
    private bool eqe;
    private float voltage = 5, current = 1;
    private double factor = 1;
    [STNodeProperty("积分时间(ms)", "本次测量的积分时间", true)]
    public float IntegralTime { get => integralTime; set { integralTime = value; OnPropertyChanged(); } }
    [STNodeProperty("平均次数", "本次测量的平均次数", true)]
    public int NumberOfAverage { get => average; set { average = value; OnPropertyChanged(); } }
    [STNodeProperty("自动积分", "自动选择积分时间", true)]
    public bool AutoIntegration { get => autoIntegration; set { autoIntegration = value; OnPropertyChanged(); } }
    [STNodeProperty("自动校零", "测量前进行暗采集，请确保光路已遮光", true)]
    public bool AutoInitDark { get => autoDark; set { autoDark = value; OnPropertyChanged(); } }
    [STNodeProperty("自适应暗校正", "使用已初始化的自适应暗数据", true)]
    public bool SelfAdaptionInitDark { get => adaptiveDark; set { adaptiveDark = value; OnPropertyChanged(); } }
    [STNodeProperty("自动连接", "未打开时按当前设备配置连接本机光谱仪", true)]
    public bool AutoConnect { get => autoConnect; set { autoConnect = value; OnPropertyChanged(); } }
    [STNodeProperty("光通量/EQE", "使用设置的电压、电流和修正系数测量 EQE", true)]
    public bool Eqe { get => eqe; set { eqe = value; OnPropertyChanged(); } }
    [STNodeProperty("修正系数", "EQE 修正系数，必须大于 0", true)]
    public double AFactor { get => factor; set { factor = value; OnPropertyChanged(); } }
    [STNodeProperty("电压(V)", "EQE 的电压输入", true)]
    public float Voltage { get => voltage; set { voltage = value; OnPropertyChanged(); } }
    [STNodeProperty("电流(A)", "EQE 的电流输入，必须大于 0", true)]
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
            device.DisplayConfig.IsWithND = false;
            device.OpenLocalSpectrumWindow(this);
        });
    }
    private DeviceSpectrum FindDevice() => ServiceManager.Current?.DeviceServices.OfType<DeviceSpectrum>().FirstOrDefault(d => d.Code == DeviceCode)
        ?? throw new InvalidOperationException($"找不到光谱仪设备：{DeviceCode}");
    protected override string GetCompactSummaryValue() => $"{IntegralTime:0.###} ms × {NumberOfAverage}";
    protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
    {
        using var execution = new LocalSpectrumFlowExecution(FindDevice(), new LocalSpectrumParameters
        {
            IntegralTime = IntegralTime, NumberOfAverage = NumberOfAverage, AutoIntegration = AutoIntegration,
            AutoInitDark = AutoInitDark, SelfAdaptionInitDark = SelfAdaptionInitDark,
            Eqe = Eqe, AFactor = AFactor, Voltage = Voltage, Current = Current
        }, Eqe ? "EQE.GetData" : "GetData", AutoConnect, ZIndex, NodeID);
        execution.Execute();
        return new LocalNodeExecutionResult { Data = execution.Complete(action) };
    }
    protected override string BuildRunPayload(CVStartCFC action) => JsonConvert.SerializeObject(new { DeviceCode, IntegralTime, NumberOfAverage, AutoIntegration, AutoInitDark, SelfAdaptionInitDark, AutoConnect, Eqe, AFactor, Voltage, Current });
}
