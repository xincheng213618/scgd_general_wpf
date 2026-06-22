#pragma warning disable CA1510,CS8625
using ColorVision.Core;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.ImageEditor.Realtime
{
    public sealed class RealtimeFramePresenter : IDisposable
    {
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
            int Transform);

        private readonly ImageView _imageView;
        private readonly object _gate = new();
        private byte[]? _latestPixels;
        private byte[]? _drawingPixels;
        private FrameInfo _latestFrame;
        private bool _hasLatestFrame;
        private bool _renderQueued;
        private bool _disposed;
        private bool _hasRenderedFrame;
        private WriteableBitmap? _bitmap;

        public RealtimeFramePresenter(ImageView imageView, RealtimeFrameOptions? options = null)
        {
            _imageView = imageView ?? throw new ArgumentNullException(nameof(imageView));
            Options = options ?? new RealtimeFrameOptions();
        }

        public RealtimeFrameOptions Options { get; private set; }

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

        public unsafe bool SubmitFrame(byte[] sourceBuffer, int width, int height, PixelFormat pixelFormat, int sourceStride = 0, int bufferLength = 0, int transform = TransformNone)
        {
            if (sourceBuffer == null) throw new ArgumentNullException(nameof(sourceBuffer));
            if (sourceBuffer.Length == 0) return false;

            bufferLength = bufferLength > 0 ? bufferLength : sourceBuffer.Length;
            if (bufferLength > sourceBuffer.Length) return false;
            fixed (byte* source = sourceBuffer)
                return SubmitFrame((IntPtr)source, width, height, pixelFormat, sourceStride, bufferLength, transform);
        }

        public bool SubmitFrame(IntPtr sourcePointer, int width, int height, PixelFormat pixelFormat, int sourceStride = 0, int bufferLength = 0, int transform = TransformNone)
        {
            if (sourcePointer == IntPtr.Zero) return false;
            if (!CanAcceptFrame(width, height, pixelFormat, ref sourceStride, ref bufferLength)) return false;

            bool queueRender;
            lock (_gate)
            {
                if (_disposed) return false;
                if (_latestPixels == null || _latestPixels.Length < bufferLength) _latestPixels = new byte[bufferLength];

                Marshal.Copy(sourcePointer, _latestPixels, 0, bufferLength);
                SaveLatestFrame(width, height, pixelFormat, sourceStride, bufferLength, transform);
                queueRender = !_renderQueued;
                _renderQueued = true;
            }

            if (queueRender) _imageView.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(RenderLatestFrame));
            return true;
        }

        public void Reset(bool clearImageSource = false)
        {
            lock (_gate)
            {
                _hasLatestFrame = false;
                _hasRenderedFrame = false;
                _latestPixels = null;
                _drawingPixels = null;
            }

            WriteableBitmap? previousBitmap = _bitmap;
            _bitmap = null;

            if (clearImageSource)
            {
                _imageView.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_imageView.ImageShow.Source == previousBitmap) _imageView.ImageShow.Source = null;
                }));
            }
        }

        private bool CanAcceptFrame(int width, int height, PixelFormat format, ref int stride, ref int length)
        {
            return !_disposed && !Options.IsFrozen && TryNormalizeLayout(width, height, format, ref stride, ref length);
        }

        private void SaveLatestFrame(int width, int height, PixelFormat format, int stride, int length, int transform)
        {
            _latestFrame = new FrameInfo(width, height, stride, length, format, transform);
            _hasLatestFrame = true;
        }

        private void RenderLatestFrame()
        {
            byte[]? pixels;
            FrameInfo frame;

            lock (_gate)
            {
                if (_disposed || !_hasLatestFrame || _latestPixels == null)
                {
                    _renderQueued = false; return;
                }

                (_latestPixels, _drawingPixels) = (_drawingPixels, _latestPixels);
                pixels = _drawingPixels;
                frame = _latestFrame;
                _hasLatestFrame = false;
            }

            if (pixels != null) RenderFrame(pixels, frame);

            bool queueAgain;
            lock (_gate)
            {
                queueAgain = _hasLatestFrame && !_disposed;
                _renderQueued = queueAgain;
            }

            if (queueAgain) _imageView.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(RenderLatestFrame));
        }

        private void RenderFrame(byte[] pixels, FrameInfo frame)
        {
            if (frame.Length < GetRequiredBufferSize(frame.Width, frame.Height, frame.Format, frame.Stride)) return;

            EnsureBitmap(frame);

            try
            {
                if (frame.Transform == TransformNone)
                {
                    _bitmap!.WritePixels(new Int32Rect(0, 0, frame.Width, frame.Height), pixels, frame.Stride, 0);
                }
                else if (!WriteTransformedPixels(pixels, frame))
                {
                    return;
                }
            }
            catch (ArgumentException)
            {
                return;
            }

            _hasRenderedFrame = true;
            _imageView.SchedulePixelValueOverlayRefresh();
        }

        private void EnsureBitmap(FrameInfo frame)
        {
            if (_bitmap != null
                && _bitmap.PixelWidth == frame.Width
                && _bitmap.PixelHeight == frame.Height
                && _bitmap.Format == frame.Format)
            {
                if (_imageView.ImageShow.Source != _bitmap) _imageView.ImageShow.Source = _bitmap;
                return;
            }

            _bitmap = new WriteableBitmap(frame.Width, frame.Height, 96, 96, frame.Format, null);
            _imageView.ViewBitmapSource = _bitmap;
            _imageView.FunctionImage = null;
            _imageView.ImageShow.Source = _bitmap;
            UpdateImageMetadata(frame);

            if (Options.AutoZoomOnFirstFrame || !_hasRenderedFrame)
            {
                _imageView.UpdateZoomAndScale();
            }
        }

        private unsafe bool WriteTransformedPixels(byte[] pixels, FrameInfo frame)
        {
            if (_bitmap == null || frame.Format.BitsPerPixel % 8 != 0) return false;

            int pixelBytes = frame.Format.BitsPerPixel / 8;
            int rowBytes = GetDefaultStride(frame.Width, frame.Format);
            bool flipX = (frame.Transform & TransformFlipX) != 0;
            bool flipY = (frame.Transform & TransformFlipY) != 0;

            fixed (byte* sourceBase = pixels)
            {
                _bitmap.Lock();
                try
                {
                    byte* targetBase = (byte*)_bitmap.BackBuffer;
                    int targetStride = _bitmap.BackBufferStride;

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
                            Buffer.MemoryCopy(
                                sourceRow + (frame.Width - 1 - x) * pixelBytes,
                                targetRow + x * pixelBytes,
                                pixelBytes,
                                pixelBytes);
                        }
                    }

                    _bitmap.AddDirtyRect(new Int32Rect(0, 0, frame.Width, frame.Height));
                    return true;
                }
                finally
                {
                    _bitmap.Unlock();
                }
            }
        }

        private void UpdateImageMetadata(FrameInfo frame)
        {
            if (!Options.UpdateImageMetadata) return;

            int channels = GetChannelCount(frame.Format);
            int depth = channels > 0 ? frame.Format.BitsPerPixel / channels : frame.Format.BitsPerPixel;
            _imageView.Config.SetImageMetadata(ImageViewPropertyKeys.PixelFormat, frame.Format, nameof(RealtimeFramePresenter), "实时图像像素格式");
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
                _bitmap = null;
            }
        }
    }
}
