using ColorVision.Core;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.PhyCameras.Calibration.Creation;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Services.Types;
using ColorVision.Engine.Templates.POI;
using ColorVision.FileIO;
using ColorVision.ImageEditor.Tif;
using cvColorVision;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using MQTTMessageLib.Algorithm;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ColorVision.UI.Tests;

public sealed class CalibrationEntryPointParityTests
{
    // Each column has a different channel pattern so RGB/BGR swaps also change the oracle.
    private static readonly byte[] Bgr8 = [0, 1, 255, 128, 254, 17, 255, 127, 0, 1, 64, 128];
    private static readonly byte[] DarkLevelBgr = [6, 12, 18, 10, 20, 30, 9, 3, 5, 4, 8, 12];
    // Complete darkness, an unsaturated signal, a near-black signal and a high signal.
    private static readonly byte[] SignalBgr = [6, 12, 18, 45, 70, 90, 8, 3, 7, 150, 220, 255];

    [Theory]
    [InlineData(8, 16, false, false)]
    [InlineData(8, 16, true, false)]
    [InlineData(16, 16, false, false)]
    [InlineData(16, 16, true, false)]
    [InlineData(8, 8, false, false)]
    [InlineData(8, 8, true, false)]
    [InlineData(8, 16, false, true)]
    [InlineData(8, 16, true, true)]
    [InlineData(16, 16, false, true)]
    [InlineData(16, 16, true, true)]
    [InlineData(8, 8, false, true)]
    [InlineData(8, 8, true, true)]
    public void AcquisitionFileAndFlowAgreeOnCorrectedRawAndXyz(int sourceBits, int modelBits, bool deferred, bool darkNoise)
        => CompareEntryPoints(sourceBits, modelBits, deferred, darkNoise, subtractDark: false);

    [Theory]
    [InlineData(8, 8, false)]
    [InlineData(8, 8, true)]
    [InlineData(8, 16, false)]
    [InlineData(8, 16, true)]
    [InlineData(16, 16, false)]
    [InlineData(16, 16, true)]
    public void CameraTemplateSubtractsMeasuredDarkFieldBeforeMultiplyingAndConvertingToXyz(int sourceBits, int modelBits, bool deferred)
        => CompareEntryPoints(sourceBits, modelBits, deferred, darkNoise: true, subtractDark: true);

