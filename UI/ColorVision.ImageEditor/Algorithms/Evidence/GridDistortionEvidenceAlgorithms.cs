using ColorVision.Algorithms;
using ColorVision.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ColorVision.ImageEditor.Algorithms.Evidence;

public static class GridDistortionEvidenceAlgorithms
{
    public const string LocalId = "distortion.grid-v2";

    public static ImageEvidenceAlgorithm CreateLocal(GridDistortionOptions options)
    {
        // Keep the report and native invocation on the same immutable parameter snapshot.
        string json = options.ToJson();
        return new(LocalId, "点阵畸变 · 本地 GridV2", image =>
        {
            GridDistortionOptions captured = JsonSerializer.Deserialize<GridDistortionOptions>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            GridDistortionResult result = GridDistortionNative.Run(image, new RoiRect(), captured);
            return ConvertLocal(result, new
            {
                Library = DistortionEvidenceArtifacts.LoadedLibrary("opencv_helper.dll"),
                EntryPoint = "M_CalDistortionGridV2", Parameters = JsonSerializer.Deserialize<JsonElement>(json),
                Input = new { image.cols, image.rows, image.depth, image.channels, image.stride },
                TvFormula = "Standard", Point9Formula = "OppositeEdgeMean",
                OpticalMethod = "CenteredProjectiveBrownK1/v2 (uncalibrated model estimate)"
            });
        });
    }

    internal static AlgorithmResult ConvertLocal(GridDistortionResult result, object provenance)
    {
        List<AlgorithmArtifact> artifacts =
        [
            new AlgorithmStructuredDataArtifact("parameters", "ColorVision.GridDistortionEvidence.v1", JsonSerializer.SerializeToElement(provenance)),
            new AlgorithmStructuredDataArtifact("diagnostics", "ColorVision.GridDistortionResult.v2", JsonSerializer.SerializeToElement(result))
        ];
        if (result.Success)
        {
            GridDistortionAnalysis analysis = GridDistortionAnalysis.Calculate(result);
            artifacts.Add(new AlgorithmStructuredDataArtifact("analysis", "ColorVision.GridDistortionAnalysis.v2", JsonSerializer.SerializeToElement(analysis)));
            List<AlgorithmMeasurement> measurements =
            [
                new("水平 TV（标准）", analysis.StandardTv.HorizontalPercent, "%"),
                new("垂直 TV（标准）", analysis.StandardTv.VerticalPercent, "%"),
                new("水平 TV（半值）", analysis.HalfTv.HorizontalPercent, "%"),
                new("垂直 TV（半值）", analysis.HalfTv.VerticalPercent, "%"),
                new("九点上畸变（对边平均）", analysis.ReferencePoint9.TopPercent, "%"),
                new("九点下畸变（对边平均）", analysis.ReferencePoint9.BottomPercent, "%"),
                new("九点左畸变（对边平均）", analysis.ReferencePoint9.LeftPercent, "%"),
                new("九点右畸变（对边平均）", analysis.ReferencePoint9.RightPercent, "%"),
                new("水平梯形（对边平均）", analysis.ReferencePoint9.KeystoneHorizontalPercent, "%"),
                new("垂直梯形（对边平均）", analysis.ReferencePoint9.KeystoneVerticalPercent, "%"),
                new("最大倾斜角", analysis.Geometry.MaximumTiltDegrees, "°"),
                new("最大边长差比例", analysis.Geometry.MaximumEdgeLengthDifferencePercent, "%")
            ];
            if (analysis.Optical.OpticRatioPercent is double optical)
                measurements.Add(new("光学畸变（未标定模型估计）", optical, "%"));
            artifacts.Add(new AlgorithmMeasurementArtifact("distortion", measurements));
            GridDistortionPoint[] points = result.Points.OrderBy(point => point.Row).ThenBy(point => point.Col).ToArray();
            DistortionEvidenceArtifacts.AddPoints(artifacts, LocalId, "grid", points.Select(point => new AlgorithmPoint(point.X, point.Y)).ToArray(), "#FFFF5252",
                result.ExpectedRows, result.ExpectedCols);
        }
        return new AlgorithmResult
        {
            AlgorithmId = new(LocalId), AlgorithmVersion = new(2, 0, 0),
            Status = result.Success ? AlgorithmResultStatus.Succeeded : AlgorithmResultStatus.Failed,
            Artifacts = artifacts, Failures = result.Success ? [] : [new(result.StatusCode, result.Message)],
            Diagnostics = new AlgorithmExecutionDiagnostics
            {
                Messages = result.Warnings.Select(message => new AlgorithmDiagnosticMessage("NativeWarning", message, "warning")).ToArray()
            }
        };
    }
}

internal static class DistortionEvidenceArtifacts
{
    internal static void AddPoints(List<AlgorithmArtifact> artifacts, string algorithmId, string group,
        IReadOnlyList<AlgorithmPoint> points, string color, int rows = 0, int cols = 0)
    {
        List<AlgorithmGeometry> geometries = [];
        for (int index = 0; index < points.Count; index++)
            geometries.Add(new($"{group}.P{index}", AlgorithmGeometryKind.Point, [points[index]]));
        if (rows > 0 && cols > 0 && rows * cols == points.Count)
        {
            for (int row = 0; row < rows; row++)
                geometries.Add(new($"{group}.row{row}", AlgorithmGeometryKind.Polyline, Enumerable.Range(0, cols).Select(col => points[row * cols + col]).ToArray()));
            for (int col = 0; col < cols; col++)
                geometries.Add(new($"{group}.col{col}", AlgorithmGeometryKind.Polyline, Enumerable.Range(0, rows).Select(row => points[row * cols + col]).ToArray()));
        }
        artifacts.Add(new AlgorithmGeometryArtifact(group, AlgorithmCoordinateSpace.Pixel, geometries));
        artifacts.Add(new AlgorithmOverlayArtifact("evidence." + algorithmId + "." + group, AlgorithmOverlayLifetime.Transient,
            geometries.Select(geometry => new AlgorithmOverlayItem(geometry.Id, new(color, null, 1.5))).ToArray()));
    }

    internal static object? LoadedLibrary(string name)
    {
        IntPtr module = GetModuleHandle(name);
        if (module == IntPtr.Zero) return null;
        StringBuilder path = new(32768);
        if (GetModuleFileName(module, path, path.Capacity) == 0) throw new IOException("无法读取已加载算法库路径。");
        return LibraryFile(path.ToString());
    }

    internal static object LibraryFile(string path)
    {
        using FileStream file = File.OpenRead(path);
        return new { Path = path, Sha256 = Convert.ToHexString(SHA256.HashData(file)) };
    }

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string name);
    [DllImport("kernel32.dll", EntryPoint = "GetModuleFileNameW", CharSet = CharSet.Unicode)]
    private static extern int GetModuleFileName(IntPtr module, StringBuilder path, int size);
}
