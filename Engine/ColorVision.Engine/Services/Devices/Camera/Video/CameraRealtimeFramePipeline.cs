using ColorVision.Core;
using ColorVision.Engine.Media;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Realtime;
using FlowEngineLib.Algorithm;
using System;
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

    internal readonly record struct CameraRealtimePipelineState(double Articulation, double LastFps);

    internal sealed class CameraRealtimeFramePipeline : IDisposable
    {
        private readonly VideoReaderConfig _config;
        private readonly DVRectangleText _roiVisual;
        private readonly DVText _statusVisual;
        private readonly Func<bool> _isActive;
        private readonly Func<CameraRealtimePipelineState, string>? _statusTextFormatter;
        private readonly Action<double>? _fpsUpdated;
        private readonly Action<double>? _articulationUpdated;
        private readonly Action<ImageView>? _firstPseudoFrameDisplayed;
        private readonly Func<CVImageFlipMode>? _flipModeProvider;

        private VideoFrameProcessor? _processor;
        private ImageView? _imageView;
        private int _frameCount;
        private readonly System.Diagnostics.Stopwatch _fpsTimer = new();
        private double _lastFps;
        private double _articulation;
        private bool _firstFrame = true;
        private bool _overlaysAdded;
        private bool _disposed;
        private readonly object _displayBufferGate = new();
        private IntPtr _displayBuffer = IntPtr.Zero;
        private int _displayBufferSize;

        public CameraRealtimeFramePipeline(
            VideoReaderConfig config,
            DVRectangleText roiVisual,
            DVText statusVisual,
            Func<bool> isActive,
            Func<CameraRealtimePipelineState, string>? statusTextFormatter = null,
            Action<double>? fpsUpdated = null,
            Action<double>? articulationUpdated = null,
            Action<ImageView>? firstPseudoFrameDisplayed = null,
            Func<CVImageFlipMode>? flipModeProvider = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _roiVisual = roiVisual ?? throw new ArgumentNullException(nameof(roiVisual));
            _statusVisual = statusVisual ?? throw new ArgumentNullException(nameof(statusVisual));
            _isActive = isActive ?? throw new ArgumentNullException(nameof(isActive));
            _statusTextFormatter = statusTextFormatter;
            _fpsUpdated = fpsUpdated;
            _articulationUpdated = articulationUpdated;
            _firstPseudoFrameDisplayed = firstPseudoFrameDisplayed;
            _flipModeProvider = flipModeProvider;
        }

        public void Start(ImageView imageView)
        {
            ArgumentNullException.ThrowIfNull(imageView);
            ThrowIfDisposed();

            if (!imageView.Dispatcher.CheckAccess())
            {
                imageView.Dispatcher.Invoke(() => Start(imageView));
                return;
            }

            _imageView = imageView;
            _processor ??= new VideoFrameProcessor(HandleProcessedFrame);
            _frameCount = 0;
            _lastFps = 0;
            _articulation = 0;
            _firstFrame = true;
            _fpsTimer.Restart();

            imageView.Realtime.Configure(new RealtimeFrameOptions
            {
                MaxDisplayFps = _config.MaxDisplayFps,
                AutoZoomOnFirstFrame = true,
                UpdateImageMetadata = true
            });

            if (!_overlaysAdded)
            {
                imageView.Realtime.AddOverlayVisual(_roiVisual);
                imageView.Realtime.AddOverlayVisual(_statusVisual);
                _overlaysAdded = true;
            }

            if (imageView.PseudoColorService.IsEnabled)
            {
                _config.IsUseCacheFile = true;
                _config.IsCalArtculation = true;
            }

            UpdateStatusText();
        }

        public void Stop(bool resetRealtime = false, bool clearImageSource = false)
        {
            _fpsTimer.Stop();
            ImageView? imageView = _imageView;
            _imageView = null;

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
                    imageView.Realtime.RemoveOverlayVisual(_roiVisual);
                    imageView.Realtime.RemoveOverlayVisual(_statusVisual);
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
            if (length <= 0 || length > sourceBuffer.Length || !_isActive())
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
            if (sourcePointer == IntPtr.Zero || length <= 0 || !_isActive())
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
            if (imageView == null)
            {
                return;
            }

            bool enablePseudo = imageView.PseudoColorService.IsEnabled;
            if (enablePseudo)
            {
                _config.IsUseCacheFile = true;
            }

            if (TryBuildProcessingRequest(imageView, layout.Width, layout.Height, enablePseudo, out VideoFrameProcessingRequest? request))
            {
                submitProcessing(request);
            }

            if (!enablePseudo)
            {
                submitDisplay(imageView);
                if (_firstFrame)
                {
                    _firstFrame = false;
                }
            }

            RecordFrameRate();
        }

        private bool TryBuildProcessingRequest(ImageView imageView, int width, int height, bool enablePseudo, out VideoFrameProcessingRequest? request)
        {
            request = null;

            bool enableArticulation = _config.IsCalArtculation;
            if (!_config.IsUseCacheFile || (!enablePseudo && !enableArticulation))
            {
                return false;
            }

            Rect rect = _roiVisual.Rect;
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                rect = new Rect(0, 0, width, height);
            }

            PseudoColorFrameRequest? pseudoColorRequest = null;
            if (enablePseudo && imageView.PseudoColorService.TryCreateRequest(out var capturedRequest, 0))
            {
                pseudoColorRequest = capturedRequest;
            }

            request = new VideoFrameProcessingRequest
            {
                EnableArticulation = enableArticulation,
                FocusAlgorithm = _config.EvaFunc,
                Roi = new RoiRect(rect),
                PseudoColor = pseudoColorRequest
            };
            return true;
        }

        private void HandleProcessedFrame(VideoFrameProcessingResult result)
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                ImageView? imageView = _imageView;
                if (!_isActive() || imageView == null)
                {
                    DisposePseudoImage(result.PseudoImage);
                    return;
                }

                if (result.Articulation is double articulation)
                {
                    _articulation = articulation;
                    _articulationUpdated?.Invoke(articulation);
                }

                if (result.PseudoImage is HImage pseudoImage)
                {
                    if (imageView.PseudoColorService.IsEnabled)
                    {
                        VideoFrameUiHelper.ApplyPseudoImage(imageView.PseudoColorService, pseudoImage);
                        if (_firstFrame)
                        {
                            _firstFrame = false;
                            _firstPseudoFrameDisplayed?.Invoke(imageView);
                        }
                    }
                    else
                    {
                        pseudoImage.Dispose();
                    }
                }

                UpdateStatusText();
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
            _fpsUpdated?.Invoke(_lastFps);
            UpdateStatusText();
        }

        private void UpdateStatusText()
        {
            if (_statusTextFormatter == null)
            {
                return;
            }

            _statusVisual.Attribute.Text = _statusTextFormatter(new CameraRealtimePipelineState(_articulation, _lastFps));
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

        private static void DisposePseudoImage(HImage? image)
        {
            if (image is HImage pseudoImage)
            {
                pseudoImage.Dispose();
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