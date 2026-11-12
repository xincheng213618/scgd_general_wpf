using ColorVision.Algorithms;
using ColorVision.Database;
using ColorVision.ImageEditor.Algorithms;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ColorVision.Engine.Templates.Jsons.FindCross;

public sealed class LocalRgbCrossHistoryRow
{
    public required string Point { get; init; }
    public int Row { get; init; }
    public int Column { get; init; }
    public required string Status { get; init; }
    public double? RG { get; init; }
    public double? BG { get; init; }
    public string Diagnostic { get; init; } = "";
}

/// <summary>Reads saved neutral measurements only; never runs an algorithm or applies a customer recipe.</summary>
internal sealed class LocalRgbCrossHistory : IViewResult
{
    internal const int MaximumJsonBytes = 32 * 1024 * 1024;
    internal int Width { get; private init; }
    internal int Height { get; private init; }
    internal string Diagnostic { get; private init; } = "";
    internal string ResultFileName { get; private init; } = "";
    internal IReadOnlyList<LocalRgbCrossHistoryRow> Rows { get; private init; } = [];
    internal IReadOnlyList<AlgorithmGeometry> Geometries { get; private init; } = [];
    internal IReadOnlyList<AlgorithmOverlayItem> Overlays { get; private init; } = [];

    internal static LocalRgbCrossHistory Failure(string message) => new() { Diagnostic = "十字 RGB 历史结果读取失败：" + message };

