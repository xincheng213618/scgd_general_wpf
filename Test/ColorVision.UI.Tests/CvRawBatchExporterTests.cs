using ColorVision.Engine.Media;
using ColorVision.FileIO;
using ColorVision.ImageEditor.Tif;
using OpenCvSharp;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class CvRawBatchExporterTests
{
    [Theory]
    [InlineData(8, 3)]
    [InlineData(16, 1)]
    [InlineData(16, 3)]
    [InlineData(32, 1)]
    [InlineData(32, 3)]
    [InlineData(64, 1)]
    [InlineData(64, 3)]
    public void ExportPreservesRawSamplesAndParameters(int bpp, int channels)
    {
        using ExportFolder folder = new();
        string sourcePath = folder.WriteRaw("样本.v1.cvraw", bpp, channels);
        byte[] original = File.ReadAllBytes(sourcePath);
        Assert.True(CVFileUtil.ReadCVRaw(sourcePath, out CVCIEFile raw));
        using (raw)
        {
            CvRawExportResult result = CvRawBatchExporter.ExportFolder(folder.Path, 2);
            Assert.Equal(1, result.Exported);
            Assert.Equal(0, result.Failed);
            Assert.False(result.Cancelled);
            string outputPath = System.IO.Path.ChangeExtension(sourcePath, ".tif");
            Assert.Equal(outputPath, Assert.Single(result.Files).OutputPath);
            using Mat image = Cv2.ImRead(outputPath, ImreadModes.Unchanged);
            Assert.Equal(MatType.MakeType(raw.Depth, channels), image.Type());
            Assert.Equal(raw.Rows, image.Rows);
            Assert.Equal(raw.Cols, image.Cols);
            byte[] actual = new byte[raw.Data.Length];
            Marshal.Copy(image.Data, actual, 0, actual.Length);
            Assert.Equal(raw.Data, actual);
            Assert.Equal(original, File.ReadAllBytes(sourcePath));
            if (bpp <= 16)
            {
                WpfTestHost.Invoke(() =>
                {
                    using FileStream stream = File.OpenRead(outputPath);
                    TiffBitmapDecoder decoder = new(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    BitmapMetadata metadata = Assert.IsType<BitmapMetadata>(Assert.Single(decoder.Frames).Metadata);
                    Assert.True(ColorVisionTiffParameters.TryRead(metadata, out ColorVisionTiffParameters? parameters));
                    Assert.Equal("CVRAW", parameters!.SourceType);
                    Assert.Equal("样本.v1.cvraw", parameters.InputFileName);
                    Assert.Equal(bpp, parameters.Bpp);
                    Assert.Equal(channels, parameters.SourceChannels);
                    Assert.Equal(2f, parameters.Gain);
                });
            }
        }
        AssertNoTemporaryFiles(folder.Path);
    }

    [Fact]
    public void RecursesSkipsExistingBeforeReadingAndContinuesAfterBadFiles()
    {
        using ExportFolder folder = new();
        string upper = folder.WriteRaw("sub/UPPER.CVRAW", 16, 3);
        folder.WriteRaw("sub/inner/sample.v2.cvraw", 8, 3);
        File.WriteAllText(System.IO.Path.Combine(folder.Path, "unrelated.txt"), "ignore");
        string skipped = System.IO.Path.Combine(folder.Path, "existing.cvraw");
        File.WriteAllText(skipped, "invalid CVRAW, should never be read");
        string existing = System.IO.Path.ChangeExtension(skipped, ".tif");
        byte[] sentinel = [3, 1, 4, 1, 5];
        File.WriteAllBytes(existing, sentinel);
        File.WriteAllText(System.IO.Path.Combine(folder.Path, "broken.cvraw"), "invalid CVRAW");
        List<CvRawExportProgress> progress = new();

        CvRawExportResult first = CvRawBatchExporter.ExportFolder(folder.Path, 3, progress.Add);

        Assert.Equal(4, first.Total);
        Assert.Equal(2, first.Exported);
        Assert.Equal(1, first.Skipped);
        Assert.Equal(1, first.Failed);
        Assert.NotEmpty(first.Files.Single(file => file.Status == CvRawExportStatus.Failed).Error!);
        Assert.Equal(sentinel, File.ReadAllBytes(existing));
        Assert.True(File.Exists(System.IO.Path.ChangeExtension(upper, ".tif")));
        Assert.False(File.Exists(System.IO.Path.Combine(folder.Path, "broken.tif")));
        Assert.Equal(Enumerable.Range(0, 5), progress.Select(value => value.Completed));
        Assert.Equal(4, progress[^1].Completed);

        CvRawExportResult second = CvRawBatchExporter.ExportFolder(folder.Path, 2);
        Assert.Equal(0, second.Exported);
        Assert.Equal(3, second.Skipped);
        Assert.Equal(1, second.Failed);
        Assert.Equal(sentinel, File.ReadAllBytes(existing));
        AssertNoTemporaryFiles(folder.Path);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CancellationKeepsCompletedFilesAndStopsScheduling(bool beforeScan)
    {
        using ExportFolder folder = new();
        folder.WriteRaw("first.cvraw", 16, 1);
        folder.WriteRaw("second.cvraw", 16, 1);
        using CancellationTokenSource cancellation = new();
        if (beforeScan) cancellation.Cancel();

        CvRawExportResult result = CvRawBatchExporter.ExportFolder(folder.Path, 1, progress =>
        {
            if (progress.Completed == 1) cancellation.Cancel();
        }, cancellation.Token);

        Assert.True(result.Cancelled);
        Assert.Equal(beforeScan ? 0 : 1, result.Exported);
        Assert.Equal(result.Exported, Directory.GetFiles(folder.Path, "*.tif").Length);
        Assert.Equal(0, result.Failed);
        AssertNoTemporaryFiles(folder.Path);
    }

    [Fact]
    public async Task ConcurrentRunsPublishEachDestinationOnce()
    {
        using ExportFolder folder = new();
        for (int index = 0; index < 4; index++) folder.WriteRaw($"{index}.cvraw", 16, 3);
        using ManualResetEventSlim start = new(false);
        Task<CvRawExportResult>[] runs = Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
        {
            start.Wait();
            return CvRawBatchExporter.ExportFolder(folder.Path, 2);
        })).ToArray();
        start.Set();
        CvRawExportResult[] results = await Task.WhenAll(runs);

        Assert.Equal(4, results.Sum(result => result.Exported));
        Assert.Equal(4, results.Sum(result => result.Skipped));
        Assert.All(results, result => Assert.Equal(0, result.Failed));
        Assert.Equal(4, Directory.GetFiles(folder.Path, "*.tif").Length);
        foreach (string output in Directory.GetFiles(folder.Path, "*.tif"))
        {
            using Mat image = Cv2.ImRead(output, ImreadModes.Unchanged);
            Assert.Equal(MatType.CV_16UC3, image.Type());
        }
        AssertNoTemporaryFiles(folder.Path);
    }

    private static void AssertNoTemporaryFiles(string folder)
        => Assert.Empty(Directory.GetFiles(folder, ".__cvraw_export_*", SearchOption.AllDirectories));

    private sealed class ExportFolder : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cvraw-export-{Guid.NewGuid():N}");

        public ExportFolder() => Directory.CreateDirectory(Path);

        public string WriteRaw(string relativePath, int bpp, int channels)
        {
            string path = System.IO.Path.Combine(Path, relativePath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            byte[] pixels = Enumerable.Range(0, 6 * channels).SelectMany(index => bpp switch
            {
                8 => new[] { (byte)(index * 7) },
                16 => BitConverter.GetBytes((ushort)(index * 1021)),
                32 => BitConverter.GetBytes((float)(index - 7) * 1.25f),
                64 => BitConverter.GetBytes((index - 7) * 1.23456789),
                _ => throw new ArgumentOutOfRangeException(nameof(bpp))
            }).ToArray();
            using CVCIEFile raw = new()
            {
                Version = 1, FileExtType = CVType.Raw, Rows = 2, Cols = 3, Bpp = bpp,
                Channels = channels, Gain = 2, Exp = [1, 2, 3], Data = pixels
            };
            Assert.True(CVFileUtil.WriteCIEFile(path, raw));
            return path;
        }

        public void Dispose()
        {
            string resolved = System.IO.Path.GetFullPath(Path);
            Assert.StartsWith(System.IO.Path.GetTempPath(), resolved, StringComparison.OrdinalIgnoreCase);
            Directory.Delete(resolved, recursive: true);
        }
    }
}
