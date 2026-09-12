using ColorVision.Core;
using System;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.ImageEditor.Documents;

/// <summary>Owns the loaded source and revision-safe pixel leases for one editor document.</summary>
public sealed class ImageDocument : IDisposable
{
    private readonly ImageFrameStore _frames = new();
    private readonly Dispatcher _dispatcher;
    private ImageSource? _source;
    private ImageSource? _cachedSource;
    private int _disposed;

    internal ImageDocument(Dispatcher dispatcher) => _dispatcher = dispatcher;

    public Guid Id { get; } = Guid.NewGuid();
    public long Revision => _frames.Revision;
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;
    public ImageSource? Source => _source;

    internal event Action<ImageDocumentMutationKind, long, long>? Changed;

    // Compatibility assignments retain their historical semantics. Producers must explicitly
    // commit in-place writes; a source-reference change is also detected when acquiring a lease.
    internal void AssignSource(ImageSource? source)
    {
        _dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        _source = source;
    }

    public bool IsCurrent(long revision) => !IsDisposed && _frames.IsCurrent(revision);

    internal void Invalidate(ImageDocumentMutationKind kind)
    {
        // In-place producers may notify from their worker after updating pixels. The frame
        // store serializes revision retirement; host cleanup dispatches to the UI separately.
        _cachedSource = null;
        long previous = Revision;
        long current = _frames.Invalidate();
        Changed?.Invoke(kind, previous, current);
    }

    internal ImageFrameLease? AcquireFrame()
    {
        if (!_dispatcher.CheckAccess())
            return _dispatcher.Invoke(AcquireFrame);

        ImageSource? source = Source;
        if (_cachedSource != null && !ReferenceEquals(_cachedSource, source))
            Invalidate(ImageDocumentMutationKind.ImageSourceReplaced);

        ImageFrameLease? lease = _frames.AcquireOrCreate(
            () => source is WriteableBitmap bitmap ? bitmap.ToHImage() : null);
        if (lease != null) _cachedSource = source;
        return lease;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _frames.Dispose();
        _source = null;
        _cachedSource = null;
        Changed = null;
    }
}
