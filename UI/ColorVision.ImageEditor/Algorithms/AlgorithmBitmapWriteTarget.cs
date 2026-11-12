using ColorVision.Algorithms;
using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>Owns an unpublished UI bitmap and its pinned input until fill finishes or ownership transfers.</summary>
    internal sealed class AlgorithmBitmapWriteTarget : IAsyncDisposable
    {
        private readonly object _sync = new();
        private readonly Dispatcher _dispatcher;
        private readonly int _width, _height, _sourceStride, _destinationStride, _pixelBytes;
        private readonly AlgorithmImageFormat _format;
        private AlgorithmImageBufferLease? _source;
        private MemoryHandle _pin;
        private IntPtr _sourceAddress, _destinationAddress;
        private WriteableBitmap? _bitmap;
        private Task? _fill;
        private bool _locked, _releaseStarted;

        public unsafe AlgorithmBitmapWriteTarget(AlgorithmImageBuffer image)
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            _width = image.Width;
            _height = image.Height;
            _sourceStride = image.Stride;
            _format = image.Format;
            _pixelBytes = image.Format.BytesPerPixel();
            try
            {
                _source = image.AcquireReadOnlyLease();
                _bitmap = new WriteableBitmap(_width, _height, image.DpiX, image.DpiY, ImageAlgorithmInputFactory.ToPixelFormat(_format), null);
                _bitmap.Lock();
                _locked = true;
                _destinationStride = _bitmap.BackBufferStride;
                _destinationAddress = _bitmap.BackBuffer;
                _pin = _source.Data.Pin();
                _sourceAddress = (IntPtr)_pin.Pointer;
            }
            catch
            {
                try { ReleaseBitmap(); }
                finally { ReleaseInput(); }
                throw;
            }
        }

        public Task FillAsync(CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_releaseStarted || _bitmap == null, this);
                if (_fill != null) throw new InvalidOperationException("The bitmap target has already been filled.");
                return _fill = Task.Run(() => CopyPixels(cancellationToken), cancellationToken);
            }
        }

        public WriteableBitmap TakeBitmap(CancellationToken cancellationToken = default)
        {
            _dispatcher.VerifyAccess();
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_releaseStarted || _bitmap == null, this);
                if (_fill?.IsCompletedSuccessfully != true)
                    throw new InvalidOperationException("The bitmap fill has not completed successfully.");
                cancellationToken.ThrowIfCancellationRequested();
                _bitmap.AddDirtyRect(new Int32Rect(0, 0, _width, _height));
                _bitmap.Unlock();
                _locked = false;
                WriteableBitmap bitmap = _bitmap;
                _bitmap = null;
                _releaseStarted = true;
                ReleaseInput();
                return bitmap;
            }
        }

        public async ValueTask DisposeAsync()
        {
            Task? fill;
            lock (_sync)
            {
                _releaseStarted = true;
                fill = _fill;
            }
            try
            {
                // The fill task keeps its own failure/cancellation for its caller. Cleanup still
                // waits for all pointer writes to finish before releasing the lock or input pin.
                if (fill != null)
                {
                    try { await fill.ConfigureAwait(false); }
                    catch { }
                }
                bool needsUnlock;
                lock (_sync) needsUnlock = _locked;
                if (needsUnlock)
                {
                    if (_dispatcher.CheckAccess()) ReleaseBitmap();
                    else await _dispatcher.InvokeAsync(ReleaseBitmap);
                }
            }
            finally
            {
                lock (_sync)
                {
                    _bitmap = null;
                    _locked = false;
                    ReleaseInput();
                }
            }
        }

        private void ReleaseBitmap()
        {
            _dispatcher.VerifyAccess();
            lock (_sync)
            {
                try { if (_locked) _bitmap!.Unlock(); }
                finally { _locked = false; _bitmap = null; }
            }
        }

        private void ReleaseInput()
        {
            _pin.Dispose();
            _pin = default;
            _source?.Dispose();
            _source = null;
            _sourceAddress = IntPtr.Zero;
            _destinationAddress = IntPtr.Zero;
        }

        private unsafe void CopyPixels(CancellationToken cancellationToken)
        {
            byte* source = (byte*)_sourceAddress;
            byte* destination = (byte*)_destinationAddress;
            if (_format is AlgorithmImageFormat.Bgr48 or AlgorithmImageFormat.Bgra64)
                ImageAlgorithmInputFactory.CopyColor16(source, _sourceStride, destination, _destinationStride, _width, _height, _pixelBytes, cancellationToken);
            else
            {
                int rowBytes = checked(_width * _pixelBytes);
                for (int row = 0; row < _height; row++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Buffer.MemoryCopy(source + (long)row * _sourceStride, destination + (long)row * _destinationStride, rowBytes, rowBytes);
                }
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }
}
