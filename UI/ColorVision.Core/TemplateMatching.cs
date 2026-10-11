using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColorVision.Core;

public enum TemplateMatchingFeature { Intensity, Gradient }

public sealed class TemplateMatchingOptions
{
    [Category("匹配判定"), DisplayName("最低匹配分数"), Description("范围 0 到 1；低于该分数的候选不作为匹配结果。")]
    public double ScoreThreshold { get; set; } = 0.8;
    [Category("匹配判定"), DisplayName("最多匹配数量"), Description("最多返回的目标数量，范围 1 到 10000。")]
    public int MaxMatches { get; set; } = 10;
    [Category("匹配判定"), DisplayName("目标最小间距(px)"), Description("抑制中心距离过近的重复匹配；0 表示不按距离抑制。")]
    public double NmsRadius { get; set; } = 12;
    [Category("旋转搜索"), DisplayName("最小角度(°)")]
    public double AngleMin { get; set; } = -15;
    [Category("旋转搜索"), DisplayName("最大角度(°)")]
    public double AngleMax { get; set; } = 15;
    [Category("旋转搜索"), DisplayName("角度步长(°)")]
    public double AngleStep { get; set; } = 1;
    [Category("尺度搜索"), DisplayName("最小尺度"), Description("相对于参考图大小的比例，必须大于 0。")]
    public double ScaleMin { get; set; } = 0.9;
    [Category("尺度搜索"), DisplayName("最大尺度"), Description("最大为 20；固定大小时将最小和最大尺度都设为 1。")]
    public double ScaleMax { get; set; } = 1.1;
    [Category("尺度搜索"), DisplayName("尺度步长")]
    public double ScaleStep { get; set; } = 0.05;
    [Category("特征与精度"), DisplayName("特征模式"), Description("Intensity 使用灰度，Gradient 使用梯度特征。")]
    public TemplateMatchingFeature FeatureMode { get; set; } = TemplateMatchingFeature.Gradient;
    [Category("特征与精度"), DisplayName("允许遮挡比例"), Description("范围 0 到 0.75；较大值允许忽略更多不一致样本，也可能增加误匹配。")]
    public double OcclusionTolerance { get; set; } = 0.2;
    [Category("特征与精度"), DisplayName("金字塔层数"), Description("范围 1 到 8。")]
    public int PyramidLevels { get; set; } = 2;
    [Category("特征与精度"), DisplayName("亚像素定位")]
    public bool Subpixel { get; set; } = true;

    public TemplateMatchingOptions Copy() => (TemplateMatchingOptions)MemberwiseClone();

    public void Validate()
    {
        double[] values = [ScoreThreshold, NmsRadius, AngleMin, AngleMax, AngleStep, ScaleMin, ScaleMax, ScaleStep, OcclusionTolerance];
        if (values.Any(value => !double.IsFinite(value))) throw new ArgumentException("匹配参数必须为有限数值。");
        if (ScoreThreshold is < 0 or > 1 || MaxMatches is < 1 or > 10000 || NmsRadius is < 0 or > 1000000)
            throw new ArgumentException("匹配分数、数量或目标间距超出允许范围。");
        if (AngleMin > AngleMax || AngleStep <= 0 || ScaleMin <= 0 || ScaleMax < ScaleMin || ScaleMax > 20 || ScaleStep <= 0)
            throw new ArgumentException("角度/尺度范围必须递增，步长必须大于 0；尺度最大为 20。");
        if (!Enum.IsDefined(FeatureMode) || OcclusionTolerance is < 0 or > 0.75 || PyramidLevels is < 1 or > 8)
            throw new ArgumentException("特征模式、遮挡比例或金字塔层数无效。");
        double angles = Math.Floor((AngleMax - AngleMin) / AngleStep + 1e-9) + 1;
        double scales = Math.Floor((ScaleMax - ScaleMin) / ScaleStep + 1e-9) + 1;
        if (!double.IsFinite(angles * scales) || angles * scales > 10000)
            throw new ArgumentException("角度和尺度组合超过 10000 个，请缩小范围或增大步长。");
    }

    public string ToNativeJson()
    {
        Validate();
        return JsonSerializer.Serialize(this, TemplateMatchingFile.JsonOptions);
    }
}

public readonly record struct TemplateMatchingPoint(double X, double Y);
public sealed record TemplateMatchingMatch
{
    public TemplateMatchingPoint Center { get; init; }
    public double AngleDegrees { get; init; }
    public double Scale { get; init; }
    public double Score { get; init; }
    public double VisibleFraction { get; init; }
    public TemplateMatchingPoint[] Corners { get; init; } = [];
}