    internal static LocalRgbCrossHistory Read(IReadOnlyList<DetailCommonModel> details, int masterId)
    {
        if (details.Count != 1 || details[0].PId != masterId) throw new InvalidDataException("需要唯一且归属当前主记录的结果明细。");
        using JsonDocument reference = JsonDocument.Parse(details[0].ResultJson);
        string? path = reference.RootElement.GetProperty("ResultFileName").GetString();
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) throw new InvalidDataException("结果文件路径必须是绝对路径。");
        using FileStream stream = File.OpenRead(path);
        if (stream.Length > MaximumJsonBytes) throw new InvalidDataException("结果 JSON 超过 32 MiB。");
        using StreamReader reader = new(stream);
        return Parse(reader.ReadToEnd(), path);
    }

    internal static LocalRgbCrossHistory Parse(string json, string fileName = "")
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Require(root.GetProperty("schemaId").GetString() == RgbCrossMeasurementExporter.SchemaId, "不是中立十字 RGB 测量结果。");
        string? version = root.GetProperty("schemaVersion").GetString();
        Require(version is "1.0.0" or "1.1.0" or "1.2.0", "不支持的十字结果 schemaVersion。");
        Require(root.GetProperty("algorithm").GetProperty("id").GetString() == DisplayMetrologyIds.RgbCrossRegistration.ToString(), "十字算法标识不匹配。");
        Require(root.GetProperty("referenceChannel").GetString() == "G", "十字参考通道必须是 G。");
        JsonElement coordinates = root.GetProperty("coordinates");
        foreach (var (key, value) in new[] { ("space", "source-image"), ("unit", "px"), ("origin", "top-left"), ("pixelCenters", "integer"), ("axes", "x-right-y-down") })
            Require(coordinates.GetProperty(key).GetString() == value, "结果不是受支持的原图像素坐标。");
        JsonElement source = root.GetProperty("source");
        int width = source.GetProperty("width").GetInt32(), height = source.GetProperty("height").GetInt32();
        Require(width >= 32 && height >= 32 && (long)width * height <= DisplayMetrologyProvider.MaximumRgbCrossPixels, "原图尺寸超出十字测量范围。");
        Box image = new(0, 0, width, height);
        Box? search = ReadBox(root.GetProperty("searchRegion"), image);
        int gridRows = 3, gridColumns = 3;
        if (root.TryGetProperty("grid", out JsonElement grid))
        {
            gridRows = grid.GetProperty("rows").GetInt32(); gridColumns = grid.GetProperty("columns").GetInt32();
        }
        else Require(version != "1.2.0", "结果缺少图案网格。");
        Require(gridRows is >= 1 and <= 16 && gridColumns is >= 1 and <= 16, "图案网格超出范围。");
        JsonElement execution = root.GetProperty("execution");
        string? executionStatus = execution.GetProperty("status").GetString();
        Require(executionStatus is "SUCCEEDED" or "FAILED" or "CANCELLED", "未知执行状态。");
        JsonElement points = root.GetProperty("points");
        Require(points.ValueKind == JsonValueKind.Array && points.GetArrayLength() <= gridRows * gridColumns, "十字点数超出图案网格。");
        Require(executionStatus != "SUCCEEDED" || points.GetArrayLength() == gridRows * gridColumns, "十字点数与图案网格不一致。");
        List<LocalRgbCrossHistoryRow> rows = [];
        List<AlgorithmGeometry> geometries = [];
        List<AlgorithmOverlayItem> overlays = [];
        HashSet<string> ids = new(StringComparer.Ordinal);
        if (search is Box searchBox) AddBox("search-region", searchBox, "#FF888888");
        foreach (JsonElement point in points.EnumerateArray())
        {
            string id = point.GetProperty("id").GetString() ?? "";
            int row = point.GetProperty("row").GetInt32(), column = point.GetProperty("column").GetInt32();
            Require(row >= 1 && row <= gridRows && column >= 1 && column <= gridColumns
                && id == "P" + ((row - 1) * gridColumns + column).ToString(CultureInfo.InvariantCulture) && ids.Add(id), "十字编号、行列或重复点无效。");
            string? status = point.GetProperty("status").GetString();
            Require(status is "VALID" or "INVALID", "未知十字测量状态。");
            string diagnostic = string.Join("; ", new[] { Codes(point, "reasonCodes"), Codes(point, "warnings") }.Where(text => text.Length > 0));
            Box? region = ReadBox(point.GetProperty("region"), image);
            if (region is Box target && search is Box limited) Require(Inside(target, limited), "十字区域超出主搜索区域。");
            JsonElement channels = point.GetProperty("channels");
            Dictionary<string, bool> channelValid = new(StringComparer.Ordinal);
            foreach (var (name, color) in new[] { ("R", "#FFFF4040"), ("G", "#FF40D060"), ("B", "#FF4090FF") })
            {
                JsonElement channel = channels.GetProperty(name);
                string? channelStatus = channel.GetProperty("status").GetString();
                Require(channelStatus is "VALID" or "INVALID", "未知通道状态。");
                Box? horizontal = ReadBox(channel.GetProperty("horizontalArm"), image), vertical = ReadBox(channel.GetProperty("verticalArm"), image);
                bool valid = channelStatus == "VALID";
                channelValid[name] = valid;
                Require(valid ? horizontal.HasValue && vertical.HasValue && region.HasValue : horizontal == null && vertical == null, "通道状态与保存的边缘不一致。");
                if (valid)
                {
                    Require(Inside(horizontal!.Value, region!.Value) && Inside(vertical!.Value, region.Value), "通道边缘超出十字区域。");
                    AddBox(id + "-" + name + "-horizontal", horizontal.Value, color);
                    AddBox(id + "-" + name + "-vertical", vertical.Value, color);
                }
                string channelReason = Codes(channel, "reasonCodes");
                if (!valid && channelReason.Length > 0) diagnostic += $"; {name}:{channelReason}";
            }
            Require(status != "VALID" || channelValid.Values.All(valid => valid), "有效十字缺少有效通道。");
            double? rg = ReadPair(point, "R-G", channelValid["R"] && channelValid["G"], Math.Max(width, height));
            double? bg = ReadPair(point, "B-G", channelValid["B"] && channelValid["G"], Math.Max(width, height));
            rows.Add(new LocalRgbCrossHistoryRow { Point = id, Row = row, Column = column, Status = status!, RG = rg, BG = bg, Diagnostic = diagnostic.TrimStart(';', ' ') });
            if (region is Box bounds) AddBox(id + "-region", bounds, status == "VALID" ? "#FF00BFFF" : "#FFFFA500",
                $"{id}  R-G {Pixel(rg)} px\nB-G {Pixel(bg)} px" + (status == "INVALID" ? "  INVALID" : ""));
        }
        return new LocalRgbCrossHistory
        {
            Width = width, Height = height, ResultFileName = fileName, Rows = rows, Geometries = geometries, Overlays = overlays,
            Diagnostic = $"执行：{executionStatus}；{rows.Count(item => item.Status == "VALID")} / {rows.Count} 点可测（像素测量，无产品判定）\n" + Codes(execution, "reasonCodes")
        };

        void AddBox(string id, Box box, string color, string? label = null)
        {
            geometries.Add(new AlgorithmGeometry(id, AlgorithmGeometryKind.Rectangle, [new(box.X, box.Y), new(box.X + box.Width, box.Y + box.Height)]));
            overlays.Add(new AlgorithmOverlayItem(id, new AlgorithmOverlayStyle(color, null, 1, label)));
        }
    }

    private static double? ReadPair(JsonElement point, string name, bool channelsValid, double maximum)
    {
        // Older saved contracts may omit comparisons. Do not substitute a recomputed measurement.
        if (!point.TryGetProperty("comparisons", out JsonElement comparisons)) return null;
        JsonElement pair = comparisons.GetProperty(name);
        string? status = pair.GetProperty("status").GetString();
        Require(status is "VALID" or "INVALID", "未知通道对状态。");
        JsonElement value = pair.GetProperty("maximumAbsoluteEdgeOffsetPx");
        if (status == "INVALID") { Require(value.ValueKind == JsonValueKind.Null, "无效通道对包含数值。"); return null; }
        Require(channelsValid, "通道对包含无效通道。");
        double result = value.GetDouble();
        Require(double.IsFinite(result) && result >= 0 && result <= maximum, "通道对偏移超出原图范围。");
        return result;
    }

    private readonly record struct Box(double X, double Y, double Width, double Height);
    private static Box? ReadBox(JsonElement value, Box image)
    {
        if (value.ValueKind == JsonValueKind.Null) return null;
        Box box = new(value.GetProperty("x").GetDouble(), value.GetProperty("y").GetDouble(), value.GetProperty("width").GetDouble(), value.GetProperty("height").GetDouble());
        Require(double.IsFinite(box.X) && double.IsFinite(box.Y) && double.IsFinite(box.Width) && double.IsFinite(box.Height)
            && box.Width > 0 && box.Height > 0 && Inside(box, image), "保存的区域或边缘超出原图范围。");
        return box;
    }
    private static bool Inside(Box value, Box bounds) => value.X >= bounds.X && value.Y >= bounds.Y
        && value.X + value.Width <= bounds.X + bounds.Width && value.Y + value.Height <= bounds.Y + bounds.Height;
    private static string Codes(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out JsonElement codes)) return "";
        Require(codes.ValueKind == JsonValueKind.Array && codes.GetArrayLength() <= 64, "诊断列表无效。");
        return string.Join("; ", codes.EnumerateArray().Select(code =>
        {
            string text = code.GetString() ?? "";
            Require(text.Length <= 512, "诊断信息过长。");
            return text;
        }));
    }
    private static string Pixel(double? value) => value?.ToString("F3", CultureInfo.InvariantCulture) ?? "—";
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
