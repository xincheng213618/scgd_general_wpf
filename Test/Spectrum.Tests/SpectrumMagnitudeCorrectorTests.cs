#pragma warning disable CA1707, CA1861
using Spectrum.Calibration.Correction;
using System.IO;

namespace Spectrum.Tests;

public class SpectrumMagnitudeCorrectorTests
{
    [Fact]
    public void MagnitudeFile_RoundTripsHeaderWavelengthsAndCoefficientsExactly()
    {
        string path = CreateTemporaryPath();
        try
        {
            MagnitudeCalibrationFile source = CreateMagnitudeFile();
            source.SaveNew(path);

            MagnitudeCalibrationFile loaded = MagnitudeCalibrationFile.Load(path);

            Assert.Equal(72UL, loaded.DataLength);
            Assert.Equal(4.5f, loaded.ExposureTime);
            Assert.Equal(683, loaded.LuminanceCoefficient);
            Assert.Equal([380d, 381d, 382d], loaded.Wavelengths);
            Assert.Equal([10d, 20d, 30d], loaded.Coefficients);
        }
        finally
        {
            DeleteTemporaryFile(path);
        }
    }

    [Fact]
    public void MagnitudeFile_SaveNewRefusesExistingAndSourcePaths()
    {
        string path = CreateTemporaryPath();
        try
        {
            MagnitudeCalibrationFile source = CreateMagnitudeFile();
            source.SaveNew(path);

            Assert.Throws<IOException>(() => source.SaveNew(path));

            MagnitudeCalibrationFile loaded = MagnitudeCalibrationFile.Load(path);
            Assert.Throws<IOException>(() => loaded.SaveNew(path));
        }
        finally
        {
            DeleteTemporaryFile(path);
        }
    }

