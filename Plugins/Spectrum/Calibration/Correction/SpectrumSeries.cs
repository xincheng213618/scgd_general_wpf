using System.Collections.ObjectModel;

namespace Spectrum.Calibration.Correction;

/// <summary>
/// A non-negative spectrum on an explicitly defined, strictly increasing wavelength grid.
/// </summary>
public sealed class SpectrumSeries
{
    private const double CoverageTolerance = 1e-7;
    private readonly double[] _wavelengths;
    private readonly double[] _values;

    public SpectrumSeries(IReadOnlyList<double> wavelengths, IReadOnlyList<double> values)
    {
        ArgumentNullException.ThrowIfNull(wavelengths);
        ArgumentNullException.ThrowIfNull(values);
        if (wavelengths.Count != values.Count)
            throw new ArgumentException("Wavelength and spectrum value counts must match.");
        if (wavelengths.Count < 2)
            throw new ArgumentException("At least two spectrum points are required.", nameof(wavelengths));

        _wavelengths = wavelengths.ToArray();
        _values = values.ToArray();
        for (int index = 0; index < _wavelengths.Length; index++)
        {
            if (!double.IsFinite(_wavelengths[index]))
                throw new ArgumentException($"Wavelength at index {index} is not finite.", nameof(wavelengths));
            if (index > 0 && _wavelengths[index] <= _wavelengths[index - 1])
                throw new ArgumentException($"Wavelengths must be strictly increasing; index {index} contains {_wavelengths[index]}.", nameof(wavelengths));

            if (!double.IsFinite(_values[index]))
                throw new ArgumentException($"Spectrum value at index {index} is not finite.", nameof(values));
            if (_values[index] < 0)
                throw new ArgumentException($"Spectrum value at index {index} is negative.", nameof(values));
        }

        Wavelengths = Array.AsReadOnly(_wavelengths);
        Values = Array.AsReadOnly(_values);
    }

    public int Count => _wavelengths.Length;
    public ReadOnlyCollection<double> Wavelengths { get; }
    public ReadOnlyCollection<double> Values { get; }
    public double StartWavelength => _wavelengths[0];
    public double EndWavelength => _wavelengths[^1];

    public void EnsureCovers(IReadOnlyList<double> targetWavelengths, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(targetWavelengths);
        if (targetWavelengths.Count == 0)
            throw new ArgumentException("At least one target wavelength is required.", parameterName);

        double targetStart = targetWavelengths[0];
        double targetEnd = targetWavelengths[^1];
        if (targetStart < StartWavelength - CoverageTolerance || targetEnd > EndWavelength + CoverageTolerance)
        {
            throw new ArgumentException(
                $"Spectrum range {StartWavelength:G17}-{EndWavelength:G17} nm does not cover target range {targetStart:G17}-{targetEnd:G17} nm.",
                parameterName);
        }
    }

    public double InterpolateLinear(double wavelength)
    {
        if (!double.IsFinite(wavelength))
            throw new ArgumentOutOfRangeException(nameof(wavelength), "Interpolation wavelength must be finite.");
        if (wavelength < StartWavelength - CoverageTolerance || wavelength > EndWavelength + CoverageTolerance)
            throw new ArgumentOutOfRangeException(nameof(wavelength), $"Wavelength {wavelength:G17} nm is outside {StartWavelength:G17}-{EndWavelength:G17} nm.");
        if (wavelength <= StartWavelength)
            return _values[0];
        if (wavelength >= EndWavelength)
            return _values[^1];

        int upperIndex = Array.BinarySearch(_wavelengths, wavelength);
        if (upperIndex >= 0)
            return _values[upperIndex];

        upperIndex = ~upperIndex;
        int lowerIndex = upperIndex - 1;
        double position = (wavelength - _wavelengths[lowerIndex]) / (_wavelengths[upperIndex] - _wavelengths[lowerIndex]);
        return _values[lowerIndex] + position * (_values[upperIndex] - _values[lowerIndex]);
    }
}