    private static void CompareEntryPoints(int sourceBits, int modelBits, bool deferred, bool darkNoise, bool subtractDark)
        => WpfTestHost.Invoke(() =>
    {
        var directory = Directory.CreateTempSubdirectory("cv-calibration-entrypoints-");
        using var devices = new CurrentCameraDefaultsScope();
        DeviceCamera camera = devices.Camera;
        using var cache = new LocalCalibrationCacheManager(camera.Code);
        typeof(DeviceCamera).GetField("<LocalCalibrationCacheManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(camera, cache);
        var physical = camera.PhyCamera!;
        physical.Config = new ConfigPhyCamera();
        physical.Config.FileServerCfg.FileBasePath = directory.FullName;
        physical.SysResourceModel = new SysResourceModel { Code = "fixture" };
        var template = Assert.Single(physical.CalibrationParams).Value;
        var group = Assert.Single(physical.VisualChildren.OfType<GroupResource>());
        var resource = new CalibrationResource(new SysResourceModel
        {
            Name = "identity", Value = "identity.json", Type = (int)ServiceTypes.LumFourColor
        });
        CalibrationResource? darkResource = null;
        CalibrationResource? dsnuResource = null;
        try
        {
            group.AddChild(resource);
            template.Color.LumFourColor.FilePath = resource.Name;
            template.Color.LumFourColor.IsSelected = true;
            string configRoot = Directory.CreateDirectory(Path.Combine(directory.FullName, physical.Code, "cfg")).FullName;
            File.WriteAllText(Path.Combine(configRoot, "identity.json"), $$"""{"bpp":{{modelBits}},"a":1,"e":1,"i":1}""");
            if (darkNoise)
            {
                darkResource = new CalibrationResource(new SysResourceModel
                    { Name = "dark", Value = "dark.json", Type = (int)ServiceTypes.DarkNoise });
                group.AddChild(darkResource);
                template.Normal.DarkNoise.FilePath = darkResource.Name;
                template.Normal.DarkNoise.IsSelected = true;
                File.WriteAllText(Path.Combine(configRoot, "dark.json"), """{"DarkNoiseRatio":2}""");
            }
            if (subtractDark)
            {
                dsnuResource = new CalibrationResource(new SysResourceModel
                    { Name = "measured offset", Value = "offset.dat", Type = (int)ServiceTypes.DSNU });
                group.AddChild(dsnuResource);
                template.Normal.DSNU.FilePath = dsnuResource.Name;
                template.Normal.DSNU.IsSelected = true;
                CreateMeasuredDarkMap(configRoot, modelBits);
            }
            Assert.True(camera.TryGetCalibrationTemplateFiles(template, out var files, out string? error), error);
            using var raw = RawColorCalibrationTests.CreateRaw(2, 2, sourceBits, 3);
            raw.Exp = [1, 1, 1];
            byte[] signal = subtractDark ? SignalBgr : Bgr8;
            raw.Data = Encode(signal, sourceBits);
            int[] expectedBgr = signal.Select((sample, index) =>
            {
                int residual = subtractDark ? Math.Max(0, sample - DarkLevelBgr[index]) : sample;
                int working = residual << (modelBits - 8);
                return Math.Min((1 << modelBits) - 1, darkNoise ? 2 * working : working);
            }).ToArray();
            CalibrationType[] expectedStages = subtractDark ? [CalibrationType.DSNU, CalibrationType.DarkNoise, CalibrationType.LumFourColor]
                : darkNoise ? [CalibrationType.DarkNoise, CalibrationType.LumFourColor] : [CalibrationType.LumFourColor];
            var acquisition = new ImageAcquisitionRecord
            {
                Width = raw.Cols, Height = raw.Rows, Channels = raw.Channels, BitsPerChannel = sourceBits, Exposure = raw.Exp
            };
            var metadata = new LocalFrameMetadata
            {
                Width = raw.Cols, Height = raw.Rows, Channels = raw.Channels, SourceBpp = sourceBits, Exposure = raw.Exp,
                Acquisition = acquisition, DeviceCode = camera.Code, PrimaryBufferKind = LocalFrameBufferKind.CvRaw
            };

            using var captured = LocalFlowFrame.Allocate(metadata, raw.Data.Length, 0);
            using (var lease = captured.Acquire())
            {
                int Read(IntPtr handle, string json, ref uint width, ref uint height, ref uint bpp, ref uint cieBpp,
                    ref uint channels, IntPtr pixels, IntPtr cie)
                {
                    Assert.Equal((2u, 2u, (uint)sourceBits, 3u), (width, height, bpp, channels));
                    Marshal.Copy(raw.Data, 0, pixels, raw.Data.Length);
                    return cvErrorDefine.CV_ERR_SUCCESS;
                }
                LocalCameraCaptureService.CaptureRawFrame(new IntPtr(42), "{}", lease, Read);
            }
            var action = new CVStartCFC("entrypoint-parity") { PersistResults = false };
            using var flowResources = action.RuntimeResources;
            // The flow gets its own RAW frame from the same acquisition, before camera-side calibration.
            using var flowSource = captured.ForkForCalibration();
            action.SetCurrentFrame(flowSource);
            var output = deferred ? LocalColorCalibrationOutput.Deferred : LocalColorCalibrationOutput.FullCie;
            LocalCameraCaptureService.ApplyCaptureCalibration(captured, cache.Prepare(files, template.Name), output);
            AssertPixelsAndMeasurements(captured, modelBits, sourceBits, !deferred, expectedBgr, expectedStages);

            var node = new LocalCalibrationNode { DeviceCode = camera.Code, CalibTempName = template.Name, AllowAcceleration = deferred };
            typeof(LocalCalibrationNode).GetMethod("ExecuteLocal", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(node, [action]);
            Assert.True(action.TryGetCurrentFrame(out var calibrated));
            AssertPixelsAndMeasurements(calibrated!, modelBits, sourceBits, !deferred, expectedBgr, expectedStages);
            Assert.NotSame(flowSource, calibrated);
            using (var original = flowSource.Acquire())
                Assert.Equal(raw.Data, original.CopyRawToArray());

            string path = Path.Combine(directory.FullName, "source.cvraw");
            Assert.True(CVFileUtil.WriteCVRaw(path, raw));
            CameraFileMetadata.Save(path, CameraFileMetadata.AcquisitionKind, acquisition);
            var model = LocalFileCalibrationService.Calibrate(new(camera.Code, directory.FullName, path, "file-parity",
                raw.Exp, template.Id, template.Name, files, cache, default, null));
            using var reopened = LocalFrameFileService.Load(Assert.IsType<string>(model.FileUrl));
            AssertPixelsAndMeasurements(reopened, modelBits, sourceBits, false, expectedBgr, expectedStages);
        }
        finally
        {
            CalibrationResource.CalibrationResources.Remove(resource);
            if (darkResource != null) CalibrationResource.CalibrationResources.Remove(darkResource);
            if (dsnuResource != null) CalibrationResource.CalibrationResources.Remove(dsnuResource);
            string fixturePrefix = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "cv-calibration-entrypoints-"));
            Assert.StartsWith(fixturePrefix, directory.FullName, StringComparison.OrdinalIgnoreCase);
            directory.Delete(recursive: true);
        }
    });

