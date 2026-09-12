using ColorVision.Core;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Realtime;
using ColorVision.ImageEditor.Settings;
using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Camera.Video
{
    internal readonly record struct RealtimeCameraMetrics(double Fps, double? Articulation);

    internal sealed class CameraRealtimeFramePipeline : IDisposable
    {
        private readonly DefaultRealtimeCameraConfig _config = DefaultRealtimeCameraConfig.Current;
        private readonly RealtimeCameraOverlayVisual _overlayVisual;
        private readonly object _processorGate = new();

        private CameraFocusFrameProcessor? _processor;
        private ImageView? _imageView;
        private int _frameCount;
        private readonly System.Diagnostics.Stopwatch _fpsTimer = new();
        private double _lastFps;
        private double _articulation;
        private bool _overlaysAdded;
        private bool _disposed;
        private bool _configSubscribed;
        private bool _isRunning;
        private long _generation;
        private bool _showOverlayRoi = true;
        private bool _showOverlayMetrics = true;
        private int _transform = RealtimeFramePresenter.TransformNone;

        public CameraRealtimeFramePipeline()
        {
            _overlayVisual = new RealtimeCameraOverlayVisual(_config);
        }

        public int Transform
        {
            get => System.Threading.Volatile.Read(ref _transform);
            set => System.Threading.Volatile.Write(ref _transform, value & RealtimeFramePresenter.TransformFlipXY);
        }

        public bool IsMetricsVisible
        {
            get => _showOverlayMetrics;
            set
            {
                if (_showOverlayMetrics == value) return;
                _showOverlayMetrics = value;
                _overlayVisual.IsStatusVisible = value;
            }
        }

        public RealtimeCameraMetrics CurrentMetrics => new(_lastFps, IsArticulationEnabled ? _articulation : null);

        public void Start(ImageView imageView, int transform = RealtimeFramePresenter.TransformNone, bool showOverlayRoi = true, bool showOverlayMetrics = true)
        {
            ArgumentNullException.ThrowIfNull(imageView);
            ThrowIfDisposed();

            if (!imageView.Dispatcher.CheckAccess())
            {
                imageView.Dispatcher.Invoke(() => Start(imageView, transform, showOverlayRoi, showOverlayMetrics));
                return;
            }

            if (_isRunning) Stop();
            Interlocked.Increment(ref _generation);
            _imageView = imageView;
            Transform = transform;
            _showOverlayRoi = showOverlayRoi;
            _showOverlayMetrics = showOverlayMetrics;
            _overlayVisual.IsRoiVisible = showOverlayRoi && IsArticulationEnabled;
            _overlayVisual.IsStatusVisible = showOverlayMetrics;
            _isRunning = true;
            _frameCount = 0;
            _lastFps = 0;
            _articulation = 0;
            _fpsTimer.Restart();
            EnsureConfigSubscription();

            imageView.Realtime.Configure(new RealtimeFrameOptions
            {
                AutoZoomOnFirstFrame = true,
                UpdateImageMetadata = true
            });

            UpdateOverlayVisibility(imageView);
        }

        public void Stop(bool resetRealtime = false, bool clearImageSource = false)
        {
            _fpsTimer.Stop();
            ImageView? imageView = _imageView;
            _imageView = null;
            _isRunning = false;
            Interlocked.Increment(ref _generation);
            ReleaseConfigSubscription();
            _overlayVisual.Detach();

            ReleaseProcessor();

            if (imageView == null)
            {
                _overlaysAdded = false;
                return;
            }

            void StopCore()
            {
                if (_overlaysAdded)
                {
                    imageView.ImageShow.RemoveOverlayVisual(_overlayVisual);
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

            if (IsArticulationEnabled)
                GetFocusProcessor()?.SubmitFrame(sourceBuffer, length, width, height, channels, bitsPerPixel, stride, CreateFocusRequest(width, height));

            imageView.Realtime.SubmitFrame(sourceBuffer, width, height,
                RealtimeFramePresenter.GetPixelFormat(channels, bitsPerPixel), stride, length, Transform);
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

            if (IsArticulationEnabled)
                GetFocusProcessor()?.SubmitFrame(sourcePointer, length, width, height, channels, bitsPerPixel, stride, CreateFocusRequest(width, height));

            imageView.Realtime.SubmitFrame(sourcePointer, width, height,
                RealtimeFramePresenter.GetPixelFormat(channels, bitsPerPixel), stride, length, Transform);
            RecordFrameRate();
        }

        private CameraFocusFrameRequest CreateFocusRequest(int width, int height)
            => new(_config.EvaFunc, new RoiRect(_overlayVisual.GetProcessingRoi(width, height)));

        private bool IsArticulationEnabled => _config.IsCalArtculation;

        private void UpdateOverlayVisibility(ImageView imageView)
        {
            _overlayVisual.IsRoiVisible = _showOverlayRoi && IsArticulationEnabled;
            _overlayVisual.Attach();
            if (!_overlaysAdded)
            {
                imageView.ImageShow.AddOverlayVisual(_overlayVisual);
                _overlaysAdded = true;
            }

            if (!IsArticulationEnabled)
            {
                Interlocked.Increment(ref _generation);
                ReleaseProcessor();
            }

            UpdateOverlayMetrics();
        }

        private CameraFocusFrameProcessor? GetFocusProcessor()
        {
            lock (_processorGate)
            {
                if (_disposed || !_isRunning || !IsArticulationEnabled) return null;
                long generation = Volatile.Read(ref _generation);
                return _processor ??= new CameraFocusFrameProcessor(result => HandleProcessedFrame(result, generation));
            }
        }

        private void ReleaseProcessor()
        {
            CameraFocusFrameProcessor? processor;
            lock (_processorGate)
            {
                processor = _processor;
                _processor = null;
            }
            processor?.Dispose();
        }

        private void HandleProcessedFrame(CameraFocusFrameResult result, long generation)
        {
            ImageView? targetView = _imageView;
            if (!_isRunning || targetView == null || targetView.Dispatcher.HasShutdownStarted
                || generation != Volatile.Read(ref _generation)) return;

            targetView.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_isRunning || !ReferenceEquals(targetView, _imageView) || !IsArticulationEnabled
                    || generation != Volatile.Read(ref _generation)) return;
                _articulation = result.Articulation;
                UpdateOverlayMetrics();
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
            UpdateOverlayMetrics();
        }

        private void UpdateOverlayMetrics()
        {
            _overlayVisual.UpdateMetrics(_lastFps, IsArticulationEnabled ? _articulation : null);
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
            ImageView? imageView = _imageView;
            if (imageView == null) return;

            void ApplyConfigChange()
            {
                if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(DefaultRealtimeCameraConfig.IsCalArtculation))
                    UpdateOverlayVisibility(imageView);
            }

            if (imageView.Dispatcher.CheckAccess())
            {
                ApplyConfigChange();
            }
            else
            {
                imageView.Dispatcher.BeginInvoke(new Action(ApplyConfigChange));
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
