using ColorVision.Engine.FlowProcessing.Nodes;
using System.Text.Json.Nodes;

namespace ColorVision.UI.Tests;

public sealed class LocalFindCrossConfigurationTests
{
    [Fact]
    public void ExistingJsonIsPreservedExactlyWithoutEditing()
    {
        const string json = "{ \"expectedangledegrees\": 1.25, \"opticsParams\": { \"stdCenter\": {\"x\":4784.5,\"y\":3190.25}, \"focusLength\": 25.4 } }";
        Assert.True(LocalFindCrossConfigurationDraft.TryCreate(json, out var draft, out _));
        Assert.False(draft!.UseImageCenter);
        Assert.Equal("4784.5", draft.CenterX);
        Assert.True(draft.TryGetJson(out var result, out _));
        Assert.Equal(json, result);
    }

    [Fact]
    public void EditingOneValuePreservesCalibrationAndDisabledDistortion()
    {
        const string json = """{"Name":"sample","CalibrationOffset":{"x":0.3,"y":-1.2},"opticsParams":{"stdCenter":{"x":100.25,"y":90.5},"distortion":{"Enabled":false,"K1":0.15,"Fx":1000,"Fy":1001,"Cx":99.5,"Cy":91.25}}}""";
        Assert.True(LocalFindCrossConfigurationDraft.TryCreate(json, out var draft, out _));
        draft!.ExpectedAngle = "2.5";
        Assert.True(draft.TryGetJson(out var result, out var error), error);
        var parsed = JsonNode.Parse(result)!;
        Assert.Equal(0.3, (double)parsed["CalibrationOffset"]!["x"]!);
        Assert.Equal(100.25, (double)parsed["opticsParams"]!["stdCenter"]!["x"]!);
        Assert.Equal(0.15, (double)parsed["opticsParams"]!["distortion"]!["K1"]!);
        Assert.False((bool)parsed["opticsParams"]!["distortion"]!["Enabled"]!);
        Assert.Equal(2.5, (double)parsed["ExpectedAngleDegrees"]!);
    }

    [Theory]
    [InlineData("ExpectedAngle", "181")]
    [InlineData("AngleTolerance", "0")]
    [InlineData("FocusLength", "-1")]
    [InlineData("PixelSize", "NaN")]
    [InlineData("ExpectedAngle", "oops")]
    public void InvalidFieldIsIdentifiedAndDraftIsRetained(string name, string value)
    {
        Assert.True(LocalFindCrossConfigurationDraft.TryCreate("{}", out var draft, out _));
        typeof(LocalFindCrossConfigurationDraft).GetProperty(name)!.SetValue(draft, value);
        Assert.False(draft!.TryGetJson(out _, out _));
        Assert.Equal(name, draft.ErrorProperty);
        Assert.Equal(value, typeof(LocalFindCrossConfigurationDraft).GetProperty(name)!.GetValue(draft));
    }

    [Fact]
    public void OptionalCalibrationSwitchesAndIntrinsicsAreValidated()
    {
        Assert.True(LocalFindCrossConfigurationDraft.TryCreate("{}", out var draft, out _));
        draft!.UseImageCenter = false; draft.CenterX = "100.25"; draft.CenterY = "90.5";
        draft.EnableDistortion = true;
        Assert.False(draft.TryGetJson(out _, out _));
        Assert.Equal("Fx", draft.ErrorProperty);
        draft.Fx = "1000"; draft.Fy = "1000"; draft.Cx = "100"; draft.Cy = "90";
        Assert.True(draft.TryGetJson(out _, out _));
        draft.UseImageCenter = true;
        Assert.True(draft.TryGetJson(out var json, out _));
        Assert.Null(JsonNode.Parse(json)!["opticsParams"]!["stdCenter"]);
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("{\"debugCfg\":{}}")]
    [InlineData("{\"opticsParams\":null}")]
    public void VendorTemplateOrBrokenJsonIsNotSilentlyConverted(string json)
    {
        Assert.False(LocalFindCrossConfigurationDraft.TryCreate(json, out _, out _));
    }
}
