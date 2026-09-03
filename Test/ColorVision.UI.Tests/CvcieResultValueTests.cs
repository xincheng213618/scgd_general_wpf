using ColorVision.Engine.Media;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.UI;
using Newtonsoft.Json;

namespace ColorVision.UI.Tests;

public sealed class CvcieResultValueTests
{
    [Fact]
    public void LegacyConfigurationKeepsDefaultReplacementAndNewSettingsRoundTrip()
    {
        CVCIEShowConfig config = JsonConvert.DeserializeObject<CVCIEShowConfig>("{\"IsShowString\":false}")!;
        Assert.True(config.ClampNonPositiveValues);
        Assert.Equal(0.0001, config.MinimumValue);
        Func<double, double> normalize = config.CreateValueNormalizer();
        Assert.Equal(0.0001, normalize(-2));
        Assert.Equal(0.0001, normalize(0));
        Assert.Equal(0.00001, normalize(0.00001));

        config.ClampNonPositiveValues = false;
        config.MinimumValue = 0.002;
        CVCIEShowConfig restored = JsonConvert.DeserializeObject<CVCIEShowConfig>(JsonConvert.SerializeObject(config))!;
        Assert.False(restored.ClampNonPositiveValues);
        Assert.Equal(0.002, restored.MinimumValue);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void MinimumValueRejectsInvalidNumbers(double value)
    {
        CVCIEShowConfig config = new() { MinimumValue = 0.002 };
        config.MinimumValue = value;
        Assert.Equal(0.002, config.MinimumValue);
        config.MinimumValue = 0;
        Assert.Equal(0, config.MinimumValue);
    }

    [Fact]
    public void ACalculationUsesCapturedSettingsAndTheNextCalculationUsesNewSettings()
    {
        CVCIEShowConfig config = new() { MinimumValue = 0.002 };
        Func<double, double> firstCalculation = config.CreateValueNormalizer();
        config.MinimumValue = 0.003;
        Assert.Equal(0.002, firstCalculation(-1));
        Assert.Equal(0.003, config.CreateValueNormalizer()(-1));
        config.ClampNonPositiveValues = false;
        Func<double, double> nextCalculation = config.CreateValueNormalizer();
        Assert.Equal(-1, nextCalculation(-1));
        Assert.Equal(0, nextCalculation(0));
        Assert.True(double.IsNaN(nextCalculation(double.NaN)));
        Assert.Equal(-1, config.CreateValueNormalizer()(-1));
        Assert.Equal(0.002, firstCalculation(-1));
    }

    [Fact]
    public void ColorAndLuminanceResultsKeepTheirValuesUntilNewResultsAreCreated()
    {
        WpfTestHost.Invoke(() =>
        {
            IConfigService? previous = ConfigService.Instance;
            try
            {
                ConfigService.SetInstance(new ConfigHandler());
                CVCIEShowConfig config = CVCIEShowConfig.Instance;
                config.MinimumValue = 0.002;
                const string raw = "{\"X\":-1,\"Y\":-2,\"Z\":0,\"x\":-0.25,\"y\":0.5,\"u\":0,\"v\":-0.5,\"CCT\":-100,\"Wave\":-20}";
                PoiPointResultModel model = new() { Value = raw };
                PoiPointResultModel luminanceModel = new() { Value = "{\"Y\":-2}" };
                PoiResultCIExyuvData firstColor = new(model);
                PoiResultCIEYData firstLuminance = new(luminanceModel);
                Assert.Equal(0.002, firstColor.X);
                Assert.Equal(0.002, firstColor.Y);
                Assert.Equal(0.002, firstColor.Z);
                Assert.InRange(firstColor.x, 1.0 / 3 - 1e-7, 1.0 / 3 + 1e-7);
                Assert.InRange(firstColor.y, 1.0 / 3 - 1e-7, 1.0 / 3 + 1e-7);
                Assert.InRange(firstColor.u, 4.0 / 19 - 1e-7, 4.0 / 19 + 1e-7);
                Assert.InRange(firstColor.v, 9.0 / 19 - 1e-7, 9.0 / 19 + 1e-7);
                Assert.Equal(0.002, firstLuminance.Y);
                double n = (firstColor.x - 0.3320) / (0.1858 - firstColor.y);
                double expectedCct = 437 * Math.Pow(n, 3) + 3601 * Math.Pow(n, 2) + 6831 * n + 5517;
                Assert.InRange(firstColor.CCT, expectedCct - 0.001, expectedCct + 0.001);
                Assert.InRange(firstColor.Wave, 380, 780);

                config.ClampNonPositiveValues = false;
                config.MinimumValue = 0.003;
                PoiResultCIExyuvData nextColor = new(model);
                PoiResultCIEYData nextLuminance = new(luminanceModel);
                Assert.Equal(new[] { -1.0, -2.0, 0.0, -0.25, 0.5, 0.0, -0.5 }, Values(nextColor));
                Assert.Equal(-2, nextLuminance.Y);
                Assert.Equal(-100, nextColor.CCT);
                Assert.Equal(-20, nextColor.Wave);
                Assert.Equal(0.002, firstColor.Y);
                Assert.Equal(0.002, firstLuminance.Y);
                Assert.Equal(raw, model.Value);
            }
            finally
            {
                ConfigService.SetInstance(previous!);
            }
        });
    }

    [Fact]
    public void UnchangedXyzKeepsExistingColorMetricsAndZeroReplacementHasNoColor()
    {
        CVCIEShowConfig config = new();
        PoiResultCIExyuvData result = new() { X = 1, Y = 2, Z = 3, x = 0.1, CCT = 5000, Wave = 550 };
        result.NormalizeXyz(config.CreateValueNormalizer());
        Assert.Equal(0.1, result.x);
        Assert.Equal(5000, result.CCT);
        Assert.Equal(550, result.Wave);

        result.X = result.Y = result.Z = -1;
        config.MinimumValue = 0;
        result.NormalizeXyz(config.CreateValueNormalizer());
        Assert.Equal(0, result.X);
        Assert.True(double.IsNaN(result.x));
        Assert.True(double.IsNaN(result.CCT));
        Assert.True(double.IsNaN(result.Wave));
    }

    private static double[] Values(PoiResultCIExyuvData result)
        => [result.X, result.Y, result.Z, result.x, result.y, result.u, result.v];
}