    [Theory]
    [InlineData(CalibrationOutputMode.FullCie)]
    [InlineData(CalibrationOutputMode.DeferredColor)]
    public void ExplicitColorRequestOnBasicTemplateLeavesFrameIntact(CalibrationOutputMode output)
    {
        string defectsPath = Path.GetTempFileName();
        try
        {
            // One defect at (0, 0). If the basic stage is executed before validation, the first pixel changes.
            using (var file = new BinaryWriter(File.Create(defectsPath)))
            {
                file.Write(1u);
                file.Write(0u);
                file.Write(0u);
            }
            using var cache = new LocalCalibrationCacheManager("basic-only");
            var plan = cache.Prepare([new("defect", CalibrationType.DefectPoint, "defects", "defects.dat", defectsPath)], "basic");
            using var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
            {
                Width = 2, Height = 2, Channels = 1, SourceBpp = 8, Exposure = [1]
            }, 4, 0);
            byte[] original = [255, 1, 1, 1];
            using (var input = frame.Acquire()) Marshal.Copy(original, 0, input.RawPointer, original.Length);
            Assert.Throws<InvalidOperationException>(() => plan.Apply(frame, output));
            using var result = frame.Acquire();
            Assert.Equal(original, result.CopyRawToArray());
            Assert.False(result.HasCie);
            Assert.Null(result.ColorCalibration);
            Assert.Empty(result.Metadata.CalibrationTemplate);
        }
        finally { File.Delete(defectsPath); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreparedTemplateRebindsOutputExposureAndGeometryForEachFrame(bool correctDefect)
    {
        string colorPath = Path.GetTempFileName();
        string defectPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(colorPath, """{"bpp":16,"a":2}""");
            using (var data = new BinaryWriter(File.Create(defectPath)))
            {
                data.Write(1u);
                data.Write(0u);
                data.Write(0u);
            }
            List<DeviceCameraCalibrationFile> files = [new("color", CalibrationType.Luminance, "Y", "Y.json", colorPath)];
            if (correctDefect) files.Add(new("defect", CalibrationType.DefectPoint, "defect", "defect.dat", defectPath));
            using var cache = new LocalCalibrationCacheManager("reused-template");
            var plan = cache.Prepare(files, "multi-layout");
            byte[] pixels = [250, 9, 9, 50, 9, 9, 9, 60, 9, 9, 9, 70];
            using var rawOnly = GrayFrame(4, 3, pixels, 0, CVImageFlipMode.Y);
            plan.Apply(rawOnly, CalibrationOutputMode.RawOnly);
            using (var raw = rawOnly.Acquire())
            {
                byte[] flipped = [50, 9, 9, correctDefect ? (byte)9 : (byte)250, 60, 9, 9, 9, 70, 9, 9, 9];
                ushort[] expected = flipped.Select(value => (ushort)(value << 8)).ToArray();
                Assert.Equal(expected, MemoryMarshal.Cast<byte, ushort>(raw.CopyRawToArray().AsSpan()).ToArray());
                Assert.True(raw.IsRawFlipApplied);
                Assert.False(raw.HasCie);
                Assert.Null(raw.ColorCalibration);
            }

            foreach (var (width, height, exposure, output) in new[]
            {
                (2, 3, 2, CalibrationOutputMode.FullCie), (5, 2, 8, CalibrationOutputMode.DeferredColor)
            })
            {
                byte[] image = Enumerable.Repeat((byte)4, width * height).ToArray();
                using var frame = GrayFrame(width, height, image, exposure, CVImageFlipMode.None);
                plan.Apply(frame, output);
                using var result = frame.Acquire();
                Assert.Equal((width, height), (result.ColorCalibration?.Width, result.ColorCalibration?.Height));
                Assert.Equal(new float[] { exposure, exposure, exposure }, result.ColorCalibration!.Exposure);
                float expected = (4 << 8) * 2f / exposure;
                var poi = new PoiParam { Name = "pixel" };
                poi.PoiPoints.Add(new PoiPoint { Id = 1, PointType = PoiShape.Point, PixX = width - 1, PixY = height - 1 });
                var point = Assert.Single(LocalPoiCalculator.Calculate(result, poi).Points);
                Assert.Equal(expected, Assert.IsType<POIResultDataCIEY>(point.Value).Y);
                Assert.Equal(output == CalibrationOutputMode.FullCie, result.HasCie);
            }

            using var invalid = GrayFrame(4, 3, pixels, 0, CVImageFlipMode.Y);
            Assert.Throws<InvalidOperationException>(() => plan.Apply(invalid, CalibrationOutputMode.FullCie));
            using var unchanged = invalid.Acquire();
            Assert.Equal(8, unchanged.Metadata.SourceBpp);
            Assert.False(unchanged.IsRawFlipApplied);
            Assert.Equal(pixels, unchanged.CopyRawToArray());
        }
        finally
        {
            File.Delete(colorPath);
            File.Delete(defectPath);
        }
    }

