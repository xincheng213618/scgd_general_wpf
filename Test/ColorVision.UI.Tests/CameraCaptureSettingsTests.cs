using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.AutoExpTime;
using ColorVision.ImageEditor.Realtime;
using FlowEngineLib.Algorithm;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ColorVision.UI.Tests;

public class CameraCaptureSettingsTests
{
    [Fact]
    public void V1AndV2ExposureTemplatesReachNativeConfigurationWithoutLosingV2Options()
    {
        var v1 = new AutoExpTimeParam { Id = 8, autoExpSaturation = 83, autoExpTimeBegin = 17, minExpTime = 0.5, maxExpTime = 1200 };
        string snapshot = LocalAutoExposureTemplate.BuildConfiguration(v1, new PhyExpTimeCfg());
        v1.autoExpSaturation = 60;
        JObject values = JObject.Parse(snapshot);
        Assert.Equal(83, values["expTimeCfg"]!["autoExpSaturation"]);
        Assert.Equal(17, values["expTimeCfg"]!["autoExpTimeBegin"]);
        values["expTimeCfg"]!["type"] = 1;
        values["expTimeCfg"]!["RoiMarginRatio"] = new JObject { ["margin_RatioTop"] = 0.2, ["margin_RatioLeft"] = 0.1 };
        var v2 = new TemplateJsonParam { Id = 9, JsonValue = values.ToString() };
        JObject roundTrip = JObject.Parse(LocalAutoExposureTemplate.BuildConfiguration(v2, new PhyExpTimeCfg()));
        Assert.True(JToken.DeepEquals(values, roundTrip));
    }

    [Fact]
    public void LocalDefaultsAreExplicitAndMalformedTemplateCannotSilentlyUsePreviousSettings()
    {
        var defaults = new PhyExpTimeCfg { AutoExpSaturation = 65, MaxExpTime = 800, AutoExpSyncFreq = 75 };
        JObject configuration = JObject.Parse(LocalAutoExposureTemplate.BuildFlowConfiguration(null, defaults));
        Assert.Equal(65, configuration["expTimeCfg"]!["autoExpSaturation"]);
        Assert.Equal(800, configuration["expTimeCfg"]!["maxExpTime"]);
        Assert.Equal(75, configuration["expTimeCfg"]!["autoExpSyncFreq"]);
        Assert.Throws<InvalidOperationException>(() => LocalAutoExposureTemplate.BuildFlowConfiguration("missing-exposure", defaults));
        var invalid = new TemplateJsonParam { Id = 10, JsonValue = "{\"expTimeCfg\":{\"minExpTime\":1}}" };
        Assert.Throws<InvalidOperationException>(() => LocalAutoExposureTemplate.BuildConfiguration(invalid, defaults));
        Assert.Throws<InvalidOperationException>(() => LocalAutoExposureTemplate.BuildConfiguration(new ParamBase { Id = -1 }, defaults));
    }

    [Fact]
    public void FlowExposureTemplateLookupPrefersV2AndPreservesV1Frequency()
    {
        StaTest.Run(() =>
        {
            const string name = "flow-exposure-test";
            var defaults = new PhyExpTimeCfg();
            var v1 = new TemplateModel<AutoExpTimeParam>(name, new AutoExpTimeParam { Name = name, autoExpSyncFreq = 60 });
            JObject settings = JObject.Parse(LocalAutoExposureTemplate.BuildConfiguration(v1.Value, defaults));
            settings["expTimeCfg"]!["autoExpSyncFreq"] = 90;
            settings["expTimeCfg"]!["type"] = 1;
            var v2 = new TemplateModel<TemplateJsonParam>(name, new TemplateJsonParam { Name = name, JsonValue = settings.ToString() });
            TemplateAutoExpTime.Params.Add(v1);
            try
            {
                JObject v1Snapshot = JObject.Parse(LocalAutoExposureTemplate.BuildFlowConfiguration(name, defaults));
                Assert.Equal(60, v1Snapshot["expTimeCfg"]!["autoExpSyncFreq"]);
                TemplateAutoExpTimeV2.Params.Add(v2);
                Assert.True(JToken.DeepEquals(settings, JObject.Parse(LocalAutoExposureTemplate.BuildFlowConfiguration(name, defaults))));
            }
            finally
            {
                TemplateAutoExpTimeV2.Params.Remove(v2);
                TemplateAutoExpTime.Params.Remove(v1);
            }
        });
    }

    [Theory]
    [InlineData(CVImageFlipMode.None, RealtimeFramePresenter.TransformNone)]
    [InlineData(CVImageFlipMode.X, RealtimeFramePresenter.TransformFlipY)]
    [InlineData(CVImageFlipMode.Y, RealtimeFramePresenter.TransformFlipX)]
    [InlineData(CVImageFlipMode.XY, RealtimeFramePresenter.TransformFlipXY)]
    public void OneFlipSettingKeepsOpenCvAndPreviewPixelDirectionsAligned(CVImageFlipMode flip, int preview)
    {
        var settings = new DisplayCameraConfig { FlipMode = flip };
        Assert.Equal(preview, settings.LocalVideoTransform);
        JObject saved = JObject.FromObject(settings);
        Assert.Null(saved["LocalVideoTransform"]);
        Assert.Equal(preview, saved.ToObject<DisplayCameraConfig>()!.LocalVideoTransform);
    }

    [Fact]
    public void ExistingMeasurementFlipWinsRegardlessOfLegacyPropertyOrder()
    {
        var oldPreviewOnly = JsonConvert.DeserializeObject<DisplayCameraConfig>("{\"LocalVideoTransform\":1}")!;
        Assert.Equal(CVImageFlipMode.Y, oldPreviewOnly.FlipMode);
        foreach (string json in new[] { "{\"FlipMode\":0,\"LocalVideoTransform\":1}", "{\"LocalVideoTransform\":1,\"FlipMode\":0}" })
        {
            var settings = JsonConvert.DeserializeObject<DisplayCameraConfig>(json)!;
            Assert.Equal(CVImageFlipMode.X, settings.FlipMode);
            Assert.Equal(RealtimeFramePresenter.TransformFlipY, settings.LocalVideoTransform);
        }
    }
}
