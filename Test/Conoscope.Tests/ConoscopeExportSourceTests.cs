using Conoscope.Core;
using Conoscope.ApplicationServices.Preprocess;
using Conoscope.Processing.Preprocess;
using OpenCvSharp;

namespace Conoscope.Tests;

public sealed class ConoscopeExportSourceTests
{
    [Fact]
    public void NativeHeadersSurviveSourceDisposalWithoutCopyingLargeBuffers()
    {
        using Mat x = new(3, 3, MatType.CV_32FC1, Scalar.All(1));
        using Mat y = new(3, 3, MatType.CV_32FC1, Scalar.All(2));
        using Mat z = new(3, 3, MatType.CV_32FC1, Scalar.All(3));
        using Mat reference = new(3, 3, MatType.CV_32FC1, Scalar.All(10));
        using var source = new ConoscopeExportSource("VA60", new(1, 1), 60, 1, x, y, z,
            ColorDifferenceReferenceMode.D65, new(.2, .4), null, null, ContrastReferenceKind.White, reference);
        x.Dispose(); y.Dispose(); z.Dispose(); reference.Dispose();
        Assert.Equal(new ConoscopeXyzValue(1, 2, 3), source.Context.ReadXyz(1, 1));
        Assert.Equal(ConoscopeColorimetry.CalculateContrast(2, 10, ContrastReferenceKind.White), source.Context.ReadContrast!(1, 1));
        Assert.Equal(ConoscopeColorimetry.CalculateColorDifference(1, 2, 3, .2, .4), source.Context.ReadColorDifference!(1, 1));
    }

    [Fact]
    public void PreprocessingReplacesPublishedBuffersWithoutChangingAnActiveExport()
    {
        Mat? x = new(5, 5, MatType.CV_32FC1, Scalar.All(1));
        Mat? y = new(5, 5, MatType.CV_32FC1, Scalar.All(2));
        Mat? z = new(5, 5, MatType.CV_32FC1, Scalar.All(3));
        y.Set(2, 2, 100f);
        using var source = new ConoscopeExportSource("VA60", new(2, 2), 60, 1, x, y, z,
            ColorDifferenceReferenceMode.D65, new(.2, .4), null, null, ContrastReferenceKind.Black, null);
        try
        {
            var options = new ConoscopePreprocessOptions(false, .000001f, false,
                new DustRemovalOptions(DustRemovalMode.DarkSpot, 12, 1, 500, 3), new ImageFilterOptions(ImageFilterType.Gaussian, 3, 1, 1, 1, 1));
            ConoscopePreprocessPipeline.Apply(ref x, ref y, ref z, options);
            Assert.True(y!.At<float>(2, 2) < 100);
            Assert.Equal(100, source.Context.ReadXyz(2, 2).Y);
        }
        finally { x?.Dispose(); y?.Dispose(); z?.Dispose(); }
    }
}
