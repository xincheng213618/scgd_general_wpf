using ColorVision.Core;
using ColorVision.Engine.Services.Devices.Camera.Video;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace ColorVision.UI.Tests;

public sealed class VideoProcessorResilienceTests
{
    [Fact]
    public void FrameProcessorContinuesAfterProcessingException()
    {
        using var firstAttempt = new ManualResetEventSlim();
        using var resultReceived = new ManualResetEventSlim();
        int attempts = 0;
        using var processor = new VideoFrameProcessor(
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

                return new VideoFrameProcessingResult(42);
            });
        byte[] frame = [0];
        var request = new VideoFrameProcessingRequest(default, new RoiRect(0, 0, 1, 1));

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
}
