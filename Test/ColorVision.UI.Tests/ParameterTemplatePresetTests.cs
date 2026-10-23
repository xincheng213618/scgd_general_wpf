using ColorVision.Engine;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam;
using ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.AutoExpTime;
using ColorVision.Engine.Templates.Jsons.PoiAnalysis;
using ColorVision.Engine.Templates.POI.BuildPoi;
using ColorVision.Themes;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public sealed class ParameterTemplatePresetTests
{
    [Fact]
    public void ReusedBusinessCodesOnlyExposePresetsForTheirStorageFormat()
    {
        var classic = new TemplateBuildPoi().GetCreatePresets().Select(preset => preset.Id).ToArray();
        var json = new ITemplateJson<TemplateJsonParam> { Code = "BuildPOI" }.GetCreatePresets().Select(preset => preset.Id).ToArray();
        Assert.Equal(["poi-quadrilateral-3x3"], classic);
        Assert.Equal(["aa-layout-3x3"], json);
        Assert.Equal(2, new TemplateAutoExpTime().GetCreatePresets().Count());
        Assert.Empty(new TemplateAutoExpTimeV2().GetCreatePresets());
        Assert.Equal(2, new TemplateCameraRunParam().GetCreatePresets().Count());
        Assert.Empty(new ITemplateJson<TemplateJsonParam> { Code = "Camera.RunParams" }.GetCreatePresets());
    }

    [Fact]
    public void DetailPresetsMapByCaseSensitiveSymbolsAndNeverReuseSourceDatabaseIds()
    {
        foreach (string code in new[] { "Camera.RunParams", "auto_exp_time", "BuildPOI", "PoiOutput" })
        foreach (var preset in ParameterTemplatePresets.Read(code, ParameterTemplatePresets.DetailsKind))
        {
            var values = (JArray)JObject.Parse(preset.PayloadJson)["details"]!;
            var definitions = values.Select((value, index) => new SysDictionaryModDetaiModel
                { Id = 10000 + index, PId = 1000, Symbol = (string?)value["symbol"], DefaultValue = "unused" }).Reverse().ToArray();
            var rows = ParameterTemplatePresets.CreateDetails(preset, definitions);
            Assert.Equal(values.Count, rows.Count);
            foreach (var value in values)
            {
                int targetId = definitions.Single(definition => definition.Symbol == (string?)value["symbol"]).Id;
                var row = Assert.Single(rows, row => row.SysPid == targetId);
                Assert.Equal((string?)value["valueA"], row.ValueA);
                Assert.Equal((string?)value["valueB"], row.ValueB);
                Assert.Equal(0, row.Id);
                Assert.Equal(-1, row.Pid);
            }
            Assert.Throws<InvalidDataException>(() => ParameterTemplatePresets.CreateDetails(preset, definitions.Skip(1).ToArray()));
            Assert.All(definitions, definition => Assert.Equal("unused", definition.DefaultValue));
        }
        var xyz = JObject.Parse(ParameterTemplatePresets.Read("PoiOutput", ParameterTemplatePresets.DetailsKind).Single(p => p.Source.Id == "poi-output-xyz").PayloadJson);
        var channels = ((JArray)xyz["details"]!).ToDictionary(value => (string)value["symbol"]!, value => (string?)value["valueA"]);
        Assert.Equal("true", channels["XIsEnable"]);
        Assert.Equal("false", channels["xIsEnable"]);
        Assert.Equal("X.tif", channels["XFileName"]);
        Assert.Equal("x1.tif", channels["xFileName"]);
    }

    [Fact]
    public void ExposurePresetTitlesAgreeWithEveryChannelAndAutoExposureLimits()
    {
        var presets = ParameterTemplatePresets.Read("Camera.RunParams", ParameterTemplatePresets.DetailsKind);
        foreach (var (id, exposure) in new[] { ("camera-exposure-15ms", 15.67), ("camera-exposure-133ms", 133.33) })
        {
            var values = (JArray)JObject.Parse(presets.Single(preset => preset.Source.Id == id).PayloadJson)["details"]!;
            var exposureValues = values.Where(value => ((string)value["symbol"]!).StartsWith("ExpTime")).ToArray();
            Assert.Equal(4, exposureValues.Length);
            Assert.All(exposureValues, value => Assert.Equal(exposure, double.Parse((string)value["valueA"]!, CultureInfo.InvariantCulture)));
        }
        foreach (var (id, max) in new[] { ("camera-auto-exposure-2s", "2000"), ("camera-auto-exposure-60s", "60000") })
        {
            var values = (JArray)JObject.Parse(ParameterTemplatePresets.Read("auto_exp_time", ParameterTemplatePresets.DetailsKind).Single(p => p.Source.Id == id).PayloadJson)["details"]!;
            Assert.Equal(max, (string?)values.Single(value => (string?)value["symbol"] == "maxExpTime")["valueA"]);
        }
    }

    [Theory]
    [InlineData("poi-luminance-uniformity", 1)]
    [InlineData("poi-color-uniformity", 7)]
    [InlineData("poi-checkerboard-white", 0)]
    [InlineData("poi-checkerboard-black", 0)]
    public void JsonPresetsPrepareIndependentDraftsWithoutQueryingMySql(string id, int analysisType)
    {
        var template = new TemplatePoiAnalysis();
        Assert.True(template.ApplyCreatePreset(id));
        var prepared = template.ExportTemp!;
        var first = Assert.IsType<TJPoiAnalysisParam>(template.CreateDefault());
        Assert.Equal(analysisType, (int)JObject.Parse(first.JsonValue)["analysisType"]!);
        Assert.NotSame(prepared.TemplateJsonModel, first.TemplateJsonModel);
        Assert.Equal(-1, first.Id);
        Assert.Equal(template.TemplateDicId, first.TemplateJsonModel.Pid);
        Assert.Equal("PoiAnalysis", first.TemplateJsonModel.Code);
        Assert.Equal(prepared.CreationPreset!.Source.Title, first.Name);
        first.JsonValue = "{\"analysisType\":26}";
        Assert.Equal(analysisType, (int)JObject.Parse(prepared.JsonValue)["analysisType"]!);
        Assert.Equal(analysisType, (int)JObject.Parse(((TJPoiAnalysisParam)template.CreateDefault()).JsonValue)["analysisType"]!);
        template.ClearCreateTemplateSource();
        Assert.Null(template.ExportTemp);
        Assert.Null(template.CreateTemp);
        Assert.Empty(template.ImportName);
    }

    [Fact]
    public void ParameterCatalogContainsGuidanceAndNoAbsolutePathsOrEnabledDebugOutput()
    {
        int count = 0;
        foreach (string code in new[] { "Camera.RunParams", "auto_exp_time", "BuildPOI", "PoiOutput", "PoiAnalysis", "FindLightArea", "MTF" })
        foreach (var preset in BuiltInTemplateCatalog.Read(code))
        {
            count++;
            Assert.False(string.IsNullOrWhiteSpace(preset.Source.Guide?.ModuleGuide));
            Assert.False(string.IsNullOrWhiteSpace(preset.Source.Guide?.DeviceGuide));
            var payload = JObject.Parse(preset.PayloadJson);
            foreach (var token in payload.Descendants().OfType<JValue>().Where(value => value.Type == JTokenType.String))
            {
                string text = (string)token!;
                Assert.DoesNotMatch(@"^[A-Za-z]:[\\/]", text);
                Assert.False(text.StartsWith(@"\\"));
            }
            foreach (var debug in payload.SelectTokens("$..debugCfg.Debug")) Assert.False((bool)debug);
        }
        Assert.Equal(15, count);
    }

    [Fact]
    public void CreationViewDisplaysGuidanceAndChangingPresetDoesNotReuseEditedJson()
    {
        WpfTestHost.Invoke(() =>
        {
            var old = Application.Current.Resources;
            var template = new TemplatePoiAnalysis();
            try
            {
                var resources = new ResourceDictionary();
                foreach (var uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                    resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) });
                Application.Current.Resources = resources;
                Assert.True(template.ApplyCreatePreset("poi-luminance-uniformity"));
                var view = new TemplateCreateView();
                view.Initialize(template, new TemplateCreateOptions { InitialSourceKind = TemplateCreateSourceKind.Preset });
                var guide = (Border)view.FindName("PresetGuide");
                Assert.Equal(Visibility.Visible, guide.Visibility);
                Assert.Contains("POI 分析", Assert.IsType<TemplateCreatePreset>(guide.DataContext).Guide!.ModuleGuide);
                template.CreateTemp!.JsonValue = "{\"analysisType\":26}";
                ((ListBox)view.FindName("SourceListBox")).SelectedIndex = 2;
                Assert.Equal(7, (int)JObject.Parse(template.CreateTemp!.JsonValue)["analysisType"]!);
                view.Discard();
                Assert.Null(template.CreateTemp);
                Assert.Null(template.ExportTemp);
                Assert.Null(guide.DataContext);
            }
            finally { template.ClearCreateTemplateSource(); Application.Current.Resources = old; }
        });
    }
}
