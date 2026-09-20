using ColorVision.Algorithms;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ColorVision.ImageEditor.Algorithms;

/// <summary>Allowlisted measurement contract, independent of WPF, database and customer judgment.</summary>
public static class RgbCrossMeasurementExporter
{
    public const string SchemaId = "colorvision.rgb-cross-measurement";
    public const string SchemaVersion = "1.2.0";

    internal static JsonElement Create(Guid id, string version, string imageId, int width, int height,
        Rect search, int rows, int columns, IEnumerable<AlgorithmArtifact> artifacts)
    {
        var table = artifacts.OfType<AlgorithmTableArtifact>().Single(a => a.Name == "RGB-cross-separation");
        var shapes = artifacts.OfType<AlgorithmGeometryArtifact>().Single(a => a.Name == "rgb-cross-regions").Geometries.ToDictionary(g => g.Id);
        var points = new JsonArray();
        var maxima = new List<double>();
        foreach (var row in table.Rows)
        {
            string pointId = row["point"].GetString()!;
            bool valid = row["valid"].GetBoolean();
            string[] reasons = Split(row["reason"].GetString());
            var channels = new JsonObject();
            foreach (string channel in new[] { "R", "G", "B" })
            {
                shapes.TryGetValue($"{pointId}-{channel}-horizontal", out var h);
                shapes.TryGetValue($"{pointId}-{channel}-vertical", out var v);
                channels[channel] = new JsonObject
                {
                    ["status"] = h != null && v != null ? "VALID" : "INVALID",
                    ["reasonCodes"] = JsonSerializer.SerializeToNode(reasons.Where(r => r.StartsWith(channel + ":", StringComparison.Ordinal)).Select(r => r[2..])),
                    ["horizontalArm"] = Box(h), ["verticalArm"] = Box(v)
                };
            }
            JsonObject? separation = null;
            if (valid)
            {
                separation = new JsonObject();
                foreach (string edge in new[] { "left", "right", "top", "bottom" })
                    separation[edge + "EdgeSpreadPx"] = row[edge + "EdgeSpread_px"].GetDouble();
                double maximum = row["maximumEdgeSeparation_px"].GetDouble();
                separation["maximumEdgeSeparationPx"] = maximum; maxima.Add(maximum);
            }
            points.Add(new JsonObject
            {
                ["id"] = pointId, ["row"] = row["row"].GetInt32(), ["column"] = row["column"].GetInt32(),
                ["status"] = valid ? "VALID" : "INVALID", ["reasonCodes"] = JsonSerializer.SerializeToNode(reasons),
                ["warnings"] = JsonSerializer.SerializeToNode(Split(row["warning"].GetString())),
                ["region"] = row["roiWidth_px"].GetDouble() > 0 ? Box(row["roiX_px"].GetDouble(), row["roiY_px"].GetDouble(), row["roiWidth_px"].GetDouble(), row["roiHeight_px"].GetDouble()) : null,
                ["channels"] = channels, ["separation"] = separation,
                ["comparisons"] = new JsonObject { ["R-G"] = Pair(row, "r"), ["B-G"] = Pair(row, "b") }
            });
        }
        var output = new JsonObject
        {
            ["schemaId"] = SchemaId, ["schemaVersion"] = SchemaVersion, ["referenceChannel"] = "G", ["capabilityProfile"] = "rgb-cross.measurement.v1",
            ["measurementId"] = id.ToString(),
            ["algorithm"] = new JsonObject { ["id"] = DisplayMetrologyIds.RgbCrossRegistration.ToString(), ["version"] = version },
            ["source"] = new JsonObject { ["imageId"] = imageId, ["width"] = width, ["height"] = height, ["sha256"] = null },
            ["coordinates"] = new JsonObject { ["space"] = "source-image", ["unit"] = "px", ["origin"] = "top-left", ["pixelCenters"] = "integer", ["axes"] = "x-right-y-down" },
            ["searchRegion"] = Box(search.X, search.Y, search.Width, search.Height),
            ["execution"] = new JsonObject { ["status"] = "SUCCEEDED", ["reasonCodes"] = new JsonArray() },
            ["points"] = points,
            ["summary"] = new JsonObject { ["validPointCount"] = maxima.Count, ["invalidPointCount"] = rows * columns - maxima.Count, ["complete"] = maxima.Count == rows * columns, ["maximumEdgeSeparationPx"] = maxima.Count == 0 ? null : maxima.Max() }
        };
        output["grid"] = new JsonObject { ["rows"] = rows, ["columns"] = columns };
        return JsonSerializer.SerializeToElement(output);
    }

    private static JsonObject Pair(IReadOnlyDictionary<string, JsonElement> row, string channel)
    {
        string[] edges = ["Left", "Right", "Top", "Bottom"];
        bool valid = edges.All(edge => row[channel + edge + "_px"].ValueKind == JsonValueKind.Number && row["g" + edge + "_px"].ValueKind == JsonValueKind.Number);
        var result = new JsonObject { ["status"] = valid ? "VALID" : "INVALID" };
        double maximum = 0;
        foreach (string edge in edges)
        {
            double? offset = valid ? row[channel + edge + "_px"].GetDouble() - row["g" + edge + "_px"].GetDouble() : null;
            result[char.ToLowerInvariant(edge[0]) + edge[1..] + "EdgeOffsetPx"] = offset;
            if (offset.HasValue) maximum = Math.Max(maximum, Math.Abs(offset.Value));
        }
        result["maximumAbsoluteEdgeOffsetPx"] = valid ? maximum : null;
        return result;
    }

    public static void Export(AlgorithmResult result, string path)
    {
        if (result.Status != AlgorithmResultStatus.Succeeded) throw new InvalidOperationException("不能导出未完成的十字测量。");
        var data = result.Artifacts.OfType<AlgorithmStructuredDataArtifact>().Single(a => a.Schema == SchemaId).Data;
        Write(data, path);
    }

    public static void Write(JsonElement data, string path)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
            File.Move(temporary, fullPath, false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static string[] Split(string? text) => (text ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries);
    private static JsonObject Box(double x, double y, double width, double height) => new() { ["x"] = x, ["y"] = y, ["width"] = width, ["height"] = height };
    private static JsonObject? Box(AlgorithmGeometry? shape) => shape == null ? null : Box(shape.Points[0].X, shape.Points[0].Y, shape.Points[1].X - shape.Points[0].X, shape.Points[1].Y - shape.Points[0].Y);
}
