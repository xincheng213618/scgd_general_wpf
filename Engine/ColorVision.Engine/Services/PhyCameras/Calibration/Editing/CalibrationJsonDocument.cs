using ColorVision.Engine.Services.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.Services.PhyCameras.Calibration.Editing;

public enum CalibrationParameterKind { Number, WholeNumber, Boolean }

public sealed class CalibrationScalarParameter : INotifyPropertyChanged
{
    public string Path { get; init; } = "";
    public string Label { get; init; } = "";
    public string Group { get; init; } = "";
    public string Description { get; init; } = "";
    public CalibrationParameterKind Kind { get; init; }
    internal string InitialValue { get; set; } = "";
    private string value = "";
    public string Value { get => value; set { this.value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class CalibrationCoefficientTable
{
    public string Key { get; init; } = "";
    public string Title { get; init; } = "";
    public ObservableCollection<CalibrationScalarParameter> Rows { get; } = new();
}

/// <summary>Edits only known calibration members, retaining all other JSON data.</summary>
public sealed class CalibrationJsonDocument
{
    public ServiceTypes Type { get; }
    public string Title => Type switch { ServiceTypes.DarkNoise => "暗噪声", ServiceTypes.ColorShift => "色偏", ServiceTypes.Distortion => "畸变", ServiceTypes.Luminance => "亮度", ServiceTypes.LumOneColor => "单色", ServiceTypes.LumFourColor => "四色", ServiceTypes.LumMultiColor => "多色", ServiceTypes.ColorDiff => "ColorDiff", ServiceTypes.AngleShift => "角度偏移", _ => throw new NotSupportedException() };
    public string Description => Type switch
    {
        ServiceTypes.ColorShift => "通道空间偏移，单位为像素。",
        ServiceTypes.ColorDiff => "GR/GB 通道空间对齐的径向多项式和偏移。",
        ServiceTypes.AngleShift => "RGB 通道径向多项式；并非旋转角度。",
        ServiceTypes.DarkNoise => "兼容旧算法：暗噪声采用倍率修正；原文件 Texp_x 为浮点数时比例才生效。",
        ServiceTypes.Luminance => "单通道亮度比例，按实际采集曝光归一化。",
        ServiceTypes.LumOneColor => "X=aR+dB，Y=bG，Z=cB；按实际采集曝光和文件通道增益归一化。",
        ServiceTypes.LumFourColor => "RGB 转 XYZ 矩阵；转换使用实际采集曝光。历史文件中的曝光和增益仅保留兼容信息。",
        ServiceTypes.LumMultiColor => "RGB 转 XYZ 矩阵；按实际采集曝光和文件通道增益归一化。",
        _ => "编辑校正参数；新建参数须结合实测标定。"
    };
    public JObject Json { get; }
    public ObservableCollection<CalibrationScalarParameter> Parameters { get; } = new();
    public ObservableCollection<CalibrationCoefficientTable> Tables { get; } = new();

    private CalibrationJsonDocument(ServiceTypes type, JObject json)
    {
        Type = type; Json = json;
        _ = Title;
        Populate();
    }

    public static CalibrationJsonDocument Parse(ServiceTypes type, string json) => new(type, ParseObject(json));

    internal static JObject ParseObject(string json)
    {
        try { using var strict = System.Text.Json.JsonDocument.Parse(json); }
        catch (System.Text.Json.JsonException ex) { throw new JsonReaderException("校正 JSON 语法无效。", ex); }
        using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };
        var token = JToken.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error, CommentHandling = CommentHandling.Load });
        if (reader.Read()) throw new JsonReaderException("JSON 后存在额外内容。");
        if (token is not JObject root) throw new JsonReaderException("校正 JSON 根节点必须是对象。");
        if (root.Descendants().Any(x => x.Type == JTokenType.Comment)) throw new JsonReaderException("校正 JSON 不支持注释。");
        return root;
    }

