using ColorVision.Engine.Services.Devices.SMU.Dao;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;

namespace ColorVision.Engine.Services.Devices.SMU.Local;

public enum SmuMeasurement { [Description("电压 / 电流")] VI, [Description("电阻（Ω）")] Resistance, [Description("功率（W）")] Power }
public enum SmuOperation { [Description("输出并测量")] SourceMeasure, [Description("仅输出")] SourceOnly, [Description("仅测量（0 V / 0 A 接法）")] MeasureOnly }
public enum SmuSweep { [Description("固定值")] Fixed, [Description("线性扫描")] Linear, [Description("对数扫描")] Logarithmic, [Description("列表扫描")] List, [Description("源记忆扫描（2400）")] SourceMemory }
public enum SmuWaveform { [Description("直流")] DC, [Description("脉冲")] Pulse }
public enum SmuOhmsMode { [Description("手动激励")] Manual, [Description("自动电阻（2400）")] Auto }
public enum SmuOffMode { [Description("正常")] Normal, [Description("高阻断开")] HighImpedance, [Description("零输出")] Zero, [Description("Guard（2400）")] Guard }
public enum SmuFilter { [Description("关闭")] Off, [Description("重复平均")] Repeat, [Description("移动平均")] Moving }
public enum SmuAutoZero { [Description("关闭")] Off, [Description("一次")] Once, [Description("连续")] Auto }
public enum SmuTrigger { [Description("立即执行")] Immediate, [Description("外部下降沿")] External }

