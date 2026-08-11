#pragma warning disable CA1707

using System.Windows.Media;
using EngineColorimetryHelper = global::ColorVision.Engine.Services.Devices.Spectrum.Views.ColorimetryHelper;
using EngineRaCalculator = global::ColorVision.Engine.Services.Devices.Spectrum.Views.RaCalculator;
using EngineWavelengthToColor = global::ColorVision.Engine.Services.Devices.Spectrum.Views.WavelengthToColor;
using PluginColorimetryHelper = global::Spectrum.View.ColorimetryHelper;
using PluginRaCalculator = global::Spectrum.View.RaCalculator;
using PluginWavelengthToColor = global::Spectrum.WavelengthToColor;

namespace Spectrum.Tests;

public class SpectrumAlgorithmParityTests
{
    private const double CctTolerance = 1e-9;
    private const double RoundedColorimetryTolerance = 1e-9;
    private const float RaTolerance = 1e-6f;
    private const double SpectrumStart = 380;
    private const double SpectrumEnd = 780;

    [Theory]
    [InlineData(379.999)]
    [InlineData(380)]
    [InlineData(419.999)]
    [InlineData(420)]
    [InlineData(439.999)]
    [InlineData(440)]
    [InlineData(489.999)]
    [InlineData(490)]
    [InlineData(509.999)]
    [InlineData(510)]
    [InlineData(555)]
    [InlineData(579.999)]
    [InlineData(580)]
    [InlineData(644.999)]
    [InlineData(645)]
    [InlineData(699.999)]
    [InlineData(700)]
    [InlineData(780)]
    [InlineData(780.001)]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.PositiveInfinity)]
    public void WavelengthToColor_PublicContractIsEquivalent(double wavelength)
    {
        Color engineColor = EngineWavelengthToColor.Convert(wavelength);
        Color pluginColor = PluginWavelengthToColor.Convert(wavelength);
        SolidColorBrush engineBrush = EngineWavelengthToColor.ToBrush(wavelength);
        SolidColorBrush pluginBrush = PluginWavelengthToColor.ToBrush(wavelength);

        Assert.Equal(engineColor, pluginColor);
        Assert.Equal(EngineWavelengthToColor.ToHex(wavelength), PluginWavelengthToColor.ToHex(wavelength));
        Assert.Equal(engineColor, engineBrush.Color);
        Assert.Equal(pluginColor, pluginBrush.Color);
        Assert.Equal(engineBrush.Color, pluginBrush.Color);
        Assert.True(engineBrush.IsFrozen);
        Assert.True(pluginBrush.IsFrozen);
    }

    // Captured from both implementations at local develop 0a52af3c1. These lock current
    // behavior for future deduplication; they are not claims of normative CIE accuracy.
    [Theory]
    [InlineData(379, 0x00FFFFFFu, "#FFFFFF")]
    [InlineData(380, 0xFF610061u, "#610061")]
    [InlineData(440, 0xFF0000FFu, "#0000FF")]
    [InlineData(555, 0xFFB3FF00u, "#B3FF00")]
    [InlineData(645, 0xFFFF0000u, "#FF0000")]
    [InlineData(780, 0xFF610000u, "#610000")]
    [InlineData(781, 0x00FFFFFFu, "#FFFFFF")]
    [InlineData(double.NaN, 0x00FFFFFFu, "#FFFFFF")]
    public void WavelengthToColor_CurrentGoldenValuesRemainStable(double wavelength, uint expectedArgb, string expectedHex)
    {
        Color engineColor = EngineWavelengthToColor.Convert(wavelength);
        Color pluginColor = PluginWavelengthToColor.Convert(wavelength);

        Assert.Equal(expectedArgb, PackArgb(engineColor));
        Assert.Equal(expectedArgb, PackArgb(pluginColor));
        Assert.Equal(expectedHex, EngineWavelengthToColor.ToHex(wavelength));
        Assert.Equal(expectedHex, PluginWavelengthToColor.ToHex(wavelength));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0.3127, 0.3290, 6505.080591307478)]
    [InlineData(0.3374, 0.6589, 5442.906899350839)]
    [InlineData(0.3, 0.6, 6068.7268829022805)]
    public void Colorimetry_CctMatchesAndCurrentGoldenValuesRemainStable(double x, double y, double expectedCct)
    {
        double engineCct = EngineColorimetryHelper.CalculateCCT(x, y);
        double pluginCct = PluginColorimetryHelper.CalculateCCT(x, y);

        AssertEquivalent(engineCct, pluginCct, CctTolerance);
        AssertEquivalent(expectedCct, engineCct, CctTolerance);
        AssertEquivalent(expectedCct, pluginCct, CctTolerance);
    }

    [Fact]
    public void Colorimetry_CctPreservesNaNAndSingularityBehavior()
    {
        AssertColorimetryPair(double.NaN, EngineColorimetryHelper.CalculateCCT(double.NaN, 0.3290), PluginColorimetryHelper.CalculateCCT(double.NaN, 0.3290), CctTolerance);
        AssertColorimetryPair(double.NaN, EngineColorimetryHelper.CalculateCCT(0.3320, 0.1858), PluginColorimetryHelper.CalculateCCT(0.3320, 0.1858), CctTolerance);
        AssertColorimetryPair(double.PositiveInfinity, EngineColorimetryHelper.CalculateCCT(0.4, 0.1858), PluginColorimetryHelper.CalculateCCT(0.4, 0.1858), CctTolerance);
    }

    [Theory]
    [InlineData(0.1741, 0.0050, 380)]
    [InlineData(0.1644, 0.0109, 440)]
    [InlineData(0.3374, 0.6589, 555)]
    [InlineData(0.7230, 0.2770, 645)]
    // The locus table repeats one coordinate from 700 through 780, so the current first match is 700.
    [InlineData(0.7347, 0.2653, 700)]
    [InlineData(0.3, 0.6, 549.14)]
    [InlineData(0.4544, 0.13515, -1)]
    [InlineData(0.3127, 0.3290, -1)]
    [InlineData(double.NaN, 0.3, -1)]
    public void Colorimetry_DominantWavelengthMatchesAndCurrentGoldenValuesRemainStable(double x, double y, double expectedWavelength)
    {
        double engineWavelength = EngineColorimetryHelper.CalculateDominantWavelength(x, y);
        double pluginWavelength = PluginColorimetryHelper.CalculateDominantWavelength(x, y);

        AssertEquivalent(engineWavelength, pluginWavelength, RoundedColorimetryTolerance);
        AssertEquivalent(expectedWavelength, engineWavelength, RoundedColorimetryTolerance);
        AssertEquivalent(expectedWavelength, pluginWavelength, RoundedColorimetryTolerance);
    }

    [Theory]
    [InlineData(0.3374, 0.6589, 555, 1)]
    [InlineData(0.32505, 0.49395, 555, 0.5)]
    [InlineData(0.3127, 0.3290, 555, 0)]
    [InlineData(0.4544, 0.13515, -1, 0)]
    [InlineData(double.NaN, 0.6589, 555, 0)]
    [InlineData(0.3374, double.NaN, 555, 0)]
    [InlineData(0.3374, 0.6589, 379, 0)]
    [InlineData(0.3374, 0.6589, 781, 0)]
    // NaN is not rejected by the wavelength range checks and currently falls back to the 780 locus point.
    [InlineData(0.3374, 0.6589, double.NaN, 0.7752)]
    public void Colorimetry_ExcitationPurityMatchesAndCurrentGoldenValuesRemainStable(double x, double y, double wavelength, double expectedPurity)
    {
        double enginePurity = EngineColorimetryHelper.CalculateExcitationPurity(x, y, wavelength);
        double pluginPurity = PluginColorimetryHelper.CalculateExcitationPurity(x, y, wavelength);

        AssertEquivalent(enginePurity, pluginPurity, RoundedColorimetryTolerance);
        AssertEquivalent(expectedPurity, enginePurity, RoundedColorimetryTolerance);
        AssertEquivalent(expectedPurity, pluginPurity, RoundedColorimetryTolerance);
    }

    // ColorimetryHelper has no SPD-to-xy entry point. Synthetic SPDs therefore exercise
    // RaCalculator, while the public colorimetry contract is covered above with fixed xy inputs.
    [Theory]
    [InlineData("flat", 0.1f, 43.0f)]
    [InlineData("flat", 1f, 43.0f)]
    [InlineData("narrow", 0.1f, 0.0f)]
    [InlineData("narrow", 1f, 0.0f)]
    [InlineData("modulated-planck", 0.1f, 93.1f)]
    [InlineData("modulated-planck", 1f, 93.1f)]
    public void Ra_RepresentativeSpdsMatchAndCurrentGoldenValuesRemainStable(string shape, float interval, float expectedRa)
    {
        float[] spd = CreateSpd(shape, interval);
        float engineRa = EngineRaCalculator.ComputeRa(spd, (float)SpectrumStart, interval, 6500);
        float pluginRa = PluginRaCalculator.ComputeRa(spd, (float)SpectrumStart, interval, 6500);

        Assert.Equal(interval == 0.1f ? 4001 : 401, spd.Length);
        AssertEquivalent(engineRa, pluginRa, RaTolerance);
        AssertEquivalent(expectedRa, engineRa, RaTolerance);
        AssertEquivalent(expectedRa, pluginRa, RaTolerance);
    }

    [Fact]
    public void Ra_InvalidInputsMatchCurrentBehavior()
    {
        float[] flatSpd = CreateSpd("flat", 1f);

        AssertRaPair(null, 1f, 6500, 0);
        AssertRaPair(Array.Empty<float>(), 1f, 6500, 0);
        AssertRaPair(flatSpd, 1f, 999, 0);
        AssertRaPair(flatSpd, 1f, 25001, 0);
        AssertRaPair(flatSpd, 0, 6500, 0);
        AssertRaPair(flatSpd, 1f, float.NaN, float.NaN);
        AssertRaPair(flatSpd, float.NaN, 6500, float.NaN);
    }

    private static float[] CreateSpd(string shape, float interval)
    {
        int count = checked((int)Math.Round((SpectrumEnd - SpectrumStart) / interval) + 1);
        float[] spd = new float[count];

        for (int i = 0; i < count; i++)
        {
            double wavelength = SpectrumStart + i * interval;
            spd[i] = shape switch
            {
                "flat" => 1f,
                "narrow" => (float)Math.Exp(-0.5 * Math.Pow((wavelength - 555) / 5, 2)),
                "modulated-planck" => (float)(PlanckianSpd(wavelength, 6500) * (1 + 0.1 * Math.Sin((wavelength - SpectrumStart) * Math.PI / 45))),
                _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, null)
            };
        }

        return spd;
    }

    private static double PlanckianSpd(double wavelength, double temperature)
    {
        double lambda = wavelength * 1e-9;
        const double c1 = 3.7418e-16;
        const double c2 = 1.4388e-2;
        return c1 / (Math.Pow(lambda, 5) * (Math.Exp(c2 / (lambda * temperature)) - 1));
    }

    private static void AssertRaPair(float[]? spd, float interval, float cct, float expected)
    {
        float engineRa = EngineRaCalculator.ComputeRa(spd!, (float)SpectrumStart, interval, cct);
        float pluginRa = PluginRaCalculator.ComputeRa(spd!, (float)SpectrumStart, interval, cct);

        AssertEquivalent(engineRa, pluginRa, RaTolerance);
        AssertEquivalent(expected, engineRa, RaTolerance);
        AssertEquivalent(expected, pluginRa, RaTolerance);
    }

    private static void AssertColorimetryPair(double expected, double engineValue, double pluginValue, double tolerance)
    {
        AssertEquivalent(engineValue, pluginValue, tolerance);
        AssertEquivalent(expected, engineValue, tolerance);
        AssertEquivalent(expected, pluginValue, tolerance);
    }

    private static void AssertEquivalent(double expected, double actual, double tolerance)
    {
        if (double.IsNaN(expected))
        {
            Assert.True(double.IsNaN(actual));
            return;
        }

        if (double.IsInfinity(expected))
        {
            Assert.Equal(expected, actual);
            return;
        }

        Assert.InRange(Math.Abs(expected - actual), 0, tolerance);
    }

    private static void AssertEquivalent(float expected, float actual, float tolerance)
    {
        if (float.IsNaN(expected))
        {
            Assert.True(float.IsNaN(actual));
            return;
        }

        if (float.IsInfinity(expected))
        {
            Assert.Equal(expected, actual);
            return;
        }

        Assert.InRange(Math.Abs(expected - actual), 0, tolerance);
    }

    private static uint PackArgb(Color color) =>
        ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
}
