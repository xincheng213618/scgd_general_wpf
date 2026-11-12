using ColorVision.Algorithms;
using ColorVision.Core;
using ColorVision.ImageEditor.Algorithms.Evidence;
using OpenCvSharp;

namespace ColorVision.UI.Tests;

// Protect the vendor ABI boundary: malformed/rejected output must never become a valid overlay,
// and a DLL accepting mutable, packed pixels must never receive the shared evidence snapshot.
public sealed class ServerAaLuminousEvidenceTests
{
    private const string Corners = """{"corner":[{"x":1,"y":2},{"x":8,"y":2},{"x":8,"y":7},{"x":1,"y":7}]}""";

    [Fact]
    public void SuccessfulReplyProducesPixelBoundaryAndRetainsNativeEvidence()
    {
        using AlgorithmResult result = ServerAaLuminousEvidenceAlgorithm.ConvertReply(1, Corners, 10, 10, new { Template = "test" });
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        AlgorithmGeometry boundary = Assert.Single(Assert.Single(result.Artifacts.OfType<AlgorithmGeometryArtifact>()).Geometries);
        Assert.Equal([new AlgorithmPoint(1, 2), new(8, 2), new(8, 7), new(1, 7)], boundary.Points);
        Assert.Single(result.Artifacts.OfType<AlgorithmOverlayArtifact>());
        Assert.Equal(2, result.Artifacts.OfType<AlgorithmStructuredDataArtifact>().Count());
    }

    [Theory]
    [InlineData(0, Corners, 10)]
    [InlineData(-1, Corners, 10)]
    [InlineData(1, Corners, 5)]
    [InlineData(1, "{}", 10)]
    [InlineData(1, "broken", 10)]
    [InlineData(1, "{\"corner\":[{\"x\":0,\"y\":0}]}", 10)]
    [InlineData(1, "{\"corner\":[{\"x\":1,\"y\":2},{\"x\":1,\"y\":7},{\"x\":8,\"y\":7},{\"x\":8,\"y\":2}]}", 10)]
    public void RejectedOrMalformedReplyRetainsDiagnosticsWithoutDrawing(int code, string json, int width)
    {
        using AlgorithmResult result = ServerAaLuminousEvidenceAlgorithm.ConvertReply(code, json, width, 10, new { });
        Assert.Equal(AlgorithmResultStatus.Failed, result.Status);
        Assert.Single(result.Failures);
        Assert.Empty(result.Artifacts.OfType<AlgorithmGeometryArtifact>());
        Assert.Empty(result.Artifacts.OfType<AlgorithmOverlayArtifact>());
        Assert.Equal(2, result.Artifacts.OfType<AlgorithmStructuredDataArtifact>().Count());
    }

    [Fact]
    public void VendorInputIsPackedPrivateBgrAndPreservesSixteenBitValues()
    {
        using Mat allocation = new(2, 4, MatType.CV_16UC4, new Scalar(1000, 2000, 3000, 65535));
        using Mat source = allocation.SubMat(new Rect(0, 0, 3, 2));
        using Mat copy = ServerAaLuminousEvidenceAlgorithm.CopyInput(new HImage
        {
            cols = source.Width, rows = source.Height, channels = 4, depth = 16,
            stride = (int)source.Step(), pData = source.Data, isDispose = true
        });
        Assert.Equal(MatType.CV_16UC3, copy.Type());
        Assert.Equal(18, copy.Step());
        Assert.Equal(new Vec3w(1000, 2000, 3000), copy.At<Vec3w>(1, 2));
        copy.Set(1, 2, new Vec3w(0, 0, 0));
        Assert.Equal(new Vec4w(1000, 2000, 3000, 65535), source.At<Vec4w>(1, 2));
    }
}