// These settings belong to the local backend. Legacy IsSourceV payloads keep their original meaning.
public sealed class LocalSmuSettings
{
    [Category("输出与测量"), DisplayName("通道")]
    public SMUChannelType Channel { get; set; }
    [Category("输出与测量"), DisplayName("电压源"), Description("开启：源值 V、保护限值 mA；关闭：源值 mA、保护限值 V。")]
    public bool IsSourceV { get; set; } = true;
    [Category("输出与测量"), DisplayName("工作方式"), Description("SourceMeasure：输出并测量；SourceOnly：仅输出；MeasureOnly：电压表/电流表接法，使用 0 A/0 V 源，仍会接通仪器输出。")]
    public SmuOperation Operation { get; set; }
    [Category("输出与测量"), DisplayName("测量量"), Description("Resistance 为直流电阻 Ω，Power 为功率 W；所有有测量的操作同时保留实测 V/I。")]
    public SmuMeasurement Measurement { get; set; }
    [Category("输出与测量"), DisplayName("源值（V / mA）")]
    public double SourceValue { get; set; } = 1;
    [Category("输出与测量"), DisplayName("保护限值（mA / V）")]
    public double Limit { get; set; } = 10;
    [Category("输出与测量"), DisplayName("完成后关闭输出")]
    public bool CloseOutput { get; set; } = true;
    [Category("扫描"), DisplayName("扫描方式"), Description("SourceMemory：2400 源记忆扫描，使用列表值与可选的逐点保护限值；覆盖本次使用的仪器源记忆槽。")]
    public SmuSweep Sweep { get; set; }
    [Category("扫描"), DisplayName("起点（V / mA）")]
    public double Start { get; set; } = 1;
    [Category("扫描"), DisplayName("终点（V / mA）")]
    public double Stop { get; set; } = 5;
    [Category("扫描"), DisplayName("点数 / 固定值脉冲次数")]
    public int Points { get; set; } = 5;
    [Category("扫描"), DisplayName("列表值（V / mA）"), Description("使用英文逗号、分号或空白分隔；小数点使用英文句点。保留重复、递减和跳变。")]
    public string Values { get; set; } = "1,2,3,4,5";
    [Category("扫描"), DisplayName("源记忆逐点保护限值"), Description("留空时共用保护限值，否则数量必须与列表相同；单位沿用保护限值。")]
    public string MemoryLimits { get; set; } = string.Empty;
    [Category("脉冲"), DisplayName("波形"), Description("2600 A/B 使用仪器定时器。普通 2400 使用 SDM 间接脉冲，实际脉宽需示波器校准；2430 使用专用脉冲模式。")]
    public SmuWaveform Waveform { get; set; }
    [Category("脉冲"), DisplayName("脉宽（ms）")]
    public double PulseWidth { get; set; } = 20;
    [Category("脉冲"), DisplayName("周期（ms）")]
    public double PulsePeriod { get; set; } = 100;
    [Category("脉冲"), DisplayName("2600 采样延时（ms）"), Description("仅用于 2600 A/B 脉冲。2400 的采样位置由 SDM 源延时和积分时间决定；2430 仅输出时不采样。")]
    public double PulseMeasureDelay { get; set; } = 1;
    [Category("脉冲"), DisplayName("基线（V / mA）"), Description("2600 脉冲间返回此值；2400/2430 脉冲只支持零基线。")]
    public double Bias { get; set; }
    [Category("电阻"), DisplayName("电阻测量方式"), Description("2400 Auto 根据电阻量程选择测试电流；2600 使用 Manual，设置测试电压或电流。")]
    public SmuOhmsMode OhmsMode { get; set; }
    [Category("电阻"), DisplayName("电阻量程（Ω）"), Description("2400 自动电阻模式所用量程；0 表示自动量程。")]
    public double OhmsRange { get; set; }
    [Category("电阻"), DisplayName("偏置补偿"), Description("2400 电阻测量的仪器偏置补偿。")]
    public bool OffsetCompensation { get; set; }
    [Category("接线与关闭状态"), DisplayName("四线制")]
    public bool FourWire { get; set; }
    [Category("接线与关闭状态"), DisplayName("前面板端子"), Description("2400 的前/后面板端子选择。")]
    public bool Front { get; set; }
    [Category("接线与关闭状态"), DisplayName("Ohms Guard"), Description("2400 Guard，配合四线接线进行六线保护电阻测量；需要相应实物接线。")]
    public bool OhmsGuard { get; set; }
    [Category("接线与关闭状态"), DisplayName("输出关闭状态"), Description("HighImpedance 为高阻断开；Zero 为零输出。Guard 关闭状态仅适用于 2400。")]
    public SmuOffMode OffMode { get; set; }
    [Category("量程与采样"), DisplayName("源量程（V / A，0 自动）")]
    public double SourceRange { get; set; }
    [Category("量程与采样"), DisplayName("测量量程（A / V，0 自动）")]
    public double MeasureRange { get; set; }
    [Category("量程与采样"), DisplayName("积分时间（NPLC）")]
    public double Nplc { get; set; } = 0.01;
    [Category("量程与采样"), DisplayName("稳定延时（ms）")]
    public double Delay { get; set; } = 1;
    [Category("量程与采样"), DisplayName("滤波")]
    public SmuFilter Filter { get; set; }
    [Category("量程与采样"), DisplayName("滤波次数")]
    public int FilterCount { get; set; } = 1;
    [Category("量程与采样"), DisplayName("自动调零")]
    public SmuAutoZero AutoZero { get; set; } = SmuAutoZero.Once;
    [Category("量程与采样"), DisplayName("高电容模式"), Description("2600B 高电容负载稳定模式，会增加稳定时间；此项不测量电容量。")]
    public bool HighCapacitance { get; set; }
    [Category("量程与采样"), DisplayName("整次操作超时（s）")]
    public double Timeout { get; set; } = 120;
    [Category("量程与采样"), DisplayName("触发源"), Description("外部触发：2400 Trigger Link 逐点触发；2600 直流逐点触发，脉冲为整组起始触发。等待受整次超时限制。")]
    public SmuTrigger Trigger { get; set; }
    [Category("量程与采样"), DisplayName("触发输入线"), Description("2400 Trigger Link 1～4；2600 数字 I/O 1～14，需要实际接线。")]
    public int TriggerLine { get; set; } = 1;

