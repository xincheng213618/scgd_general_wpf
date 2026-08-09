using Conoscope.Core;
using OpenCvSharp;

namespace Conoscope.Tests;

public class ConoscopeColorimetryTests
{
    [Fact]
    public void ScalarReferenceColorDifferenceMatchesPointCalculation()
    {
        using Mat x = Mat.FromArray(new float[,] { { 1, 2, 0 }, { 4, 5, 6 } });
        using Mat y = Mat.FromArray(new float[,] { { 3, 4, 0 }, { 6, 7, 8 } });
        using Mat z = Mat.FromArray(new float[,] { { 5, 6, 0 }, { 8, 9, 10 } });
        const double referenceU = 0.1978;
        const double referenceV = 0.4684;

        using Mat result = ConoscopeColorimetry.CreateColorDifferenceMat(x, y, z, referenceU, referenceV);

        int rows = x.Rows;
        int columns = x.Cols;
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                double expected = ConoscopeColorimetry.CalculateColorDifference(
                    x.At<float>(row, column),
                    y.At<float>(row, column),
                    z.At<float>(row, column),
                    referenceU,
                    referenceV);
                Assert.Equal(expected, result.At<float>(row, column), precision: 5);
            }
        }
    }

    [Fact]
    public void ImageReferenceColorDifferenceUsesPerPixelReferences()
    {
        using Mat x = new(2, 3, MatType.CV_32FC1, Scalar.All(2));
        using Mat y = new(2, 3, MatType.CV_32FC1, Scalar.All(3));
        using Mat z = new(2, 3, MatType.CV_32FC1, Scalar.All(4));
        ConoscopeChromaticity chromaticity = ConoscopeColorimetry.Calculate(2, 3, 4);
        using Mat referenceU = new(2, 3, MatType.CV_32FC1, Scalar.All(chromaticity.u));
        using Mat referenceV = new(2, 3, MatType.CV_32FC1, Scalar.All(chromaticity.v));

        using Mat result = ConoscopeColorimetry.CreateColorDifferenceMat(x, y, z, referenceU, referenceV);

        Assert.InRange(Math.Abs(result.At<float>(0, 0)), 0, 1e-6);
        Assert.InRange(Math.Abs(result.At<float>(1, 2)), 0, 1e-6);
    }

    [Theory]
    [InlineData(ContrastReferenceKind.Black, 4, 2, 2)]
    [InlineData(ContrastReferenceKind.White, 4, 2, 0.5)]
    [InlineData(ContrastReferenceKind.Black, 4, 0, 0)]
    [InlineData(ContrastReferenceKind.White, 0, 2, 0)]
    public void ContrastMatrixMatchesScalarZeroGuard(
        ContrastReferenceKind kind,
        float current,
        float reference,
        double expected)
    {
        using Mat currentMat = new(1, 1, MatType.CV_32FC1, Scalar.All(current));
        using Mat referenceMat = new(1, 1, MatType.CV_32FC1, Scalar.All(reference));

        using Mat result = ConoscopeColorimetry.CreateContrastMat(currentMat, referenceMat, kind);

        Assert.Equal(expected, result.At<float>(0, 0), precision: 6);
    }

    [Fact]
    public void ChromaticityMatrixKeepsLegacyInvalidDenominatorBehavior()
    {
        using Mat x = Mat.FromArray(new float[,] { { 0, -16, float.NaN } });
        using Mat y = Mat.FromArray(new float[,] { { 0, 1, 1 } });
        using Mat z = Mat.FromArray(new float[,] { { 0, 0, 0 } });

        using Mat result = ConoscopeColorimetry.CreateChannelMat(x, y, z, ExportChannel.CieU);

        Assert.Equal(0, result.At<float>(0, 0));
        Assert.Equal(0, result.At<float>(0, 1));
        Assert.True(float.IsNaN(result.At<float>(0, 2)));
    }

    [Fact]
    public void ImageReferenceColorDifferenceAcceptsConvertibleReferenceType()
    {
        using Mat x = new(1, 1, MatType.CV_32FC1, Scalar.All(2));
        using Mat y = new(1, 1, MatType.CV_32FC1, Scalar.All(3));
        using Mat z = new(1, 1, MatType.CV_32FC1, Scalar.All(4));
        ConoscopeChromaticity chromaticity = ConoscopeColorimetry.Calculate(2, 3, 4);
        using Mat referenceU = new(1, 1, MatType.CV_64FC1, Scalar.All(chromaticity.u));
        using Mat referenceV = new(1, 1, MatType.CV_64FC1, Scalar.All(chromaticity.v));

        using Mat result = ConoscopeColorimetry.CreateColorDifferenceMat(x, y, z, referenceU, referenceV);

        Assert.InRange(Math.Abs(result.At<float>(0, 0)), 0, 1e-6);
    }
}
