using ColorVision.Core;
using ColorVision.ImageEditor.Abstractions;
using log4net;
using System;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Presentation;

/// <summary>
/// A bounded continuous-frame display queue. Each processed display is committed together with
/// its source pixels; acquisition, playback clocks, and measurement are owned by their producers.
/// </summary>
public sealed class ImageStreamPresentation : IDisposable
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(ImageStreamPresentation));
    private readonly ImageProcessingContext _context;
    private readonly Func<HImage, PseudoColorFrameRequest, HImage> _process;
    private FrameWork? _pending;
    private WriteableBitmap? _latestSource;
    private Func<bool>? _canPublish;
    private Guid _sourceId;
    private long _epoch;
    private long _sequence;
    private long _publishedSequence;
    private long _expectedRevision;
    private bool _running;
    private bool _publishing;
    private bool _disposed;
    private long _workerId;
    private Task _completion = Task.CompletedTask;

    private sealed record FrameWork(WriteableBitmap Source, PseudoColorFrameRequest Settings, long Epoch, long Sequence);

    internal ImageStreamPresentation(ImageProcessingContext context)
        : this(context, ProcessPseudoColor) { }

    internal ImageStreamPresentation(ImageProcessingContext context, Func<HImage, PseudoColorFrameRequest, HImage> process)
    {
        _context = context;
        _process = process;
        _expectedRevision = context.ImageRevision;
        context.DocumentScopeChanged += OnDocumentScopeChanged;
    }

    public bool IsActive { get; private set; }
    internal Task Completion => _completion;
    internal event Action<Guid, WriteableBitmap>? FramePresented;

    public void Submit(WriteableBitmap source, Guid sourceId, Func<bool>? canPublish = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        _context.Dispatcher.VerifyAccess();
        if (_disposed || _context.IsDisposed || canPublish?.Invoke() == false) return;
        if (_sourceId != sourceId || _expectedRevision != _context.ImageRevision)
        {
            Reset();
            _sourceId = sourceId;
        }
        IsActive = true;
        _canPublish = canPublish;
        WriteableBitmap frozen = source.IsFrozen ? source : source.Clone();
        if (!frozen.IsFrozen) frozen.Freeze();
        _latestSource = frozen;
        _expectedRevision = _context.ImageRevision;

        if (!_context.DisplayEffects.TryCapturePseudoColorRequest(out PseudoColorFrameRequest request))
        {
            ++_epoch;
            _pending = null;
            Publish(frozen, null, ++_sequence);
            return;
        }

        // Only queued/processing frames retain snapshots. Replacing the pending work releases
        // its managed reference, and native buffers are allocated only when processing starts.
        _pending = new FrameWork(frozen, request, _epoch, ++_sequence);
        if (_running) return;
        _running = true;
        _completion = RunAsync(++_workerId);
    }

    public void RefreshCurrent()
    {
        _context.Dispatcher.VerifyAccess();
        if (IsActive && _latestSource != null) Submit(_latestSource, _sourceId, _canPublish);
    }

    public void Reset()
    {
        _context.Dispatcher.VerifyAccess();
        ++_epoch;
        _pending = null;
        _latestSource = null;
        _canPublish = null;
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

                HImage output = default;
                try
                {
                    output = await Task.Run(() =>
                    {
                        using HImage source = work.Source.ToHImage();
                        return _process(source, work.Settings);
                    }).ConfigureAwait(false);
                    await _context.Dispatcher.InvokeAsync(() =>
                    {
                        if (!CanPublish(work)) return;
                        HImage borrowed = output;
                        borrowed.isDispose = true;
                        // A displayed bitmap can be retained by an algorithm preview or output
                        // capture. Never borrow and mutate another operation's FunctionImage.
                        var display = borrowed.ToWriteableBitmap();
                        display.Freeze();
                        Publish(work.Source, display, work.Sequence);
                    });
                }
                catch (Exception ex)
                {
                    Log.Warn("Unable to process a continuous preview frame.", ex);
                    if (!_context.Dispatcher.HasShutdownStarted)
                        await _context.Dispatcher.InvokeAsync(() =>
                        {
                            if (CanPublish(work)) Publish(work.Source, null, work.Sequence);
                        });
                }
                finally
                {
                    output.Dispose();
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
        => !_disposed && !_context.IsDisposed && IsActive && work.Epoch == _epoch
            && work.Sequence > _publishedSequence && _context.ImageRevision == _expectedRevision
            && _canPublish?.Invoke() != false
            && _context.DisplayEffects.TryCapturePseudoColorRequest(out PseudoColorFrameRequest current)
            && current == work.Settings;

    private void Publish(WriteableBitmap source, System.Windows.Media.ImageSource? display, long sequence)
    {
        long previousRevision = _context.ImageRevision;
        long selectionVersion = _context.Presentation.SelectionVersion;
        long epoch = _epoch;
        Guid sourceId = _sourceId;
        _publishing = true;
        try
        {
            _context.CommitSourcePixels(source);
            // A synchronous revision subscriber may replace or clear the document. Such a
            // newer publication wins over this frame, just as it does for algorithm previews.
            if (_disposed || !IsActive || epoch != _epoch || sourceId != _sourceId
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
            _context.Presentation.Publish(display ?? source, display);
        }
        finally
        {
            _publishing = false;
        }
        FramePresented?.Invoke(sourceId, source);
    }

    private void OnDocumentScopeChanged(object? sender, EventArgs e)
    {
        if (!_publishing) Reset();
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
