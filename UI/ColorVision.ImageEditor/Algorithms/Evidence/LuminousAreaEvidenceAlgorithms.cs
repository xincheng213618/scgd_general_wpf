using ColorVision.Algorithms;
using ColorVision.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ColorVision.ImageEditor.Algorithms.Evidence;

public static class LuminousAreaEvidenceAlgorithms
{
    public static IReadOnlyList<ImageEvidenceAlgorithm> Create() =>
    [
        Create("luminous.robust-v2", "发光区 · RobustV2", "#FFFF5252", new { MinConfidence = 0.25 }, image => LuminousAreaNative.DetectV2(image, new RoiRect(), 0.25)),
        Create("luminous.legacy", "发光区 · 经典兼容（阈值20）", "#FF00BFFF", new { Threshold = 20, UseRotatedRect = true }, image => LuminousAreaNative.DetectLegacy(image, new RoiRect(), 20, true))
    ];

    private static ImageEvidenceAlgorithm Create(string id, string name, string color, object parameters, Func<HImage, LuminousAreaDetectionResult> detect) =>
        new(id, name, image => Convert(id, color, parameters, detect(image)));

    private static AlgorithmResult Convert(string id, string color, object parameters, LuminousAreaDetectionResult result)
    {
        List<AlgorithmArtifact> artifacts =
        [
            new AlgorithmStructuredDataArtifact("diagnostics", "ColorVision.LuminousEvidence.v1", JsonSerializer.SerializeToElement(result)),
            new AlgorithmStructuredDataArtifact("parameters", "ColorVision.LuminousEvidenceParameters.v1", JsonSerializer.SerializeToElement(parameters))
        ];
        if (result.Confidence is double confidence)
            artifacts.Add(new AlgorithmMeasurementArtifact("quality", [new AlgorithmMeasurement("Confidence", confidence)]));
        // Rejected diagnostic corners remain in the report, but are never drawn as a valid boundary.
        if (result.HasValidCorners)
        {
            artifacts.Add(new AlgorithmGeometryArtifact("boundary", AlgorithmCoordinateSpace.Pixel,
                [new AlgorithmGeometry("boundary", AlgorithmGeometryKind.Polygon,
                    result.Corners.Select(point => new AlgorithmPoint(point.X, point.Y)).ToArray())]));
            artifacts.Add(new AlgorithmOverlayArtifact("evidence." + id, AlgorithmOverlayLifetime.Transient,
                [new AlgorithmOverlayItem("boundary", new AlgorithmOverlayStyle(color, null, 2))]));
        }
        return new AlgorithmResult
        {
            AlgorithmId = new AlgorithmId(id),
            AlgorithmVersion = new AlgorithmVersion(id.EndsWith("v2", StringComparison.Ordinal) ? 2 : 1, 0, 0),
            Status = result.HasValidCorners ? AlgorithmResultStatus.Succeeded : AlgorithmResultStatus.Failed,
            Artifacts = artifacts,
            Failures = result.HasValidCorners ? [] : [new AlgorithmFailure(result.FailureReason, LuminousAreaDetector.GetFailureMessage(result))],
            Diagnostics = new AlgorithmExecutionDiagnostics
            {
                Messages = result.Warnings.Select(warning => new AlgorithmDiagnosticMessage(warning, warning, "warning")).ToArray()
            }
        };
    }
}
