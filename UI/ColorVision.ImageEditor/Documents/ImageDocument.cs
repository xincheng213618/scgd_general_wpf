using ColorVision.Core;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Tif;
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
    private long _contentVersion;
    private readonly object _contentSync = new();
    private IImageContentCapability? _capability;

    internal ImageDocument(Dispatcher dispatcher) => _dispatcher = dispatcher;

    public Guid Id { get; } = Guid.NewGuid();
    public long Revision => _frames.Revision;
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;
    public ImageSource? Source => _source;
    public DecodedImageContent? Content { get; internal set; }
    public ColorVisionTiffParameters? ParameterRecord { get; internal set; }
    public IImageContentCapability? Capability => Volatile.Read(ref _capability);
    public long ContentVersion => Volatile.Read(ref _contentVersion);
    internal event Action<long>? CapabilityRetired;

    internal void RetireCapability(bool clearContent)
    {
        CompleteRetirement(DetachCapability(clearContent));
    }

    internal void ReplaceCapability(IImageContentCapability? capability)
    {
        (Action? Release, long Version) retirement;
        lock (_contentSync)
        {
            if (ReferenceEquals(_capability, capability)) return;
            long version = Interlocked.Increment(ref _contentVersion);
            retirement = (_capability?.DetachForRetirement(version), version);
            Volatile.Write(ref _capability, capability);
        }
        CompleteRetirement(retirement);
    }

    private (Action? Release, long Version) DetachCapability(bool clearContent)
    {
        lock (_contentSync)
        {
            if (clearContent) Content = null;
            var previous = _capability;
            Volatile.Write(ref _capability, null);
            long version = Interlocked.Increment(ref _contentVersion);
            return (previous?.DetachForRetirement(version), version);
        }
    }

    private void CompleteRetirement((Action? Release, long Version) retirement)
    {
        if (retirement.Release == null) return;
        // Cancel old UI consumers before waiting for a reader that may still be computing a layer.
        try { CapabilityRetired?.Invoke(retirement.Version); }
        finally { retirement.Release(); }
    }

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
        (Action? Release, long Version) retirement;
        long previous;
        long current;
        lock (_contentSync)
        {
            retirement = kind == ImageDocumentMutationKind.DisplayLayerChanged
                ? (Release: null, Version: ContentVersion) : DetachCapability(true);
            _cachedSource = null;
            previous = Revision;
            current = _frames.Invalidate();
        }
        CompleteRetirement(retirement);
        // A slow retiring reader must not invalidate consumers of a replacement installed meanwhile.
        if (_frames.IsCurrent(current) && ContentVersion == retirement.Version)
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
        RetireCapability(true);
        ParameterRecord = null;
        _source = null;
        _cachedSource = null;
        Changed = null;
    }
}
