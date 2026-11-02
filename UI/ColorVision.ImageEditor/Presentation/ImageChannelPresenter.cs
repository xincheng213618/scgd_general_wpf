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

internal enum ChannelSelectionResult
{
    Superseded,
    Ordinary,
    StreamAccepted,
    StreamRejected,
}

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

    public ChannelSelectionResult SelectChannel(int channel, bool preferStream = false)
    {
        context.Dispatcher.VerifyAccess();
        if (_disposed || context.IsDisposed || context.ViewBitmapSource == null) return ChannelSelectionResult.Superseded;
        CancellationTokenSource selection = BeginSelection();
        ImageFrameLease? lease = null;
        bool handedOff = false;
        try
        {
            Guid documentId = context.DocumentInstanceId;
            long revision = context.ImageRevision;
            ImageSource source = context.ViewBitmapSource;
            long claimSequence = context.AlgorithmClaimSequence;
            long selectionVersion = context.Presentation.SelectionVersion;
            bool restoredPreview = context.SupersedePreviewForDisplaySelection();
            long expectedSelectionVersion = selectionVersion + (restoredPreview ? 1 : 0);

            bool CanContinue() => !_disposed && !context.IsDisposed && !selection.IsCancellationRequested
                && context.DocumentInstanceId == documentId && context.IsCurrentImageRevision(revision)
                && ReferenceEquals(context.ViewBitmapSource, source) && context.AlgorithmClaimSequence == claimSequence
                && !context.HasActiveAlgorithmPreview && context.Presentation.SelectionVersion == expectedSelectionVersion;

            // Cancel publishes the canonical source synchronously. Its subscribers may
            // already have selected another display, source, channel, or algorithm owner.
            if (!CanContinue()) return ChannelSelectionResult.Superseded;
            if (channel == -1)
            {
                context.Presentation.RestoreSource();
                expectedSelectionVersion++;
                return CanContinue() ? ChannelSelectionResult.Ordinary : ChannelSelectionResult.Superseded;
            }

            ChannelSelectionResult result = ChannelSelectionResult.Ordinary;
            ImagePresentationRequest? request = null;
            if (preferStream && channel is >= 0 and < 3 && channel == context.GetSelectedLayerSourceChannelIndex()
                && (!context.DisplayEffects.PseudoColor.IsEnabled || !context.DisplayEffects.PseudoColor.IsAutoSetRange)
                && context.StreamPresentation.IsActive
                && source is BitmapSource bitmap && (bitmap.Format == PixelFormats.Bgr24 || bitmap.Format == PixelFormats.Bgr32
                    || bitmap.Format == PixelFormats.Bgra32 || bitmap.Format == PixelFormats.Pbgra32 || bitmap.Format == PixelFormats.Rgb24
                    || bitmap.Format == PixelFormats.Rgb48 || bitmap.Format == PixelFormats.Rgba64))
            {
                context.DisplayEffects.RetirePreviewForStreamSelection();
                if (!CanContinue()) return ChannelSelectionResult.Superseded;
                request = context.Presentation.BeginRequest();
                expectedSelectionVersion++;
                StreamRefreshResult refresh = context.StreamPresentation.TryRefreshCurrent(CanContinue);
                if (!CanContinue() || refresh == StreamRefreshResult.Superseded) return ChannelSelectionResult.Superseded;
                if (refresh == StreamRefreshResult.Accepted) return ChannelSelectionResult.StreamAccepted;
                // The stream predicate already declined this selection. Ordinary fallback
                // must not invoke the same effect refresh and predicate a second time.
                result = ChannelSelectionResult.StreamRejected;
            }

            lease = context.AcquireImageFrame();
            if (lease == null || !CanContinue()) return ChannelSelectionResult.Superseded;
            _ = RenderAsync(lease, request ?? context.Presentation.BeginRequest(), channel, selection);
            lease = null;
            handedOff = true;
            return result;
        }
        finally
        {
            lease?.Dispose();
            if (!handedOff) CompleteSelection(selection);
        }
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
        bool enteredGate = false;
        try
        {
            await _renderGate.WaitAsync(selection.Token).ConfigureAwait(false);
            enteredGate = true;
            selection.Token.ThrowIfCancellationRequested();

            HImage source = lease.Image;
            WriteableBitmap prepared = await Task.Run(() => PrepareChannel(source, channel), selection.Token).ConfigureAwait(false);

            await context.Dispatcher.InvokeAsync(() =>
            {
                if (context.IsDisposed || selection.IsCancellationRequested || !context.Presentation.IsCurrent(request)) return;
                context.Presentation.TryPublish(request, prepared, prepared);
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
            if (enteredGate) _renderGate.Release();
            CompleteSelection(selection);
        }
    }

    internal static WriteableBitmap PrepareChannel(HImage source, int channel)
    {
        if (channel < 0 || channel >= source.channels)
            throw new ArgumentOutOfRangeException(nameof(channel), channel, "Channel must identify a source channel.");
        ChannelTarget target = CreateTarget(source);
        bool hasPixels = false;
        try
        {
            ExtractPixels(source, target, channel);
            hasPixels = true;
        }
        finally { target.Unlock(hasPixels); }
        target.Bitmap.Freeze();
        return target.Bitmap;
    }

    private static void ExtractPixels(HImage source, ChannelTarget target, int channel)
    {
        using Mat input = Mat.FromPixelData(source.rows, source.cols,
            MatType.MakeType(GetOpenCvDepth(source.depth), source.channels), source.pData, source.stride);
        using Mat output = Mat.FromPixelData(source.rows, source.cols, target.Type, target.Buffer, target.Stride);
        Cv2.ExtractChannel(input, output, channel);
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
