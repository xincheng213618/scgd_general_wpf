using ColorVision.ImageEditor.BatchProcessing;
using ColorVision.Engine.Media;
using ColorVision.FileIO;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace ColorVision.UI.Tests;

public class BatchImageProcessingTests
{
    [Theory]
    [InlineData("image.png", ".png")]
    [InlineData("image.tiff", ".tiff")]
    [InlineData("image.cvraw", ".tiff")]
    [InlineData("image.cvcie", ".tiff")]
    public void SameAsSourceUsesExpectedExtension(string sourcePath, string expected)
    {
        Assert.Equal(expected, BatchImageOutput.ResolveExtension(sourcePath, BatchOutputFormat.SameAsSource));
    }

    [Fact]
    public void OutputPathPreservesFolderStructureAndAddsSuffix()
    {
        string root = Path.Combine(Path.GetTempPath(), "batch-source");
        string output = Path.Combine(Path.GetTempPath(), "batch-output");
        BatchImageItem item = new(Path.Combine(root, "group", "sample.cvraw"), root);
        HashSet<string> reserved = new(StringComparer.OrdinalIgnoreCase);

        string actual = BatchImageOutput.CreateOutputPath(
            item,
            output,
            "_invert",
            BatchOutputFormat.SameAsSource,
            preserveFolderStructure: true,
            avoidOverwrite: true,
            reservedPaths: reserved);

        Assert.Equal(Path.Combine(output, "group", "sample_invert.tiff"), actual);
    }

    [Fact]
    public void OutputPathAvoidsOverwritingAnExistingResult()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"colorvision-batch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string sourcePath = Path.Combine(directory, "sample.png");
            string existingPath = Path.Combine(directory, "sample_invert.png");
            File.WriteAllBytes(existingPath, new byte[] { 1 });
            BatchImageItem item = new(sourcePath);

