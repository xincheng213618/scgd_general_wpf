using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ColorVision.Core;

public enum SfrInputEncoding
{
    [Description("未知（仅诊断）")] Unknown,
    [Description("线性信号")] Linear,
    [Description("sRGB 编码")] Srgb,
    [Description("已知幂函数编码")] Power
}

public sealed record SfrAnalysisOptions
{
    [Category("输入"), DisplayName("输入编码"), Description("线性图选择 Linear；sRGB 图片选择 Srgb。未知编码仅供诊断，不自动猜 Gamma。")]
    public SfrInputEncoding InputEncoding { get; set; }
    [Category("输入"), DisplayName("解码指数"), Description("仅 Power 生效：线性值 = 归一化输入值 ^ 解码指数。")]
    public double DecodeExponent { get; set; } = 2.2;
    [Category("输入"), DisplayName("黑电平"), Description("原始像素数值；低于此值的输入被拒绝，不做 ROI 自动拉伸。")]
    public double BlackLevel { get; set; }
    [Category("输入"), DisplayName("白电平（0=类型满量程）"), Description("0 表示 8 位用 255、16 位用 65535、浮点用 1。标定浮点或 12 位装入 16 位容器时请填写实际满量程。")]
    public double WhiteLevel { get; set; }
    [Category("测量对象"), DisplayName("显示屏／近眼显示"), Description("结果包含显示像素结构与测试相机。明显子像素或摩尔纹可能使单斜边模型不适用；不会自动模糊原图。")]
    public bool DisplayTarget { get; set; }
    [Category("质量门限"), DisplayName("最小边缘跨度"), Description("线性化后黑白平台差，按输入满量程归一化。诊断门限，不是产品合格规格。")]
    public double MinimumContrast { get; set; } = 0.02;
    [Category("质量门限"), DisplayName("最小边缘信噪比"), Description("平台差 / 两侧平台的合并标准差；周期纹理也计入平台波动。")]
    public double MinimumSnr { get; set; } = 10;
    [Category("质量门限"), DisplayName("最大直线拟合残差 (px)"), Description("超过此值拒绝单直线边缘模型；不要为了得到数值而放宽门限。")]
    public double MaximumFitRms { get; set; } = 0.35;

    public void Validate()
    {
        if (!Enum.IsDefined(InputEncoding) || !double.IsFinite(BlackLevel) || !double.IsFinite(WhiteLevel) || WhiteLevel < 0 || (WhiteLevel > 0 && WhiteLevel <= BlackLevel)
            || !double.IsFinite(DecodeExponent) || DecodeExponent <= 0 || DecodeExponent > 5
            || !double.IsFinite(MinimumContrast) || MinimumContrast <= 0 || MinimumContrast > 1
            || !double.IsFinite(MinimumSnr) || MinimumSnr <= 0 || MinimumSnr > 1000
            || !double.IsFinite(MaximumFitRms) || MaximumFitRms <= 0 || MaximumFitRms > 5)
            throw new ArgumentException("请检查黑白电平、解码指数和质量门限的范围。");
    }

    public string ToJson()
    {
        Validate();
        return JsonSerializer.Serialize(new
        {
            encoding = InputEncoding.ToString().ToLowerInvariant(), decodeExponent = DecodeExponent,
            blackLevel = BlackLevel, whiteLevel = WhiteLevel, minimumContrast = MinimumContrast,
            minimumSnr = MinimumSnr, maximumFitRms = MaximumFitRms, displayTarget = DisplayTarget
        });
    }
}

public sealed record SfrChannelAnalysis
{
    public string Channel { get; init; } = "L";
    public bool Valid { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string[] Warnings { get; init; } = [];
    public double Contrast { get; init; }
    public double Noise { get; init; }
    public double Snr { get; init; }
    public double ClippedFraction { get; init; }
    public double AngleDegrees { get; init; }
    public double FitRms { get; init; }
    public double BinCoverage { get; init; }
    public double EdgeIntercept { get; init; }
    public double EdgeSlope { get; init; }
    public bool Rotated { get; init; }
    public bool PlateausAvailable { get; init; }
    public bool FitAvailable { get; init; }
    public bool SamplingAvailable { get; init; }
    public double[] Frequencies { get; init; } = [];
    public double[] Mtf { get; init; } = [];
    public double? Mtf50 { get; init; }
    public double? Mtf10 { get; init; }
    public double[] EdgePositions { get; init; } = [];
    public double[] Esf { get; init; } = [];
    public double[] LsfPositions { get; init; } = [];
    public double[] Lsf { get; init; } = [];
}

public sealed record SfrAnalysisResult
{
    public string AlgorithmVersion { get; init; } = string.Empty;
    // Earlier V2 DLLs omitted this field and used a windowed derivative centroid.
    public string EdgeLocalization { get; init; } = "centroid";
    public string Unit { get; init; } = string.Empty;
    public double Nyquist { get; init; }
    public int SourceDepth { get; init; }
    public IReadOnlyList<SfrChannelAnalysis> Channels { get; init; } = [];

