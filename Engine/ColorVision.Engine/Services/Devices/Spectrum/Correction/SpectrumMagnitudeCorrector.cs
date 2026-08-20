using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.Spectrum.Correction;

public enum SpectrumCorrectionMode
{
    FullSpectrum,
    BrightnessOnly
}

public sealed class SpectrumCorrectionOptions
{
    public double MeasuredEpsilon { get; init; } = 1e-12;
    public double RelativeMeasuredThreshold { get; init; } = 1e-4;
    public double MinimumCorrectionFactor { get; init; }
    public double MaximumCorrectionFactor { get; init; } = double.PositiveInfinity;
}

public sealed class SpectrumCorrectionResult
{
    internal SpectrumCorrectionResult(
        SpectrumCorrectionMode mode,
        MagnitudeCalibrationFile correctedFile,
        double[] correctionFactors,
        double[] measuredValues,
        double[] standardValues,
        int filledFactorCount = 0)
    {
        Mode = mode;
        CorrectedFile = correctedFile;
        CorrectionFactors = Array.AsReadOnly(correctionFactors);
        MeasuredValues = Array.AsReadOnly(measuredValues);
        StandardValues = Array.AsReadOnly(standardValues);
        FilledFactorCount = filledFactorCount;
    }

    public SpectrumCorrectionMode Mode { get; }
    public MagnitudeCalibrationFile CorrectedFile { get; }
    public ReadOnlyCollection<double> CorrectionFactors { get; }
    public ReadOnlyCollection<double> MeasuredValues { get; }
    public ReadOnlyCollection<double> StandardValues { get; }
    /// <summary>
    /// Number of low/zero measured points whose correction factor was filled from
    /// neighboring valid factors instead of dividing by an unstable value.
    /// </summary>
    public int FilledFactorCount { get; }
    public double? UniformCorrectionFactor => Mode == SpectrumCorrectionMode.BrightnessOnly ? CorrectionFactors[0] : null;
}

public static class SpectrumMagnitudeCorrector
{
    public static SpectrumCorrectionResult CorrectFullSpectrum(
        MagnitudeCalibrationFile currentFile,
        ServiceSpectrumMeasurement measuredSpectrum,
        SpectrumSeries standardSpectrum,
        SpectrumCorrectionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(currentFile);
        ArgumentNullException.ThrowIfNull(measuredSpectrum);
        ArgumentNullException.ThrowIfNull(standardSpectrum);
        SpectrumCorrectionOptions validatedOptions = ValidateOptions(options);

        var targetWavelengths = currentFile.Wavelengths;
        SpectrumSeries measuredAbsoluteSpectrum = measuredSpectrum.ToAbsoluteSpectrum();
        measuredAbsoluteSpectrum.EnsureCovers(targetWavelengths, nameof(measuredSpectrum));
        standardSpectrum.EnsureCovers(targetWavelengths, nameof(standardSpectrum));

        double[] measuredValues = new double[currentFile.Count];
        double[] standardValues = new double[currentFile.Count];
        double[] factors = new double[currentFile.Count];
        double[] correctedCoefficients = new double[currentFile.Count];
        bool[] hasMeasuredFactor = new bool[currentFile.Count];
        List<int> validFactorIndices = [];
        double peakMeasuredValue = 0;

        for (int index = 0; index < currentFile.Count; index++)
        {
            double wavelength = targetWavelengths[index];
            double measured = measuredAbsoluteSpectrum.InterpolateLinear(wavelength);
            double standard = standardSpectrum.InterpolateLinear(wavelength);
            measuredValues[index] = measured;
            standardValues[index] = standard;
            peakMeasuredValue = Math.Max(peakMeasuredValue, measured);
        }

        double effectiveMeasuredThreshold = Math.Max(
            validatedOptions.MeasuredEpsilon,
            peakMeasuredValue * validatedOptions.RelativeMeasuredThreshold);

        for (int index = 0; index < currentFile.Count; index++)
        {
            double wavelength = targetWavelengths[index];
            double measured = measuredValues[index];
            if (measured > effectiveMeasuredThreshold)
            {
                double factor = standardValues[index] / measured;
                ValidateFactor(factor, wavelength, validatedOptions);
                factors[index] = factor;
                hasMeasuredFactor[index] = true;
                validFactorIndices.Add(index);
            }
        }

        if (validFactorIndices.Count < 2)
        {
            throw new InvalidOperationException(
                $"Only {validFactorIndices.Count} measured spectrum point(s) are greater than the effective low-signal threshold {effectiveMeasuredThreshold:G17}; at least two are required.");
        }
        FillMissingFactors(targetWavelengths, factors, hasMeasuredFactor, validFactorIndices);

        int filledFactorCount = currentFile.Count - validFactorIndices.Count;
        for (int index = 0; index < currentFile.Count; index++)
        {
            double wavelength = targetWavelengths[index];
            double factor = factors[index];
            ValidateFactor(factor, wavelength, validatedOptions);

            double correctedCoefficient = currentFile.Coefficients[index] * factor;
            if (!double.IsFinite(correctedCoefficient))
                throw new InvalidOperationException($"Corrected coefficient at {wavelength:G17} nm is not finite.");

            correctedCoefficients[index] = correctedCoefficient;
        }

        return new SpectrumCorrectionResult(
            SpectrumCorrectionMode.FullSpectrum,
            currentFile.WithCoefficients(correctedCoefficients),
            factors,
            measuredValues,
            standardValues,
            filledFactorCount);
    }

