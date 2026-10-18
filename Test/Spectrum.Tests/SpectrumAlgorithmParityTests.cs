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
    [InlineData(0.3127, 0.3290)]
    [InlineData(0.3374, 0.6589)]
    [InlineData(0.3, 0.6)]
    [InlineData(0.37605506, 0.24452225)]
    [InlineData(0.8, 0.2)]
    [InlineData(double.NaN, 0.3)]
    public void ColourimetryCompatibilityEntryPointsUseSharedResults(double x, double y)
    {
        var result = cvColorVision.SpectrumChromaticity.Calculate(x, y);
        double wavelength = result.DominantWavelength ?? -result.ComplementaryWavelength ?? double.NaN;
        AssertColorimetryPair(result.ColorTemperature ?? double.NaN, EngineColorimetryHelper.CalculateCCT(x, y), PluginColorimetryHelper.CalculateCCT(x, y), CctTolerance);
        AssertColorimetryPair(wavelength, EngineColorimetryHelper.CalculateDominantWavelength(x,y), PluginColorimetryHelper.CalculateDominantWavelength(x,y), RoundedColorimetryTolerance);
        AssertColorimetryPair(result.ExcitationPurity ?? double.NaN, EngineColorimetryHelper.CalculateExcitationPurity(x,y,wavelength), PluginColorimetryHelper.CalculateExcitationPurity(x,y,wavelength), RoundedColorimetryTolerance);
    }

    [Fact]
    public void RaCompatibilityEntryPointsUseSpectrumDerivedColorTemperature()
    {
        float[] spectrum = ((SpectrumColorRenderingTests.Fixture)SpectrumColorRenderingTests.ReferenceSpectra().First()[0]).Spectrum;
        float expected = (float)cvColorVision.SpectrumColorRendering.Calculate(spectrum, 380, 1).Ra!.Value;
        // The legacy CCT argument cannot select an incorrect CRI reference illuminant.
        Assert.Equal(expected, EngineRaCalculator.ComputeRa(spectrum, 380, 1, 1000));
        Assert.Equal(expected, PluginRaCalculator.ComputeRa(spectrum, 380, 1, 25000));
        Assert.True(float.IsNaN(EngineRaCalculator.ComputeRa([], 380, 1, 6500)));
        Assert.True(float.IsNaN(PluginRaCalculator.ComputeRa([], 380, 1, 6500)));
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
