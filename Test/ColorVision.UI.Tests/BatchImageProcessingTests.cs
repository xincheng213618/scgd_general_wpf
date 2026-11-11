using ColorVision.ImageEditor.BatchProcessing;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.Engine.Media;
using ColorVision.FileIO;
using OpenCvSharp;
using System.IO;
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
    public void OverwritingBatchOutputsPreservesOtherSelectedSourceFiles()
    {
        string directory = Directory.CreateTempSubdirectory("ColorVision-BatchSourceCollision-").FullName;
        try
        {
            string firstPath = Path.Combine(directory, "capture.png");
            string secondPath = Path.Combine(directory, "capture_processed.png");
            string previousOutput = Path.Combine(directory, "capture_processed_2.png");
            using Mat first = Mat.FromArray(new byte[,] { { 10, 20 } });
            using Mat second = Mat.FromArray(new byte[,] { { 30, 40 } });
            BatchImageOutput.Save(first, firstPath);
            BatchImageOutput.Save(second, secondPath);
            File.WriteAllBytes(previousOutput, [1, 2, 3]);
            byte[] originalSecondFile = File.ReadAllBytes(secondPath);
            var processor = new BatchImageProcessor([new StandardBatchImageLoader()]);

            BatchImageRunResult run = processor.Process(new BatchImageProcessingRequest
            {
                Items = [new BatchImageItem(firstPath), new BatchImageItem(Path.Combine(directory, ".", "capture_processed.png"))],
                Algorithm = BatchImageAlgorithms.CreateAll()[0],
                Suffix = "_processed",
                AvoidOverwrite = false,
            });

            Assert.Equal(originalSecondFile, File.ReadAllBytes(secondPath));
            Assert.All(run.Files, result => Assert.True(result.Success, result.ErrorMessage));
            Assert.Equal(previousOutput, run.Files[0].OutputPath);
            using Mat firstOutput = Cv2.ImRead(previousOutput, ImreadModes.Unchanged);
            using Mat secondOutput = Cv2.ImRead(run.Files[1].OutputPath, ImreadModes.Unchanged);
            AssertMatsEqual(first, firstOutput);
            AssertMatsEqual(second, secondOutput);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BatchOutputProtectsSourceFilesWhenEitherDirectoryUsesALink(bool sourceUsesLink)
    {
        DirectoryInfo root = Directory.CreateTempSubdirectory("ColorVision-BatchOutputLink-");
        DirectoryInfo actual = root.CreateSubdirectory("actual");
        DirectoryInfo nested = actual.CreateSubdirectory("nested");
        string link = Path.Combine(root.FullName, "alias");
        string realFirst = Path.Combine(nested.FullName, "capture.png");
        string realSecond = Path.Combine(nested.FullName, "capture_processed.png");
        try
        {
            try { Directory.CreateSymbolicLink(link, actual.FullName); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return; }
            using Mat first = Mat.FromArray(new byte[,] { { 10, 20 } });
            using Mat second = Mat.FromArray(new byte[,] { { 30, 40 } });
            BatchImageOutput.Save(first, realFirst);
            BatchImageOutput.Save(second, realSecond);
            byte[] original = File.ReadAllBytes(realSecond);
            string linkedDirectory = Path.Combine(link, nested.Name);
            string inputDirectory = sourceUsesLink ? linkedDirectory : nested.FullName;
            string outputDirectory = sourceUsesLink ? nested.FullName : linkedDirectory;

            var processor = new BatchImageProcessor([new StandardBatchImageLoader()]);
            BatchImageRunResult result = processor.Process(new BatchImageProcessingRequest
            {
                Items = [new BatchImageItem(Path.Combine(inputDirectory, Path.GetFileName(realFirst))),
                    new BatchImageItem(Path.Combine(inputDirectory, Path.GetFileName(realSecond)))],
                Algorithm = BatchImageAlgorithms.CreateAll()[0],
                OutputDirectory = outputDirectory,
                Suffix = "_processed",
                AvoidOverwrite = false,
            });

            Assert.Equal(original, File.ReadAllBytes(realSecond));
            Assert.All(result.Files, item => Assert.True(item.Success, item.ErrorMessage));
            Assert.Equal(Path.Combine(outputDirectory, "capture_processed_2.png"), result.Files[0].OutputPath);
            using Mat output = Cv2.ImRead(result.Files[0].OutputPath, ImreadModes.Unchanged);
            AssertMatsEqual(first, output);
        }
        finally
        {
            if (Directory.Exists(link)) Directory.Delete(link);
            foreach (FileInfo file in nested.EnumerateFiles()) file.Delete();
            nested.Delete();
            actual.Delete();
            root.Delete();
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

        Assert.Equal(ColorVision.ImageEditor.Properties.Resources.BatchFormatOnly, algorithm.Name);
        Assert.Equal(string.Empty, algorithm.Suffix);
        Assert.NotSame(source, result);
        Assert.Equal(source.Type(), result.Type());
        Assert.Equal((ushort)1234, result.At<ushort>(0, 0));
        result.SetTo(Scalar.All(4321));
        Assert.Equal((ushort)1234, source.At<ushort>(0, 0));
    }

    [Fact]
    public void CatalogBatchOutputRemainsAvailableWhenSaveRetriesANameCollision()
    {
        string directory = Directory.CreateTempSubdirectory("ColorVision-BatchRetry-").FullName;
        try
        {
            string sourcePath = Path.Combine(directory, "source.png");
            using Mat source = Mat.FromArray(new ushort[,] { { 1234, 5678 }, { 1111, 2222 } });
            BatchImageOutput.Save(source, sourcePath);
            BatchImageAlgorithmDefinition algorithm = BatchImageAlgorithms.CreateAll()
                .Single(entry => entry.Descriptor?.Id == StandardAlgorithmIds.ImagingCorrection);
            var publicationAttempts = new List<(string Destination, string Staged)>();
            var processor = new BatchImageProcessor([new StandardBatchImageLoader()]);

            var run = processor.Process(new BatchImageProcessingRequest
            {
                Items = [new BatchImageItem(sourcePath)],
                Algorithm = algorithm,
                Suffix = "_corrected",
                ValidateOutputPath = path =>
                {
                    string? staged = Directory.EnumerateFiles(directory, ".__colorvision_*.tmp.png").SingleOrDefault();
                    if (staged == null) return;
                    publicationAttempts.Add((path, staged));
                    if (publicationAttempts.Count <= 2)
                        File.WriteAllBytes(path, [1, 2, 3]);
                },
            });

            BatchImageFileResult result = Assert.Single(run.Files);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(Path.Combine(directory, "source_corrected_3.png"), result.OutputPath);
            Assert.Equal(3, publicationAttempts.Count);
            Assert.Single(publicationAttempts.Select(attempt => attempt.Staged).Distinct());
            Assert.All(publicationAttempts.Take(2), attempt => Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(attempt.Destination)));
            Assert.Empty(Directory.EnumerateFiles(directory, ".__colorvision_*.tmp.png"));
            using Mat pixels = Cv2.ImRead(result.OutputPath, ImreadModes.Unchanged);
            AssertMatsEqual(source, pixels);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void BatchCanReuseANumberFreedBetweenItemsWithoutOverwritingItsOwnOutputs()
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("ColorVision-BatchNumbering-");
        string sourcePath = Path.Combine(directory.FullName, "capture.png");
        string baseOutput = Path.Combine(directory.FullName, "capture_processed.png");
        string numberedOutput = Path.Combine(directory.FullName, "capture_processed_2.png");
        try
        {
            using Mat source = Mat.FromArray(new byte[,] { { 10, 20 } });
            BatchImageOutput.Save(source, sourcePath);
            File.WriteAllBytes(numberedOutput, [1, 2, 3]);
            var processor = new BatchImageProcessor([new StandardBatchImageLoader()]);

            BatchImageRunResult run = processor.Process(new BatchImageProcessingRequest
            {
                Items = Enumerable.Repeat(new BatchImageItem(sourcePath), 4).ToArray(),
                Algorithm = BatchImageAlgorithms.CreateAll()[0],
                Suffix = "_processed",
            }, progress =>
            {
                if (progress.Completed == 2 && progress.OutputPath != null)
                    File.Delete(numberedOutput);
            });

            Assert.All(run.Files, result => Assert.True(result.Success, result.ErrorMessage));
            Assert.Equal([baseOutput, Path.Combine(directory.FullName, "capture_processed_3.png"), numberedOutput,
                Path.Combine(directory.FullName, "capture_processed_4.png")], run.Files.Select(result => result.OutputPath));
        }
        finally
        {
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
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
    [InlineData(false)]
    [InlineData(true)]
    public void CancellationDuringOutputValidationKeepsCompletedFilesAndPreventsPublication(bool afterEncoding)
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("ColorVision-BatchCancelSave-");
        string first = Path.Combine(directory.FullName, "first.png");
        string second = Path.Combine(directory.FullName, "second.png");
        string firstOutput = Path.Combine(directory.FullName, "first_result.png");
        string secondOutput = Path.Combine(directory.FullName, "second_result.png");
        try
        {
            using Mat source = new(2, 3, MatType.CV_8UC1, new Scalar(42));
            BatchImageOutput.Save(source, first);
            BatchImageOutput.Save(source, second);
            byte[] previousBytes = [7, 11, 13];
            if (afterEncoding) File.WriteAllBytes(secondOutput, previousBytes);

            using var stop = new CancellationTokenSource();
            var processor = new BatchImageProcessor([new StandardBatchImageLoader()]);
            BatchImageRunResult run = processor.Process(new BatchImageProcessingRequest
            {
                Items = [new BatchImageItem(first), new BatchImageItem(second)],
                Algorithm = BatchImageAlgorithms.CreateAll()[0],
                Suffix = "_result",
                AvoidOverwrite = !afterEncoding,
                ValidateOutputPath = path =>
                {
                    if (path == secondOutput && (!afterEncoding || Directory.EnumerateFiles(directory.FullName, ".__colorvision_*.tmp.png").Any()))
                        stop.Cancel();
                }
            }, cancellationToken: stop.Token);

            Assert.True(run.Cancelled);
            Assert.Equal(1, run.Succeeded);
            Assert.True(run.Files[1].Cancelled);
            using Mat saved = Cv2.ImRead(firstOutput, ImreadModes.Unchanged);
            AssertMatsEqual(source, saved);
            if (afterEncoding) Assert.Equal(previousBytes, File.ReadAllBytes(secondOutput));
            else Assert.False(File.Exists(secondOutput));
            Assert.Empty(directory.EnumerateFiles(".__colorvision_*.tmp.png"));
        }
        finally
        {
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
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
    [InlineData("伪彩色", 1)]
    [InlineData("Canny 边缘检测", 1)]
    [InlineData("直方图均衡化", 1)]
    [InlineData("直方图均衡化", 3)]
    public void EightBitFastPathsHandleNonContinuousInput(string algorithmName, int channels)
    {
        using Mat backing = new(25, 31, MatType.MakeType(MatType.CV_8U, channels));
        Cv2.Randu(backing, Scalar.All(0), Scalar.All(byte.MaxValue + 1d));
        using Mat original = backing.Clone();
        using Mat source = new(backing, new Rect(3, 2, 23, 19));
        using Mat continuousSource = source.Clone();
        Assert.False(source.IsContinuous());

        BatchImageAlgorithmDefinition algorithm = BatchImageAlgorithms.CreateAll()
            .Single(item => item.Name == algorithmName);
        if (algorithm.Options is CannyParameters canny)
        {
            canny.LowThreshold = 100;
            canny.HighThreshold = 200;
        }
        using Mat expected = algorithm.Apply(continuousSource);
        using Mat actual = algorithm.Apply(source);

        AssertMatsEqual(expected, actual);
        AssertMatsEqual(original, backing);
    }

    [Theory]
    [InlineData("伪彩色")]
    [InlineData("Canny 边缘检测")]
    [InlineData("直方图均衡化")]
    public void SixteenBitNormalizationMatchesLegacyOutput(string algorithmName)
    {
        using Mat backing = new(25, 31, MatType.CV_16UC1);
        Cv2.Randu(backing, Scalar.All(0), Scalar.All(ushort.MaxValue + 1d));
        using Mat original = backing.Clone();
        using Mat source = new(backing, new Rect(3, 2, 23, 19));
        using Mat normalized = new();
        using Mat source8 = new();
        Cv2.Normalize(source, normalized, 0, byte.MaxValue, NormTypes.MinMax);
        normalized.ConvertTo(source8, MatType.CV_8U);
        using Mat expected = ApplyLegacyEightBitAlgorithm(algorithmName, source8);

        BatchImageAlgorithmDefinition algorithm = BatchImageAlgorithms.CreateAll()
            .Single(item => item.Name == algorithmName);
        if (algorithm.Options is CannyParameters canny)
        {
            canny.LowThreshold = 100;
            canny.HighThreshold = 200;
        }
        using Mat actual = algorithm.Apply(source);

        AssertMatsEqual(expected, actual);
        AssertMatsEqual(original, backing);
    }

    [Fact]
    public void CannyLegacyCompatibilityTestUsesExplicitThresholdSensitive100And200Parameters()
    {
        using Mat source = new(48, 64, MatType.CV_8UC1, Scalar.All(50));
        using (Mat rightHalf = new(source, new Rect(32, 0, 32, 48))) rightHalf.SetTo(Scalar.All(90));
        BatchImageAlgorithmDefinition explicitAlgorithm = BatchImageAlgorithms.CreateAll()
            .Single(item => item.Name == "Canny 边缘检测");
        CannyParameters explicitParameters = Assert.IsType<CannyParameters>(explicitAlgorithm.Options);
        explicitParameters.LowThreshold = 100;
        explicitParameters.HighThreshold = 200;
        BatchImageAlgorithmDefinition defaultAlgorithm = BatchImageAlgorithms.CreateAll()
            .Single(item => item.Name == "Canny 边缘检测");
        CannyParameters defaults = Assert.IsType<CannyParameters>(defaultAlgorithm.Options);
        Assert.Equal(50, defaults.LowThreshold);
        Assert.Equal(150, defaults.HighThreshold);

        using Mat expected = new();
        Cv2.Canny(source, expected, 100, 200);
        using Mat actual = explicitAlgorithm.Apply(source);
        using Mat defaultOutput = defaultAlgorithm.Apply(source);

        AssertMatsEqual(expected, actual);
        Assert.NotEqual(0, Cv2.CountNonZero(defaultOutput));
        Assert.NotEqual(Cv2.CountNonZero(defaultOutput), Cv2.CountNonZero(actual));
    }

    [Theory]
    [InlineData("sample.cvraw", true)]
    [InlineData("sample.CVRAW", true)]
    [InlineData("sample.cvcie", false)]
    [InlineData("capture.cvraw/sample.cvcie", false)]
    public void ColorVisionLoaderReadsARealSerializedFile(string fileName, bool raw)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"colorvision-batch-{Guid.NewGuid():N}");
        string filePath = Path.Combine(directory, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        try
        {
            using CVCIEFile file = new()
            {
                Version = 1,
                FileExtType = raw ? CVType.Raw : CVType.CIE,
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
            Assert.Equal(raw ? MatType.CV_8UC3 : MatType.CV_8UC1, loaded.Type());
            byte[] expected = raw ? file.Data : file.Data[..(file.Rows * file.Cols)];
            byte[] actual = new byte[expected.Length];
            Marshal.Copy(loaded.Data, actual, 0, actual.Length);
            Assert.Equal(expected, actual);
            Cv2.BitwiseNot(loaded, loaded);
            Assert.Equal((byte)255, Marshal.ReadByte(loaded.Data));
            using Mat reloaded = loader.Load(filePath);
            Assert.Equal((byte)0, Marshal.ReadByte(reloaded.Data));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("sample.cvcie", true, true)]
    [InlineData("sample.cvcie", false, false)]
    [InlineData("sample.CVCIE", true, true)]
    public void BatchCiePreservesSourceSelectionAndReadsOnlyTheDisplayedPlane(string fileName, bool hasAssociatedSource, bool usesAssociatedSource)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"colorvision-batch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, fileName);
        string rawName = $"source-{Guid.NewGuid():N}.cvraw";
        try
        {
            if (hasAssociatedSource)
            {
                using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(3, 2, 16, 3);
                Assert.True(CVFileUtil.WriteCIEFile(Path.Combine(directory, rawName), raw));
            }
            using CVCIEFile file = new()
            {
                Version = 2, FileExtType = CVType.CIE, Cols = 3, Rows = 2, Bpp = 32, Channels = 3,
                SrcFileName = rawName, Exp = [1, 1, 1], Gain = 1, Data = new byte[18 * sizeof(float)]
            };
            float[] samples = Enumerable.Range(0, 18).Select(index => (float)(index * index - 3)).ToArray();
            Buffer.BlockCopy(samples, 0, file.Data, 0, file.Data.Length);
            Assert.True(CVFileUtil.WriteCIEFile(path, file));
            using CVCIEFile legacy = CVFileUtil.OpenLocalCVFile(path);
            using Mat expected = legacy.ToMat(showErrors: false);
            using CVCIEFile display = CVFileUtil.OpenLocalCVFileForDisplay(path);
            using Mat loaded = new CVRawBatchImageLoader().Load(path);

            Assert.Equal(usesAssociatedSource ? MatType.CV_16UC3 : MatType.CV_8UC1, loaded.Type());
            Assert.Equal(usesAssociatedSource ? 36 : 24, display.Data.Length);
            AssertMatsEqual(expected, loaded);
        }
        finally
        {
            File.Delete(path);
            File.Delete(Path.Combine(directory, rawName));
            Directory.Delete(directory);
        }
    }

    [Fact]
    public void DisplayReadRejectsExtraDeclaredCiePayloadWhileChannelReadRemainsPermissive()
    {
        string path = Path.Combine(Path.GetTempPath(), $"colorvision-batch-extra-payload-{Guid.NewGuid():N}.cvcie");
        try
        {
            using CVCIEFile file = new()
            {
                Version = 2, FileExtType = CVType.CIE, Cols = 2, Rows = 1, Bpp = 16, Channels = 3,
                Exp = [1, 1, 1], Gain = 1, Data = new byte[13]
            };
            Assert.True(CVFileUtil.WriteCIEFile(path, file));
            using CVCIEFile display = CVFileUtil.OpenLocalCVFileForDisplay(path);
            Assert.Null(display.Data);
            Assert.True(CVFileUtil.ReadCIEFileChannel(path, 0, out CVCIEFile channel));
            using (channel) Assert.Equal(4, channel.Data.Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("locked")]
    [InlineData("empty")]
    [InlineData("corrupt")]
    [InlineData("valid")]
    public void BatchCieUsesEmbeddedPixelsWhenAssociatedImageCannotBeReadOrDecoded(string sourceState)
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("ColorVision-BatchCieSource-");
        string path = Path.Combine(directory.FullName, "capture.cvcie");
        string sourcePath = Path.Combine(directory.FullName, "source.tiff");
        try
        {
            using CVCIEFile file = new()
            {
                Version = 2, FileExtType = CVType.CIE, Cols = 2, Rows = 1, Bpp = 8, Channels = 3,
                SrcFileName = "source.tiff", Exp = [1, 1, 1], Gain = 1, Data = [12, 34, 56, 78, 90, 123]
            };
            Assert.True(CVFileUtil.WriteCIEFile(path, file));
            if (sourceState == "empty")
                File.WriteAllBytes(sourcePath, []);
            else if (sourceState == "corrupt")
                File.WriteAllBytes(sourcePath, [1, 2, 3, 4, 5, 6]);
            else
            {
                using Mat image = new(1, 2, MatType.CV_8UC1, new Scalar(42));
                Assert.True(Cv2.ImWrite(sourcePath, image));
            }

            using FileStream? exclusive = sourceState == "locked"
                ? File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.None)
                : null;
            if (sourceState is "locked" or "empty")
            {
                Assert.False(CVFileUtil.ReadCVCIESrc(path, out CVCIEFile associated));
                associated.Dispose();
                Assert.True(CVFileUtil.ReadCVCIE(path, out CVCIEFile complete));
                using (complete) Assert.Equal(file.Data, complete.Data);
            }

            using Mat loaded = new CVRawBatchImageLoader().Load(path);
            Assert.Equal(MatType.CV_8UC1, loaded.Type());
            Assert.Equal(1, loaded.Rows);
            Assert.Equal(2, loaded.Cols);
            Assert.Equal(sourceState == "valid" ? (byte)42 : (byte)12, loaded.At<byte>(0, 0));
            Assert.Equal(sourceState == "valid" ? (byte)42 : (byte)34, loaded.At<byte>(0, 1));
        }
        finally
        {
            File.Delete(sourcePath);
            File.Delete(path);
            directory.Delete();
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

    private static Mat ApplyLegacyEightBitAlgorithm(string algorithmName, Mat source)
    {
        Mat result = new();
        switch (algorithmName)
        {
            case "伪彩色":
                Cv2.ApplyColorMap(source, result, OpenCvSharp.ColormapTypes.Jet);
                break;
            case "Canny 边缘检测":
                Cv2.Canny(source, result, 100, 200);
                break;
            case "直方图均衡化":
                Cv2.EqualizeHist(source, result);
                break;
            default:
                result.Dispose();
                throw new ArgumentOutOfRangeException(nameof(algorithmName), algorithmName, null);
        }
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