            string actual = BatchImageOutput.CreateOutputPath(
                item,
                outputDirectory: null,
                suffix: "_invert",
                format: BatchOutputFormat.SameAsSource,
                preserveFolderStructure: true,
                avoidOverwrite: true,
                reservedPaths: new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            Assert.Equal(Path.Combine(directory, "sample_invert_2.png"), actual);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void EveryCatalogAlgorithmProcessesAnEightBitColorImage()
    {
        using Mat source = new(24, 32, MatType.CV_8UC3);
        Cv2.Randu(source, Scalar.All(0), Scalar.All(256));

        foreach (BatchImageAlgorithmDefinition algorithm in BatchImageAlgorithms.CreateAll())
        {
            using Mat result = algorithm.Apply(source);
            Assert.False(result.Empty());
            Assert.Equal(source.Rows, result.Rows);
            Assert.Equal(source.Cols, result.Cols);
        }
    }

    [Fact]
    public void FirstCatalogAlgorithmPerformsFormatOnlyConversionWithoutChangingPixels()
    {
        using Mat source = new(2, 3, MatType.CV_16UC1);
        source.SetTo(Scalar.All(1234));
        BatchImageAlgorithmDefinition algorithm = BatchImageAlgorithms.CreateAll()[0];

        using Mat result = algorithm.Apply(source);

        Assert.Equal("仅转换格式", algorithm.Name);
        Assert.Equal(string.Empty, algorithm.Suffix);
        Assert.NotSame(source, result);
        Assert.Equal(source.Type(), result.Type());
        Assert.Equal((ushort)1234, result.At<ushort>(0, 0));
    }

    [Fact]
    public void EveryCatalogAlgorithmProcessesASixteenBitColorImage()
    {
        using Mat source = new(24, 32, MatType.CV_16UC3);
        Cv2.Randu(source, Scalar.All(0), Scalar.All(ushort.MaxValue));

        foreach (BatchImageAlgorithmDefinition algorithm in BatchImageAlgorithms.CreateAll())
        {
            using Mat result = algorithm.Apply(source);
            Assert.False(result.Empty());
            Assert.Equal(source.Rows, result.Rows);
            Assert.Equal(source.Cols, result.Cols);
        }
    }

    [Fact]
    public void BasicAdjustmentDefaultsPreservePixels()
    {
        using Mat source = Mat.FromArray(new byte[,] { { 0, 64, 128, 255 } });
        BatchImageAlgorithmDefinition algorithm = CreateBasicAdjustmentAlgorithm();

        using Mat result = algorithm.Apply(source);

        for (int column = 0; column < source.Cols; column++)
        {
            Assert.Equal(source.At<byte>(0, column), result.At<byte>(0, column));
        }
    }

    [Fact]
    public void ExposureAdjustmentUsesGainWithoutLiftingBlack()
    {
        using Mat source = Mat.FromArray(new byte[,] { { 0, 64, 128 } });
        BatchImageAlgorithmDefinition algorithm = CreateBasicAdjustmentAlgorithm(exposure: 1);

        using Mat result = algorithm.Apply(source);

        Assert.Equal((byte)0, result.At<byte>(0, 0));
        Assert.Equal((byte)128, result.At<byte>(0, 1));
        Assert.Equal(byte.MaxValue, result.At<byte>(0, 2));
    }

    [Fact]
    public void BrightnessOffsetExplicitlyRaisesTheBlackLevel()
    {
        using Mat source = Mat.FromArray(new byte[,] { { 0, 64, 128 } });
        BatchImageAlgorithmDefinition algorithm = CreateBasicAdjustmentAlgorithm(brightness: 25);

        using Mat result = algorithm.Apply(source);

        Assert.Equal((byte)64, result.At<byte>(0, 0));
        Assert.Equal((byte)128, result.At<byte>(0, 1));
        Assert.Equal((byte)192, result.At<byte>(0, 2));
    }

    [Fact]
    public void ContrastAdjustmentUsesTheMidpointAsItsPivot()
    {
        using Mat source = Mat.FromArray(new byte[,] { { 0, 128, 255 } });
        BatchImageAlgorithmDefinition algorithm = CreateBasicAdjustmentAlgorithm(contrast: -50);

        using Mat result = algorithm.Apply(source);

        Assert.Equal((byte)64, result.At<byte>(0, 0));
        Assert.Equal((byte)128, result.At<byte>(0, 1));
        Assert.Equal((byte)191, result.At<byte>(0, 2));
    }

    [Fact]
    public void BasicAdjustmentPreservesAlpha()
    {
        using Mat source = new(1, 1, MatType.CV_8UC4);
        source.Set(0, 0, new Vec4b(10, 20, 30, 40));
        BatchImageAlgorithmDefinition algorithm = CreateBasicAdjustmentAlgorithm(exposure: 1);

        using Mat result = algorithm.Apply(source);
        Vec4b pixel = result.At<Vec4b>(0, 0);

        Assert.Equal((byte)20, pixel.Item0);
        Assert.Equal((byte)40, pixel.Item1);
        Assert.Equal((byte)60, pixel.Item2);
        Assert.Equal((byte)40, pixel.Item3);
    }

    [Fact]
    public void FloatExposureAdjustmentUsesTheNormalizedRange()
    {
        using Mat source = Mat.FromArray(new float[,] { { 0, 0.25f, 0.75f } });
        BatchImageAlgorithmDefinition algorithm = CreateBasicAdjustmentAlgorithm(exposure: 1);

        using Mat result = algorithm.Apply(source);

        Assert.Equal(0, result.At<float>(0, 0), precision: 6);
        Assert.Equal(0.5f, result.At<float>(0, 1), precision: 6);
        Assert.Equal(1, result.At<float>(0, 2), precision: 6);
    }

    [Fact]
    public void SixteenBitExposureAdjustmentUsesTheFullRange()
    {
        using Mat source = Mat.FromArray(new ushort[,] { { 0, 16384, 32768 } });
        BatchImageAlgorithmDefinition algorithm = CreateBasicAdjustmentAlgorithm(exposure: 1);

        using Mat result = algorithm.Apply(source);

        Assert.Equal((ushort)0, result.At<ushort>(0, 0));
        Assert.Equal((ushort)32768, result.At<ushort>(0, 1));
        Assert.Equal(ushort.MaxValue, result.At<ushort>(0, 2));
    }

    [Fact]
    public void GammaAdjustmentChangesMidtonesAndKeepsEndpoints()
    {
        using Mat source = Mat.FromArray(new float[,] { { 0, 0.25f, 1 } });
        BatchImageAlgorithmDefinition algorithm = CreateBasicAdjustmentAlgorithm(gamma: 2);

        using Mat result = algorithm.Apply(source);

        Assert.Equal(0, result.At<float>(0, 0), precision: 6);
        Assert.Equal(0.5f, result.At<float>(0, 1), precision: 6);
        Assert.Equal(1, result.At<float>(0, 2), precision: 6);
    }

    [Fact]
    public void SavingEightBitPngPreservesPixelValues()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"colorvision-batch-{Guid.NewGuid():N}");
        string filePath = Path.Combine(directory, "result.png");
        try
        {
            using Mat source = new(2, 2, MatType.CV_8UC1, Scalar.All(42));
            BatchImageOutput.Save(source, filePath);
            using Mat loaded = Cv2.ImRead(filePath, ImreadModes.Unchanged);

            Assert.Equal(42, loaded.At<byte>(0, 0));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void SavingNonContinuousPngPreservesRoiPixelsAndSource()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"colorvision-batch-{Guid.NewGuid():N}");
        string filePath = Path.Combine(directory, "roi.png");
        try
        {
            using Mat source = new(5, 7, MatType.CV_8UC3);
            Cv2.Randu(source, Scalar.All(0), Scalar.All(256));
            using Mat original = source.Clone();
            using Mat roi = new(source, new Rect(2, 1, 3, 3));
            using Mat expected = roi.Clone();
            Assert.False(roi.IsContinuous());

            BatchImageOutput.Save(roi, filePath);

            using Mat loaded = Cv2.ImRead(filePath, ImreadModes.Unchanged);
            AssertMatsEqual(expected, loaded);
            AssertMatsEqual(original, source);
            Assert.Equal(expected.At<Vec3b>(1, 1), roi.At<Vec3b>(1, 1));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void SavingFourChannelJpegDropsAlphaWithoutChangingSource()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"colorvision-batch-{Guid.NewGuid():N}");
        string filePath = Path.Combine(directory, "result.jpg");
        try
        {
            using Mat source = new(8, 8, MatType.CV_8UC4);
            Cv2.Randu(source, Scalar.All(0), Scalar.All(256));
            using Mat original = source.Clone();

            BatchImageOutput.Save(source, filePath);

            using Mat loaded = Cv2.ImRead(filePath, ImreadModes.Unchanged);
            Assert.Equal(3, loaded.Channels());
            AssertMatsEqual(original, source);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void SavingFloatPngNormalizesToSixteenBitWithoutChangingSource()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"colorvision-batch-{Guid.NewGuid():N}");
        string filePath = Path.Combine(directory, "normalized.png");
        try
        {
            using Mat source = Mat.FromArray(new float[,] { { -2, 0, 2 }, { 4, 8, 16 } });
            using Mat original = source.Clone();
            using Mat normalized = new();
            Cv2.Normalize(source, normalized, 0, ushort.MaxValue, NormTypes.MinMax);
            using Mat expected = new();
            normalized.ConvertTo(expected, MatType.CV_16U);

            BatchImageOutput.Save(source, filePath);

            using Mat loaded = Cv2.ImRead(filePath, ImreadModes.Unchanged);
            AssertMatsEqual(expected, loaded);
            AssertMatsEqual(original, source);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(4, false)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    public void HistogramEqualizationMatchesLegacyOutput(int channels, bool sixteenBit)
    {
        MatType type = MatType.MakeType(sixteenBit ? MatType.CV_16U : MatType.CV_8U, channels);
        using Mat source = new(19, 23, type);
        Cv2.Randu(source, Scalar.All(0), Scalar.All(sixteenBit ? ushort.MaxValue + 1d : byte.MaxValue + 1d));
        using Mat original = source.Clone();
        using Mat expected = ApplyLegacyHistogramEqualization(source);
        BatchImageAlgorithmDefinition algorithm = BatchImageAlgorithms.CreateAll()
            .Single(item => item.Name == "直方图均衡化");

        using Mat actual = algorithm.Apply(source);

        AssertMatsEqual(expected, actual);
        AssertMatsEqual(original, source);
    }

    [Theory]
    [InlineData("sample.cvraw")]
    [InlineData("sample.cvcie")]
    public void ColorVisionLoaderReadsARealSerializedFile(string fileName)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"colorvision-batch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string filePath = Path.Combine(directory, fileName);
        try
        {
            using CVCIEFile file = new()
            {
                Version = 1,
                FileExtType = fileName.EndsWith(".cvraw", StringComparison.OrdinalIgnoreCase) ? CVType.Raw : CVType.CIE,
                Rows = 2,
                Cols = 3,
                Bpp = 8,
                Channels = 3,
                Gain = 1,
                Exp = new[] { 1f, 1f, 1f },
                Data = Enumerable.Range(0, 18).Select(value => (byte)value).ToArray(),
            };
            Assert.True(CVFileUtil.WriteCIEFile(filePath, file));

            CVRawBatchImageLoader loader = new();
            using Mat loaded = loader.Load(filePath);

            Assert.False(loaded.Empty());
            Assert.Equal(2, loaded.Rows);
            Assert.Equal(3, loaded.Cols);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static BatchImageAlgorithmDefinition CreateBasicAdjustmentAlgorithm(
        double exposure = 0,
        double brightness = 0,
        double contrast = 0,
        double gamma = 1)
    {
        BatchImageAlgorithmDefinition algorithm = BatchImageAlgorithms.CreateAll()
            .Single(item => item.Name == "基础调整");
        Type optionsType = algorithm.Options.GetType();
        optionsType.GetProperty("Exposure")!.SetValue(algorithm.Options, exposure);
        optionsType.GetProperty("Brightness")!.SetValue(algorithm.Options, brightness);
        optionsType.GetProperty("Contrast")!.SetValue(algorithm.Options, contrast);
        optionsType.GetProperty("Gamma")!.SetValue(algorithm.Options, gamma);
        return algorithm;
    }

    private static Mat ApplyLegacyHistogramEqualization(Mat source)
    {
        using Mat source8 = ConvertTo8BitLegacy(source);
        using Mat bgr = new();
        if (source8.Channels() == 3)
        {
            source8.CopyTo(bgr);
        }
        else
        {
            Cv2.CvtColor(source8, bgr, ColorConversionCodes.BGRA2BGR);
        }

        using Mat yCrCb = new();
        Cv2.CvtColor(bgr, yCrCb, ColorConversionCodes.BGR2YCrCb);
        Mat[] channels = Cv2.Split(yCrCb);
        try
        {
            Cv2.EqualizeHist(channels[0], channels[0]);
            using Mat merged = new();
            Cv2.Merge(channels, merged);
            Mat result = new();
            Cv2.CvtColor(merged, result, ColorConversionCodes.YCrCb2BGR);
            return result;
        }
        finally
        {
            foreach (Mat channel in channels)
            {
                channel.Dispose();
            }
        }
    }

    private static Mat ConvertTo8BitLegacy(Mat source)
    {
        if (source.Depth() == MatType.CV_8U)
        {
            return source.Clone();
        }

        using Mat normalized = new();
        Cv2.Normalize(source, normalized, 0, byte.MaxValue, NormTypes.MinMax);
        Mat result = new();
        normalized.ConvertTo(result, MatType.CV_8U);
        return result;
    }

    private static void AssertMatsEqual(Mat expected, Mat actual)
    {
        Assert.Equal(expected.Type(), actual.Type());
        Assert.Equal(expected.Rows, actual.Rows);
        Assert.Equal(expected.Cols, actual.Cols);
        Assert.True(expected.IsContinuous());
        Assert.True(actual.IsContinuous());

        int length = checked((int)(expected.Total() * expected.ElemSize()));
        byte[] expectedBytes = new byte[length];
        byte[] actualBytes = new byte[length];
        Marshal.Copy(expected.Data, expectedBytes, 0, length);
        Marshal.Copy(actual.Data, actualBytes, 0, length);
        Assert.Equal(expectedBytes, actualBytes);
    }
}
