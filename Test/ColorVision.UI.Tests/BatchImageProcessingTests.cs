using ColorVision.ImageEditor.BatchProcessing;
using ColorVision.Engine.Media;
using ColorVision.FileIO;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
}
