using ColorVision.Core;
using log4net;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Devices.Camera.Video
{
    internal readonly record struct CameraFocusFrameRequest(FocusAlgorithm FocusAlgorithm, RoiRect Roi);
    internal readonly record struct CameraFocusFrameResult(double Articulation);

    internal sealed class CameraFocusFrameProcessor : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CameraFocusFrameProcessor));
        private readonly object _gate = new();
        private readonly AutoResetEvent _frameReady = new(false);
        private readonly TaskCompletionSource _retirement = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Action<CameraFocusFrameResult> _resultHandler;
        private readonly Func<HImage, CameraFocusFrameRequest, CameraFocusFrameResult> _frameProcessor;
        private readonly Task _workerTask;

        private HImage? _pendingFrame;
        private HImage? _workingFrame;
        private int _pendingCapacity;
        private int _workingCapacity;
        private CameraFocusFrameRequest _pendingRequest;
        private bool _hasPendingFrame;
        private int _stopRequested;

        internal Task Retirement => _retirement.Task;

        public CameraFocusFrameProcessor(Action<CameraFocusFrameResult> resultHandler)
            : this(resultHandler, ProcessFrame)
        {
        }

        internal CameraFocusFrameProcessor(
            Action<CameraFocusFrameResult> resultHandler,
            Func<HImage, CameraFocusFrameRequest, CameraFocusFrameResult> frameProcessor)
        {
            ArgumentNullException.ThrowIfNull(resultHandler);
            ArgumentNullException.ThrowIfNull(frameProcessor);
            _resultHandler = resultHandler;
            _frameProcessor = frameProcessor;
            _workerTask = Task.Factory.StartNew(
                WorkerLoop,
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        public void SubmitFrame(byte[] sourceBuffer, int length, int width, int height, int channels, int depth, int stride, CameraFocusFrameRequest request)
        {
            ArgumentNullException.ThrowIfNull(sourceBuffer);
            if (length <= 0 || length > sourceBuffer.Length || Volatile.Read(ref _stopRequested) != 0) return;

            unsafe
            {
                fixed (byte* sourcePtr = sourceBuffer)
                {
                    SubmitFrameCore((IntPtr)sourcePtr, length, width, height, channels, depth, stride, request);
                }
            }
        }

        public void SubmitFrame(IntPtr sourcePointer, int length, int width, int height, int channels, int depth, int stride, CameraFocusFrameRequest request)
        {
            if (sourcePointer == IntPtr.Zero || length <= 0 || Volatile.Read(ref _stopRequested) != 0) return;
            SubmitFrameCore(sourcePointer, length, width, height, channels, depth, stride, request);
        }

        private unsafe void SubmitFrameCore(IntPtr sourcePointer, int length, int width, int height, int channels, int depth, int stride, CameraFocusFrameRequest request)
        {
            lock (_gate)
            {
                if (Volatile.Read(ref _stopRequested) != 0) return;
                EnsureBuffer(ref _pendingFrame, ref _pendingCapacity, width, height, channels, depth, stride, length);
                Buffer.MemoryCopy((void*)sourcePointer, (void*)_pendingFrame!.Value.pData, _pendingCapacity, length);
                _pendingRequest = request;
                _hasPendingFrame = true;
                _frameReady.Set();
            }
        }

        private void WorkerLoop()
        {
            while (Volatile.Read(ref _stopRequested) == 0)
            {
                _frameReady.WaitOne();

                HImage workingFrame;
                CameraFocusFrameRequest request;

                lock (_gate)
                {
                    if (Volatile.Read(ref _stopRequested) != 0) break;
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

                CameraFocusFrameResult result;
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
                    if (Volatile.Read(ref _stopRequested) != 0)
                    {
                        break;
                    }

                    _resultHandler(result);
                }
                catch (Exception ex)
                {
                    log.Warn("Unable to publish camera focus metric.", ex);
                }
            }
        }

        private static CameraFocusFrameResult ProcessFrame(HImage frame, CameraFocusFrameRequest request)
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
            if (Interlocked.Exchange(ref _stopRequested, 1) != 0) return;
            // A camera callback may be copying a full frame and the worker may be in native code.
            _frameReady.Set();
            _ = Task.Run(FinishRetirementAsync);
        }

        private async Task FinishRetirementAsync()
        {
            try
            {
                await _workerTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log.Warn("Camera focus worker stopped unexpectedly.", ex);
            }
            lock (_gate)
            {
                _pendingFrame?.Dispose();
                _workingFrame?.Dispose();
                _pendingFrame = null;
                _workingFrame = null;
                _frameReady.Dispose();
            }
            _retirement.SetResult();
        }
    }
}
