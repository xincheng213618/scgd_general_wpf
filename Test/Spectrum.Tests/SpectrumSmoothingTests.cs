using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using cvColorVision;
using Newtonsoft.Json;
using EngineSettings = ColorVision.Engine.Services.Devices.Spectrum.Configs.GetDataConfig;
using StandaloneSettings = Spectrum.GetDataConfig;

namespace Spectrum.Tests;

// Protect the persisted defaults and native algorithm selection, not timing.
public sealed class SpectrumSmoothingTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(5)]
    public void LegacySettingsKeepMeanAndOriginalWidth(int width)
    {
        string json = JsonConvert.SerializeObject(new { FilterBW = width });
        var main = JsonConvert.DeserializeObject<EngineSettings>(json)!;
        var standalone = JsonConvert.DeserializeObject<StandaloneSettings>(json)!;
        Assert.Equal(SpectrumSmoothingMethod.Mean, main.SmoothingMethod);
        Assert.Equal(SpectrumSmoothingMethod.Mean, standalone.SmoothingMethod);
        Assert.Equal(width, main.FilterBW);
        Assert.Equal(width, standalone.FilterBW);
        SpectrumSmoothing.Validate(main.SmoothingMethod, main.FilterBW);
        SpectrumSmoothing.Validate(standalone.SmoothingMethod, standalone.FilterBW);
    }

    [Fact]
    public void MissingAcquisitionSettingsAndNewSettingsKeepMeanFive()
    {
        var main = JsonConvert.DeserializeObject<ConfigSpectrum>("{}")!.GetDataConfig;
        var standalone = JsonConvert.DeserializeObject<Spectrum.MeasurementDataConfig>("{}")!.GetDataConfig;
        Assert.Equal(SpectrumSmoothingMethod.Mean, main.SmoothingMethod);
        Assert.Equal(SpectrumSmoothingMethod.Mean, standalone.SmoothingMethod);
        Assert.Equal(5, main.FilterBW);
        Assert.Equal(5, standalone.FilterBW);
    }

    [Fact]
    public void SgSelectionRoundTripsInBothClientsAndDraftIsIsolated()
    {
        var config = new ConfigSpectrum();
        var draft = new SpectrumConfigurationDraft(config);
        draft.Config.GetDataConfig.SmoothingMethod = SpectrumSmoothingMethod.SavitzkyGolay;
        draft.Config.GetDataConfig.FilterBW = 7;
        Assert.Equal(SpectrumSmoothingMethod.Mean, config.GetDataConfig.SmoothingMethod);
        Assert.True(draft.TryApply(config, out _));
        var main = JsonConvert.DeserializeObject<ConfigSpectrum>(JsonConvert.SerializeObject(config))!.GetDataConfig;
        var standalone = JsonConvert.DeserializeObject<StandaloneSettings>(JsonConvert.SerializeObject(main))!;
        Assert.Equal(SpectrumSmoothingMethod.SavitzkyGolay, standalone.SmoothingMethod);
        Assert.Equal(7, standalone.FilterBW);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(2047)]
    public void SgAcceptsOddWindows(int width) => SpectrumSmoothing.Validate(SpectrumSmoothingMethod.SavitzkyGolay, width);

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(2049)]
    public void InvalidSgCannotBeSavedByMainConfigurationDraft(int width)
    {
        var draft = new SpectrumConfigurationDraft(new ConfigSpectrum());
        draft.Config.GetDataConfig.SmoothingMethod = SpectrumSmoothingMethod.SavitzkyGolay;
        draft.Config.GetDataConfig.FilterBW = width;
        Assert.False(draft.Prepare(out string error));
        Assert.Contains("SG", error);
    }

    [Fact]
    public void UnknownAlgorithmCannotSilentlyMeasureAsMean() =>
        Assert.Throws<InvalidOperationException>(() => SpectrumSmoothing.Validate((SpectrumSmoothingMethod)2, 5));
}
