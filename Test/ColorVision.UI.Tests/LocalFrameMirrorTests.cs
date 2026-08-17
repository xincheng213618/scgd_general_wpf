using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using cvColorVision;
using FlowEngineLib.Algorithm;
using System.Runtime.InteropServices;

namespace ColorVision.UI.Tests;

public sealed class LocalFrameMirrorTests
{
    [Fact]
    public void ApplyPendingFlipsOnlyPrimaryCieAndIsIdempotent()
    {
        LocalFrameMetadata metadata = new()
        {
            Width = 3,
            Height = 2,
            SourceBpp = 16,
            Channels = 3,
            PrimaryBufferKind = LocalFrameBufferKind.CvCie,
            FlipMode = CVImageFlipMode.Y,
            IsMirrorReady = true
        };
        using LocalFlowFrame frame = LocalFlowFrame.Allocate(metadata, 3 * 2 * 3 * sizeof(short), 3 * 2 * 3 * sizeof(float));
        short[] raw = Enumerable.Range(1, 18).Select(value => (short)value).ToArray();
        float[] cie = Enumerable.Range(1, 18).Select(value => (float)value).ToArray();
        using (LocalFlowFrameLease lease = frame.Acquire())
        {
            Marshal.Copy(raw, 0, lease.RawPointer, raw.Length);
            Marshal.Copy(cie, 0, lease.CiePointer, cie.Length);
        }

        LocalFrameMirrorService.ApplyPending(frame);
        LocalFrameMirrorService.ApplyPending(frame);

        using LocalFlowFrameLease result = frame.Acquire();
        Assert.Equal(raw, ReadInt16(result.RawPointer, raw.Length));
        Assert.Equal(
            new float[]
            {
                3, 2, 1, 6, 5, 4,
                9, 8, 7, 12, 11, 10,
                15, 14, 13, 18, 17, 16
            },
            ReadSingle(result.CiePointer, cie.Length));
        Assert.True(frame.IsFlipApplied);
        Assert.True(frame.IsCieFlipApplied);
        Assert.False(frame.IsRawFlipApplied);
    }

    [Theory]
    [InlineData(CVImageFlipMode.X, new short[] { 4, 5, 6, 1, 2, 3 })]
    [InlineData(CVImageFlipMode.Y, new short[] { 3, 2, 1, 6, 5, 4 })]
    [InlineData(CVImageFlipMode.XY, new short[] { 6, 5, 4, 3, 2, 1 })]
    public void ApplyPendingUsesExistingFlipModeSemantics(CVImageFlipMode flipMode, short[] expected)
    {
        LocalFrameMetadata metadata = new()
        {
            Width = 3,
            Height = 2,
            SourceBpp = 16,
            Channels = 1,
            PrimaryBufferKind = LocalFrameBufferKind.CvRaw,
            FlipMode = flipMode,
            IsMirrorReady = true
        };
        using LocalFlowFrame frame = LocalFlowFrame.Allocate(metadata, 3 * 2 * sizeof(short), 0);
        using (LocalFlowFrameLease lease = frame.Acquire())
        {
            Marshal.Copy(new short[] { 1, 2, 3, 4, 5, 6 }, 0, lease.RawPointer, 6);
        }

        LocalFrameMirrorService.ApplyPending(frame);

        using LocalFlowFrameLease result = frame.Acquire();
        Assert.Equal(expected, ReadInt16(result.RawPointer, expected.Length));
        Assert.True(frame.IsRawFlipApplied);
        Assert.False(frame.IsCieFlipApplied);
    }

    [Fact]
    public void ApplyPendingRejectsRawBeforeSpatialCalibration()
    {
        LocalFrameMetadata metadata = new()
        {
            Width = 2,
            Height = 1,
            SourceBpp = 16,
            Channels = 1,
            PrimaryBufferKind = LocalFrameBufferKind.CvRaw,
            FlipMode = CVImageFlipMode.Y,
            IsMirrorReady = false
        };
        using LocalFlowFrame frame = LocalFlowFrame.Allocate(metadata, 2 * sizeof(short), 0);
        using (LocalFlowFrameLease lease = frame.Acquire())
        {
            Marshal.Copy(new short[] { 1, 2 }, 0, lease.RawPointer, 2);
        }

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => LocalFrameMirrorService.ApplyPending(frame));

