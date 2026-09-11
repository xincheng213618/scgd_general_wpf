using ColorVision.Engine.Media;
using System.ComponentModel;
using System.Reflection;

namespace ColorVision.UI.Tests;

public sealed class CvcieTemplateEditorTests
{
    [Fact]
    public void OpeningLegacyTemplatePreservesExactTextAndIndependentPrecision()
    {
        const string original = "Y:@Y:F1\\nx:@x:F4 y:@y:F4 \\nCCT:@CCT:F1 Wave:@Wave:F1";
        var draft = new CvcieTemplateDraft(original);
        Assert.Equal(original, draft.Template);
        Assert.True(draft.HasCustomLayout);
        Assert.Equal(1, draft.Fields.Single(f => f.Key == "Y").DecimalPlaces);
        Assert.Equal(4, draft.Fields.Single(f => f.Key == "x").DecimalPlaces);
        Assert.False(draft.Fields.Single(f => f.Key == "X").Enabled);
        Assert.DoesNotContain("@", draft.Preview);
    }

    [Fact]
    public void FieldSelectionGeneratesGroupedTemplateWithoutChangingSourceConfig()
    {
        var source = new CVCIEShowConfig();
        string original = source.Template;
        Assert.Equal("X:@X:F3  Y:@Y:F3  Z:@Z:F3\\nx:@x:F3  y:@y:F3\\nu′:@u:F3  v′:@v:F3\\nCCT:@CCT:F3 K  λd:@Wave:F3 nm", original);
        var draft = new CvcieTemplateDraft(original);
        Assert.False(draft.HasCustomLayout);
        draft.Fields.Single(f => f.Key == "X").Enabled = false;
        draft.Fields.Single(f => f.Key == "Y").DecimalPlaces = 1;
        Assert.DoesNotContain("@X:", draft.Template);
        Assert.Contains("Y:@Y:F1", draft.Template);
        Assert.Contains("x:@x:F3  y:@y:F3", draft.Template);
        Assert.Equal(original, source.Template);
        Assert.False(draft.HasCustomLayout);
    }

    [Fact]
    public void AdvancedEditingRefreshesCheckboxesPreviewAndEscapedNewlines()
    {
        var draft = new CvcieTemplateDraft("");
        draft.AdvancedText = "亮度 @Y:F2\r\n色温 @CCT:F0 K";
        Assert.Equal("亮度 @Y:F2\\n色温 @CCT:F0 K", draft.Template);
        Assert.Equal(new[] { "Y", "CCT" }, draft.Fields.Where(f => f.Enabled).Select(f => f.Key));
        Assert.Equal(0, draft.Fields.Single(f => f.Key == "CCT").DecimalPlaces);
        Assert.Contains(Environment.NewLine, draft.Preview);
        Assert.True(draft.HasCustomLayout);
    }

    [Fact]
    public void AllFieldsMayBeDisabledAndInvalidFormatDoesNotCrashPreview()
    {
        var draft = new CvcieTemplateDraft(new CVCIEShowConfig().Template);
        foreach (var field in draft.Fields) field.Enabled = false;
        Assert.Equal("", draft.Template);
        Assert.True(draft.CanApply);
        draft.AdvancedText = "@Y:F9999999999";
        Assert.False(draft.CanApply);
        Assert.NotNull(draft.Error);
        draft.AdvancedText = "@Y:F3";
        Assert.True(draft.CanApply);
    }

    [Fact]
    public void OnlyTemplateUsesSpecializedEditorAndCommandIsHidden()
    {
        var template = typeof(CVCIEShowConfig).GetProperty(nameof(CVCIEShowConfig.Template))!;
        Assert.Equal(typeof(CvcieTemplatePropertiesEditor), template.GetCustomAttribute<PropertyEditorTypeAttribute>()!.EditorType);
        Assert.False(typeof(CVCIEShowConfig).GetProperty(nameof(CVCIEShowConfig.EditCommand))!.GetCustomAttribute<BrowsableAttribute>()!.Browsable);
    }
}
