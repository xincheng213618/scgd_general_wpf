using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace ColorVision.Engine.Media;

public sealed class CvcieTemplateField(string key, string label, string symbol, string unit, int group) : ViewModelBase
{
    public string Key { get; } = key;
    public string Label { get; } = label;
    public string Symbol { get; } = symbol;
    public string Unit { get; } = unit;
    public int Group { get; } = group;
    private bool enabled;
    public bool Enabled { get => enabled; set { if (enabled == value) return; enabled = value; OnPropertyChanged(); } }
    private int decimalPlaces = 3;
    public int DecimalPlaces { get => decimalPlaces; set { if (value < 0 || value > 9 || decimalPlaces == value) return; decimalPlaces = value; OnPropertyChanged(); } }
}

/// <summary>Edits a local draft; opening or cancelling never rewrites a saved template.</summary>
public sealed class CvcieTemplateDraft : ViewModelBase
{
    public IReadOnlyList<CvcieTemplateField> Fields { get; } = new CvcieTemplateField[]
    {
        new("X", "CIE X · 三刺激值", "X", "", 0), new("Y", "CIE Y · 亮度", "Y", "", 0), new("Z", "CIE Z · 三刺激值", "Z", "", 0),
        new("x", "CIE x · 色度坐标", "x", "", 1), new("y", "CIE y · 色度坐标", "y", "", 1),
        new("u", "CIE u′ · 色度坐标", "u′", "", 2), new("v", "CIE v′ · 色度坐标", "v′", "", 2),
        new("CCT", "CCT · 相关色温", "CCT", " K", 3), new("Wave", "λd · 主波长", "λd", " nm", 3)
    };
    public static IReadOnlyList<int> PrecisionChoices { get; } = Enumerable.Range(0, 10).ToArray();
    private bool loading;
    private string template;
    public string Template => template;
    public string AdvancedText
    {
        get => template.Replace("\\n", Environment.NewLine);
        set { Load(value.ReplaceLineEndings("\\n")); }
    }
    public string Preview { get; private set; } = "";
    public string? Error { get; private set; }
    public bool CanApply => Error == null;
    public bool HasCustomLayout => template != BuildTemplate();
    public string Summary => string.Join("、", Fields.Where(f => f.Enabled).Select(f => f.Symbol));

    public CvcieTemplateDraft(string template)
    {
        this.template = template;
        Load(template);
        foreach (var field in Fields) field.PropertyChanged += FieldChanged;
    }

    private void Load(string value)
    {
        loading = true;
        template = value;
        foreach (var field in Fields)
        {
            Match match = Regex.Match(value, "@" + field.Key + @":F(\d+)(?!\w)");
            field.Enabled = match.Success;
            field.DecimalPlaces = match.Success && int.TryParse(match.Groups[1].Value, out int digits) && digits <= 9 ? digits : 3;
        }
        loading = false;
        Refresh();
    }

    private void FieldChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (loading) return;
        template = BuildTemplate();
        Refresh();
    }

    private string BuildTemplate() => string.Join("\\n", Fields.Where(f => f.Enabled).GroupBy(f => f.Group)
        .Select(group => string.Join("  ", group.Select(f => $"{f.Symbol}:@{f.Key}:F{f.DecimalPlaces}{f.Unit}"))));

    private void Refresh()
    {
        try
        {
            Preview = CVRawOpen.FormatMessage(template, new PoiResultCIExyuvData
            { X = 95.047, Y = 100, Z = 108.883, x = 0.3127, y = 0.3290, u = 0.1978, v = 0.4683, CCT = 6504, Wave = 555.25 });
            Error = null;
        }
        catch (FormatException)
        {
            Preview = "";
            Error = "格式无效，请检查高级模板中的小数位格式（例如 @Y:F1）。";
        }
        OnPropertyChanged(nameof(Template));
        OnPropertyChanged(nameof(AdvancedText));
        OnPropertyChanged(nameof(Preview));
        OnPropertyChanged(nameof(Error));
        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(HasCustomLayout));
        OnPropertyChanged(nameof(Summary));
    }
}
