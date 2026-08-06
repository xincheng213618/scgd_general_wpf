using System;
using System.Threading;

namespace ColorVision.Core
{
    /// <summary>
    /// Owns one source-image buffer and keeps it alive until the last reader lease is released.
    /// </summary>
    internal sealed class SourceImageFrame : IDisposable
    {
        private readonly SharedImageStorage _storage;
        private int _disposed;

        internal SourceImageFrame(HImage image, long revision, Action<HImage> releaseImage)
        {
            ValidateOwnedImage(image);
            ArgumentNullException.ThrowIfNull(releaseImage);
            _storage = new SharedImageStorage(image, releaseImage);
            Revision = revision;
        }

        private static void ValidateOwnedImage(HImage image)
        {
            if (image.pData == IntPtr.Zero)
            {
                throw new ArgumentException("The image buffer cannot be empty.", nameof(image));
            }
            if (image.isDispose)
            {
                throw new ArgumentException("A source frame must own its image buffer.", nameof(image));
            }
            if (image.rows <= 0 || image.cols <= 0 || image.channels <= 0 || image.depth <= 0 || image.depth % 8 != 0 || image.stride < 0)
            {
                throw new ArgumentException("The image layout is invalid.", nameof(image));
            }
        }

        internal long Revision { get; }

        public ImageFrameLease Acquire()
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            _storage.AddReference();
            if (Volatile.Read(ref _disposed) != 0)
            {
                _storage.ReleaseReference();
                throw new ObjectDisposedException(nameof(SourceImageFrame));
            }
            return new ImageFrameLease(_storage, Revision);
        }

        public bool TryAcquire(out ImageFrameLease? lease)
        {
            lease = null;
            if (Volatile.Read(ref _disposed) != 0)
            {
                return false;
            }

            try
            {
                _storage.AddReference();
                if (Volatile.Read(ref _disposed) != 0)
                {
                    _storage.ReleaseReference();
                    return false;
                }
                lease = new ImageFrameLease(_storage, Revision);
                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _storage.ReleaseReference();
            }
        }

        internal sealed class SharedImageStorage : IDisposable
        {
            private readonly int _rows;
            private readonly int _cols;
            private readonly int _channels;
            private readonly int _depth;
            private readonly int _stride;
            private readonly bool _isDispose;
            private readonly Action<HImage> _releaseImage;
            private int _referenceCount = 1;
            private IntPtr _pointer;

            internal SharedImageStorage(HImage image, Action<HImage> releaseImage)
            {
                _rows = image.rows;
                _cols = image.cols;
                _channels = image.channels;
                _depth = image.depth;
                _stride = image.stride;
                _isDispose = image.isDispose;
                _pointer = image.pData;
                _releaseImage = releaseImage;
            }

            ~SharedImageStorage()
            {
                try
                {
                    ReleaseBuffer();
                }
                catch
                {
                    // Finalizers must not surface cleanup failures.
                }
            }

            internal HImage GetBorrowedImage()
            {
                IntPtr pointer = Volatile.Read(ref _pointer);
                ObjectDisposedException.ThrowIf(pointer == IntPtr.Zero, nameof(SourceImageFrame));
                return new HImage
                {
                    rows = _rows,
                    cols = _cols,
                    channels = _channels,
                    depth = _depth,
                    stride = _stride,
                    isDispose = true,
                    pData = pointer,
                };
            }

            internal void AddReference()
            {
                while (true)
                {
                    int current = Volatile.Read(ref _referenceCount);
                    ObjectDisposedException.ThrowIf(current <= 0, nameof(SourceImageFrame));
                    if (Interlocked.CompareExchange(ref _referenceCount, current + 1, current) == current)
                    {
                        return;
                    }
                }
            }

            internal void ReleaseReference()
            {
                int remaining = Interlocked.Decrement(ref _referenceCount);
                if (remaining == 0)
                {
                    Dispose();
                }
                ObjectDisposedException.ThrowIf(remaining < 0, nameof(SourceImageFrame));
            }

            public void Dispose()
            {
                try
                {
                    ReleaseBuffer();
                }
                finally
                {
                    GC.SuppressFinalize(this);
                }
            }