    public static SfrAnalysisResult Parse(string json)
    {
        var result = JsonSerializer.Deserialize<SfrAnalysisResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new FormatException("SFR 结果为空。");
        if (result.AlgorithmVersion != "2.0" || result.Unit != "cycles/pixel" || result.Nyquist != 0.5 || result.Channels == null || result.Channels.Count is not (1 or 4)
            || result.Channels.Any(c => c == null) || result.Channels.Select(c => c.Channel).Distinct().Count() != result.Channels.Count
            || !result.Channels.Any(c => c.Channel == "L"))
            throw new FormatException("SFR 结果版本、单位或通道不匹配。");
        foreach (var c in result.Channels)
        {
            if (c.Channel is not ("L" or "R" or "G" or "B")) throw new FormatException("未知 SFR 通道。");
            if (c.Warnings == null || c.Reason == null || c.Frequencies == null || c.Mtf == null || c.EdgePositions == null || c.Esf == null || c.LsfPositions == null || c.Lsf == null
                || !new[] { c.Contrast, c.Noise, c.Snr, c.ClippedFraction, c.AngleDegrees, c.FitRms, c.BinCoverage, c.EdgeIntercept, c.EdgeSlope }.All(double.IsFinite))
                throw new FormatException("SFR 诊断数据无效。");
            if (!c.Valid)
            {
                if (string.IsNullOrEmpty(c.Reason) || c.Mtf50.HasValue || c.Mtf10.HasValue || c.Frequencies.Length != 0 || c.Mtf.Length != 0
                    || c.EdgePositions.Length != 0 || c.Esf.Length != 0 || c.LsfPositions.Length != 0 || c.Lsf.Length != 0) throw new FormatException("无效测量包含数值结果。");
                continue;
            }
            if (!SfrCurveQueries.IsValid(c.Frequencies, c.Mtf) || !SfrCurveQueries.IsValid(c.EdgePositions, c.Esf)
                || !SfrCurveQueries.IsValid(c.LsfPositions, c.Lsf) || c.Frequencies[0] != 0 || Math.Abs(c.Mtf[0] - 1) > 1e-6)
                throw new FormatException("SFR 曲线数据无效。");
            if (!SameMetric(c.Mtf50, SfrCurveQueries.Crossing(c.Frequencies, c.Mtf, 0.5))
                || !SameMetric(c.Mtf10, SfrCurveQueries.Crossing(c.Frequencies, c.Mtf, 0.1)))
                throw new FormatException("SFR 指标与曲线不一致。");
        }
        return result;
    }

    private static bool SameMetric(double? a, double? b) => a.HasValue == b.HasValue && (!a.HasValue || double.IsFinite(a.Value) && Math.Abs(a.Value - b!.Value) < 1e-9);
}

public static class SfrCurveQueries
{
    public static bool IsValid(double[]? x, double[]? y) => x != null && y != null && x.Length >= 2 && x.Length == y.Length
        && x.All(double.IsFinite) && y.All(double.IsFinite) && x.Zip(x.Skip(1), (a, b) => a < b).All(v => v);

    public static double? AtFrequency(double[] x, double[] y, double frequency)
    {
        if (!double.IsFinite(frequency) || frequency < 0 || frequency > 0.5 || !IsValid(x, y) || frequency < x[0] || frequency > x[^1]) return null;
        for (int i = 1; i < x.Length; i++)
            if (frequency <= x[i]) return y[i - 1] + (frequency - x[i - 1]) * (y[i] - y[i - 1]) / (x[i] - x[i - 1]);
        return null;
    }

    public static double? Crossing(double[] x, double[] y, double threshold)
    {
        if (!double.IsFinite(threshold) || threshold <= 0 || threshold >= 1 || !IsValid(x, y)) return null;
        for (int i = 1; i < x.Length; i++)
        {
            if (y[i - 1] >= threshold && y[i] <= threshold && y[i - 1] != y[i])
            {
                double frequency = x[i - 1] + (threshold - y[i - 1]) * (x[i] - x[i - 1]) / (y[i] - y[i - 1]);
                return frequency is >= 0 and <= 0.5 ? frequency : null;
            }
        }
        return null;
    }
}

public static class SfrAnalyzer
{
    public static SfrAnalysisResult Analyze(HImage image, RoiRect roi, SfrAnalysisOptions options)
    {
        IntPtr pointer = IntPtr.Zero;
        try
        {
            int code = OpenCVMediaHelper.M_AnalyzeSfrV2(image, roi, options.ToJson(), out pointer);
            if (code <= 0 || pointer == IntPtr.Zero)
                throw new InvalidOperationException($"SFR 无法分析输入（返回码 {code}）。请检查 ROI、位深和黑白电平；浮点图默认范围为 0..1。单 ROI 最多 1600 万像素，边长不超过 8192。");
            return SfrAnalysisResult.Parse(Marshal.PtrToStringUTF8(pointer, code - 1) ?? string.Empty);
        }
        catch (EntryPointNotFoundException exception)
        {
            throw new InvalidOperationException("当前 opencv_helper.dll 不支持 SFR 诊断接口，请使用与程序配套的原生 DLL。", exception);
        }
        finally { if (pointer != IntPtr.Zero) _ = OpenCVMediaHelper.FreeResult(pointer); }
    }
}
