using ColorVision.ImageEditor.Algorithms;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace ColorVision.ImageEditor.EditorTools.Algorithms;

/// <summary>Shared transactional settings for local RGB cross measurement; hosts opt into one-off judgment.</summary>
public sealed class RgbCrossConfigurationDraft
{
    private string originalJson = "{}";
    private string initialState = "";
    private readonly bool includeJudgment;
    private RgbCrossConfigurationDraft(bool includeJudgment)
    {
        this.includeJudgment = includeJudgment;
        var defaults = new RgbCrossRegistrationParameters();
        foreach (string name in Properties)
            GetType().GetProperty(name)!.SetValue(this, Convert.ToString(typeof(RgbCrossRegistrationParameters).GetProperty(name)!.GetValue(defaults), CultureInfo.InvariantCulture)!);
    }
    private string[] ActiveProperties => includeJudgment ? [.. Properties, nameof(MaximumEdgeSeparationPixels)] : Properties;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    internal static readonly string[] Properties =
    ["Rows", "Columns", "TargetThreshold", "MinimumArmSpanFraction", "AxisBandThreshold", "MinimumArmCoverage", "MinimumContrast", "DecodeExponent"];
    [Category("图案布局"), DisplayName("行数"), Description("1 到 16，按图案的实际行数设置。"), PropertyEditorType]
    public string Rows { get; set; } = "";
    [Category("图案布局"), DisplayName("列数"), Description("1 到 16，按图案的实际列数设置。"), PropertyEditorType]
    public string Columns { get; set; } = "";
    [Category("检测参数"), DisplayName("前景阈值比例"), Description("范围 0.1 到 0.9。"), PropertyEditorType]
    public string TargetThreshold { get; set; } = "";
    [Category("检测参数"), DisplayName("最小十字跨度比例"), Description("范围 0.1 到 0.9。"), PropertyEditorType]
    public string MinimumArmSpanFraction { get; set; } = "";
    [Category("检测参数"), DisplayName("轴带支持阈值"), Description("范围 0.1 到 0.9。"), PropertyEditorType]
    public string AxisBandThreshold { get; set; } = "";
    [Category("检测参数"), DisplayName("最小臂截面覆盖率"), Description("范围 0.1 到 1。"), PropertyEditorType]
    public string MinimumArmCoverage { get; set; } = "";
    [Category("检测参数"), DisplayName("最小信号跨度"), Description("范围 0.000001 到 1，按满量程比例设置。"), PropertyEditorType]
    public string MinimumContrast { get; set; } = "";
    [Category("检测参数"), DisplayName("输入解码指数"), Description("范围 0.1 到 5。线性图使用 1，仅在已知编码时修改。"), PropertyEditorType]
    public string DecodeExponent { get; set; } = "";

    [DisplayName("最大边缘分离 (px)"), Description("留空仅测量。"), PropertyEditorType]
    public string MaximumEdgeSeparationPixels { get; set; } = "";

    /// <summary>Only editor-owned settings are copied. RGB measurement always uses all three channels.</summary>
    public static string SerializeParameters(RgbCrossRegistrationParameters parameters, bool includeJudgment = false)
    {
        string[] names = includeJudgment ? [.. Properties, nameof(MaximumEdgeSeparationPixels)] : Properties;
        return JsonSerializer.Serialize(names.ToDictionary(name => name, name => typeof(RgbCrossRegistrationParameters).GetProperty(name)!.GetValue(parameters)), JsonOptions);
    }

    [Browsable(false)] public string ErrorProperty { get; private set; } = "";
    public static bool TryCreate(string json, out RgbCrossConfigurationDraft? draft, out string error, bool includeJudgment = false)
    {
        draft = null;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("配置必须为 JSON 对象。");
            var next = new RgbCrossConfigurationDraft(includeJudgment) { originalJson = json };
            var names = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                string? name = next.ActiveProperties.FirstOrDefault(n => string.Equals(n, property.Name, StringComparison.OrdinalIgnoreCase));
                if (name == null) throw new ArgumentException("不支持的十字参数：" + property.Name);
                if (!names.Add(name)) throw new ArgumentException("配置字段重复：" + property.Name);
                if (name == nameof(MaximumEdgeSeparationPixels) && property.Value.ValueKind == JsonValueKind.Null) continue;
                if (property.Value.ValueKind != JsonValueKind.Number) throw new ArgumentException(property.Name + " 必须是数字。");
                typeof(RgbCrossConfigurationDraft).GetProperty(name)!.SetValue(next, property.Value.GetRawText());
            }
            next.initialState = next.State();
            draft = next; error = ""; return true;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        { error = "十字配置无效：" + ex.Message; return false; }
    }

    public bool TryGetParameters(out RgbCrossRegistrationParameters parameters, out string error)
    {
        parameters = new(); ErrorProperty = "";
        try
        {
            parameters.Rows = GridCount(Rows, nameof(Rows), "行数");
            parameters.Columns = GridCount(Columns, nameof(Columns), "列数");
            parameters.TargetThreshold = Number(TargetThreshold, nameof(TargetThreshold), "前景阈值比例", 0.1, 0.9);
            parameters.MinimumArmSpanFraction = Number(MinimumArmSpanFraction, nameof(MinimumArmSpanFraction), "最小十字跨度比例", 0.1, 0.9);
            parameters.AxisBandThreshold = Number(AxisBandThreshold, nameof(AxisBandThreshold), "轴带支持阈值", 0.1, 0.9);
            parameters.MinimumArmCoverage = Number(MinimumArmCoverage, nameof(MinimumArmCoverage), "最小臂截面覆盖率", 0.1, 1);
            parameters.MinimumContrast = Number(MinimumContrast, nameof(MinimumContrast), "最小信号跨度", 1e-06, 1);
            parameters.DecodeExponent = Number(DecodeExponent, nameof(DecodeExponent), "输入解码指数", 0.1, 5);
            if (includeJudgment && !string.IsNullOrWhiteSpace(MaximumEdgeSeparationPixels))
                parameters.MaximumEdgeSeparationPixels = Number(MaximumEdgeSeparationPixels, nameof(MaximumEdgeSeparationPixels), "最大边缘分离", 0, 1000);
            error = ""; return true;
        }
        catch (ArgumentException ex) { error = ex.Message; return false; }
    }
    public bool TryGetJson(out string json, out string error)
    {
        json = originalJson;
        if (!TryGetParameters(out var parameters, out error)) return false;
        if (State() != initialState)
            json = SerializeParameters(parameters, includeJudgment);
        return true;
    }
    private string State() => string.Join("\n", ActiveProperties.Select(name => (string)typeof(RgbCrossConfigurationDraft).GetProperty(name)!.GetValue(this)!));
    private int GridCount(string text, string property, string label)
    {
        double value = Number(text, property, label, 1, 16);
        if (value != Math.Truncate(value)) { ErrorProperty = property; throw new ArgumentException(label + "必须是整数。"); }
        return (int)value;
    }
    private double Number(string text, string property, string label, double min, double max)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value) || value < min || value > max)
        {
            ErrorProperty = property;
            throw new ArgumentException($"{label}必须为 {min:G} 到 {max:G} 的有限数字，小数点使用 .。");
        }
        return value;
    }
}
