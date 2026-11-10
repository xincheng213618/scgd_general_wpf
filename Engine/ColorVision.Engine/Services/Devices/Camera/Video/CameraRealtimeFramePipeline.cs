using ColorVision.Core;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Realtime;
using ColorVision.ImageEditor.Settings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Camera.Video
{
    internal readonly record struct RealtimeCameraMetrics(double Fps, double? Articulation);

    internal sealed record CameraPreviewGeometry(Rect DisplayRoi, int Transform)
    {
        public RoiRect GetSourceRoi(int width, int height)
            => VideoCrossGuideDetector.TransformDisplayRoiToSource(DisplayRoi, width, height, Transform);
    }

    internal sealed class CameraPreviewSession
    {
        private readonly CancellationTokenSource _retirement = new();

        internal CameraPreviewSession(ImageView view, long fpsStartedAt, Action<double>? onArticulation, Action<RealtimeCameraMetrics>? onFrameRateChanged)
        {
            View = view;
            FpsStartedAt = fpsStartedAt;
            OnArticulation = onArticulation;
            OnFrameRateChanged = onFrameRateChanged;
            Lifetime = _retirement.Token;
        }

        internal ImageView View { get; }
        internal Action<double>? OnArticulation { get; }
        internal Action<RealtimeCameraMetrics>? OnFrameRateChanged { get; }
        internal CancellationToken Lifetime { get; }
        internal bool IsRetired => Lifetime.IsCancellationRequested;
        internal EventHandler? FrameDisplayedHandler { get; set; }
        internal int FrameCount;
        internal long FpsStartedAt;

        internal void Retire()
        {
            _retirement.Cancel();
            _retirement.Dispose();
        }
    }

    internal sealed class CameraRealtimeFramePipeline : IDisposable
    {
        private readonly DefaultRealtimeCameraConfig _config = DefaultRealtimeCameraConfig.Current;
        private readonly RealtimeCameraOverlayVisual _overlayVisual;
        private readonly Dictionary<ImageView, CameraPreviewSession> _viewSessions = new();
        private readonly object _processorGate = new();
        private readonly TimeProvider _clock;
        private readonly Func<Action<CameraFocusFrameResult>, CameraFocusFrameProcessor> _createFocusProcessor;

        private CameraFocusFrameProcessor? _processor;
        private Task? _processorRetirement;
        private volatile CameraPreviewSession? _session;
        private long _displayedFrameCount;
        private double _lastFps;
        private double _articulation;
        private ImageView? _overlayView;
        private bool _disposed;
        private bool _configSubscribed;
        private long _generation;
        private bool _showOverlayRoi = true;
        private volatile CameraPreviewGeometry _previewGeometry = new(Rect.Empty, RealtimeFramePresenter.TransformNone);

        public CameraRealtimeFramePipeline(TimeProvider? clock = null,
            Func<Action<CameraFocusFrameResult>, CameraFocusFrameProcessor>? createFocusProcessor = null)
        {
            _clock = clock ?? TimeProvider.System;
            _createFocusProcessor = createFocusProcessor ?? (resultHandler => new CameraFocusFrameProcessor(resultHandler));
            _overlayVisual = new RealtimeCameraOverlayVisual(_config);
        }

        public CameraPreviewGeometry PreviewGeometry => _previewGeometry;

        internal CameraPreviewSession? CaptureSession() => _session;

        public void SetPreviewGeometry(Rect displayRoi, int transform)
        {
            _previewGeometry = new(displayRoi, transform & RealtimeFramePresenter.TransformFlipXY);
            if (_overlayVisual.Dispatcher.CheckAccess())
                _overlayVisual.DisplayRoi = _previewGeometry.DisplayRoi;
            else
                _overlayVisual.Dispatcher.BeginInvoke(() => _overlayVisual.DisplayRoi = _previewGeometry.DisplayRoi);
        }

        public RealtimeCameraMetrics CurrentMetrics => new(_lastFps, IsArticulationEnabled ? _articulation : null);

        public long CurrentDisplayedFrameCount => Interlocked.Read(ref _displayedFrameCount);

        public void Start(ImageView imageView, int transform = RealtimeFramePresenter.TransformNone, bool showOverlayRoi = true,
            bool showOverlayMetrics = true, Action<double>? onArticulation = null, bool preserveDisplayedFrameCount = false,
            Action<RealtimeCameraMetrics>? onFrameRateChanged = null, Rect displayRoi = default)
        {
            ArgumentNullException.ThrowIfNull(imageView);
            ThrowIfDisposed();

            if (!imageView.Dispatcher.CheckAccess())
            {
                imageView.Dispatcher.Invoke(() => Start(imageView, transform, showOverlayRoi, showOverlayMetrics, onArticulation,
                    preserveDisplayedFrameCount, onFrameRateChanged, displayRoi));
                return;
            }

            if (_session != null) Stop(resetRealtime: true);
            // A camera stream owns new pixels, not the previous file's path, layers or measurements.
            // Clearing the file identity also rejects a CVRAW load that is still finishing in the background.
            imageView.ReleaseImageContent();
            imageView.IEditorToolFactory.ApplyImageOpenTools(null);
            imageView.SetLayerController(null);
            imageView.Config.ClearProperties();
            imageView.Realtime.Reset();
            if (!preserveDisplayedFrameCount) Interlocked.Exchange(ref _displayedFrameCount, 0);
            CameraPreviewSession session = new(imageView, _clock.GetTimestamp(), onArticulation, onFrameRateChanged);
            session.FrameDisplayedHandler = (_, _) =>
            {
                if (ReferenceEquals(_session, session))
                    Interlocked.Increment(ref _displayedFrameCount);
            };
            _viewSessions[imageView] = session;
            imageView.Realtime.FrameDisplayed += session.FrameDisplayedHandler;
            SetPreviewGeometry(displayRoi, transform);
            _showOverlayRoi = showOverlayRoi;
            _overlayVisual.IsRoiVisible = showOverlayRoi && IsArticulationEnabled;
            _overlayVisual.IsStatusVisible = showOverlayMetrics;
            _lastFps = 0;
            _articulation = 0;

            imageView.Realtime.Configure(new RealtimeFrameOptions
            {
                AutoZoomOnFirstFrame = true,
                UpdateImageMetadata = true
            });

            UpdateOverlayVisibility(imageView);
            lock (_processorGate)
            {
                Interlocked.Increment(ref _generation);
                EnsureConfigSubscription();
                _session = session;
            }
        }

        public void Stop(bool resetRealtime = false, bool clearImageSource = false)
        {
            CameraPreviewSession? session;
            lock (_processorGate)
            {
                session = _session;
                if (session == null) return;
                session.Retire();
                _session = null;
                Interlocked.Increment(ref _generation);
                ReleaseConfigSubscription();
                ReleaseProcessor();
            }
            ImageView imageView = session.View;

            void StopCore()
            {
                imageView.Realtime.FrameDisplayed -= session.FrameDisplayedHandler;
                // The same view may already belong to a newer preview when this cleanup runs.
                if (!_viewSessions.TryGetValue(imageView, out var owner) || !ReferenceEquals(owner, session)) return;
                _viewSessions.Remove(imageView);

                if (ReferenceEquals(_overlayView, imageView))
                {
                    _overlayVisual.Detach();
                    imageView.ImageShow.RemoveOverlayVisual(_overlayVisual);
                    _overlayView = null;
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
            CameraPreviewSession? session = CaptureSession();
            if (session == null || session.IsRetired || length <= 0 || length > sourceBuffer.Length) return;

            CameraPreviewGeometry geometry = PreviewGeometry;
            if (IsArticulationEnabled)
                GetFocusProcessor(session)?.SubmitFrame(sourceBuffer, length, width, height, channels, bitsPerPixel, stride, CreateFocusRequest(geometry, width, height));

            session.View.Realtime.SubmitFrame(sourceBuffer, width, height,
                RealtimeFramePresenter.GetPixelFormat(channels, bitsPerPixel), stride, length, geometry.Transform, session.Lifetime);
            RecordFrameRate(session);
        }

        public void SubmitFrame(IntPtr sourcePointer, int length, int width, int height, int channels, int bitsPerPixel, int stride,
            CameraPreviewGeometry? geometry = null)
            => SubmitFrame(CaptureSession(), sourcePointer, length, width, height, channels, bitsPerPixel, stride, geometry);

        internal void SubmitFrame(CameraPreviewSession? session, IntPtr sourcePointer, int length, int width, int height, int channels, int bitsPerPixel,
            int stride, CameraPreviewGeometry? geometry = null)
        {
            if (session == null || session.IsRetired || !ReferenceEquals(session, _session) || sourcePointer == IntPtr.Zero || length <= 0) return;

            geometry ??= PreviewGeometry;
            if (IsArticulationEnabled)
                GetFocusProcessor(session)?.SubmitFrame(sourcePointer, length, width, height, channels, bitsPerPixel, stride, CreateFocusRequest(geometry, width, height));

            session.View.Realtime.SubmitFrame(sourcePointer, width, height,
                RealtimeFramePresenter.GetPixelFormat(channels, bitsPerPixel), stride, length, geometry.Transform, session.Lifetime);
            RecordFrameRate(session);
        }

        private CameraFocusFrameRequest CreateFocusRequest(CameraPreviewGeometry geometry, int width, int height)
            => new(_config.EvaFunc, geometry.GetSourceRoi(width, height));

        private bool IsArticulationEnabled => _config.IsCalArtculation;

        private void UpdateOverlayVisibility(ImageView imageView)
        {
            _overlayVisual.IsRoiVisible = _showOverlayRoi && IsArticulationEnabled;
            _overlayVisual.Attach();
            if (!ReferenceEquals(_overlayView, imageView))
            {
                _overlayView?.ImageShow.RemoveOverlayVisual(_overlayVisual);
                imageView.ImageShow.AddOverlayVisual(_overlayVisual);
                _overlayView = imageView;
            }

            if (!IsArticulationEnabled)
            {
                Interlocked.Increment(ref _generation);
                ReleaseProcessor();
            }

            UpdateOverlayMetrics();
        }

        private CameraFocusFrameProcessor? GetFocusProcessor(CameraPreviewSession session)
        {
            lock (_processorGate)
            {
                if (_disposed || !ReferenceEquals(_session, session) || !IsArticulationEnabled) return null;
                // A previous session can still own pixels in native focus processing.
                if (_processorRetirement is { IsCompleted: false }) return null;
                _processorRetirement = null;
                long generation = Volatile.Read(ref _generation);
                return _processor ??= _createFocusProcessor(result => HandleProcessedFrame(result, session, generation));
            }
        }

        private void ReleaseProcessor()
        {
            lock (_processorGate)
            {
                if (_processor is not { } processor) return;
                _processor = null;
                _processorRetirement = processor.Retirement;
                processor.Dispose();
            }
        }

        private void HandleProcessedFrame(CameraFocusFrameResult result, CameraPreviewSession session, long generation)
        {
            ImageView targetView = session.View;
            if (!ReferenceEquals(_session, session) || targetView.Dispatcher.HasShutdownStarted
                || generation != Volatile.Read(ref _generation)) return;

            targetView.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!ReferenceEquals(_session, session) || !IsArticulationEnabled
                    || generation != Volatile.Read(ref _generation)) return;
                _articulation = result.Articulation;
                UpdateOverlayMetrics();
                session.OnArticulation?.Invoke(result.Articulation);
            }));
        }

        private void RecordFrameRate(CameraPreviewSession session)
        {
            if (!ReferenceEquals(session, _session)) return;
            Interlocked.Increment(ref session.FrameCount);
            long now = _clock.GetTimestamp();
            double elapsedSeconds = _clock.GetElapsedTime(Volatile.Read(ref session.FpsStartedAt), now).TotalSeconds;
            if (elapsedSeconds < 1) return;

            double fps = Interlocked.Exchange(ref session.FrameCount, 0) / elapsedSeconds;
            Volatile.Write(ref session.FpsStartedAt, now);
            void Publish()
            {
                if (!ReferenceEquals(session, _session)) return;
                _lastFps = fps;
                RealtimeCameraMetrics metrics = CurrentMetrics;
                if (_overlayVisual.IsStatusVisible) _overlayVisual.UpdateMetrics(metrics.Fps, metrics.Articulation);
                session.OnFrameRateChanged?.Invoke(metrics);
            }

            ImageView imageView = session.View;
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
            CameraPreviewSession? session = _session;
            if (session == null) return;
            ImageView imageView = session.View;

            void ApplyConfigChange()
            {
                if (!ReferenceEquals(session, _session)) return;
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
