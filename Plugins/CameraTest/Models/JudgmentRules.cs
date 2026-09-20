using System.ComponentModel;

namespace CameraTest.Models;

public sealed record JudgmentRules
{
    [Category("SFR 通道"), DisplayName("检查 Y 通道")]
    public bool CheckY { get; set; } = true;
    [Category("SFR 通道"), DisplayName("检查 R 通道")]
    public bool CheckR { get; set; }
    [Category("SFR 通道"), DisplayName("检查 G 通道")]
    public bool CheckG { get; set; }
    [Category("SFR 通道"), DisplayName("检查 B 通道")]
    public bool CheckB { get; set; }
    [Category("SFR 下限"), DisplayName("MTF50 下限 (cycles/pixel)"), Description("留空不检查；应用于全部点位四边的选定通道。须使用已确认的产品规格。")]
    public double? MinimumMtf50 { get; set; }
    [Category("SFR 下限"), DisplayName("MTF10 下限 (cycles/pixel)"), Description("留空不检查；Nyquist 范围内未找到交点时为无法判定。")]
    public double? MinimumMtf10 { get; set; }
    [Category("SFR 下限"), DisplayName("指定频率 MTF 下限 (0～1)"), Description("留空不检查；频率使用分析设置中的目标频率。")]
    public double? MinimumResponse { get; set; }
    [Category("色差上限"), DisplayName("R−G 位移绝对值上限 (px)"), Description("留空不检查；使用公共法线方向的 50% 边缘位置差。")]
    public double? MaximumRedGreenShift { get; set; }
    [Category("色差上限"), DisplayName("R−B 位移绝对值上限 (px)")]
    public double? MaximumRedBlueShift { get; set; }
    [Category("色差上限"), DisplayName("G−B 位移绝对值上限 (px)")]
    public double? MaximumGreenBlueShift { get; set; }

    public IEnumerable<string> SelectedChannels()
    {
        if (CheckY) yield return "L";
        if (CheckR) yield return "R";
        if (CheckG) yield return "G";
        if (CheckB) yield return "B";
    }

    public void Validate()
    {
        static bool Invalid(double? value, double maximum) => value.HasValue && (!double.IsFinite(value.Value) || value.Value < 0 || value.Value > maximum);
        if (Invalid(MinimumMtf50, 0.5) || Invalid(MinimumMtf10, 0.5) || Invalid(MinimumResponse, 1)
            || Invalid(MaximumRedGreenShift, double.MaxValue) || Invalid(MaximumRedBlueShift, double.MaxValue) || Invalid(MaximumGreenBlueShift, double.MaxValue))
            throw new ArgumentException("判定阈值须为有限非负数；MTF50/10 最大 0.5，指定频率响应最大 1。留空表示不检查。");
        if ((MinimumMtf50.HasValue || MinimumMtf10.HasValue || MinimumResponse.HasValue) && !SelectedChannels().Any())
            throw new ArgumentException("设置了 SFR 阈值时，至少选择一个通道。");
    }
}
