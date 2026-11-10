using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Tif;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.Algorithms;
using ColorVision.Core;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Services.POI;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.ImageEditor.Layers;
using System.Threading;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AppCommandContextMenu = ColorVision.ImageEditor.EditorTools.AppCommand.ImageViewSettingsEditorToolContextMenu;
using Xunit.Abstractions;

namespace ColorVision.UI.Tests;

public sealed class ImageOpenCompletionContractTests(ITestOutputHelper output)
{
    [Fact]
    public async Task FloatTiffDisplayNormalizationPreservesSamplesAcrossPaddedRowsAndFinalChunk()
    {
        const int width = 513;
        const int height = 513;
        const float min = -3.25f;
        const float max = 10.1f;
        double range = (double)max - min;
        float[] source = new float[width * height];
        for (int index = 0; index < source.Length; index++)
        {
            int code = index % (ushort.MaxValue - 1) + 1;
            float edge = (float)(min + range * code / ushort.MaxValue);
            source[index] = (index % 3) switch
            {
                0 => MathF.BitIncrement(edge),
                1 => MathF.BitDecrement(edge),
                _ => edge,
            };
        }
        source[0] = min;
        source[width - 1] = float.NegativeInfinity;
        source[width + 2] = float.NaN;
        source[^2] = float.PositiveInfinity;
        source[^1] = max;
        string path = Path.Combine(Path.GetTempPath(), $"colorvision-float-open-{Guid.NewGuid():N}.tiff");
        ImageView? view = null;
        try
        {
            await WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                BitmapSource pixels = BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray32Float, null, source, width * sizeof(float));
                ColorVision.Engine.Media.CalibratedImageExport.Save(pixels, null, path);
                view = new ImageView { EnableEditorImageServices = false };
                view.OpenImage(path);
                return view.ImageOpenCompletion;
            });

            WpfTestHost.Invoke(() =>
            {
                var display = Assert.IsType<WriteableBitmap>(view!.ViewBitmapSource);
                Assert.Equal(PixelFormats.Gray16, display.Format);
                Assert.False(display.IsFrozen);
                ushort[] actual = new ushort[source.Length];
                display.CopyPixels(actual, width * sizeof(ushort), 0);
                ushort[] expected = source.Select(value => float.IsFinite(value)
                    ? (ushort)(Math.Clamp(((double)value - min) / range, 0, 1) * ushort.MaxValue)
                    : (ushort)0).ToArray();
                Assert.Equal(expected, actual);

                BitmapSource original = view.ImageContent!.Pixels;
                Assert.Equal(PixelFormats.Gray32Float, original.Format);
                float[] samples = new float[source.Length];
                original.CopyPixels(samples, width * sizeof(float), 0);
                Assert.Equal(source, samples);
            });
        }
        finally
        {
            if (view != null) WpfTestHost.Invoke(view.Dispose);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FloatDisplayWithoutAFiniteRangeIsBlack(bool allNonFinite)
    {
        WpfTestHost.Invoke(() =>
        {
            const int width = 9;
            const int height = 3;
            float[] samples = Enumerable.Repeat(allNonFinite ? float.NaN : -2.5f, width * height).ToArray();
            samples[0] = float.PositiveInfinity;
            samples[^1] = float.NegativeInfinity;
            BitmapSource source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray32Float, null, samples, width * sizeof(float));

            WriteableBitmap display = Opentif.ConvertGray32FloatToBitmapSource(source);

            Assert.Equal(PixelFormats.Gray16, display.Format);
            ushort[] actual = new ushort[samples.Length];
            display.CopyPixels(actual, width * sizeof(ushort), 0);
            Assert.Equal(new ushort[samples.Length], actual);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LargePngPublishesEditablePixelsWithDecodedDpiAndPalette(bool indexed)
    {
        int width = indexed ? 4097 : 2049;
        int height = indexed ? 2051 : 1025;
        int stride = width * (indexed ? 1 : 4);
        string path = Path.Combine(Path.GetTempPath(), $"colorvision-large-open-{Guid.NewGuid():N}.png");
        ImageView? view = null;
        BitmapSource? expected = null;
        try
        {
            await WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                PixelFormat format = indexed ? PixelFormats.Indexed8 : PixelFormats.Bgra32;
                BitmapPalette? palette = indexed ? new BitmapPalette(Enumerable.Range(0, 256)
                    .Select(value => Color.FromRgb((byte)value, (byte)(255 - value), (byte)(value * 37))).ToArray()) : null;
                byte[] data = new byte[stride * height];
                for (int index = 0; index < data.Length; index++) data[index] = (byte)(index + (index >> 8));
                BitmapSource source = BitmapSource.Create(width, height, 144, 120, format, palette, data, stride);
                PngBitmapEncoder encoder = new();
                encoder.Frames.Add(BitmapFrame.Create(source));
                using (FileStream file = File.Create(path)) encoder.Save(file);
                using (FileStream file = File.OpenRead(path))
                    expected = new PngBitmapDecoder(file, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];

                view = new ImageView { EnableEditorImageServices = false };
                view.OpenImage(path);
                return view.ImageOpenCompletion;
            });

            WpfTestHost.Invoke(() =>
            {
                WriteableBitmap actual = Assert.IsType<WriteableBitmap>(view!.ViewBitmapSource);
                Assert.False(actual.IsFrozen);
                Assert.Equal(expected!.Format, actual.Format);
                Assert.Equal((expected.DpiX, expected.DpiY), (actual.DpiX, actual.DpiY));
                Assert.Equal(expected.Palette?.Colors.ToArray(), actual.Palette?.Colors.ToArray());
                byte[] expectedPixels = new byte[stride * height];
                byte[] actualPixels = new byte[expectedPixels.Length];
                expected.CopyPixels(expectedPixels, stride, 0);
                actual.CopyPixels(actualPixels, stride, 0);
                Assert.True(expectedPixels.AsSpan().SequenceEqual(actualPixels));
                Assert.Same(actual, view.ImageContent!.Pixels);
            });
        }
        finally
        {
            if (view != null) WpfTestHost.Invoke(view.Dispose);
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(".cvraw")]
    [InlineData(".cvcie")]
    public async Task AlreadyCancelledCvOpenPreservesCurrentContentAndReportsCancellation(string extension)
    {
        ImageView? view = null;
        ColorVision.Engine.Media.CVRawOpen? opener = null;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                view = new ImageView();
                view.SetImageSource(new WriteableBitmap(2, 2, 96, 96, PixelFormats.Gray8, null));
                opener = new(view.EditorContext);
                opener.AttachLiveCvcie(view, 2, 2, 32, 1, new byte[16], [1]);
            });
            Task completion = WpfTestHost.Invoke(() => opener!.OpenImageAsync(view!.EditorContext, "unused" + extension, cancellation.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => completion);
            Assert.True(completion.IsCanceled);
            WpfTestHost.Invoke(() =>
            {
                Assert.Same(opener, view!.ImageContentCapability);
                Assert.True(view.Config.GetProperties<bool>("IsCVCIE"));
            });
        }
        finally { if (view != null) WpfTestHost.Invoke(view.Dispose); }
    }

    [Fact]
    public async Task CvciePublicationAfterPathChangesReportsCancellationWithoutLoadingOldPixels()
    {
        string path = Path.Combine(Path.GetTempPath(), $"cv-cancel-publication-{Guid.NewGuid():N}.cvcie");
        ImageView? view = null;
        Task completion = Task.CompletedTask;
        int loaded = 0;
        try
        {
            using CVCIEFile file = new()
            {
                Version = 1, FileExtType = CVType.CIE, Rows = 2, Cols = 2, Bpp = 32, Channels = 1,
                Exp = [1], Data = new float[] { 1, 2, 3, 4 }.SelectMany(BitConverter.GetBytes).ToArray()
            };
            Assert.True(CVFileUtil.WriteCIEFile(path, file));
            WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                view = new ImageView();
                view.ImageSourceLoaded += (_, _) => loaded++;
                view.Config.SetImageMetadata(ImageViewPropertyKeys.FilePath, path, nameof(ImageOpenCompletionContractTests), "initial target");
                completion = new ColorVision.Engine.Media.CVRawOpen(view.EditorContext).OpenImageAsync(view.EditorContext, path);
                // Publication needs this dispatcher, so the path is guaranteed to change before it can run.
                view.Config.SetImageMetadata(ImageViewPropertyKeys.FilePath, path + ".replacement", nameof(ImageOpenCompletionContractTests), "replacement target");
            });
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => completion.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.True(completion.IsCanceled);
            WpfTestHost.Invoke(() =>
            {
                Assert.Null(view!.ViewBitmapSource);
                Assert.Null(view.ImageContentCapability);
                Assert.Equal(0, loaded);
            });
        }
        finally
        {
            if (view != null) WpfTestHost.Invoke(view.Dispose);
            File.Delete(path);
        }
    }

    [Fact]
    public async Task DiskCvcieSharesPendingMeasurementAndCancelsItOnContentRetirement()
    {
        string path = Path.Combine(Path.GetTempPath(), $"cv-measurement-{Guid.NewGuid():N}.cvcie");
        ImageView? view = null;
        try
        {
            WriteCvcieMeasurementFixture(path, firstValue: 1);
            CVRawOpen opener = WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                view = new ImageView();
                return new CVRawOpen(view.EditorContext);
            });
            Task Open() => WpfTestHost.Invoke(() =>
            {
                view!.Config.SetImageMetadata(ImageViewPropertyKeys.FilePath, path, nameof(ImageOpenCompletionContractTests), "measurement target");
                return opener.OpenImageAsync(view.EditorContext, path);
            });
            await Open().WaitAsync(TimeSpan.FromSeconds(10));
            object sync = typeof(CalibratedImageCapability).GetField("_bufferSync", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(opener)!;
            Task<PoiMeasurementBuffer> old = WpfTestHost.Invoke(() =>
            {
                lock (sync) // Hold publication until the UI retires the measurement source.
                {
                    Task<PoiMeasurementBuffer> first = opener.PrepareMeasurementAsync().AsTask();
                    Assert.False(first.IsCompleted);
                    Assert.Same(first, opener.PrepareMeasurementAsync().AsTask());
                    view!.AttachImageContentCapability(null);
                    return first;
                }
            });
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => old.WaitAsync(TimeSpan.FromSeconds(10)));

            await Open().WaitAsync(TimeSpan.FromSeconds(10));
            PoiMeasurementBuffer buffer = await WpfTestHost.Invoke(() => opener.PrepareMeasurementAsync().AsTask()).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Same(buffer, await WpfTestHost.Invoke(() => opener.PrepareMeasurementAsync().AsTask()));
            PoiMeasurementResult point = WpfTestHost.Invoke(() => opener.CalculatePoi([new(1, 0, 1, 1, PoiMeasurementShape.Point)], true).Results[0]);
            Assert.Equal((2f, 6f, 10f), (point.X, point.Y, point.Z));
        }
        finally
        {
            if (view != null) WpfTestHost.Invoke(view.Dispose);
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DiskCvcieRejectsNewPixelsAtTheSamePathUntilReopened(bool measurementWasLoaded)
    {
        string path = Path.Combine(Path.GetTempPath(), $"cv-measurement-revision-{Guid.NewGuid():N}.cvcie");
        ImageView? view = null;
        try
        {
            WriteCvcieMeasurementFixture(path, firstValue: 1);
            CvcieFileRevision originalRevision = CvcieFileRevision.Capture(path);
            CVRawOpen opener = WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                view = new ImageView();
                return new CVRawOpen(view.EditorContext);
            });
            Task Open() => WpfTestHost.Invoke(() =>
            {
                view!.Config.SetImageMetadata(ImageViewPropertyKeys.FilePath, path, nameof(ImageOpenCompletionContractTests), "measurement target");
                return opener.OpenImageAsync(view.EditorContext, path);
            });
            await Open().WaitAsync(TimeSpan.FromSeconds(10));
            if (measurementWasLoaded)
                await WpfTestHost.Invoke(() => opener.PrepareMeasurementAsync().AsTask()).WaitAsync(TimeSpan.FromSeconds(10));
            var old = WpfTestHost.Invoke(() =>
            {
                var controller = (CvRawLayerController)typeof(ImageView).GetField("_layerController", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!;
                var profile = view!.EditorContext.ProcessingContext.ProfileMeasurementSources!.Single(option => option.Name == "CIE XYZ");
                return (Controller: controller, Profile: profile, Bitmap: view.ViewBitmapSource, Layer: view.SelectedLayer);
            });

            WriteCvcieMeasurementFixture(path, firstValue: 101);
            File.SetLastWriteTimeUtc(path, originalRevision.LastWriteTimeUtc.AddMinutes(-5));
            Assert.Equal(originalRevision.Length, new FileInfo(path).Length);

            PoiMeasurementPoint[] points = [new(1, 0, 1, 1, PoiMeasurementShape.Point)];
            if (measurementWasLoaded)
            {
                PoiMeasurementResult original = WpfTestHost.Invoke(() => opener.CalculatePoi(points, true).Results[0]);
                Assert.Equal((2f, 6f, 10f), (original.X, original.Y, original.Z));
            }
            else
            {
                await Assert.ThrowsAsync<CvcieFileChangedException>(() => WpfTestHost.Invoke(() => opener.PrepareMeasurementAsync().AsTask()));
            }
            Assert.Throws<CvcieFileChangedException>(() => old.Profile.Open(default));
            ImageLayerDescriptor layer = old.Controller.Layers.Single(item => item.Id == "cie-x");
            await WpfTestHost.Invoke(() => old.Controller.SelectLayerAsync(layer)).WaitAsync(TimeSpan.FromSeconds(10));
            WpfTestHost.Invoke(() =>
            {
                Assert.Same(old.Bitmap, view!.ViewBitmapSource);
                Assert.Same(old.Layer, view.SelectedLayer);
            });

            await Open().WaitAsync(TimeSpan.FromSeconds(10));
            await WpfTestHost.Invoke(() => opener.PrepareMeasurementAsync().AsTask()).WaitAsync(TimeSpan.FromSeconds(10));
            PoiMeasurementResult point = WpfTestHost.Invoke(() => opener.CalculatePoi(points, true).Results[0]);
            Assert.Equal((102f, 106f, 110f), (point.X, point.Y, point.Z));
        }
        finally
        {
            if (view != null) WpfTestHost.Invoke(view.Dispose);
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(".cvraw", false, false)]
    [InlineData(".tif", true, false)]
    [InlineData(".cvraw", false, true)]
    public async Task CvcieDisplaySkipsAnAssociatedImageWithDifferentDimensions(string sourceExtension, bool trueColor, bool hasMatchingSibling)
    {
        string path = Path.Combine(Path.GetTempPath(), $"cv-measurement-geometry-{Guid.NewGuid():N}.cvcie");
        string linked = Path.ChangeExtension(path, null) + "-linked" + sourceExtension;
        string sibling = Path.ChangeExtension(path, ".cvraw");
        ImageView? view = null;
        CvcieDisplayMode originalMode = WpfTestHost.Invoke(() => CvcieDisplayConfig.Current.DisplayMode);
        try
        {
            if (sourceExtension == ".tif")
            {
                using var image = new OpenCvSharp.Mat(1, 4, OpenCvSharp.MatType.CV_8UC3, OpenCvSharp.Scalar.All(88));
                Assert.True(OpenCvSharp.Cv2.ImWrite(linked, image));
            }
            else
            {
                WriteRaw(linked, rows: 1, cols: 4);
            }
            if (hasMatchingSibling) WriteRaw(sibling, rows: 2, cols: 2);
            WriteCvcieMeasurementFixture(path, firstValue: 1, sourcePath: linked);

            CVRawOpen opener = WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                CvcieDisplayConfig.Current.EnableTrueColor = trueColor;
                view = new ImageView();
                view.Config.SetImageMetadata(ImageViewPropertyKeys.FilePath, path, nameof(ImageOpenCompletionContractTests), "measurement target");
                return new CVRawOpen(view.EditorContext);
            });
            await WpfTestHost.Invoke(() => opener.OpenImageAsync(view!.EditorContext, path)).WaitAsync(TimeSpan.FromSeconds(10));

            if (trueColor)
            {
                await WpfTestHost.Invoke(() =>
                {
                    Assert.Equal("cie-srgb", view!.SelectedLayer?.Id);
                    var controller = (CvRawLayerController)typeof(ImageView).GetField("_layerController", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!;
                    return controller.SelectLayerAsync(controller.Layers.Single(item => item.Id == "red"));
                }).WaitAsync(TimeSpan.FromSeconds(10));
            }

            await WpfTestHost.Invoke(() => opener.PrepareMeasurementAsync().AsTask()).WaitAsync(TimeSpan.FromSeconds(10));
            WpfTestHost.Invoke(() =>
            {
                var bitmap = Assert.IsAssignableFrom<BitmapSource>(view!.ViewBitmapSource);
                Assert.Equal((2, 2), (bitmap.PixelWidth, bitmap.PixelHeight));
                Assert.Equal(hasMatchingSibling ? "composite" : "cie-y", view.SelectedLayer?.Id);
                Assert.Equal(hasMatchingSibling ? PixelFormats.Bgr24 : PixelFormats.Gray8, bitmap.Format);
                PoiMeasurementResult point = opener.CalculatePoi([new(1, 1, 1, 1, PoiMeasurementShape.Point)], true).Results[0];
                Assert.Equal((4f, 8f, 12f), (point.X, point.Y, point.Z));
            });
        }
        finally
        {
            WpfTestHost.Invoke(() =>
            {
                view?.Dispose();
                CvcieDisplayConfig.Current.DisplayMode = originalMode;
            });
            File.Delete(path);
            File.Delete(linked);
            File.Delete(sibling);
        }

        static void WriteRaw(string rawPath, int rows, int cols)
        {
            using CVCIEFile raw = new()
            {
                Version = 2, FileExtType = CVType.Raw, Rows = rows, Cols = cols, Bpp = 8, Channels = 3,
                Exp = [1, 1, 1], Data = Enumerable.Repeat((byte)88, rows * cols * 3).ToArray()
            };
            Assert.True(CVFileUtil.WriteCIEFile(rawPath, raw));
        }
    }

    [Fact]
    public async Task CacheOnlyCvRawOpensWithPixelsAndSerializedLengthWithoutADiskFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"colorvision-cache-only-open-{Guid.NewGuid():N}.cvraw");
        bool cacheWasEnabled = CVFileReadCache.IsEnabled;
        int previousMaximumEntries = CVFileReadCache.MaximumEntries;
        ImageView? view = null;
        CVRawOpen? opener = null;
        int loadedCount = 0;
        byte[] samples = [5, 11, 19, 29, 47, 71];
        try
        {
            CVFileReadCache.Release();
            CVFileReadCache.IsEnabled = true;
            CVFileReadCache.MaximumEntries = 1;
            using CVCIEFile raw = new()
            {
                Version = 1, FileExtType = CVType.Raw, Rows = 2, Cols = 3, Bpp = 8, Channels = 1,
                Exp = [1], Data = samples,
            };
            Assert.True(CVFileUtil.WriteCVRaw(path, raw, CVFileSaveMode.MemoryOnly));
            Assert.False(File.Exists(path));
            long cachedLength = Assert.NotNull(CVFileReadCache.GetCachedLength(path));
            Assert.True(cachedLength > samples.Length);

            Task completion = WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                view = new ImageView { EnableEditorImageServices = false };
                opener = new CVRawOpen(view.EditorContext);
                view.IEditorToolFactory.IImageOpens[".cvraw"] = opener;
                view.ImageSourceLoaded += (_, _) => loadedCount++;
                view.OpenImage(path);
                return view.ImageOpenCompletion;
            });
            await completion.WaitAsync(TimeSpan.FromSeconds(10));

            WpfTestHost.Invoke(() =>
            {
                BitmapSource source = Assert.IsAssignableFrom<BitmapSource>(view!.ViewBitmapSource);
                Assert.Equal((3, 2), (source.PixelWidth, source.PixelHeight));
                Assert.Equal(PixelFormats.Gray8, source.Format);
                byte[] pixels = new byte[samples.Length];
                source.CopyPixels(pixels, 3, 0);
                Assert.Equal(samples, pixels);
                Assert.Equal(1, loadedCount);
                Assert.Same(opener, view.EditorContext.IImageOpen);
                Assert.Same(opener, view.ImageContentCapability);
                Assert.Equal(path, view.Config.FilePath);
                Assert.Equal(path, view.Config.GetProperties<string>(ImageViewPropertyKeys.FileSource));
                Assert.Equal(Path.GetFileName(path), view.Config.GetProperties<string>(ImageViewPropertyKeys.FileName));
                Assert.Equal(cachedLength, view.Config.GetProperties<long>(ImageViewPropertyKeys.FileSize));
                Assert.False(view.Config.Properties.ContainsKey(ImageViewPropertyKeys.FileCreationTime));
                Assert.False(view.Config.Properties.ContainsKey(ImageViewPropertyKeys.FileModifiedTime));
            });
            Assert.False(File.Exists(path));
        }
        finally
        {
            try
            {
                if (view != null)
                {
                    Task retirement = WpfTestHost.Invoke(() => { view.Dispose(); return view.PendingContentRelease; });
                    await retirement.WaitAsync(TimeSpan.FromSeconds(10));
                }
            }
            finally
            {
                CVFileReadCache.Release();
                CVFileReadCache.MaximumEntries = previousMaximumEntries;
                CVFileReadCache.IsEnabled = cacheWasEnabled;
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task DiskCvcieUsesPreparedPointsAndKeepsPixelsWhenNextFileIsMissing()
    {
        string prefix = Path.Combine(Path.GetTempPath(), $"colorvision-cvcie-publication-{Guid.NewGuid():N}");
        string path = prefix + ".cvcie";
        string missing = prefix + "-missing.cvcie";
        string pointsPath = prefix + ".csv";
        ImageView? view = null;
        CVRawOpen? opener = null;
        ViewAlgorithmConfig? settings = null;
        string? previousPointsPath = null;
        bool replacedPointsFile = false;
        int loadedCount = 0;
        try
        {
            WriteCvcieMeasurementFixture(path, firstValue: 1);
            long fileSize = new FileInfo(path).Length;
            File.WriteAllLines(pointsPath, ["1,2", "header", "1,2,0,0,3,4,0,0"]);
            Task completion = WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                settings = ViewAlgorithmConfig.Instance;
                previousPointsPath = settings.ShowDateFilePath;
                settings.ShowDateFilePath = pointsPath;
                view = new ImageView { EnableEditorImageServices = false };
                opener = new CVRawOpen(view.EditorContext);
                view.IEditorToolFactory.IImageOpens[".cvcie"] = opener;
                view.ImageSourceLoaded += (_, _) => loadedCount++;
                view.Config.PropertyChanged += (_, args) =>
                {
                    // The host sets the requested path without this notification. The opener's
                    // publishing setter marks the point by which side-file reads must be done.
                    if (args.PropertyName != nameof(ImageViewConfig.FilePath) || view.Config.FilePath != path || replacedPointsFile) return;
                    File.WriteAllLines(pointsPath, ["1,2", "header", "10,20,0,0,30,40,0,0"]);
                    replacedPointsFile = true;
                };
                view.OpenImage(path);
                return view.ImageOpenCompletion;
            });
            await completion.WaitAsync(TimeSpan.FromSeconds(10));

            BitmapSource current = WpfTestHost.Invoke(() =>
            {
                Assert.True(replacedPointsFile);
                Assert.Equal(new[] { new System.Windows.Point(1, 2), new System.Windows.Point(3, 4) }, Assert.Single(opener!.Points));
                Assert.Equal(1, loadedCount);
                Assert.Same(opener, view!.ImageContentCapability);
                Assert.Equal(path, view.Config.FilePath);
                Assert.Equal(path, view.Config.GetProperties<string>(ImageViewPropertyKeys.FileSource));
                Assert.Equal(Path.GetFileName(path), view.Config.GetProperties<string>(ImageViewPropertyKeys.FileName));
                Assert.Equal(fileSize, view.Config.GetProperties<long>(ImageViewPropertyKeys.FileSize));
                var parameters = Assert.IsType<ColorVisionTiffParameters>(view.Document.ParameterRecord);
                Assert.Equal("CVCIE", parameters.SourceType);
                Assert.Equal(3, parameters.SourceChannels);
                Assert.Equal(32, parameters.Bpp);
                return Assert.IsAssignableFrom<BitmapSource>(view.ViewBitmapSource);
            });

            Task missingOpen = WpfTestHost.Invoke(() =>
            {
                view!.OpenImage(missing);
                return view.ImageOpenCompletion;
            });
            FileNotFoundException error = await Assert.ThrowsAsync<FileNotFoundException>(() => missingOpen.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(missing, error.FileName);
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(1, loadedCount);
                Assert.Same(current, view!.ViewBitmapSource);
                Assert.Equal(missing, view.Config.FilePath);
                Assert.Null(view.Config.GetProperties<string>(ImageViewPropertyKeys.FileSource));
                Assert.False(view.Config.Properties.ContainsKey(ImageViewPropertyKeys.FileSize));
                Assert.Null(view.EditorContext.IImageOpen);
                Assert.Null(view.ImageContentCapability);
            });
        }
        finally
        {
            try
            {
                Task retirement = WpfTestHost.Invoke(() =>
                {
                    if (settings != null) settings.ShowDateFilePath = previousPointsPath!;
                    view?.Dispose();
                    return view?.PendingContentRelease ?? Task.CompletedTask;
                });
                await retirement.WaitAsync(TimeSpan.FromSeconds(10));
            }
            finally
            {
                File.Delete(path);
                File.Delete(pointsPath);
            }
        }
    }

    private static void WriteCvcieMeasurementFixture(string path, int firstValue, string? sourcePath = null)
    {
        using CVCIEFile file = new()
        {
            Version = 2, FileExtType = CVType.CIE, Rows = 2, Cols = 2, Bpp = 32, Channels = 3,
            SrcFileName = sourcePath ?? string.Empty,
            Exp = [1, 1, 1], Data = Enumerable.Range(firstValue, 12).SelectMany(value => BitConverter.GetBytes((float)value)).ToArray()
        };
        Assert.True(CVFileUtil.WriteCIEFile(path, file));
    }

    [Theory]
    [InlineData(32, 1u)]
    [InlineData(64, 2u)]
    public async Task DiskCvcieRendersDefaultYAndEachGrayLayerAcrossChunkBoundaries(int bpp, uint version)
    {
        const int width = 257, height = 257, pixelCount = width * height;
        string path = Path.Combine(Path.GetTempPath(), $"cv-gray-layers-{Guid.NewGuid():N}.cvcie");
        ImageView? view = null;
        CvcieDisplayMode originalMode = WpfTestHost.Invoke(() => CvcieDisplayConfig.Current.DisplayMode);
        try
        {
            int sampleBytes = bpp / 8;
            byte[] data = new byte[3 * pixelCount * sampleBytes];
            for (int channel = 0; channel < 3; channel++)
            {
                for (int index = 0; index < pixelCount; index++)
                {
                    int value = GrayValue(index, channel) - 128;
                    int offset = (channel * pixelCount + index) * sampleBytes;
                    if (bpp == 32) BitConverter.TryWriteBytes(data.AsSpan(offset), (float)value);
                    else BitConverter.TryWriteBytes(data.AsSpan(offset), (double)value);
                }
            }
            using CVCIEFile file = new()
            {
                Version = version, FileExtType = CVType.CIE, Rows = height, Cols = width, Bpp = bpp,
                Channels = 3, Exp = [1, 1, 1], Data = data
            };
            Assert.True(CVFileUtil.WriteCIEFile(path, file));

            CVRawOpen opener = WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                CvcieDisplayConfig.Current.EnableTrueColor = false;
                view = new ImageView();
                view.Config.SetImageMetadata(ImageViewPropertyKeys.FilePath, path, nameof(ImageOpenCompletionContractTests), "gray layers");
                using CVCIEFile source = CvRawLayerController.LoadSourceForDisplay(path, out bool usesLuminance, out WriteableBitmap? bitmap);
                Assert.True(usesLuminance);
                Assert.Null(source.Data);
                Assert.NotNull(bitmap);
                return new CVRawOpen(view.EditorContext);
            });
            await WpfTestHost.Invoke(() => opener.OpenImageAsync(view!.EditorContext, path)).WaitAsync(TimeSpan.FromSeconds(10));
            CvRawLayerController controller = WpfTestHost.Invoke(() =>
            {
                Assert.Equal("cie-y", view!.SelectedLayer?.Id);
                AssertPlane(view, 1);
                return (CvRawLayerController)typeof(ImageView).GetField("_layerController", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!;
            });
            foreach (var (id, channel) in new[] { ("cie-x", 0), ("cie-z", 2), ("cie-y", 1) })
            {
                await WpfTestHost.Invoke(() => controller.SelectLayerAsync(controller.Layers.Single(layer => layer.Id == id))).WaitAsync(TimeSpan.FromSeconds(10));
                WpfTestHost.Invoke(() => AssertPlane(view!, channel));
            }
        }
        finally
        {
            WpfTestHost.Invoke(() =>
            {
                view?.Dispose();
                CvcieDisplayConfig.Current.DisplayMode = originalMode;
            });
            File.Delete(path);
        }

        static byte GrayValue(int index, int channel) => (byte)((index * (channel * 2 + 1) + channel * 37) % 256);

        static void AssertPlane(ImageView imageView, int channel)
        {
            var bitmap = Assert.IsAssignableFrom<BitmapSource>(imageView.ViewBitmapSource);
            Assert.Equal(PixelFormats.Gray8, bitmap.Format);
            Assert.Equal((width, height), (bitmap.PixelWidth, bitmap.PixelHeight));
            byte[] pixels = new byte[pixelCount];
            bitmap.CopyPixels(pixels, width, 0);
            Assert.Equal(Enumerable.Range(0, pixelCount).Select(index => GrayValue(index, channel)), pixels);
        }
    }

    [Fact]
    public async Task LargeRgb48RawOpenReplacesFrozenDisplayThenReusesMutableDisplayAndPreservesNativeLease()
    {
        const int width = 2049, height = 683, channels = 3;
        const int rowBytes = width * channels * sizeof(ushort), pixelBytes = rowBytes * height;
        const int firstSeed = 123, secondSeed = 20000;
        string prefix = Path.Combine(Path.GetTempPath(), $"colorvision-large-raw-{Guid.NewGuid():N}");
        string firstPath = prefix + "-first.cvraw";
        string secondPath = prefix + "-second.cvraw";
        ImageView? view = null;
        System.Windows.Window? host = null;
        System.Windows.Window? previousMainWindow = null;
        CVRawOpen? opener = null;
        WriteableBitmap? frozenDisplay = null;
        ImageFrameLease? oldLease = null;
        int loadedCount = 0;
        Matrix viewport = new(1.375, 0, 0, 1.375, -37, 21);
        TimeSpan testWait = TimeSpan.FromSeconds(10);
        try
        {
            WriteFixture(firstPath, firstSeed);
            WriteFixture(secondPath, secondSeed);
            Task firstOpen = WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                view = new ImageView { EnableEditorImageServices = false };
                frozenDisplay = new WriteableBitmap(width, height, 144, 120, PixelFormats.Rgb48, null);
                frozenDisplay.Freeze();
                view.SetImageSource(frozenDisplay);
                opener = new CVRawOpen(view.EditorContext);
                view.IEditorToolFactory.IImageOpens[".cvraw"] = opener;
                view.ImageSourceLoaded += (_, _) => loadedCount++;
                previousMainWindow = Application.Current.MainWindow;
                host = new System.Windows.Window
                {
                    Content = view, Width = 640, Height = 480, Left = -30000, Top = -30000,
                    ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual,
                };
                host.Show();
                host.UpdateLayout();
                Assert.True(view.Zoombox1.ActualWidth > 0);
                view.OpenImage(firstPath);
                return view.ImageOpenCompletion;
            });
            await firstOpen.WaitAsync(testWait);

            byte[] pixels = new byte[pixelBytes];
            var (original, rawBuffer) = WpfTestHost.Invoke(() =>
            {
                Assert.Equal(1, loadedCount);
                var display = Assert.IsType<WriteableBitmap>(view!.ViewBitmapSource);
                Assert.NotSame(frozenDisplay, display);
                Assert.Same(display, view.Presentation.DisplaySource);
                AssertBitmap(display, firstSeed, pixels);
                oldLease = Assert.IsType<ImageFrameLease>(view.AcquireImageFrame());
                view.Zoombox1.RestoreView(viewport);
                return (display, ReadRawBuffer(opener!));
            });

            Task secondOpen = WpfTestHost.Invoke(() =>
            {
                view!.OpenImage(secondPath);
                return view.ImageOpenCompletion;
            });
            await secondOpen.WaitAsync(testWait);
            WpfTestHost.Invoke(() =>
            {
                host!.UpdateLayout();
                Assert.Equal(2, loadedCount);
                Assert.Same(opener, view!.EditorContext.IImageOpen);
                Assert.Same(rawBuffer, ReadRawBuffer(opener!));
                AssertSamples(rawBuffer, secondSeed, rgb: false);
                Assert.Equal(viewport, view.Zoombox1.ContentMatrix);
                Assert.Equal(rowBytes, view.Config.GetProperties<int>(ImageViewPropertyKeys.Stride));

                var next = Assert.IsType<WriteableBitmap>(view.ViewBitmapSource);
                Assert.Same(original, next);
                Assert.Same(next, view.Presentation.DisplaySource);
                AssertBitmap(next, secondSeed, pixels);
                Assert.False(view.IsCurrentImageRevision(oldLease!.Revision));
                Assert.True(frozenDisplay!.IsFrozen);
                frozenDisplay.CopyPixels(pixels, rowBytes, 0);
                Assert.Equal(-1, Array.FindIndex(pixels, value => value != 0));
            });

            HImage frame = oldLease!.Image;
            Assert.Equal((width, height, channels, 16, rowBytes), (frame.cols, frame.rows, frame.channels, frame.depth, frame.stride));
            Marshal.Copy(frame.pData, pixels, 0, pixels.Length);
            AssertSamples(pixels, firstSeed, rgb: true);
        }
        finally
        {
            oldLease?.Dispose();
            try
            {
                Task retirement = WpfTestHost.Invoke(() =>
                {
                    if (host != null)
                    {
                        host.Close();
                        Application.Current.MainWindow = previousMainWindow;
                    }
                    view?.Dispose();
                    return view?.PendingContentRelease ?? Task.CompletedTask;
                });
                await retirement.WaitAsync(testWait);
            }
            finally
            {
                File.Delete(firstPath);
                File.Delete(secondPath);
            }
        }

        static ushort Sample(int pixel, int channel, int seed) => (ushort)(seed + channel * 10000 + pixel % 8191);

        static void WriteFixture(string path, int seed)
        {
            using CVCIEFile raw = new()
            {
                Version = 2, FileExtType = CVType.Raw, Rows = height, Cols = width,
                Bpp = 16, Channels = channels, Exp = [1, 1, 1],
            };
            Assert.True(CVFileUtil.WriteCVRaw(path, raw, pixelBytes, stream =>
            {
                byte[] row = new byte[rowBytes];
                Span<ushort> samples = MemoryMarshal.Cast<byte, ushort>(row.AsSpan());
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    for (int channel = 0; channel < channels; channel++)
                        samples[x * channels + channel] = Sample(y * width + x, channel, seed);
                    stream.Write(row);
                }
            }));
        }

        static byte[] ReadRawBuffer(CVRawOpen opener)
        {
            var raw = Assert.IsType<CvRawPixelBuffer>(typeof(CalibratedImageCapability)
                .GetField("_rawPixels", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(opener));
            return Assert.IsType<byte[]>(typeof(CvRawPixelBuffer)
                .GetField("pixels", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(raw));
        }

        static void AssertBitmap(WriteableBitmap bitmap, int seed, byte[] pixels)
        {
            Assert.False(bitmap.IsFrozen);
            Assert.Equal(PixelFormats.Rgb48, bitmap.Format);
            Assert.Equal((width, height, 96d, 96d), (bitmap.PixelWidth, bitmap.PixelHeight, bitmap.DpiX, bitmap.DpiY));
            Assert.True(bitmap.BackBufferStride > rowBytes);
            bitmap.CopyPixels(pixels, rowBytes, 0);
            AssertSamples(pixels, seed, rgb: true);
        }

        static void AssertSamples(byte[] pixels, int seed, bool rgb)
        {
            Assert.Equal(pixelBytes, pixels.Length);
            ReadOnlySpan<ushort> samples = MemoryMarshal.Cast<byte, ushort>(pixels.AsSpan());
            for (int pixel = 0; pixel < width * height; pixel++)
            for (int channel = 0; channel < channels; channel++)
            {
                ushort expected = Sample(pixel, rgb ? channels - 1 - channel : channel, seed);
                ushort actual = samples[pixel * channels + channel];
                if (actual != expected) Assert.Fail($"Pixel {pixel}, channel {channel}: expected {expected}, found {actual}.");
            }
        }
    }

    [Fact]
    public async Task LargeCvcieLayerSwitchPreservesPreviousDisplayAndNativeLease()
    {
        const int width = 4097, height = 2048, pixelCount = width * height;
        string path = Path.Combine(Path.GetTempPath(), $"colorvision-large-cie-layer-{Guid.NewGuid():N}.cvcie");
        ImageView? view = null;
        ImageFrameLease? oldLease = null;
        CvcieDisplayMode originalMode = WpfTestHost.Invoke(() => CvcieDisplayConfig.Current.DisplayMode);
        int loadedCount = 0;
        TimeSpan testWait = TimeSpan.FromSeconds(10);
        try
        {
            using CVCIEFile file = new()
            {
                Version = 2, FileExtType = CVType.CIE, Rows = height, Cols = width,
                Bpp = 32, Channels = 3, Exp = [1, 1, 1],
            };
            Assert.True(CVFileUtil.WriteCIEFile(path, file, 3L * pixelCount * sizeof(float), stream =>
            {
                byte[] row = new byte[width * sizeof(float)];
                Span<float> samples = MemoryMarshal.Cast<byte, float>(row.AsSpan());
                for (int channel = 0; channel < 3; channel++)
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++) samples[x] = ExpectedGray(y * width + x, channel) - 128;
                    stream.Write(row);
                }
            }));

            Task opened = WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                CvcieDisplayConfig.Current.EnableTrueColor = false;
                view = new ImageView { EnableEditorImageServices = false };
                view.IEditorToolFactory.IImageOpens[".cvcie"] = new CVRawOpen(view.EditorContext);
                view.ImageSourceLoaded += (_, _) => loadedCount++;
                view.OpenImage(path);
                return view.ImageOpenCompletion;
            });
            await opened.WaitAsync(testWait);

            byte[] pixels = new byte[pixelCount];
            var (original, controller) = WpfTestHost.Invoke(() =>
            {
                Assert.Equal(1, loadedCount);
                Assert.Equal("cie-y", view!.SelectedLayer?.Id);
                var initial = Assert.IsType<WriteableBitmap>(view.ViewBitmapSource);
                AssertPlane(initial, 1, pixels);
                oldLease = Assert.IsType<ImageFrameLease>(view.AcquireImageFrame());
                var layers = Assert.IsType<CvRawLayerController>(typeof(ImageView)
                    .GetField("_layerController", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(view));
                return (initial, layers);
            });

            ImageLayerDescriptor xLayer = controller.Layers.Single(layer => layer.Id == "cie-x");
            await WpfTestHost.Invoke(() => controller.SelectLayerAsync(xLayer)).WaitAsync(testWait);
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(2, loadedCount);
                var selected = Assert.IsType<WriteableBitmap>(view!.ViewBitmapSource);
                Assert.NotSame(original, selected);
                AssertPlane(selected, 0, pixels);
                AssertPlane(original, 1, pixels);
                Assert.False(view.IsCurrentImageRevision(oldLease!.Revision));

                var cache = Assert.IsAssignableFrom<object>(typeof(CvRawLayerController)
                    .GetField("_channelCache", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(controller));
                var cached = Assert.IsAssignableFrom<BitmapSource>(cache.GetType().GetProperty("Bitmap")?.GetValue(cache));
                Assert.True(cached.IsFrozen);
                Assert.NotSame(cached, selected);
                var firstPixel = new Int32Rect(0, 0, 1, 1);
                selected.WritePixels(firstPixel, new byte[] { 123 }, 1, 0);
                byte[] sample = new byte[1];
                cached.CopyPixels(firstPixel, sample, 1, 0);
                Assert.Equal(ExpectedGray(0, 0), sample[0]);
                Assert.Equal(2, loadedCount);
            });

            HImage oldFrame = oldLease!.Image;
            Assert.Equal((width, height, width), (oldFrame.cols, oldFrame.rows, oldFrame.stride));
            Marshal.Copy(oldFrame.pData, pixels, 0, pixels.Length);
            AssertPixels(pixels, 1);
        }
        finally
        {
            oldLease?.Dispose();
            try
            {
                Task retirement = WpfTestHost.Invoke(() =>
                {
                    view?.Dispose();
                    CvcieDisplayConfig.Current.DisplayMode = originalMode;
                    return view?.PendingContentRelease ?? Task.CompletedTask;
                });
                await retirement.WaitAsync(testWait);
            }
            finally { File.Delete(path); }
        }

        static byte ExpectedGray(int index, int channel) => (byte)((index * (channel * 2 + 1) + channel * 37) & 255);

        static void AssertPlane(WriteableBitmap bitmap, int channel, byte[] pixels)
        {
            Assert.False(bitmap.IsFrozen);
            Assert.Equal(PixelFormats.Gray8, bitmap.Format);
            Assert.Equal((width, height), (bitmap.PixelWidth, bitmap.PixelHeight));
            bitmap.CopyPixels(pixels, width, 0);
            AssertPixels(pixels, channel);
        }

        static void AssertPixels(byte[] pixels, int channel)
        {
            for (int index = 0; index < pixels.Length; index++)
            {
                byte expected = ExpectedGray(index, channel);
                if (pixels[index] != expected) Assert.Fail($"Channel {channel}, pixel {index}: expected {expected}, found {pixels[index]}.");
            }
        }
    }

    [Theory]
    [InlineData(1, 1u)]
    [InlineData(3, 2u)]
    public async Task DiskCvcieProbesMatchInMemoryGeometryWithoutRetainingPixels(int channels, uint version)
    {
        const int width = 43, height = 32;
        string path = Path.Combine(Path.GetTempPath(), $"colorvision-disk-probe-{Guid.NewGuid():N}.cvcie");
        byte[] pixels = new byte[width * height * channels * sizeof(float)];
        FillFixture(pixels, channels, changed: false);
        ImageView? view = null;
        CVRawOpen? opener = null;
        FieldInfo measurementBuffer = typeof(CalibratedImageCapability).GetField("_measurementBuffer", BindingFlags.NonPublic | BindingFlags.Instance)!;
        PoiMeasurementPoint[] points =
        [
            new(0, 0, 1, 1, PoiMeasurementShape.Point),
            new(20, 16, 1, 1, PoiMeasurementShape.Point),
            new(20, 16, 5, 5, PoiMeasurementShape.Circle),
            new(20, 16, 6, 6, PoiMeasurementShape.Circle),
            new(20, 16, 5, 7, PoiMeasurementShape.Rect),
            new(20, 16, 6, 4, PoiMeasurementShape.Rect),
            // Translating this ellipse's center can add a sample at X=17, Y=10 due to floating-point rounding.
            new(20, 16, 10, 15, PoiMeasurementShape.Ellipse),
            new(0, 0, 6, 6, PoiMeasurementShape.Circle),
            new(width - 1, height - 1, 6, 5, PoiMeasurementShape.Rect),
            new(width - 1, 0, 7, 4, PoiMeasurementShape.Ellipse),
        ];
        TimeSpan timeout = TimeSpan.FromSeconds(10);
        try
        {
            WriteFixture();
            using (FileStream stream = File.OpenRead(path))
            {
                int headerEnd = CVFileUtil.ReadCIEFileHeader(stream, out CVCIEFile header);
                using (header)
                    Assert.NotEqual(0L, CvcieDiskPixels.Locate(stream, header, headerEnd, pixels.Length, requireExactLength: true).Offset % sizeof(float));
            }
            CvcieFileRevision revision = CvcieFileRevision.Capture(path);
            await OpenAsync(clear: false).WaitAsync(timeout);
            using var reference = new PoiMeasurementBuffer(pixels, width, height, 32, channels);
            PoiMeasurementResult[] expected = PoiMeasurementService.Calculate(reference, points);
            Assert.NotEqual(expected[2], expected[4]);
            if (channels == 1) Assert.True(expected[0].Y < 0);
            else Assert.Equal(0.000001f, expected[0].X);
            for (int i = 0; i < points.Length; i++)
            {
                CvcieProbeResult result = Assert.IsType<CvcieProbeResult>(await ProbeAsync(points[i]).WaitAsync(timeout));
                Assert.Equal(channels, result.Channels);
                Assert.Equal(expected[i], result.Measurement);
            }
            Assert.Null(WpfTestHost.Invoke(() => measurementBuffer.GetValue(opener)));
            using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }

            FillFixture(pixels, channels, changed: true);
            WriteFixture();
            File.SetLastWriteTimeUtc(path, revision.LastWriteTimeUtc.AddMinutes(-5));
            Assert.Equal(revision.Length, new FileInfo(path).Length);
            Assert.Null(await ProbeAsync(points[1]).WaitAsync(timeout));
            Assert.Null(WpfTestHost.Invoke(() => measurementBuffer.GetValue(opener)));

            await OpenAsync(clear: true).WaitAsync(timeout);
            var updated = Assert.IsType<CvcieProbeResult>(await ProbeAsync(points[1]).WaitAsync(timeout));
            Assert.NotEqual(expected[1], updated.Measurement);
            Assert.Equal(PoiMeasurementService.Calculate(reference, points[1]), updated.Measurement);
        }
        finally
        {
            try
            {
                Task retirement = WpfTestHost.Invoke(() =>
                {
                    view?.Dispose();
                    return view?.PendingContentRelease ?? Task.CompletedTask;
                });
                await retirement.WaitAsync(timeout);
            }
            finally { File.Delete(path); }
        }

        Task OpenAsync(bool clear) => WpfTestHost.Invoke(() =>
        {
            if (view == null)
            {
                EnsureImageViewTestResources();
                view = new ImageView();
                opener = new CVRawOpen(view.EditorContext);
                view.IEditorToolFactory.IImageOpens[".cvcie"] = opener;
            }
            if (clear) view.Clear();
            view.OpenImage(path);
            return view.ImageOpenCompletion;
        });

        Task<CvcieProbeResult?> ProbeAsync(PoiMeasurementPoint point)
            => WpfTestHost.Invoke(() => opener!.CalculateProbeAsync(point, CancellationToken.None).AsTask());

        void WriteFixture()
        {
            using CVCIEFile file = new()
            {
                Version = version, FileExtType = CVType.CIE, Rows = height, Cols = width,
                Bpp = 32, Channels = channels, Exp = Enumerable.Repeat(1f, channels).ToArray(), Data = pixels,
            };
            Assert.True(CVFileUtil.WriteCIEFile(path, file));
        }

        static void FillFixture(byte[] data, int channels, bool changed)
        {
            Span<float> samples = MemoryMarshal.Cast<byte, float>(data.AsSpan());
            for (int channel = 0; channel < channels; channel++)
            {
                int plane = channel * width * height;
                for (int pixel = 0; pixel < width * height; pixel++)
                    samples[plane + pixel] = ((pixel * (channel + 3) + channel * 37) % 163) - 41.25f + (changed ? 175 : 0);
                samples[plane + 10 * width + 17] += 1234.5f;
            }
        }
    }

    [Fact]
    public async Task DiskCvcieProbeStillMeasuresRegionsLargerThanTheCropBudget()
    {
        const int width = 850, height = 850, channels = 3;
        string path = Path.Combine(Path.GetTempPath(), $"colorvision-large-probe-{Guid.NewGuid():N}.cvcie");
        byte[] pixels = CreatePixels();
        PoiMeasurementPoint point = new(width / 2, height / 2, width, height, PoiMeasurementShape.Rect);
        ImageView? view = null;
        CVRawOpen? opener = null;
        TimeSpan timeout = TimeSpan.FromSeconds(10);
        try
        {
            using var fixture = new CVCIEFile
            {
                Version = 2, FileExtType = CVType.CIE, Rows = height, Cols = width,
                Bpp = 32, Channels = channels, Exp = [1, 1, 1], Data = pixels,
            };
            Assert.True(CVFileUtil.WriteCIEFile(path, fixture));
            await WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                view = new ImageView();
                opener = new CVRawOpen(view.EditorContext);
                view.IEditorToolFactory.IImageOpens[".cvcie"] = opener;
                view.OpenImage(path);
                return view.ImageOpenCompletion;
            }).WaitAsync(timeout);

            using var reference = new PoiMeasurementBuffer(pixels, width, height, 32, channels);
            PoiMeasurementResult expected = PoiMeasurementService.Calculate(reference, point);
            var actual = Assert.IsType<CvcieProbeResult>(await WpfTestHost.Invoke(() =>
                opener!.CalculateProbeAsync(point, CancellationToken.None).AsTask()).WaitAsync(timeout));
            Assert.Equal(channels, actual.Channels);
            Assert.Equal(expected, actual.Measurement);
            using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        }
        finally
        {
            try
            {
                await WpfTestHost.Invoke(() =>
                {
                    view?.Dispose();
                    return view?.PendingContentRelease ?? Task.CompletedTask;
                }).WaitAsync(timeout);
            }
            finally { File.Delete(path); }
        }

        static byte[] CreatePixels()
        {
            const int planePixels = width * height;
            byte[] data = new byte[planePixels * channels * sizeof(float)];
            Span<float> samples = MemoryMarshal.Cast<byte, float>(data.AsSpan());
            for (int channel = 0; channel < channels; channel++)
            for (int index = 0; index < planePixels; index++)
                samples[channel * planePixels + index] = index % 191 + (index / width) * 0.125f + channel + 1;
            return data;
        }
    }

    [Theory]
    [InlineData("continue")]
    [InlineData("reset")]
    [InlineData("replace")]
    public async Task CvcieProbeCoalescesSamplesAndKeepsItsSlotUntilTheWorkerReturns(string transition)
    {
        var first = new ImagePixelSample { PixelX = 2, PixelY = 3 };
        var skipped = new ImagePixelSample { PixelX = 5, PixelY = 4 };
        var latest = new ImagePixelSample { PixelX = 8, PixelY = 6 };
        PoiMeasurementPoint firstPoint = new(2, 3, 7, 7, PoiMeasurementShape.Circle);
        PoiMeasurementPoint skippedPoint = new(5, 4, 2, 4, PoiMeasurementShape.Rect);
        PoiMeasurementPoint latestPoint = new(8, 6, 9, 5, PoiMeasurementShape.Ellipse);
        CvcieProbeResult firstResult = new(3, new(1, 2, 3, 4, 5, 6, 7, 8, 9));
        CvcieProbeResult latestResult = new(1, new(0, 11, 0, 0, 0, 0, 0, 0, 0));
        var firstWorker = new TaskCompletionSource<CvcieProbeResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var latestRendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new List<(PoiMeasurementPoint Point, CancellationToken Token)>();
        var rendered = new List<(ImagePixelSample Sample, PoiMeasurementPoint Point, CvcieProbeResult Result)>();
        ImageView? view = null;
        CvcieProbeUpdates? updates = null;
        try
        {
            WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                view = new ImageView();
                updates = new CvcieProbeUpdates(view, (point, token) =>
                {
                    started.Add((point, token));
                    return point == firstPoint ? new(firstWorker.Task) : new(latestResult);
                }, (sample, point, result) =>
                {
                    rendered.Add((sample, point, result));
                    if (ReferenceEquals(sample, latest)) latestRendered.TrySetResult();
                });
                updates.Update(first, firstPoint);
                updates.Update(skipped, skippedPoint);
                if (transition == "reset") updates.Reset();
                if (transition == "replace")
                {
                    long version = view.ContentVersion;
                    view.Clear();
                    Assert.NotEqual(version, view.ContentVersion);
                }
                updates.Update(latest, latestPoint);
                var active = Assert.Single(started);
                Assert.Equal(firstPoint, active.Point);
                if (transition == "reset") Assert.True(active.Token.IsCancellationRequested);
                Assert.Empty(rendered);
            });

            // This fake deliberately ignores cancellation, like an already-running synchronous native call.
            firstWorker.SetResult(firstResult);
            await latestRendered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(new[] { firstPoint, latestPoint }, started.Select(request => request.Point));
                Assert.Equal((latest, latestPoint, latestResult), rendered[^1]);
                if (transition == "continue")
                {
                    if (rendered.Count == 2) Assert.Equal((first, firstPoint, firstResult), rendered[0]);
                    else Assert.Single(rendered);
                }
                else Assert.Single(rendered);
            });
        }
        finally
        {
            WpfTestHost.Invoke(() =>
            {
                updates?.Reset();
                firstWorker.TrySetResult(null);
                view?.Dispose();
            });
        }
    }

    [Theory]
    [InlineData(".png")]
    [InlineData(".tiff")]
    [InlineData(".cvraw")]
    public async Task OpenImage_ReplacesMatchingFrozenStreamSourceWithoutChangingOldPixels(string extension)
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"{nameof(ImageOpenCompletionContractTests)}-{Guid.NewGuid():N}{extension}");
        ImageView? imageView = null;
        WriteableBitmap? frozenSource = null;
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int completionCount = 0;
        try
        {
            WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                if (extension == ".cvraw") WriteCvRaw(filePath, 200);
                else
                {
                    BitmapSource source = BitmapSource.Create(2, 3, 96, 96, PixelFormats.Gray8, null,
                        Enumerable.Repeat((byte)200, 6).ToArray(), 2);
                    BitmapEncoder encoder = extension == ".png" ? new PngBitmapEncoder() : new TiffBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(source));
                    using FileStream file = new(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    encoder.Save(file);
                }

                imageView = new ImageView { EnableEditorImageServices = false };
                imageView.IEditorToolFactory.IEditorTools.Clear();
                if (extension == ".cvraw")
                    imageView.IEditorToolFactory.IImageOpens[extension] = new ColorVision.Engine.Media.CVRawOpen(imageView.EditorContext);
                imageView.EditorContext.ProcessingContext.DisplayEffects.PseudoColor.IsEnabled = false;
                WriteableBitmap scratch = new(2, 3, 96, 96, PixelFormats.Gray8, null);
                scratch.WritePixels(new Int32Rect(0, 0, 2, 3), Enumerable.Repeat((byte)10, 6).ToArray(), 2, 0);
                imageView.EditorContext.ProcessingContext.StreamPresentation.Submit(scratch, Guid.NewGuid());
                frozenSource = Assert.IsType<WriteableBitmap>(imageView.ViewBitmapSource);
                Assert.True(frozenSource.IsFrozen);
                imageView.ImageSourceLoaded += (_, _) =>
                {
                    completionCount++;
                    completion.TrySetResult();
                };

                imageView.OpenImage(filePath);
            });

            await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));

            WpfTestHost.Invoke(() =>
            {
                WriteableBitmap replacement = Assert.IsType<WriteableBitmap>(imageView!.ViewBitmapSource);
                Assert.NotSame(frozenSource, replacement);
                Assert.False(replacement.IsFrozen);
                Assert.Equal(frozenSource!.PixelWidth, replacement.PixelWidth);
                Assert.Equal(frozenSource.PixelHeight, replacement.PixelHeight);
                Assert.Equal(frozenSource.Format, replacement.Format);
                byte[] oldPixels = new byte[6];
                byte[] newPixels = new byte[6];
                frozenSource.CopyPixels(oldPixels, 2, 0);
                replacement.CopyPixels(newPixels, 2, 0);
                Assert.Equal(Enumerable.Repeat((byte)10, 6), oldPixels);
                Assert.Equal(Enumerable.Repeat((byte)200, 6), newPixels);
                Assert.Same(replacement, imageView.Presentation.DisplaySource);
                Assert.Equal(filePath, imageView.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath));
                Assert.Equal(1, completionCount);
            });
        }
        finally
        {
            if (imageView != null) WpfTestHost.Invoke(imageView.Dispose);
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [Theory]
    [InlineData(".png", typeof(CommonImageOpen))]
    [InlineData(".tiff", typeof(Opentif))]
    public async Task OpenImage_SuccessRaisesOneCompletionWithFinalState(
        string extension,
        Type expectedOpenerType)
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"{nameof(ImageOpenCompletionContractTests)}-{Guid.NewGuid():N}{extension}");
        ImageView? imageView = null;
        EventHandler<ImageViewImageSourceLoadedEventArgs>? loadedHandler = null;
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int completionCount = 0;
        Task openCompletion = Task.CompletedTask;

        try
        {
            WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                WriteEncodedImage(filePath);
                File.SetCreationTimeUtc(filePath, new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc));
                File.SetLastWriteTimeUtc(filePath, new DateTime(2021, 2, 3, 4, 5, 6, DateTimeKind.Utc));

                imageView = new ImageView();
                loadedHandler = (_, _) =>
                {
                    completionCount++;
                    completion.TrySetResult();
                };
                imageView.ImageSourceLoaded += loadedHandler;

                imageView.OpenImage(filePath);
                openCompletion = imageView.ImageOpenCompletion;
            });

            await openCompletion.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(completion.Task.IsCompletedSuccessfully);

            var file = new FileInfo(filePath);
            var (length, created, modified) = (file.Length, file.CreationTime, file.LastWriteTime);
            OpenedImageState state = WpfTestHost.Invoke(() =>
            {
                Assert.Equal(length, imageView!.Config.GetProperties<long>(ImageViewPropertyKeys.FileSize));
                Assert.Equal(created, imageView.Config.GetProperties<DateTime>(ImageViewPropertyKeys.FileCreationTime));
                Assert.Equal(modified, imageView.Config.GetProperties<DateTime>(ImageViewPropertyKeys.FileModifiedTime));
                return CaptureState(imageView, completionCount);
            });

            Assert.Equal(1, state.CompletionCount);
            Assert.Equal(2, state.PixelWidth);
            Assert.Equal(3, state.PixelHeight);
            Assert.Equal(Path.GetFileName(filePath), state.FileName);
            Assert.Equal(filePath, state.FilePath);
            Assert.Equal(filePath, state.FileSource);
            Assert.Equal(2, state.MetadataWidth);
            Assert.Equal(3, state.MetadataHeight);
            Assert.Equal(expectedOpenerType, state.OpenerType);
        }
        finally
        {
            if (imageView != null)
            {
                WpfTestHost.Invoke(() =>
                {
                    if (loadedHandler != null)
                        imageView.ImageSourceLoaded -= loadedHandler;
                    imageView.Dispose();
                });
            }

            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Theory]
    [InlineData(".png")]
    [InlineData(".tiff")]
    public async Task OpenImage_WhenPathChangesBeforeDecodeCompletes_DiscardsStaleFrame(string extension)
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"{nameof(ImageOpenCompletionContractTests)}-{Guid.NewGuid():N}{extension}");
        string missingFilePath = Path.Combine(
            Path.GetTempPath(),
            $"{nameof(ImageOpenCompletionContractTests)}-missing-{Guid.NewGuid():N}{extension}");
        ImageView? imageView = null;
        int completionCount = 0;
        Task firstCompletion = Task.CompletedTask;

        try
        {
            WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                WriteEncodedImage(filePath);

                imageView = new ImageView();
                imageView.ImageSourceLoaded += (_, _) => completionCount++;

                imageView.OpenImage(filePath);
                firstCompletion = imageView.ImageOpenCompletion;
                imageView.OpenImage(missingFilePath);
            });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => firstCompletion.WaitAsync(TimeSpan.FromSeconds(10)));

            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(missingFilePath, imageView!.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath));
                Assert.Null(imageView.ViewBitmapSource);
                Assert.Equal(0, completionCount);
            });
        }
        finally
        {
            if (imageView != null)
                WpfTestHost.Invoke(imageView.Dispose);

            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnsupportedFilePreservesCurrentImageDocumentAndGroup(bool droppedOnWorkspace)
    {
        string imagePath = Path.Combine(Path.GetTempPath(), $"colorvision-open-{Guid.NewGuid():N}.png");
        string unsupportedPath = Path.ChangeExtension(imagePath, ".mp4");
        string[] groupPaths = [imagePath, Path.ChangeExtension(imagePath, ".tiff")];
        var capability = new AttachmentCapability();
        ImageView? view = null;

        try
        {
            await WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                WriteEncodedImage(imagePath);
                File.WriteAllBytes(unsupportedPath, []);
                view = new ImageView();
                view.RegisterImageContentProvider(new AttachmentContentProvider(capability));
                view.OpenImages(groupPaths);
                return view.ImageOpenCompletion;
            });

            WpfTestHost.Invoke(() =>
            {
                ImageView current = view!;
                BitmapSource pixels = Assert.IsAssignableFrom<BitmapSource>(current.ViewBitmapSource);
                var content = Assert.IsType<DecodedImageContent>(current.ImageContent);
                Assert.Same(capability, current.ImageContentCapability);
                var opener = Assert.IsAssignableFrom<IImageOpen>(current.EditorContext.IImageOpen);
                Task completion = current.ImageOpenCompletion;
                long revision = current.ImageRevision;
                long length = current.Config.GetProperties<long>(ImageViewPropertyKeys.FileSize);
                int changes = 0;
                current.SelectedImageChanged += (_, _) => changes++;

                if (droppedOnWorkspace)
                {
                    var host = new Border();
                    ImageView.SetForwardUnsupportedFileDrops(host, true);
                    host.Child = current;
                    Assert.False(current.TryHandleFileDrop(unsupportedPath));
                }
                else
                {
                    Assert.False(current.TryOpenImage(unsupportedPath));
                }

                Assert.Equal(groupPaths, current.ImageGroupItems.Select(item => item.FilePath));
                Assert.Equal(0, current.SelectedImageIndex);
                Assert.Equal(0, changes);
                Assert.Equal(revision, current.ImageRevision);
                Assert.Equal(imagePath, current.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath));
                Assert.Equal(length, current.Config.GetProperties<long>(ImageViewPropertyKeys.FileSize));
                Assert.Same(opener, current.EditorContext.IImageOpen);
                Assert.Same(completion, current.ImageOpenCompletion);
                Assert.Same(pixels, current.ViewBitmapSource);
                Assert.Same(content, current.ImageContent);
                Assert.Same(capability, current.ImageContentCapability);
                Assert.Equal(0, capability.DisposeCalls);
            });
        }
        finally
        {
            if (view != null) WpfTestHost.Invoke(view.Dispose);
            File.Delete(imagePath);
            File.Delete(unsupportedPath);
        }
    }

    [Fact]
    public async Task RestoreOriginalImageAfterProcessingReloadsCurrentSourceThroughMenuCommand()
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"{nameof(ImageOpenCompletionContractTests)}-{Guid.NewGuid():N}.png");
        ImageView? imageView = null;
        EventHandler<ImageViewImageSourceLoadedEventArgs>? loadedHandler = null;
        TaskCompletionSource firstLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource restoredLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int completionCount = 0;

        try
        {
            WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                WriteEncodedImage(filePath);

                imageView = new ImageView();
                loadedHandler = (_, _) =>
                {
                    completionCount++;
                    if (completionCount == 1)
                        firstLoad.TrySetResult();
                    else if (completionCount == 2)
                        restoredLoad.TrySetResult();
                };
                imageView.ImageSourceLoaded += loadedHandler;
                imageView.OpenImage(filePath);
            });

            await firstLoad.Task.WaitAsync(TimeSpan.FromSeconds(10));

            WpfTestHost.Invoke(() =>
            {
                byte[] processedPixels = new byte[2 * 3 * 4];
                for (int offset = 3; offset < processedPixels.Length; offset += 4)
                    processedPixels[offset] = 0xFF;

                WriteableBitmap processed = new(BitmapSource.Create(
                    2,
                    3,
                    96,
                    96,
                    PixelFormats.Bgra32,
                    null,
                    processedPixels,
                    8));
                imageView!.ViewBitmapSource = processed;
                imageView.ImageShow.Source = processed;
                imageView.NotifySourcePixelsChanged();

                Assert.True(imageView.CanRestoreOriginalImage);
                AppCommandContextMenu menuProvider = Assert.Single(
                    imageView.IEditorToolFactory.IIEditorToolContextMenus.OfType<AppCommandContextMenu>());
                var restoreMenuItem = Assert.Single(
                    menuProvider.GetContextMenuItems(),
                    item => item.GuidId == "RestoreOriginalImage");
                Assert.True(restoreMenuItem.Command!.CanExecute(null));
                restoreMenuItem.Command.Execute(null);
            });

            await restoredLoad.Task.WaitAsync(TimeSpan.FromSeconds(10));

            WpfTestHost.Invoke(() =>
            {
                BitmapSource restored = Assert.IsAssignableFrom<BitmapSource>(imageView!.ViewBitmapSource);
                byte[] pixel = new byte[4];
                restored.CopyPixels(new Int32Rect(0, 0, 1, 1), pixel, 4, 0);

                Assert.Equal(0x00, pixel[0]);
                Assert.Equal(0x00, pixel[1]);
                Assert.Equal(0xFF, pixel[2]);
                Assert.Equal(0xFF, pixel[3]);
                Assert.Equal(2, completionCount);
            });
        }
        finally
        {
            if (imageView != null)
            {
                WpfTestHost.Invoke(() =>
                {
                    if (loadedHandler != null)
                        imageView.ImageSourceLoaded -= loadedHandler;
                    imageView.Dispose();
                });
            }

            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Theory]
    [InlineData(".png")]
    [InlineData(".tiff")]
    public async Task PendingStaticOpenPreservesCurrentPixelsAndCachedFrameUntilPublication(string extension)
    {
        string path = Path.Combine(Path.GetTempPath(), $"colorvision-pending-content-{Guid.NewGuid():N}{extension}");
        string missingPath = path + ".missing" + extension;
        byte[] oldPixels = Enumerable.Repeat((byte)10, 6).ToArray();
        byte[] newPixels = Enumerable.Repeat((byte)200, 6).ToArray();
        ImageView? view = null;
        WriteableBitmap? original = null;
        AlgorithmImageBuffer? retainedCanonical = null;
        ImageFrameLease? retainedLease = null;
        EventHandler? baselineHandler = null;
        DelayedContentProvider provider = new();
        Task completion = Task.CompletedTask;
        Task replacement = Task.CompletedTask;
        PendingOpenSourceState? before = null;
        PendingOpenSourceState? during = null;
        PendingOpenSourceState? afterCancellation = null;
        byte[] retainedCanonicalPixels = [];
        byte[] retainedNativePixels = [];
        int loadedCount = 0;
        bool pendingDuringPreparation = false;
        Exception? openError = null;
        Exception? replacementError = null;
        string assemblyPath = typeof(CommonImageOpen).Assembly.Location;
        output.WriteLine($"ImageEditor: {assemblyPath}; SHA256={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assemblyPath)))}");
        try
        {
            WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                BitmapSource encoded = BitmapSource.Create(2, 3, 96, 96, PixelFormats.Gray8, null, newPixels, 2);
                BitmapEncoder encoder = extension == ".png" ? new PngBitmapEncoder() : new TiffBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(encoded));
                using (FileStream file = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) encoder.Save(file);

                view = new ImageView { EnableEditorImageServices = false };
                view.IEditorToolFactory.IEditorTools.Clear();
                view.EditorContext.ProcessingContext.DisplayEffects.PseudoColor.IsEnabled = false;
                view.IEditorToolFactory.IImageOpens[extension] = extension == ".png"
                    ? new CommonImageOpen(view.EditorContext) : new Opentif(view.EditorContext);
                view.RegisterImageContentProvider(provider);
                // PNG DPI can round during encoding. Match the actual decoded layout so the
                // existing mutable source qualifies for the file opener's reuse path.
                using (FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    BitmapSource decoded = BitmapDecoder.Create(file, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
                    original = extension == ".png" ? new WriteableBitmap(decoded)
                        : new WriteableBitmap(2, 3, 96, 96, decoded.Format, decoded.Palette);
                }
                original.WritePixels(new Int32Rect(0, 0, 2, 3), oldPixels, 2, 0);
                view.SetImageSource(original);
                view.ImageSourceLoaded += (_, _) => loadedCount++;
                // This later handler runs after the session retires the old revision, but before
                // normal OpenImage invokes the decoder. Keep both representations of that revision.
                baselineHandler = (_, _) =>
                {
                    view.Config.Cleared -= baselineHandler;
                    (retainedCanonical, _) = ImageAlgorithmInputFactory.AcquireCurrentFrame(view.EditorContext.ProcessingContext);
                    retainedLease = view.AcquireImageFrame() ?? throw new InvalidOperationException("The mutable Gray8 source has no native frame.");
                    before = CapturePendingOpenSource(view, original);
                };
                view.Config.Cleared += baselineHandler;
                view.OpenImage(path);
                completion = view.ImageOpenCompletion;
            });
            await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            WpfTestHost.Invoke(() =>
            {
                during = CapturePendingOpenSource(view!, original!);
                pendingDuringPreparation = !completion.IsCompleted;
                view!.OpenImage(missingPath);
                replacement = view.ImageOpenCompletion;
            });
            try { await replacement; }
            catch (Exception error) { replacementError = error; }
            provider.Continue.TrySetResult();
            try { await completion.WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (Exception error) { openError = error; }
            WpfTestHost.Invoke(() =>
            {
                afterCancellation = CapturePendingOpenSource(view!, original!);
                retainedCanonicalPixels = retainedCanonical!.Data.ToArray();
                retainedNativePixels = ReadPendingOpenNativePixels(retainedLease!);
                Assert.Equal(missingPath, view!.Config.FilePath);
                Assert.Null(view.EditorContext.IImageOpen);
                Assert.False(view.Config.Properties.ContainsKey(ImageViewPropertyKeys.FileSize));
            });
            // Record all observations and the settled lifecycle before evaluating pixel assertions.
            output.WriteLine(JsonSerializer.Serialize(new
            {
                before, during, afterCancellation, retainedCanonicalPixels, retainedNativePixels,
                pendingDuringPreparation, completion.IsCanceled, loadedCount,
                openError = openError?.GetType().FullName, replacementError = replacementError?.GetType().FullName,
                provider.Capability.Activated, provider.Capability.Disposed
            }));
            Assert.NotNull(before);
            Assert.NotNull(during);
            Assert.NotNull(afterCancellation);
            Assert.Equal(oldPixels, before.SourcePixels);
            Assert.Equal(oldPixels, before.CanonicalPixels);
            Assert.Equal(oldPixels, before.NativePixels);
            Assert.True(before.SourceIsOriginal);
            Assert.False(before.SourceIsFrozen);
            Assert.Equal(before.Revision, before.CanonicalRevision);
            Assert.Equal(before.Revision, before.NativeRevision);
            Assert.True(pendingDuringPreparation);
            Assert.True(during.SourceIsOriginal);
            Assert.Equal(before.Revision, during.Revision);
            Assert.Equal(during.Revision, during.CanonicalRevision);
            Assert.Equal(during.Revision, during.NativeRevision);
            Assert.Equal(oldPixels, during.SourcePixels);
            Assert.Equal(oldPixels, during.CanonicalPixels);
            Assert.Equal(oldPixels, during.NativePixels);
            Assert.Equal(oldPixels, retainedCanonicalPixels);
            Assert.Equal(oldPixels, retainedNativePixels);
            Assert.True(afterCancellation.SourceIsOriginal);
            Assert.Equal(oldPixels, afterCancellation.SourcePixels);
            Assert.Equal(oldPixels, afterCancellation.CanonicalPixels);
            Assert.Equal(oldPixels, afterCancellation.NativePixels);
            Assert.IsAssignableFrom<OperationCanceledException>(openError);
            Assert.IsType<FileNotFoundException>(replacementError);
            Assert.True(completion.IsCanceled);
            Assert.Equal(0, loadedCount);
            Assert.True(provider.Capability.Disposed);
            Assert.False(provider.Capability.Activated);
        }
        finally
        {
            provider.Continue.TrySetResult();
            if (!completion.IsCompleted)
            {
                try { await completion.WaitAsync(TimeSpan.FromSeconds(10)); }
                catch (Exception) { }
            }
            retainedLease?.Dispose();
            retainedCanonical?.Dispose();
            if (view != null) WpfTestHost.Invoke(() =>
            {
                if (baselineHandler != null) view.Config.Cleared -= baselineHandler;
                view.Dispose();
            });
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static PendingOpenSourceState CapturePendingOpenSource(ImageView view, WriteableBitmap original)
    {
        BitmapSource source = (BitmapSource)view.ViewBitmapSource;
        byte[] pixels = new byte[6];
        source.CopyPixels(pixels, 2, 0);
        (AlgorithmImageBuffer canonical, long revision) = ImageAlgorithmInputFactory.AcquireCurrentFrame(view.EditorContext.ProcessingContext);
        using (canonical)
        using (ImageFrameLease native = view.AcquireImageFrame() ?? throw new InvalidOperationException("The mutable Gray8 source has no native frame."))
            return new(pixels, canonical.Data.ToArray(), ReadPendingOpenNativePixels(native), view.ImageRevision,
                revision, native.Revision, native.Image.pData.ToInt64(), ReferenceEquals(source, original), source.IsFrozen);
    }

    private static byte[] ReadPendingOpenNativePixels(ImageFrameLease lease)
    {
        HImage image = lease.Image;
        byte[] pixels = new byte[6];
        int stride = image.stride == 0 ? 2 : image.stride;
        for (int row = 0; row < 3; row++) Marshal.Copy(IntPtr.Add(image.pData, row * stride), pixels, row * 2, 2);
        return pixels;
    }

    private sealed record PendingOpenSourceState(byte[] SourcePixels, byte[] CanonicalPixels, byte[] NativePixels,
        long Revision, long CanonicalRevision, long NativeRevision, long NativeData, bool SourceIsOriginal, bool SourceIsFrozen);

    [Theory]
    [InlineData(".png")]
    [InlineData(".tiff")]
    public async Task ClearWhileContentProviderIsPreparingDisposesLateCapability(string extension)
    {
        string path = Path.Combine(Path.GetTempPath(), $"colorvision-late-content-{Guid.NewGuid():N}{extension}");
        ImageView? view = null;
        DelayedContentProvider provider = new();
        Task completion = Task.CompletedTask;
        int loadedCount = 0;
        try
        {
            WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                WriteEncodedImage(path);
                view = new ImageView { EnableEditorImageServices = false };
                view.RegisterImageContentProvider(provider);
                view.ImageSourceLoaded += (_, _) => loadedCount++;
                view.OpenImage(path);
                completion = view.ImageOpenCompletion;
            });
            await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            WpfTestHost.Invoke(() => view!.Clear());
            provider.Continue.TrySetResult();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => completion.WaitAsync(TimeSpan.FromSeconds(10)));
            WpfTestHost.Invoke(() =>
            {
                Assert.Null(view!.ViewBitmapSource);
                Assert.Null(view.ImageContent);
                Assert.Null(view.ImageContentCapability);
                Assert.Equal(0, loadedCount);
                Assert.True(provider.Capability.Disposed);
                Assert.False(provider.Capability.Activated);
            });
        }
        finally
        {
            provider.Continue.TrySetResult();
            if (view != null) WpfTestHost.Invoke(view.Dispose);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Theory]
    [InlineData(".png", typeof(CommonImageOpen))]
    [InlineData(".tiff", typeof(Opentif))]
    public async Task LatestStaticOpenPublishesWhileSupersededCapabilityIsPreparing(string extension, Type openerType)
    {
        string prefix = Path.Combine(Path.GetTempPath(), $"colorvision-superseded-content-{Guid.NewGuid():N}");
        string supersededPath = prefix + "-old" + extension;
        string latestPath = prefix + "-latest" + extension;
        var provider = new DelayedContentProvider(supersededPath);
        ImageView? view = null;
        Task supersededOpen = Task.CompletedTask;
        Task latestOpen = Task.CompletedTask;
        var loadedPaths = new List<string?>();
        try
        {
            IImageOpen opener = WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                WriteEncodedImage(supersededPath);
                WriteEncodedImage(latestPath, landscape: true);
                view = new ImageView { EnableEditorImageServices = false };
                view.RegisterImageContentProvider(provider);
                view.ImageSourceLoaded += (_, _) => loadedPaths.Add(view.ImageContent?.FilePath);
                var selectedOpener = Assert.IsAssignableFrom<IImageOpen>(view.IEditorToolFactory.IImageOpens[extension]);
                Assert.IsType(openerType, selectedOpener);
                view.OpenImage(supersededPath);
                supersededOpen = view.ImageOpenCompletion;
                return selectedOpener;
            });
            await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));

            WpfTestHost.Invoke(() =>
            {
                Assert.Same(opener, view!.EditorContext.IImageOpen);
                view.OpenImage(latestPath);
                latestOpen = view.ImageOpenCompletion;
            });
            await latestOpen.WaitAsync(TimeSpan.FromSeconds(10));
            var (latestSource, latestRevision, pendingRelease) = WpfTestHost.Invoke(() =>
            {
                BitmapSource source = Assert.IsAssignableFrom<BitmapSource>(view!.ViewBitmapSource);
                Assert.Equal((3, 2), (source.PixelWidth, source.PixelHeight));
                Assert.Equal(latestPath, Assert.Single(loadedPaths));
                Assert.Same(opener, view.EditorContext.IImageOpen);
                Assert.Same(provider.ImmediateCapability, view.ImageContentCapability);
                Assert.True(provider.ImmediateCapability.Activated);
                return (source, view.ImageRevision, view.PendingContentRelease);
            });
            Assert.False(pendingRelease.IsCompleted);

            provider.Continue.TrySetResult();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => supersededOpen.WaitAsync(TimeSpan.FromSeconds(10)));
            await pendingRelease.WaitAsync(TimeSpan.FromSeconds(10));
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(latestPath, Assert.Single(loadedPaths));
                Assert.Equal(latestRevision, view!.ImageRevision);
                Assert.Same(latestSource, view.ViewBitmapSource);
                Assert.Same(provider.ImmediateCapability, view.ImageContentCapability);
                Assert.False(provider.ImmediateCapability.Disposed);
                Assert.False(provider.Capability.Activated);
                Assert.True(provider.Capability.Disposed);
            });
        }
        finally
        {
            provider.Continue.TrySetResult();
            try { await Task.WhenAll(supersededOpen, latestOpen).WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (Exception) when (supersededOpen.IsCompleted && latestOpen.IsCompleted) { }
            finally
            {
                if (view != null) WpfTestHost.Invoke(view.Dispose);
                File.Delete(supersededPath);
                File.Delete(latestPath);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingBitmapFileProbeDoesNotBlockDispatcherOrRestoreRetiredState(bool replace)
    {
        const string probeExtension = ".cv-gated-bitmap";
        string prefix = Path.Combine(Path.GetTempPath(), $"colorvision-pending-file-probe-{Guid.NewGuid():N}");
        string pendingPath = prefix + probeExtension;
        string latestPath = prefix + ".png";
        using var opener = new GatedImageOpen(pendingPath);
        ImageView? view = null;
        Task<Task>? start = null;
        Task successor = Task.CompletedTask;
        var loadedPaths = new List<string>();
        TimeSpan testWait = TimeSpan.FromSeconds(10);
        try
        {
            File.WriteAllBytes(pendingPath, [1]);
            var dispatcher = WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                if (replace) WriteEncodedImage(latestPath);
                view = new ImageView { EnableEditorImageServices = false };
                view.IEditorToolFactory.IImageOpens[probeExtension] = opener;
                view.ImageSourceLoaded += (_, _) => loadedPaths.Add(view.Config.FilePath);
                return view.Dispatcher;
            });
            start = dispatcher.InvokeAsync<Task>(() =>
            {
                view!.OpenImage(pendingPath);
                return view.ImageOpenCompletion;
            }).Task;
            Assert.Equal(pendingPath, await opener.Entered.Task.WaitAsync(testWait));
            await dispatcher.InvokeAsync(() =>
            {
                Assert.Equal(pendingPath, view!.Config.FilePath);
                Assert.Null(view.EditorContext.IImageOpen);
                Assert.False(view.Config.Properties.ContainsKey(ImageViewPropertyKeys.FileSize));
                Assert.Empty(loadedPaths);
            }).Task.WaitAsync(testWait);
            Task retired = await start.WaitAsync(testWait);
            Assert.False(retired.IsCompleted);

            WpfTestHost.Invoke(() =>
            {
                if (replace)
                {
                    view!.OpenImage(latestPath);
                    successor = view.ImageOpenCompletion;
                }
                else view!.Clear();
            });
            await successor.WaitAsync(testWait);
            opener.Release();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => retired.WaitAsync(testWait));
            await opener.Exited.Task.WaitAsync(testWait);

            WpfTestHost.Invoke(() =>
            {
                Assert.Empty(opener.OpenedPaths);
                if (replace)
                {
                    Assert.Equal(latestPath, Assert.Single(loadedPaths));
                    Assert.Equal(latestPath, view!.Config.FilePath);
                    Assert.Equal(latestPath, view.ImageContent?.FilePath);
                    Assert.Equal(Path.GetFileName(latestPath), view.Config.GetProperties<string>(ImageViewPropertyKeys.FileName));
                    Assert.IsType<CommonImageOpen>(view.EditorContext.IImageOpen);
                    Assert.Same(successor, view.ImageOpenCompletion);
                }
                else
                {
                    Assert.Empty(loadedPaths);
                    Assert.Equal(string.Empty, view!.Config.FilePath);
                    Assert.Null(view.ViewBitmapSource);
                    Assert.Null(view.EditorContext.IImageOpen);
                    Assert.False(view.Config.Properties.ContainsKey(ImageViewPropertyKeys.FileSize));
                }
            });
        }
        finally
        {
            opener.Release();
            Task drain = Task.WhenAll(start?.Unwrap() ?? Task.CompletedTask, successor);
            try
            {
                try { await drain.WaitAsync(testWait); }
                catch (Exception) when (drain.IsCompleted) { }
                if (opener.Entered.Task.IsCompleted) await opener.Exited.Task.WaitAsync(testWait);
            }
            finally
            {
                if (view != null) WpfTestHost.Invoke(view.Dispose);
                File.Delete(pendingPath);
                File.Delete(latestPath);
            }
        }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("clear")]
    [InlineData("replacement")]
    public async Task SameOpenerProbeRetiresOrTransfersRetainedPixelsExactlyOnce(string outcome)
    {
        const string extension = ".cv-retained-pixels";
        string prefix = Path.Combine(Path.GetTempPath(), $"colorvision-retained-probe-{Guid.NewGuid():N}");
        string initialPath = prefix + "-initial" + extension;
        string pendingPath = prefix + "-pending" + extension;
        string latestPath = prefix + "-latest" + extension;
        using var opener = new GatedImageOpen(pendingPath, missing: outcome == "missing");
        ImageView? view = null;
        Task initial = Task.CompletedTask;
        Task<Task>? start = null;
        Task successor = Task.CompletedTask;
        TimeSpan testWait = TimeSpan.FromSeconds(10);
        try
        {
            var dispatcher = WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                view = new ImageView { EnableEditorImageServices = false };
                view.IEditorToolFactory.IImageOpens[extension] = opener;
                view.OpenImage(initialPath);
                initial = view.ImageOpenCompletion;
                return view.Dispatcher;
            });
            await initial.WaitAsync(testWait);
            var (originalPixels, originalSource) = WpfTestHost.Invoke(() =>
                (Assert.IsType<byte[]>(opener.RetainedPixels), Assert.IsAssignableFrom<BitmapSource>(view!.ViewBitmapSource)));

            start = dispatcher.InvokeAsync<Task>(() =>
            {
                view!.OpenImage(pendingPath);
                return view.ImageOpenCompletion;
            }).Task;
            Assert.Equal(pendingPath, await opener.Entered.Task.WaitAsync(testWait));
            await dispatcher.InvokeAsync(() =>
            {
                Assert.Equal(pendingPath, view!.Config.FilePath);
                Assert.Null(view.EditorContext.IImageOpen);
                Assert.Same(originalSource, view.ViewBitmapSource);
                Assert.Same(originalPixels, opener.RetainedPixels);
                Assert.True(Assert.Single(opener.ReleaseRequests));
                Assert.Equal(initialPath, Assert.Single(opener.OpenedPaths));
            }).Task.WaitAsync(testWait);
            Task pending = await start.WaitAsync(testWait);
            Assert.False(pending.IsCompleted);

            if (outcome == "clear") WpfTestHost.Invoke(() => view!.Clear());
            if (outcome == "replacement")
            {
                successor = WpfTestHost.Invoke(() =>
                {
                    view!.OpenImage(latestPath);
                    return view.ImageOpenCompletion;
                });
                await successor.WaitAsync(testWait);
            }

            opener.Release();
            if (outcome == "missing")
            {
                FileNotFoundException error = await Assert.ThrowsAsync<FileNotFoundException>(() => pending.WaitAsync(testWait));
                Assert.Equal(pendingPath, error.FileName);
            }
            else await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(testWait));
            await opener.Exited.Task.WaitAsync(testWait);
            await WpfTestHost.Invoke(() => view!.PendingContentRelease).WaitAsync(testWait);

            WpfTestHost.Invoke(() =>
            {
                if (outcome == "replacement")
                {
                    Assert.Equal(new[] { initialPath, latestPath }, opener.OpenedPaths);
                    Assert.True(Assert.Single(opener.ReleaseRequests));
                    Assert.Same(originalPixels, opener.RetainedPixels);
                    Assert.Same(opener, view!.EditorContext.IImageOpen);
                    Assert.Equal(latestPath, view.Config.FilePath);
                    Assert.Same(successor, view.ImageOpenCompletion);
                    Assert.NotSame(originalSource, view.ViewBitmapSource);
                }
                else
                {
                    Assert.Equal(initialPath, Assert.Single(opener.OpenedPaths));
                    Assert.Equal(new[] { true, false }, opener.ReleaseRequests);
                    Assert.Null(opener.RetainedPixels);
                    Assert.Null(view!.EditorContext.IImageOpen);
                    Assert.Equal(outcome == "missing" ? pendingPath : string.Empty, view.Config.FilePath);
                    Assert.Same(outcome == "missing" ? originalSource : null, view.ViewBitmapSource);
                }
            });
        }
        finally
        {
            opener.Release();
            Task drain = Task.WhenAll(initial, start?.Unwrap() ?? Task.CompletedTask, successor);
            try
            {
                try { await drain.WaitAsync(testWait); }
                catch (Exception) when (drain.IsCompleted) { }
                if (opener.Entered.Task.IsCompleted) await opener.Exited.Task.WaitAsync(testWait);
            }
            finally { if (view != null) WpfTestHost.Invoke(view.Dispose); }
        }
    }

    [Theory]
    [InlineData(".png")]
    [InlineData(".tiff")]
    public async Task AwaitableStaticOpenerRejectsInvalidPaths(string extension)
    {
        ImageView? view = null;
        IAsyncImageOpen? opener = null;
        try
        {
            WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                view = new ImageView { EnableEditorImageServices = false };
                opener = extension == ".png" ? new CommonImageOpen(view.EditorContext) : new Opentif(view.EditorContext);
            });
            string missing = Path.Combine(Path.GetTempPath(), $"colorvision-missing-{Guid.NewGuid():N}{extension}");
            await Assert.ThrowsAsync<FileNotFoundException>(() => WpfTestHost.Invoke(() => opener!.OpenImageAsync(view!.EditorContext, missing)));
            await Assert.ThrowsAnyAsync<ArgumentException>(() => WpfTestHost.Invoke(() => opener!.OpenImageAsync(view!.EditorContext, null)));
            await Assert.ThrowsAnyAsync<ArgumentException>(() => WpfTestHost.Invoke(() => opener!.OpenImageAsync(view!.EditorContext, string.Empty)));
        }
        finally { if (view != null) WpfTestHost.Invoke(view.Dispose); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourcePublicationReentryCannotRestoreAnObsoleteDecodedContent(bool replaceWithSameSource)
    {
        const string path = "source-publication-fixture.png";
        ImageView? view = null;
        AttachmentCapability incoming = new(), replacement = new();
        ColorVisionTiffParameters incomingParameters = new() { InputFileName = "incoming.png" };
        ColorVisionTiffParameters replacementParameters = new() { InputFileName = "replacement.png" };
        WriteableBitmap? pixels = null;
        Task publication = Task.CompletedTask;
        Exception? error = null;
        EventHandler? reenter = null;
        int callbackCalls = 0, loadedCount = 0;
        long successorContentVersion = -1, successorRevision = -1;
        bool disposed = false;
        try
        {
            WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                view = new ImageView { EnableEditorImageServices = false };
                view.IEditorToolFactory.IEditorTools.Clear();
                view.EditorContext.ProcessingContext.DisplayEffects.PseudoColor.IsEnabled = false;
                view.SetImageSource(new WriteableBitmap(BitmapSource.Create(1, 1, 96, 96, PixelFormats.Gray8, null, new byte[] { 10 }, 1)));
                view.Config.SetImageMetadata(ImageViewPropertyKeys.FilePath, path, nameof(ImageOpenCompletionContractTests));
                view.RegisterImageContentProvider(new AttachmentContentProvider(incoming));
                view.ImageSourceLoaded += (_, _) => loadedCount++;
                pixels = new WriteableBitmap(BitmapSource.Create(1, 1, 96, 96, PixelFormats.Gray8, null, new byte[] { 200 }, 1));
                reenter = (_, _) =>
                {
                    view.StatusBarItemsChanged -= reenter;
                    callbackCalls++;
                    if (replaceWithSameSource)
                    {
                        // Source identity and file path deliberately stay unchanged. This is a
                        // newer content transaction, so the obsolete open must also check its version.
                        view.SetImageSource(pixels);
                        view.SetImageParameterRecord(replacementParameters);
                        view.AttachImageContentCapability(replacement);
                    }
                    else view.Clear();
                    successorContentVersion = view.ContentVersion;
                    successorRevision = view.ImageRevision;
                };
                view.StatusBarItemsChanged += reenter;
                publication = view.PublishDecodedImageContentAsync(new(pixels, incomingParameters, path), pixels);
            });
            try { await publication.WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (Exception failure) { error = failure; }
            var settled = WpfTestHost.Invoke(() => new
            {
                SourceIsIncoming = ReferenceEquals(view!.ViewBitmapSource, pixels),
                SourceIsNull = view.ViewBitmapSource == null,
                ContentIsNull = view.ImageContent == null,
                ParameterRecordIsReplacement = ReferenceEquals(view.Document.ParameterRecord, replacementParameters),
                ParameterRecordIsNull = view.Document.ParameterRecord == null,
                HasCapability = view.ImageContentCapability != null,
                CapabilityIsReplacement = ReferenceEquals(view.ImageContentCapability, replacement),
                FilePath = view.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath),
                InputFileName = view.Config.GetProperties<string>(ImageViewPropertyKeys.ColorVisionInputFileName),
                view.ContentVersion,
                view.ImageRevision,
                IncomingActivateCalls = incoming.ActivateCalls,
                IncomingDisposeCalls = incoming.DisposeCalls,
                IncomingToolsRequested = incoming.ToolsRequested,
                ReplacementActivateCalls = replacement.ActivateCalls,
                ReplacementDisposeCalls = replacement.DisposeCalls,
                ReplacementToolsActive = replacement.ToolsActive,
                callbackCalls,
                loadedCount,
            });
            WpfTestHost.Invoke(view!.Dispose);
            disposed = true;
            output.WriteLine(JsonSerializer.Serialize(new
            {
                replaceWithSameSource, error = error?.GetType().FullName, publication.IsCanceled,
                successorContentVersion, successorRevision, settled,
                incomingDisposeAfterClose = incoming.DisposeCalls,
                replacementDisposeAfterClose = replacement.DisposeCalls,
            }));
            Assert.IsAssignableFrom<OperationCanceledException>(error);
            Assert.True(publication.IsCanceled);
            Assert.Equal(1, settled.callbackCalls);
            Assert.Equal(0, settled.loadedCount);
            Assert.True(settled.ContentIsNull);
            Assert.Equal(successorContentVersion, settled.ContentVersion);
            Assert.Equal(successorRevision, settled.ImageRevision);
            Assert.Equal(replaceWithSameSource, settled.SourceIsIncoming);
            Assert.Equal(!replaceWithSameSource, settled.SourceIsNull);
            Assert.Equal(replaceWithSameSource, settled.ParameterRecordIsReplacement);
            Assert.Equal(!replaceWithSameSource, settled.ParameterRecordIsNull);
            Assert.Equal(replaceWithSameSource, settled.HasCapability);
            Assert.Equal(replaceWithSameSource, settled.CapabilityIsReplacement);
            Assert.Equal(replaceWithSameSource ? path : null, settled.FilePath);
            Assert.Equal(replaceWithSameSource ? "replacement.png" : null, settled.InputFileName);
            Assert.Equal(0, settled.IncomingActivateCalls);
            Assert.Equal(0, settled.IncomingToolsRequested);
            Assert.Equal(1, settled.IncomingDisposeCalls);
            Assert.Equal(1, incoming.DisposeCalls);
            Assert.Equal(replaceWithSameSource ? 1 : 0, settled.ReplacementActivateCalls);
            Assert.Equal(0, settled.ReplacementDisposeCalls);
            Assert.Equal(replaceWithSameSource, settled.ReplacementToolsActive);
            Assert.Equal(replaceWithSameSource ? 1 : 0, replacement.DisposeCalls);
        }
        finally
        {
            if (view != null && !disposed) WpfTestHost.Invoke(() =>
            {
                view.StatusBarItemsChanged -= reenter;
                view.Dispose();
            });
        }
    }

    [Theory]
    [InlineData(false, "activation-throws")]
    [InlineData(true, "activation-throws")]
    [InlineData(true, "dispose-installs-replacement")]
    [InlineData(true, "activation-installs-replacement")]
    [InlineData(false, "old-retirement-throws")]
    public async Task FailedCapabilityAttachmentReleasesOnlyItsOwnedActivation(bool publish, string scenario)
    {
        const string failureMessage = "Deliberate capability lifecycle failure.";
        ImageView? view = null;
        AttachmentCapability incoming = new(), replacement = new(), old = new();
        Task? publication = null;
        Exception? error = null;
        bool disposed = false;
        int loadedCount = 0;
        string assemblyPath = typeof(CommonImageOpen).Assembly.Location;
        output.WriteLine($"ImageEditor: {assemblyPath}; SHA256={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assemblyPath)))}");
        try
        {
            WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                view = new ImageView { EnableEditorImageServices = false };
                view.IEditorToolFactory.IEditorTools.Clear();
                view.EditorContext.ProcessingContext.DisplayEffects.PseudoColor.IsEnabled = false;
                view.SetImageSource(new WriteableBitmap(BitmapSource.Create(1, 1, 96, 96, PixelFormats.Gray8, null, new byte[] { 10 }, 1)));
                if (scenario == "old-retirement-throws")
                {
                    view.AttachImageContentCapability(old);
                    old.OnDispose = () => throw new InvalidOperationException(failureMessage);
                }
                incoming.OnActivate = () =>
                {
                    if (scenario == "old-retirement-throws") return;
                    if (scenario == "activation-installs-replacement")
                    {
                        view.SetImageSource(new WriteableBitmap(BitmapSource.Create(1, 1, 96, 96, PixelFormats.Gray8, null, new byte[] { 80 }, 1)));
                        view.AttachImageContentCapability(replacement);
                        return;
                    }
                    throw new InvalidOperationException(failureMessage);
                };
                if (scenario == "dispose-installs-replacement")
                    incoming.OnDispose = () => view.AttachImageContentCapability(replacement);
                view.ImageSourceLoaded += (_, _) => loadedCount++;
                view.RegisterImageContentProvider(new AttachmentContentProvider(incoming));
            });
            try
            {
                publication = WpfTestHost.Invoke(() =>
                {
                    if (!publish)
                    {
                        view!.AttachImageContentCapability(incoming);
                        return Task.CompletedTask;
                    }
                    const string path = "attachment-fixture.png";
                    view!.Config.SetImageMetadata(ImageViewPropertyKeys.FilePath, path, nameof(ImageOpenCompletionContractTests));
                    BitmapSource pixels = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Gray8, null, new byte[] { 200 }, 1);
                    return view.PublishDecodedImageContentAsync(new(pixels, null, path), pixels);
                });
                await publication.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (Exception failure) { error = failure; }
            var settled = WpfTestHost.Invoke(() =>
            {
                byte[] pixels = new byte[1];
                ((BitmapSource)view!.ViewBitmapSource).CopyPixels(pixels, 1, 0);
                byte[]? contentPixels = view.ImageContent == null ? null : new byte[1];
                if (contentPixels != null) view.ImageContent!.Pixels.CopyPixels(contentPixels, 1, 0);
                return new
                {
                    DocumentHasIncoming = ReferenceEquals(view.ImageContentCapability, incoming),
                    DocumentHasReplacement = ReferenceEquals(view.ImageContentCapability, replacement),
                    HasCapability = view.ImageContentCapability != null, SourcePixels = pixels, ContentPixels = contentPixels,
                    IncomingDisposeCalls = incoming.DisposeCalls, incoming.ActivateCalls, incoming.ToolsRequested, incoming.ToolsActive,
                    ReplacementDisposeCalls = replacement.DisposeCalls, ReplacementToolsActive = replacement.ToolsActive, loadedCount
                };
            });
            WpfTestHost.Invoke(view!.Dispose);
            disposed = true;
            output.WriteLine(JsonSerializer.Serialize(new
            {
                publish, scenario, error = error?.GetType().FullName, message = error?.Message,
                publicationFaulted = publication?.IsFaulted, publicationCancelled = publication?.IsCanceled,
                settled, incomingDisposeAfterClose = incoming.DisposeCalls, replacementDisposeAfterClose = replacement.DisposeCalls,
                oldDisposeAfterClose = old.DisposeCalls, replacement.ToolsActive
            }));
            if (scenario == "activation-installs-replacement")
            {
                Assert.IsAssignableFrom<OperationCanceledException>(error);
                Assert.True(publication!.IsCanceled);
            }
            else
            {
                Assert.IsType<InvalidOperationException>(error);
                Assert.Equal(failureMessage, error.Message);
                if (publish) Assert.True(publication!.IsFaulted);
            }
            bool replacementInstalled = scenario is "dispose-installs-replacement" or "activation-installs-replacement";
            Assert.False(settled.DocumentHasIncoming);
            Assert.Equal(replacementInstalled, settled.DocumentHasReplacement);
            Assert.Equal(replacementInstalled, settled.HasCapability);
            Assert.Equal(1, settled.IncomingDisposeCalls);
            Assert.Equal(1, incoming.DisposeCalls);
            Assert.Equal(0, settled.ToolsRequested);
            Assert.False(settled.ToolsActive);
            Assert.Equal(replacementInstalled, settled.ReplacementToolsActive);
            Assert.Equal(0, settled.ReplacementDisposeCalls);
            Assert.Equal(replacementInstalled ? 1 : 0, replacement.DisposeCalls);
            Assert.False(replacement.ToolsActive);
            Assert.Equal(0, settled.loadedCount);
            if (publish && scenario != "activation-installs-replacement")
            {
                Assert.Equal(new byte[] { 200 }, settled.SourcePixels);
                Assert.Equal(new byte[] { 200 }, settled.ContentPixels);
            }
            if (scenario == "activation-installs-replacement") Assert.Equal(new byte[] { 80 }, settled.SourcePixels);
            if (scenario == "old-retirement-throws")
            {
                Assert.Equal(0, settled.ActivateCalls);
                Assert.Equal(1, old.DisposeCalls);
            }
        }
        finally { if (view != null && !disposed) WpfTestHost.Invoke(view.Dispose); }
    }

    [Theory]
    [InlineData("provider")]
    [InlineData("iterator")]
    [InlineData("previous-deactivation")]
    [InlineData("activation")]
    [InlineData("dispose")]
    public async Task ReentrantToolPublicationCannotInstallToolsFromAnObsoleteCapability(string boundary)
    {
        ImageView? view = null;
        AttachmentCapability incoming = new(), replacement = new(), previous = new();
        AttachmentTool incomingTool = new("incoming"), replacementTool = new("replacement"), previousTool = new("previous");
        Task publication = Task.CompletedTask;
        Exception? error = null;
        int callbackCalls = 0, loadedCount = 0;
        string assemblyPath = typeof(CommonImageOpen).Assembly.Location;
        output.WriteLine($"ImageEditor: {assemblyPath}; SHA256={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assemblyPath)))}");
        try
        {
            WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                view = new ImageView { EnableEditorImageServices = false };
                view.IEditorToolFactory.IEditorTools.Clear();
                view.EditorContext.ProcessingContext.DisplayEffects.PseudoColor.IsEnabled = false;
                replacement.OnGetTools = () => [replacementTool];
                void Replace()
                {
                    // A one-shot callback also lets the RED collect repeated old deactivation safely.
                    if (callbackCalls++ != 0) return;
                    view.SetImageSource(new WriteableBitmap(BitmapSource.Create(1, 1, 96, 96, PixelFormats.Gray8, null, new byte[] { 80 }, 1)));
                    view.AttachImageContentCapability(replacement);
                }
                IEnumerable<IEditorTool> DeferredTools()
                {
                    Replace();
                    yield return incomingTool;
                }
                incoming.OnGetTools = () =>
                {
                    if (boundary == "provider") Replace();
                    if (boundary == "dispose") view.IEditorToolFactory.Dispose();
                    return boundary == "iterator" ? DeferredTools() : [incomingTool];
                };
                if (boundary == "activation") incoming.OnToolsActivated = Replace;
                if (boundary == "previous-deactivation")
                {
                    previous.OnGetTools = () => [previousTool];
                    view.IEditorToolFactory.ApplyImageContentTools(previous);
                    previousTool.IsChecked = true;
                    previous.OnToolsDeactivated = Replace;
                }
                view.RegisterImageContentProvider(new AttachmentContentProvider(incoming));
                view.ImageSourceLoaded += (_, _) => loadedCount++;
                const string path = "tool-publication-fixture.png";
                view.Config.SetImageMetadata(ImageViewPropertyKeys.FilePath, path, nameof(ImageOpenCompletionContractTests));
                BitmapSource pixels = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Gray8, null, new byte[] { 200 }, 1);
                publication = view.PublishDecodedImageContentAsync(new(pixels, null, path), pixels);
            });
            try { await publication.WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (Exception failure) { error = failure; }
            var settled = WpfTestHost.Invoke(() => new
            {
                HasCapability = view!.ImageContentCapability != null,
                DocumentHasReplacement = ReferenceEquals(view.ImageContentCapability, replacement),
                Tools = view.IEditorToolFactory.GetEffectiveEditorTools().Select(tool => tool.GuidId).ToArray(),
                IncomingDisposeCalls = incoming.DisposeCalls, incoming.ToolsActivatedCalls, incoming.ToolsActive,
                ReplacementDisposeCalls = replacement.DisposeCalls, ReplacementToolsActive = replacement.ToolsActive,
                PreviousDeactivatedCalls = previous.ToolsDeactivatedCalls, PreviousChecked = previousTool.IsChecked,
                callbackCalls, loadedCount
            });
            // Retirement callbacks may arrive after factory disposal; its compatibility entry must stay quiet.
            WpfTestHost.Invoke(() => view!.IEditorToolFactory.ApplyImageOpenTools(null));
            WpfTestHost.Invoke(view!.Dispose);
            output.WriteLine(JsonSerializer.Serialize(new
            {
                boundary, error = error?.GetType().FullName, publication.IsCanceled, settled,
                incomingDisposeAfterClose = incoming.DisposeCalls, replacementDisposeAfterClose = replacement.DisposeCalls
            }));
            Assert.IsAssignableFrom<OperationCanceledException>(error);
            Assert.True(publication.IsCanceled);
            Assert.Equal(0, settled.loadedCount);
            Assert.Equal(1, settled.IncomingDisposeCalls);
            Assert.Equal(1, incoming.DisposeCalls);
            Assert.False(settled.ToolsActive);
            Assert.Equal(boundary == "activation" ? 1 : 0, settled.ToolsActivatedCalls);
            bool replaced = boundary != "dispose";
            Assert.Equal(replaced, settled.HasCapability);
            Assert.Equal(replaced, settled.DocumentHasReplacement);
            Assert.Equal(replaced ? new[] { "replacement" } : [], settled.Tools);
            Assert.Equal(replaced, settled.ReplacementToolsActive);
            Assert.Equal(0, settled.ReplacementDisposeCalls);
            Assert.Equal(replaced ? 1 : 0, replacement.DisposeCalls);
            if (boundary == "previous-deactivation")
            {
                Assert.Equal(1, settled.PreviousDeactivatedCalls);
                Assert.False(settled.PreviousChecked);
            }
        }
        finally { if (view != null) WpfTestHost.Invoke(view.Dispose); }
    }

    private sealed class AttachmentTool(string id) : IEditorToggleTool
    {
        public ToolBarLocal ToolBarLocal => ToolBarLocal.Top;
        public string GuidId => id;
        public int Order => 0;
        public object? Icon => null;
        public System.Windows.Input.ICommand? Command => null;
        public bool IsChecked { get; set; }
    }

    private sealed class AttachmentContentProvider(IImageContentCapability capability) : IImageContentProvider
    {
        public Task<IImageContentCapability?> PrepareAsync(EditorContext context, DecodedImageContent content, CancellationToken cancellationToken)
            => Task.FromResult<IImageContentCapability?>(capability);
    }

    private sealed class AttachmentCapability : IImageContentCapability
    {
        public Action? OnActivate { get; set; }
        public Action? OnDispose { get; set; }
        public Func<IEnumerable<IEditorTool>>? OnGetTools { get; set; }
        public Action? OnToolsActivated { get; set; }
        public Action? OnToolsDeactivated { get; set; }
        public int ActivateCalls { get; private set; }
        public int DisposeCalls { get; private set; }
        public int ToolsRequested { get; private set; }
        public int ToolsActivatedCalls { get; private set; }
        public int ToolsDeactivatedCalls { get; private set; }
        public bool ToolsActive { get; private set; }
        public void Activate() { ActivateCalls++; OnActivate?.Invoke(); }
        public void Dispose() { DisposeCalls++; OnDispose?.Invoke(); }
        public IEnumerable<IEditorTool> GetEditorTools() { ToolsRequested++; return OnGetTools?.Invoke() ?? []; }
        public void OnEditorToolsActivated(EditorContext context) { ToolsActivatedCalls++; ToolsActive = true; OnToolsActivated?.Invoke(); }
        public void OnEditorToolsDeactivated(EditorContext context) { ToolsDeactivatedCalls++; ToolsActive = false; OnToolsDeactivated?.Invoke(); }
    }

    private sealed class DelayedContentProvider(string? delayedPath = null) : IImageContentProvider
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public DelayedCapability Capability { get; } = new();
        public DelayedCapability ImmediateCapability { get; } = new();
        public async Task<IImageContentCapability?> PrepareAsync(EditorContext context, DecodedImageContent content, CancellationToken cancellationToken)
        {
            if (delayedPath != null && !string.Equals(delayedPath, content.FilePath, StringComparison.Ordinal)) return ImmediateCapability;
            Started.TrySetResult();
            // Deliberately return despite cancellation, as a third-party provider might.
            await Continue.Task;
            return Capability;
        }
    }

    private sealed class DelayedCapability : IImageContentCapability
    {
        public bool Activated { get; private set; }
        public bool Disposed { get; private set; }
        public void Activate() => Activated = true;
        public void Dispose() => Disposed = true;
        public IEnumerable<IEditorTool> GetEditorTools() => [];
        public void OnEditorToolsActivated(EditorContext context) { }
        public void OnEditorToolsDeactivated(EditorContext context) { }
    }

    private sealed class GatedImageOpen(string pendingPath, bool missing = false) : IPreparedImageOpen, IImageOpenContentLifetime, IDisposable
    {
        private readonly ManualResetEventSlim _release = new();
        public TaskCompletionSource<string> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Exited { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public byte[]? RetainedPixels { get; private set; }
        public List<string> OpenedPaths { get; } = new();
        public List<bool> ReleaseRequests { get; } = new();

        public ImageFileMetadata ReadFileMetadata(string filePath)
        {
            if (filePath == pendingPath)
            {
                Entered.TrySetResult(filePath);
                try
                {
                    if (!_release.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("Image file probe gate was not released.");
                    if (missing) throw new FileNotFoundException("Test image file was not found.", filePath);
                }
                finally { Exited.TrySetResult(); }
            }
            return new ImageFileMetadata(Path.GetFileName(filePath), 1, null, null);
        }

        public Task OpenPreparedImageAsync(EditorContext context, string filePath, ImageFileMetadata file, CancellationToken cancellationToken)
        {
            OpenedPaths.Add(filePath);
            if (filePath == pendingPath) throw new InvalidOperationException("A retired file probe cannot start the opener.");
            RetainedPixels ??= [37];
            BitmapSource source = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Gray8, null, RetainedPixels, 1);
            context.ImageView.SetImageSource(new WriteableBitmap(source));
            return Task.CompletedTask;
        }

        public Task ReleaseContentAsync(bool reuseBuffers)
        {
            ReleaseRequests.Add(reuseBuffers);
            if (!reuseBuffers) RetainedPixels = null;
            return Task.CompletedTask;
        }

        public void OpenImage(EditorContext context, string? filePath) => throw new NotSupportedException();
        public Task OpenImageAsync(EditorContext context, string? filePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Release() => _release.Set();
        public void Dispose() => _release.Dispose();
    }

    private static OpenedImageState CaptureState(ImageView imageView, int completionCount)
    {
        BitmapSource source = Assert.IsAssignableFrom<BitmapSource>(imageView.ViewBitmapSource);
        return new OpenedImageState(
            completionCount,
            source.PixelWidth,
            source.PixelHeight,
            imageView.Config.GetProperties<string>(ImageViewPropertyKeys.FileName),
            imageView.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath),
            imageView.Config.GetProperties<string>(ImageViewPropertyKeys.FileSource),
            imageView.Config.GetProperties<int>(ImageViewPropertyKeys.ImageWidth),
            imageView.Config.GetProperties<int>(ImageViewPropertyKeys.ImageHeight),
            imageView.EditorContext.IImageOpen?.GetType());
    }

    private static void WriteEncodedImage(string filePath, bool landscape = false)
    {
        byte[] pixels =
        [
            0x00, 0x00, 0xFF, 0xFF,
            0x00, 0xFF, 0x00, 0xFF,
            0xFF, 0x00, 0x00, 0xFF,
            0xFF, 0xFF, 0xFF, 0xFF,
            0x00, 0xFF, 0xFF, 0xFF,
            0xFF, 0x00, 0xFF, 0xFF,
        ];
        int width = landscape ? 3 : 2;
        int height = landscape ? 2 : 3;
        BitmapSource source = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            width * 4);
        BitmapEncoder encoder = Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".png" => new PngBitmapEncoder(),
            ".tiff" => new TiffBitmapEncoder(),
            _ => throw new InvalidOperationException($"Unsupported test image extension: {filePath}"),
        };
        encoder.Frames.Add(BitmapFrame.Create(source));

        using FileStream stream = new(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        encoder.Save(stream);
    }

    private static void WriteCvRaw(string filePath, byte value)
    {
        using CVCIEFile file = new()
        {
            Version = 1,
            FileExtType = CVType.Raw,
            Rows = 3,
            Cols = 2,
            Bpp = 8,
            Channels = 1,
            Gain = 1,
            Exp = [1f],
            Data = Enumerable.Repeat(value, 6).ToArray(),
        };
        Assert.True(CVFileUtil.WriteCIEFile(filePath, file));
    }

    private static void EnsureImageViewTestResources()
    {
        Application application = Application.Current ?? new Application();
        application.Resources["TextBox.Small"] = new Style(typeof(TextBox));
        application.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
        application.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
        application.Resources["ToolBarImage"] = new Style(typeof(Image));
        application.Resources["BaseStyle"] = new Style(typeof(Control));
        application.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
        application.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
    }

    private sealed record OpenedImageState(
        int CompletionCount,
        int PixelWidth,
        int PixelHeight,
        string? FileName,
        string? FilePath,
        string? FileSource,
        int MetadataWidth,
        int MetadataHeight,
        Type? OpenerType);
}
