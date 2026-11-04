using ColorVision.Algorithms;
using ColorVision.Core;
using System;
using System.Text.Json;

namespace ColorVision.ImageEditor.Algorithms.Evidence;

public static class RgbCrossEvidenceAlgorithm
{
    public const string Id = "rgb-cross.local";

    public static ImageEvidenceAlgorithm Create(RgbCrossRegistrationParameters parameters, RectangleAlgorithmRoi? region = null)
    {
        // Snapshot configuration before the worker starts; edits must not change an in-flight run.
        AlgorithmInvocation invocation = AlgorithmInvocation.Create(DisplayMetrologyIds.RgbCrossRegistration, parameters, region);
        var provenance = new AlgorithmStructuredDataArtifact("parameters", "ColorVision.RgbCrossEvidence.v1",
            JsonSerializer.SerializeToElement(new { Parameters = invocation.Parameters, SearchRegion = region }));
        return new(Id, "十字 RGB · 本地", image => Evaluate(image, invocation, provenance));
    }

    private static AlgorithmResult Evaluate(HImage source, AlgorithmInvocation invocation, AlgorithmStructuredDataArtifact provenance)
    {
        AlgorithmImageFormat format = (source.depth, source.channels) switch
        {
            (8, 3) => AlgorithmImageFormat.Bgr24, (16, 3) => AlgorithmImageFormat.Bgr48, (32, 3) => AlgorithmImageFormat.Bgr96Float,
            (8, 4) => AlgorithmImageFormat.Bgra32, (16, 4) => AlgorithmImageFormat.Bgra64, (32, 4) => AlgorithmImageFormat.Bgra128Float,
            _ => throw new NotSupportedException("十字 RGB 实证需要 8/16/32F 位 BGR/BGRA 彩色原图。")
        };
        if ((long)source.cols * source.rows > DisplayMetrologyProvider.MaximumRgbCrossPixels
            || (long)source.cols * source.rows * format.BytesPerPixel() > 512L * 1024 * 1024)
            throw new ArgumentException("十字 RGB 原图超过像素或内存预算。");
        using AlgorithmImageBuffer image = ImageAlgorithmInputFactory.Copy(source, format);
        AlgorithmResult result = ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = invocation,
            Inputs = [new AlgorithmInput { Name = "source", Image = image, Ownership = AlgorithmInputOwnership.Borrowed, ColorSpace = "relative-device-values" }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        }).AsTask().GetAwaiter().GetResult();
        // Transfer artifact ownership to the evidence result, including channel preview buffers.
        return new AlgorithmResult
        {
            InvocationId = result.InvocationId, AlgorithmId = result.AlgorithmId, AlgorithmVersion = result.AlgorithmVersion,
            Status = result.Status, Failures = result.Failures, Diagnostics = result.Diagnostics,
            Artifacts = [.. result.Artifacts, provenance]
        };
    }
}
