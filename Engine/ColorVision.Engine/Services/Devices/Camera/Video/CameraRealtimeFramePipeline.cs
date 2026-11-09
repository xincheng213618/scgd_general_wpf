using ColorVision.Core;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Realtime;
using ColorVision.ImageEditor.Settings;
using System;
using System.ComponentModel;
using System.Threading;

namespace ColorVision.Engine.Services.Devices.Camera.Video
{
    internal readonly record struct RealtimeCameraMetrics(double Fps, double? Articulation);

    internal sealed class CameraRealtimeFramePipeline : IDisposable
    {
        private readonly DefaultRealtimeCameraConfig _config = DefaultRealtimeCameraConfig.Current;
        private readonly RealtimeCameraOverlayVisual _overlayVisual;
        private readonly object _processorGate = new();
        private readonly TimeProvider _clock;

        private CameraFocusFrameProcessor? _processor;
        private ImageView? _imageView;
        private Action<double>? _onArticulation;
        private Action<RealtimeCameraMetrics>? _onFrameRateChanged;
        private volatile EventHandler? _frameDisplayedHandler;
        private long _displayedFrameCount;
        private int _frameCount;
        private long _fpsStartedAt;
        private double _lastFps;
        private double _articulation;
        private bool _overlaysAdded;
        private bool _disposed;
        private bool _configSubscribed;
        private bool _isRunning;
        private long _generation;
        private bool _showOverlayRoi = true;
        private int _transform = RealtimeFramePresenter.TransformNone;

        public CameraRealtimeFramePipeline(TimeProvider? clock = null)
        {
            _clock = clock ?? TimeProvider.System;
            _overlayVisual = new RealtimeCameraOverlayVisual(_config);
        }

        public int Transform
        {
            get => System.Threading.Volatile.Read(ref _transform);
            set => System.Threading.Volatile.Write(ref _transform, value & RealtimeFramePresenter.TransformFlipXY);
        }

        public RealtimeCameraMetrics CurrentMetrics => new(_lastFps, IsArticulationEnabled ? _articulation : null);

        public long CurrentDisplayedFrameCount => Interlocked.Read(ref _displayedFrameCount);

        public void Start(ImageView imageView, int transform = RealtimeFramePresenter.TransformNone, bool showOverlayRoi = true,
            bool showOverlayMetrics = true, Action<double>? onArticulation = null, bool preserveDisplayedFrameCount = false,
            Action<RealtimeCameraMetrics>? onFrameRateChanged = null)
        {
            ArgumentNullException.ThrowIfNull(imageView);
            ThrowIfDisposed();

            if (!imageView.Dispatcher.CheckAccess())
            {
                imageView.Dispatcher.Invoke(() => Start(imageView, transform, showOverlayRoi, showOverlayMetrics, onArticulation,
                    preserveDisplayedFrameCount, onFrameRateChanged));
                return;
            }

            if (_isRunning) Stop(resetRealtime: true);
            // A camera stream owns new pixels, not the previous file's path, layers or measurements.
            // Clearing the file identity also rejects a CVRAW load that is still finishing in the background.
            imageView.ReleaseImageContent();
            imageView.IEditorToolFactory.ApplyImageOpenTools(null);
            imageView.SetLayerController(null);
            imageView.Config.ClearProperties();
            imageView.Realtime.Reset();
            if (!preserveDisplayedFrameCount) Interlocked.Exchange(ref _displayedFrameCount, 0);
            EventHandler? frameDisplayedHandler = null;
            frameDisplayedHandler = (_, _) =>
            {
                if (ReferenceEquals(_frameDisplayedHandler, frameDisplayedHandler))
                    Interlocked.Increment(ref _displayedFrameCount);
            };
            _frameDisplayedHandler = frameDisplayedHandler;
            imageView.Realtime.FrameDisplayed += frameDisplayedHandler;
            Interlocked.Increment(ref _generation);
            _imageView = imageView;
            _onArticulation = onArticulation;
            _onFrameRateChanged = onFrameRateChanged;
            Transform = transform;
            _showOverlayRoi = showOverlayRoi;
            _overlayVisual.IsRoiVisible = showOverlayRoi && IsArticulationEnabled;
            _overlayVisual.IsStatusVisible = showOverlayMetrics;
            _frameCount = 0;
            _lastFps = 0;
            _articulation = 0;
            _fpsStartedAt = _clock.GetTimestamp();
            _isRunning = true;
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
            ImageView? imageView = _imageView;
            EventHandler? frameDisplayedHandler = _frameDisplayedHandler;
            _frameDisplayedHandler = null;
            _imageView = null;
            _onArticulation = null;
            _onFrameRateChanged = null;
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
                imageView.Realtime.FrameDisplayed -= frameDisplayedHandler;
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
            RecordFrameRate(imageView);
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
            RecordFrameRate(imageView);
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
                _onArticulation?.Invoke(result.Articulation);
            }));
        }

        private void RecordFrameRate(ImageView imageView)
        {
            EventHandler? session = _frameDisplayedHandler;
            if (session == null || !ReferenceEquals(imageView, _imageView)) return;
            Interlocked.Increment(ref _frameCount);
            long now = _clock.GetTimestamp();
            double elapsedSeconds = _clock.GetElapsedTime(Volatile.Read(ref _fpsStartedAt), now).TotalSeconds;
            if (elapsedSeconds < 1) return;

            double fps = Interlocked.Exchange(ref _frameCount, 0) / elapsedSeconds;
            Volatile.Write(ref _fpsStartedAt, now);
            void Publish()
            {
                if (!ReferenceEquals(session, _frameDisplayedHandler)) return;
                _lastFps = fps;
                RealtimeCameraMetrics metrics = CurrentMetrics;
                if (_overlayVisual.IsStatusVisible) _overlayVisual.UpdateMetrics(metrics.Fps, metrics.Articulation);
                _onFrameRateChanged?.Invoke(metrics);
            }

            if (imageView.Dispatcher.CheckAccess()) Publish();
            else if (!imageView.Dispatcher.HasShutdownStarted) imageView.Dispatcher.BeginInvoke(new Action(Publish));
        }

        private void UpdateOverlayMetrics()
        {
            if (_overlayVisual.IsStatusVisible) _overlayVisual.UpdateMetrics(_lastFps, IsArticulationEnabled ? _articulation : null);
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
                if (!_isRunning || !ReferenceEquals(imageView, _imageView)) return;
                if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(DefaultRealtimeCameraConfig.IsCalArtculation))
                    UpdateOverlayVisibility(imageView);
                if (e.PropertyName == nameof(DefaultRealtimeCameraConfig.EvaFunc))
                {
                    Interlocked.Increment(ref _generation);
                    ReleaseProcessor();
                }
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
