using ColorVision.Core;
using log4net;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Presentation;

/// <summary>Produces channel previews without owning the document or the display surface.</summary>
internal sealed class ImageChannelPresenter(ImageProcessingContext context) : IDisposable
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(ImageChannelPresenter));
    private readonly object _selectionSync = new();
    private readonly SemaphoreSlim _renderGate = new(1, 1);
    private CancellationTokenSource? _selection;
    private bool _disposed;

    public void SelectChannel(int channel)
    {
        context.Dispatcher.VerifyAccess();
        if (_disposed || context.ViewBitmapSource == null) return;
        CancellationTokenSource selection = BeginSelection();
        context.SupersedePreviewForDisplaySelection();
        if (channel == -1)
        {
            context.Presentation.RestoreSource();
            CompleteSelection(selection);
            return;
        }

        ImageFrameLease? lease = context.AcquireImageFrame();
        if (lease == null)
        {
            CompleteSelection(selection);
            return;
        }
        ImagePresentationRequest request = context.Presentation.BeginRequest();
        _ = RenderAsync(lease, request, channel, selection);
    }

    private CancellationTokenSource BeginSelection()
    {
        CancellationTokenSource selection = new();
        lock (_selectionSync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _selection?.Cancel();
            _selection = selection;
        }
        return selection;
    }

    private void CompleteSelection(CancellationTokenSource selection)
    {
        lock (_selectionSync)
        {
            if (ReferenceEquals(_selection, selection)) _selection = null;
        }
        selection.Dispose();
    }

    private async Task RenderAsync(ImageFrameLease lease, ImagePresentationRequest request, int channel, CancellationTokenSource selection)
    {
        HImage output = default;
        bool enteredGate = false;
        try
        {
            await _renderGate.WaitAsync(selection.Token).ConfigureAwait(false);
            enteredGate = true;
            selection.Token.ThrowIfCancellationRequested();
            (int status, HImage result) = await Task.Run(() =>
            {
                using (lease)
                {
                    int status = OpenCVMediaHelper.M_ExtractChannel(lease.Image, out HImage result, channel);
                    return (status, result);
                }
            }).ConfigureAwait(false);
            output = result;
            if (status != 0 || selection.IsCancellationRequested) return;

            await context.Dispatcher.InvokeAsync(() =>
            {
                if (context.IsDisposed || selection.IsCancellationRequested || !context.Presentation.IsCurrent(request)) return;
                // Conversion helpers receive a borrowed descriptor; this operation retains the
                // sole owner and releases it on success, rejection, and dispatcher failure.
                HImage borrowed = output;
                borrowed.isDispose = true;
                var image = borrowed.ToWriteableBitmap();
                image.Freeze();
                context.Presentation.TryPublish(request, image, image);
            });
        }
        catch (OperationCanceledException) when (selection.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Log.Warn("Unable to render the selected image channel.", ex);
        }
        finally
        {
            lease.Dispose();
            output.Dispose();
            if (enteredGate) _renderGate.Release();
            CompleteSelection(selection);
        }
    }

    public void Dispose()
    {
        lock (_selectionSync)
        {
            if (_disposed) return;
            _disposed = true;
            _selection?.Cancel();
        }
    }
}
