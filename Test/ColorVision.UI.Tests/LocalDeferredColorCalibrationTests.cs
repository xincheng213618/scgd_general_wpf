using ColorVision.Core;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Templates.POI;
using ColorVision.FileIO;
using cvColorVision;
using FlowEngineLib.Algorithm;
using Newtonsoft.Json;
using System.IO;
using System.Runtime.InteropServices;

namespace ColorVision.UI.Tests;

public sealed class LocalDeferredColorCalibrationTests
{
    private const string ColorJson = """
        {"a":0.123456789,"b":-0.025,"c":0.007,"d":0.009,"e":0.22,"f":-0.05,"g":0.03,"h":0.009,"i":0.32,
         "Gain_x":2,"Gain_y":3,"Gain_z":4,"Gain":[2.123456789,3,4],"pa":[0.123456789,-0.025,0.007,0.009,0.22,-0.05,0.03,0.009,0.32]}
        """;

    public static IEnumerable<object[]> ColorCases()
    {
        foreach (CalibrationType type in new[] { CalibrationType.Luminance, CalibrationType.LumOneColor, CalibrationType.LumFourColor, CalibrationType.LumMultiColor })
        foreach (int bpp in new[] { 8, 16 })
        foreach (CVImageFlipMode flip in new[] { CVImageFlipMode.None, CVImageFlipMode.X, CVImageFlipMode.Y, CVImageFlipMode.XY })
            yield return new object[] { type, bpp, flip };
    }

    [Theory]
    [MemberData(nameof(ColorCases))]
    public void DeferredPoiMatchesFullCieAndRawFileReplay(CalibrationType type, int bpp, CVImageFlipMode flip)
        => ComparePaths(type, bpp, flip, false, false);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BasicStagesStillRunBeforeDeferredColor(bool legacy)
        => ComparePaths(CalibrationType.LumFourColor, 16, CVImageFlipMode.Y, true, legacy);

    [Theory]
    [InlineData(CalibrationType.Luminance)]
    [InlineData(CalibrationType.LumOneColor)]
    [InlineData(CalibrationType.LumFourColor)]
    [InlineData(CalibrationType.LumMultiColor)]
    public void LegacyColorBackendSupportsDeferredPoi(CalibrationType type)
        => ComparePaths(type, 16, CVImageFlipMode.None, false, true);

    private static void ComparePaths(CalibrationType type, int bpp, CVImageFlipMode flip, bool basic, bool legacy)
    {
        AppContext.TryGetSwitch("ColorVision.UseLegacyLocalCalibration", out bool previous);
        AppContext.SetSwitch("ColorVision.UseLegacyLocalCalibration", legacy);
        string root = Path.Combine(Path.GetTempPath(), $"cv-deferred-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            string colorPath = Path.Combine(root, "color.dat"), darkPath = Path.Combine(root, "dark.dat");
            File.WriteAllText(colorPath, ColorJson);
            File.WriteAllText(darkPath, """{"bpp":16,"Texp_x":1.0,"DarkNoiseRatio":2.0}""");
            DeviceCameraCalibrationFile color = new("color", type, "color", "color.dat", colorPath);
            DeviceCameraCalibrationFile dark = new("dark", CalibrationType.DarkNoise, "dark", "dark.dat", darkPath);
            // Native semantics move color last even when the template lists it first.
            DeviceCameraCalibrationFile[] files = basic ? [color, dark] : [color];
            using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(97, 73, bpp, type == CalibrationType.Luminance ? 1 : 3);
            using LocalFlowFrame full = CreateFrame(raw, flip);
            using LocalFlowFrame fast = CreateFrame(raw, flip);
            using LocalCalibrationCacheManager cache = new("deferred-tests");
            LocalFrameCalibrationService.CalibrateInPlace(full, cache, files, "test", default);
            Assert.True(full.HasCie);
            LocalFrameCalibrationService.CalibrateInPlace(fast, cache, files, "test", default, allowAcceleration: true);
            Assert.False(fast.HasCie);
            Assert.NotNull(fast.ColorCalibration);
            using (LocalFlowFrameLease lease = fast.Acquire())
            {
                Assert.Equal(IntPtr.Zero, lease.CiePointer);
                Assert.Equal(0, lease.CieLength);
                Assert.True(lease.IsRawFlipApplied);
            }
            string expected = Measure(full);
            Assert.Equal(expected, Measure(fast));
            // Repeated measurement never promotes the flow frame to full XYZ.
            for (int i = 0; i < 10; i++) Assert.Equal(expected, Measure(fast));
            Assert.False(fast.HasCie);
            LocalFrameFileService.SaveCapture(fast, root, "camera");
            Assert.Empty(fast.CvCieFilePath);
            Assert.Empty(Directory.GetFiles(root, "*.cvcie", SearchOption.AllDirectories));
            using LocalFlowFrame reopened = LocalFrameFileService.Load(fast.CvRawFilePath);
            Assert.True(reopened.ColorCalibration!.CanReplay);
            Assert.Equal(expected, Measure(reopened));
            Assert.False(reopened.HasCie);
            // Switching back to full color on already-oriented RAW must not mirror twice.
            LocalFrameCalibrationService.ReuseColorCalibration(fast, allowAcceleration: false);
            Assert.True(fast.HasCie);
            Assert.Equal(expected, Measure(fast));
            LocalFrameCalibrationService.ReuseColorCalibration(fast, allowAcceleration: true);
            Assert.False(fast.HasCie);
            Assert.Equal(expected, Measure(fast));
            var action = new FlowEngineLib.Base.CVStartCFC("deferred-reuse");
            using var resources = action.RuntimeResources;
            action.SetCurrentFrame(full);
            LocalCalibrationNode node = new() { AllowAcceleration = true, SaveFiles = true, CalibTempName = "test" };
            var execute = typeof(LocalCalibrationNodeBase).GetMethod("ExecuteCalibration", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            using var execution = (IDisposable)execute.Invoke(node, [action])!;
            Assert.False(full.HasCie);
            Assert.Empty(full.CvCieFilePath);
            Assert.Equal(expected, Measure(full));
        }
        finally
        {
            AppContext.SetSwitch("ColorVision.UseLegacyLocalCalibration", previous);
            foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) File.Delete(file);
        }
    }