            private void ReleaseBuffer()
            {
                IntPtr pointer = Interlocked.Exchange(ref _pointer, IntPtr.Zero);
                if (pointer == IntPtr.Zero)
                {
                    return;
                }

                HImage image = new()
                {
                    rows = _rows,
                    cols = _cols,
                    channels = _channels,
                    depth = _depth,
                    stride = _stride,
                    isDispose = _isDispose,
                    pData = pointer,
                };
                _releaseImage(image);
            }
        }
    }

    /// <summary>
    /// Pins a source-image buffer for one reader. The returned HImage is a borrowed view.
    /// </summary>
    public sealed class ImageFrameLease : IDisposable
    {
        private SourceImageFrame.SharedImageStorage? _storage;

        internal ImageFrameLease(SourceImageFrame.SharedImageStorage storage, long revision)
        {
            _storage = storage;
            Revision = revision;
        }

        public long Revision { get; }

        public HImage Image => GetStorage().GetBorrowedImage();

        public int Width => Image.cols;

        public int Height => Image.rows;

        public void Dispose()
        {
            Interlocked.Exchange(ref _storage, null)?.ReleaseReference();
            GC.SuppressFinalize(this);
        }

        ~ImageFrameLease()
        {
            try
            {
                Dispose();
            }
            catch
            {
                // Finalizers must not surface cleanup failures.
            }
        }

        private SourceImageFrame.SharedImageStorage GetStorage()
        {
            return Volatile.Read(ref _storage) ?? throw new ObjectDisposedException(nameof(ImageFrameLease));
        }
    }

    /// <summary>
    /// Atomically publishes the current source frame and its monotonically increasing revision.
    /// </summary>
    internal sealed class ImageFrameStore : IDisposable
    {
        private readonly object _sync = new();
        private SourceImageFrame? _current;
        private long _revision;
        private int _disposed;

        public long Revision => Volatile.Read(ref _revision);

        public bool IsCurrent(long revision)
        {
            lock (_sync)
            {
                return _disposed == 0 && revision == _revision;
            }
        }

        public ImageFrameLease? AcquireOrCreate(Func<HImage?> imageFactory)
        {
            return AcquireOrCreate(imageFactory, static ownedImage => ownedImage.Dispose());
        }

        internal ImageFrameLease? AcquireOrCreate(Func<HImage?> imageFactory, Action<HImage> releaseImage)
        {
            ArgumentNullException.ThrowIfNull(imageFactory);
            ArgumentNullException.ThrowIfNull(releaseImage);
            long revision;
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed != 0, this);
                if (_current != null && _current.TryAcquire(out ImageFrameLease? existingLease))
                {
                    return existingLease;
                }
                revision = _revision;
            }

            HImage? image = imageFactory();
            if (!image.HasValue || image.Value.pData == IntPtr.Zero)
            {
                return null;
            }

            HImage ownedImage = image.Value;
            bool ownershipTransferred = false;
            try
            {
                lock (_sync)
                {
                    ObjectDisposedException.ThrowIf(_disposed != 0, this);
                    if (_current != null && _current.TryAcquire(out ImageFrameLease? existingLease))
                    {
                        return existingLease;
                    }
                    if (_revision != revision)
                    {
                        return null;
                    }

                    _current = new SourceImageFrame(ownedImage, revision, releaseImage);
                    ownershipTransferred = true;
                    return _current.Acquire();
                }
            }
            finally
            {
                if (!ownershipTransferred)
                {
                    releaseImage(ownedImage);
                }
            }
        }

        public long Invalidate()
        {
            SourceImageFrame? previous;
            long revision;
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed != 0, this);
                revision = Interlocked.Increment(ref _revision);
                previous = _current;
                _current = null;
            }

            previous?.Dispose();
            return revision;
        }

        public void Dispose()
        {
            SourceImageFrame? previous;
            lock (_sync)
            {
                if (_disposed != 0)
                {
                    return;
                }

                _disposed = 1;
                Interlocked.Increment(ref _revision);
                previous = _current;
                _current = null;
            }

            previous?.Dispose();
        }
    }
}