public sealed record TemplateMatchingResult
{
    public bool Success { get; init; }
    public string StatusCode { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string[] Warnings { get; init; } = [];
    public TemplateMatchingMatch[] Matches { get; init; } = [];
    [JsonIgnore] public string RawJson { get; init; } = string.Empty;
}

/// <summary>Shared native call for the image editor and local flow. Source memory is borrowed.</summary>
public static class TemplateMatching
{
    private static readonly JsonSerializerOptions ResultOptions = new() { PropertyNameCaseInsensitive = true };

    public static TemplateMatchingResult Run(HImage source, TemplateMatchingDocument document, RoiRect roi)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Validate();
        TemplateMatchingImage.ValidateImage(source);
        ValidateRoi(roi, source.cols, source.rows);
        TemplateMatchingOptions options = document.Options.Copy();
        string config = options.ToNativeJson();
        // A private copy prevents UI edits or a later file load from changing this invocation.
        byte[] pixels = (byte[])document.Image.Pixels.Clone();
        GCHandle pin = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        IntPtr resultPointer = IntPtr.Zero;
        try
        {
            HImage template = document.Image.Borrow(pin.AddrOfPinnedObject());
            int length = OpenCVMediaHelper.M_MatchRotatedTemplate(source, template, roi, config, out resultPointer);
            if (length <= 0 || resultPointer == IntPtr.Zero)
                throw new InvalidOperationException($"本地模板匹配调用失败，返回码 {length}。");
            IntPtr owned = resultPointer;
            resultPointer = IntPtr.Zero;
            string json = OpenCVMediaHelper.PtrToStringUtf8AndFree(owned);
            TemplateMatchingResult result = JsonSerializer.Deserialize<TemplateMatchingResult>(json, ResultOptions)
                ?? throw new InvalidOperationException("模板匹配返回空结果。");
            result = result with { RawJson = json };
            ValidateResult(result, source.cols, source.rows, options);
            return result;
        }
        finally
        {
            if (resultPointer != IntPtr.Zero) _ = OpenCVMediaHelper.FreeResult(resultPointer);
            pin.Free();
        }
    }

    public static void ValidateRoi(RoiRect roi, int width, int height)
    {
        if (roi.X == 0 && roi.Y == 0 && roi.Width == 0 && roi.Height == 0) return;
        if (roi.X < 0 || roi.Y < 0 || roi.Width <= 0 || roi.Height <= 0 || (long)roi.X + roi.Width > width || (long)roi.Y + roi.Height > height)
            throw new ArgumentException("搜索区域必须完整位于图像内；0,0,0,0 表示整图。");
    }

    public static void ValidateResult(TemplateMatchingResult result, int width, int height, TemplateMatchingOptions options)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Matches == null || result.Warnings == null) throw new InvalidOperationException("模板匹配返回无效结果集合。");
        if (!result.Success) return;
        if (result.Matches.Length == 0 || result.Matches.Length > options.MaxMatches)
            throw new InvalidOperationException("模板匹配成功结果的目标数量无效。");
        foreach (TemplateMatchingMatch match in result.Matches)
        {
            if (match == null || !double.IsFinite(match.Score) || match.Score < options.ScoreThreshold || match.Score > 1.000001 ||
                !double.IsFinite(match.AngleDegrees) || !double.IsFinite(match.Scale) || match.Scale <= 0 ||
                !double.IsFinite(match.VisibleFraction) || match.VisibleFraction is < 0 or > 1 ||
                match.Corners == null || match.Corners.Length != 4 || !IsInside(match.Center, width, height) ||
                match.Corners.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
                throw new InvalidOperationException("模板匹配返回无效的分数、姿态或坐标。");
            double area = 0;
            for (int i = 0; i < 4; i++)
            {
                TemplateMatchingPoint a = match.Corners[i], b = match.Corners[(i + 1) % 4];
                area += a.X * b.Y - b.X * a.Y;
            }
            if (Math.Abs(area) < 1e-6) throw new InvalidOperationException("模板匹配返回退化四角。");
        }
    }

    private static bool IsInside(TemplateMatchingPoint point, int width, int height) =>
        double.IsFinite(point.X) && double.IsFinite(point.Y) && point.X >= 0 && point.Y >= 0 && point.X < width && point.Y < height;
}
