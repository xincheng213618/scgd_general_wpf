using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Tif;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