        Assert.Contains("spatial calibration", error.Message, StringComparison.OrdinalIgnoreCase);
        using LocalFlowFrameLease result = frame.Acquire();
        Assert.Equal(new short[] { 1, 2 }, ReadInt16(result.RawPointer, 2));
        Assert.False(frame.IsFlipApplied);
    }

    [Fact]
    public void FlipStateBelongsToFrameStorageInsteadOfReusableMetadata()
    {
        LocalFrameMetadata metadata = new()
        {
            Width = 2,
            Height = 1,
            SourceBpp = 16,
            Channels = 1,
            PrimaryBufferKind = LocalFrameBufferKind.CvRaw,
            FlipMode = CVImageFlipMode.Y,
            IsMirrorReady = true
        };
        using LocalFlowFrame first = LocalFlowFrame.Allocate(metadata, 2 * sizeof(short), 0);
        using LocalFlowFrame second = LocalFlowFrame.Allocate(metadata, 2 * sizeof(short), 0);
        using (LocalFlowFrameLease lease = first.Acquire())
        {
            Marshal.Copy(new short[] { 1, 2 }, 0, lease.RawPointer, 2);
        }
        using (LocalFlowFrameLease lease = second.Acquire())
        {
            Marshal.Copy(new short[] { 3, 4 }, 0, lease.RawPointer, 2);
        }

        LocalFrameMirrorService.ApplyPending(first);

        Assert.True(first.IsFlipApplied);
        Assert.False(second.IsFlipApplied);
        using LocalFlowFrameLease secondResult = second.Acquire();
        Assert.Equal(new short[] { 3, 4 }, ReadInt16(secondResult.RawPointer, 2));
    }

    [Fact]
    public void ColorOnlyTemplateCanContinueFromMirroredRaw()
    {
        DeviceCameraCalibrationFile color = new("color", CalibrationType.LumFourColor, "color", "color.cfg", "C:\\color.cfg");
        DeviceCameraCalibrationFile basic = new("uniformity", CalibrationType.Uniformity, "uniformity", "uniformity.cfg", "C:\\uniformity.cfg");

        Assert.True(LocalFrameCalibrationService.IsColorOnlyTemplate(new[] { color }));
        Assert.False(LocalFrameCalibrationService.IsColorOnlyTemplate(new[] { basic, color }));
        Assert.False(LocalFrameCalibrationService.IsColorOnlyTemplate(Array.Empty<DeviceCameraCalibrationFile>()));
    }

    [Fact]
    public void PreparingColorCalibrationKeepsRawAndAddsOnlyCieData()
    {
        LocalFrameMetadata metadata = new()
        {
            Width = 2,
            Height = 1,
            SourceBpp = 16,
            Channels = 3,
            SourceFilePath = "source.cvraw",
            PrimaryBufferKind = LocalFrameBufferKind.CvRaw
        };
        using LocalFlowFrame frame = LocalFlowFrame.Allocate(metadata, 2 * 3 * sizeof(short), 0);
        IntPtr rawPointer;
        using (LocalFlowFrameLease before = frame.Acquire())
        {
            rawPointer = before.RawPointer;
        }

        frame.PrepareForCalibration("color", 2 * 3 * sizeof(float), hasBasicCalibration: false);

        using LocalFlowFrameLease after = frame.Acquire();
        Assert.Equal(rawPointer, after.RawPointer);
        Assert.Equal(2 * 3 * sizeof(short), after.RawLength);
        Assert.NotEqual(IntPtr.Zero, after.CiePointer);
        Assert.Equal(2 * 3 * sizeof(float), after.CieLength);
        Assert.Equal(LocalFrameBufferKind.CvCie, after.Metadata.PrimaryBufferKind);
        Assert.Equal("source.cvraw", after.Metadata.SourceFilePath);
    }

    [Fact]
    public void InheritedFlipStatePreventsGeneratedCieFromBeingMirroredTwice()
    {
        LocalFrameMetadata metadata = new()
        {
            Width = 3,
            Height = 1,
            SourceBpp = 16,
            Channels = 1,
            PrimaryBufferKind = LocalFrameBufferKind.CvCie,
            FlipMode = CVImageFlipMode.Y,
            IsMirrorReady = true
        };
        using LocalFlowFrame frame = LocalFlowFrame.Allocate(metadata, 0, 3 * sizeof(float));
        float[] alreadyMirrored = new float[] { 3, 2, 1 };
        using (LocalFlowFrameLease lease = frame.Acquire())
        {
            Marshal.Copy(alreadyMirrored, 0, lease.CiePointer, alreadyMirrored.Length);
        }

        frame.MarkPrimaryBufferFlipApplied();
        LocalFrameMirrorService.ApplyPending(frame);

        using LocalFlowFrameLease result = frame.Acquire();
        Assert.Equal(alreadyMirrored, ReadSingle(result.CiePointer, alreadyMirrored.Length));
        Assert.True(frame.IsCieFlipApplied);
    }

    private static short[] ReadInt16(IntPtr pointer, int length)
    {
        short[] result = new short[length];
        Marshal.Copy(pointer, result, 0, result.Length);
        return result;
    }

    private static float[] ReadSingle(IntPtr pointer, int length)
    {
        float[] result = new float[length];
        Marshal.Copy(pointer, result, 0, result.Length);
        return result;
    }
}
