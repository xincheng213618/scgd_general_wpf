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

                return new VideoFrameProcessingResult(42, null, null);
            });
        byte[] frame = [0];
        var request = new VideoFrameProcessingRequest(true, default, new RoiRect(0, 0, 1, 1), null, 0);

        processor.SubmitFrame(frame, frame.Length, 1, 1, 1, 8, 1, request);
        Assert.True(firstAttempt.Wait(TimeSpan.FromSeconds(5)));
        processor.SubmitFrame(frame, frame.Length, 1, 1, 1, 8, 1, request);

        Assert.True(resultReceived.Wait(TimeSpan.FromSeconds(5)));
        Assert.Equal(2, Volatile.Read(ref attempts));
    }

    [Theory]
    [InlineData(1, new byte[] { 3, 4, 1, 2 })]
    [InlineData(2, new byte[] { 2, 1, 4, 3 })]
    [InlineData(3, new byte[] { 4, 3, 2, 1 })]
    public void PseudoColorTransformMatchesRealtimeDisplayTransform(int transform, byte[] expected)
    {
        HImage image = OpenCVMediaHelper.AllocateHImage(2, 2, 1, 8);
        try
        {
            Marshal.Copy(new byte[] { 1, 2, 3, 4 }, 0, image.pData, 4);

            VideoFrameProcessor.ApplyTransform(image, transform);

            byte[] actual = new byte[4];
            Marshal.Copy(image.pData, actual, 0, actual.Length);
            Assert.Equal(expected, actual);
        }
        finally
        {
            image.Dispose();
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
}