    public static CalibrationJsonDocument CreateDefault(ServiceTypes type)
    {
        var root = type switch
        {
            ServiceTypes.DarkNoise => JObject.Parse("{\"Texp_x\":1.0,\"DarkNoiseRatio\":1.0}"),
            ServiceTypes.ColorShift => JObject.Parse("{\"fillOffset\":false,\"offset\":[{\"X\":0,\"Y\":0},{\"X\":0,\"Y\":0},{\"X\":0,\"Y\":0}]}"),
            ServiceTypes.Distortion => JObject.Parse("{\"w\":1920,\"h\":1080,\"s_w\":1920,\"s_h\":1080,\"useFisheye\":false,\"alpha\":0.0,\"cameraMatrix\":[1,0,960,0,1,540,0,0,1],\"distCoeffs\":[0,0,0,0,0]}"),
            ServiceTypes.LumMultiColor => JObject.Parse("{\"bpp\":16,\"pa\":[1,0,0,0,1,0,0,0,1],\"Gain\":[1,1,1]}"),
            ServiceTypes.ColorDiff => JObject.Parse("{\"w\":1920,\"h\":1080,\"CenterCol\":960,\"CenterRow\":540,\"MeasDis\":1.0,\"CalibDis\":1.0,\"ColorDiffCoeffs_GR\":[0,0],\"ColorDiffCoeffs_GB\":[0,0],\"ColRowCoeffs_GR\":[0,0],\"ColRowCoeffs_GB\":[0,0]}"),
            ServiceTypes.AngleShift => JObject.Parse("{\"optical_center_x\":960,\"optical_center_y\":540,\"interpolate_ratio\":1.0,\"coefficient_order\":1,\"target_row\":1080,\"target_col\":1920,\"coeff_r\":[0,0],\"coeff_g\":[0,0],\"coeff_b\":[0,0],\"rowColShift\":[0,0]}"),
            ServiceTypes.Luminance or ServiceTypes.LumOneColor or ServiceTypes.LumFourColor => JObject.Parse("{\"bpp\":16,\"Texp_x\":1.0,\"Texp_y\":1.0,\"Texp_z\":1.0,\"Gain_x\":1.0,\"Gain_y\":1.0,\"Gain_z\":1.0,\"a\":1.0,\"b\":0.0,\"c\":0.0,\"d\":0.0}"),
            _ => throw new NotSupportedException("此校正不是 JSON 参数校正。")
        };
        if (type == ServiceTypes.LumFourColor) { root["e"] = 1.0; root["f"] = 0.0; root["g"] = 0.0; root["h"] = 0.0; root["i"] = 1.0; }
        if (type == ServiceTypes.LumOneColor) { root["b"] = 1.0; root["c"] = 1.0; }
        return new(type, root);
    }

    private void Add(string path, string label, string group = "参数", CalibrationParameterKind kind = CalibrationParameterKind.Number)
    {
        var token = Json.SelectToken(path);
        string text = token == null || token.Type == JTokenType.Null ? "" : token.ToString(Formatting.None);
        label = path switch
        {
            "Texp_x" => "曝光 X（ms）", "Texp_y" => "曝光 Y（ms）", "Texp_z" => "曝光 Z（ms）",
            "Gain_x" => "增益 X（倍率）", "Gain_y" => "增益 Y（倍率）", "Gain_z" => "增益 Z（倍率）",
            "w" => "输入宽度（px）", "h" => "输入高度（px）", "s_w" => "输出宽度（px）", "s_h" => "输出高度（px）",
            "CenterCol" => "中心 X（px）", "CenterRow" => "中心 Y（px）",
            "optical_center_x" => "光学中心 X（px）", "optical_center_y" => "光学中心 Y（px）",
            "interpolate_ratio" => "插值放大倍数", "coefficient_order" => "多项式阶数",
            "target_row" => "目标高度（px）", "target_col" => "目标宽度（px）", "alpha" => "输出视场参数 α", _ => label
        };
        string description = path switch
        {
            "interpolate_ratio" => "原图插值放大的倍数，必须大于零；实际输入图像放大后的尺寸需要符合算法限制。",
            "coefficient_order" => "径向位移多项式的最高幂次；每个通道需提供阶数加一项系数，从常数项开始。",
            "MeasDis" => "测量距离，不可为零；与标定距离使用相同单位。",
            "CalibDis" => "标定距离，与测量距离使用相同单位。",
            "alpha" => "鱼眼模式下用于计算输出相机矩阵的视场参数。",
            "DarkNoiseRatio" => "暗噪声乘法比例；兼容旧文件，曝光 X 为 JSON 浮点数时才生效。",
            "fillOffset" => "通道平移后是否按现有校正算法填充边缘。",
            _ => label
        };
        Parameters.Add(new() { Path = path, Label = label, Group = group, Description = description + "\n字段：" + path, Kind = kind, Value = text, InitialValue = text });
    }

