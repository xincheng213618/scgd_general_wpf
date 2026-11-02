using ColorVision.Core;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Tif;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.ImageEditor.Documents;

/// <summary>Owns the loaded source and revision-safe pixel leases for one editor document.</summary>
public sealed class ImageDocument : IDisposable
{
    private const int FrameCopyBatchBytes = 4 * 1024 * 1024;
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

    internal void ReplaceCapability(IImageContentCapability? capability, out long installedContentVersion)
    {
        installedContentVersion = -1;
        (Action? Release, long Version) retirement;
        lock (_contentSync)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (ReferenceEquals(_capability, capability))
            {
                installedContentVersion = ContentVersion;
                return;
            }
            var previous = _capability;
            long version = Interlocked.Increment(ref _contentVersion);
            Volatile.Write(ref _capability, capability);
            // Ownership is established before either retirement phase can throw or reenter.
            installedContentVersion = version;
            retirement = (previous?.DetachForRetirement(version), version);
        }
        CompleteRetirement(retirement);
    }

    internal void RetireCapabilityIfCurrent(IImageContentCapability? capability, long installedContentVersion)
    {
        (Action? Release, long Version) retirement;
        lock (_contentSync)
        {
            if (!ReferenceEquals(_capability, capability) || ContentVersion != installedContentVersion) return;
            retirement = DetachCapability(clearContent: false);
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
    // commit in-place writes; source captures and lease acquisition also detect reference changes.
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

    internal (ImageSource? Source, long Revision) CaptureSourceState()
    {
        if (!_dispatcher.CheckAccess())
            return _dispatcher.Invoke(CaptureSourceState);

        while (true)
        {
            lock (_contentSync)
            {
                ObjectDisposedException.ThrowIf(IsDisposed, this);
                ImageSource? source = Source;
                if (_cachedSource == null || ReferenceEquals(_cachedSource, source))
                {
                    _cachedSource = source;
                    return (source, Revision);
                }
            }
            // Retirement callbacks can capture an intermediate source and then replace it.
            // Reconcile that binding too; never hold the content lock across those callbacks.
            Invalidate(ImageDocumentMutationKind.ImageSourceReplaced);
        }
    }

    internal ImageFrameLease? AcquireFrame()
    {
        if (!_dispatcher.CheckAccess())
            return _dispatcher.Invoke(AcquireFrame);

        ImageSource? source = CaptureSourceState().Source;
        ImageFrameLease? lease = _frames.AcquireOrCreate(
            () => source is WriteableBitmap bitmap ? bitmap.ToHImage() : null);
        lock (_contentSync)
        {
            // A worker may invalidate between source capture and native frame creation.
            // Only rebind the cache for a lease that still belongs to this source and revision.
            if (lease != null && _frames.IsCurrent(lease.Revision) && ReferenceEquals(Source, source))
                _cachedSource = source;
        }
        return lease;
    }

    internal async Task<ImageFrameLease?> AcquireFrameAsync(
        ImageSource expectedSource,
        long expectedRevision,
        Func<bool> canContinue,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expectedSource);
        ArgumentNullException.ThrowIfNull(canContinue);
        HImage candidate = default;
        bool candidateTransferred = false;
        ImageFrameLease? lease = null;
        WriteableBitmap? frozenSource = null;
        int copiedBytes = 0;
        int bufferSize = 0;

        bool IsScopeCurrent()
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsDisposed) return false;
            (ImageSource? source, long revision) = CaptureSourceState();
            cancellationToken.ThrowIfCancellationRequested();
            return !IsDisposed && revision == expectedRevision && ReferenceEquals(source, expectedSource)
                && _frames.IsCurrent(expectedRevision) && canContinue();
        }

        bool AdmitCandidate()
        {
            _dispatcher.VerifyAccess();
            if (!IsScopeCurrent()) return true;
            lease = _frames.AcquireOrCreate(() =>
            {
                if (!_frames.IsCurrent(expectedRevision)) return null;
                // The store owns this candidate even if it later loses to another frame.
                candidateTransferred = true;
                return candidate;
            });
            if (lease == null) return true;
            if (lease.Revision != expectedRevision || !IsScopeCurrent())
            {
                lease.Dispose();
                lease = null;
                return true;
            }
            lock (_contentSync)
            {
                if (_frames.IsCurrent(lease.Revision) && ReferenceEquals(Source, expectedSource))
                    _cachedSource = expectedSource;
            }
            return true;
        }

        bool CopyNextBatch()
        {
            _dispatcher.VerifyAccess();
            if (!IsScopeCurrent()) return true;
            lease = _frames.AcquireOrCreate(static () => null);
            if (lease != null)
            {
                if (lease.Revision == expectedRevision && IsScopeCurrent()) return true;
                lease.Dispose();
                lease = null;
                return true;
            }
            if (!IsScopeCurrent() || expectedSource is not WriteableBitmap bitmap) return true;
            if (bitmap.IsFrozen)
            {
                frozenSource = bitmap;
                return true;
            }

            bitmap.Lock();
            try
            {
                HImage source = bitmap.ForHImage();
                if (candidate.pData == IntPtr.Zero)
                {
                    int stride = checked(bitmap.PixelWidth * (bitmap.Format.BitsPerPixel / 8));
                    bufferSize = checked(bitmap.PixelHeight * stride);
                    candidate = source;
                    candidate.pData = IntPtr.Zero;
                    candidate.isDispose = false;
                    candidate.stride = stride;
                    candidate.pData = Marshal.AllocCoTaskMem(bufferSize);
                }
                if (source.rows != candidate.rows || source.cols != candidate.cols
                    || source.channels != candidate.channels || source.depth != candidate.depth || source.stride < candidate.stride)
                    throw new InvalidOperationException("The source frame layout changed during acquisition.");

                int count = Math.Min(FrameCopyBatchBytes, bufferSize - copiedBytes);
                CopyFrameBytes(source, candidate, copiedBytes, count);
                copiedBytes += count;
            }
            finally { bitmap.Unlock(); }

            // Unlock may notify subscribers. A successor wins before another batch or admission.
            if (!IsScopeCurrent()) return true;
            if (copiedBytes < bufferSize) return false;
            return AdmitCandidate();
        }

        try
        {
            _dispatcher.VerifyAccess();
            if (!IsScopeCurrent()) return null;
            lease = _frames.AcquireOrCreate(static () => null);
            if (lease != null)
            {
                if (lease.Revision != expectedRevision || !IsScopeCurrent()) return null;
                ImageFrameLease cached = lease;
                lease = null;
                return cached;
            }

            while (true)
            {
                // The first cold copy also yields; only one bounded UI batch is outstanding.
                bool complete = await _dispatcher.InvokeAsync(CopyNextBatch, DispatcherPriority.Background, cancellationToken)
                    .Task.ConfigureAwait(false);
                if (!complete) continue;
                if (frozenSource != null)
                {
                    // Free the partial mutable copy only after its UI batch has unlocked.
                    // A frozen source can then be copied independently on the worker.
                    candidate.Dispose();
                    candidate = default;
                    candidate = await Task.Run(frozenSource.ToHImage, cancellationToken).ConfigureAwait(false);
                    await _dispatcher.InvokeAsync(AdmitCandidate, DispatcherPriority.Background, cancellationToken)
                        .Task.ConfigureAwait(false);
                }
                ImageFrameLease? result = lease;
                lease = null;
                return result;
            }
        }
        catch (ObjectDisposedException) when (IsDisposed)
        {
            return null;
        }
        finally
        {
            lease?.Dispose();
            if (!candidateTransferred) candidate.Dispose();
        }
    }

    private static unsafe void CopyFrameBytes(HImage source, HImage destination, int byteOffset, int count)
    {
        if (source.stride == destination.stride)
        {
            Buffer.MemoryCopy((byte*)source.pData + byteOffset, (byte*)destination.pData + byteOffset, count, count);
            return;
        }
        while (count > 0)
        {
            int row = byteOffset / destination.stride;
            int column = byteOffset % destination.stride;
            int bytes = Math.Min(count, destination.stride - column);
            Buffer.MemoryCopy((byte*)source.pData + (long)row * source.stride + column,
                (byte*)destination.pData + byteOffset, bytes, bytes);
            byteOffset += bytes;
            count -= bytes;
        }
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
