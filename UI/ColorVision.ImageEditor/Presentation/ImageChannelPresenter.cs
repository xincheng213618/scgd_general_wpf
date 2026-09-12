using ColorVision.Core;
using log4net;
using System;
using System.Threading.Tasks;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Presentation;

/// <summary>Produces channel previews without owning the document or the display surface.</summary>
internal sealed class ImageChannelPresenter(ImageProcessingContext context)
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(ImageChannelPresenter));

    public void SelectChannel(int channel)
    {
        context.Dispatcher.VerifyAccess();
        if (context.ViewBitmapSource == null) return;
        context.SupersedePreviewForDisplaySelection();
        if (channel == -1)
        {
            context.Presentation.RestoreSource();
            return;
        }

        ImageFrameLease? lease = context.AcquireImageFrame();
        if (lease == null) return;
        ImagePresentationRequest request = context.Presentation.BeginRequest();
        _ = RenderAsync(lease, request, channel);
    }

    private async Task RenderAsync(ImageFrameLease lease, ImagePresentationRequest request, int channel)
    {
        HImage output = default;
        try
        {
            (int status, HImage result) = await Task.Run(() =>
            {
                using (lease)
                {
                    int status = OpenCVMediaHelper.M_ExtractChannel(lease.Image, out HImage result, channel);
                    return (status, result);
                }
            }).ConfigureAwait(false);
            output = result;
            if (status != 0) return;

            await context.Dispatcher.InvokeAsync(() =>
            {
                if (context.IsDisposed || !context.Presentation.IsCurrent(request)) return;
                // Conversion helpers receive a borrowed descriptor; this operation retains the
                // sole owner and releases it on success, rejection, and dispatcher failure.
                HImage borrowed = output;
                borrowed.isDispose = true;
                var image = borrowed.ToWriteableBitmap();
                image.Freeze();
                context.Presentation.TryPublish(request, image, image);
            });
        }
        catch (Exception ex)
        {
            Log.Warn("Unable to render the selected image channel.", ex);
        }
        finally
        {
            lease.Dispose();
            output.Dispose();
        }
    }
}
