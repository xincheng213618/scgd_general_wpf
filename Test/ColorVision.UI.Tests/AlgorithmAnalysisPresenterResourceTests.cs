using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.EditorTools.Algorithms;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class AlgorithmAnalysisPresenterResourceTests
{
    [Fact]
    public void ConstructionRetainsLargeImagesWithoutCopyingOrScanningTheirPixels()
    {
        const int width = 2048;
        const int height = 2048;
        int stride = checked(width * sizeof(float));
        AlgorithmResult result = new()
        {
            Status = AlgorithmResultStatus.Succeeded,
            Artifacts =
            [
                Image("first", width, height, stride, AlgorithmImageFormat.Gray32Float, new byte[checked(stride * height)]),
                Image("second", width, height, stride, AlgorithmImageFormat.Gray32Float, new byte[checked(stride * height)]),
            ],
        };

        long before = GC.GetAllocatedBytesForCurrentThread();
        using AlgorithmAnalysisResultPresentation presentation =
            DefaultAlgorithmAnalysisResultPresenter.CreatePresentation(result, "large-float");
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(2, presentation.Images.Count);
        Assert.All(presentation.Images, image => Assert.False(image.IsBitmapMaterialized));
        Assert.True(allocated < 2 * 1024 * 1024, $"Construction allocated {allocated:N0} bytes.");
        result.Dispose();
        GC.KeepAlive(presentation);
    }

    [Fact]
    public void FourKilobyteBgr24CanBeExplicitlyExportedWhenAutomaticPreviewIsOverBudget()
    {
        const int width = 3840;
        const int height = 2160;
        const double dpiX = 144;
        const double dpiY = 120;
        int stride = checked(width * 3);
        byte[] pixels = new byte[checked(stride * height)];
        pixels[0] = 7;
        pixels[1] = 11;
        pixels[2] = 13;
        string directory = Path.Combine(Path.GetTempPath(), $"algorithm-large-image-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            AlgorithmResult result = new()
            {
                Status = AlgorithmResultStatus.Succeeded,
                Artifacts = [Image("4k", width, height, stride, AlgorithmImageFormat.Bgr24, pixels, dpiX, dpiY)],
            };
            using AlgorithmAnalysisResultPresentation presentation =
                DefaultAlgorithmAnalysisResultPresenter.CreatePresentation(result, "4K");
            AlgorithmAnalysisImagePresentation image = Assert.Single(presentation.Images);
            Assert.False(image.IsPreviewAvailable);
            result.Dispose();

            string path = WpfTestHost.Invoke(() => image.Export(Path.Combine(directory, "4k.png")));
            BitmapSource decoded = Decode(path);
            Assert.Equal((width, height), (decoded.PixelWidth, decoded.PixelHeight));
            Assert.Equal(dpiX, decoded.DpiX, 1);
            Assert.Equal(dpiY, decoded.DpiY, 1);
            byte[] firstPixel = new byte[3];
            decoded.CopyPixels(new System.Windows.Int32Rect(0, 0, 1, 1), firstPixel, 3, 0);
            Assert.Equal(new byte[] { 7, 11, 13 }, firstPixel);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FourKilobyteGray16PaddedStrideExportsWithoutLosingLayoutOrDpi()
    {
        const int width = 3840;
        const int height = 2160;
        const double dpiX = 110;
        const double dpiY = 111;
        int stride = checked(width * sizeof(ushort) + 128);
        byte[] pixels = new byte[checked(stride * height)];
        pixels[0] = 0x34;
        pixels[1] = 0x12;
        pixels[checked(width * sizeof(ushort))] = 0xee;
        string directory = CreateTemporaryDirectory("algorithm-large-gray16");
        try
        {
            AlgorithmResult result = new()
            {
                Status = AlgorithmResultStatus.Succeeded,
                Artifacts = [Image("gray16", width, height, stride, AlgorithmImageFormat.Gray16, pixels, dpiX, dpiY)],
            };
            using AlgorithmAnalysisResultPresentation presentation =
                DefaultAlgorithmAnalysisResultPresenter.CreatePresentation(result, "Gray16 4K");
            AlgorithmAnalysisImagePresentation image = Assert.Single(presentation.Images);
            Assert.False(image.IsPreviewAvailable);
            result.Dispose();

            string path = image.Export(Path.Combine(directory, "gray16.tiff"));
            BitmapSource decoded = Decode(path);
            Assert.Equal(PixelFormats.Gray16, decoded.Format);
            Assert.Equal((width, height), (decoded.PixelWidth, decoded.PixelHeight));
            Assert.Equal(dpiX, decoded.DpiX, 1);
            Assert.Equal(dpiY, decoded.DpiY, 1);
            byte[] firstPixel = new byte[2];
            decoded.CopyPixels(new System.Windows.Int32Rect(0, 0, 1, 1), firstPixel, 2, 0);
            Assert.Equal(new byte[] { 0x34, 0x12 }, firstPixel);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FourKilobyteGray32FloatPaddedStrideExportsBoundedNormalizedPixelsAfterResultDisposal()
    {
        const int width = 3840;
        const int height = 2160;
        const double dpiX = 123;
        const double dpiY = 124;
        int stride = checked(width * sizeof(float) + 16);
        byte[] pixels = new byte[checked(stride * height)];
        MemoryMarshal.Write(pixels.AsSpan(0, sizeof(float)), 1f);
        string directory = CreateTemporaryDirectory("algorithm-large-gray32f");
        try
        {
            AlgorithmResult result = new()
            {
                Status = AlgorithmResultStatus.Succeeded,
                Artifacts = [Image("gray32f", width, height, stride, AlgorithmImageFormat.Gray32Float, pixels, dpiX, dpiY)],
            };
            using AlgorithmAnalysisResultPresentation presentation =
                DefaultAlgorithmAnalysisResultPresenter.CreatePresentation(result, "Gray32Float 4K");
            AlgorithmAnalysisImagePresentation image = Assert.Single(presentation.Images);
            Assert.False(image.IsPreviewAvailable);
            result.Dispose();

            string path = image.Export(Path.Combine(directory, "gray32f.png"));
            BitmapSource decoded = Decode(path);
            Assert.Equal(PixelFormats.Gray8, decoded.Format);
            Assert.Equal((width, height), (decoded.PixelWidth, decoded.PixelHeight));
            Assert.Equal(dpiX, decoded.DpiX, 1);
            Assert.Equal(dpiY, decoded.DpiY, 1);
            byte[] firstTwo = new byte[2];
            decoded.CopyPixels(new System.Windows.Int32Rect(0, 0, 2, 1), firstTwo, 2, 0);
            Assert.Equal(new byte[] { 255, 0 }, firstTwo);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task MillionRowCsvCancellationIsStreamingAndPreservesTheExistingTarget()
    {
        string directory = CreateTemporaryDirectory("algorithm-streaming-csv");
        string target = Path.Combine(directory, "result.csv");
        await File.WriteAllTextAsync(target, "original");
        using CancellationTokenSource cancellation = new();
        RepeatedRows rows = new(1_000_000, cancelAt: 128, cancellation);
        using AlgorithmResult result = TableResult(rows);
        long before = GC.GetTotalAllocatedBytes(precise: true);
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                AlgorithmResultExporter.ExportCsvBundleAsync(result, target, overwrite: true, cancellation.Token));
            long allocated = GC.GetTotalAllocatedBytes(precise: true) - before;

            Assert.True(allocated < 64L * 1024 * 1024, $"Cancelled export allocated {allocated:N0} bytes before row 128.");
            Assert.Equal("original", await File.ReadAllTextAsync(target));
            AssertNoStagingFiles(directory);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task MillionRowJsonCancellationIsStreamingAndPreservesTheExistingTarget()
    {
        string directory = CreateTemporaryDirectory("algorithm-streaming-json");
        string target = Path.Combine(directory, "result.json");
        await File.WriteAllTextAsync(target, "original");
        using CancellationTokenSource cancellation = new();
        RepeatedRows rows = new(1_000_000, cancelAt: 128, cancellation);
        using AlgorithmResult result = TableResult(rows);
        long before = GC.GetTotalAllocatedBytes(precise: true);
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                AlgorithmResultExporter.ExportJsonAsync(result, target, overwrite: true, cancellation.Token));
            long allocated = GC.GetTotalAllocatedBytes(precise: true) - before;

            Assert.True(allocated < 64L * 1024 * 1024, $"Cancelled export allocated {allocated:N0} bytes before row 128.");
            Assert.Equal("original", await File.ReadAllTextAsync(target));
            AssertNoStagingFiles(directory);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SuccessfulStreamingExportsAtomicallyReplaceExistingTargets()
    {
        string directory = CreateTemporaryDirectory("algorithm-atomic-export");
        string json = Path.Combine(directory, "result.json");
        string csv = Path.Combine(directory, "result.csv");
        await File.WriteAllTextAsync(json, "old-json");
        await File.WriteAllTextAsync(csv, "old-csv");
        using AlgorithmResult result = new()
        {
            InvocationId = Guid.NewGuid(),
            AlgorithmId = new AlgorithmId("test.atomic-export"),
            AlgorithmVersion = new AlgorithmVersion(1, 0, 0),
            Status = AlgorithmResultStatus.Succeeded,
            Artifacts = [new AlgorithmMeasurementArtifact("metrics", [new AlgorithmMeasurement("mean", 12.5)])],
        };
        try
        {
            await AlgorithmResultExporter.ExportJsonAsync(result, json, overwrite: true);
            await AlgorithmResultExporter.ExportCsvBundleAsync(result, csv, overwrite: true);

            using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(json));
            Assert.Equal("test.atomic-export", document.RootElement.GetProperty("algorithmId").GetString());
            Assert.Contains("mean", await File.ReadAllTextAsync(csv), StringComparison.Ordinal);
            AssertNoStagingFiles(directory);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static AlgorithmResult TableResult(IReadOnlyList<IReadOnlyDictionary<string, JsonElement>> rows)
        => new()
        {
            InvocationId = Guid.NewGuid(),
            AlgorithmId = new AlgorithmId("test.streaming-export"),
            AlgorithmVersion = new AlgorithmVersion(1, 0, 0),
            Status = AlgorithmResultStatus.Succeeded,
            Artifacts =
            [
                new AlgorithmTableArtifact(
                    "large-table",
                    [new AlgorithmTableColumn("value", "integer")],
                    rows),
            ],
        };

    private static string CreateTemporaryDirectory(string prefix)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static BitmapSource Decode(string path)
        => WpfTestHost.Invoke(() =>
        {
            BitmapSource frame = BitmapDecoder.Create(
                new Uri(path), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
            WriteableBitmap detached = new(frame);
            detached.Freeze();
            return detached;
        });

    private static void AssertNoStagingFiles(string directory)
        => Assert.DoesNotContain(Directory.EnumerateFiles(directory), path =>
            path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".bak", StringComparison.OrdinalIgnoreCase));

    private sealed class RepeatedRows(
        int count,
        int cancelAt,
        CancellationTokenSource cancellation) : IReadOnlyList<IReadOnlyDictionary<string, JsonElement>>
    {
        private readonly IReadOnlyDictionary<string, JsonElement> _row = new Dictionary<string, JsonElement>
        {
            ["value"] = JsonSerializer.SerializeToElement(42),
        };

        public int Count { get; } = count;

        public IReadOnlyDictionary<string, JsonElement> this[int index]
        {
            get
            {
                if (index == cancelAt) cancellation.Cancel();
                return _row;
            }
        }

        public IEnumerator<IReadOnlyDictionary<string, JsonElement>> GetEnumerator()
        {
            for (int index = 0; index < Count; index++) yield return this[index];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static AlgorithmImageArtifact Image(
        string name,
        int width,
        int height,
        int stride,
        AlgorithmImageFormat format,
        byte[] pixels,
        double dpiX = 96,
        double dpiY = 96)
        => new(name, "visualization", new AlgorithmImageBuffer(width, height, stride, format, pixels, dpiX, dpiY));
}
