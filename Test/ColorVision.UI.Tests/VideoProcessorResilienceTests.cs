using ColorVision.Core;
using ColorVision.Engine.Services.Devices.Camera.Video;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class VideoProcessorResilienceTests
{
    [Fact]
    public void FrameProcessorContinuesAfterProcessingException()
    {
        using var firstAttempt = new ManualResetEventSlim();
        using var resultReceived = new ManualResetEventSlim();
        int attempts = 0;
        using var processor = new CameraFocusFrameProcessor(
            result =>
            {
                if (result.Articulation == 42)
                {
                    resultReceived.Set();
                }
            },
            (_, _) =>
            {
                if (Interlocked.Increment(ref attempts) == 1)
                {
                    firstAttempt.Set();
                    throw new InvalidOperationException("simulated frame failure");
                }

                return new CameraFocusFrameResult(42);
            });
        byte[] frame = [0];
        var request = new CameraFocusFrameRequest(default, new RoiRect(0, 0, 1, 1));

        processor.SubmitFrame(frame, frame.Length, 1, 1, 1, 8, 1, request);
        Assert.True(firstAttempt.Wait(TimeSpan.FromSeconds(5)));
        processor.SubmitFrame(frame, frame.Length, 1, 1, 1, 8, 1, request);

        Assert.True(resultReceived.Wait(TimeSpan.FromSeconds(5)));
        Assert.Equal(2, Volatile.Read(ref attempts));
    }

    [Theory]
    [InlineData(8, 1, false)]
    [InlineData(16, 1, true)]
    [InlineData(16, 3, false)]
    [InlineData(8, 4, true)]
    public async Task FocusUsesHaloAndPreservesNativeScoreWithPaddedRows(int depth, int channels, bool edge)
    {
        const int width = 17, height = 11;
        int pixelBytes = channels * depth / 8;
        int rowBytes = width * pixelBytes;
        int stride = rowBytes + 8;
        byte[] samples = new byte[(height - 1) * stride + rowBytes];
        new Random(1729).NextBytes(samples);
        RoiRect roi = edge ? new(-3, 6, 9, 9) : new(4, 3, 7, 5);
        var request = new CameraFocusFrameRequest(FocusAlgorithm.VarianceOfLaplacian, roi);
        using HImage original = new()
        {
            cols = width,
            rows = height,
            channels = channels,
            depth = depth,
            stride = stride,
            pData = Marshal.AllocCoTaskMem(samples.Length)
        };
        Marshal.Copy(samples, 0, original.pData, samples.Length);
        double expected = OpenCVMediaHelper.M_CalArtculation(original, request.FocusAlgorithm, roi);
        Assert.True(expected > 0);

        TaskCompletionSource<double> published = new(TaskCreationOptions.RunContinuationsAsynchronously);
        (int Width, int Height, int Stride, RoiRect Roi) observed = default;
        var processor = new CameraFocusFrameProcessor(result => published.TrySetResult(result.Articulation), (image, local) =>
        {
            observed = (image.cols, image.rows, image.stride, local.Roi);
            return new(OpenCVMediaHelper.M_CalArtculation(image, local.FocusAlgorithm, local.Roi));
        });
        try
        {
            if (edge) processor.SubmitFrame(original.pData, samples.Length, width, height, channels, depth, stride, request);
            else processor.SubmitFrame(samples, samples.Length, width, height, channels, depth, stride, request);

            double actual = await published.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
            if (channels == 4) Assert.Equal((width, height, stride, roi), observed);
            else if (edge) Assert.Equal((7, 6, 7 * pixelBytes, new RoiRect(0, 1, 6, 5)), observed);
            else Assert.Equal((9, 7, 9 * pixelBytes, new RoiRect(1, 1, 7, 5)), observed);
        }
        finally
        {
            processor.Dispose();
            await processor.Retirement.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task FocusRejectsMalformedLayoutsBeforeTakingOwnershipOfAnotherFrame()
    {
        TimeSpan testWait = TimeSpan.FromSeconds(5);
        TaskCompletionSource processingStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim finishProcessing = new();
        var processor = new CameraFocusFrameProcessor(_ => { }, (_, _) =>
        {
            processingStarted.SetResult();
            finishProcessing.Wait();
            return default;
        });
        byte[] frame = new byte[16];
        var request = new CameraFocusFrameRequest(FocusAlgorithm.Variance, new RoiRect(0, 0, 1, 1));
        try
        {
            processor.SubmitFrame(frame, 12, 3, 2, 1, 16, 0, request);
            await processingStarted.Task.WaitAsync(testWait);
            processor.SubmitFrame(frame, frame.Length, 3, 2, 1, 16, -8, request);
            processor.SubmitFrame(frame, frame.Length, 3, 2, 1, 16, 4, request);
            processor.SubmitFrame(frame, frame.Length, 3, 2, 1, 16, 7, request);
            processor.SubmitFrame(frame, 13, 3, 2, 1, 16, 8, request);
            processor.SubmitFrame(frame, frame.Length, int.MaxValue, 2, int.MaxValue, int.MaxValue - 7, 0, request);

            // The first callback holds the worker so an invalid frame could not have been consumed.
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Assert.Null(typeof(CameraFocusFrameProcessor).GetField("_pendingFrame", flags)!.GetValue(processor));
        }
        finally
        {
            processor.Dispose();
            finishProcessing.Set();
            await processor.Retirement.WaitAsync(testWait);
        }
    }

    [Fact]
    public void CrossGuideProcessorContinuesAfterProcessingException()
    {
        using var firstAttempt = new ManualResetEventSlim();
        using var resultReceived = new ManualResetEventSlim();
        int attempts = 0;
        IntPtr frame = Marshal.AllocCoTaskMem(1);
        try
        {
            Marshal.WriteByte(frame, 0);
            using var processor = new VideoCrossGuideProcessor(
                _ => resultReceived.Set(),
                (_, _, _, _, _) =>
                {
                    if (Interlocked.Increment(ref attempts) == 1)
                    {
                        firstAttempt.Set();
                        throw new InvalidOperationException("simulated cross-guide failure");
                    }

                    return default;
                });
            var request = new VideoCrossGuideRequest(
                new RoiRect(0, 0, 1, 1),
                new Point(),
                0,
                50,
                0.5,
                0.1,
                1);

            processor.SubmitFrame(frame, 1, 1, 1, 1, 8, 1, request);
            Assert.True(firstAttempt.Wait(TimeSpan.FromSeconds(5)));
            processor.Reset();
            processor.SubmitFrame(frame, 1, 1, 1, 1, 8, 1, request);

            Assert.True(resultReceived.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(2, Volatile.Read(ref attempts));
        }
        finally
        {
            Marshal.FreeCoTaskMem(frame);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CrossGuideStopReleasesBuffersAfterInFlightDetectionAndCanRestart(bool processingFails)
    {
        using var processingStarted = new ManualResetEventSlim();
        using var finishProcessing = new ManualResetEventSlim();
        using var resultReceived = new ManualResetEventSlim();
        int attempts = 0;
        int results = 0;
        int sampleAfterStop = 0;
        IntPtr frame = Marshal.AllocCoTaskMem(1);
        VideoCrossGuideProcessor? processor = null;
        try
        {
            Marshal.WriteByte(frame, 17);
            processor = new VideoCrossGuideProcessor(
                _ =>
                {
                    Interlocked.Increment(ref results);
                    resultReceived.Set();
                },
                (image, _, _, _, _) =>
                {
                    if (Interlocked.Increment(ref attempts) == 1)
                    {
                        processingStarted.Set();
                        finishProcessing.Wait();
                        Interlocked.Exchange(ref sampleAfterStop, Marshal.ReadByte(image.pData));
                        if (processingFails) throw new InvalidOperationException("simulated in-flight failure");
                    }
                    return default;
                });
            var request = new VideoCrossGuideRequest(new RoiRect(0, 0, 1, 1), new Point(), 0, 50, 0.5, 0.1, 1);

            processor.SubmitFrame(frame, 1, 1, 1, 1, 8, 1, request);
            Assert.True(processingStarted.Wait(TimeSpan.FromSeconds(5)));
            processor.Reset();
            processor.SubmitFrame(frame, 1, 1, 1, 1, 8, 1, request);
            Assert.NotNull(ReadCrossGuideBuffers(processor).Pending);

            processor.Stop();
            processor.Stop();
            Assert.Null(ReadCrossGuideBuffers(processor).Pending);
            Assert.NotNull(ReadCrossGuideBuffers(processor).Working);
            processor.SubmitFrame(frame, 1, 1, 1, 1, 8, 1, request);
            Assert.Null(ReadCrossGuideBuffers(processor).Pending);

            finishProcessing.Set();
            Assert.True(SpinWait.SpinUntil(() => ReadCrossGuideBuffers(processor) is (null, null), TimeSpan.FromSeconds(5)));
            Assert.Equal(17, Volatile.Read(ref sampleAfterStop));
            Assert.Equal(0, Volatile.Read(ref results));
            Assert.Equal(1, Volatile.Read(ref attempts));

            processor.Start();
            processor.SubmitFrame(frame, 1, 1, 1, 1, 8, 1, request);
            Assert.True(resultReceived.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(2, Volatile.Read(ref attempts));
            Assert.Equal(1, Volatile.Read(ref results));
            processor.Stop();
            Assert.True(SpinWait.SpinUntil(() => ReadCrossGuideBuffers(processor) is (null, null), TimeSpan.FromSeconds(5)));
        }
        finally
        {
            finishProcessing.Set();
            processor?.Dispose();
            Marshal.FreeCoTaskMem(frame);
        }
    }

    [Fact]
    public async Task CrossGuideRejectsFramesFromRetiredPreviewAndInvalidatesQueuedResults()
    {
        using var firstPreview = new CancellationTokenSource();
        var firstResult = new TaskCompletionSource<VideoCrossGuideResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondResult = new TaskCompletionSource<VideoCrossGuideResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        int results = 0;
        IntPtr pixels = Marshal.AllocCoTaskMem(1);
        var processor = new VideoCrossGuideProcessor(result =>
        {
            if (Interlocked.Increment(ref results) == 1) firstResult.TrySetResult(result);
            else secondResult.TrySetResult(result);
        }, (frame, _, _, _, _) => default(VideoCrossGuideResult) with { FrameWidth = Marshal.ReadByte(frame.pData) });
        TimeSpan timeout = TimeSpan.FromSeconds(10);
        var request = new VideoCrossGuideRequest(new RoiRect(0, 0, 1, 1), new Point(), 0, 50, 0.5, 0.1, 1);
        try
        {
            long oldGeneration = processor.CaptureGeneration();
            Submit(17, firstPreview.Token, oldGeneration);
            VideoCrossGuideResult first = await firstResult.Task.WaitAsync(timeout);
            Assert.True(processor.IsCurrentResult(first));
            firstPreview.Cancel();
            Assert.False(processor.IsCurrentResult(first));

            processor.Reset();
            long currentGeneration = processor.CaptureGeneration();
            Submit(29, CancellationToken.None, oldGeneration);
            Submit(37, firstPreview.Token, currentGeneration);
            Submit(71, CancellationToken.None, currentGeneration);
            VideoCrossGuideResult second = await secondResult.Task.WaitAsync(timeout);
            Assert.Equal(71, second.FrameWidth);
            Assert.True(processor.IsCurrentResult(second));
            processor.Reset();
            Assert.False(processor.IsCurrentResult(second));
        }
        finally
        {
            processor.Dispose();
            try { await processor.Retirement.WaitAsync(timeout); }
            finally { Marshal.FreeCoTaskMem(pixels); }
        }

        void Submit(byte value, CancellationToken lifetime, long generation)
        {
            Marshal.WriteByte(pixels, value);
            processor.SubmitFrame(pixels, 1, 1, 1, 1, 8, 1, request, lifetime, generation);
        }
    }

    [Fact]
    public async Task CrossGuideDisposeReturnsOnDispatcherBeforeRetiringTheBlockedDetection()
    {
        var dispatcher = WpfTestHost.Invoke(() => Dispatcher.CurrentDispatcher);
        TimeSpan testWait = TimeSpan.FromSeconds(10);
        TaskCompletionSource processingStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim finishProcessing = new();
        int attempts = 0;
        int results = 0;
        int sampleAfterDispose = -1;
        IntPtr frame = Marshal.AllocCoTaskMem(1);
        var processor = new VideoCrossGuideProcessor(
            _ => Interlocked.Increment(ref results),
            (image, _, _, _, _) =>
            {
                Interlocked.Increment(ref attempts);
                processingStarted.SetResult();
                if (!finishProcessing.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("Cross-guide gate was not released.");
                sampleAfterDispose = Marshal.ReadByte(image.pData);
                return default;
            });
        var request = new VideoCrossGuideRequest(new RoiRect(0, 0, 1, 1), new Point(), 0, 50, 0.5, 0.1, 1);

        try
        {
            Marshal.WriteByte(frame, 17);
            processor.SubmitFrame(frame, 1, 1, 1, 1, 8, 1, request);
            await processingStarted.Task.WaitAsync(testWait);
            Marshal.WriteByte(frame, 99);

            await dispatcher.InvokeAsync(processor.Dispose).Task.WaitAsync(testWait);
            await dispatcher.InvokeAsync(() =>
            {
                processor.Dispose();
                processor.Reset();
                processor.Stop();
                processor.Start();
                Assert.False(processor.Retirement.IsCompleted);
                Assert.NotNull(ReadCrossGuideBuffers(processor).Working);
            }).Task.WaitAsync(testWait);
            processor.SubmitFrame(frame, 1, 1, 1, 1, 8, 1, request);
            Assert.Null(ReadCrossGuideBuffers(processor).Pending);

            finishProcessing.Set();
            await processor.Retirement.WaitAsync(testWait);
            Assert.True(ReadCrossGuideBuffers(processor) is (null, null));
            Assert.Equal(17, sampleAfterDispose);
            Assert.Equal(1, Volatile.Read(ref attempts));
            Assert.Equal(0, Volatile.Read(ref results));
        }
        finally
        {
            finishProcessing.Set();
            processor.Dispose();
            try { await processor.Retirement.WaitAsync(testWait); }
            finally { Marshal.FreeCoTaskMem(frame); }
        }
    }

    private static (HImage? Pending, HImage? Working) ReadCrossGuideBuffers(VideoCrossGuideProcessor processor)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Type type = typeof(VideoCrossGuideProcessor);
        lock (type.GetField("_gate", flags)!.GetValue(processor)!)
        {
            return ((HImage?)type.GetField("_pendingFrame", flags)!.GetValue(processor),
                (HImage?)type.GetField("_workingFrame", flags)!.GetValue(processor));
        }
    }
}
