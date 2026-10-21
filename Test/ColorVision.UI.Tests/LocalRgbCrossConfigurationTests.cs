using ColorVision.ImageEditor.EditorTools.Algorithms;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.PropertyEditor;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Nodes;

namespace ColorVision.UI.Tests;

public sealed class LocalRgbCrossConfigurationTests
{
    [Fact]
    public void DefaultsMatchMeasurementAndDoNotAddJudgment()
    {
        Assert.True(RgbCrossConfigurationDraft.TryCreate("{}", out var draft, out _));
        Assert.True(draft!.TryGetParameters(out var p, out _));
        Assert.Equal(0.5, p.TargetThreshold);
        Assert.Equal(3, p.Rows); Assert.Equal(3, p.Columns);
        Assert.Null(p.MaximumEdgeSeparationPixels);
        Assert.True(draft.TryGetJson(out var json, out _)); Assert.Equal("{}", json);
        var property = typeof(LocalRgbCrossNode).GetProperty(nameof(LocalRgbCrossNode.ParameterJson))!;
        Assert.Equal(typeof(LocalRgbCrossConfigurationEditor), property.GetCustomAttribute<PropertyEditorTypeAttribute>()!.EditorType);
    }
    [Theory]
    [InlineData("{broken")]
    [InlineData("[]")]
    [InlineData("{\"TemplateName\":\"old\"}")]
    [InlineData("{\"Channel\":2}")]
    [InlineData("{\"MaximumEdgeSeparationPixels\":6}")]
    [InlineData("{\"TargetThreshold\":0.5,\"targetThreshold\":0.6}")]
    [InlineData("{\"TargetThreshold\":\"0.5\"}")]
    public void BrokenOrUnrelatedJsonIsNotSilentlyAccepted(string json)
        => Assert.False(RgbCrossConfigurationDraft.TryCreate(json, out _, out _));
    [Fact]
    public void EditingPreservesOtherSettingsAndInvalidTextCanBeCorrected()
    {
        const string json = "{ \"targetThreshold\":0.65,\"DecodeExponent\":2.2 }";
        Assert.True(RgbCrossConfigurationDraft.TryCreate(json, out var draft, out _));
        Assert.True(draft!.TryGetJson(out var untouched, out _)); Assert.Equal(json, untouched);
        draft.MinimumArmCoverage = "wrong";
        Assert.False(draft.TryGetJson(out _, out _)); Assert.Equal("MinimumArmCoverage", draft.ErrorProperty);
        draft.MinimumArmCoverage = "0.8";
        Assert.True(draft.TryGetJson(out var changed, out _));
        Assert.True(RgbCrossConfigurationDraft.TryCreate(changed, out var restored, out _));
        Assert.True(restored!.TryGetParameters(out var parameters, out _));
        Assert.Equal(0.65, parameters.TargetThreshold); Assert.Equal(2.2, parameters.DecodeExponent);
        Assert.Equal(0.8, parameters.MinimumArmCoverage); Assert.Null(parameters.MaximumEdgeSeparationPixels);
    }
    [Theory]
    [InlineData("0.09")]
    [InlineData("0.91")]
    [InlineData("NaN")]
    public void OutOfRangeValueCannotExecute(string value)
    {
        Assert.True(RgbCrossConfigurationDraft.TryCreate("{}", out var draft, out _));
        draft!.TargetThreshold = value;
        Assert.False(draft.TryGetParameters(out _, out _));
    }
    [Fact]
    public void GridConfigurationRoundTripsAndRejectsFractionalDimensions()
    {
        Assert.True(RgbCrossConfigurationDraft.TryCreate("{\"Rows\":1,\"Columns\":1}", out var draft, out _));
        Assert.True(draft!.TryGetParameters(out var parameters, out _));
        Assert.Equal(1, parameters.Rows); Assert.Equal(1, parameters.Columns);
        draft.Columns = "2";
        Assert.True(draft.TryGetJson(out var json, out _));
        Assert.True(RgbCrossConfigurationDraft.TryCreate(json, out var restored, out _));
        Assert.Equal("2", restored!.Columns);
        draft.Rows = "1.5"; Assert.False(draft.TryGetParameters(out _, out _));
        draft.Rows = "0"; Assert.False(draft.TryGetParameters(out _, out _));
        draft.Rows = "17"; Assert.False(draft.TryGetParameters(out _, out _));
    }

    [Fact]
    public void OneOffJudgmentRoundTripsAndCanBeClearedWithoutChangingMeasurement()
    {
        const string json = "{\"TargetThreshold\":0.65,\"MaximumEdgeSeparationPixels\":null}";
        Assert.True(RgbCrossConfigurationDraft.TryCreate(json, out var draft, out _, includeJudgment: true));
        draft!.MaximumEdgeSeparationPixels = "4.25";
        Assert.True(draft.TryGetJson(out string changed, out _));
        Assert.False(RgbCrossConfigurationDraft.TryCreate(changed, out _, out _));
        Assert.True(RgbCrossConfigurationDraft.TryCreate(changed, out var restored, out _, includeJudgment: true));
        Assert.True(restored!.TryGetParameters(out var p, out _));
        Assert.Equal(4.25, p.MaximumEdgeSeparationPixels); Assert.Equal(0.65, p.TargetThreshold);
        foreach (string value in new[] { "-1", "1001", "NaN", "invalid" })
        {
            restored.MaximumEdgeSeparationPixels = value;
            Assert.False(restored.TryGetJson(out _, out _));
            Assert.Equal("MaximumEdgeSeparationPixels", restored.ErrorProperty);
        }
        restored.MaximumEdgeSeparationPixels = " ";
        Assert.True(restored.TryGetParameters(out p, out _));
        Assert.Null(p.MaximumEdgeSeparationPixels); Assert.Equal(0.65, p.TargetThreshold);
        Assert.True(restored.TryGetJson(out changed, out _));
        Assert.Null(JsonNode.Parse(changed)!["MaximumEdgeSeparationPixels"]);
    }
}
