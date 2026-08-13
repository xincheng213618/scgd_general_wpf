using Spectrum.Models;

namespace Spectrum.Calibration.Correction;

internal sealed record SpectrumCorrectionOutput(
    MagnitudeCalibrationFile CorrectedFile,
    double[] Wavelengths,
    double[] MeasuredValues,
    double[] StandardValues,
    double[] CorrectionFactors,
    int FilledFactorCount);

internal static class SpectrumCorrectionCalculator
{
    private const int PointCount = 4001;
    private const double Start = 380d;
    private const double End = 780d;
    private const double Interval = 0.1d;
    private const double MinimumFactor = 0.1d;

    public static SpectrumCorrectionOutput CorrectSpectrum(
        MagnitudeCalibrationFile source,
        ViewResultSpectrum measured,
        IReadOnlyList<(double Wavelength, double Value)> standard)
    {
        ValidateCanonicalFile(source);
        double[] measuredValues = GetAbsoluteSpectrum(measured);
        (double[] standardWavelengths, double[] standardInputValues) = ValidateStandard(standard);
        double[] standardValues = new double[PointCount];
        double[] factors = new double[PointCount];
        double[] coefficients = new double[PointCount];
        bool[] valid = new bool[PointCount];
        List<int> validIndices = [];
        double peak = measuredValues.Max();
        double threshold = Math.Max(1e-12, peak * 1e-4);

        for (int index = 0; index < PointCount; index++)
        {
            double wavelength = Start + Interval * index;
            standardValues[index] = Interpolate(standardWavelengths, standardInputValues, wavelength);
            if (measuredValues[index] <= threshold) continue;
            double factor = standardValues[index] / measuredValues[index];
            ValidateFactor(factor, wavelength);
            factors[index] = factor;
            valid[index] = true;
            validIndices.Add(index);
        }

        int required = (int)Math.Ceiling(PointCount * 0.1);
        if (validIndices.Count < required)
            throw new InvalidOperationException($"有效实测点只有 {validIndices.Count} 个，至少需要 {required} 个。");
        double validSpan = (validIndices[^1] - validIndices[0]) * Interval;
        if (validSpan < (End - Start) * 0.5)
            throw new InvalidOperationException("有效实测光谱覆盖范围不足 50%。");

        FillMissingFactors(factors, valid, validIndices);
        for (int index = 0; index < PointCount; index++)
        {
            double wavelength = Start + Interval * index;
            ValidateFactor(factors[index], wavelength);
            coefficients[index] = source.Coefficients[index] * factors[index];
            if (!double.IsFinite(coefficients[index]))
                throw new InvalidOperationException($"{wavelength:F1} nm 的新系数无效。");
        }

        double[] wavelengths = Enumerable.Range(0, PointCount).Select(index => Start + Interval * index).ToArray();
        return new SpectrumCorrectionOutput(source.WithCoefficients(coefficients), wavelengths,
            measuredValues, standardValues, factors, PointCount - validIndices.Count);
    }

