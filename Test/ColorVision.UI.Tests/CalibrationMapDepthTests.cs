using ColorVision.Core;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using cvColorVision;
using System.IO;
using System.Runtime.InteropServices;

namespace ColorVision.UI.Tests;

public sealed class CalibrationMapDepthTests
{
    // The last DSNU offset exercises both the byte-packed and the wide native representations.
    [Theory]
    [InlineData(CalibrationType.DSNU, null, 5)]
    [InlineData(CalibrationType.Uniformity, null, 0)]
    [InlineData(CalibrationType.DSNU, 8, 5)]
    [InlineData(CalibrationType.DSNU, 16, 5)]
    [InlineData(CalibrationType.DSNU, 16, 300)]
    [InlineData(CalibrationType.Uniformity, 8, 0)]
    [InlineData(CalibrationType.Uniformity, 16, 0)]
    public void InputDepthComesFromTheMapSourceField(CalibrationType type, int? mapDepth, int lastOffset)
    {
        byte[] samples = [0, 1, 7, 32, 127, 255];
        string path = Path.GetTempFileName();
        try
        {
            var map = WriteMap(path, type, mapDepth, lastOffset);
            using var cache = new LocalCalibrationCacheManager("map-depth");
            var plan = cache.Prepare([map], "map");
            foreach (int inputDepth in new[] { 8, 16 })
            {
                int[] original = samples.Select(pixel => inputDepth == 8 ? (int)pixel : pixel << 8).ToArray();
                byte[] bytes = inputDepth == 8 ? samples : original.SelectMany(pixel => BitConverter.GetBytes((ushort)pixel)).ToArray();
                using var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
                    { Width = 3, Height = 2, Channels = 1, SourceBpp = inputDepth }, bytes.Length, 0);
                using (var buffer = frame.Acquire()) Marshal.Copy(bytes, 0, buffer.RawPointer, bytes.Length);
                if (mapDepth == 8 && inputDepth == 16)
                {
                    Assert.Throws<InvalidOperationException>(() => plan.Apply(frame, CalibrationOutputMode.RawOnly));
                    using var unchanged = frame.Acquire();
                    Assert.Equal(bytes, unchanged.CopyRawToArray());
                    continue;
                }

                plan.Apply(frame, CalibrationOutputMode.RawOnly);
                using var result = frame.Acquire();
                int depth = mapDepth ?? inputDepth;
                Assert.Equal(depth, result.Metadata.SourceBpp);
                var expected = samples.Select((pixel, index) =>
                {
                    int value = depth == 16 ? pixel << 8 : pixel;
                    return type == CalibrationType.DSNU
                        ? Math.Max(0, value - (index == samples.Length - 1 ? lastOffset : 1))
                        : Math.Min((1 << depth) - 1, value * 2);
                }).ToArray();
                byte[] corrected = result.CopyRawToArray();
                int[] actual = depth == 8 ? corrected.Select(value => (int)value).ToArray()
                    : MemoryMarshal.Cast<byte, ushort>(corrected.AsSpan()).ToArray().Select(value => (int)value).ToArray();
                Assert.Equal(expected, actual);
            }
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void MapsWithDifferentSourceDepthsConflictDuringPreparation()
    {
        string dsnu = Path.GetTempFileName(), uniformity = Path.GetTempFileName();
        try
        {
            var offsets = WriteMap(dsnu, CalibrationType.DSNU, 8, 5);
            var gains = WriteMap(uniformity, CalibrationType.Uniformity, 16, 0);
            using var cache = new LocalCalibrationCacheManager("conflicting-maps");
            Assert.Throws<InvalidOperationException>(() => cache.Prepare([offsets, gains], "mixed maps"));
        }
        finally { File.Delete(dsnu); File.Delete(uniformity); }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12)]
    [InlineData(32)]
    public void VersionOneMapRequiresAValidSourceDepth(int invalidDepth)
    {
        string path = Path.GetTempFileName();
        try
        {
            var map = WriteMap(path, CalibrationType.Uniformity, invalidDepth, 0);
            using var cache = new LocalCalibrationCacheManager("invalid-map-depth");
            Assert.Throws<InvalidOperationException>(() => cache.Prepare([map], "invalid map"));
        }
        finally { File.Delete(path); }
    }

    private static DeviceCameraCalibrationFile WriteMap(string path, CalibrationType type, int? mapDepth, int lastOffset)
    {
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write(2u);
        writer.Write(3u);
        if (mapDepth.HasValue)
        {
            writer.Write(type == CalibrationType.DSNU ? 16u : 32u);
            writer.Write(1u);
            writer.Write((uint)mapDepth.Value);
        }
        for (int index = 0; index < 6; index++)
        {
            if (type == CalibrationType.DSNU) writer.Write((ushort)(index == 5 ? lastOffset : 1));
            else writer.Write(2f);
        }
        return new(type.ToString(), type, type.ToString(), Path.GetFileName(path), path);
    }
}