    [Fact]
    public void SixteenBitLinearityUsesTheSameSampleValuesForPromotedAndNativeFrames()
    {
        string path = Path.GetTempFileName();
        string colorPath = Path.GetTempFileName();
        try
        {
            using (var table = new BinaryWriter(File.Create(path)))
            {
                float[] factors = [2, 0.25f, 1.5f, 0.5f];
                table.Write((uint)factors.Length);
                foreach (float factor in factors) table.Write(factor);
            }
            using var cache = new LocalCalibrationCacheManager("linearity-depth");
            DeviceCameraCalibrationFile linearity = new("LineArity", CalibrationType.LineArity, "linearity", "linearity.dat", path);
            var plan = cache.Prepare([linearity], "basic-only");
            byte[] samples = [1, 127, 128, 255];
            ushort[] expected = [512, 8128, 49152, 32640];
            foreach (int bpp in new[] { 8, 16 })
            {
                byte[] pixels = bpp == 8 ? samples : samples.SelectMany(value => BitConverter.GetBytes((ushort)(value << 8))).ToArray();
                using var frame = GrayFrame(2, 2, pixels, 0, CVImageFlipMode.None, bpp);
                plan.Apply(frame, CalibrationOutputMode.RawOnly);
                using var corrected = frame.Acquire();
                Assert.Equal(16, corrected.Metadata.SourceBpp);
                Assert.Equal(expected, MemoryMarshal.Cast<byte, ushort>(corrected.CopyRawToArray().AsSpan()).ToArray());
                Assert.False(corrected.HasCie);
            }

            File.WriteAllText(colorPath, """{"bpp":8,"a":1}""");
            DeviceCameraCalibrationFile color = new("Y", CalibrationType.Luminance, "8-bit Y", "color8.json", colorPath);
            using var source = GrayFrame(2, 2, samples, 1, CVImageFlipMode.None);
            Assert.Throws<InvalidOperationException>(() => cache.Prepare([linearity, color], "conflicting-depths"));
            using var original = source.Acquire();
            plan.Apply(source, CalibrationOutputMode.RawOnly);
            using var retried = source.Acquire();
            Assert.Equal(8, original.Metadata.SourceBpp);
            Assert.Equal(samples, original.CopyRawToArray());
            Assert.Equal(expected, MemoryMarshal.Cast<byte, ushort>(retried.CopyRawToArray().AsSpan()).ToArray());
        }
        finally
        {
            File.Delete(path);
            File.Delete(colorPath);
        }
    }