    [Fact]
    public void MagnitudeFile_LoadRejectsCountThatDoesNotMatchExactLength()
    {
        string path = CreateTemporaryPath();
        try
        {
            using (FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write))
            using (BinaryWriter writer = new(stream))
            {
                writer.Write(40UL);
                writer.Write(4f);
                writer.Write(683);
                writer.Write(3UL);
                writer.Write(380d);
                writer.Write(1d);
            }

            Assert.Throws<InvalidDataException>(() => MagnitudeCalibrationFile.Load(path));
        }
        finally
        {
            DeleteTemporaryFile(path);
        }
    }

    [Fact]
    public void MagnitudeFile_CreateRejectsNegativeCoefficient()
    {
        Assert.Throws<InvalidDataException>(() => MagnitudeCalibrationFile.Create(
            4.5f,
            683,
            [380d, 381d],
            [1d, -1d]));
    }

    [Fact]
    public void BundledMagnitudeFile_LoadsAsInclusiveFourThousandAndOnePointGrid()
    {
        string bundledPath = Path.Combine(AppContext.BaseDirectory, "Magiude.dat");

        MagnitudeCalibrationFile loaded = MagnitudeCalibrationFile.Load(bundledPath);

        Assert.Equal(4001, loaded.Count);
        Assert.Equal(380d, loaded.Wavelengths[0], 10);
        Assert.Equal(780d, loaded.Wavelengths[^1], 10);
        Assert.Equal(64040UL, loaded.DataLength);
    }

    [Fact]
    public void FullSpectrumCorrection_ReconstructsAbsoluteSpectrumAndInterpolatesToDatGrid()
    {
        MagnitudeCalibrationFile current = CreateMagnitudeFile();
        ServiceSpectrumMeasurement measured = new(
            380,
            382,
            1,
            new[] { 0.5, 1.0, 0.5 },
            absoluteScale: 2);
        SpectrumSeries standard = new(
            new[] { 380d, 382d },
            new[] { 2d, 4d });

        SpectrumCorrectionResult result = SpectrumMagnitudeCorrector.CorrectFullSpectrum(current, measured, standard);

        Assert.Equal(SpectrumCorrectionMode.FullSpectrum, result.Mode);
        Assert.Equal([1d, 2d, 1d], result.MeasuredValues);
        Assert.Equal([2d, 3d, 4d], result.StandardValues);
        Assert.Equal([2d, 1.5d, 4d], result.CorrectionFactors);
        Assert.Equal([20d, 30d, 120d], result.CorrectedFile.Coefficients);
        Assert.Equal(current.Wavelengths, result.CorrectedFile.Wavelengths);
        Assert.Equal(current.ExposureTime, result.CorrectedFile.ExposureTime);
        Assert.Equal(current.LuminanceCoefficient, result.CorrectedFile.LuminanceCoefficient);
    }

    [Fact]
    public void FullSpectrumCorrection_UsesFourThousandAndOneInclusiveServicePoints()
    {
        double[] wavelengths = Enumerable.Range(0, 4001).Select(index => 380d + index * 0.1d).ToArray();
        double[] coefficients = Enumerable.Repeat(2d, 4001).ToArray();
        double[] relative = Enumerable.Repeat(0.5d, 4001).ToArray();
        MagnitudeCalibrationFile current = MagnitudeCalibrationFile.Create(4, 683, wavelengths, coefficients);
        ServiceSpectrumMeasurement measured = new(380, 780, 0.1, relative, absoluteScale: 4);
        SpectrumSeries standard = new([380d, 780d], [6d, 6d]);

        SpectrumCorrectionResult result = SpectrumMagnitudeCorrector.CorrectFullSpectrum(current, measured, standard);

        Assert.Equal(4001, result.CorrectedFile.Count);
        Assert.Equal(780d, result.CorrectedFile.Wavelengths[^1], 10);
        Assert.All(result.CorrectionFactors, factor => Assert.Equal(3d, factor, 10));
        Assert.All(result.CorrectedFile.Coefficients, coefficient => Assert.Equal(6d, coefficient, 10));
    }

    [Fact]
    public void BrightnessCorrection_ScalesEveryCoefficientUniformly()
    {
        SpectrumCorrectionResult result = SpectrumMagnitudeCorrector.CorrectBrightness(
            CreateMagnitudeFile(),
            targetBrightness: 150,
            measuredBrightness: 100);

        Assert.Equal(SpectrumCorrectionMode.BrightnessOnly, result.Mode);
        Assert.Equal(1.5d, result.UniformCorrectionFactor);
        Assert.Equal([15d, 30d, 45d], result.CorrectedFile.Coefficients);
    }

    [Fact]
    public void SpectrumSeries_RejectsUnsortedDuplicateAndNonFiniteInput()
    {
        Assert.Throws<ArgumentException>(() => new SpectrumSeries([380d, 379d], [1d, 1d]));
        Assert.Throws<ArgumentException>(() => new SpectrumSeries([380d, 380d], [1d, 1d]));
        Assert.Throws<ArgumentException>(() => new SpectrumSeries([380d, double.NaN], [1d, 1d]));
        Assert.Throws<ArgumentException>(() => new SpectrumSeries([380d, 381d], [1d, double.PositiveInfinity]));
    }

    [Fact]
    public void ServiceMeasurement_RequiresCountToMatchInclusiveMetadata()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            new ServiceSpectrumMeasurement(380, 780, 0.1, new double[4000], absoluteScale: 1));

        Assert.Contains("4001", exception.Message);
    }

    [Fact]
    public void FullSpectrumCorrection_RejectsIncompleteStandardCoverage()
    {
        SpectrumSeries incompleteStandard = new([381d, 382d], [1d, 1d]);
        ServiceSpectrumMeasurement measured = new(380, 382, 1, new[] { 1d, 1d, 1d }, 1);

        Assert.Throws<ArgumentException>(() =>
            SpectrumMagnitudeCorrector.CorrectFullSpectrum(CreateMagnitudeFile(), measured, incompleteStandard));
    }

    [Fact]
    public void FullSpectrumCorrection_FillsMeasuredValuesAtOrBelowEpsilonFromNeighbors()
    {
        MagnitudeCalibrationFile current = MagnitudeCalibrationFile.Create(
            4.5f,
            683,
            [380d, 381d, 382d, 383d, 384d],
            [1d, 1d, 1d, 1d, 1d]);
        ServiceSpectrumMeasurement measured = new(380, 384, 1, new[] { 0d, 1d, 0d, 0.5d, 0d }, 1);
        SpectrumSeries standard = new([380d, 384d], [2d, 2d]);

        SpectrumCorrectionResult result = SpectrumMagnitudeCorrector.CorrectFullSpectrum(
            current,
            measured,
            standard,
            new SpectrumCorrectionOptions { MeasuredEpsilon = 1e-9 });

        Assert.Equal(3, result.FilledFactorCount);
        Assert.Equal([2d, 2d, 3d, 4d, 4d], result.CorrectionFactors);
    }

    [Fact]
    public void FullSpectrumCorrection_RequiresAtLeastTwoValidMeasuredValues()
    {
        ServiceSpectrumMeasurement measured = new(380, 382, 1, new[] { 0d, 1d, 0d }, 1);
        SpectrumSeries standard = new([380d, 382d], [1d, 1d]);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            SpectrumMagnitudeCorrector.CorrectFullSpectrum(CreateMagnitudeFile(), measured, standard));

        Assert.Contains("at least two", exception.Message);
    }

    [Fact]
    public void Correction_RejectsFactorsOutsideConfiguredSafetyBounds()
    {
        ServiceSpectrumMeasurement measured = new(380, 382, 1, new[] { 1d, 1d, 1d }, 1);
        SpectrumSeries standard = new([380d, 382d], [20d, 20d]);
        SpectrumCorrectionOptions options = new() { MinimumCorrectionFactor = 0.1, MaximumCorrectionFactor = 10 };

        Assert.Throws<InvalidOperationException>(() =>
            SpectrumMagnitudeCorrector.CorrectFullSpectrum(CreateMagnitudeFile(), measured, standard, options));
        Assert.Throws<InvalidOperationException>(() =>
            SpectrumMagnitudeCorrector.CorrectBrightness(CreateMagnitudeFile(), 20, 1, options));
    }

    [Fact]
    public void Correction_EnforcesProductionSafetyBoundsByDefault()
    {
        ServiceSpectrumMeasurement measured = new(380, 382, 1, new[] { 1d, 1d, 1d }, 1);
        SpectrumSeries excessiveStandard = new([380d, 382d], [20d, 20d]);

        Assert.Throws<InvalidOperationException>(() =>
            SpectrumMagnitudeCorrector.CorrectFullSpectrum(CreateMagnitudeFile(), measured, excessiveStandard));
        Assert.Throws<InvalidOperationException>(() =>
            SpectrumMagnitudeCorrector.CorrectBrightness(CreateMagnitudeFile(), 20, 1));
        Assert.Throws<InvalidOperationException>(() =>
            SpectrumMagnitudeCorrector.CorrectBrightness(CreateMagnitudeFile(), 0.01, 1));
    }

    [Fact]
    public void FullSpectrumCorrection_TreatsValuesBelowRelativePeakThresholdAsLowSignal()
    {
        MagnitudeCalibrationFile current = MagnitudeCalibrationFile.Create(
            4.5f,
            683,
            [380d, 381d, 382d, 383d, 384d],
            [1d, 1d, 1d, 1d, 1d]);
        ServiceSpectrumMeasurement measured = new(380, 384, 1, new[] { 1d, 1e-5, 1e-5, 1e-5, 1d }, 1);
        SpectrumSeries standard = new([380d, 384d], [1d, 1d]);

        SpectrumCorrectionResult result = SpectrumMagnitudeCorrector.CorrectFullSpectrum(current, measured, standard);

        Assert.Equal(3, result.FilledFactorCount);
        Assert.All(result.CorrectionFactors, factor => Assert.Equal(1d, factor, 10));
    }

    [Fact]
    public void FullSpectrumCorrection_RejectsUsableSignalConfinedToNarrowWavelengthSpan()
    {
        MagnitudeCalibrationFile current = MagnitudeCalibrationFile.Create(
            4.5f,
            683,
            [380d, 381d, 382d, 383d, 384d],
            [1d, 1d, 1d, 1d, 1d]);
        ServiceSpectrumMeasurement measured = new(380, 384, 1, new[] { 1d, 1d, 0d, 0d, 0d }, 1);
        SpectrumSeries standard = new([380d, 384d], [1d, 1d]);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            SpectrumMagnitudeCorrector.CorrectFullSpectrum(current, measured, standard));

        Assert.Contains("span", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FullSpectrumCorrection_RequiresMinimumUsablePointFraction()
    {
        double[] wavelengths = Enumerable.Range(0, 100).Select(index => 380d + index).ToArray();
        double[] measuredValues = new double[100];
        measuredValues[0] = 1;
        measuredValues[^1] = 1;
        MagnitudeCalibrationFile current = MagnitudeCalibrationFile.Create(
            4.5f,
            683,
            wavelengths,
            Enumerable.Repeat(1d, wavelengths.Length).ToArray());
        ServiceSpectrumMeasurement measured = new(380, 479, 1, measuredValues, 1);
        SpectrumSeries standard = new([380d, 479d], [1d, 1d]);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            SpectrumMagnitudeCorrector.CorrectFullSpectrum(current, measured, standard));

        Assert.Contains("at least 10", exception.Message);
    }

    [Theory]
    [InlineData(double.NaN, 1)]
    [InlineData(1, double.NaN)]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void BrightnessCorrection_RejectsInvalidBrightness(double target, double measured)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            SpectrumMagnitudeCorrector.CorrectBrightness(CreateMagnitudeFile(), target, measured));
    }

    private static MagnitudeCalibrationFile CreateMagnitudeFile() =>
        MagnitudeCalibrationFile.Create(
            exposureTime: 4.5f,
            luminanceCoefficient: 683,
            wavelengths: new[] { 380d, 381d, 382d },
            coefficients: new[] { 10d, 20d, 30d });

    private static string CreateTemporaryPath() =>
        Path.Combine(Path.GetTempPath(), $"spectrum-magnitude-{Guid.NewGuid():N}.dat");

    private static void DeleteTemporaryFile(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
