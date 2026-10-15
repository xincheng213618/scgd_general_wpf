using ColorVision.Engine.Services.POI;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.POIGenCali;
using CVCommCore.CVAlgorithm;
using System;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.Camera.Local;

internal sealed record SpectrumColorCorrection
{
    public required double[] OriginalCoefficients { get; init; }
    public required double[] ReferenceXyz { get; init; }
    public required double[] SpectrumXyz { get; init; }
    public required double[] Factors { get; init; }
    public string PointName { get; init; } = string.Empty;
    public GenCalibrationType Mode { get; init; }
    public int PoiMasterId { get; init; }
    public int SpectrumMasterId { get; init; }
    public DateTime CorrectedUtc { get; init; }
}

/// <summary>Composes an XYZ correction with the executed RAW transform, without creating full XYZ planes.</summary>
internal static class SpectrumRawCalibrationService
{
    internal static LocalPoiPointResult SelectPoint(LocalPoiResultSet poi, string name)
    {
        LocalPoiPointResult[] matches = string.IsNullOrWhiteSpace(name)
            ? poi.Points.ToArray()
            : poi.Points.Where(point => string.Equals(point.Name, name, StringComparison.Ordinal)).ToArray();
        return matches.Length == 1 ? matches[0]
            : throw new InvalidOperationException("请指定一个与光谱仪测量区域对应的唯一 POI 点名称；仅有一个点时可留空。");
    }

    internal static ColorCalibrationSnapshot Calculate(LocalFlowFrame frame, LocalPoiPointResult point,
        double x, double y, double luminance, GenCalibrationType mode, int poiMasterId, int spectrumMasterId)
    {
        ColorCalibrationSnapshot current = frame.ColorCalibration
            ?? throw new InvalidOperationException("输入 RAW 缺少色度矩阵，请使用带颜色校正的本地相机和本地 POI。");
        current.Validate();
        if (!frame.HasRaw || frame.HasCie || !current.CanReplay || current.Channels != 3 || !frame.IsRawFlipApplied
            || current.Width != frame.Metadata.Width || current.Height != frame.Metadata.Height || current.RawBpp != frame.Metadata.SourceBpp)
            throw new InvalidOperationException("光谱参考校正需要已完成基础校正和镜像的三通道 RAW；上游请使用本地相机的加速路径。");
        if (!double.IsFinite(x) || !double.IsFinite(y) || x <= 0 || y <= 0 || x + y >= 1
            || !double.IsFinite(luminance) || luminance <= 0)
            throw new InvalidOperationException("光谱仪的 x、y 或亮度无效，不能生成色度修正矩阵。");

        ColorCalibrationSnapshot baseline = current with
        {
            Coefficients = (double[])(current.SpectrumCorrection?.OriginalCoefficients ?? current.Coefficients).Clone(),
            SpectrumCorrection = null
        };
        baseline.Validate();
        PoiMeasurementShape shape = point.PointType switch
        {
            POIPointTypes.SolidPoint => PoiMeasurementShape.Point,
            POIPointTypes.Circle => PoiMeasurementShape.Circle,
            POIPointTypes.Rect => PoiMeasurementShape.Rect,
            _ => throw new InvalidOperationException("参考 POI 的形状不支持本地 RAW 测量。")
        };
        using LocalFlowFrameLease lease = frame.Acquire();
        // Re-measure the original transform so retries and a second correction never reuse already scaled POI values.
        PoiMeasurementResult measured = PoiMeasurementService.CalculateCalibratedRaw(lease.RawPointer, lease.RawLength, baseline,
            [new PoiMeasurementPoint(point.X, point.Y, point.Width, point.Height, shape)])[0];
        double[] reference = [measured.X, measured.Y, measured.Z];
        double[] target = [x * luminance / y, luminance, (1 - x - y) * luminance / y];
        double[] factors = GetFactors(reference, target, mode);
        double[] coefficients = Compose(baseline, factors);
        return baseline with
        {
            Coefficients = coefficients,
            SpectrumCorrection = new SpectrumColorCorrection
            {
                OriginalCoefficients = (double[])baseline.Coefficients.Clone(), ReferenceXyz = reference,
                SpectrumXyz = target, Factors = factors, PointName = point.Name, Mode = mode,
                PoiMasterId = poiMasterId, SpectrumMasterId = spectrumMasterId, CorrectedUtc = DateTime.UtcNow
            }
        };
    }

