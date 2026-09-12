using Conoscope.Core;
using Newtonsoft.Json;

namespace Conoscope.Tests;

public class ConoscopeExportSettingsTests
{
    [Fact]
    public void LegacyFineStepsAreNormalizedWithoutLosingOtherOptions()
    {
        var config = JsonConvert.DeserializeObject<ConoscopeConfig>("""
            {"CurrentCurveExportStepDegrees":0.01,"AdvancedExport":{"AzimuthStep":0.01,"RadialStep":0.01,
            "PolarStep":0.01,"CircumferentialStep":0.01,"FilePrefix":"lab","DecimalPlaces":6,
            "EnableCrossSection":true,"UseAzimuthCrossSection":false,"CrossSectionPolarAngle":25}}
            """)!;
        Assert.Equal(.1, config.CurrentCurveExportStepDegrees);
        Assert.Equal(.1, config.AdvancedExport.AzimuthStep);
        Assert.Equal(.1, config.AdvancedExport.RadialStep);
        Assert.Equal(.1, config.AdvancedExport.PolarStep);
        Assert.Equal(.1, config.AdvancedExport.CircumferentialStep);
        Assert.Equal("lab", config.AdvancedExport.FilePrefix);
        Assert.Equal(6, config.ExportDecimalPlaces);
        Assert.True(config.AdvancedExport.EnableCrossSection);
        Assert.Equal(CrossSectionType.Polar, config.AdvancedExport.CrossSectionType);
        Assert.Equal(25, config.AdvancedExport.CrossSectionPolarAngle);
        var restored = JsonConvert.DeserializeObject<ConoscopeConfig>(JsonConvert.SerializeObject(config))!;
        Assert.Equal(.1, restored.AdvancedExport.RadialStep);
        Assert.Equal(CrossSectionType.Polar, restored.AdvancedExport.CrossSectionType);
    }

    [Theory]
    [InlineData(double.NaN, 1)]
    [InlineData(double.PositiveInfinity, 1)]
    [InlineData(-1, .1)]
    [InlineData(.1, .1)]
    [InlineData(1, 1)]
    public void SettingsRemainFiniteAndRespectMinimum(double input, double expected)
    {
        var config = new ConoscopeConfig { CurrentCurveExportStepDegrees = input };
        var settings = new AdvancedExportSettings { AzimuthStep = input, RadialStep = input, PolarStep = input, CircumferentialStep = input };
        Assert.Equal(expected, config.CurrentCurveExportStepDegrees);
        Assert.Equal(expected, settings.AzimuthStep);
        Assert.Equal(expected, settings.RadialStep);
        Assert.Equal(expected, settings.PolarStep);
        Assert.Equal(expected, settings.CircumferentialStep);
    }

    [Fact]
    public void Va60MinimumMatrixHasBoundedSampleCount()
    {
        Assert.Equal(2_161_800, ConoscopeExportService.EstimateMatrixSamples(60, .1, .1, false));
        Assert.Equal(2_164_201, ConoscopeExportService.EstimateMatrixSamples(60, .1, .1, true));
    }
}
