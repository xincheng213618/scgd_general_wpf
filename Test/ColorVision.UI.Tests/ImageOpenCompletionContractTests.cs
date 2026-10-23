using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Tif;
using ColorVision.ImageEditor.Abstractions;
using System.Threading;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AppCommandContextMenu = ColorVision.ImageEditor.EditorTools.AppCommand.ImageViewSettingsEditorToolContextMenu;

namespace ColorVision.UI.Tests;

public sealed class ImageOpenCompletionContractTests
{
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