    public static SpectrumCorrectionResult CorrectBrightness(
        MagnitudeCalibrationFile currentFile,
        double targetBrightness,
        double measuredBrightness,
        SpectrumCorrectionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(currentFile);
        SpectrumCorrectionOptions validatedOptions = ValidateOptions(options);
        if (!double.IsFinite(targetBrightness) || targetBrightness <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetBrightness), "Target brightness must be finite and positive.");
        if (!double.IsFinite(measuredBrightness) || measuredBrightness <= validatedOptions.MeasuredEpsilon)
        {
            throw new ArgumentOutOfRangeException(
                nameof(measuredBrightness),
                $"Measured brightness must be finite and greater than epsilon {validatedOptions.MeasuredEpsilon:G17}.");
        }

        double factor = targetBrightness / measuredBrightness;
        ValidateFactor(factor, null, validatedOptions);
        double[] factors = new double[currentFile.Count];
        double[] correctedCoefficients = new double[currentFile.Count];
        for (int index = 0; index < currentFile.Count; index++)
        {
            factors[index] = factor;
            correctedCoefficients[index] = currentFile.Coefficients[index] * factor;
            if (!double.IsFinite(correctedCoefficients[index]))
                throw new InvalidOperationException($"Corrected coefficient at index {index} is not finite.");
        }

        return new SpectrumCorrectionResult(
            SpectrumCorrectionMode.BrightnessOnly,
            currentFile.WithCoefficients(correctedCoefficients),
            factors,
            [measuredBrightness],
            [targetBrightness]);
    }

    private static void FillMissingFactors(
        ReadOnlyCollection<double> wavelengths,
        double[] factors,
        bool[] hasMeasuredFactor,
        List<int> validFactorIndices)
    {
        int firstValidIndex = validFactorIndices[0];
        for (int index = 0; index < firstValidIndex; index++)
            factors[index] = factors[firstValidIndex];

        for (int validIndex = 0; validIndex < validFactorIndices.Count - 1; validIndex++)
        {
            int leftIndex = validFactorIndices[validIndex];
            int rightIndex = validFactorIndices[validIndex + 1];
            if (rightIndex == leftIndex + 1)
                continue;

            double wavelengthRange = wavelengths[rightIndex] - wavelengths[leftIndex];
            for (int index = leftIndex + 1; index < rightIndex; index++)
            {
                if (hasMeasuredFactor[index])
                    continue;

                double position = (wavelengths[index] - wavelengths[leftIndex]) / wavelengthRange;
                factors[index] = factors[leftIndex] + position * (factors[rightIndex] - factors[leftIndex]);
            }
        }

        int lastValidIndex = validFactorIndices[^1];
        for (int index = lastValidIndex + 1; index < factors.Length; index++)
            factors[index] = factors[lastValidIndex];
    }

    private static SpectrumCorrectionOptions ValidateOptions(SpectrumCorrectionOptions? options)
    {
        options ??= new SpectrumCorrectionOptions();
        if (!double.IsFinite(options.MeasuredEpsilon) || options.MeasuredEpsilon < 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Measured epsilon must be finite and non-negative.");
        if (!double.IsFinite(options.RelativeMeasuredThreshold) || options.RelativeMeasuredThreshold < 0 || options.RelativeMeasuredThreshold >= 1)
            throw new ArgumentOutOfRangeException(nameof(options), "Relative measured threshold must be finite and in the range [0, 1).");
        if (!double.IsFinite(options.MinimumCorrectionFactor) || options.MinimumCorrectionFactor < 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Minimum correction factor must be finite and non-negative.");
        if (double.IsNaN(options.MaximumCorrectionFactor) || options.MaximumCorrectionFactor <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum correction factor must be positive.");
        if (options.MinimumCorrectionFactor > options.MaximumCorrectionFactor)
            throw new ArgumentException("Minimum correction factor cannot exceed maximum correction factor.", nameof(options));
        return options;
    }

    private static void ValidateFactor(double factor, double? wavelength, SpectrumCorrectionOptions options)
    {
        string location = wavelength.HasValue ? $" at {wavelength.Value:G17} nm" : string.Empty;
        if (!double.IsFinite(factor) || factor < 0)
            throw new InvalidOperationException($"Correction factor{location} is invalid: {factor:G17}.");
        if (factor < options.MinimumCorrectionFactor)
            throw new InvalidOperationException($"Correction factor{location} ({factor:G17}) is below the configured minimum {options.MinimumCorrectionFactor:G17}.");
        if (factor > options.MaximumCorrectionFactor)
            throw new InvalidOperationException($"Correction factor{location} ({factor:G17}) exceeds the configured maximum {options.MaximumCorrectionFactor:G17}.");
    }
}