    public LocalSmuSettings Clone() => (LocalSmuSettings)MemberwiseClone();
    internal static double[] ParseList(string text) => text.Split([',', ';', ' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)
        .Select(value => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
    internal double[] SourceValues()
    {
        if (Sweep is SmuSweep.List or SmuSweep.SourceMemory) return ParseList(Values);
        if (Sweep == SmuSweep.Fixed) return Enumerable.Repeat(SourceValue, Waveform == SmuWaveform.Pulse ? Points : 1).ToArray();
        if (Points is < 2 or > 100000) throw new ArgumentException("扫描点数必须在 2 到 100000 之间。");
        if (Sweep == SmuSweep.Logarithmic && (Start == 0 || Stop == 0 || Math.Sign(Start) != Math.Sign(Stop)))
            throw new ArgumentException("对数扫描的起止值必须非零且同号。");
        return Enumerable.Range(0, Points).Select(i => Sweep == SmuSweep.Linear ? Start + (Stop - Start) * i / (Points - 1)
            : Math.Sign(Start) * Math.Exp(Math.Log(Math.Abs(Start)) + (Math.Log(Math.Abs(Stop)) - Math.Log(Math.Abs(Start))) * i / (Points - 1))).ToArray();
    }
    internal double[] Limits(int count) => Sweep == SmuSweep.SourceMemory && !string.IsNullOrWhiteSpace(MemoryLimits)
        ? ParseList(MemoryLimits) : Enumerable.Repeat(Limit, count).ToArray();

    internal void Validate(cvColorVision.Pss_Type type, bool checkLimits)
    {
        if (!Enum.IsDefined(Channel) || !Enum.IsDefined(Operation) || !Enum.IsDefined(Measurement) || !Enum.IsDefined(Sweep)
            || !Enum.IsDefined(Waveform) || !Enum.IsDefined(OhmsMode) || !Enum.IsDefined(OffMode) || !Enum.IsDefined(Filter) || !Enum.IsDefined(AutoZero) || !Enum.IsDefined(Trigger))
            throw new ArgumentException("源表模式无效。");
        double[] finite = [SourceValue, Limit, Start, Stop, Bias, PulseWidth, PulsePeriod, PulseMeasureDelay, SourceRange, MeasureRange, Nplc, Delay, OhmsRange, Timeout];
        if (finite.Any(value => !double.IsFinite(value))) throw new ArgumentException("源表参数必须为有限数值。");
        if (Nplc is < 0.001 or > 10 || Delay < 0 || SourceRange < 0 || MeasureRange < 0 || OhmsRange < 0 || FilterCount is < 1 or > 100 || Timeout is < 1 or > 3600)
            throw new ArgumentException("请检查积分时间、延时、量程、滤波次数和超时范围。");
        if (Points is < 1 or > 100000) throw new ArgumentException("点数必须在 1 到 100000 之间。");
        if (TriggerLine is < 1 or > 14) throw new ArgumentException("触发输入线必须在 1～14 之间。");
        if (Waveform == SmuWaveform.Pulse && (PulseWidth <= 0 || PulsePeriod <= PulseWidth || PulseMeasureDelay < 0
            || Operation != SmuOperation.SourceOnly && (type == cvColorVision.Pss_Type.Keithley_2600 ? PulseMeasureDelay : 0) + 2 * Nplc / 50 * 1000 >= PulseWidth))
            throw new ArgumentException("脉冲周期必须大于脉宽，采样延时与 V/I 积分窗口必须位于脉冲内。");
        if (Operation == SmuOperation.MeasureOnly && (Sweep != SmuSweep.Fixed || Waveform != SmuWaveform.DC || Measurement == SmuMeasurement.Resistance))
            throw new ArgumentException("仅测量使用固定直流的电压表/电流表接法；电阻测量需要激励源。");
        double[] sources = SourceValues(), limits = Limits(sources.Length);
        if (Waveform == SmuWaveform.Pulse && ((sources.Length - 1) * PulsePeriod + PulseWidth) / 1000 >= Timeout)
            throw new ArgumentException("脉冲组的最短执行时间超过整次超时，请减少点数、周期或增加超时。");
        if (sources.Length is < 1 or > 100000 || limits.Length != sources.Length || sources.Any(value => !double.IsFinite(value)))
            throw new ArgumentException("列表数量必须有效，逐点保护限值数量必须与列表相同。");
        foreach (var pair in sources.Zip(limits))
            new LocalSmuParameters { Channel = Channel, IsSourceV = IsSourceV, MeasureValue = pair.First, LimitValue = pair.Second }
                .Validate(type, false, checkLimits);
        new LocalSmuParameters { Channel = Channel, IsSourceV = IsSourceV, MeasureValue = Bias, LimitValue = Limit }.Validate(type, false, checkLimits);
    }

    internal void ValidateFamily(int family)
    {
        if (family is < 1 or > 4) throw new NotSupportedException("仪器未提供受支持的本地命令集。");
        if (family < 3 && Nplc < 0.01) throw new ArgumentException("2400 的积分时间不能小于 0.01 NPLC。");
        if (family < 3 && TriggerLine > 4) throw new ArgumentException("2400 Trigger Link 输入线必须在 1～4 之间。");
        if (HighCapacitance && IsSourceV && Limit < 0.001) throw new ArgumentException("高电容模式的电流保护限值不能低于 0.001 mA，避免仪器自动提高保护限值。");
        if (family < 3 && Channel != SMUChannelType.A) throw new ArgumentException("2400 系列只支持 A 通道。");
        if (family >= 3 && (OhmsGuard || OffMode == SmuOffMode.Guard || OffsetCompensation || OhmsMode == SmuOhmsMode.Auto || Sweep == SmuSweep.SourceMemory))
            throw new ArgumentException("自动电阻、偏置补偿、Ohms Guard 和源记忆扫描仅适用于 2400 系列。");
        if (HighCapacitance && family != 4) throw new ArgumentException("高电容模式需要受支持的 2600 A/B 仪器。");
        if (OhmsGuard && (!FourWire || Measurement != SmuMeasurement.Resistance)) throw new ArgumentException("Ohms Guard 需要四线电阻测量和正确的六线接线。");
        if (OhmsMode == SmuOhmsMode.Auto && (Measurement != SmuMeasurement.Resistance || IsSourceV || Sweep != SmuSweep.Fixed || Waveform != SmuWaveform.DC))
            throw new ArgumentException("自动电阻需要 2400 固定直流、电流源和电阻测量。");
        if (OffsetCompensation && (Measurement != SmuMeasurement.Resistance || Waveform != SmuWaveform.DC)) throw new ArgumentException("偏置补偿仅适用于直流电阻测量。");
        if (Sweep == SmuSweep.SourceMemory && (SourceValues().Length > 100 || Waveform != SmuWaveform.DC || Operation != SmuOperation.SourceMeasure))
            throw new ArgumentException("源记忆扫描支持 1～100 个直流测量配置。");
        if (Waveform != SmuWaveform.Pulse) return;
        if (family == 2 && (Operation != SmuOperation.SourceOnly || PulseWidth > 5 || PulsePeriod - PulseWidth < 1.1))
            throw new ArgumentException("2430 专用脉冲当前仅支持输出：脉宽 0.15～5 ms、关闭时间至少 1.1 ms；不提供并发 V/I 测量。");
        if (family == 3) throw new NotSupportedException("原始 2600 不支持当前 A/B 定时器协议，请使用受支持的 A/B 仪器。");
        if (SourceRange <= 0 || MeasureRange <= 0 || Filter != SmuFilter.Off || AutoZero != SmuAutoZero.Off || HighCapacitance)
            throw new ArgumentException("脉冲要求固定源/测量量程，并关闭滤波、自动调零和高电容模式。");
        if (family < 3 && (Bias != 0 || OffMode != SmuOffMode.Normal || PulseWidth < (family == 2 ? 0.15 : 2)))
            throw new ArgumentException("2400/2430 脉冲要求零基线和 Normal 关闭状态；最小目标脉宽分别为 2 ms / 0.15 ms。");
        if (family == 1 && Operation != SmuOperation.SourceMeasure) throw new ArgumentException("2400 间接脉冲需要输出并测量。");
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct SmuNativeSettings
{
    internal int Size, Version, Channel, SourceVoltage, Measurement, Operation, Sweep, Waveform, OhmsMode, OffsetCompensation,
        OffMode, FourWire, OhmsGuard, Front, Filter, FilterCount, AutoZero, HighCapacitance, Count, CloseOutput, Trigger, TriggerLine;
    internal double SourceRange, MeasureRange, Limit, Delay, Nplc, OhmsRange, PulseWidth, PulsePeriod, PulseMeasureDelay, Bias, Timeout;
    internal static SmuNativeSettings From(LocalSmuSettings s, int count) => new()
    {
        Size = Marshal.SizeOf<SmuNativeSettings>(), Version = 1, Channel = (int)s.Channel, SourceVoltage = s.IsSourceV ? 1 : 0,
        Measurement = (int)s.Measurement, Operation = (int)s.Operation, Sweep = (int)s.Sweep, Waveform = (int)s.Waveform,
        OhmsMode = (int)s.OhmsMode, OffsetCompensation = s.OffsetCompensation ? 1 : 0, OffMode = (int)s.OffMode,
        FourWire = s.FourWire ? 1 : 0, OhmsGuard = s.OhmsGuard ? 1 : 0, Front = s.Front ? 1 : 0,
        Filter = (int)s.Filter, FilterCount = s.FilterCount, AutoZero = (int)s.AutoZero, HighCapacitance = s.HighCapacitance ? 1 : 0,
        Count = count, CloseOutput = s.CloseOutput ? 1 : 0, Trigger = (int)s.Trigger, TriggerLine = s.TriggerLine, SourceRange = s.SourceRange, MeasureRange = s.MeasureRange,
        Limit = s.IsSourceV ? s.Limit / 1000 : s.Limit, Delay = s.Delay / 1000, Nplc = s.Nplc, OhmsRange = s.OhmsRange,
        PulseWidth = s.PulseWidth / 1000, PulsePeriod = s.PulsePeriod / 1000, PulseMeasureDelay = s.PulseMeasureDelay / 1000,
        Bias = s.IsSourceV ? s.Bias : s.Bias / 1000, Timeout = s.Timeout
    };
}

internal sealed record LocalSmuDetails(LocalSmuSettings Settings, double[] SourceValues, double?[] Resistance, int[] Status, string TimingMode)
{
    internal static double? ResistanceFromVI(double v, double milliAmps) => milliAmps == 0 || !double.IsFinite(v / milliAmps * 1000) ? null : v / milliAmps * 1000;
}
