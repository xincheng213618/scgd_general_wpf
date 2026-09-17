using ColorVision.Core;
using log4net;
using OpenCvSharp;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Presentation;

/// <summary>Produces channel previews without owning the document or the display surface.</summary>
internal sealed class ImageChannelPresenter(ImageProcessingContext context) : IDisposable
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(ImageChannelPresenter));
    private readonly object _selectionSync = new();
    private readonly SemaphoreSlim _renderGate = new(1, 1);
    private CancellationTokenSource? _selection;
    private bool _disposed;

    private sealed class ChannelTarget
    {
        public ChannelTarget(WriteableBitmap bitmap, IntPtr buffer, int stride, MatType type)
        {
            Bitmap = bitmap;
            Buffer = buffer;
            Stride = stride;
            Type = type;
        }

        public WriteableBitmap Bitmap { get; }
        public IntPtr Buffer { get; }
        public int Stride { get; }
        public MatType Type { get; }
        public bool IsLocked { get; private set; } = true;

        public void Unlock(bool hasPixels)
        {
            if (!IsLocked) return;
            try
            {
                if (hasPixels) Bitmap.AddDirtyRect(new Int32Rect(0, 0, Bitmap.PixelWidth, Bitmap.PixelHeight));
            }
            finally
            {
                Bitmap.Unlock();
                IsLocked = false;
            }
        }
    }

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

    internal void CancelPending()
    {
        lock (_selectionSync)
        {
            _selection?.Cancel();
        }
    }

    private async Task RenderAsync(ImageFrameLease lease, ImagePresentationRequest request, int channel, CancellationTokenSource selection)
    {
        ChannelTarget? target = null;
        bool enteredGate = false;
        try
        {
            await _renderGate.WaitAsync(selection.Token).ConfigureAwait(false);
            enteredGate = true;
            selection.Token.ThrowIfCancellationRequested();

            HImage source = lease.Image;
            target = await context.Dispatcher.InvokeAsync(() => CreateTarget(source));
            selection.Token.ThrowIfCancellationRequested();
            await Task.Run(() =>
            {
                using Mat input = Mat.FromPixelData(source.rows, source.cols,
                    MatType.MakeType(GetOpenCvDepth(source.depth), source.channels), source.pData, source.stride);
                using Mat output = Mat.FromPixelData(source.rows, source.cols, target.Type, target.Buffer, target.Stride);
                Cv2.ExtractChannel(input, output, channel);
            }, selection.Token).ConfigureAwait(false);

            await context.Dispatcher.InvokeAsync(() =>
            {
                target.Unlock(hasPixels: true);
                if (context.IsDisposed || selection.IsCancellationRequested || !context.Presentation.IsCurrent(request)) return;
                target.Bitmap.Freeze();
                context.Presentation.TryPublish(request, target.Bitmap, target.Bitmap);
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
            if (target?.IsLocked == true)
            {
                try { await context.Dispatcher.InvokeAsync(() => target.Unlock(hasPixels: false)); }
                catch (Exception ex) { Log.Warn("Unable to release the channel preview buffer.", ex); }
            }
            lease.Dispose();
            if (enteredGate) _renderGate.Release();
            CompleteSelection(selection);
        }
    }

    private static ChannelTarget CreateTarget(HImage source)
    {
        PixelFormat format = source.depth switch
        {
            8 => PixelFormats.Gray8,
            16 => PixelFormats.Gray16,
            _ => throw new NotSupportedException($"Channel preview does not support {source.depth}-bit pixels."),
        };
        MatType type = MatType.MakeType(GetOpenCvDepth(source.depth), 1);
        WriteableBitmap bitmap = new(source.cols, source.rows, 96, 96, format, null);
        bitmap.Lock();
        return new(bitmap, bitmap.BackBuffer, bitmap.BackBufferStride, type);
    }

    private static int GetOpenCvDepth(int depth) => depth switch
    {
        8 => MatType.CV_8U,
        16 => MatType.CV_16U,
        _ => throw new NotSupportedException($"Channel preview does not support {depth}-bit pixels."),
    };

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
