using ColorVision.ImageEditor.BatchProcessing;
using ColorVision.Engine.Media;
using ColorVision.FileIO;
using ColorVision.Copilot;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

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

    [Fact]
    public async Task ApprovedCopilotToolConvertsARealCvrawAndNeverOverwrites()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"colorvision-copilot-batch-{Guid.NewGuid():N}");
        string sourcePath = Path.Combine(directory, "sample.cvraw");
        string outputDirectory = Path.Combine(directory, "output");
        Directory.CreateDirectory(directory);
        try
        {
            using (CVCIEFile file = new()
            {
                Version = 1,
                FileExtType = CVType.Raw,
                Rows = 2,
                Cols = 3,
                Bpp = 16,
                Channels = 3,
                Gain = 1,
                Exp = new[] { 1f, 1f, 1f },
                Data = Enumerable.Range(0, 18).SelectMany(value => BitConverter.GetBytes((ushort)(value * 100))).ToArray(),
            })
            {
                Assert.True(CVFileUtil.WriteCIEFile(sourcePath, file));
            }

            CopilotConvertBatchImagesTool tool = new();
            CopilotAgentRequest request = new()
            {
                UserText = "批量转换 cvraw 文件为 TIFF",
                Mode = CopilotAgentMode.Auto,
                SearchRootPaths = [directory],
                ReadableLocalDirectoryPaths = [directory],
                WritableLocalRootPaths = [directory],
            };
            var arguments = new Dictionary<string, object?>
            {
                ["sources"] = new[] { sourcePath },
                ["outputDirectory"] = outputDirectory,
                ["format"] = "tiff",
            };
            Assert.True(tool.InputSchema.TryBind(arguments, out CopilotAgentToolInput input, out string bindError), bindError);
            Assert.Equal(CopilotToolApprovalMode.Always, tool.Capability.ApprovalMode);

            CopilotToolResult denied = await tool.ExecuteAsync(request, input, CancellationToken.None);
            Assert.False(denied.Success);
            Assert.Equal(CopilotToolFailureKind.Authorization, denied.FailureKind);

            var approvedTool = (ICopilotFrameworkApprovedTool)tool;
            CopilotToolResult first = await approvedTool.ExecuteApprovedAsync(request, input, CancellationToken.None);
            var progress = new CopilotToolProgressContext();
            CopilotToolResult second = await ((ICopilotFrameworkApprovedProgressReportingTool)tool).ExecuteApprovedWithProgressAsync(
                request,
                input,
                progress,
                CancellationToken.None);

            Assert.True(first.Success, first.ErrorMessage);
            Assert.True(second.Success, second.ErrorMessage);
            string firstOutput = Path.Combine(outputDirectory, "sample.tiff");
            string secondOutput = Path.Combine(outputDirectory, "sample_2.tiff");
            Assert.True(File.Exists(firstOutput));
            Assert.True(File.Exists(secondOutput));
            using Mat converted = Cv2.ImRead(firstOutput, ImreadModes.Unchanged);
            Assert.False(converted.Empty());
            Assert.Equal(2, converted.Rows);
            Assert.Equal(3, converted.Cols);
            Assert.Contains(sourcePath, first.SuccessfullyReadLocalFilePaths, StringComparer.OrdinalIgnoreCase);
            using JsonDocument evidence = JsonDocument.Parse(first.Content);
            Assert.Equal(1, evidence.RootElement.GetProperty("requested").GetInt32());
            Assert.Equal(1, evidence.RootElement.GetProperty("processed").GetInt32());
            Assert.True(evidence.RootElement.GetProperty("results")[0].GetProperty("source_read").GetBoolean());
            Assert.Equal(firstOutput, evidence.RootElement.GetProperty("results")[0].GetProperty("output").GetString());
            Assert.Equal(1, progress.LatestSnapshot?.Completed);
            Assert.Equal(1, progress.LatestSnapshot?.Total);
            Assert.Equal("files", progress.LatestSnapshot?.Unit);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ApprovedCopilotToolSkipsIdentityOutputsInsteadOfFailingTheBatch()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"colorvision-copilot-batch-identity-{Guid.NewGuid():N}");
        string existingTiff = Path.Combine(directory, "already-converted.tiff");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(existingTiff, [0x49, 0x49, 0x2A, 0x00]);
        try
        {
            CopilotConvertBatchImagesTool tool = new();
            CopilotAgentRequest request = new()
            {
                UserText = "批量转换图片为 TIFF",
                Mode = CopilotAgentMode.Auto,
                SearchRootPaths = [directory],
                ReadableLocalDirectoryPaths = [directory],
                WritableLocalRootPaths = [directory],
            };
            Assert.True(tool.InputSchema.TryBind(
                new Dictionary<string, object?>
                {
                    ["sources"] = new[] { directory },
                    ["format"] = "tiff",
                },
                out CopilotAgentToolInput input,
                out string bindError), bindError);

            CopilotToolResult result = await ((ICopilotFrameworkApprovedTool)tool).ExecuteApprovedAsync(
                request,
                input,
                CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Contains("skipped 1", result.Summary, StringComparison.OrdinalIgnoreCase);
            using JsonDocument evidence = JsonDocument.Parse(result.Content);
            Assert.Equal(1, evidence.RootElement.GetProperty("requested").GetInt32());
            Assert.Equal(0, evidence.RootElement.GetProperty("processed").GetInt32());
            Assert.Equal(1, evidence.RootElement.GetProperty("skipped_identity").GetInt32());
            Assert.Equal(existingTiff, evidence.RootElement.GetProperty("skipped_identity_sources")[0].GetString());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CopilotBatchConversionRejectsAnOutputOutsideApprovedRoots()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"colorvision-copilot-scope-{Guid.NewGuid():N}");
        string outsideDirectory = Path.Combine(Path.GetTempPath(), $"colorvision-copilot-outside-{Guid.NewGuid():N}");
        string sourcePath = Path.Combine(directory, "sample.png");
        Directory.CreateDirectory(directory);
        try
        {
            using (Mat source = new(2, 2, MatType.CV_8UC1, Scalar.All(42)))
            {
                Assert.True(Cv2.ImWrite(sourcePath, source));
            }

            CopilotConvertBatchImagesTool tool = new();
            CopilotAgentRequest request = new()
            {
                UserText = "批量转换图片为 TIFF",
                Mode = CopilotAgentMode.Auto,
                SearchRootPaths = [directory],
                WritableLocalRootPaths = [directory],
            };
            Assert.True(tool.InputSchema.TryBind(
                new Dictionary<string, object?>
                {
                    ["sources"] = new[] { sourcePath },
                    ["outputDirectory"] = outsideDirectory,
                    ["format"] = "tiff",
                },
                out CopilotAgentToolInput input,
                out string bindError), bindError);

            CopilotToolResult result = await ((ICopilotFrameworkApprovedTool)tool).ExecuteApprovedAsync(
                request,
                input,
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(CopilotToolFailureKind.Authorization, result.FailureKind);
            Assert.False(Directory.Exists(outsideDirectory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
            if (Directory.Exists(outsideDirectory))
            {
                Directory.Delete(outsideDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CopilotBatchConversionCannotWriteBesideAReadOnlyAddedDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"colorvision-copilot-read-only-{Guid.NewGuid():N}");
        string sourcePath = Path.Combine(directory, "sample.png");
        string outputPath = Path.Combine(directory, "sample.tiff");
        Directory.CreateDirectory(directory);
        try
        {
            using (Mat source = new(2, 2, MatType.CV_8UC1, Scalar.All(42)))
            {
                Assert.True(Cv2.ImWrite(sourcePath, source));
            }

            CopilotConvertBatchImagesTool tool = new();
            CopilotAgentRequest request = new()
            {
                UserText = "把参考目录中的图片转换为 TIFF",
                Mode = CopilotAgentMode.Auto,
                SearchRootPaths = [directory],
                ReadableLocalDirectoryPaths = [directory],
                WritableLocalRootPaths = [],
            };
            Assert.True(tool.InputSchema.TryBind(
                new Dictionary<string, object?>
                {
                    ["sources"] = new[] { sourcePath },
                    ["format"] = "tiff",
                },
                out CopilotAgentToolInput input,
                out string bindError), bindError);

            CopilotToolResult result = await ((ICopilotFrameworkApprovedTool)tool).ExecuteApprovedAsync(
                request,
                input,
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(CopilotToolFailureKind.Authorization, result.FailureKind);
            Assert.Contains("read-only", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(outputPath));
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