    [Fact]
    public void ColorMetadataIsWrittenWithoutSavingCieAndNonReplayableRawIsRejected()
    {
        string root = Path.Combine(Path.GetTempPath(), $"cv-deferred-metadata-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "input.cvraw"), calibration = Path.Combine(root, "color.dat");
        try
        {
            using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(97, 73, 16, 3);
            Assert.True(CVFileUtil.WriteCVRaw(path, raw));
            byte[] original = File.ReadAllBytes(path);
            File.WriteAllText(calibration, ColorJson);
            using LocalFlowFrame frame = LocalFrameFileService.Load(path);
            using LocalCalibrationCacheManager cache = new("deferred-metadata");
            DeviceCameraCalibrationFile file = new("color", CalibrationType.LumFourColor, "color", "color.dat", calibration);
            LocalFrameCalibrationService.CalibrateInPlace(frame, cache, [file], "test", default, allowAcceleration: true);
            Assert.Equal(original, File.ReadAllBytes(path).Take(original.Length));
            Assert.Empty(frame.CvCieFilePath);
            using (LocalFlowFrame reopened = LocalFrameFileService.Load(path)) Assert.Equal(Measure(frame), Measure(reopened));
            frame.ColorCalibration!.Save(path, canReplay: false);
            using LocalFlowFrame invalid = LocalFrameFileService.Load(path);
            Assert.Throws<InvalidOperationException>(() => Measure(invalid));
            using LocalFlowFrame uncalibrated = CreateFrame(raw, CVImageFlipMode.None);
            Assert.Throws<InvalidOperationException>(() => Measure(uncalibrated));
        }
        finally
        {
            foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) File.Delete(file);
        }
    }

    [Theory]
    [InlineData(typeof(LocalCalibrationNode), false)]
    [InlineData(typeof(LocalCalibrationRealPoiNode), true)]
    [InlineData(typeof(LocalCameraNode), false)]
    public void AccelerationUsesNodeDefaultAndPreservesExplicitSavedValues(Type type, bool expectedDefault)
    {
        var node = (LocalFlowNodeBase)Activator.CreateInstance(type)!;
        node.Create();
        var property = type.GetProperty("AllowAcceleration")!;
        Assert.Equal(expectedDefault, (bool)property.GetValue(node)!);
        node.OnLoadNode(new Dictionary<string, byte[]>());
        Assert.Equal(expectedDefault, (bool)property.GetValue(node)!);
        node.OnLoadNode(new Dictionary<string, byte[]> { ["AllowAcceleration"] = System.Text.Encoding.UTF8.GetBytes("True"), ["SaveFiles"] = System.Text.Encoding.UTF8.GetBytes("True") });
        Assert.True((bool)property.GetValue(node)!);
        Assert.True((bool)type.GetProperty("SaveFiles")!.GetValue(node)!);
        Assert.Contains("AllowAcceleration", System.Text.Encoding.UTF8.GetString(node.GetSaveData()));
        node.OnLoadNode(new Dictionary<string, byte[]> { ["AllowAcceleration"] = System.Text.Encoding.UTF8.GetBytes("False") });
        Assert.False((bool)property.GetValue(node)!);
    }

    private static LocalFlowFrame CreateFrame(CVCIEFile raw, CVImageFlipMode flip)
    {
        LocalFlowFrame frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
        {
            Width = raw.Cols, Height = raw.Rows, SourceBpp = raw.Bpp, Channels = raw.Channels,
            Exposure = raw.Exp, PrimaryBufferKind = LocalFrameBufferKind.CvRaw, FlipMode = flip
        }, raw.Data.Length, 0);
        using LocalFlowFrameLease lease = frame.Acquire();
        Marshal.Copy(raw.Data, 0, lease.RawPointer, raw.Data.Length);
        return frame;
    }

    private static string Measure(LocalFlowFrame frame)
    {
        PoiParam poi = new() { Name = "test" };
        poi.PoiPoints.Add(new PoiPoint { Id = 1, PointType = PoiShape.Point, PixX = 0, PixY = 0 });
        poi.PoiPoints.Add(new PoiPoint { Id = 2, PointType = PoiShape.Circle, PixX = 10, PixY = 12, PixWidth = 9, PixHeight = 9 });
        poi.PoiPoints.Add(new PoiPoint { Id = 3, PointType = PoiShape.Rect, PixX = 96, PixY = 72, PixWidth = 7, PixHeight = 5 });
        using LocalFlowFrameLease lease = frame.Acquire();
        return JsonConvert.SerializeObject(LocalPoiCalculator.Calculate(lease, poi).Points);
    }
}
