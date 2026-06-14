using ColorVision.Core;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Devices.Camera.Video
{
    internal readonly record struct VideoFrameProcessingRequest(FocusAlgorithm FocusAlgorithm, RoiRect Roi);

    internal readonly record struct VideoFrameProcessingResult(double Articulation);

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
        private VideoFrameProcessingRequest _pendingRequest;
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
            ArgumentNullException.ThrowIfNull(sourceBuffer);
            if (length <= 0 || length > sourceBuffer.Length || _disposed) return;

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
            if (sourcePointer == IntPtr.Zero || length <= 0 || _disposed) return;
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

                VideoFrameProcessingResult result = ProcessFrame(workingFrame, request);

                try
                {
                    if (_cts.IsCancellationRequested)
                    {
                        break;
                    }

                    _resultHandler(result);
                }
                catch
                {
                }
            }
        }

        private static VideoFrameProcessingResult ProcessFrame(HImage frame, VideoFrameProcessingRequest request)
            => new(OpenCVMediaHelper.M_CalArtculation(frame, request.FocusAlgorithm, request.Roi));

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
