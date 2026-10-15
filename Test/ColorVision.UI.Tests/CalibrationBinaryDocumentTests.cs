using ColorVision.Engine.Services.PhyCameras.Calibration.Creation;
using ColorVision.Engine.Services.PhyCameras.Calibration.Editing;
using ColorVision.Engine.Services.Types;
using cvColorVision;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CalibrationBinaryDocumentTests
{
    [Theory]
    [InlineData(ServiceTypes.DSNU, CalibrationType.DSNU)]
    [InlineData(ServiceTypes.Uniformity, CalibrationType.Uniformity)]
    public void GeneratedV1MapsReadWithMatchingDimensionsAndExactRoundTrip(ServiceTypes service, CalibrationType native)
    {
        using var stream = new MemoryStream();
        CalibrationMapCreation.WriteMap(stream, new(2, 2, 16, 3, Enumerable.Range(1, 12).Select(i => (double)i).ToArray(), 1), native, 1);
        byte[] bytes = stream.ToArray();
        var document = CalibrationBinaryDocument.Parse(service, bytes);
        Assert.Equal(1, document.Version); Assert.Equal(2u, document.Width); Assert.Equal(2u, document.Height);
        Assert.Equal(3u, document.Channels); Assert.Equal(16u, document.SourceBits); Assert.Equal(12, document.ValueCount);
        Assert.Equal(bytes, document.BuildBytes());
        Assert.Throws<InvalidOperationException>(() => document.SetValue(0, 4));
    }

    [Fact]
    public void LegacyMapAndLinearTablePreserveTheirOriginalFormat()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(2u); writer.Write(3u);
        foreach (ushort value in new ushort[] { 0, 1, 2, 3, 4, 65535 }) writer.Write(value);
        var map = CalibrationBinaryDocument.Parse(ServiceTypes.DSNU, stream.ToArray());
        Assert.Equal(0, map.Version); Assert.Equal(65535, map.Maximum); Assert.Equal(stream.ToArray(), map.BuildBytes());
        stream.SetLength(0); stream.Position = 0;
        writer.Write(4u); foreach (float value in new[] { -1f, 0, 1, 2 }) writer.Write(value);
        byte[] original = stream.ToArray();
        var linear = CalibrationBinaryDocument.Parse(ServiceTypes.LineArity, original);
        linear.SetValue(2, -3.5f);
        byte[] saved = linear.BuildBytes();
        Assert.Equal(original.AsSpan(0, 12).ToArray(), saved.AsSpan(0, 12).ToArray());
        Assert.Equal(-3.5f, CalibrationBinaryDocument.Parse(ServiceTypes.LineArity, saved).GetValue(2));
        Assert.Throws<FormatException>(() => linear.SetValue(0, float.NaN));
    }

    [Fact]
    public void DefectCoordinateEditsPreserveUnknownTrailingBytes()
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write(1u); writer.Write(7u); writer.Write(11u); writer.Write(new byte[] { 3, 4, 5 });
        var document = CalibrationBinaryDocument.Parse(ServiceTypes.DefectPoint, stream.ToArray());
        Assert.Equal(new CalibrationDefectPoint(7, 11), document.GetPoint(0));
        document.SetPoint(0, new(2, 3)); document.AddPoint(new(5, 6));
        var saved = document.BuildBytes();
        Assert.Equal(new byte[] { 3, 4, 5 }, saved[^3..]);
        var loaded = CalibrationBinaryDocument.Parse(ServiceTypes.DefectPoint, saved);
        Assert.Equal(2, loaded.PointCount); Assert.Equal(new CalibrationDefectPoint(5, 6), loaded.GetPoint(1));
    }

    [Fact]
    public void TruncatedHeadersAndNonfiniteLinearValuesAreRejected()
    {
        Assert.Throws<FormatException>(() => CalibrationBinaryDocument.Parse(ServiceTypes.DefectPoint, new byte[] { 1, 0, 0, 0 }));
        Assert.Throws<FormatException>(() => CalibrationBinaryDocument.Parse(ServiceTypes.DSNU, new byte[20]));
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write(4u); foreach (float value in new[] { 1f, float.PositiveInfinity, 2, 3 }) writer.Write(value);
        Assert.Throws<FormatException>(() => CalibrationBinaryDocument.Parse(ServiceTypes.LineArity, stream.ToArray()));
    }

    [Fact]
    public void BinaryReplacementKeepsOriginalBackupAndRejectsStaleSnapshotAndCollisions()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ColorVisionBinaryCalibrationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string source = Path.Combine(directory, "defect.dat"), copy = Path.Combine(directory, "copy.dat");
            byte[] original = { 0, 0, 0, 0, 77 }, edited = { 0, 0, 0, 0, 99 };
            File.WriteAllBytes(source, original);
            var snapshot = CalibrationBinaryFileStore.Load(source);
            var saved = CalibrationBinaryFileStore.Save(snapshot, edited);
            Assert.Equal(original, File.ReadAllBytes(saved.LastBackupPath!));
            Assert.Equal(edited, File.ReadAllBytes(source));
            Assert.Throws<IOException>(() => CalibrationBinaryFileStore.Save(snapshot, original));
            CalibrationBinaryFileStore.SaveAs(copy, edited, saved);
            Assert.Throws<IOException>(() => CalibrationBinaryFileStore.SaveAs(copy, original, saved));
            File.WriteAllBytes(source, original);
            Assert.Throws<IOException>(() => CalibrationBinaryFileStore.SaveAs(Path.Combine(directory, "new.dat"), edited, saved));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally { Directory.Delete(directory, true); }
    }
}
