using ColorVision.Core;
using ColorVision.Engine.Media;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Abstractions;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Devices.Camera.Video
{
    internal sealed class VideoFrameProcessingRequest
    {
        public bool EnableArticulation { get; init; }
        public FocusAlgorithm FocusAlgorithm { get; init; }
        public RoiRect Roi { get; init; }
        public PseudoColorFrameRequest? PseudoColor { get; init; }

        public bool NeedsProcessing => EnableArticulation || PseudoColor.HasValue;
    }

    internal sealed class VideoFrameProcessingResult
    {
        public double? Articulation { get; init; }
        public HImage? PseudoImage { get; init; }
    }

    internal sealed class VideoFrameProcessor : IDisposable
    {
        private readonly object _gate = new();
        private readonly AutoResetEvent _frameReady = new(false);
        private readonly CancellationTokenSource _cts = new();
        private readonly Action<VideoFrameProcessingResult> _resultHandler;
        private readonly Task _workerTask;

        private HImage? _pendingFrame;
        private HImage? _workingFrame;
        private int _pendingCapacity;
        private int _workingCapacity;
        private VideoFrameProcessingRequest? _pendingRequest;
        private bool _hasPendingFrame;
        private bool _disposed;

        public VideoFrameProcessor(Action<VideoFrameProcessingResult> resultHandler)
        {
            ArgumentNullException.ThrowIfNull(resultHandler);
            _resultHandler = resultHandler;
            _workerTask = Task.Factory.StartNew(
                WorkerLoop,
                _cts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        public void SubmitFrame(byte[] sourceBuffer, int length, int width, int height, int channels, int depth, int stride, VideoFrameProcessingRequest request)
        {
            if (sourceBuffer == null) throw new ArgumentNullException(nameof(sourceBuffer));
            if (length <= 0 || length > sourceBuffer.Length || request == null || !request.NeedsProcessing || _disposed) return;

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
            if (sourcePointer == IntPtr.Zero || length <= 0 || request == null || !request.NeedsProcessing || _disposed) return;
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
                VideoFrameProcessingRequest? request;

                lock (_gate)
                {
                    if (!_hasPendingFrame || _pendingFrame == null || _pendingRequest == null)
                    {
                        continue;
                    }

                    (_pendingFrame, _workingFrame) = (_workingFrame, _pendingFrame);
                    (_pendingCapacity, _workingCapacity) = (_workingCapacity, _pendingCapacity);
                    request = _pendingRequest;
                    _pendingRequest = null;
                    _hasPendingFrame = false;
                    workingFrame = _workingFrame!.Value;
                }

                VideoFrameProcessingResult result = ProcessFrame(workingFrame, request);

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
            double? articulation = null;
            HImage? pseudoImage = null;

            if (request.EnableArticulation)
            {
                articulation = OpenCVMediaHelper.M_CalArtculation(frame, request.FocusAlgorithm, request.Roi);
            }

            if (request.PseudoColor is PseudoColorFrameRequest pseudoColor)
            {
                int ret;
                if (pseudoColor.HasValidAutoRange)
                {
                    ret = OpenCVMediaHelper.M_PseudoColorAutoRange(frame, out HImage processedImage, pseudoColor.Min, pseudoColor.Max, pseudoColor.ColormapTypes, pseudoColor.Channel, pseudoColor.DataMin, pseudoColor.DataMax);
                    if (ret == 0)
                    {
                        pseudoImage = processedImage;
                    }
                }
                else
                {
                    ret = OpenCVMediaHelper.M_PseudoColor(frame, out HImage processedImage, pseudoColor.Min, pseudoColor.Max, pseudoColor.ColormapTypes, pseudoColor.Channel);
                    if (ret == 0)
                    {
                        pseudoImage = processedImage;
                    }
                }
            }

            return new VideoFrameProcessingResult
            {
                Articulation = articulation,
                PseudoImage = pseudoImage
            };
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
                    pData = Marshal.AllocHGlobal(requiredLength)
                };
                capacity = requiredLength;
            }
        }

        private static void DisposePseudoImage(HImage? image)
        {
            if (image is HImage pseudoImage)
            {
                pseudoImage.Dispose();
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

    internal static class VideoFrameUiHelper
    {
        public static void ApplyPseudoImage(IPseudoColorService pseudoColorService, HImage pseudoImage)
        {
            pseudoColorService.ApplyProcessedImage(pseudoImage);
        }
    }
}