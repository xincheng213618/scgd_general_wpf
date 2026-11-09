using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Tif;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.Algorithms;
using ColorVision.Core;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.Engine.Media;
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

    [Fact]
    public async Task CvcieProbeRendersTheLatestSampleAfterPreparation()
    {
        var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var rendered = new TaskCompletionSource<ImagePixelSample>(TaskCreationOptions.RunContinuationsAsynchronously);
        var latest = new ImagePixelSample { PixelX = 8, PixelY = 6 };
        ImageView? view = null;
        try
        {
            WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                view = new ImageView();
                int preparations = 0;
                var updates = new CvcieProbeUpdates(view, () =>
                {
                    preparations++;
                    return new ValueTask<bool>(ready.Task);
                }, sample => rendered.TrySetResult(sample));
                updates.Update(new ImagePixelSample { PixelX = 2, PixelY = 3 });
                updates.Update(latest);
                Assert.Equal(1, preparations);
                Assert.False(rendered.Task.IsCompleted);
            });
            ready.SetResult(true);
            Assert.Same(latest, await rendered.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        }
        finally { if (view != null) WpfTestHost.Invoke(view.Dispose); }
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

            OpenedImageState state = WpfTestHost.Invoke(() => CaptureState(imageView!, completionCount));

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

    private sealed class DelayedContentProvider : IImageContentProvider
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public DelayedCapability Capability { get; } = new();
        public async Task<IImageContentCapability?> PrepareAsync(EditorContext context, DecodedImageContent content, CancellationToken cancellationToken)
        {
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

    private static void WriteEncodedImage(string filePath)
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
        BitmapSource source = BitmapSource.Create(
            2,
            3,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            8);
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
