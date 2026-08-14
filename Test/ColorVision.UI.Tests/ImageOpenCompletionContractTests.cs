using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Tif;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AppCommandContextMenu = ColorVision.ImageEditor.EditorTools.AppCommand.ImageViewSettingsEditorToolContextMenu;

namespace ColorVision.UI.Tests;

public sealed class ImageOpenCompletionContractTests
{
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
            });

            await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));

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

        try
        {
            WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                WriteEncodedImage(filePath);

                imageView = new ImageView();
                imageView.ImageSourceLoaded += (_, _) => completionCount++;

                imageView.OpenImage(filePath);
                imageView.OpenImage(missingFilePath);
            });

            await Task.Delay(TimeSpan.FromSeconds(1));

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
    public async Task OpenPng_ReusesMatchingBitmapAndReplacesIncompatibleBitmap()
    {
        string firstFilePath = Path.Combine(
            Path.GetTempPath(),
            $"{nameof(ImageOpenCompletionContractTests)}-{Guid.NewGuid():N}-first.png");
        string secondFilePath = Path.Combine(
            Path.GetTempPath(),
            $"{nameof(ImageOpenCompletionContractTests)}-{Guid.NewGuid():N}-second.png");
        string thirdFilePath = Path.Combine(
            Path.GetTempPath(),
            $"{nameof(ImageOpenCompletionContractTests)}-{Guid.NewGuid():N}-third.png");
        ImageView? imageView = null;
        EventHandler<ImageViewImageSourceLoadedEventArgs>? loadedHandler = null;
        TaskCompletionSource firstLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource thirdLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);
        WriteableBitmap? firstBitmap = null;
        WriteableBitmap? secondBitmap = null;
        int completionCount = 0;

        try
        {
            WriteSolidPng(firstFilePath, 2, 3, 10);
            WriteSolidPng(secondFilePath, 2, 3, 200);
            WriteSolidPng(thirdFilePath, 3, 2, 50);

            WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                imageView = new ImageView();
                imageView.IEditorToolFactory.IEditorTools.Clear();
                loadedHandler = (_, _) =>
                {
                    completionCount++;
                    if (completionCount == 1)
                        firstLoad.TrySetResult();
                    else if (completionCount == 2)
                        secondLoad.TrySetResult();
                    else if (completionCount == 3)
                        thirdLoad.TrySetResult();
                };
                imageView.ImageSourceLoaded += loadedHandler;
                imageView.OpenImage(firstFilePath);
            });

            await firstLoad.Task.WaitAsync(TimeSpan.FromSeconds(10));

            WpfTestHost.Invoke(() =>
            {
                firstBitmap = Assert.IsType<WriteableBitmap>(imageView!.ViewBitmapSource);
                imageView.OpenImage(secondFilePath);
            });

            await secondLoad.Task.WaitAsync(TimeSpan.FromSeconds(10));

            WpfTestHost.Invoke(() =>
            {
                secondBitmap = Assert.IsType<WriteableBitmap>(imageView!.ViewBitmapSource);
                byte[] pixel = new byte[4];
                secondBitmap.CopyPixels(new Int32Rect(0, 0, 1, 1), pixel, 4, 0);

                Assert.Same(firstBitmap, secondBitmap);
                Assert.Equal(200, pixel[0]);
                imageView.OpenImage(thirdFilePath);
            });

            await thirdLoad.Task.WaitAsync(TimeSpan.FromSeconds(10));

            WpfTestHost.Invoke(() =>
            {
                WriteableBitmap thirdBitmap = Assert.IsType<WriteableBitmap>(imageView!.ViewBitmapSource);
                Assert.NotSame(secondBitmap, thirdBitmap);
                Assert.Equal(3, thirdBitmap.PixelWidth);
                Assert.Equal(2, thirdBitmap.PixelHeight);
                Assert.Equal(3, completionCount);
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

            foreach (string filePath in new[] { firstFilePath, secondFilePath, thirdFilePath })
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task OpenTiff_ReusesMatchingBitmapAndNormalizesDpiWithoutChangingPixels()
    {
        string firstFilePath = Path.Combine(
            Path.GetTempPath(),
            $"{nameof(ImageOpenCompletionContractTests)}-{Guid.NewGuid():N}-first.tiff");
        string secondFilePath = Path.Combine(
            Path.GetTempPath(),
            $"{nameof(ImageOpenCompletionContractTests)}-{Guid.NewGuid():N}-second.tiff");
        ImageView? imageView = null;
        EventHandler<ImageViewImageSourceLoadedEventArgs>? loadedHandler = null;
        TaskCompletionSource firstLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);
        WriteableBitmap? firstBitmap = null;
        int completionCount = 0;

        try
        {
            WriteSolidTiff(firstFilePath, 2, 3, 10, 120);
            WriteSolidTiff(secondFilePath, 2, 3, 200, 120);

            WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                imageView = new ImageView();
                imageView.IEditorToolFactory.IEditorTools.Clear();
                loadedHandler = (_, _) =>
                {
                    completionCount++;
                    if (completionCount == 1)
                        firstLoad.TrySetResult();
                    else if (completionCount == 2)
                        secondLoad.TrySetResult();
                };
                imageView.ImageSourceLoaded += loadedHandler;
                imageView.OpenImage(firstFilePath);
            });

            await firstLoad.Task.WaitAsync(TimeSpan.FromSeconds(10));

            WpfTestHost.Invoke(() =>
            {
                firstBitmap = Assert.IsType<WriteableBitmap>(imageView!.ViewBitmapSource);
                Assert.Equal(96, firstBitmap.DpiX, 2);
                Assert.Equal(96, firstBitmap.DpiY, 2);
                imageView.OpenImage(secondFilePath);
            });

            await secondLoad.Task.WaitAsync(TimeSpan.FromSeconds(10));

            WpfTestHost.Invoke(() =>
            {
                WriteableBitmap secondBitmap = Assert.IsType<WriteableBitmap>(imageView!.ViewBitmapSource);
                byte[] pixel = new byte[1];
                secondBitmap.CopyPixels(new Int32Rect(0, 0, 1, 1), pixel, 1, 0);

                Assert.Same(firstBitmap, secondBitmap);
                Assert.Equal(200, pixel[0]);
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

            foreach (string filePath in new[] { firstFilePath, secondFilePath })
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task OpenGray32FloatTiff_ReusesGray16DisplayBitmapAndNormalizesFiniteRange()
    {
        string firstFilePath = Path.Combine(
            Path.GetTempPath(),
            $"{nameof(ImageOpenCompletionContractTests)}-{Guid.NewGuid():N}-first-float.tiff");
        string secondFilePath = Path.Combine(
            Path.GetTempPath(),
            $"{nameof(ImageOpenCompletionContractTests)}-{Guid.NewGuid():N}-second-float.tiff");
        ImageView? imageView = null;
        EventHandler<ImageViewImageSourceLoadedEventArgs>? loadedHandler = null;
        TaskCompletionSource firstLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);
        WriteableBitmap? firstBitmap = null;
        int completionCount = 0;
        bool previousConvertSetting = TifOpenConfig.Current.ConvertGray32FloatToGray16OnOpen;

        try
        {
            WriteGray32FloatTiff(firstFilePath, [-10f, 0f, 10f, 20f, float.NaN, float.PositiveInfinity]);
            WriteGray32FloatTiff(secondFilePath, [100f, 200f, 300f, 400f, float.NaN, float.PositiveInfinity]);
            Assert.Equal(PixelFormats.Gray32Float, ReadTiffPixelFormat(firstFilePath));
            Assert.Equal(PixelFormats.Gray32Float, ReadTiffPixelFormat(secondFilePath));
            TifOpenConfig.Current.ConvertGray32FloatToGray16OnOpen = true;

            WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                imageView = new ImageView();
                imageView.IEditorToolFactory.IEditorTools.Clear();
                loadedHandler = (_, _) =>
                {
                    completionCount++;
                    if (completionCount == 1)
                        firstLoad.TrySetResult();
                    else if (completionCount == 2)
                        secondLoad.TrySetResult();
                };
                imageView.ImageSourceLoaded += loadedHandler;
                imageView.OpenImage(firstFilePath);
            });

            await firstLoad.Task.WaitAsync(TimeSpan.FromSeconds(10));

            WpfTestHost.Invoke(() =>
            {
                firstBitmap = Assert.IsType<WriteableBitmap>(imageView!.ViewBitmapSource);
                Assert.Equal(PixelFormats.Gray16, firstBitmap.Format);
                imageView.OpenImage(secondFilePath);
            });

            await secondLoad.Task.WaitAsync(TimeSpan.FromSeconds(10));

            WpfTestHost.Invoke(() =>
            {
                WriteableBitmap secondBitmap = Assert.IsType<WriteableBitmap>(imageView!.ViewBitmapSource);
                ushort[] pixels = new ushort[6];
                secondBitmap.CopyPixels(pixels, 4, 0);

                Assert.Same(firstBitmap, secondBitmap);
                Assert.Equal(0, pixels[0]);
                Assert.Equal(ushort.MaxValue, pixels[3]);
                Assert.Equal(0, pixels[4]);
                Assert.Equal(0, pixels[5]);
                Assert.Equal(2, completionCount);
            });
        }
        finally
        {
            TifOpenConfig.Current.ConvertGray32FloatToGray16OnOpen = previousConvertSetting;

            if (imageView != null)
            {
                WpfTestHost.Invoke(() =>
                {
                    if (loadedHandler != null)
                        imageView.ImageSourceLoaded -= loadedHandler;
                    imageView.Dispose();
                });
            }

            foreach (string filePath in new[] { firstFilePath, secondFilePath })
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task OpenCvRaw_WithMatchingSizeAndFormat_ReusesBitmapAndPreservesViewport()
    {
        string firstFilePath = Path.Combine(
            Path.GetTempPath(),
            $"{nameof(ImageOpenCompletionContractTests)}-{Guid.NewGuid():N}-first.cvraw");
        string secondFilePath = Path.Combine(
            Path.GetTempPath(),
            $"{nameof(ImageOpenCompletionContractTests)}-{Guid.NewGuid():N}-second.cvraw");
        ImageView? imageView = null;
        EventHandler<ImageViewImageSourceLoadedEventArgs>? loadedHandler = null;
        TaskCompletionSource firstLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);
        WriteableBitmap? firstBitmap = null;
        int completionCount = 0;
        Matrix expectedViewport = new(2, 0, 0, 2, 17, 23);

        try
        {
            WriteCvRaw(firstFilePath, 10);
            WriteCvRaw(secondFilePath, 200);

            WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                imageView = new ImageView();
                // Toolbar regeneration requires a loaded visual tree and is unrelated to this image-open contract.
                imageView.IEditorToolFactory.IEditorTools.Clear();
                loadedHandler = (_, _) =>
                {
                    completionCount++;
                    if (completionCount == 1)
                        firstLoad.TrySetResult();
                    else if (completionCount == 2)
                        secondLoad.TrySetResult();
                };
                imageView.ImageSourceLoaded += loadedHandler;
                imageView.OpenImage(firstFilePath);
            });

            await firstLoad.Task.WaitAsync(TimeSpan.FromSeconds(10));

            WpfTestHost.Invoke(() =>
            {
                firstBitmap = Assert.IsType<WriteableBitmap>(imageView!.ViewBitmapSource);
                imageView.Zoombox1.ContentMatrix = expectedViewport;
                imageView.OpenImage(secondFilePath);
            });

            await secondLoad.Task.WaitAsync(TimeSpan.FromSeconds(10));

            WpfTestHost.Invoke(() =>
            {
                WriteableBitmap secondBitmap = Assert.IsType<WriteableBitmap>(imageView!.ViewBitmapSource);
                byte[] pixel = new byte[1];
                secondBitmap.CopyPixels(new Int32Rect(0, 0, 1, 1), pixel, 1, 0);

                Assert.Same(firstBitmap, secondBitmap);
                Assert.Equal(expectedViewport, imageView.Zoombox1.ContentMatrix);
                Assert.Equal(200, pixel[0]);
                Assert.Equal(secondFilePath, imageView.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath));
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

            if (File.Exists(firstFilePath))
                File.Delete(firstFilePath);
            if (File.Exists(secondFilePath))
                File.Delete(secondFilePath);
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

    private static void WriteSolidPng(string filePath, int width, int height, byte value)
    {
        byte[] pixels = new byte[width * height * 4];
        for (int offset = 0; offset < pixels.Length; offset += 4)
        {
            pixels[offset] = value;
            pixels[offset + 1] = value;
            pixels[offset + 2] = value;
            pixels[offset + 3] = 0xFF;
        }

        BitmapSource source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using FileStream stream = new(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        encoder.Save(stream);
    }

    private static void WriteSolidTiff(string filePath, int width, int height, byte value, double dpi)
    {
        byte[] pixels = Enumerable.Repeat(value, width * height).ToArray();
        BitmapSource source = BitmapSource.Create(width, height, dpi, dpi, PixelFormats.Gray8, null, pixels, width);
        TiffBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using FileStream stream = new(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        encoder.Save(stream);
    }

    private static void WriteGray32FloatTiff(string filePath, float[] pixels)
    {
        const int width = 2;
        const int height = 3;
        const ushort entryCount = 13;
        const uint firstIfdOffset = 8;
        const uint rationalOffset = firstIfdOffset + 2 + entryCount * 12 + 4;
        const uint pixelOffset = rationalOffset + 16;

        Assert.Equal(width * height, pixels.Length);
        using FileStream stream = new(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using BinaryWriter writer = new(stream);

        writer.Write((byte)'I');
        writer.Write((byte)'I');
        writer.Write((ushort)42);
        writer.Write(firstIfdOffset);
        writer.Write(entryCount);
        WriteTiffEntry(writer, 256, 4, 1, width);                  // ImageWidth
        WriteTiffEntry(writer, 257, 4, 1, height);                 // ImageLength
        WriteTiffEntry(writer, 258, 3, 1, 32);                     // BitsPerSample
        WriteTiffEntry(writer, 259, 3, 1, 1);                      // Compression: none
        WriteTiffEntry(writer, 262, 3, 1, 1);                      // PhotometricInterpretation: BlackIsZero
        WriteTiffEntry(writer, 273, 4, 1, pixelOffset);            // StripOffsets
        WriteTiffEntry(writer, 277, 3, 1, 1);                      // SamplesPerPixel
        WriteTiffEntry(writer, 278, 4, 1, height);                 // RowsPerStrip
        WriteTiffEntry(writer, 279, 4, 1, checked((uint)pixels.Length * 4)); // StripByteCounts
        WriteTiffEntry(writer, 282, 5, 1, rationalOffset);         // XResolution
        WriteTiffEntry(writer, 283, 5, 1, rationalOffset + 8);     // YResolution
        WriteTiffEntry(writer, 296, 3, 1, 2);                      // ResolutionUnit: inch
        WriteTiffEntry(writer, 339, 3, 1, 3);                      // SampleFormat: IEEE floating point
        writer.Write(0u);
        writer.Write(96u);
        writer.Write(1u);
        writer.Write(96u);
        writer.Write(1u);

        foreach (float pixel in pixels)
            writer.Write(pixel);
    }

    private static void WriteTiffEntry(BinaryWriter writer, ushort tag, ushort type, uint count, uint valueOrOffset)
    {
        writer.Write(tag);
        writer.Write(type);
        writer.Write(count);
        writer.Write(valueOrOffset);
    }

    private static PixelFormat ReadTiffPixelFormat(string filePath)
    {
        using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        TiffBitmapDecoder decoder = new(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        return decoder.Frames[0].Format;
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