    public static MagnitudeCalibrationFile CorrectBrightness(
        MagnitudeCalibrationFile source, double targetBrightness, double measuredBrightness)
    {
        ValidateCanonicalFile(source);
        if (!double.IsFinite(targetBrightness) || targetBrightness <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetBrightness), "目标亮度必须为有限正数。");
        if (!double.IsFinite(measuredBrightness) || measuredBrightness <= 1e-12)
            throw new ArgumentOutOfRangeException(nameof(measuredBrightness), "实测亮度必须为有限正数。");
        double factor = targetBrightness / measuredBrightness;
        ValidateFactor(factor, null);
        return source.WithCoefficients(source.Coefficients.Select(value => value * factor).ToArray());
    }

    public static double[] GetAbsoluteSpectrum(ViewResultSpectrum result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.fPL is null || result.fPL.Length != PointCount ||
            Math.Abs(result.fSpect1 - Start) > 1e-4 || Math.Abs(result.fSpect2 - End) > 1e-4 ||
            Math.Abs(result.fInterval - Interval) > 1e-6)
        {
            throw new InvalidOperationException("仅支持 380–780 nm、0.1 nm 间隔、4001 点的完整光谱结果。");
        }
        if (!float.IsFinite(result.fPlambda) || result.fPlambda <= 0)
            throw new InvalidOperationException("当前结果的绝对光谱系数无效。");
        double[] absolute = new double[PointCount];
        for (int index = 0; index < PointCount; index++)
        {
            absolute[index] = result.fPL[index] * result.fPlambda;
            if (!double.IsFinite(absolute[index]) || absolute[index] < 0)
                throw new InvalidOperationException($"实测光谱在索引 {index} 处无效。");
        }
        return absolute;
    }

    private static void ValidateCanonicalFile(MagnitudeCalibrationFile file)
    {
        if (file.Count != PointCount) throw new InvalidOperationException("幅值 DAT 必须包含 4001 点。");
        for (int index = 0; index < PointCount; index++)
        {
            double expected = Start + Interval * index;
            if (Math.Abs(file.Wavelengths[index] - expected) > 1e-6)
                throw new InvalidOperationException("幅值 DAT 必须使用 380–780 nm、0.1 nm 的标准网格。");
        }
    }

    private static (double[], double[]) ValidateStandard(IReadOnlyList<(double Wavelength, double Value)> standard)
    {
        ArgumentNullException.ThrowIfNull(standard);
        if (standard.Count < 2) throw new InvalidOperationException("标准光谱至少需要两行数据。");
        double[] wavelengths = new double[standard.Count];
        double[] values = new double[standard.Count];
        for (int index = 0; index < standard.Count; index++)
        {
            (double wavelength, double value) = standard[index];
            if (!double.IsFinite(wavelength) || !double.IsFinite(value) || value < 0)
                throw new InvalidOperationException($"标准光谱第 {index + 1} 行无效。");
            if (index > 0 && wavelength <= wavelengths[index - 1])
                throw new InvalidOperationException("标准光谱波长必须严格递增。");
            wavelengths[index] = wavelength;
            values[index] = value;
        }
        if (wavelengths[0] > Start + 1e-7 || wavelengths[^1] < End - 1e-7)
            throw new InvalidOperationException("标准光谱必须完整覆盖 380–780 nm。");
        return (wavelengths, values);
    }

    private static double Interpolate(double[] wavelengths, double[] values, double wavelength)
    {
        int upper = Array.BinarySearch(wavelengths, wavelength);
        if (upper >= 0) return values[upper];
        upper = ~upper;
        if (upper <= 0 || upper >= wavelengths.Length)
            throw new InvalidOperationException("标准光谱不能覆盖目标波长。");
        int lower = upper - 1;
        double position = (wavelength - wavelengths[lower]) / (wavelengths[upper] - wavelengths[lower]);
        return values[lower] + position * (values[upper] - values[lower]);
    }

    private static void FillMissingFactors(double[] factors, bool[] valid, List<int> indices)
    {
        int first = indices[0];
        for (int index = 0; index < first; index++) factors[index] = factors[first];
        for (int pair = 0; pair < indices.Count - 1; pair++)
        {
            int left = indices[pair];
            int right = indices[pair + 1];
            for (int index = left + 1; index < right; index++)
            {
                if (valid[index]) continue;
                double position = (double)(index - left) / (right - left);
                factors[index] = factors[left] + position * (factors[right] - factors[left]);
            }
        }
        int last = indices[^1];
        for (int index = last + 1; index < factors.Length; index++) factors[index] = factors[last];
    }

    private static void ValidateFactor(double factor, double? wavelength)
    {
        string where = wavelength.HasValue ? $"（{wavelength:F1} nm）" : string.Empty;
        if (!double.IsFinite(factor) || factor < MinimumFactor)
            throw new InvalidOperationException($"校正倍率{where}必须为不小于 {MinimumFactor} 的有限值，实际为 {factor:G8}。");
    }
}
