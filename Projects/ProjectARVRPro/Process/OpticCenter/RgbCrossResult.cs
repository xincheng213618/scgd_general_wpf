using Newtonsoft.Json;
using ProjectARVRPro.Recipe;
using Newtonsoft.Json.Linq;
using System.IO;

namespace ProjectARVRPro.Process.OpticCenter;

public sealed record RgbCrossRectangle(double X, double Y, double Width, double Height);
public sealed record RgbCrossChannel(string Name, RgbCrossRectangle Horizontal, RgbCrossRectangle Vertical);
public sealed class RgbCrossComparison
{
    public double? Value { get; set; }
    public double? JudgedValue { get; set; }
    public string Judgment { get; set; } = "MEASURED";
}
public sealed class RgbCrossPoint
{
    public string Id { get; set; } = "";
    public bool Valid { get; set; }
    public string Reason { get; set; } = "";
    public string Warning { get; set; } = "";
    public RgbCrossRectangle? Region { get; set; }
    public List<RgbCrossChannel> Channels { get; set; } = [];
    public Dictionary<string, RgbCrossComparison> Comparisons { get; set; } = [];
    public double? MaximumEdgeSeparation { get; set; }
    public double? JudgedEdgeSeparation { get; set; }
    public string Judgment { get; set; } = "MEASURED";
}

public sealed class RgbCrossViewResult
{
    public int GridRows { get; set; } = 3;
    public int GridColumns { get; set; } = 3;
    public string ExportName { get; set; } = "RgbCross";
    public int? SourceMasterId { get; set; }
    public string MeasurementId { get; set; } = "";
    public string AlgorithmVersion { get; set; } = "";
    public string SourceImageId { get; set; } = "";
    public string? SourceSha256 { get; set; }
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }
    public string JsonFile { get; set; } = "";
    public string JsonSha256 { get; set; } = "";
    public string SourceImageFile { get; set; } = "";
    public RecipeBase? AppliedRecipe { get; set; }
    public string Status { get; set; } = "MEASURED";
    public string Error { get; set; } = "";
    public List<RgbCrossPoint> Points { get; set; } = [];
}

internal static class RgbCrossResultParser
{
    internal const int MaximumJsonBytes = 32 * 1024 * 1024;
    internal const string AlgorithmId = "colorvision.display.rgb-cross-registration";

