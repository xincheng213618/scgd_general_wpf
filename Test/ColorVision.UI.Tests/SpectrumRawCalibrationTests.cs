using ColorVision.Core;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.POIGenCali;
using ColorVision.FileIO;
using CVCommCore.CVAlgorithm;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class SpectrumRawCalibrationTests
{
    [Theory]
    [InlineData(0, GenCalibrationType.BrightnessAndChroma)]
    [InlineData(0, GenCalibrationType.BrightnessOnly)]
    [InlineData(0, GenCalibrationType.ChromaOnly)]
    [InlineData(1, GenCalibrationType.BrightnessAndChroma)]
    [InlineData(1, GenCalibrationType.BrightnessOnly)]
    [InlineData(1, GenCalibrationType.ChromaOnly)]
    public void CorrectionReplaysFromUnchangedRawAndRetriesDoNotCompound(int kind, GenCalibrationType mode)
    {
        string path = Path.Combine(Path.GetTempPath(), $"spectrum-raw-{Guid.NewGuid():N}.cvraw");
        try
        {
            using CVCIEFile raw = WriteRaw(path);
            byte[] originalPixelsAndHeader = File.ReadAllBytes(path);
            ColorCalibrationSnapshot original = Snapshot(kind);
            original.Save(path, true);
            using LocalFlowFrame frame = LocalFrameFileService.Load(path);
            ColorCalibrationSnapshot corrected = SpectrumRawCalibrationService.Calculate(frame, Point(), .3, .4, 120, mode, 12, 34);
            Assert.Equal(original.Coefficients, corrected.SpectrumCorrection!.OriginalCoefficients);
            Assert.Equal(original.Exposure, corrected.Exposure);
            Assert.Equal(original.Template, corrected.Template);
            Assert.False(frame.HasCie);
            int id = SpectrumRawCalibrationService.Save(frame, corrected, () =>
            {
                // The latest database row is published only after the actual file contains the new matrix.
                Assert.Equal(corrected.Coefficients, ColorCalibrationSnapshot.Read(path, raw)!.Coefficients);
                return 45;
            });
            Assert.Equal(45, id);
            Assert.Equal(45, frame.MasterId);
            Assert.False(frame.HasCie);
            Assert.Equal(originalPixelsAndHeader, File.ReadAllBytes(path).Take(originalPixelsAndHeader.Length));
            Assert.Equal(corrected.Coefficients,
                SpectrumRawCalibrationService.Calculate(frame, Point(), .3, .4, 120, mode, 12, 34).Coefficients);
            // Reopening preserves the baseline too; a second application must not scale twice.
            using LocalFlowFrame reopened = LocalFrameFileService.Load(path);
            Assert.Equal(corrected.Coefficients,
                SpectrumRawCalibrationService.Calculate(reopened, Point(), .3, .4, 120, mode, 12, 34).Coefficients);
            using var reader = new CalibratedRawFileReader(path);
            for (int c = 0; c < 3; c++)
            {
                using CVCIEFile channel = reader.ReadChannel(c);
                double expected = corrected.SpectrumCorrection.ReferenceXyz[c] * corrected.SpectrumCorrection.Factors[c];
                // Native output is float32; this is its rounding bound, not a measurement tolerance.
                Assert.InRange(Math.Abs(BitConverter.ToSingle(channel.Data, 0) - expected), 0, Math.Abs(expected) * 2e-7);
            }
            Assert.False(File.Exists(Path.ChangeExtension(path, ".cvcie")));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void FailedResultPersistenceRestoresFileAndLeavesFrameUnchanged()
    {
        string path = Path.Combine(Path.GetTempPath(), $"spectrum-rollback-{Guid.NewGuid():N}.cvraw");
        try
        {
            using CVCIEFile raw = WriteRaw(path);
            Snapshot(0).Save(path, true);
            CVFileMetadata.SetProperty(path, "unrelated", 9, [1, 3, 5]);
            byte[] before = File.ReadAllBytes(path);
            using LocalFlowFrame frame = LocalFrameFileService.Load(path);
            ColorCalibrationSnapshot original = frame.ColorCalibration!;
            ColorCalibrationSnapshot corrected = SpectrumRawCalibrationService.Calculate(frame, Point(), .3, .4, 120,
                GenCalibrationType.BrightnessAndChroma, 12, 34);
            Assert.Throws<IOException>(() => SpectrumRawCalibrationService.Save(frame, corrected, () => throw new IOException("DB unavailable")));
            Assert.Equal(before, File.ReadAllBytes(path));
            Assert.Same(original, frame.ColorCalibration);
            Assert.Equal(-1, frame.MasterId);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void MemoryOnlyCaptureIsSavedAsRawBeforePublishingResult()
    {
        string path = Path.Combine(Path.GetTempPath(), $"spectrum-memory-{Guid.NewGuid():N}.cvraw");
        try
        {
            using CVCIEFile raw = WriteRaw(path);
            Snapshot(0).Save(path, true);
            using LocalFlowFrame frame = LocalFrameFileService.Load(path);
            File.Delete(path);
            var corrected = SpectrumRawCalibrationService.Calculate(frame, Point(), .3, .4, 120,
                GenCalibrationType.BrightnessAndChroma, 12, 34);
            SpectrumRawCalibrationService.Save(frame, corrected, () =>
            {
                Assert.True(File.Exists(path));
                return 46;
            });
            using LocalFlowFrame reopened = LocalFrameFileService.Load(path);
            Assert.Equal(corrected.Coefficients, reopened.ColorCalibration!.Coefficients);
            Assert.False(reopened.HasCie);
            using LocalFlowFrameLease lease = reopened.Acquire();
            byte[] actual = new byte[lease.RawLength];
            System.Runtime.InteropServices.Marshal.Copy(lease.RawPointer, actual, 0, actual.Length);
            Assert.Equal(raw.Data, actual);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ReferenceMustBeUniqueAndInvalidDataCannotProduceMatrix()
    {
        LocalPoiResultSet poi = new() { Points = [Point(), Point()] };
        Assert.Throws<InvalidOperationException>(() => SpectrumRawCalibrationService.SelectPoint(poi, ""));
        Assert.Throws<InvalidOperationException>(() => SpectrumRawCalibrationService.SelectPoint(poi, "Reference"));
        Assert.Throws<InvalidOperationException>(() => SpectrumRawCalibrationService.SelectPoint(poi, "missing"));
        Assert.Same(poi.Points[0], SpectrumRawCalibrationService.SelectPoint(new() { Points = [poi.Points[0]] }, ""));
        foreach (double invalid in new[] { 0d, -1, double.NaN, double.PositiveInfinity })
            Assert.Throws<InvalidOperationException>(() => SpectrumRawCalibrationService.GetFactors([100, 120, invalid], [90, 120, 90], GenCalibrationType.BrightnessAndChroma));
    }

    [Fact]
    public void MatrixScalesXyzRowsAndOneColorCrossTerm()
    {
        var matrix = Snapshot(0) with { Coefficients = [1,2,3,4,5,6,7,8,9] };
        Assert.Equal(new double[] { 2,4,6,12,15,18,28,32,36 }, SpectrumRawCalibrationService.Compose(matrix, [2,3,4]));
        var oneColor = matrix with { TransformKind = 1 };
        Assert.Equal(new double[] { 2,6,12,8,5,6,7,8,9 }, SpectrumRawCalibrationService.Compose(oneColor, [2,3,4]));
    }

    private static LocalPoiPointResult Point() => new()
    {
        Name = "Reference", X = 1, Y = 1, Width = 1, Height = 1, PointType = POIPointTypes.SolidPoint, Value = new object()
    };

    private static ColorCalibrationSnapshot Snapshot(int kind) => new()
    {
        Width = 3, Height = 3, RawBpp = 16, Channels = 3, TransformKind = kind,
        Coefficients = [.5, .25, .125, .25, .5, .125, .25, .125, .5],
        Exposure = [10, 20, 30], Template = "CameraOriginal", CanReplay = true
    };

    private static CVCIEFile WriteRaw(string path)
    {
        var raw = new CVCIEFile { Version = 2, Rows = 3, Cols = 3, Bpp = 16, Channels = 3, FileExtType = CVType.Raw,
            Exp = [10,20,30], Data = Enumerable.Range(0, 9).SelectMany(_ => new ushort[] { 100,200,300 }).SelectMany(BitConverter.GetBytes).ToArray() };
        Assert.True(CVFileUtil.WriteCIEFile(path, raw));
        return raw;
    }
}
