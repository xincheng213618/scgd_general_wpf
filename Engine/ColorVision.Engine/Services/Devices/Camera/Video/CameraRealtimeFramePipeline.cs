using ColorVision.Core;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Realtime;
using ColorVision.ImageEditor.Settings;
using System;
using System.ComponentModel;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Camera.Video
{
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
        private bool _configSubscribed;
        private bool _isRunning;
        private int _transform = (int)RealtimeFrameTransform.None;

        public CameraRealtimeFramePipeline()
        {
            _overlayVisual = new RealtimeCameraOverlayVisual(_config);
        }

        public RealtimeFrameTransform Transform
        {
            get => (RealtimeFrameTransform)System.Threading.Volatile.Read(ref _transform);
            set => System.Threading.Volatile.Write(ref _transform, (int)value);
        }

        public void Start(ImageView imageView, RealtimeFrameTransform transform = RealtimeFrameTransform.None)
        {
            ArgumentNullException.ThrowIfNull(imageView);
            ThrowIfDisposed();

            if (!imageView.Dispatcher.CheckAccess())
            {
                imageView.Dispatcher.Invoke(() => Start(imageView, transform));
                return;
            }

            _imageView = imageView;
            Transform = transform;
            _isRunning = true;
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
            ReleaseConfigSubscription();
            _overlayVisual.Detach();

            _processor?.Dispose();
            _processor = null;

            if (imageView == null)
            {
                _overlaysAdded = false;
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

        }

        public void SubmitFrame(byte[] sourceBuffer, int length, int width, int height, int channels, int bitsPerPixel, int stride)
        {
            ArgumentNullException.ThrowIfNull(sourceBuffer);
            if (length <= 0 || length > sourceBuffer.Length || !_isRunning)
            {
                return;
            }

            ImageView? imageView = _imageView;
            if (imageView == null)
            {
                return;
            }

            if (TryCreateProcessingRequest(width, height, out VideoFrameProcessingRequest request))
            {
                EnsureProcessor().SubmitFrame(sourceBuffer, length, width, height, channels, bitsPerPixel, stride, request);
            }

            imageView.Realtime.SubmitFrame(
                sourceBuffer,
                width,
                height,
                RealtimeFramePresenter.GetPixelFormat(channels, bitsPerPixel),
                stride,
                length,
                Transform);
            RecordFrameRate();
        }

        public void SubmitFrame(IntPtr sourcePointer, int length, int width, int height, int channels, int bitsPerPixel, int stride)
        {
            if (sourcePointer == IntPtr.Zero || length <= 0 || !_isRunning)
            {
                return;
            }

            ImageView? imageView = _imageView;
            if (imageView == null)
            {
                return;
            }

            if (TryCreateProcessingRequest(width, height, out VideoFrameProcessingRequest request))
            {
                EnsureProcessor().SubmitFrame(sourcePointer, length, width, height, channels, bitsPerPixel, stride, request);
            }

            imageView.Realtime.SubmitFrame(
                sourcePointer,
                width,
                height,
                RealtimeFramePresenter.GetPixelFormat(channels, bitsPerPixel),
                stride,
                length,
                Transform);
            RecordFrameRate();
        }

        private bool TryCreateProcessingRequest(int width, int height, out VideoFrameProcessingRequest request)
        {
            request = default;

            if (!_config.IsUseCacheFile || !_config.IsCalArtculation)
            {
                return false;
            }

            Rect rect = _overlayVisual.GetProcessingRoi(width, height);

            request = new VideoFrameProcessingRequest(_config.EvaFunc, new RoiRect(rect));
            return true;
        }

        private VideoFrameProcessor EnsureProcessor()
        {
            ThrowIfDisposed();
            return _processor ??= new VideoFrameProcessor(HandleProcessedFrame);
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

                _articulation = result.Articulation;

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
