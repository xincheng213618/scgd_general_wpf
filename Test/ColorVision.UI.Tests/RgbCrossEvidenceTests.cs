using ColorVision.Algorithms;
using ColorVision.Core;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.Algorithms.Evidence;
using OpenCvSharp;

namespace ColorVision.UI.Tests;

public sealed class RgbCrossEvidenceTests
{
    [Fact]
    public void EvidenceUsesCurrentRgbMeasurementAndSnapshotsItsParameters()
    {
        using Mat pixels = new(320, 320, MatType.CV_16UC3, Scalar.All(0));
        for (int y = 80; y <= 240; y++)
            for (int x = 78; x <= 242; x++)
            {
                ushort Value(int shift) => Math.Abs(x - 160 - shift) <= 2 || Math.Abs(y - 160) <= 2 && Math.Abs(x - 160 - shift) <= 80 ? (ushort)50000 : (ushort)0;
                pixels.Set(y, x, new Vec3w(Value(-2), Value(0), Value(1)));
            }
        HImage image = new() { cols = pixels.Width, rows = pixels.Height, channels = 3, depth = 16, stride = (int)pixels.Step(), pData = pixels.Data, isDispose = true };
        var parameters = new RgbCrossRegistrationParameters { Rows = 1, Columns = 1 };
        ImageEvidenceAlgorithm algorithm = RgbCrossEvidenceAlgorithm.Create(parameters, new RectangleAlgorithmRoi(30, 30, 260, 260));
        parameters.Rows = 3;
        using AlgorithmResult result = algorithm.Evaluate(image);
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        var row = Assert.Single(result.GetArtifact<AlgorithmTableArtifact>("RGB-cross-separation")!.Rows);
        Assert.True(row["valid"].GetBoolean());
        Assert.Equal(1, row["rToGMaximumEdge_px"].GetDouble());
        Assert.Equal(2, row["bToGMaximumEdge_px"].GetDouble());
        Assert.Equal(DisplayMetrologyIds.RgbCrossRegistration, result.AlgorithmId);
        var provenance = result.GetArtifact<AlgorithmStructuredDataArtifact>("parameters")!.Data;
        Assert.Equal(1, provenance.GetProperty("Parameters").GetProperty("rows").GetInt32());
        Assert.Equal(30, provenance.GetProperty("SearchRegion").GetProperty("X").GetInt32());
        Assert.Equal((ushort)50000, pixels.At<Vec3w>(160, 160).Item1);
    }
}
