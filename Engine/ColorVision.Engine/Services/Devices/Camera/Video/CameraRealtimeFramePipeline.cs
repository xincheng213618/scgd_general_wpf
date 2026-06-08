using ColorVision.Core;
using ColorVision.Engine.Media;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Realtime;
using ColorVision.ImageEditor.Settings;
using FlowEngineLib.Algorithm;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Camera.Video
{
    internal readonly record struct CameraRealtimeFrameLayout(
        int Width,
        int Height,
        int Channels,
        int BitsPerPixel,
        int Stride,
        int BufferLength)
    {
        public System.Windows.Media.PixelFormat PixelFormat => RealtimeFramePresenter.GetPixelFormat(Channels, BitsPerPixel);
    }

    internal sealed class CameraRealtimeFramePipeline : IDisposable
    {
        private readonly DefaultRealtimeCameraConfig _config = DefaultRealtimeCameraConfig.Current;
        private readonly RealtimeCameraOverlayVisual _overlayVisual;

        private VideoFrameProcessor? _processor;
        private ImageView? _imageView;
        private int _frameCount;
        private readonly System.Diagnostics.Stopwatch _fpsTimer = new();
        private double _lastFps;
        private double _articulation;
        private bool _overlaysAdded;
        private bool _disposed;
        private readonly object _displayBufferGate = new();
        private IntPtr _displayBuffer = IntPtr.Zero;
        private int _displayBufferSize;
        private bool _configSubscribed;
        private bool _isRunning;
        private Func<CVImageFlipMode>? _flipModeProvider;

        public CameraRealtimeFramePipeline()
        {
            _overlayVisual = new RealtimeCameraOverlayVisual(_config);
        }

        public void Start(ImageView imageView, Func<CVImageFlipMode>? flipModeProvider = null)
        {
            ArgumentNullException.ThrowIfNull(imageView);
            ThrowIfDisposed();

            if (!imageView.Dispatcher.CheckAccess())
            {
                imageView.Dispatcher.Invoke(() => Start(imageView, flipModeProvider));
                return;
            }

            _imageView = imageView;
            _flipModeProvider = flipModeProvider;
            _isRunning = true;
            _processor ??= new VideoFrameProcessor(HandleProcessedFrame);
            _frameCount = 0;
            _lastFps = 0;
            _articulation = 0;
            _fpsTimer.Restart();
            EnsureConfigSubscription();
            _overlayVisual.Attach();

            imageView.Realtime.Configure(new RealtimeFrameOptions
            {
                MaxDisplayFps = _config.MaxDisplayFps,
                AutoZoomOnFirstFrame = true,
                UpdateImageMetadata = true
            });

            if (!_overlaysAdded)
            {
                imageView.Realtime.AddOverlayVisual(_overlayVisual);
                _overlaysAdded = true;
            }

            _overlayVisual.UpdateMetrics(_articulation, _lastFps);
        }

        public void Stop(bool resetRealtime = false, bool clearImageSource = false)
        {
            _fpsTimer.Stop();
            ImageView? imageView = _imageView;
            _imageView = null;
            _isRunning = false;
            _flipModeProvider = null;
            ReleaseConfigSubscription();
            _overlayVisual.Detach();

            _processor?.Dispose();
            _processor = null;

            if (imageView == null)
            {
                _overlaysAdded = false;
                ReleaseDisplayBuffer();
                return;
            }

            void StopCore()
            {
                if (_overlaysAdded)
                {
                    imageView.Realtime.RemoveOverlayVisual(_overlayVisual);
                    _overlaysAdded = false;
                }

                if (resetRealtime)
                {
                    imageView.Realtime.Reset(clearImageSource);
                }
            }

            if (imageView.Dispatcher.CheckAccess())
            {
                StopCore();
            }
            else
            {
                imageView.Dispatcher.BeginInvoke(new Action(StopCore));
            }

            ReleaseDisplayBuffer();
        }

        public void SubmitFrame(byte[] sourceBuffer, int length, int width, int height, int channels, int bitsPerPixel, int stride)
        {
            ArgumentNullException.ThrowIfNull(sourceBuffer);
            if (length <= 0 || length > sourceBuffer.Length || !_isRunning)
            {
                return;
            }

            SubmitFrameInternal(
                new CameraRealtimeFrameLayout(width, height, channels, bitsPerPixel, stride, length),
                submitProcessing: request => _processor?.SubmitFrame(sourceBuffer, length, width, height, channels, bitsPerPixel, stride, request),
                submitDisplay: imageView => imageView.Realtime.SubmitFrame(sourceBuffer, width, height, RealtimeFramePresenter.GetPixelFormat(channels, bitsPerPixel), stride, length));
        }

        public void SubmitFrame(IntPtr sourcePointer, int length, int width, int height, int channels, int bitsPerPixel, int stride)
        {
            if (sourcePointer == IntPtr.Zero || length <= 0 || !_isRunning)
            {
                return;
            }

            CameraRealtimeFrameLayout layout = new(width, height, channels, bitsPerPixel, stride, length);
            SubmitFrameInternal(
                layout,
                submitProcessing: request => _processor?.SubmitFrame(sourcePointer, length, width, height, channels, bitsPerPixel, stride, request),
                submitDisplay: imageView => SubmitPointerDisplayFrame(imageView, sourcePointer, layout));
        }

        private void SubmitFrameInternal(
            CameraRealtimeFrameLayout layout,
            Action<VideoFrameProcessingRequest> submitProcessing,
            Action<ImageView> submitDisplay)
        {
            ImageView? imageView = _imageView;
            if (!_isRunning || imageView == null)
            {
                return;
            }

            if (TryBuildProcessingRequest(layout.Width, layout.Height, out VideoFrameProcessingRequest? request))
            {
                submitProcessing(request);
            }

            submitDisplay(imageView);

            RecordFrameRate();
        }

        private bool TryBuildProcessingRequest(int width, int height, out VideoFrameProcessingRequest? request)
        {
            request = null;

            bool enableArticulation = _config.IsUseCacheFile && _config.IsCalArtculation;
            if (!enableArticulation)
            {
                return false;
            }

            Rect rect = _overlayVisual.GetProcessingRoi(width, height);

            request = new VideoFrameProcessingRequest
            {
                EnableArticulation = enableArticulation,
                FocusAlgorithm = _config.EvaFunc,
                Roi = new RoiRect(rect)
            };
            return true;
        }

        private void HandleProcessedFrame(VideoFrameProcessingResult result)
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                ImageView? imageView = _imageView;
                if (!_isRunning || imageView == null)
                {
                    return;
                }

                if (result.Articulation is double articulation)
                {
                    _articulation = articulation;
                }

                _overlayVisual.UpdateMetrics(_articulation, _lastFps);
            }));
        }

        private void RecordFrameRate()
        {
            System.Threading.Interlocked.Increment(ref _frameCount);
            if (_fpsTimer.ElapsedMilliseconds < 1000)
            {
                return;
            }

            _lastFps = (double)_frameCount * 1000 / _fpsTimer.ElapsedMilliseconds;
            System.Threading.Interlocked.Exchange(ref _frameCount, 0);
            _fpsTimer.Restart();
            _overlayVisual.UpdateMetrics(_articulation, _lastFps);
        }

        private void EnsureConfigSubscription()
        {
            if (_configSubscribed)
            {
                return;
            }

            _config.PropertyChanged += Config_PropertyChanged;
            _configSubscribed = true;
        }

        private void ReleaseConfigSubscription()
        {
            if (!_configSubscribed)
            {
                return;
            }

            _config.PropertyChanged -= Config_PropertyChanged;
            _configSubscribed = false;
        }

        private void Config_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(DefaultRealtimeCameraConfig.MaxDisplayFps))
            {
                return;
            }

            ImageView? imageView = _imageView;
            if (imageView == null)
            {
                return;
            }

            void ApplyMaxDisplayFps()
            {
                imageView.Realtime.Options.MaxDisplayFps = _config.MaxDisplayFps;
            }

            if (imageView.Dispatcher.CheckAccess())
            {
                ApplyMaxDisplayFps();
            }
            else
            {
                imageView.Dispatcher.BeginInvoke(new Action(ApplyMaxDisplayFps));
            }
        }

        private void SubmitPointerDisplayFrame(ImageView imageView, IntPtr sourcePointer, CameraRealtimeFrameLayout layout)
        {
            var pixelFormat = layout.PixelFormat;
            CVImageFlipMode flipMode = _flipModeProvider?.Invoke() ?? CVImageFlipMode.None;
            if (flipMode == CVImageFlipMode.None)
            {
                imageView.Realtime.SubmitFrame(sourcePointer, layout.Width, layout.Height, pixelFormat, layout.Stride, layout.BufferLength);
                return;
            }

            int targetStride = RealtimeFramePresenter.GetDefaultStride(layout.Width, pixelFormat);
            int targetBytes = targetStride * layout.Height;
            lock (_displayBufferGate)
            {
                EnsureDisplayBuffer(targetBytes);
                OpenCvSharp.MatType matType = pixelFormat.GetPixelFormat();
                using var srcMat = OpenCvSharp.Mat.FromPixelData(layout.Height, layout.Width, matType, sourcePointer, layout.Stride);
                using var dstMat = OpenCvSharp.Mat.FromPixelData(layout.Height, layout.Width, matType, _displayBuffer, targetStride);
                OpenCvSharp.Cv2.Flip(srcMat, dstMat, (OpenCvSharp.FlipMode)flipMode);
                imageView.Realtime.SubmitFrame(_displayBuffer, layout.Width, layout.Height, pixelFormat, targetStride, targetBytes);
            }
        }

        private void EnsureDisplayBuffer(int requiredBytes)
        {
            if (_displayBuffer != IntPtr.Zero && _displayBufferSize >= requiredBytes)
            {
                return;
            }

            if (_displayBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_displayBuffer);
            }

            _displayBuffer = Marshal.AllocHGlobal(requiredBytes);
            _displayBufferSize = requiredBytes;
        }

        private void ReleaseDisplayBuffer()
        {
            lock (_displayBufferGate)
            {
                if (_displayBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_displayBuffer);
                    _displayBuffer = IntPtr.Zero;
                }

                _displayBufferSize = 0;
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Stop();
            _disposed = true;
        }
    }
}