    private void Table(string key, string title, IEnumerable<string> paths)
    {
        var table = new CalibrationCoefficientTable { Key = key, Title = title };
        int index = 0;
        foreach (var path in paths)
        {
            var token = Json.SelectToken(path); string text = token == null || token.Type == JTokenType.Null ? "" : token.ToString(Formatting.None);
            string label = key switch
            {
                "matrix" or "pa" when index < 9 => $"{"XYZ"[index / 3]} ← {"RGB"[index % 3]}",
                "coefficients" when Type == ServiceTypes.LumOneColor => new[] { "a：X ← R", "b：Y ← G", "c：Z ← B", "d：X ← B" }[index],
                "coefficients" when Type == ServiceTypes.Luminance => index == 0 ? "a：亮度比例" : $"{path}：兼容参数",
                "cameraMatrix" when index < 9 => $"第 {index / 3 + 1} 行，第 {index % 3 + 1} 列",
                "Gain" when index < 3 => $"{"RGB"[index]} 通道增益（倍率）",
                "coeff_r" or "coeff_g" or "coeff_b" or "ColorDiffCoeffs_GR" or "ColorDiffCoeffs_GB" => index == 0 ? "常数项" : $"半径 {index} 次项",
                "ColRowCoeffs_GR" or "ColRowCoeffs_GB" => index == 0 ? "X 偏移（px）" : "Y 偏移（px）",
                "rowColShift" => index == 0 ? "Y 偏移（px）" : "X 偏移（px）",
                _ => path
            };
            table.Rows.Add(new() { Path = path, Label = label, Group = title, Description = label + "\n字段：" + path, Kind = CalibrationParameterKind.Number, Value = text, InitialValue = text });
            index++;
        }
        Tables.Add(table);
    }
    private void Array(string key, string title)
    {
        title = key switch
        {
            "ColorDiffCoeffs_GR" => "GR 通道径向位移系数（从常数项开始）",
            "ColorDiffCoeffs_GB" => "GB 通道径向位移系数（从常数项开始）",
            "ColRowCoeffs_GR" => "GR 通道空间偏移", "ColRowCoeffs_GB" => "GB 通道空间偏移",
            "coeff_r" => "R 通道径向位移系数（从常数项开始）",
            "coeff_g" => "G 通道径向位移系数（从常数项开始）",
            "coeff_b" => "B 通道径向位移系数（从常数项开始）",
            "rowColShift" => "共用空间偏移（Y / X）", _ => title
        };
        Table(key, title, Enumerable.Range(0, (Json[key] as JArray)?.Count ?? 0).Select(i => $"{key}[{i}]"));
    }