    public static RgbCrossViewResult Parse(string json)
    {
        using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None, MaxDepth = 64 };
        JObject root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        if (reader.Read()) throw new InvalidDataException("JSON 结果后存在额外内容。");
        if (root["schemaId"] == null && root["algorithmId"] == null) return ParseCompact(root);
        var result = new RgbCrossViewResult();
        bool portable = root["schemaId"] != null;
        JArray rows;
        RgbCrossRectangle? search = null;
        Dictionary<string, RgbCrossRectangle> geometry = new(StringComparer.Ordinal);
        if (portable)
        {
            Require(Text(root, "schemaId") == "colorvision.rgb-cross-measurement" && (Text(root, "schemaVersion") is "1.0.0" or "1.1.0" or "1.2.0"), "不支持的十字测量协议版本。");
            Require(Text(root, "capabilityProfile") == "rgb-cross.measurement.v1", "不支持的测量能力类型。");
            Require(Text(root["algorithm"]!, "id") == AlgorithmId, "不是十字 RGB 结果。");
            Require(Text(root["execution"]!, "status") == "SUCCEEDED", "算法未成功完成。");
            JToken coords = root["coordinates"] ?? throw new InvalidDataException("缺少坐标约定。");
            Require(Text(coords, "space") == "source-image" && Text(coords, "unit") == "px" && Text(coords, "origin") == "top-left" && Text(coords, "pixelCenters") == "integer" && Text(coords, "axes") == "x-right-y-down", "只支持原图像素坐标。");
            result.MeasurementId = Text(root, "measurementId");
            result.AlgorithmVersion = Text(root["algorithm"]!, "version");
            result.SourceImageId = Text(root["source"]!, "imageId");
            result.ImageWidth = Integer(root["source"]!, "width"); result.ImageHeight = Integer(root["source"]!, "height");
            Require(result.ImageWidth >= 32 && result.ImageHeight >= 32 && (long)result.ImageWidth.Value * result.ImageHeight.Value <= 67_108_864, "原图尺寸无效或超出预算。");
            search = ReadRectangle(root["searchRegion"]) ?? throw new InvalidDataException("缺少搜索区域。");
            Require(Inside(search, new(0, 0, result.ImageWidth.Value, result.ImageHeight.Value)), "搜索区域超出原图。");
            result.SourceSha256 = root["source"]?["sha256"]?.Value<string>();
            if (result.SourceSha256 != null) Require(result.SourceSha256.Length == 64 && result.SourceSha256.All(Uri.IsHexDigit), "原图 SHA-256 无效。");
            ReadGrid(root, result, Text(root, "schemaVersion") != "1.0.0");
            if (Text(root, "schemaVersion") == "1.2.0") Require(Text(root, "referenceChannel") == "G", "仅支持 G 基准。");
            rows = Array(root, "points");
        }
        else
        {
            Require(Text(root, "algorithmId") == AlgorithmId, "不是十字 RGB 导出结果。");
            result.AlgorithmVersion = Text(root, "algorithmVersion");
            Require(result.AlgorithmVersion is "1.1.0" or "1.2.0" or "1.3.0" or "1.4.0" or "1.5.0", "不支持的十字算法版本。");
            Require(Text(root, "status") == "Succeeded", "算法未成功完成。");
            result.MeasurementId = Text(root, "invocationId");
            JArray artifacts = Array(root, "artifacts");
            if (result.AlgorithmVersion is "1.4.0" or "1.5.0")
            {
                var payload = artifacts.SingleOrDefault(a => a["kind"]?.Value<string>() == "structuredData" && a["name"]?.Value<string>() == "rgb-cross-measurement")?["data"] as JObject
                    ?? throw new InvalidDataException("缺少十字结构化测量结果。");
                Require(payload["schemaId"]?.Value<string>() == "colorvision.rgb-cross-measurement", "缺少十字测量协议。");
                var parsed = Parse(payload.ToString(Formatting.None));
                Require(parsed.AlgorithmVersion == result.AlgorithmVersion && parsed.MeasurementId == result.MeasurementId, "结构化结果与执行标识不一致。");
                return parsed;
            }
            var table = artifacts.SingleOrDefault(a => a["kind"]?.Value<string>() == "table" && a["name"]?.Value<string>() == "RGB-cross-separation") ?? throw new InvalidDataException("缺少十字测量表。");
            rows = Array(table, "rows");
            var shapes = artifacts.SingleOrDefault(a => a["kind"]?.Value<string>() == "geometry" && a["name"]?.Value<string>() == "rgb-cross-regions") ?? throw new InvalidDataException("缺少 RGB 边缘几何。");
            Require(Text(shapes, "coordinateSpace") == "pixel", "只支持原图像素坐标。");
            foreach (JToken item in Array(shapes, "geometries"))
            {
                Require(Text(item, "kind") == "rectangle", "十字结果包含未知几何类型。");
                JArray points = Array(item, "points"); Require(points.Count == 2, "边缘矩形需要两个端点。");
                double x = Number(points[0], "x"), y = Number(points[0], "y");
                Require(geometry.TryAdd(Text(item, "id"), Rectangle(x, y, Number(points[1], "x") - x, Number(points[1], "y") - y)), "重复几何 ID。");
            }
        }
        int expectedCount = result.GridRows * result.GridColumns;
        Require(rows.Count == expectedCount, "点位数量与配置布局不一致；缺失点应保留 INVALID 占位。");
        for (int i = 0; i < rows.Count; i++)
        {
            JToken row = rows[i]; string id = Text(row, portable ? "id" : "point");
            Require(id == $"P{i + 1}" && Integer(row, "row") == i / result.GridColumns + 1 && Integer(row, "column") == i % result.GridColumns + 1, "十字编号或行列顺序无效。");
            bool valid;
            if (portable) { string status = Text(row, "status"); Require(status is "VALID" or "INVALID", "未知点位状态。"); valid = status == "VALID"; }
            else { Require(row["valid"]?.Type == JTokenType.Boolean, "缺少有效状态。"); valid = row["valid"]!.Value<bool>(); }
            var point = new RgbCrossPoint { Id = id, Valid = valid, Reason = portable ? string.Join(";", Array(row, "reasonCodes").Values<string>()) : row["reason"]?.Value<string>() ?? "", Warning = portable ? string.Join(";", Array(row, "warnings").Values<string>()) : row["warning"]?.Value<string>() ?? "" };
            if (portable) point.Region = ReadRectangle(row["region"]);
            else if (Number(row, "roiWidth_px") > 0 && Number(row, "roiHeight_px") > 0) point.Region = Rectangle(Number(row, "roiX_px"), Number(row, "roiY_px"), Number(row, "roiWidth_px"), Number(row, "roiHeight_px"));
            foreach (string channel in new[] { "R", "G", "B" })
            {
                RgbCrossRectangle? h, v;
                if (portable)
                {
                    JToken c = row["channels"]?[channel] ?? throw new InvalidDataException("缺少颜色通道。");
                    string state = Text(c, "status"); Require(state is "VALID" or "INVALID", "未知通道状态。");
                    h = ReadRectangle(c["horizontalArm"]); v = ReadRectangle(c["verticalArm"]);
                    Require(state == "VALID" ? h != null && v != null : h == null && v == null, "通道状态与几何不一致。");
                }
                else
                {
                    geometry.TryGetValue($"{id}-{channel}-horizontal", out h); geometry.TryGetValue($"{id}-{channel}-vertical", out v);
                    Require((h == null) == (v == null), "通道几何不完整。");
                }
                if (h != null && v != null) point.Channels.Add(new(channel, h, v));
            }
            Require(valid == (point.Channels.Count == 3), "点位状态与 RGB 通道有效性不一致。");
            JToken? measure = portable ? (row["separation"] as JObject)?["maximumEdgeSeparationPx"] : row["maximumEdgeSeparation_px"];
            if (valid)
            {
                Require(point.Region != null, "有效点缺少搜索框。");
                double value = Numeric(measure);
                double[] spreads = [Spread(point.Channels.Select(c => c.Vertical.X)), Spread(point.Channels.Select(c => c.Vertical.X + c.Vertical.Width)), Spread(point.Channels.Select(c => c.Horizontal.Y)), Spread(point.Channels.Select(c => c.Horizontal.Y + c.Horizontal.Height))];
                Require(value >= 0 && Math.Abs(value - spreads.Max()) <= 1e-6 * Math.Max(1, Math.Abs(value)), "最大边缘分离与几何不一致。");
                if (portable)
                {
                    string[] names = ["leftEdgeSpreadPx", "rightEdgeSpreadPx", "topEdgeSpreadPx", "bottomEdgeSpreadPx"];
                    for (int edge = 0; edge < names.Length; edge++) Require(Math.Abs(Number(row["separation"]!, names[edge]) - spreads[edge]) <= 1e-6 * Math.Max(1, spreads[edge]), "对应边缘跨度与几何不一致。");
                }
                point.MaximumEdgeSeparation = value;
            }
            else Require(measure == null || measure.Type == JTokenType.Null, "无效点不得有分离测量值。");
            Require(valid || !string.IsNullOrWhiteSpace(point.Reason), "无效点缺少原因。");
            if (point.Region != null)
            {
                if (result.ImageWidth.HasValue) Require(Inside(point.Region, new(0, 0, result.ImageWidth.Value, result.ImageHeight!.Value)), "点位超出原图。");
                if (search != null) Require(Inside(point.Region, search), "点位超出搜索区域。");
                foreach (var c in point.Channels) Require(Inside(c.Horizontal, point.Region) && Inside(c.Vertical, point.Region), "通道框超出点位区域。");
            }
            PopulateComparisons(point);
            if (portable && Text(root, "schemaVersion") == "1.2.0") ValidateComparisons(row, point);
            result.Points.Add(point);
        }
        if (portable)
        {
            JToken summary = root["summary"] ?? throw new InvalidDataException("缺少汇总。");
            int count = result.Points.Count(p => p.Valid);
            Require(Integer(summary, "validPointCount") == count && Integer(summary, "invalidPointCount") == expectedCount - count, "汇总计数与点位不一致。");
            Require(summary["complete"]?.Type == JTokenType.Boolean && summary["complete"]!.Value<bool>() == (count == expectedCount), "汇总完整性与点位不一致。");
            if (count == 0) Require(summary["maximumEdgeSeparationPx"]?.Type == JTokenType.Null, "无有效点时汇总值必须为空。");
            else Require(Math.Abs(Number(summary, "maximumEdgeSeparationPx") - result.Points.Where(p => p.Valid).Max(p => p.MaximumEdgeSeparation!.Value)) <= 1e-6, "汇总分离与点位不一致。");
        }
        Evaluate(result, null);
        return result;
    }

    private static RgbCrossViewResult ParseCompact(JObject root)
    {
        int width = Integer(root, "width"), height = Integer(root, "height");
        Require(width >= 32 && height >= 32 && (long)width * height <= 67_108_864, "原图尺寸无效或超出预算。");
        var result = new RgbCrossViewResult { ImageWidth = width, ImageHeight = height };
        var bounds = new RgbCrossRectangle(0, 0, width, height);
        JArray points = Array(root, "points");
        ReadGrid(root, result, false);
        Require(points.Count == result.GridRows * result.GridColumns, "点位数量与配置布局不一致，无效点也需保留。");
        for (int i = 0; i < points.Count; i++)
        {
            var row = points[i];
            Require(Integer(row, "id") == i + 1, "点号必须从 1 开始连续排列。");
            var point = new RgbCrossPoint { Id = $"P{i + 1}" };
            foreach (string channel in new[] { "R", "G", "B" })
            {
                var c = row[channel] ?? throw new InvalidDataException($"缺少 {channel} 通道。");
                if (c.Type == JTokenType.Null) continue;
                Require(c is JObject, "通道必须是矩形对象或 null。");
                RgbCrossRectangle ReadArm(string name)
                {
                    var values = Array(c, name);
                    Require(values.Count == 4, "边缘框需要 [x,y,width,height]。");
                    var rect = Rectangle(Numeric(values[0]), Numeric(values[1]), Numeric(values[2]), Numeric(values[3]));
                    Require(Inside(rect, bounds), "边缘框超出原图。");
                    return rect;
                }
                point.Channels.Add(new(channel, ReadArm("horizontal"), ReadArm("vertical")));
            }
            point.Valid = point.Channels.Count == 3;
            var separation = row["separation"] ?? throw new InvalidDataException("缺少 separation 字段。");
            if (point.Valid)
            {
                double value = Numeric(separation);
                double measured = new[] { Spread(point.Channels.Select(c => c.Vertical.X)), Spread(point.Channels.Select(c => c.Vertical.X + c.Vertical.Width)),
                    Spread(point.Channels.Select(c => c.Horizontal.Y)), Spread(point.Channels.Select(c => c.Horizontal.Y + c.Horizontal.Height)) }.Max();
                Require(value >= 0 && Math.Abs(value - measured) <= 1e-6 * Math.Max(1, Math.Abs(value)), "分离值与 RGB 边缘不一致。");
                point.MaximumEdgeSeparation = value;
            }
            else
            {
                Require(separation.Type == JTokenType.Null, "无效点的分离值必须是 null。");
                point.Reason = Text(row, "reason");
            }
            if (point.Channels.Count > 0)
            {
                var rectangles = point.Channels.SelectMany(c => new[] { c.Horizontal, c.Vertical }).ToArray();
                double x = rectangles.Min(r => r.X), y = rectangles.Min(r => r.Y);
                point.Region = new(x, y, rectangles.Max(r => r.X + r.Width) - x, rectangles.Max(r => r.Y + r.Height) - y);
            }
            result.Points.Add(point);
        }
        Evaluate(result, null);
        return result;
    }

    internal static void Evaluate(RgbCrossViewResult result, RecipeBase? recipe)
    {
        if (recipe != null)
        {
            Require(new[] { recipe.Min, recipe.Max, recipe.Fix, recipe.B }.All(double.IsFinite), "Recipe 的上下限、K、B 必须为有限数值。");
            Require(recipe.Min == 0 || recipe.Max == 0 || recipe.Min <= recipe.Max, "Recipe 下限不能高于上限。");
        }
        result.AppliedRecipe = recipe == null ? null : new RecipeBase(recipe.Min, recipe.Max, recipe.Fix, recipe.B);
        foreach (var p in result.Points)
        {
            PopulateComparisons(p);
            foreach (var comparison in p.Comparisons.Values)
            {
                comparison.JudgedValue = comparison.Value.HasValue && recipe != null ? recipe.Apply(comparison.Value.Value) : null;
                Require(!comparison.JudgedValue.HasValue || double.IsFinite(comparison.JudgedValue.Value), "K/B 修正后的分离值超出有限数值范围。");
                comparison.Judgment = !comparison.Value.HasValue ? "INVALID" : recipe == null ? "MEASURED" :
                    new ObjectiveTestItem { Value = comparison.JudgedValue!.Value, LowLimit = recipe.Min, UpLimit = recipe.Max }.TestResult ? "PASS" : "FAIL";
            }
            p.JudgedEdgeSeparation = null; // Legacy aggregate retained as raw data only.
            p.Judgment = !p.Valid ? "INVALID" : recipe == null ? "MEASURED" : p.Comparisons.Values.All(c => c.Judgment == "PASS") ? "PASS" : "FAIL";
        }
        result.Status = result.Points.Count != result.GridRows * result.GridColumns || result.Points.Any(p => !p.Valid) ? "INVALID" : recipe == null ? "MEASURED" : result.Points.All(p => p.Judgment == "PASS") ? "PASS" : "FAIL";
    }

    private static double[] Offsets(RgbCrossChannel channel, RgbCrossChannel green) =>
        [channel.Vertical.X - green.Vertical.X, channel.Vertical.X + channel.Vertical.Width - green.Vertical.X - green.Vertical.Width,
         channel.Horizontal.Y - green.Horizontal.Y, channel.Horizontal.Y + channel.Horizontal.Height - green.Horizontal.Y - green.Horizontal.Height];

    internal static void PopulateComparisons(RgbCrossPoint point)
    {
        var green = point.Channels.SingleOrDefault(c => c.Name == "G");
        foreach (string channel in new[] { "R", "B" })
        {
            var value = point.Channels.SingleOrDefault(c => c.Name == channel);
            if (!point.Comparisons.TryGetValue(channel + "-G", out var comparison))
                point.Comparisons[channel + "-G"] = comparison = new();
            comparison.Value = green != null && value != null ? Offsets(value, green).Select(Math.Abs).Max() : null;
        }
    }

    private static void ValidateComparisons(JToken row, RgbCrossPoint point)
    {
        foreach (string channel in new[] { "R", "B" })
        {
            var data = row["comparisons"]?[channel + "-G"] ?? throw new InvalidDataException("缺少 G 基准通道测量。");
            double? value = point.Comparisons[channel + "-G"].Value;
            Require(Text(data, "status") == (value.HasValue ? "VALID" : "INVALID"), "G 基准状态与通道几何不一致。");
            string[] names = ["leftEdgeOffsetPx", "rightEdgeOffsetPx", "topEdgeOffsetPx", "bottomEdgeOffsetPx", "maximumAbsoluteEdgeOffsetPx"];
            if (!value.HasValue) { foreach (string name in names) Require(data[name]?.Type == JTokenType.Null, "无效通道对必须使用 null。"); continue; }
            double[] expected = [.. Offsets(point.Channels.Single(c => c.Name == channel), point.Channels.Single(c => c.Name == "G")), value.Value];
            for (int i = 0; i < names.Length; i++) Require(Math.Abs(Number(data, names[i]) - expected[i]) <= 1e-6 * Math.Max(1, Math.Abs(expected[i])), "G 基准测量与对应边缘不一致。");
        }
    }

    private static void ReadGrid(JObject root, RgbCrossViewResult result, bool required)
    {
        if (root["grid"] == null) { Require(!required, "缺少十字布局 grid。"); return; }
        JToken grid = root["grid"]!;
        result.GridRows = Integer(grid, "rows"); result.GridColumns = Integer(grid, "columns");
        Require(result.GridRows is >= 1 and <= 16 && result.GridColumns is >= 1 and <= 16, "十字行列数必须为 1 到 16。");
        if (root["schemaVersion"]?.Value<string>() == "1.0.0")
            Require(result.GridRows == 3 && result.GridColumns == 3, "1.0.0 协议仅用于 3×3，其他布局使用 1.1.0。");
    }

    private static double Spread(IEnumerable<double> values) => values.Max() - values.Min();
    private static JArray Array(JToken token, string key) => token[key] as JArray ?? throw new InvalidDataException($"缺少数组 {key}。");
    private static string Text(JToken token, string key) => token?[key]?.Type == JTokenType.String && !string.IsNullOrWhiteSpace(token[key]!.Value<string>()) ? token[key]!.Value<string>()! : throw new InvalidDataException($"缺少字符串 {key}。");
    private static int Integer(JToken token, string key) { Require(token[key]?.Type == JTokenType.Integer, $"无效整数 {key}。"); return token[key]!.Value<int>(); }
    private static double Number(JToken token, string key) => Numeric(token[key]);
    private static double Numeric(JToken? value) { Require(value?.Type is JTokenType.Integer or JTokenType.Float, "缺少有限数值。"); double d = value!.Value<double>(); Require(double.IsFinite(d), "数值必须有限。"); return d; }
    private static RgbCrossRectangle? ReadRectangle(JToken? token) => token == null || token.Type == JTokenType.Null ? null : Rectangle(Number(token, "x"), Number(token, "y"), Number(token, "width"), Number(token, "height"));
    private static RgbCrossRectangle Rectangle(double x, double y, double width, double height) { Require(x >= 0 && y >= 0 && width > 0 && height > 0 && x + width <= int.MaxValue && y + height <= int.MaxValue, "矩形坐标无效。"); return new(x, y, width, height); }
    internal static bool Inside(RgbCrossRectangle r, RgbCrossRectangle bounds) => r.X >= bounds.X && r.Y >= bounds.Y && r.X + r.Width <= bounds.X + bounds.Width + 1e-6 && r.Y + r.Height <= bounds.Y + bounds.Height + 1e-6;
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
