using ColorVision.Core;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Realtime;
using log4net;
using OpenCvSharp;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Devices.Camera.Video
{
    internal readonly record struct VideoFrameProcessingRequest(
        bool EnableArticulation,
        FocusAlgorithm FocusAlgorithm,
        RoiRect Roi,
        RealtimePseudoColorRequest? PseudoColor,
        int Transform)
    {
        public bool NeedsProcessing => EnableArticulation || PseudoColor.HasValue;
    }

    internal readonly record struct VideoFrameProcessingResult(
        double? Articulation,
        HImage? PseudoImage,
        RealtimePseudoColorRequest? PseudoColorRequest);

    internal sealed class VideoFrameProcessor : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(VideoFrameProcessor));
        private readonly object _gate = new();
        private readonly AutoResetEvent _frameReady = new(false);
        private readonly CancellationTokenSource _cts = new();
        private readonly Action<VideoFrameProcessingResult> _resultHandler;
        private readonly Func<HImage, VideoFrameProcessingRequest, VideoFrameProcessingResult> _frameProcessor;
        private readonly Task _workerTask;

        private HImage? _pendingFrame;
        private HImage? _workingFrame;
        private int _pendingCapacity;
        private int _workingCapacity;
        private VideoFrameProcessingRequest _pendingRequest;
        private bool _hasPendingFrame;
        private bool _disposed;

        public VideoFrameProcessor(Action<VideoFrameProcessingResult> resultHandler)
            : this(resultHandler, ProcessFrame)
        {
        }

        internal VideoFrameProcessor(
            Action<VideoFrameProcessingResult> resultHandler,
            Func<HImage, VideoFrameProcessingRequest, VideoFrameProcessingResult> frameProcessor)
        {
            ArgumentNullException.ThrowIfNull(resultHandler);
            ArgumentNullException.ThrowIfNull(frameProcessor);
            _resultHandler = resultHandler;
            _frameProcessor = frameProcessor;
            _workerTask = Task.Factory.StartNew(
                WorkerLoop,
                _cts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        public void SubmitFrame(byte[] sourceBuffer, int length, int width, int height, int channels, int depth, int stride, VideoFrameProcessingRequest request)
        {
            ArgumentNullException.ThrowIfNull(sourceBuffer);
            if (length <= 0 || length > sourceBuffer.Length || !request.NeedsProcessing || _disposed) return;

            unsafe
            {
                fixed (byte* sourcePtr = sourceBuffer)
                {
                    SubmitFrameCore((IntPtr)sourcePtr, length, width, height, channels, depth, stride, request);
                }
            }
        }

        public void SubmitFrame(IntPtr sourcePointer, int length, int width, int height, int channels, int depth, int stride, VideoFrameProcessingRequest request)
        {
            if (sourcePointer == IntPtr.Zero || length <= 0 || !request.NeedsProcessing || _disposed) return;
            SubmitFrameCore(sourcePointer, length, width, height, channels, depth, stride, request);
        }

        private unsafe void SubmitFrameCore(IntPtr sourcePointer, int length, int width, int height, int channels, int depth, int stride, VideoFrameProcessingRequest request)
        {
            lock (_gate)
            {
                EnsureBuffer(ref _pendingFrame, ref _pendingCapacity, width, height, channels, depth, stride, length);
                Buffer.MemoryCopy((void*)sourcePointer, (void*)_pendingFrame!.Value.pData, _pendingCapacity, length);
                _pendingRequest = request;
                _hasPendingFrame = true;
            }

            _frameReady.Set();
        }

        private void WorkerLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                _frameReady.WaitOne(100);
                if (_cts.IsCancellationRequested)
                {
                    break;
                }

                HImage workingFrame;
                VideoFrameProcessingRequest request;

                lock (_gate)
                {
                    if (!_hasPendingFrame || _pendingFrame == null)
                    {
                        continue;
                    }

                    (_pendingFrame, _workingFrame) = (_workingFrame, _pendingFrame);
                    (_pendingCapacity, _workingCapacity) = (_workingCapacity, _pendingCapacity);
                    request = _pendingRequest;
                    _pendingRequest = default;
                    _hasPendingFrame = false;
                    workingFrame = _workingFrame!.Value;
                }

                VideoFrameProcessingResult result;
                try
                {
                    result = _frameProcessor(workingFrame, request);
                }
                catch (Exception ex)
                {
                    log.Error("Video frame processing failed.", ex);
                    continue;
                }

                try
                {
                    if (_cts.IsCancellationRequested)
                    {
                        DisposePseudoImage(result.PseudoImage);
                        break;
                    }

                    _resultHandler(result);
                }
                catch
                {
                    DisposePseudoImage(result.PseudoImage);
                }
            }
        }

        private static VideoFrameProcessingResult ProcessFrame(HImage frame, VideoFrameProcessingRequest request)
        {
            double? articulation = request.EnableArticulation
                ? OpenCVMediaHelper.M_CalArtculation(frame, request.FocusAlgorithm, request.Roi)
                : null;
            HImage? pseudoImage = null;

            if (request.PseudoColor is RealtimePseudoColorRequest pseudoRequest)
            {
                PseudoColorFrameRequest parameters = pseudoRequest.FrameRequest;
                int ret = parameters.HasValidAutoRange
                    ? OpenCVMediaHelper.ApplyPseudoColorAutoRange(frame, out HImage processedImage, parameters.Min, parameters.Max, parameters.ColormapTypes, parameters.Channel, parameters.DataMin, parameters.DataMax)
                    : OpenCVMediaHelper.ApplyPseudoColor(frame, out processedImage, parameters.Min, parameters.Max, parameters.ColormapTypes, parameters.Channel);
                if (ret == 0)
                {
                    try
                    {
                        ApplyTransform(processedImage, request.Transform);
                        pseudoImage = processedImage;
                    }
                    catch
                    {
                        processedImage.Dispose();
                        throw;
                    }
                }
            }

            return new VideoFrameProcessingResult(articulation, pseudoImage, request.PseudoColor);
        }

        internal static void ApplyTransform(HImage image, int transform)
        {
            if (transform == RealtimeFramePresenter.TransformNone || image.pData == IntPtr.Zero) return;

            FlipMode flipMode = transform switch
            {
                RealtimeFramePresenter.TransformFlipX => FlipMode.X,
                RealtimeFramePresenter.TransformFlipY => FlipMode.Y,
                RealtimeFramePresenter.TransformFlipXY => FlipMode.XY,
                _ => throw new ArgumentOutOfRangeException(nameof(transform), transform, "Unsupported realtime frame transform."),
            };
            MatType type = (image.depth, image.channels) switch
            {
                (8, 1) => MatType.CV_8UC1,
                (8, 3) => MatType.CV_8UC3,
                (8, 4) => MatType.CV_8UC4,
                _ => throw new NotSupportedException($"Realtime pseudo color transform does not support {image.depth}-bit, {image.channels}-channel images."),
            };

            using Mat mat = Mat.FromPixelData(image.rows, image.cols, type, image.pData, image.stride);
            Cv2.Flip(mat, mat, flipMode);
        }

        private static void DisposePseudoImage(HImage? image)
        {
            if (image is HImage pseudoImage)
            {
                pseudoImage.Dispose();
            }
        }

        private static void EnsureBuffer(ref HImage? buffer, ref int capacity, int width, int height, int channels, int depth, int stride, int requiredLength)
        {
            bool needsAllocation = buffer == null
                || capacity < requiredLength
                || buffer.Value.cols != width
                || buffer.Value.rows != height
                || buffer.Value.channels != channels
                || buffer.Value.depth != depth
                || buffer.Value.stride != stride;

            if (needsAllocation)
            {
                buffer?.Dispose();
                buffer = new HImage
                {
                    rows = height,
                    cols = width,
                    channels = channels,
                    depth = depth,
                    stride = stride,
                    pData = Marshal.AllocCoTaskMem(requiredLength)
                };
                capacity = requiredLength;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cts.Cancel();
            _frameReady.Set();

            try
            {
                _workerTask.Wait();
            }
            catch (AggregateException)
            {
            }

            _pendingFrame?.Dispose();
            _workingFrame?.Dispose();
            _frameReady.Dispose();
            _cts.Dispose();
        }
    }
}