    private void Populate()
    {
        switch (Type)
        {
            case ServiceTypes.DarkNoise: Add("Texp_x", "曝光 Texp_x"); Add("DarkNoiseRatio", "暗噪声比例"); break;
            case ServiceTypes.ColorShift:
                Add("fillOffset", "边缘填充", kind: CalibrationParameterKind.Boolean);
                if (Json["offset"] is JArray offsets) for (int i = 0; i < offsets.Count; i++) { Add($"offset[{i}].X", $"通道 {i + 1} X (px)"); Add($"offset[{i}].Y", $"通道 {i + 1} Y (px)"); }
                else { Add("offset.X", "共用 X (px)"); Add("offset.Y", "共用 Y (px)"); } break;
            case ServiceTypes.Distortion:
                foreach (string k in new[] { "w", "h", "s_w", "s_h" }) Add(k, k + " (px)", kind: CalibrationParameterKind.WholeNumber);
                Add("useFisheye", "鱼眼模式", kind: CalibrationParameterKind.Boolean); Add("alpha", "alpha"); Array("cameraMatrix", "相机内参 3×3（按行排列）"); Array("distCoeffs", "畸变系数"); break;
            case ServiceTypes.Luminance:
            case ServiceTypes.LumOneColor:
            case ServiceTypes.LumFourColor:
                Add("bpp", "位深", kind: CalibrationParameterKind.WholeNumber);
                foreach (string k in new[] { "Texp_x", "Texp_y", "Texp_z", "Gain_x", "Gain_y", "Gain_z" }) Add(k, k, "采集参数");
                Table(Type == ServiceTypes.LumFourColor ? "matrix" : "coefficients", Type == ServiceTypes.LumFourColor ? "四色 3×3 系数（行 X/Y/Z，列 R/G/B）" : "亮度/单色系数", (Type == ServiceTypes.LumFourColor ? "abcdefghi" : "abcd").Select(c => c.ToString())); break;
            case ServiceTypes.LumMultiColor: Add("bpp", "位深", kind: CalibrationParameterKind.WholeNumber); Array("pa", "多色 3×3 系数（行 X/Y/Z，列 R/G/B；前九项）"); Array("Gain", "R/G/B 通道增益"); break;
            case ServiceTypes.ColorDiff:
                foreach (string k in new[] { "w", "h", "CenterCol", "CenterRow" }) Add(k, k + " (px)", kind: CalibrationParameterKind.WholeNumber);
                Add("MeasDis", "测量距离"); Add("CalibDis", "标定距离"); foreach (string k in new[] { "ColorDiffCoeffs_GR", "ColorDiffCoeffs_GB", "ColRowCoeffs_GR", "ColRowCoeffs_GB" }) Array(k, k); break;
            case ServiceTypes.AngleShift:
                foreach (string k in new[] { "optical_center_x", "optical_center_y", "interpolate_ratio" }) Add(k, k);
                foreach (string k in new[] { "coefficient_order", "target_row", "target_col" }) Add(k, k, kind: CalibrationParameterKind.WholeNumber);
                foreach (string k in new[] { "coeff_r", "coeff_g", "coeff_b", "rowColShift" }) Array(k, k); break;
        }
    }

    private JObject ApplyValues()
    {
        var root = (JObject)Json.DeepClone();
        foreach (var p in Parameters.Concat(Tables.SelectMany(t => t.Rows)))
        {
            if (p.Value == p.InitialValue) continue;
            JToken value;
            if (p.Kind == CalibrationParameterKind.Boolean && bool.TryParse(p.Value, out bool boolean)) value = new JValue(boolean);
            else if (p.Kind == CalibrationParameterKind.WholeNumber && int.TryParse(p.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int integer)) value = new JValue(integer);
            else if (p.Kind == CalibrationParameterKind.Number && double.TryParse(p.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && double.IsFinite(number)) value = new JValue(number);
            else throw new FormatException($"{p.Label} 必须是有效的{(p.Kind == CalibrationParameterKind.WholeNumber ? "整数" : p.Kind == CalibrationParameterKind.Boolean ? "布尔" : "数")}值。");
            var target = root.SelectToken(p.Path);
            if (target != null) target.Replace(value);
            else if (!p.Path.Contains('.') && !p.Path.Contains('[')) root[p.Path] = value;
            else throw new FormatException($"缺少字段 {p.Path}；请在高级 JSON 中补全结构。");
        }
        return root;
    }

    public IReadOnlyList<string> Validate()
    {
        try { return ValidateRoot(ApplyValues()); }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException) { return new[] { ex.Message }; }
    }

