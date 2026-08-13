#pragma warning disable CA1707
using cvColorVision;
using Spectrum.Calibration.Correction;
using Spectrum.Models;
using System.IO;

namespace Spectrum.Tests;

public class SpectrumPluginCorrectionCalculatorTests
{
    [Fact]
    public void FullCorrection_UsesAbsoluteSpectrumAndPreservesDatHeader()
    {
        MagnitudeCalibrationFile source = CreateCanonicalFile(2d);
        ViewResultSpectrum measured = CreateResult(relativeValue: 0.5f, absoluteScale: 4f, brightness: 100f);
        (double Wavelength, double Value)[] standard = [(380d, 3d), (780d, 3d)];

        SpectrumCorrectionOutput output = SpectrumCorrectionCalculator.CorrectSpectrum(source, measured, standard);

        Assert.Equal(source.ExposureTime, output.CorrectedFile.ExposureTime);
        Assert.Equal(source.LuminanceCoefficient, output.CorrectedFile.LuminanceCoefficient);
        Assert.All(output.MeasuredValues, value => Assert.Equal(2d, value, 10));
        Assert.All(output.CorrectionFactors, factor => Assert.Equal(1.5d, factor, 10));
        Assert.All(output.CorrectedFile.Coefficients, coefficient => Assert.Equal(3d, coefficient, 10));
    }

    [Fact]
    public void BrightnessCorrection_ScalesAllCoefficientsWithoutOverwritingSource()
    {
        MagnitudeCalibrationFile source = CreateCanonicalFile(2d);

        MagnitudeCalibrationFile corrected = SpectrumCorrectionCalculator.CorrectBrightness(source, 150, 100);

        Assert.All(corrected.Coefficients, coefficient => Assert.Equal(3d, coefficient, 10));
        Assert.All(source.Coefficients, coefficient => Assert.Equal(2d, coefficient, 10));
    }

    [Fact]
    public void FullCorrection_AllowsLargeFiniteCorrectionFactor()
    {
        MagnitudeCalibrationFile source = CreateCanonicalFile(1d);
        ViewResultSpectrum measured = CreateResult(relativeValue: 1f, absoluteScale: 1f, brightness: 100f);
        (double Wavelength, double Value)[] standard = [(380d, 20d), (780d, 20d)];

        SpectrumCorrectionOutput output = SpectrumCorrectionCalculator.CorrectSpectrum(source, measured, standard);

        Assert.All(output.CorrectionFactors, factor => Assert.Equal(20d, factor, 10));
        Assert.All(output.CorrectedFile.Coefficients, coefficient => Assert.Equal(20d, coefficient, 10));
    }

    private static MagnitudeCalibrationFile CreateCanonicalFile(double coefficient)
    {
        double[] wavelengths = Enumerable.Range(0, 4001).Select(index => 380d + 0.1d * index).ToArray();
        double[] coefficients = Enumerable.Repeat(coefficient, wavelengths.Length).ToArray();
        string path = Path.Combine(Path.GetTempPath(), $"spectrum-plugin-correction-{Guid.NewGuid():N}.dat");
        MagnitudeCalibrationFile.Create(4f, 683, wavelengths, coefficients).SaveNew(path);
        try
        {
            return MagnitudeCalibrationFile.Load(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static ViewResultSpectrum CreateResult(float relativeValue, float absoluteScale, float brightness)
    {
        return new ViewResultSpectrum(new COLOR_PARA
        {
            fSpect1 = 380,
            fSpect2 = 780,
            fInterval = 0.1f,
            fPL = Enumerable.Repeat(relativeValue, 4001).ToArray(),
            fPlambda = absoluteScale,
            fPh = brightness,
            fRi = Array.Empty<float>(),
        });
    }
}