    internal static double[] GetFactors(double[] reference, double[] target, GenCalibrationType mode)
    {
        if (reference.Length != 3 || target.Length != 3
            || reference.Any(value => !double.IsFinite(value) || value <= 0)
            || target.Any(value => !double.IsFinite(value) || value <= 0))
            throw new InvalidOperationException("参考 POI 或光谱 XYZ 为零、负值或无效值，请检查光路、曝光和参考区域。");
        double m = target[0] / reference[0], n = target[1] / reference[1], p = target[2] / reference[2];
        double[] factors = mode switch
        {
            GenCalibrationType.BrightnessAndChroma => [m, n, p],
            GenCalibrationType.BrightnessOnly => [n, n, n],
            GenCalibrationType.ChromaOnly => [m / n, 1, p / n],
            _ => throw new InvalidOperationException("请选择亮度、色度或亮色度均修正。")
        };
        if (factors.Any(value => !double.IsFinite(value) || value <= 0))
            throw new InvalidOperationException("光谱修正倍率无效。");
        return factors;
    }

    internal static double[] Compose(ColorCalibrationSnapshot baseline, double[] factors)
    {
        baseline.Validate();
        if (baseline.Channels != 3 || factors.Length != 3 || factors.Any(value => !double.IsFinite(value) || value <= 0))
            throw new InvalidOperationException("仅支持三通道 XYZ 色度倍率。");
        double[] matrix = (double[])baseline.Coefficients.Clone();
        if (baseline.TransformKind == 0)
        {
            // XYZ = M * RGB: scale output rows, not the RGB input columns. Exposure/gain are already folded into M.
            for (int i = 0; i < matrix.Length; i++) matrix[i] *= factors[i / 3];
        }
        else
        {
            // One-color layout: X = aR + dB, Y = bG, Z = cB.
            matrix[0] *= factors[0]; matrix[3] *= factors[0];
            matrix[1] *= factors[1]; matrix[2] *= factors[2];
        }
        if (matrix.Any(value => !double.IsFinite(value))) throw new InvalidOperationException("修正后的矩阵超出有效范围。");
        return matrix;
    }

    internal static int Save(LocalFlowFrame frame, ColorCalibrationSnapshot corrected, Func<int> persistResult)
    {
        ColorCalibrationSnapshot previous = frame.ColorCalibration ?? throw new InvalidOperationException("RAW 缺少色度矩阵。");
        string path = frame.CvRawFilePath;
        if (string.IsNullOrWhiteSpace(path) || !string.Equals(Path.GetExtension(path), ".cvraw", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("本地相机没有提供 CVRAW 保存路径。");
        // Memory-only camera captures become durable here; existing RAW pixels are never re-written.
        if (!File.Exists(path)) LocalFrameFileService.SaveCapture(frame, path);
        corrected.Save(path, canReplay: true);
        int masterId;
        try
        {
            masterId = persistResult();
            if (masterId <= 0) throw new InvalidOperationException("保存光谱参考校正图像记录失败。");
        }
        catch (Exception saveError)
        {
            try { previous.Save(path, canReplay: true); }
            catch (Exception rollbackError) { throw new AggregateException("图像记录保存失败，且 RAW 色度参数恢复失败。", saveError, rollbackError); }
            throw;
        }
        frame.ColorCalibration = corrected;
        frame.MasterId = masterId;
        return masterId;
    }
}
