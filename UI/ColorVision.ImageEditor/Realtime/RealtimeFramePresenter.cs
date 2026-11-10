#pragma warning disable CA1510,CS8625
using ColorVision.Core;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.Presentation;
using log4net;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.ImageEditor.Realtime
{
    public sealed class RealtimeFramePresenter : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(RealtimeFramePresenter));
        public const int TransformNone = 0;
        public const int TransformFlipX = 1;
        public const int TransformFlipY = 2;
        public const int TransformFlipXY = TransformFlipX | TransformFlipY;

        private readonly record struct FrameInfo(
            int Width,
            int Height,
            int Stride,
            int Length,
            PixelFormat Format,
            int Transform,
            CancellationToken CancellationToken = default,
            bool SourceIsBgr48 = false);

        private readonly ImageProcessingContext _context;
        private readonly Action _refreshPixelOverlay;
        private Guid _sourceId = Guid.NewGuid();
        private readonly object _gate = new();
        private readonly object _submitGate = new();
        private byte[]? _latestPixels;
        private byte[]? _drawingPixels;
        private FrameInfo _latestFrame;
        private bool _hasLatestFrame;
        private bool _renderQueued;
        private bool _disposed;
        private bool _hasRenderedFrame;
        private Task _completion = Task.CompletedTask;
        private WriteableBitmap? _lastPresentedSource;
        private ImageSource? _lastPresentedDisplay;
        private long _lastPresentedRevision;

        public RealtimeFramePresenter(ImageView imageView, RealtimeFrameOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(imageView);
            _context = imageView.EditorContext.ProcessingContext;
            _refreshPixelOverlay = imageView.SchedulePixelValueOverlayRefresh;
            Options = options ?? new RealtimeFrameOptions();
            _context.StreamPresentation.FramePresented += OnFramePresented;
        }

        public RealtimeFrameOptions Options { get; private set; }

        /// <summary>Raised on the dispatcher when a new frame from this presenter reaches the display.</summary>
        public event EventHandler? FrameDisplayed;

        internal Task Completion { get { lock (_gate) return _completion; } }

        public void Configure(RealtimeFrameOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            Options.ApplyFrom(options);
        }

        public bool SubmitFrame(HImage frame)
        {
            if (frame.pData == IntPtr.Zero) return false;

            PixelFormat format = GetPixelFormat(frame.channels, frame.depth);
            int stride = frame.stride > 0 ? frame.stride : GetDefaultStride(frame.cols, format);
            return SubmitFrame(frame.pData, frame.cols, frame.rows, format, stride, stride * frame.rows);
        }

        public bool SubmitFrame(byte[] sourceBuffer, int width, int height, PixelFormat pixelFormat, int sourceStride = 0, int bufferLength = 0, int transform = TransformNone)
            => SubmitFrame(sourceBuffer, width, height, pixelFormat, sourceStride, bufferLength, transform, CancellationToken.None);

        public unsafe bool SubmitFrame(byte[] sourceBuffer, int width, int height, PixelFormat pixelFormat, int sourceStride, int bufferLength, int transform, CancellationToken cancellationToken)
        {
            if (sourceBuffer == null) throw new ArgumentNullException(nameof(sourceBuffer));
            if (sourceBuffer.Length == 0) return false;

            bufferLength = bufferLength > 0 ? bufferLength : sourceBuffer.Length;
            if (bufferLength > sourceBuffer.Length) return false;
            fixed (byte* source = sourceBuffer)
                return SubmitFrame((IntPtr)source, width, height, pixelFormat, sourceStride, bufferLength, transform, cancellationToken);
        }

        public bool SubmitFrame(IntPtr sourcePointer, int width, int height, PixelFormat pixelFormat, int sourceStride = 0, int bufferLength = 0, int transform = TransformNone)
            => SubmitFrame(sourcePointer, width, height, pixelFormat, sourceStride, bufferLength, transform, CancellationToken.None);

        /// <summary>Copies the borrowed pixels synchronously and discards the frame if its producer session is canceled before publication.</summary>
        public bool SubmitFrame(IntPtr sourcePointer, int width, int height, PixelFormat pixelFormat, int sourceStride, int bufferLength, int transform, CancellationToken cancellationToken)
            => SubmitFrame(sourcePointer, width, height, pixelFormat, sourceStride, bufferLength, transform, cancellationToken, sourceIsBgr48: false);

        /// <summary>For an Rgb48 bitmap, accepts packed BGR16 samples and reorders them while writing the display snapshot.</summary>
        public bool SubmitFrame(IntPtr sourcePointer, int width, int height, PixelFormat pixelFormat, int sourceStride, int bufferLength, int transform,
            CancellationToken cancellationToken, bool sourceIsBgr48)
        {
            if (sourcePointer == IntPtr.Zero || cancellationToken.IsCancellationRequested
                || (sourceIsBgr48 && pixelFormat != PixelFormats.Rgb48)) return false;
            if (!CanAcceptFrame(width, height, pixelFormat, ref sourceStride, ref bufferLength)) return false;

            Guid sourceId;
            lock (_gate)
            {
                if (_disposed) return false;
                sourceId = _sourceId;
            }

            // The camera owns sourcePointer only until this call returns. Copy synchronously,
            // but never hold the state gate during a full-frame copy: Reset runs on the UI.
            lock (_submitGate)
            {
                byte[]? pixels;
                lock (_gate)
                {
                    if (_disposed || sourceId != _sourceId || cancellationToken.IsCancellationRequested) return false;
                    pixels = _latestPixels;
                    _latestPixels = null;
                    _hasLatestFrame = false;
                }

                if (pixels == null || pixels.Length < bufferLength) pixels = new byte[bufferLength];
                Marshal.Copy(sourcePointer, pixels, 0, bufferLength);
                lock (_gate)
                {
                    if (_disposed || sourceId != _sourceId || cancellationToken.IsCancellationRequested) return false;
                    _latestPixels = pixels;
                    _latestFrame = new FrameInfo(width, height, sourceStride, bufferLength, pixelFormat, transform, cancellationToken, sourceIsBgr48);
                    _hasLatestFrame = true;
                    if (!_renderQueued)
                    {
                        _renderQueued = true;
                        _completion = Task.Run(RenderLatestFramesAsync, CancellationToken.None);
                    }
                }
            }
            return true;
        }

        public void Reset(bool clearImageSource = false)
        {
            if (!_context.Dispatcher.CheckAccess())
            {
                _context.Dispatcher.Invoke(() => Reset(clearImageSource));
                return;
            }
            bool ownsCurrentDisplay = _lastPresentedSource != null
                && _context.ImageRevision == _lastPresentedRevision
                && ReferenceEquals(_context.ViewBitmapSource, _lastPresentedSource)
                && ReferenceEquals(_context.Presentation.DisplaySource, _lastPresentedDisplay);
            _context.StreamPresentation.ResetSource(_sourceId);
            lock (_gate)
            {
                _sourceId = Guid.NewGuid();
                _hasLatestFrame = false;
                _hasRenderedFrame = false;
                _latestPixels = null;
                _drawingPixels = null;
                _latestFrame = default;
            }

            _lastPresentedSource = null;
            _lastPresentedDisplay = null;

            if (clearImageSource && ownsCurrentDisplay) _context.Presentation.Publish(null, null);
        }

        private bool CanAcceptFrame(int width, int height, PixelFormat format, ref int stride, ref int length)
        {
            return !_disposed && !Options.IsFrozen && TryNormalizeLayout(width, height, format, ref stride, ref length);
        }

        private async Task RenderLatestFramesAsync()
        {
            while (true)
            {
                byte[] pixels;
                FrameInfo frame;
                Guid sourceId;
                lock (_gate)
                {
                    if (_disposed || !_hasLatestFrame || _latestPixels == null)
                    {
                        _renderQueued = false;
                        return;
                    }
                    (_latestPixels, _drawingPixels) = (_drawingPixels, _latestPixels);
                    pixels = _drawingPixels!;
                    frame = _latestFrame;
                    _latestFrame = default;
                    sourceId = _sourceId;
                    _hasLatestFrame = false;
                }

                try
                {
                    if (frame.CancellationToken.IsCancellationRequested) continue;
                    // Allocation, copying, flipping and freezing all belong to this worker.
                    // StreamPresentation can consume the frozen bitmap without cloning it on UI.
                    (WriteableBitmap? bitmap, BitmapSource? renderProxy) = await Task.Run(() =>
                    {
                        WriteableBitmap? created = CreateFrameBitmap(pixels, frame);
                        return (created, created != null && _context.StreamPresentation.SourceIsDisplayed ? ImageRenderProxy.TryCreate(created) : (BitmapSource?)null);
                    }).ConfigureAwait(false);
                    if (bitmap == null || _context.Dispatcher.HasShutdownStarted) continue;
                    await _context.Dispatcher.InvokeAsync(() =>
                    {
                        bool CanPublish() => !frame.CancellationToken.IsCancellationRequested && !Options.IsFrozen && !_disposed && sourceId == _sourceId;
                        if (_context.IsDisposed || !CanPublish()) return;
                        _context.StreamPresentation.Submit(bitmap, renderProxy, sourceId, CanPublish, frame.CancellationToken);
                    }, DispatcherPriority.Background).Task.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Warn("Unable to present a realtime frame.", ex);
                }
            }
        }

        private static WriteableBitmap? CreateFrameBitmap(byte[] pixels, FrameInfo frame)
        {
            if (frame.Length < GetRequiredBufferSize(frame.Width, frame.Height, frame.Format, frame.Stride)) return null;
            WriteableBitmap bitmap = new(frame.Width, frame.Height, 96, 96, frame.Format, null);

            try
            {
                if (frame.Transform == TransformNone && !frame.SourceIsBgr48)
                {
                    bitmap.WritePixels(new Int32Rect(0, 0, frame.Width, frame.Height), pixels, frame.Stride, 0);
                }
                else if (!WriteTransformedPixels(bitmap, pixels, frame))
                {
                    return null;
                }
            }
            catch (ArgumentException)
            {
                return null;
            }

            bitmap.Freeze();
            return bitmap;
        }

        private void OnFramePresented(Guid sourceId, WriteableBitmap source)
        {
            if (_disposed || sourceId != _sourceId || !ReferenceEquals(_context.ViewBitmapSource, source)) return;
            bool newFrame = !ReferenceEquals(_lastPresentedSource, source);
            bool geometryChanged = !_hasRenderedFrame || _lastPresentedSource == null
                || _lastPresentedSource.Width != source.Width || _lastPresentedSource.Height != source.Height;
            _lastPresentedSource = source;
            _lastPresentedDisplay = _context.Presentation.DisplaySource;
            _lastPresentedRevision = _context.ImageRevision;
            UpdateImageMetadata(new FrameInfo(source.PixelWidth, source.PixelHeight,
                source.BackBufferStride, 0, source.Format, TransformNone));
            if (Options.AutoZoomOnFirstFrame && geometryChanged) _context.UpdateZoomAndScale();
            _hasRenderedFrame = true;
            _refreshPixelOverlay();
            // Applying display effects again can republish the same acquired frame.
            if (newFrame && _lastPresentedDisplay != null) FrameDisplayed?.Invoke(this, EventArgs.Empty);
        }

        private static unsafe bool WriteTransformedPixels(WriteableBitmap bitmap, byte[] pixels, FrameInfo frame)
        {
            if (frame.Format.BitsPerPixel % 8 != 0) return false;

            int pixelBytes = frame.Format.BitsPerPixel / 8;
            int rowBytes = GetDefaultStride(frame.Width, frame.Format);
            bool flipX = (frame.Transform & TransformFlipX) != 0;
            bool flipY = (frame.Transform & TransformFlipY) != 0;

            fixed (byte* sourceBase = pixels)
            {
                bitmap.Lock();
                try
                {
                    byte* targetBase = (byte*)bitmap.BackBuffer;
                    int targetStride = bitmap.BackBufferStride;

                    if (frame.SourceIsBgr48 && !flipY)
                    {
                        byte* firstRow = sourceBase + (flipX ? (long)(frame.Height - 1) * frame.Stride : 0);
                        ImageAlgorithmInputFactory.CopyColor16(firstRow, flipX ? -frame.Stride : frame.Stride,
                            targetBase, targetStride, frame.Width, frame.Height, pixelBytes);
                    }
                    else
                    {
                        for (int y = 0; y < frame.Height; y++)
                        {
                            int sourceY = flipX ? frame.Height - 1 - y : y;
                            byte* sourceRow = sourceBase + sourceY * frame.Stride;
                            byte* targetRow = targetBase + y * targetStride;

                            if (!flipY)
                            {
                                Buffer.MemoryCopy(sourceRow, targetRow, targetStride, rowBytes);
                                continue;
                            }

                            for (int x = 0; x < frame.Width; x++)
                            {
                                byte* source = sourceRow + (frame.Width - 1 - x) * pixelBytes;
                                byte* target = targetRow + x * pixelBytes;
                                if (frame.SourceIsBgr48)
                                {
                                    target[0] = source[4];
                                    target[1] = source[5];
                                    target[2] = source[2];
                                    target[3] = source[3];
                                    target[4] = source[0];
                                    target[5] = source[1];
                                }
                                else Buffer.MemoryCopy(source, target, pixelBytes, pixelBytes);
                            }
                        }
                    }

                    bitmap.AddDirtyRect(new Int32Rect(0, 0, frame.Width, frame.Height));
                    return true;
                }
                finally
                {
                    bitmap.Unlock();
                }
            }
        }

        private void UpdateImageMetadata(FrameInfo frame)
        {
            if (!Options.UpdateImageMetadata) return;

            int channels = GetChannelCount(frame.Format);
            int depth = channels > 0 ? frame.Format.BitsPerPixel / channels : frame.Format.BitsPerPixel;
            _context.Config.SetImageMetadata(ImageViewPropertyKeys.PixelFormat, frame.Format, nameof(RealtimeFramePresenter), "实时图像像素格式");
            _context.Config.SetImageMetadata(ImageViewPropertyKeys.Cols, frame.Width, nameof(RealtimeFramePresenter), "实时图像列数");
            _context.Config.SetImageMetadata(ImageViewPropertyKeys.Rows, frame.Height, nameof(RealtimeFramePresenter), "实时图像行数");
            _context.Config.SetImageMetadata(ImageViewPropertyKeys.Channel, channels, nameof(RealtimeFramePresenter), "实时图像通道数");
            _context.Config.SetImageMetadata(ImageViewPropertyKeys.Depth, depth, nameof(RealtimeFramePresenter), "实时图像位深");
            _context.Config.SetImageMetadata(ImageViewPropertyKeys.Stride, frame.Stride, nameof(RealtimeFramePresenter), "实时图像 stride");
        }

        public static int GetDefaultStride(int width, PixelFormat pixelFormat)
        {
            return (width * pixelFormat.BitsPerPixel + 7) / 8;
        }

        private static bool TryNormalizeLayout(int width, int height, PixelFormat pixelFormat, ref int sourceStride, ref int bufferLength)
        {
            if (width <= 0 || height <= 0) return false;

            int rowBytes = GetDefaultStride(width, pixelFormat);
            if (rowBytes <= 0) return false;

            if (sourceStride < rowBytes) sourceStride = rowBytes;

            int requiredBufferSize = GetRequiredBufferSize(width, height, pixelFormat, sourceStride);
            if (requiredBufferSize <= 0) return false;

            if (bufferLength <= 0) bufferLength = requiredBufferSize;

            return bufferLength >= requiredBufferSize;
        }

        private static int GetRequiredBufferSize(int width, int height, PixelFormat pixelFormat, int stride)
        {
            try
            {
                checked
                {
                    return stride * (height - 1) + GetDefaultStride(width, pixelFormat);
                }
            }
            catch (OverflowException)
            {
                return -1;
            }
        }

        public static PixelFormat GetPixelFormat(int channels, int bpp)
        {
            if (channels == 4) return PixelFormats.Bgra32;
            if (channels == 3) return bpp == 16 ? PixelFormats.Rgb48 : PixelFormats.Bgr24;
            return bpp == 16 ? PixelFormats.Gray16 : PixelFormats.Gray8;
        }

        public static int GetChannelCount(PixelFormat pixelFormat)
        {
            if (pixelFormat == PixelFormats.Gray8 || pixelFormat == PixelFormats.Gray16 || pixelFormat == PixelFormats.Gray32Float) return 1;
            if (pixelFormat == PixelFormats.Bgr24 || pixelFormat == PixelFormats.Rgb24 || pixelFormat == PixelFormats.Rgb48) return 3;
            if (pixelFormat == PixelFormats.Bgr32 || pixelFormat == PixelFormats.Bgra32 || pixelFormat == PixelFormats.Pbgra32) return 4;
            return 0;
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _hasLatestFrame = false;
                _renderQueued = false;
                _latestPixels = null;
                _drawingPixels = null;
                _latestFrame = default;
            }
            void Detach()
            {
                FrameDisplayed = null;
                _lastPresentedSource = null;
                _lastPresentedDisplay = null;
                _context.StreamPresentation.FramePresented -= OnFramePresented;
                _context.StreamPresentation.ResetSource(_sourceId);
            }
            if (_context.Dispatcher.CheckAccess()) Detach();
            else if (!_context.Dispatcher.HasShutdownStarted) _context.Dispatcher.BeginInvoke(Detach);
        }
    }
}
