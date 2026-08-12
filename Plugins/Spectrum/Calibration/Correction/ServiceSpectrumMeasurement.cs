namespace Spectrum.Calibration.Correction;

/// <summary>
/// Complete service spectrum metadata. Absolute values are reconstructed as fPL * fPlambda.
/// </summary>
public sealed class ServiceSpectrumMeasurement
{
    private readonly double[] _relativeSpectrum;

    public ServiceSpectrumMeasurement(
        double startWavelength,
        double endWavelength,
        double interval,
        IReadOnlyList<double> relativeSpectrum,
        double absoluteScale)
    {
        ArgumentNullException.ThrowIfNull(relativeSpectrum);
        ValidateMetadata(startWavelength, endWavelength, interval, relativeSpectrum.Count);
        if (!double.IsFinite(absoluteScale) || absoluteScale <= 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteScale), "Absolute spectrum scale must be finite and positive.");

        _relativeSpectrum = relativeSpectrum.ToArray();
        for (int index = 0; index < _relativeSpectrum.Length; index++)
        {
            if (!double.IsFinite(_relativeSpectrum[index]))
                throw new ArgumentException($"Relative spectrum value at index {index} is not finite.", nameof(relativeSpectrum));
            if (_relativeSpectrum[index] < 0)
                throw new ArgumentException($"Relative spectrum value at index {index} is negative.", nameof(relativeSpectrum));
        }

        StartWavelength = startWavelength;
        EndWavelength = endWavelength;
        Interval = interval;
        AbsoluteScale = absoluteScale;
    }

    public ServiceSpectrumMeasurement(
        double startWavelength,
        double endWavelength,
        double interval,
        IReadOnlyList<float> relativeSpectrum,
        double absoluteScale)
        : this(startWavelength, endWavelength, interval, ConvertValues(relativeSpectrum), absoluteScale)
    {
    }

    public double StartWavelength { get; }
    public double EndWavelength { get; }
    public double Interval { get; }
    public double AbsoluteScale { get; }
    public int Count => _relativeSpectrum.Length;

    public SpectrumSeries ToAbsoluteSpectrum()
    {
        double[] wavelengths = new double[Count];
        double[] absoluteValues = new double[Count];
        double range = EndWavelength - StartWavelength;
        for (int index = 0; index < Count; index++)
        {
            wavelengths[index] = index == Count - 1
                ? EndWavelength
                : StartWavelength + range * index / (Count - 1);
            absoluteValues[index] = _relativeSpectrum[index] * AbsoluteScale;
            if (!double.IsFinite(absoluteValues[index]))
                throw new InvalidOperationException($"Absolute spectrum value at index {index} is not finite.");
        }
        return new SpectrumSeries(wavelengths, absoluteValues);
    }

    private static void ValidateMetadata(double startWavelength, double endWavelength, double interval, int actualCount)
    {
        if (!double.IsFinite(startWavelength))
            throw new ArgumentOutOfRangeException(nameof(startWavelength), "Start wavelength must be finite.");
        if (!double.IsFinite(endWavelength) || endWavelength <= startWavelength)
            throw new ArgumentOutOfRangeException(nameof(endWavelength), "End wavelength must be finite and greater than the start wavelength.");
        if (!double.IsFinite(interval) || interval <= 0)
            throw new ArgumentOutOfRangeException(nameof(interval), "Spectrum interval must be finite and positive.");

        double stepCount = (endWavelength - startWavelength) / interval;
        double roundedStepCount = Math.Round(stepCount);
        double tolerance = Math.Max(1d, Math.Abs(stepCount)) * 1e-6;
        if (Math.Abs(stepCount - roundedStepCount) > tolerance || roundedStepCount > int.MaxValue - 1)
            throw new ArgumentException("Spectrum range is not an integral number of intervals.", nameof(interval));

        int expectedCount = checked((int)roundedStepCount + 1);
        if (expectedCount < 2 || actualCount != expectedCount)
        {
            throw new ArgumentException(
                $"Spectrum metadata requires {expectedCount} values, but {actualCount} values were supplied.",
                nameof(actualCount));
        }
    }

    private static double[] ConvertValues(IReadOnlyList<float> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        double[] converted = new double[values.Count];
        for (int index = 0; index < values.Count; index++)
            converted[index] = values[index];
        return converted;
    }
}
