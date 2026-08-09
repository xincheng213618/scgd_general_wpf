using ColorVision.ImageEditor;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class ImageViewSnapshotSaveTests
{
    [Fact]
    public async Task SaveSnapshotAsync_ComposesFrozenSceneOnBackgroundStaThread()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "ColorVision.Tests",
            Guid.NewGuid().ToString("N"));
        string fileName = Path.Combine(directory, "background-scene.png");
        try
        {
            DrawingGroup scene = new();
            scene.Children.Add(new GeometryDrawing(
                Brushes.Blue,
                null,
                new RectangleGeometry(new Rect(0, 0, 4, 4))));
            scene.Children.Add(new GeometryDrawing(
                Brushes.Red,
                null,
                new RectangleGeometry(new Rect(1, 1, 2, 2))));
            ImageViewSnapshot snapshot = ImageViewSnapshot.Create(scene, 4, 4);

            await ImageView.SaveSnapshotAsync(snapshot, fileName);

            BitmapFrame frame;
            using (FileStream stream = File.OpenRead(fileName))
            {
                PngBitmapDecoder decoder = new(
                    stream,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);
                frame = decoder.Frames[0];
                frame.Freeze();
            }

            Assert.Equal(4, frame.PixelWidth);
            Assert.Equal(4, frame.PixelHeight);
            AssertPixel(frame, 0, 0, blue: 255, green: 0, red: 0, alpha: 255);
            AssertPixel(frame, 2, 2, blue: 0, green: 0, red: 255, alpha: 255);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveSnapshotAsync_WritesFrozenBitmapAsPng()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "ColorVision.Tests",
            Guid.NewGuid().ToString("N"));
        string fileName = Path.Combine(directory, "snapshot.png");
        try
        {
            byte[] pixels =
            [
                0x00, 0x00, 0xFF, 0xFF,
                0x00, 0xFF, 0x00, 0xFF,
                0xFF, 0x00, 0x00, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF,
            ];
            BitmapSource snapshot = BitmapSource.Create(
                2,
                2,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                pixels,
                8);
            snapshot.Freeze();

            await ImageView.SaveSnapshotAsync(snapshot, fileName);

            byte[] signature = new byte[8];
            using FileStream stream = File.OpenRead(fileName);
            int bytesRead = await stream.ReadAsync(signature);
            Assert.Equal(signature.Length, bytesRead);
            Assert.Equal(
                new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 },
                signature);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(2, 4, 2)]
    [InlineData(4, 2, 1)]
    public async Task SaveSnapshotAsync_ScalesSceneByConfiguredDivisor(
        int scaleDivisor,
        int expectedWidth,
        int expectedHeight)
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "ColorVision.Tests",
            Guid.NewGuid().ToString("N"));
        string fileName = Path.Combine(directory, "scaled-scene.png");
        try
        {
            DrawingGroup scene = new();
            scene.Children.Add(new GeometryDrawing(
                Brushes.Blue,
                null,
                new RectangleGeometry(new Rect(0, 0, 8, 4))));
            scene.Children.Add(new GeometryDrawing(
                Brushes.Red,
                null,
                new RectangleGeometry(new Rect(4, 0, 4, 4))));
            ImageViewSnapshot snapshot = ImageViewSnapshot.Create(scene, 8, 4);

            await ImageView.SaveSnapshotWithOptionsAsync(
                snapshot,
                fileName,
                new ImageViewSnapshotSaveOptions { ScaleDivisor = scaleDivisor });

            BitmapFrame frame = LoadPng(fileName);
            Assert.Equal(expectedWidth, frame.PixelWidth);
            Assert.Equal(expectedHeight, frame.PixelHeight);
            AssertPixel(frame, 0, 0, blue: 255, green: 0, red: 0, alpha: 255);
            AssertPixel(frame, expectedWidth - 1, 0, blue: 0, green: 0, red: 255, alpha: 255);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveSnapshotAsync_RoundsOddScaledDimensionsAwayFromZero()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "ColorVision.Tests",
            Guid.NewGuid().ToString("N"));
        string fileName = Path.Combine(directory, "odd-scaled-scene.png");
        try
        {
            DrawingGroup scene = new();
            scene.Children.Add(new GeometryDrawing(
                Brushes.Blue,
                null,
                new RectangleGeometry(new Rect(0, 0, 5, 3))));
            ImageViewSnapshot snapshot = ImageViewSnapshot.Create(scene, 5, 3);

            await ImageView.SaveSnapshotWithOptionsAsync(
                snapshot,
                fileName,
                new ImageViewSnapshotSaveOptions { ScaleDivisor = 2 });

            BitmapFrame frame = LoadPng(fileName);
            Assert.Equal(3, frame.PixelWidth);
            Assert.Equal(2, frame.PixelHeight);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CaptureSnapshotForBackgroundSave_CanExcludeOverlays()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "ColorVision.Tests",
            Guid.NewGuid().ToString("N"));
        try
        {
            IReadOnlyList<(bool UseWriteableBitmap, ImageViewSnapshot WithOverlay, ImageViewSnapshot WithoutOverlay)> captures = RunOnSta(() =>
            {
                EnsureImageViewTestResources();

                byte[] pixels = new byte[4 * 4 * 4];
                for (int offset = 0; offset < pixels.Length; offset += 4)
                {
                    pixels[offset] = 255;
                    pixels[offset + 3] = 255;
                }
                BitmapSource frozenSource = BitmapSource.Create(
                    4,
                    4,
                    96,
                    96,
                    PixelFormats.Bgra32,
                    null,
                    pixels,
                    16);
                frozenSource.Freeze();

                List<(bool, ImageViewSnapshot, ImageViewSnapshot)> results = new();
                foreach (bool useWriteableBitmap in new[] { false, true })
                {
                    ImageView imageView = new();
                    try
                    {
                        imageView.ImageShow.Clear();
                        imageView.ImageShow.Source = useWriteableBitmap
                            ? new WriteableBitmap(frozenSource)
                            : frozenSource;
                        DrawingVisual overlay = new();
                        using (DrawingContext context = overlay.RenderOpen())
                            context.DrawRectangle(Brushes.Red, null, new Rect(1, 1, 2, 2));
                        imageView.ImageShow.AddVisual(overlay);

                        results.Add((
                            useWriteableBitmap,
                            imageView.CaptureSnapshotForBackgroundSave()!,
                            imageView.CaptureSnapshotForBackgroundSave(includeOverlays: false)!));
                    }
                    finally
                    {
                        imageView.Dispose();
                    }
                }

                return results;
            });

            foreach ((bool useWriteableBitmap, ImageViewSnapshot withOverlay, ImageViewSnapshot withoutOverlay) in captures)
            {
                string suffix = useWriteableBitmap ? "writeable" : "frozen";
                string withOverlayFile = Path.Combine(directory, $"with-overlay-{suffix}.png");
                string withoutOverlayFile = Path.Combine(directory, $"without-overlay-{suffix}.png");

                await ImageView.SaveSnapshotAsync(withOverlay, withOverlayFile);
                await ImageView.SaveSnapshotAsync(withoutOverlay, withoutOverlayFile);

                BitmapFrame withOverlayFrame = LoadPng(withOverlayFile);
                BitmapFrame withoutOverlayFrame = LoadPng(withoutOverlayFile);
                AssertPixel(withOverlayFrame, 0, 0, blue: 255, green: 0, red: 0, alpha: 255);
                AssertPixel(withOverlayFrame, 2, 2, blue: 0, green: 0, red: 255, alpha: 255);
                AssertPixel(withoutOverlayFrame, 2, 2, blue: 255, green: 0, red: 0, alpha: 255);
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveSnapshotAsync_WritesConfiguredJpegWithoutUpscaling()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "ColorVision.Tests",
            Guid.NewGuid().ToString("N"));
        string fileName = Path.Combine(directory, "snapshot.jpg");
        try
        {
            DrawingGroup scene = new();
            scene.Children.Add(new GeometryDrawing(
                Brushes.White,
                null,
                new RectangleGeometry(new Rect(0, 0, 4, 2))));
            ImageViewSnapshot snapshot = ImageViewSnapshot.Create(scene, 4, 2);

            await ImageView.SaveSnapshotWithOptionsAsync(
                snapshot,
                fileName,
                new ImageViewSnapshotSaveOptions
                {
                    Format = ImageViewSnapshotFormat.Jpeg,
                    JpegQuality = 100,
                });

            byte[] signature = new byte[2];
            using (FileStream stream = File.OpenRead(fileName))
            {
                Assert.Equal(signature.Length, await stream.ReadAsync(signature));
            }
            Assert.Equal(new byte[] { 0xFF, 0xD8 }, signature);

            using FileStream jpegStream = File.OpenRead(fileName);
            JpegBitmapDecoder decoder = new(
                jpegStream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            Assert.Equal(4, decoder.Frames[0].PixelWidth);
            Assert.Equal(2, decoder.Frames[0].PixelHeight);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(false, ImageViewSourceFormat.Png, ImageViewTiffCompression.Lzw, ".png", 0)]
    [InlineData(true, ImageViewSourceFormat.Png, ImageViewTiffCompression.Lzw, ".png", 0)]
    [InlineData(false, ImageViewSourceFormat.Tiff, ImageViewTiffCompression.Lzw, ".tif", 5)]
    [InlineData(true, ImageViewSourceFormat.Tiff, ImageViewTiffCompression.Zip, ".tif", 8)]
    public async Task SaveSnapshotExportsAsync_PreservesRgb48SourcePixels(
        bool useWriteableBitmap,
        ImageViewSourceFormat format,
        ImageViewTiffCompression tiffCompression,
        string extension,
        int expectedTiffCompressionTag)
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "ColorVision.Tests",
            Guid.NewGuid().ToString("N"));
        string fileName = Path.Combine(directory, "source" + extension);
        ushort[] values =
        [
            0, 1, 255,
            256, 32768, ushort.MaxValue,
            1234, 2345, 3456,
            4567, 5678, 6789,
        ];
        byte[] pixels = new byte[values.Length * sizeof(ushort)];
        Buffer.BlockCopy(values, 0, pixels, 0, pixels.Length);
        try
        {
            ImageViewSnapshot snapshot = RunOnSta(() =>
            {
                EnsureImageViewTestResources();
                BitmapSource source = BitmapSource.Create(
                    2,
                    2,
                    96,
                    96,
                    PixelFormats.Rgb48,
                    null,
                    pixels,
                    12);
                source.Freeze();
                ImageView imageView = new();
                try
                {
                    imageView.ImageShow.Source = useWriteableBitmap
                        ? new WriteableBitmap(source)
                        : source;
                    return imageView.CaptureSnapshotForBackgroundSave(includeOverlays: false)!;
                }
                finally
                {
                    imageView.Dispose();
                }
            });

            await ImageView.SaveSnapshotExportsAsync(
                snapshot,
                new ImageViewSnapshotExportOptions
                {
                    SourceFileName = fileName,
                    SourceOptions = new ImageViewSourceSaveOptions
                    {
                        Format = format,
                        TiffCompression = tiffCompression,
                    },
                });

            BitmapFrame frame = LoadBitmap(fileName);
            Assert.Equal(PixelFormats.Rgb48, frame.Format);
            byte[] actual = new byte[pixels.Length];
            frame.CopyPixels(actual, 12, 0);
            Assert.Equal(pixels, actual);
            if (expectedTiffCompressionTag != 0)
            {
                BitmapMetadata metadata = Assert.IsType<BitmapMetadata>(frame.Metadata);
                object? compression = metadata.GetQuery("/ifd/{ushort=259}");
                Assert.Equal(expectedTiffCompressionTag, Convert.ToInt32(compression));
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveSnapshotExportsAsync_SavesRenderedOverlayAndUnmarkedSourceFromOneCapture()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "ColorVision.Tests",
            Guid.NewGuid().ToString("N"));
        string renderedFile = Path.Combine(directory, "rendered.png");
        string sourceFile = Path.Combine(directory, "source.png");
        try
        {
            ImageViewSnapshot snapshot = RunOnSta(() =>
            {
                EnsureImageViewTestResources();
                ushort[] values = new ushort[4 * 4 * 3];
                for (int offset = 0; offset < values.Length; offset += 3)
                    values[offset + 2] = ushort.MaxValue;
                byte[] pixels = new byte[values.Length * sizeof(ushort)];
                Buffer.BlockCopy(values, 0, pixels, 0, pixels.Length);
                BitmapSource source = BitmapSource.Create(
                    4,
                    4,
                    96,
                    96,
                    PixelFormats.Rgb48,
                    null,
                    pixels,
                    24);
                source.Freeze();

                ImageView imageView = new();
                try
                {
                    imageView.ImageShow.Clear();
                    imageView.ImageShow.Source = new WriteableBitmap(source);
                    DrawingVisual overlay = new();
                    using (DrawingContext context = overlay.RenderOpen())
                        context.DrawRectangle(Brushes.Red, null, new Rect(1, 1, 2, 2));
                    imageView.ImageShow.AddVisual(overlay);
                    return imageView.CaptureSnapshotForBackgroundSave(includeOverlays: true)!;
                }
                finally
                {
                    imageView.Dispose();
                }
            });

            await ImageView.SaveSnapshotExportsAsync(
                snapshot,
                new ImageViewSnapshotExportOptions
                {
                    RenderedFileName = renderedFile,
                    SourceFileName = sourceFile,
                });

            BitmapFrame rendered = LoadPng(renderedFile);
            BitmapFrame source = LoadPng(sourceFile);
            Assert.Equal(32, rendered.Format.BitsPerPixel);
            Assert.Equal(PixelFormats.Rgb48, source.Format);
            byte[] sourcePixels = new byte[4 * 4 * 6];
            source.CopyPixels(sourcePixels, 24, 0);
            ushort[] expectedValues = new ushort[4 * 4 * 3];
            for (int offset = 0; offset < expectedValues.Length; offset += 3)
                expectedValues[offset + 2] = ushort.MaxValue;
            byte[] expectedPixels = new byte[expectedValues.Length * sizeof(ushort)];
            Buffer.BlockCopy(expectedValues, 0, expectedPixels, 0, expectedPixels.Length);
            Assert.Equal(expectedPixels, sourcePixels);
            AssertPixel(rendered, 0, 0, blue: 255, green: 0, red: 0, alpha: 255);
            AssertPixel(rendered, 2, 2, blue: 0, green: 0, red: 255, alpha: 255);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveSnapshotExportsAsync_PreservesMutableRgbAlphaAndPaletteSemantics()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "ColorVision.Tests",
            Guid.NewGuid().ToString("N"));
        try
        {
            IReadOnlyList<(string Name, ImageViewSnapshot Snapshot, byte Blue, byte Green, byte Red, byte Alpha)> captures = RunOnSta(() =>
            {
                EnsureImageViewTestResources();
                BitmapSource rgb24 = BitmapSource.Create(
                    1, 1, 96, 96, PixelFormats.Rgb24, null, new byte[] { 255, 0, 0 }, 3);
                BitmapSource bgra32 = BitmapSource.Create(
                    1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 255, 128 }, 4);
                BitmapPalette palette = new([Colors.Black, Colors.Lime]);
                BitmapSource indexed8 = BitmapSource.Create(
                    1, 1, 96, 96, PixelFormats.Indexed8, palette, new byte[] { 1 }, 1);

                List<(string, ImageViewSnapshot, byte, byte, byte, byte)> results = [];
                foreach ((string name, BitmapSource source, byte blue, byte green, byte red, byte alpha) in new[]
                {
                    ("rgb24", rgb24, (byte)0, (byte)0, (byte)255, (byte)255),
                    ("bgra32", bgra32, (byte)0, (byte)0, (byte)255, (byte)128),
                    ("indexed8", indexed8, (byte)0, (byte)255, (byte)0, (byte)255),
                })
                {
                    source.Freeze();
                    ImageView imageView = new();
                    try
                    {
                        imageView.ImageShow.Source = new WriteableBitmap(source);
                        ImageViewSnapshot snapshot = imageView.CaptureSnapshotForBackgroundSave(includeOverlays: false)!;
                        Assert.NotNull(snapshot);
                        results.Add((name, snapshot, blue, green, red, alpha));
                    }
                    finally
                    {
                        imageView.Dispose();
                    }
                }
                return results;
            });

            foreach ((string name, ImageViewSnapshot snapshot, byte blue, byte green, byte red, byte alpha) in captures)
            {
                string fileName = Path.Combine(directory, name + ".png");
                await ImageView.SaveSnapshotExportsAsync(
                    snapshot,
                    new ImageViewSnapshotExportOptions
                    {
                        SourceFileName = fileName,
                    });
                AssertPixel(LoadPng(fileName), 0, 0, blue, green, red, alpha);
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CaptureSnapshotForBackgroundSave_UsesLoadedBaseInsteadOfDisplayedFunctionImage()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "ColorVision.Tests",
            Guid.NewGuid().ToString("N"));
        string fileName = Path.Combine(directory, "source.png");
        try
        {
            ImageViewSnapshot snapshot = RunOnSta(() =>
            {
                EnsureImageViewTestResources();
                BitmapSource loadedBlue = BitmapSource.Create(
                    1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 255, 0, 0, 255 }, 4);
                BitmapSource displayedRed = BitmapSource.Create(
                    1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 255, 255 }, 4);
                loadedBlue.Freeze();
                displayedRed.Freeze();

                ImageView imageView = new();
                try
                {
                    imageView.ViewBitmapSource = loadedBlue;
                    imageView.ImageShow.Source = displayedRed;
                    return imageView.CaptureSnapshotForBackgroundSave(includeOverlays: false)!;
                }
                finally
                {
                    imageView.Dispose();
                }
            });

            await ImageView.SaveSnapshotExportsAsync(
                snapshot,
                new ImageViewSnapshotExportOptions { SourceFileName = fileName });

            AssertPixel(LoadPng(fileName), 0, 0, blue: 255, green: 0, red: 0, alpha: 255);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveSnapshotExportsAsync_RejectsRgb48BmpInsteadOfChangingSourcePixels()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "ColorVision.Tests",
            Guid.NewGuid().ToString("N"));
        string fileName = Path.Combine(directory, "source.bmp");
        string renderedFileName = Path.Combine(directory, "rendered.png");
        try
        {
            ImageViewSnapshot snapshot = RunOnSta(() =>
            {
                EnsureImageViewTestResources();
                byte[] pixels = new byte[2 * 2 * 6];
                BitmapSource source = BitmapSource.Create(
                    2,
                    2,
                    96,
                    96,
                    PixelFormats.Rgb48,
                    null,
                    pixels,
                    12);
                source.Freeze();
                ImageView imageView = new();
                try
                {
                    imageView.ImageShow.Source = source;
                    return imageView.CaptureSnapshotForBackgroundSave(includeOverlays: false)!;
                }
                finally
                {
                    imageView.Dispose();
                }
            });

            NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(() =>
                ImageView.SaveSnapshotExportsAsync(
                    snapshot,
                    new ImageViewSnapshotExportOptions
                    {
                        RenderedFileName = renderedFileName,
                        SourceFileName = fileName,
                        SourceOptions = new ImageViewSourceSaveOptions
                        {
                            Format = ImageViewSourceFormat.Bmp,
                        },
                    }));

            Assert.Contains("PNG or TIFF", exception.Message, StringComparison.Ordinal);
            Assert.True(File.Exists(renderedFileName));
            Assert.False(File.Exists(fileName));
            Assert.False(ImageView.CanBmpPreserveSourceBitDepth(PixelFormats.Rgb48));
            Assert.False(ImageView.CanBmpPreserveSourceBitDepth(PixelFormats.Gray16));
            Assert.False(ImageView.CanBmpPreserveSourceBitDepth(PixelFormats.Bgra32));
            Assert.False(ImageView.CanBmpPreserveSourceBitDepth(PixelFormats.Pbgra32));
            Assert.True(ImageView.CanBmpPreserveSourceBitDepth(PixelFormats.Bgr24));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveSnapshotExportsAsync_PreservesEightBitBgr24AsBmp()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "ColorVision.Tests",
            Guid.NewGuid().ToString("N"));
        string fileName = Path.Combine(directory, "source.bmp");
        byte[] pixels =
        [
            1, 2, 3, 4, 5, 6,
            7, 8, 9, 10, 11, 12,
        ];
        try
        {
            ImageViewSnapshot snapshot = RunOnSta(() =>
            {
                EnsureImageViewTestResources();
                BitmapSource source = BitmapSource.Create(
                    2,
                    2,
                    96,
                    96,
                    PixelFormats.Bgr24,
                    null,
                    pixels,
                    6);
                source.Freeze();
                ImageView imageView = new();
                try
                {
                    imageView.ImageShow.Source = source;
                    return imageView.CaptureSnapshotForBackgroundSave(includeOverlays: false)!;
                }
                finally
                {
                    imageView.Dispose();
                }
            });

            await ImageView.SaveSnapshotExportsAsync(
                snapshot,
                new ImageViewSnapshotExportOptions
                {
                    SourceFileName = fileName,
                    SourceOptions = new ImageViewSourceSaveOptions
                    {
                        Format = ImageViewSourceFormat.Bmp,
                    },
                });

            BitmapFrame frame = LoadBitmap(fileName);
            Assert.Equal(PixelFormats.Bgr24, frame.Format);
            byte[] actual = new byte[pixels.Length];
            frame.CopyPixels(actual, 6, 0);
            Assert.Equal(pixels, actual);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SourceSaveOptions_DoNotExposeResizeOrLossyQuality()
    {
        Assert.Null(typeof(ImageViewSourceSaveOptions).GetProperty("ScaleDivisor"));
        Assert.Null(typeof(ImageViewSourceSaveOptions).GetProperty("Quality"));
        Assert.Null(typeof(ImageViewSourceSaveOptions).GetProperty("JpegQuality"));
    }

    private static BitmapFrame LoadPng(string fileName)
    {
        using FileStream stream = File.OpenRead(fileName);
        PngBitmapDecoder decoder = new(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        BitmapFrame frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }

    private static BitmapFrame LoadBitmap(string fileName)
    {
        using FileStream stream = File.OpenRead(fileName);
        BitmapDecoder decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        BitmapFrame frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }

    [Fact]
    public void LifecycleEvents_RaiseLoadedBeforeExplicitExternalRenderCompleted()
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            ImageView imageView = new();
            try
            {
                List<string> events = [];
                object renderContext = new();
                ImageViewImageSourceLoadedEventArgs? loaded = null;
                ImageViewExternalRenderCompletedEventArgs? rendered = null;
                imageView.ImageSourceLoaded += (_, e) =>
                {
                    loaded = e;
                    events.Add("loaded");
                };
                imageView.ExternalRenderCompleted += (_, e) =>
                {
                    rendered = e;
                    events.Add("rendered");
                };

                WriteableBitmap source = new(2, 2, 96, 96, PixelFormats.Bgra32, null);
                imageView.SetImageSource(source);

                Assert.Equal(["loaded"], events);
                Assert.NotNull(loaded);
                Assert.Same(source, loaded.Source);
                Assert.Equal(imageView.ImageRevision, loaded.ImageRevision);

                imageView.NotifyExternalRenderCompleted(renderContext);

                Assert.Equal(["loaded", "rendered"], events);
                Assert.NotNull(rendered);
                Assert.Same(source, rendered.Source);
                Assert.Same(renderContext, rendered.Context);
                Assert.True(rendered.Succeeded);
                Assert.Equal(imageView.ImageRevision, rendered.ImageRevision);
            }
            finally
            {
                imageView.Dispose();
            }
        });
    }

    private static T RunOnSta<T>(Func<T> action)
    {
        return WpfTestHost.Invoke(action);
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

    private static void AssertPixel(
        BitmapSource source,
        int x,
        int y,
        byte blue,
        byte green,
        byte red,
        byte alpha)
    {
        FormatConvertedBitmap converted = new(source, PixelFormats.Bgra32, null, 0);
        byte[] pixel = new byte[4];
        converted.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        Assert.Equal(new[] { blue, green, red, alpha }, pixel);
    }

}