    public string BuildJson()
    {
        var root = ApplyValues(); var errors = ValidateRoot(root);
        if (errors.Count != 0) throw new FormatException(string.Join(Environment.NewLine, errors));
        return root.ToString(Formatting.Indented);
    }

    /// <summary>Exports current form values for advanced repair without requiring a valid runtime schema.</summary>
    public string BuildDraftJson() => ApplyValues().ToString(Formatting.Indented);

    private List<string> ValidateRoot(JObject root)
    {
        var errors = new List<string>();
        bool Number(string path, bool required = true, bool legacy = false)
        {
            var v = root.SelectToken(path);
            if (!required && (v == null || v.Type == JTokenType.Null)) return true;
            if (legacy && v?.Type == JTokenType.Boolean) return true;
            if (v?.Type is not (JTokenType.Integer or JTokenType.Float) || !double.IsFinite(v.Value<double>())) { errors.Add($"{path} 必须是有限数值。"); return false; }
            return true;
        }
        // Existing native files may encode an integer field as a real; native truncates it.
        void Integer(string path, double min, double max, bool required = true) { if (Number(path, required) && root.SelectToken(path) is JToken v && (v.Value<double>() < int.MinValue || v.Value<double>() > int.MaxValue || Math.Truncate(v.Value<double>()) < min || Math.Truncate(v.Value<double>()) > max)) errors.Add($"{path} 必须在 {min} 至 {max} 范围内。"); }
        void Boolean(string key) { if (root[key] != null && root[key]!.Type != JTokenType.Boolean) errors.Add($"{key} 必须是布尔值。"); }
        void FloatValue(string path)
        {
            var v = root.SelectToken(path);
            if ((v?.Type is JTokenType.Integer or JTokenType.Float) && !float.IsFinite((float)v.Value<double>())) errors.Add($"{path} 超出运行算法的 32 位浮点数范围。");
        }
        void NonzeroGain(string path, bool usesFloat = false)
        {
            var v = root.SelectToken(path);
            double n = v == null || v.Type == JTokenType.Null ? 0 : v.Type == JTokenType.Boolean ? (v.Value<bool>() ? 1 : 0) : v.Type is JTokenType.Integer or JTokenType.Float ? v.Value<double>() : double.NaN;
            if (n == 0 || (usesFloat && (float)n == 0)) errors.Add($"{path} 不可为零、缺失或小到转换为零，否则运行时会除以零。");
        }
        void ArrayValues(string key, int min, int? exact = null, bool optional = false, bool legacy = false, int? consumed = null)
        {
            if (optional && (root[key] == null || root[key]!.Type == JTokenType.Null)) return;
            if (root[key] is not JArray a || a.Count < min || (exact.HasValue && a.Count != exact.Value)) { errors.Add($"{key} 数组长度无效（至少 {min}" + (exact.HasValue ? $"，必须 {exact}" : "") + "）。"); return; }
            for (int i = 0; i < Math.Min(a.Count, consumed ?? a.Count); i++) Number($"{key}[{i}]", !legacy, legacy);
        }
        switch (Type)
        {
            case ServiceTypes.DarkNoise:
                // Legacy parser ignores nonnumeric fields and the ratio unless exposure is a JSON real.
                if (root["Texp_x"]?.Type == JTokenType.Float)
                {
                    Number("Texp_x", false);
                    if (root["DarkNoiseRatio"]?.Type is JTokenType.Integer or JTokenType.Float) { Number("DarkNoiseRatio", false); FloatValue("DarkNoiseRatio"); }
                }
                break;
            case ServiceTypes.ColorShift:
                Boolean("fillOffset");
                if (root["offset"] is JArray a) { if (a.Count != 3) errors.Add("offset 必须包含三个通道。"); for (int i = 0; i < a.Count; i++) { Integer($"offset[{i}].X", int.MinValue, int.MaxValue); Integer($"offset[{i}].Y", int.MinValue, int.MaxValue); } }
                else { Integer("offset.X", int.MinValue, int.MaxValue); Integer("offset.Y", int.MinValue, int.MaxValue); } break;
            case ServiceTypes.Distortion:
                Integer("w", 1, int.MaxValue); Integer("h", 1, int.MaxValue); Integer("s_w", 1, int.MaxValue, false); Integer("s_h", 1, int.MaxValue, false); Boolean("useFisheye"); Number("alpha", false); ArrayValues("cameraMatrix", 9); ArrayValues("distCoeffs", 4);
                for (int i = 0; i < 9; i++) FloatValue($"cameraMatrix[{i}]");
                for (int i = 0; i < Math.Min(5, (root["distCoeffs"] as JArray)?.Count ?? 0); i++) FloatValue($"distCoeffs[{i}]");
                if (root["useFisheye"]?.Type == JTokenType.Boolean && root["useFisheye"]!.Value<bool>())
                    foreach (int i in new[] { 2, 5 })
                    {
                        var v = root.SelectToken($"cameraMatrix[{i}]");
                        if ((v?.Type is JTokenType.Integer or JTokenType.Float) && ((double)(float)v.Value<double>() < int.MinValue || (double)(float)v.Value<double>() > int.MaxValue)) errors.Add($"cameraMatrix[{i}] 超出鱼眼中心的整数范围。");
                    }
                break;
            case ServiceTypes.Luminance:
            case ServiceTypes.LumOneColor:
            case ServiceTypes.LumFourColor:
                foreach (string k in new[] { "Texp_x", "Texp_y", "Texp_z", "Gain_x", "Gain_y", "Gain_z" }) Number(k, false, true);
                foreach (char k in Type == ServiceTypes.LumFourColor ? "abcdefghi" : "abcd") Number(k.ToString(), false, true);
                if (Type == ServiceTypes.LumOneColor) foreach (string k in new[] { "Gain_x", "Gain_y", "Gain_z" }) NonzeroGain(k);
                break;
            case ServiceTypes.LumMultiColor:
                ArrayValues("pa", 9, legacy: true, consumed: 9); ArrayValues("Gain", 0, optional: true, legacy: true, consumed: 3);
                for (int i = 0; i < 9; i++) FloatValue($"pa[{i}]");
                for (int i = 0; i < 3; i++) { FloatValue($"Gain[{i}]"); NonzeroGain($"Gain[{i}]", true); }
                break;
            case ServiceTypes.ColorDiff:
                Integer("w", 3, 65535); Integer("h", 3, 65535); Integer("CenterCol", int.MinValue, int.MaxValue); Integer("CenterRow", int.MinValue, int.MaxValue);
                if (Number("MeasDis") && root["MeasDis"]!.Value<double>() == 0) errors.Add("MeasDis 不可为零。"); Number("CalibDis"); ArrayValues("ColorDiffCoeffs_GR", 1); ArrayValues("ColorDiffCoeffs_GB", 1); ArrayValues("ColRowCoeffs_GR", 2, 2); ArrayValues("ColRowCoeffs_GB", 2, 2); break;
            case ServiceTypes.AngleShift:
                Number("optical_center_x"); Number("optical_center_y"); if (Number("interpolate_ratio") && root["interpolate_ratio"]!.Value<double>() <= 0) errors.Add("interpolate_ratio 必须大于零。");
                Integer("coefficient_order", 0, int.MaxValue - 1); Integer("target_row", 1, int.MaxValue); Integer("target_col", 1, int.MaxValue);
                int? count = (root["coefficient_order"]?.Type is JTokenType.Integer or JTokenType.Float) && root["coefficient_order"]!.Value<double>() is >= 0 and < int.MaxValue ? (int)root["coefficient_order"]!.Value<double>() + 1 : null;
                foreach (string key in new[] { "coeff_r", "coeff_g", "coeff_b" }) ArrayValues(key, 1, count); ArrayValues("rowColShift", 2, 2); break;
        }
        return errors;
    }
}
