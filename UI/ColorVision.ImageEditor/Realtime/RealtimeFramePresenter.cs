using ColorVision.Core;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.ImageEditor.Realtime
{
    public sealed class RealtimeFrameRenderedEventArgs : EventArgs
    {
        public RealtimeFrameRenderedEventArgs(int width, int height, PixelFormat pixelFormat)
        {
            Width = width;
            Height = height;
            PixelFormat = pixelFormat;
        }

        public int Width { get; }
        public int Height { get; }
        public PixelFormat PixelFormat { get; }
    }

    public sealed class RealtimeFramePresenter : IDisposable
    {
        private sealed class FrameBuffer : IDisposable
        {
            public IntPtr Buffer;
            public int Capacity;
            public int Length;
            public int Width;
            public int Height;
            public int Stride;
            public PixelFormat PixelFormat;
            public RealtimeFrameTransform Transform;
            public DateTime TimestampUtc;

            public void EnsureCapacity(int requiredLength)
            {
                if (requiredLength <= Capacity) return;
                if (Buffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(Buffer);
                }
                Buffer = Marshal.AllocHGlobal(requiredLength);
                Capacity = requiredLength;
            }

            public void Dispose()
            {
                if (Buffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(Buffer);
                    Buffer = IntPtr.Zero;
                }
                Capacity = 0;
                Length = 0;
            }
        }

        private readonly ImageView _imageView;
        private readonly object _gate = new();
        private FrameBuffer? _pendingFrame;
        private FrameBuffer? _displayFrame;
        private bool _hasPendingFrame;
        private bool _renderScheduled;
        private bool _isDisposed;
        private bool _hasRenderedFrame;
        private long _lastAcceptedTimestamp;
        private WriteableBitmap? _writeableBitmap;

        public RealtimeFramePresenter(ImageView imageView, RealtimeFrameOptions? options = null)
        {
            _imageView = imageView ?? throw new ArgumentNullException(nameof(imageView));
            Options = options ?? new RealtimeFrameOptions();
        }

        public RealtimeFrameOptions Options { get; private set; }
        public RealtimeFrameStats Stats { get; } = new();
        public event EventHandler<RealtimeFrameRenderedEventArgs>? FrameRendered;

        public void Configure(RealtimeFrameOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            Options.ApplyFrom(options);
            Stats.Refresh(Options.IsFrozen);
        }

        public unsafe bool SubmitFrame(HImage frame)
        {
            if (frame.pData == IntPtr.Zero) return false;
            PixelFormat pixelFormat = GetPixelFormat(frame.channels, frame.depth);
            int stride = frame.stride > 0 ? frame.stride : GetDefaultStride(frame.cols, pixelFormat);
            int length = stride * frame.rows;
            return SubmitFrame(frame.pData, frame.cols, frame.rows, pixelFormat, stride, length);
        }

        public unsafe bool SubmitFrame(byte[] sourceBuffer, int width, int height, PixelFormat pixelFormat, int sourceStride = 0, int bufferLength = 0, RealtimeFrameTransform transform = RealtimeFrameTransform.None)
        {
            if (sourceBuffer == null) throw new ArgumentNullException(nameof(sourceBuffer));
            if (sourceBuffer.Length == 0) return false;
            bufferLength = bufferLength > 0 ? bufferLength : sourceBuffer.Length;
            if (bufferLength > sourceBuffer.Length) return false;
            if (!TryNormalizeLayout(width, height, pixelFormat, ref sourceStride, ref bufferLength))
            {
                return false;
            }

            fixed (byte* source = sourceBuffer)
            {
                return SubmitFrame((IntPtr)source, width, height, pixelFormat, sourceStride, bufferLength, transform);
            }
        }

        public unsafe bool SubmitFrame(IntPtr sourcePointer, int width, int height, PixelFormat pixelFormat, int sourceStride = 0, int bufferLength = 0, RealtimeFrameTransform transform = RealtimeFrameTransform.None)
        {
            if (_isDisposed || sourcePointer == IntPtr.Zero || width <= 0 || height <= 0) return false;
            if (!TryNormalizeLayout(width, height, pixelFormat, ref sourceStride, ref bufferLength))
            {
                return false;
            }

            Stats.RecordSubmitted();

            if (Options.IsFrozen)
            {
                Stats.RecordDropped(becauseFrozen: true);
                return false;
            }

            if (!ShouldAcceptFrame())
            {
                Stats.RecordDropped();
                return false;
            }

            lock (_gate)
            {
                _pendingFrame ??= new FrameBuffer();
                _pendingFrame.EnsureCapacity(bufferLength);
                Buffer.MemoryCopy((void*)sourcePointer, (void*)_pendingFrame.Buffer, _pendingFrame.Capacity, bufferLength);
                _pendingFrame.Width = width;
                _pendingFrame.Height = height;
                _pendingFrame.Stride = sourceStride;
                _pendingFrame.Length = bufferLength;
                _pendingFrame.PixelFormat = pixelFormat;
                _pendingFrame.Transform = transform;
                _pendingFrame.TimestampUtc = DateTime.UtcNow;
                _hasPendingFrame = true;
            }

            Stats.RecordAccepted();
            ScheduleRender();
            return true;
        }

        public void Reset(bool clearImageSource = false)
        {
            lock (_gate)
            {
                _hasPendingFrame = false;
                _hasRenderedFrame = false;
                _lastAcceptedTimestamp = 0;
                _pendingFrame?.Dispose();
                _displayFrame?.Dispose();
                _pendingFrame = null;
                _displayFrame = null;
            }

            WriteableBitmap? previousBitmap = _writeableBitmap;
            _writeableBitmap = null;
            Stats.Reset(Options.IsFrozen);

            if (clearImageSource)
            {
                _imageView.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_imageView.ImageShow.Source == previousBitmap)
                    {
                        _imageView.ImageShow.Source = null;
                    }
                }));
            }
        }

        private bool ShouldAcceptFrame()
        {
            int maxDisplayFps = Options.MaxDisplayFps;
            if (maxDisplayFps <= 0)
            {
                return true;
            }

            long now = Stopwatch.GetTimestamp();
            long minTicks = Stopwatch.Frequency / maxDisplayFps;
            long last = Interlocked.Read(ref _lastAcceptedTimestamp);
            if (last != 0 && now - last < minTicks)
            {
                return false;
            }

            Interlocked.Exchange(ref _lastAcceptedTimestamp, now);
            return true;
        }

        private void ScheduleRender()
        {
            bool shouldSchedule = false;
            lock (_gate)
            {
                if (!_renderScheduled && !_isDisposed)
                {
                    _renderScheduled = true;
                    shouldSchedule = true;
                }
            }

            if (shouldSchedule)
            {
                _imageView.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(RenderLatestFrame));
            }
        }

        private void RenderLatestFrame()
        {
            FrameBuffer? frame;
            lock (_gate)
            {
                if (_isDisposed || !_hasPendingFrame || _pendingFrame == null)
                {
                    _renderScheduled = false;
                    return;
                }

                (_pendingFrame, _displayFrame) = (_displayFrame, _pendingFrame);
                _hasPendingFrame = false;
                frame = _displayFrame;
            }

            if (frame != null)
            {
                RenderFrame(frame);
            }

            bool shouldContinue;
            lock (_gate)
            {
                shouldContinue = _hasPendingFrame && !_isDisposed;
                if (!shouldContinue)
                {
                    _renderScheduled = false;
                }
            }

            if (shouldContinue)
            {
                _imageView.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(RenderLatestFrame));
            }
        }

        private void RenderFrame(FrameBuffer frame)
        {
            if (frame.Length < GetRequiredBufferSize(frame.Width, frame.Height, frame.PixelFormat, frame.Stride))
            {
                return;
            }

            if (_writeableBitmap == null
                || _writeableBitmap.PixelWidth != frame.Width
                || _writeableBitmap.PixelHeight != frame.Height
                || _writeableBitmap.Format != frame.PixelFormat)
            {
                _writeableBitmap = new WriteableBitmap(frame.Width, frame.Height, 96, 96, frame.PixelFormat, null);
                _imageView.ViewBitmapSource = _writeableBitmap;
                _imageView.FunctionImage = null;
                _imageView.ImageShow.Source = _writeableBitmap;
                UpdateImageMetadata(frame);

                if (Options.AutoZoomOnFirstFrame || !_hasRenderedFrame)
                {
                    _imageView.UpdateZoomAndScale();
                }
            }
            else if (_imageView.ImageShow.Source != _writeableBitmap)
            {
                _imageView.ImageShow.Source = _writeableBitmap;
            }

            try
            {
                if (frame.Transform == RealtimeFrameTransform.None)
                {
                    _writeableBitmap.WritePixels(
                        new Int32Rect(0, 0, frame.Width, frame.Height),
                        frame.Buffer,
                        frame.Length,
                        frame.Stride);
                }
                else if (!WriteTransformedPixels(frame))
                {
                    return;
                }
            }
            catch (ArgumentException)
            {
                return;
            }

            _hasRenderedFrame = true;

            FrameRendered?.Invoke(this, new RealtimeFrameRenderedEventArgs(frame.Width, frame.Height, frame.PixelFormat));
            _imageView.SchedulePixelValueOverlayRefresh();
            Stats.RecordDisplayed(frame.TimestampUtc, Options.IsFrozen);
        }

        private unsafe bool WriteTransformedPixels(FrameBuffer frame)
        {
            if (_writeableBitmap == null || frame.PixelFormat.BitsPerPixel % 8 != 0)
            {
                return false;
            }

            int pixelBytes = frame.PixelFormat.BitsPerPixel / 8;
            int rowBytes = GetDefaultStride(frame.Width, frame.PixelFormat);
            if (pixelBytes <= 0 || rowBytes <= 0)
            {
                return false;
            }

            bool flipX = frame.Transform is RealtimeFrameTransform.FlipX or RealtimeFrameTransform.FlipXY;
            bool flipY = frame.Transform is RealtimeFrameTransform.FlipY or RealtimeFrameTransform.FlipXY;

            _writeableBitmap.Lock();
            try
            {
                byte* sourceBase = (byte*)frame.Buffer;
                byte* targetBase = (byte*)_writeableBitmap.BackBuffer;
                int targetStride = _writeableBitmap.BackBufferStride;

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
                        byte* sourcePixel = sourceRow + (frame.Width - 1 - x) * pixelBytes;
                        byte* targetPixel = targetRow + x * pixelBytes;
                        Buffer.MemoryCopy(sourcePixel, targetPixel, pixelBytes, pixelBytes);
                    }
                }

                _writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, frame.Width, frame.Height));
                return true;
            }
            finally
            {
                _writeableBitmap.Unlock();
            }
        }

        public RealtimeFrameSnapshot? CaptureCurrentFrame()
        {
            lock (_gate)
            {
                if (_displayFrame == null || _displayFrame.Buffer == IntPtr.Zero || _displayFrame.Length <= 0)
                {
                    return null;
                }

                byte[] data = new byte[_displayFrame.Length];
                Marshal.Copy(_displayFrame.Buffer, data, 0, data.Length);
                return new RealtimeFrameSnapshot(
                    data,
                    _displayFrame.Width,
                    _displayFrame.Height,
                    _displayFrame.Stride,
                    _displayFrame.PixelFormat,
                    _displayFrame.TimestampUtc);
            }
        }

        private void UpdateImageMetadata(FrameBuffer frame)
        {
            if (!Options.UpdateImageMetadata) return;
            int channels = GetChannelCount(frame.PixelFormat);
            int depth = channels > 0 ? frame.PixelFormat.BitsPerPixel / channels : frame.PixelFormat.BitsPerPixel;
            _imageView.Config.SetImageMetadata(ImageViewPropertyKeys.PixelFormat, frame.PixelFormat, nameof(RealtimeFramePresenter), "实时图像像素格式");
            _imageView.Config.SetImageMetadata(ImageViewPropertyKeys.Cols, frame.Width, nameof(RealtimeFramePresenter), "实时图像列数");
            _imageView.Config.SetImageMetadata(ImageViewPropertyKeys.Rows, frame.Height, nameof(RealtimeFramePresenter), "实时图像行数");
            _imageView.Config.SetImageMetadata(ImageViewPropertyKeys.Channel, channels, nameof(RealtimeFramePresenter), "实时图像通道数");
            _imageView.Config.SetImageMetadata(ImageViewPropertyKeys.Depth, depth, nameof(RealtimeFramePresenter), "实时图像位深");
            _imageView.Config.SetImageMetadata(ImageViewPropertyKeys.Stride, frame.Stride, nameof(RealtimeFramePresenter), "实时图像 stride");
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

            if (sourceStride < rowBytes)
            {
                sourceStride = rowBytes;
            }

            int requiredBufferSize = GetRequiredBufferSize(width, height, pixelFormat, sourceStride);
            if (requiredBufferSize <= 0) return false;

            if (bufferLength <= 0)
            {
                bufferLength = requiredBufferSize;
            }

            return bufferLength >= requiredBufferSize;
        }

        private static int GetRequiredBufferSize(int width, int height, PixelFormat pixelFormat, int stride)
        {
            try
            {
                checked
                {
                    int rowBytes = GetDefaultStride(width, pixelFormat);
                    return stride * (height - 1) + rowBytes;
                }
            }
            catch (OverflowException)
            {
                return -1;
            }
        }

        public static PixelFormat GetPixelFormat(int channels, int bpp)
        {
            if (channels == 4)
            {
                return PixelFormats.Bgra32;
            }

            if (channels == 3)
            {
                return bpp == 16 ? PixelFormats.Rgb48 : PixelFormats.Bgr24;
            }

            return bpp == 16 ? PixelFormats.Gray16 : PixelFormats.Gray8;
        }

        public static int GetChannelCount(PixelFormat pixelFormat)
        {
            if (pixelFormat == PixelFormats.Gray8 || pixelFormat == PixelFormats.Gray16 || pixelFormat == PixelFormats.Gray32Float)
            {
                return 1;
            }

            if (pixelFormat == PixelFormats.Bgr24 || pixelFormat == PixelFormats.Rgb24 || pixelFormat == PixelFormats.Rgb48)
            {
                return 3;
            }

            if (pixelFormat == PixelFormats.Bgr32 || pixelFormat == PixelFormats.Bgra32 || pixelFormat == PixelFormats.Pbgra32)
            {
                return 4;
            }

            return 0;
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_isDisposed) return;
                _isDisposed = true;
                _hasPendingFrame = false;
                _renderScheduled = false;
            }

            _pendingFrame?.Dispose();
            _displayFrame?.Dispose();
            _pendingFrame = null;
            _displayFrame = null;
            _writeableBitmap = null;
        }
    }
}
