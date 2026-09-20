using ColorVision.Core;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.POI;
using ColorVision.FileIO;
using ColorVision.ImageEditor.Draw;
using cvColorVision;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace ColorVision.UI.Tests;

public sealed class RawColorCalibrationTests
{
    private const string CalibrationJson = """
        {"a":0.123456789,"b":-0.025,"c":0.007,"d":0.009,"e":0.22,"f":-0.05,"g":0.03,"h":0.009,"i":0.32,
         "Gain_x":2,"Gain_y":3,"Gain_z":4,"Gain":[2.123456789,3,4],"pa":[0.123456789,-0.025,0.007,0.009,0.22,-0.05,0.03,0.009,0.32]}
        """;

    [Theory]
    [InlineData(CalibrationType.LumFourColor, 8, true)]
    [InlineData(CalibrationType.LumFourColor, 16, false)]
    [InlineData(CalibrationType.LumMultiColor, 16, true)]
    [InlineData(CalibrationType.LumMultiColor, 8, false)]
    [InlineData(CalibrationType.LumOneColor, 8, true)]
    [InlineData(CalibrationType.LumOneColor, 16, false)]
    [InlineData(CalibrationType.Luminance, 8, true)]
    [InlineData(CalibrationType.Luminance, 16, false)]
    public void ExecutedSnapshotReplaysExactlyAndMeasuresWithoutFullPlanes(CalibrationType type, int bpp, bool bgr)
    {
        const int width = 97, height = 73;
        int channels = type == CalibrationType.Luminance ? 1 : 3;
        using CVCIEFile raw = CreateRaw(width, height, bpp, channels);
        string calibration = WriteCalibration();
        Assert.Equal(96, Marshal.SizeOf<RawColorTransformV1>());
        Assert.Equal(12, Marshal.SizeOf<RawPixelRunV1>());
        Assert.Equal(1, OpenCVCalibration.M_CalibrationCreate(out IntPtr context));
        try
        {
            Assert.Equal(1, OpenCVCalibration.M_CalibrationLoadFileW(context, (int)type, calibration));
            // A loaded transform owns its parameters even if its external file changes later.
            File.WriteAllText(calibration, "{}");
            CalibrationExecutionOptionsV1 options = CalibrationExecutionOptionsV1.Create([198, 212, 117]);
            options.InterleavedBgr = bgr ? 1 : 0;
            byte[] expected = new byte[width * height * channels * 4];
            WithPinned(raw.Data, expected, (input, output) => Assert.Equal(1, OpenCVCalibration.M_CalibrationExecute(
                context, width, height, (uint)bpp, (uint)channels, input, (ulong)raw.Data.Length, output, (ulong)expected.Length / 4, in options)));
            RawColorTransformV1 transform = RawColorTransformV1.Create();
            Assert.Equal(1, OpenCVMediaHelper.M_CalibrationGetColorTransformV1(context, in options, ref transform));
            byte[] actual = new byte[expected.Length];
            WithPinned(raw.Data, actual, (input, output) => Assert.Equal(OpenCVCalibration.PoiOk,
                OpenCVMediaHelper.M_TransformRawColorV1(width, height, bpp, input, (ulong)raw.Data.Length, in transform, -1, output, (ulong)actual.Length / 4)));
            Assert.Equal(expected, actual);
            ColorCalibrationSnapshot snapshot = ColorCalibrationSnapshot.Create(transform, width, height, bpp, [198, 212, 117], "test");
            using PoiMeasurementBuffer demand = new(raw, snapshot);
            using PoiMeasurementBuffer full = new(expected, width, height, 32, channels);
            for (int channel = 0; channel < channels; channel++)
                Assert.Equal(expected.AsSpan(channel * width * height * 4, width * height * 4).ToArray(), demand.RawSource!.CreateChannel(channel));
            PoiMeasurementPoint[] points = [new(13, 11, 1, 1, PoiMeasurementShape.Point),
                new(0, 0, 7, 7, PoiMeasurementShape.Circle), new(95, 71, 5, 4, PoiMeasurementShape.Rect),
                new(20, 10, 7, 3, PoiMeasurementShape.Ellipse)];
            Assert.Equal(PoiMeasurementService.Calculate(full, points), PoiMeasurementService.Calculate(demand, points));
            Assert.Equal(PoiMeasurementService.CalculateRaw(full, points), PoiMeasurementService.CalculateRaw(demand, points));
            ClosedPixelRegion polygon = ClosedPixelRegion.Polygon([new Point(3, 3), new Point(12, 4), new Point(7, 14)]);
            Assert.Equal(PoiMeasurementService.CalculateRegion(full, polygon, true), PoiMeasurementService.CalculateRegion(demand, polygon, true));
            Assert.Equal(0, demand.RawSource!.CachedXyzBytes);
            // Repeated large regions promote to a reusable full buffer with the same result.
            PoiMeasurementPoint large = new(width / 2, height / 2, width, height, PoiMeasurementShape.Rect);
            Assert.Equal(PoiMeasurementService.CalculateRaw(full, [large]), PoiMeasurementService.CalculateRaw(demand, [large]));
            Assert.Equal(expected.Length, demand.RawSource.CachedXyzBytes);
            demand.Dispose();
            Assert.Throws<ObjectDisposedException>(() => demand.RawSource.CreateChannel(0));
        }
        finally { OpenCVCalibration.M_CalibrationDestroy(context); File.Delete(calibration); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SuccessfulColorCalibrationUpdatesOnlyLastSnapshotAndSavingCopiesItToRawAndCie(bool legacy)
    {
        AppContext.TryGetSwitch("ColorVision.UseLegacyLocalCalibration", out bool previousLegacy);
        AppContext.SetSwitch("ColorVision.UseLegacyLocalCalibration", legacy);
        string root = Path.Combine(Path.GetTempPath(), $"cv-color-pipeline-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "input.cvraw");
        string calibration = WriteCalibration();
        try
        {
            using CVCIEFile raw = CreateRaw(17, 13, 16, 3);
            Assert.True(CVFileUtil.WriteCIEFile(path, raw));
            byte[] original = File.ReadAllBytes(path);
            CVFileMetadata.SetProperty(path, "future", 12, new byte[] { 7 });
            using LocalFlowFrame frame = LocalFrameFileService.Load(path);
            using LocalCalibrationCacheManager cache = new("test");
            DeviceCameraCalibrationFile file = new("color", CalibrationType.LumFourColor, "color", Path.GetFileName(calibration), calibration);
            LocalFrameCalibrationService.CalibrateInPlace(frame, cache, [file], "first", default);
            Assert.Equal("first", ColorCalibrationSnapshot.Read(path, raw)!.Template);
            Assert.True(ColorCalibrationSnapshot.Read(path, raw)!.CanReplay);
            LocalFrameCalibrationService.CalibrateInPlace(frame, cache, [file], "final", default);
            Assert.Equal("final", ColorCalibrationSnapshot.Read(path, raw)!.Template);
            Assert.Equal(2, CVFileMetadata.Read(path).Count);
            Assert.Equal(original, File.ReadAllBytes(path).Take(original.Length));
            byte[] last = File.ReadAllBytes(path);
            DeviceCameraCalibrationFile missing = file with { FullPath = calibration + ".missing" };
            Assert.ThrowsAny<Exception>(() => LocalFrameCalibrationService.CalibrateInPlace(frame, cache, [missing], "failed", default));
            Assert.Equal(last, File.ReadAllBytes(path));
            // A failed attempt is not a new parameter snapshot; run successfully before saving.
            LocalFrameCalibrationService.CalibrateInPlace(frame, cache, [file], "saved", default);
            LocalFrameFileService.SaveCapture(frame, root, "camera");
            Assert.True(CVFileUtil.Read(frame.CvCieFilePath, out CVCIEFile cie));
            using (cie)
            {
                var saved = ColorCalibrationSnapshot.Read(frame.CvCieFilePath, cie)!;
                Assert.Equal("saved", saved.Template);
                Assert.Equal(CVFileMetadata.Read(frame.CvRawFilePath)[ColorCalibrationSnapshot.PropertyKind].Value,
                    CVFileMetadata.Read(frame.CvCieFilePath)[ColorCalibrationSnapshot.PropertyKind].Value);
            }
        }
        finally
        {
            AppContext.SetSwitch("ColorVision.UseLegacyLocalCalibration", previousLegacy);
            File.Delete(calibration);
            // This unique test-owned directory contains only the generated fixtures.
            foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) File.Delete(file);
        }
    }

    internal static CVCIEFile CreateRaw(int width, int height, int bpp, int channels)
    {
        byte[] data = new byte[width * height * channels * (bpp / 8)];
        new Random(73).NextBytes(data);
        return new CVCIEFile { Version = 2, Cols = width, Rows = height, Channels = channels, Bpp = bpp,
            FileExtType = CVType.Raw, Gain = 1, Exp = [198, 212, 117], Data = data };
    }

    private static string WriteCalibration()
    {
        string path = Path.Combine(Path.GetTempPath(), $"cv-transform-{Guid.NewGuid():N}.dat");
        File.WriteAllText(path, CalibrationJson);
        return path;
    }

    private static void WithPinned(byte[] input, byte[] output, Action<IntPtr, IntPtr> action)
    {
        GCHandle a = GCHandle.Alloc(input, GCHandleType.Pinned), b = GCHandle.Alloc(output, GCHandleType.Pinned);
        try { action(a.AddrOfPinnedObject(), b.AddrOfPinnedObject()); }
        finally { a.Free(); b.Free(); }
    }
}
