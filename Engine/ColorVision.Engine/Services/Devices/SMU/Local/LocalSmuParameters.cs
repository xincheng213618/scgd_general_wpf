using ColorVision.Engine.Services.Devices.SMU.Configs;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using cvColorVision;
using System;

namespace ColorVision.Engine.Services.Devices.SMU.Local;

internal sealed record LocalSmuConnection(bool IsNet, string DeviceName, Pss_Type DeviceType, double DelayTime, bool Is4Wire, bool IsFront, int BaudRate = 9600)
{
    internal static LocalSmuConnection From(ConfigSMU config, int baudRate = 9600) => new(config.IsNet, config.DevName?.Trim() ?? string.Empty, config.DevType, config.DelayTime, config.Is4Wire, config.IsFront, config.IsNet ? 9600 : baudRate);
    internal string Endpoint => $"{(IsNet ? "NET" : "COM")}:{DeviceName.ToUpperInvariant()}";
    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(DeviceName) || !Enum.IsDefined(DeviceType)) throw new ArgumentException("请设置有效的源表设备名和类型。");
        if (!double.IsFinite(DelayTime) || DelayTime < 0) throw new ArgumentException("源表延时必须是非负有限值。");
        if (!IsNet)
        {
            if (!Enum.IsDefined((SMUSerialBaudRate)BaudRate)) throw new ArgumentException("请选择有效的源表串口波特率。");
            if (DeviceType == Pss_Type.Keithley_2400 && BaudRate > 57600) throw new ArgumentException("Keithley 2400 串口波特率最高支持 57600。");
            if (DeviceType is not (Pss_Type.Keithley_2400 or Pss_Type.Keithley_2600) && BaudRate != 9600)
                throw new ArgumentException("当前源表类型不支持自定义串口波特率，请使用 9600。");
        }
    }
}

// Parameters and session captures use V / mA. Legacy scan payloads/storage retain A in the result adapter.
internal sealed record LocalSmuParameters
{
    public SMUChannelType Channel { get; init; }
    public bool IsSourceV { get; init; } = true;
    public double MeasureValue { get; init; }
    public double LimitValue { get; init; }
    public bool IsAutoRng { get; init; } = true;
    public double SrcRng { get; init; }
    public double LmtRng { get; init; }
    public double BeginValue { get; init; }
    public double EndValue { get; init; }
    public int Points { get; init; }
    public bool IsCloseOutput { get; init; }

    internal static LocalSmuParameters FromTemplate(SMUParam template, SMUChannelType channel, bool closeOutput) => new()
    {
        Channel = channel, IsSourceV = template.IsSourceV, BeginValue = template.StartMeasureVal, EndValue = template.StopMeasureVal,
        MeasureValue = template.StartMeasureVal, LimitValue = template.LmtVal, Points = template.Number,
        IsAutoRng = template.IsAutoRng, SrcRng = template.SrcRng, LmtRng = template.LmtRng, IsCloseOutput = closeOutput
    };

    internal void Validate(Pss_Type type, bool scan, bool checkLimits)
    {
        if (!Enum.IsDefined(Channel)) throw new ArgumentException("源表通道无效。");
        if (!double.IsFinite(LimitValue) || LimitValue == 0) throw new ArgumentException("源表保护限值必须是非零有限值。");
        if (!double.IsFinite(MeasureValue) || !double.IsFinite(BeginValue) || !double.IsFinite(EndValue)) throw new ArgumentException("源表设置值必须为有限值。");
        if (scan && Points is < 2 or > 100000) throw new ArgumentException("源表扫描点数必须在 2 到 100000 之间。");
        if (!IsAutoRng && (!double.IsFinite(SrcRng) || !double.IsFinite(LmtRng) || SrcRng <= 0 || LmtRng <= 0)) throw new ArgumentException("手动源量程和限量程必须大于零。");
        if (!checkLimits) return;
        double source = scan ? Math.Max(Math.Abs(BeginValue), Math.Abs(EndValue)) : Math.Abs(MeasureValue);
        double voltage = IsSourceV ? source : Math.Abs(LimitValue);
        double current = (IsSourceV ? Math.Abs(LimitValue) : source) / 1000;
        bool valid = type switch
        {
            Pss_Type.Keithley_2400 => voltage <= 200 && current <= (voltage > 20 ? 0.1 : 1),
            Pss_Type.Keithley_2600 => voltage <= 40 && current <= (voltage > 6 ? 1 : 3),
            Pss_Type.Precise_S100 => voltage <= 30 && current <= 1,
            _ => true
        };
        if (!valid) throw new ArgumentException("源值或保护限值超过当前源表的客户端限制。");
    }
    internal double NativeSource(double value) => IsSourceV ? value : value / 1000;
    internal double NativeLimit => IsSourceV ? LimitValue / 1000 : LimitValue;
}

internal sealed record LocalSmuCapture(LocalSmuParameters Parameters, double[] Voltages, double[] Currents, int TotalTime, bool IsScan)
{
    internal DateTime CapturedAt { get; } = DateTime.Now;
    internal long Generation { get; init; }
    internal double V => Voltages[^1];
    internal double I => Currents[^1];
}