    private static LocalFlowFrame GrayFrame(int width, int height, byte[] pixels, float exposure, CVImageFlipMode flip, int bpp = 8)
    {
        var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
        {
            Width = width, Height = height, Channels = 1, SourceBpp = bpp, Exposure = [exposure], FlipMode = flip
        }, pixels.Length, 0);
        using var input = frame.Acquire();
        Marshal.Copy(pixels, 0, input.RawPointer, pixels.Length);
        return frame;
    }

    private static void CreateMeasuredDarkMap(string directory, int modelBits)
    {
        string[] paths = [Path.Combine(directory, "dark-low.cvraw"), Path.Combine(directory, "dark-high.cvraw")];
        for (int i = 0; i < paths.Length; i++)
        {
            using var reference = RawColorCalibrationTests.CreateRaw(2, 2, modelBits, 3);
            reference.Exp = [1, 1, 1];
            int deviation = i == 0 ? -1 : 1;
            reference.Data = Encode(DarkLevelBgr.Select(level => (byte)(level + deviation)).ToArray(), modelBits);
            Assert.True(CVFileUtil.WriteCVRaw(paths[i], reference));
        }
        var darkAverage = CalibrationMapCreation.Average(paths);
        using var map = File.Create(Path.Combine(directory, "offset.dat"));
        CalibrationMapCreation.WriteMap(map, darkAverage, CalibrationType.DSNU);
    }

    private static byte[] Encode(byte[] samples, int bits) => bits == 16
        ? samples.SelectMany(value => BitConverter.GetBytes((ushort)(value << 8))).ToArray()
        : (byte[])samples.Clone();

    private static void AssertPixelsAndMeasurements(LocalFlowFrame frame, int bits, int acquiredBits, bool hasFullCie,
        int[] expectedBgr, CalibrationType[] expectedStages)
    {
        using var lease = frame.Acquire();
        Assert.Equal(bits, lease.Metadata.SourceBpp);
        Assert.Equal(acquiredBits, lease.Metadata.Acquisition?.BitsPerChannel);
        Assert.Equal(bits, lease.ColorCalibration?.RawBpp);
        Assert.Equal(expectedStages, lease.CalibrationProvenance!.Sources.Select(source => (CalibrationType)source.CalibrationType).ToArray());
        byte[] expectedRaw = bits == 8
            ? expectedBgr.Select(sample => (byte)sample).ToArray()
            : expectedBgr.SelectMany(sample => BitConverter.GetBytes((ushort)sample)).ToArray();
        Assert.Equal(expectedRaw, lease.CopyRawToArray());
        Assert.Equal(hasFullCie, lease.HasCie);
        // Independent expected RGB planes for an identity matrix and unit exposure.
        float[] Plane(int bgrChannel) => Enumerable.Range(0, expectedBgr.Length / 3)
            .Select(i => (float)expectedBgr[3 * i + bgrChannel]).ToArray();
        float[] x = Plane(2), y = Plane(1), z = Plane(0);
        if (hasFullCie)
            Assert.Equal(x.Concat(y).Concat(z).ToArray(), MemoryMarshal.Cast<byte, float>(lease.CopyCieToArray().AsSpan()).ToArray());
        var poi = new PoiParam { Name = "all four pixels" };
        for (int i = 0; i < x.Length; i++)
            poi.PoiPoints.Add(new PoiPoint { Id = i + 1, PointType = PoiShape.Point, PixX = i % 2, PixY = i / 2 });
        var points = LocalPoiCalculator.Calculate(lease, poi).Points;
        Assert.Equal(x.Length, points.Count);
        for (int i = 0; i < x.Length; i++)
        {
            var value = Assert.IsType<POIResultDataCIExyuv>(points[i].Value);
            // The POI reporting contract floors nonpositive components before calculating chromaticity.
            Assert.Equal((Math.Max(x[i], 1e-6f), Math.Max(y[i], 1e-6f), Math.Max(z[i], 1e-6f)),
                (value.X, value.Y, value.Z));
        }
    }
}
