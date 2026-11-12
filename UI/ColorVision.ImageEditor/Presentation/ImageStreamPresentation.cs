using ColorVision.Core;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Documents;
using log4net;
using OpenCvSharp;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Presentation;

internal enum StreamRefreshResult
{
    Accepted,
    Unavailable,
    Superseded,
}

/// <summary>
/// A bounded continuous-frame display queue. Each processed display is committed together with
/// its source pixels; acquisition, playback clocks, and measurement are owned by their producers.
/// </summary>
public sealed class ImageStreamPresentation : IDisposable
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(ImageStreamPresentation));
    private readonly ImageProcessingContext _context;
    private readonly Func<HImage, PseudoColorFrameRequest, HImage>? _process;
    private FrameWork? _pending;
    private WriteableBitmap? _latestSource;
    private BitmapSource? _latestRenderProxy;
    private Func<bool>? _canPublish;
    private CancellationToken _latestLifetime;
    private Guid _sourceId;
    private long _epoch;
    private long _sequence;
    private long _publishedSequence;
    private long _expectedRevision;
    private volatile bool _sourceIsDisplayed = true;
    private bool _running;
    private bool _publishing;
    private bool _disposed;
    private long _workerId;
    private Task _completion = Task.CompletedTask;

    private sealed record FrameWork(WriteableBitmap Source, PseudoColorFrameRequest? Settings, int Channel, long Epoch, long Sequence, CancellationToken Lifetime);

    internal ImageStreamPresentation(ImageProcessingContext context)
        : this(context, null) { }

    internal ImageStreamPresentation(ImageProcessingContext context, Func<HImage, PseudoColorFrameRequest, HImage>? process)
    {
        _context = context;
        _process = process;
        _expectedRevision = context.ImageRevision;
        context.DocumentScopeChanged += OnDocumentScopeChanged;
    }

    public bool IsActive { get; private set; }
    internal Task Completion => _completion;
    internal event Action<Guid, WriteableBitmap>? FramePresented;

    /// <summary>False while pseudo color or a channel replaces the source, so a producer can skip its display copy.</summary>
    internal bool SourceIsDisplayed => _sourceIsDisplayed;

    public void Submit(WriteableBitmap source, Guid sourceId, Func<bool>? canPublish = null)
        => SubmitCore(source, null, sourceId, canPublish, default, acceptanceGuard: null);

    internal void Submit(WriteableBitmap source, BitmapSource? renderProxy, Guid sourceId, Func<bool>? canPublish, CancellationToken lifetime)
        => SubmitCore(source, renderProxy, sourceId, canPublish, lifetime, acceptanceGuard: null);

    private bool SubmitCore(WriteableBitmap source, BitmapSource? renderProxy, Guid sourceId, Func<bool>? canPublish, CancellationToken lifetime,
        Func<bool>? acceptanceGuard)
    {
        ArgumentNullException.ThrowIfNull(source);
        _context.Dispatcher.VerifyAccess();
        if (_disposed || _context.IsDisposed || lifetime.IsCancellationRequested || canPublish?.Invoke() == false) return false;
        if (acceptanceGuard?.Invoke() == false) return false;
        if (_sourceId != sourceId || _expectedRevision != _context.ImageRevision)
        {
            Reset();
            _sourceId = sourceId;
        }
        IsActive = true;
        _canPublish = canPublish;
        _latestLifetime = lifetime;
        WriteableBitmap frozen = source.IsFrozen ? source : source.Clone();
        if (!frozen.IsFrozen) frozen.Freeze();
        _latestSource = frozen;
        _latestRenderProxy = renderProxy;
        _expectedRevision = _context.ImageRevision;

        bool pseudoColorEnabled = _context.DisplayEffects.TryCapturePseudoColorRequest(out PseudoColorFrameRequest request);
        int channel = _context.GetSelectedLayerSourceChannelIndex();
        if (!pseudoColorEnabled && (channel < 0 || _context.ViewBitmapSource == null))
        {
            ++_epoch;
            _pending = null;
            _sourceIsDisplayed = true;
            Publish(frozen, null, ++_sequence, renderProxy, lifetime);
            return true;
        }

        _sourceIsDisplayed = false;

        // Only queued/processing frames retain snapshots. Replacing the pending work releases
        // its managed reference, and native buffers are allocated only when processing starts.
        _pending = new FrameWork(frozen, pseudoColorEnabled ? request : null, channel, _epoch, ++_sequence, lifetime);
        if (_running) return true;
        _running = true;
        _completion = RunAsync(++_workerId);
        return true;
    }

    public void RefreshCurrent() => TryRefreshCurrent(acceptanceGuard: null);

    internal StreamRefreshResult TryRefreshCurrent(Func<bool>? acceptanceGuard)
    {
        _context.Dispatcher.VerifyAccess();
        if (!IsActive || _latestSource == null) return StreamRefreshResult.Unavailable;
        long epoch = _epoch;
        long sequence = _sequence;
        Guid sourceId = _sourceId;
        WriteableBitmap source = _latestSource;
        bool IsCurrent() => !_disposed && !_context.IsDisposed && IsActive && _epoch == epoch && _sequence == sequence
            && _sourceId == sourceId && ReferenceEquals(_latestSource, source) && acceptanceGuard?.Invoke() != false;

        if (SubmitCore(source, _latestRenderProxy, sourceId, _canPublish, _latestLifetime, IsCurrent)) return StreamRefreshResult.Accepted;
        // A rejecting predicate may still have installed a newer stream. Recheck the
        // scope without invoking it again before allowing an ordinary display fallback.
        return IsCurrent() ? StreamRefreshResult.Unavailable : StreamRefreshResult.Superseded;
    }

    public void Reset()
    {
        _context.Dispatcher.VerifyAccess();
        ++_epoch;
        _pending = null;
        _latestSource = null;
        _latestRenderProxy = null;
        _sourceIsDisplayed = true;
        _canPublish = null;
        _latestLifetime = default;
        _sourceId = Guid.Empty;
        IsActive = false;
    }

    public void ResetSource(Guid sourceId)
    {
        _context.Dispatcher.VerifyAccess();
        if (_sourceId == sourceId) Reset();
    }

    private async Task RunAsync(long workerId)
    {
        try
        {
            while (true)
            {
                FrameWork? work = await _context.Dispatcher.InvokeAsync(() =>
                {
                    FrameWork? current = _pending;
                    _pending = null;
                    if (current == null || _disposed) _running = false;
                    return _disposed ? null : current;
                });
                if (work == null) return;
                if (work.Lifetime.IsCancellationRequested) continue;

                try
                {
                    (WriteableBitmap display, BitmapSource? displayProxy) = await Task.Run(() =>
                    {
                        WriteableBitmap bitmap = PrepareDisplayBitmap(work);
                        bitmap.Freeze();
                        return (bitmap, (BitmapSource?)ImageRenderProxy.TryCreate(bitmap));
                    }).ConfigureAwait(false);
                    await _context.Dispatcher.InvokeAsync(() =>
                    {
                        if (!CanPublish(work)) return;
                        // A displayed bitmap can be retained by an algorithm preview or output
                        // capture. Never borrow and mutate another operation's FunctionImage.
                        Publish(work.Source, display, work.Sequence, displayProxy, work.Lifetime);
                    });
                }
                catch (Exception ex)
                {
                    Log.Warn("Unable to process a continuous preview frame.", ex);
                    if (!_context.Dispatcher.HasShutdownStarted)
                        await _context.Dispatcher.InvokeAsync(() =>
                        {
                            if (CanPublish(work)) Publish(work.Source, null, work.Sequence, null, work.Lifetime);
                        });
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn("Continuous preview dispatcher has stopped.", ex);
        }
        finally
        {
            if (!_context.Dispatcher.HasShutdownStarted)
                await _context.Dispatcher.InvokeAsync(() =>
                {
                    if (workerId == _workerId) _running = false;
                });
        }
    }

    private bool CanPublish(FrameWork work)
    {
        if (_disposed || _context.IsDisposed || !IsActive || work.Lifetime.IsCancellationRequested || work.Epoch != _epoch
            || work.Sequence <= _publishedSequence || _context.ImageRevision != _expectedRevision
            || _canPublish?.Invoke() == false) return false;
        bool pseudoColorEnabled = _context.DisplayEffects.TryCapturePseudoColorRequest(out PseudoColorFrameRequest current);
        return work.Settings is PseudoColorFrameRequest settings
            ? pseudoColorEnabled && current == settings
            : !pseudoColorEnabled && _context.GetSelectedLayerSourceChannelIndex() == work.Channel;
    }

    private void Publish(WriteableBitmap source, System.Windows.Media.ImageSource? display, long sequence, BitmapSource? renderProxy, CancellationToken lifetime)
    {
        if (lifetime.IsCancellationRequested) return;
        long previousRevision = _context.ImageRevision;
        long selectionVersion = _context.Presentation.SelectionVersion;
        long epoch = _epoch;
        Guid sourceId = _sourceId;
        _publishing = true;
        try
        {
            using IDisposable? sourceRestore = display != null
                ? _context.Presentation.DeferSourceRestoreForFrame(source, previousRevision + 1)
                : null;
            _context.CommitSourcePixels(source);
            // A synchronous revision subscriber may replace or clear the document. Such a
            // newer publication wins over this frame, just as it does for algorithm previews.
            if (_disposed || !IsActive || lifetime.IsCancellationRequested || epoch != _epoch || sourceId != _sourceId
                || _context.IsDisposed || _context.ImageRevision != previousRevision + 1
                || !ReferenceEquals(_context.ViewBitmapSource, source)
                || _context.HasActiveAlgorithmPreview
                || _context.Presentation.SelectionVersion != selectionVersion)
            {
                // A reentrant submission can already own this queue. Retire only the
                // scope belonging to this publication, never its newer replacement.
                if (!_disposed && epoch == _epoch && sourceId == _sourceId && sequence > _publishedSequence)
                    Reset();
                return;
            }
            _expectedRevision = _context.ImageRevision;
            _publishedSequence = sequence;
            _context.Presentation.PublishFrame(display ?? source, display, renderProxy);
            ImageMemoryReclaimer.RecordStreamFrame(ImageMemoryReclaimer.GetPixelBytes(source)
                + ImageMemoryReclaimer.GetPixelBytes(display) + ImageMemoryReclaimer.GetPixelBytes(renderProxy));
        }
        finally
        {
            _publishing = false;
        }
        if (!lifetime.IsCancellationRequested) FramePresented?.Invoke(sourceId, source);
    }

    private void OnDocumentScopeChanged(object? sender, EventArgs e)
    {
        if (!_publishing) Reset();
    }

    private static HImage ExtractChannel(HImage source, int channel)
    {
        int status = OpenCVMediaHelper.M_ExtractChannel(source, out HImage output, channel);
        if (status == 0) return output;
        output.Dispose();
        throw new InvalidOperationException($"Channel extraction failed with status {status}.");
    }

    private WriteableBitmap PrepareDisplayBitmap(FrameWork work)
    {
        PixelFormat format = work.Source.Format;
        if (_process == null && work.Settings is PseudoColorFrameRequest request && work.Source.IsFrozen
            && (format == PixelFormats.Gray8 || format == PixelFormats.Gray16 || format == PixelFormats.Bgr24
                || format == PixelFormats.Rgb24 || format == PixelFormats.Rgb48 || format == PixelFormats.Bgra32))
            return PreparePseudoColorBitmap(work.Source, request);

        using HImage source = work.Source.ToHImage();
        using HImage output = work.Settings is PseudoColorFrameRequest settings
            ? (_process ?? ProcessPseudoColor)(source, settings)
            : ExtractChannel(source, work.Channel);
        return output.ToWriteableBitmap();
    }

    private static WriteableBitmap PreparePseudoColorBitmap(WriteableBitmap source, PseudoColorFrameRequest request)
    {
        // The native Into methods read the frozen source and write only to this private bitmap.
        using HImage sourcePixels = source.ForHImage();
        WriteableBitmap bitmap = new(source.PixelWidth, source.PixelHeight, 96, 96, PixelFormats.Bgr24, null);
        bitmap.Lock();
        try
        {
            HImage input = sourcePixels;
            using Mat? gray = NeedsRgbLuminance(source.Format, input, request.Channel) ? CreateRgbLuminance(input, out input) : null;
            using HImage output = bitmap.ForHImage();
            int status = request.HasValidAutoRange
                ? OpenCVMediaHelper.M_PseudoColorAutoRangeInto(input, output, request.Min, request.Max, request.ColormapTypes, request.Channel, request.DataMin, request.DataMax)
                : OpenCVMediaHelper.M_PseudoColorInto(input, output, request.Min, request.Max, request.ColormapTypes, request.Channel);
            if (status != 0) throw new InvalidOperationException($"Pseudo-color processing failed with status {status}.");
            bitmap.AddDirtyRect(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
        }
        finally
        {
            GC.KeepAlive(source);
            bitmap.Unlock();
        }
        return bitmap;
    }

    internal static bool NeedsRgbLuminance(PixelFormat format, HImage image, int channel)
        => channel is < 0 or >= 3 && image.channels == 3
            && ((format == PixelFormats.Rgb24 && image.depth == 8) || (format == PixelFormats.Rgb48 && image.depth == 16));

    // Native pseudo-color and range calculation use BGR for composite luminance.
    // RGB source snapshots need only one luminance plane, and selected channels stay in source order.
    internal static Mat CreateRgbLuminance(HImage image, out HImage luminance)
    {
        using Mat rgb = Mat.FromPixelData(image.rows, image.cols, image.depth == 16 ? MatType.CV_16UC3 : MatType.CV_8UC3,
            image.pData, image.stride);
        Mat gray = new();
        try
        {
            Cv2.CvtColor(rgb, gray, ColorConversionCodes.RGB2GRAY);
            luminance = image;
            luminance.pData = gray.Data;
            luminance.channels = 1;
            luminance.stride = checked((int)gray.Step());
            luminance.isDispose = true;
            return gray;
        }
        catch
        {
            gray.Dispose();
            throw;
        }
    }

    private static HImage ProcessPseudoColor(HImage source, PseudoColorFrameRequest request)
    {
        HImage output = default;
        int status = request.HasValidAutoRange
            ? OpenCVMediaHelper.ApplyPseudoColorAutoRange(source, out output, request.Min, request.Max, request.ColormapTypes, request.Channel, request.DataMin, request.DataMax)
            : OpenCVMediaHelper.ApplyPseudoColor(source, out output, request.Min, request.Max, request.ColormapTypes, request.Channel);
        if (status == 0) return output;
        output.Dispose();
        throw new InvalidOperationException($"Pseudo-color processing failed with status {status}.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Reset();
        _context.DocumentScopeChanged -= OnDocumentScopeChanged;
        FramePresented = null;
    }
}
