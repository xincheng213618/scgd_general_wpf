using ColorVision.Engine.Services.PhyCameras.Calibration.Creation;
using ColorVision.FileIO;
using cvColorVision;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CalibrationMapCreationTests
{
    [Fact]
    public void DsnuUsesNativeHeaderInterleavedBgrOrderAndDeterministicHalfRounding()
    {
        var image = new CalibrationRawAverage(2, 1, 16, 3, new double[] { 1.5, 2, 3, 4, 5, 65535 }, 2);
        using var output = new MemoryStream();
        CalibrationMapCreation.WriteMap(output, image, CalibrationType.DSNU);
        output.Position = 0;
        using var reader = new BinaryReader(output);
        Assert.Equal(new uint[] { 1, 2, 16, 3, 16 }, Enumerable.Range(0, 5).Select(_ => reader.ReadUInt32()));
        Assert.Equal(new ushort[] { 5, 3, 2, 65535, 4, 2 }, Enumerable.Range(0, 6).Select(_ => reader.ReadUInt16()));
        Assert.Equal(output.Length, output.Position);
    }

    [Fact]
    public void UniformityKeepsEachChannelNormalizationIndependent()
    {
        var image = new CalibrationRawAverage(2, 1, 8, 3, new double[] { 10, 30, 20, 40, 40, 80 }, 1);
        using var output = new MemoryStream();
        CalibrationMapCreation.WriteMap(output, image, CalibrationType.Uniformity, 1);
        output.Position = 0;
        using var reader = new BinaryReader(output);
        Assert.Equal(new uint[] { 1, 2, 32, 3, 8 }, Enumerable.Range(0, 5).Select(_ => reader.ReadUInt32()));
        var expected = new[] { 1.5f, 1.5f, 2f, 0.75f, 0.75f, 2f / 3f };
        foreach (float value in expected) Assert.Equal(value, reader.ReadSingle());
    }

    [Theory]
    [InlineData(true, 100, 0, 1)]
    [InlineData(false, 1, 1, 0)]
    public void DefectDetectionUnionsChannelsAndWritesUniqueRowColumnPairs(bool bright, double threshold, uint row, uint col)
    {
        var image = new CalibrationRawAverage(2, 2, 8, 3, new double[] { 10, 110, 0, 10, 10, 120, 0, 10, 10, 10, 10, 10 }, 1);
        using var output = new MemoryStream();
        Assert.Equal(1, CalibrationMapCreation.WriteDefects(output, image, threshold, bright));
        output.Position = 0;
        using var reader = new BinaryReader(output);
        Assert.Equal(1u, reader.ReadUInt32());
        Assert.Equal(row, reader.ReadUInt32());
        Assert.Equal(col, reader.ReadUInt32());
        Assert.Equal(output.Length, output.Position);
    }

    [Fact]
    public void InvalidPixelLayoutZeroUniformityAndCancellationFailExplicitly()
    {
        using var output = new MemoryStream();
        Assert.Throws<InvalidDataException>(() => CalibrationMapCreation.WriteMap(output,
            new(2, 1, 8, 1, new double[] { 1 }, 1), CalibrationType.DSNU));
        Assert.Throws<InvalidDataException>(() => CalibrationMapCreation.WriteMap(output,
            new(2, 1, 8, 1, new double[] { 0, 1 }, 1), CalibrationType.Uniformity));
        Assert.Throws<OperationCanceledException>(() => CalibrationMapCreation.WriteDefects(output,
            new(1, 1, 8, 1, new double[] { 1 }, 1), 2, false, new CancellationToken(true)));
        Assert.Throws<ArgumentException>(() => CalibrationMapCreation.Average(Array.Empty<string>()));
    }

    [Theory]
    [InlineData(1, 8)]
    [InlineData(2, 16)]
    [InlineData(3, 16)]
    public void AverageReadsLegacyContainersInPlanarOrderAndRejectsCorrectedInput(int version, int bpp)
    {
        string first = Path.Combine(Path.GetTempPath(), $"calibration-average-{Guid.NewGuid():N}.cvraw");
        string second = Path.Combine(Path.GetTempPath(), $"calibration-average-{Guid.NewGuid():N}.cvraw");
        try
        {
            WriteRaw(first, version, bpp, new ushort[] { 1, 3, 5, 7, 9, 11 });
            WriteRaw(second, version, bpp, new ushort[] { 3, 5, 7, 9, 11, 13 });
            var average = CalibrationMapCreation.Average(new[] { first, second }, interleavedBgr: false);
            Assert.Equal(2, average.Width);
            Assert.Equal(1, average.Height);
            Assert.Equal(3, average.Channels);
            Assert.Equal(bpp, average.BitsPerChannel);
            Assert.Equal(2, average.FrameCount);
            Assert.Equal(new double[] { 2, 4, 6, 8, 10, 12 }, average.Values);
            var interleaved = CalibrationMapCreation.Average(new[] { first, second }, interleavedBgr: true);
            Assert.Equal(new double[] { 6, 12, 4, 10, 2, 8 }, interleaved.Values);
            CVFileMetadata.SetProperty(first, "colorvision.calibration.color", 1, new byte[] { 1 });
            Assert.Throws<InvalidDataException>(() => CalibrationMapCreation.Average(new[] { first }));
        }
        finally { File.Delete(first); File.Delete(second); }
    }

    private static void WriteRaw(string path, int version, int bpp, ushort[] values)
    {
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("CVCIE"));
        writer.Write((uint)version); writer.Write(0);
        if (version == 3) writer.Write(0);
        writer.Write(1f); writer.Write(3);
        for (int i = 0; i < 3; i++) writer.Write(10f);
        writer.Write(2); writer.Write(1); writer.Write(bpp);
        int length = values.Length * bpp / 8;
        if (version == 2) writer.Write((long)length); else writer.Write(length);
        foreach (ushort value in values) { if (bpp == 8) writer.Write((byte)value); else writer.Write(value); }
    }
}
