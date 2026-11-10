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
