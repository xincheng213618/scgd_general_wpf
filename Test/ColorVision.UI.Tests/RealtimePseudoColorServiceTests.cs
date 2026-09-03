using ColorVision.Core;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.EditorTools.PseudoColor;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class RealtimePseudoColorServiceTests
{
    [Fact]
    public void RealtimeRequestRequiresSourceAndCurrentGenerationPublishesProcessedFrame()
    {
        (ImageProcessingContext context, PseudoColorController controller) = CreateController();

        try
        {
            Assert.False(controller.TryCreateRequest(out _, 0));

            WpfTestHost.Invoke(() =>
            {
                WriteableBitmap source = new(2, 2, 96, 96, PixelFormats.Gray8, null);
                source.WritePixels(new Int32Rect(0, 0, 2, 2), new byte[] { 1, 2, 3, 4 }, 2, 0);
                context.ViewBitmapSource = source;
                context.ImageShow.Source = source;
            });
            Assert.True(controller.TryCreateRequest(out RealtimePseudoColorRequest request, 0));
            Assert.True(controller.IsEnabled);

            HImage currentFrame = CreateBgrFrame(11);
            WpfTestHost.Invoke(() => controller.ApplyProcessedImage(request, currentFrame));

            BitmapSource published = WpfTestHost.Invoke(() => Assert.IsAssignableFrom<BitmapSource>(context.FunctionImage));
            Assert.Same(published, WpfTestHost.Invoke(() => context.ImageShow.Source));
            Assert.Equal((byte)11, WpfTestHost.Invoke(() => ReadFirstByte(published)));

            controller.Invalidate();
            HImage staleFrame = CreateBgrFrame(77);
            WpfTestHost.Invoke(() => controller.ApplyProcessedImage(request, staleFrame));

            Assert.Same(published, WpfTestHost.Invoke(() => context.FunctionImage));
            Assert.Equal((byte)11, WpfTestHost.Invoke(() => ReadFirstByte(published)));
        }
        finally
        {
            WpfTestHost.Invoke(controller.Dispose);
        }
    }

    private static (ImageProcessingContext Context, PseudoColorController Controller) CreateController()
        => WpfTestHost.Invoke(() =>
        {
            ImageSource? source = null;
            ImageSource? function = null;
            Guid documentId = Guid.NewGuid();
            ImageProcessingContext context = new(
                new ImageViewConfig(),
                new DrawCanvas(),
                Dispatcher.CurrentDispatcher,
                new ImageProcessingContextBinding
                {
                    IsInitialized = () => true,
                    GetDocumentInstanceId = () => documentId,
                    IsDisposed = () => false,
                    GetImageRevision = () => 1,
                    AcquireImageFrame = () => null,
                    IsCurrentImageRevision = revision => revision == 1,
                    NotifySourcePixelsChanged = () => { },
                    GetFunctionImage = () => function,
                    SetFunctionImage = value => function = value,
                    GetViewBitmapSource = () => source,
                    SetViewBitmapSource = value => source = value,
                    GetSelectedLayerSourceChannelIndex = () => 0,
                    SetImageSource = value => source = value,
                    UpdateZoomAndScale = () => { },
                });
            PseudoColorToolState state = new() { IsEnabled = true };
            return (context, new PseudoColorController(context, state));
        });

    private static HImage CreateBgrFrame(byte value)
    {
        HImage image = OpenCVMediaHelper.AllocateHImage(2, 2, 3, 8);
        Marshal.Copy(Enumerable.Repeat(value, image.stride * image.rows).ToArray(), 0, image.pData, image.stride * image.rows);
        return image;
    }

    private static byte ReadFirstByte(BitmapSource source)
    {
        byte[] pixel = new byte[Math.Max(1, source.Format.BitsPerPixel / 8)];
        source.CopyPixels(new Int32Rect(0, 0, 1, 1), pixel, pixel.Length, 0);
        return pixel[0];
    }
}
