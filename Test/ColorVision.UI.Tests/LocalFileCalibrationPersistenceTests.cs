using ColorVision.Core;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.FileIO;
using ColorVision.ImageEditor.Tif;
using cvColorVision;
using SqlSugar;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class LocalFileCalibrationPersistenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AcceptedCalibrationRemainsInTheOriginalDatabaseAfterReconnect(bool existingBatch)
    {
        using var fixture = new CalibrationFiles();
        using var originalDb = fixture.Open("original.db");
        using var replacementDb = fixture.Open("replacement.db");
        Initialize(originalDb);
        Initialize(replacementDb);
        const string serialNumber = "calibrated-image";
        const string replacementSerialNumber = "unrelated-image";
        if (existingBatch)
            originalDb.Insertable(new MeasureBatchModel { Code = serialNumber, Name = "original batch" }).ExecuteCommand();

        int replacementId = replacementDb.Insertable(new MeasureBatchModel { Code = replacementSerialNumber }).ExecuteReturnIdentity();
        replacementDb.Insertable(new MeasureResultImgModel { BatchId = replacementId, FileUrl = "replacement-image.cvraw" }).ExecuteCommand();

        MySqlConnectedEndpoint? current = null;
        var original = new MySqlConnectedEndpoint(candidate => ReferenceEquals(candidate, current), () => fixture.Open("original.db"));
        var replacement = new MySqlConnectedEndpoint(candidate => ReferenceEquals(candidate, current), () => fixture.Open("replacement.db"));
        current = original;
        DeviceCalibration device = CreateOwner(1, original);
        var accepted = Assert.IsType<MySqlConnectedEndpoint>(LocalFileCalibrationRequest.CapturePersistenceEndpoint(device, current));
        Assert.Same(original, accepted);
        var model = fixture.CreateFileResult();
        current = replacement;
        Assert.False(accepted.IsCurrent);

        LocalFileCalibrationService.TryPersist(accepted, serialNumber, model);

        MeasureBatchModel batch = originalDb.Queryable<MeasureBatchModel>().Single();
        MeasureResultImgModel image = originalDb.Queryable<MeasureResultImgModel>().Single();
        Assert.Equal(replacementId, batch.Id);
        Assert.Equal(serialNumber, batch.Code);
        Assert.Equal(existingBatch ? "original batch" : serialNumber, batch.Name);
        Assert.Equal((batch.Id, model.Id, model.FileUrl), (image.BatchId, image.Id, image.FileUrl));
        Assert.Equal(batch.Id, model.BatchId);
        Assert.True(model.Id > 0);
        Assert.Equal(replacementSerialNumber, replacementDb.Queryable<MeasureBatchModel>().Single().Code);
        Assert.Equal("replacement-image.cvraw", replacementDb.Queryable<MeasureResultImgModel>().Single().FileUrl);
        Assert.Equal(CalibrationFiles.FileContents, File.ReadAllBytes(model.FileUrl!));
    }

    [Fact]
    public void OfflineAdmissionStaysFileOnlyWhenTheConnectionAppears()
    {
        using var fixture = new CalibrationFiles();
        using var db = fixture.Open("reconnected.db");
        Initialize(db);
        MySqlConnectedEndpoint? current = null;
        var endpoint = new MySqlConnectedEndpoint(candidate => ReferenceEquals(candidate, current), () => fixture.Open("reconnected.db"));
        DeviceCalibration device = CreateOwner(-2, null);
        MySqlConnectedEndpoint? accepted = LocalFileCalibrationRequest.CapturePersistenceEndpoint(device, current);
        Assert.Null(accepted);
        var model = fixture.CreateFileResult();
        current = endpoint;
        Assert.Same(endpoint, LocalFileCalibrationRequest.CapturePersistenceEndpoint(device, current));

        LocalFileCalibrationService.TryPersist(accepted, "file-only", model);

        Assert.Empty(db.Queryable<MeasureBatchModel>().ToList());
        Assert.Empty(db.Queryable<MeasureResultImgModel>().ToList());
        Assert.Equal((0, -1), (model.Id, model.BatchId));
        Assert.Equal(CalibrationFiles.FileContents, File.ReadAllBytes(model.FileUrl!));
    }

    [Fact]
    public void AStaleDatabaseOwnerCannotAdmitTheVerifiedReplacement()
    {
        MySqlConnectedEndpoint? current = null;
        var original = new MySqlConnectedEndpoint(candidate => ReferenceEquals(candidate, current), UnexpectedClient);
        var replacement = new MySqlConnectedEndpoint(candidate => ReferenceEquals(candidate, current), UnexpectedClient);
        var device = CreateOwner(1, original);

        current = original;
        Assert.Same(original, LocalFileCalibrationRequest.CapturePersistenceEndpoint(device, current));
        current = replacement;
        Assert.Null(LocalFileCalibrationRequest.CapturePersistenceEndpoint(device, original));
        Assert.Null(LocalFileCalibrationRequest.CapturePersistenceEndpoint(device, current));

        device.ResultDatabaseEndpoint = replacement;
        Assert.Same(replacement, LocalFileCalibrationRequest.CapturePersistenceEndpoint(device, current));

        static SqlSugarClient UnexpectedClient() => throw new InvalidOperationException("Admission must not open a database client.");
    }

    [Fact]
    public void FailedImageInsertRollsBackTheBatchAndLeavesAReopenableCalibratedRaw()
    {
        using var fixture = new CalibrationFiles();
        using var db = fixture.Open("rollback.db");
        Initialize(db);
        db.Ado.ExecuteCommand("CREATE TRIGGER reject_image BEFORE INSERT ON t_scgd_measure_result_img "
            + "BEGIN SELECT RAISE(ABORT, 'synthetic calibration result failure'); END");
        bool batchWasInserted = false;
        var endpoint = new MySqlConnectedEndpoint(_ => true, () =>
        {
            SqlSugarClient writer = fixture.Open("rollback.db");
            writer.Aop.OnLogExecuted = (sql, _) =>
            {
                if (sql.TrimStart().StartsWith("INSERT INTO", StringComparison.OrdinalIgnoreCase)
                    && sql.Contains("t_scgd_measure_batch", StringComparison.OrdinalIgnoreCase))
                    batchWasInserted = true;
            };
            return writer;
        });
        const int width = 4, height = 3, bitDepth = 8, channels = 1;
        const string templateName = "synthetic defect correction";
        string sourcePath = fixture.GetPath("source.cvraw");
        using var source = RawColorCalibrationTests.CreateRaw(width, height, bitDepth, channels);
        Array.Fill(source.Data, (byte)10);
        source.Data[0] = 100;
        Assert.True(CVFileUtil.WriteCVRaw(sourcePath, source));
        string defectsPath = fixture.GetPath("defects.dat");
        using (var writer = new BinaryWriter(File.Create(defectsPath)))
        {
            writer.Write(1u);
            writer.Write(0u);
            writer.Write(0u);
        }

        DeviceCalibration device = CreateOwner(1, endpoint);
        string deviceCode = Assert.IsType<string>(device.SysResourceModel.Code);
        string outputRoot = fixture.GetPath("captures");
        float[] exposure = [3, 5, 7];
        float[] fileExposure = exposure[..channels];
        using var cache = new LocalCalibrationCacheManager(deviceCode);
        var request = new LocalFileCalibrationRequest(deviceCode, outputRoot, sourcePath, "rolled-back", exposure,
            TemplateId: 17, TemplateName: templateName,
            CalibrationFiles: [new DeviceCameraCalibrationFile("defect", CalibrationType.DefectPoint, "defects", "defects.dat", defectsPath)],
            Cache: cache, FallbackRoi: default, PersistenceEndpoint: LocalFileCalibrationRequest.CapturePersistenceEndpoint(device, endpoint));
        device.SysResourceModel.Code = "replacement-calibration";

        MeasureResultImgModel model = LocalFileCalibrationService.Calibrate(request);

        Assert.True(batchWasInserted);
        Assert.Empty(db.Queryable<MeasureBatchModel>().ToList());
        Assert.Empty(db.Queryable<MeasureResultImgModel>().ToList());
        Assert.Equal((0, -1), (model.Id, model.BatchId));
        Assert.Equal(deviceCode, model.DeviceCode);
        string outputPath = Assert.IsType<string>(model.FileUrl);
        Assert.NotEqual(sourcePath, outputPath, StringComparer.OrdinalIgnoreCase);
        Assert.StartsWith(Path.Combine(outputRoot, deviceCode, "Data") + Path.DirectorySeparatorChar, outputPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(CVFileUtil.Read(File.ReadAllBytes(outputPath), out CVCIEFile reopened));
        using (reopened)
        {
            Assert.Equal((width, height, bitDepth, channels), (reopened.Cols, reopened.Rows, reopened.Bpp, reopened.Channels));
            Assert.Equal(fileExposure, reopened.Exp);
            Assert.Equal(source.Data.Length, reopened.Data.Length);
            Assert.All(reopened.Data, pixel => Assert.Equal((byte)10, pixel));
        }
        using LocalFlowFrame loaded = LocalFrameFileService.Load(outputPath);
        Assert.True(loaded.HasRaw);
        Assert.Equal((width, height, bitDepth, channels), (loaded.Metadata.Width, loaded.Metadata.Height, loaded.Metadata.SourceBpp, loaded.Metadata.Channels));
        Assert.Equal(fileExposure, loaded.Metadata.Exposure);
        Assert.Equal(templateName, loaded.CalibrationProvenance?.Template);
    }

    [Fact]
    public void DuplicateBatchCodesLeaveTheCalibrationResultFileOnly()
    {
        using var fixture = new CalibrationFiles();
        using var db = fixture.Open("ambiguous.db");
        Initialize(db);
        const string serialNumber = "ambiguous";
        db.Insertable(new MeasureBatchModel { Code = serialNumber, Name = "first run" }).ExecuteCommand();
        db.Insertable(new MeasureBatchModel { Code = serialNumber, Name = "second run" }).ExecuteCommand();
        var endpoint = new MySqlConnectedEndpoint(_ => true, () => fixture.Open("ambiguous.db"));
        var model = fixture.CreateFileResult();

        LocalFileCalibrationService.TryPersist(endpoint, serialNumber, model);

        Assert.Equal(new[] { "first run", "second run" }, db.Queryable<MeasureBatchModel>().OrderBy(batch => batch.Id)
            .Select(batch => batch.Name).ToArray());
        Assert.Empty(db.Queryable<MeasureResultImgModel>().ToList());
        Assert.Equal((0, -1), (model.Id, model.BatchId));
        Assert.Equal(CalibrationFiles.FileContents, File.ReadAllBytes(model.FileUrl!));
    }

    [Theory]
    [InlineData(1, "16", 16)]
    [InlineData(3, "16", 16)]
    [InlineData(1, "8", 8)]
    [InlineData(1, "16.0", 8)]
    [InlineData(1, "\"16\"", 8)]
    [InlineData(1, null, 8)]
    public void ManualCalibrationPromotesOnlyForExplicit16BitColorAssets(int channels, string? declaredDepth, int expectedBpp)
    {
        using var fixture = new CalibrationFiles();
        const int width = 3, height = 2;
        string sourcePath = fixture.GetPath("source.cvraw");
        using CVCIEFile source = RawColorCalibrationTests.CreateRaw(width, height, 8, channels);
        byte[] samples = [0, 1, 127, 128, 254, 255];
        for (int i = 0; i < source.Data.Length; i++) source.Data[i] = samples[i % samples.Length];
        Assert.True(CVFileUtil.WriteCVRaw(sourcePath, source));
        var acquisition = new ImageAcquisitionRecord
        {
            Width = width, Height = height, BitsPerChannel = 8, Channels = channels,
            SensorRoi = new ImageSensorRoi(2, 3, width, height), Exposure = source.Exp
        };
        CameraFileMetadata.Save(sourcePath, CameraFileMetadata.AcquisitionKind, acquisition);
        byte[] sourceBytes = File.ReadAllBytes(sourcePath);
        string calibrationPath = fixture.GetPath("color.json");
        string calibration = channels == 1 ? "\"a\":1" : "\"a\":1,\"e\":1,\"i\":1,\"Gain_x\":1,\"Gain_y\":1,\"Gain_z\":1";
        if (declaredDepth != null) calibration += $", /* legacy comments are accepted */ \"bpp\":{declaredDepth}";
        File.WriteAllText(calibrationPath, "{" + calibration + "}", new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        const string deviceCode = "file-depth-calibration";
        using var cache = new LocalCalibrationCacheManager(deviceCode);
        var request = new LocalFileCalibrationRequest(deviceCode, fixture.GetPath("captures"), sourcePath, "depth-case", source.Exp,
            TemplateId: 17, TemplateName: "color depth",
            CalibrationFiles: [new DeviceCameraCalibrationFile("color", channels == 1 ? CalibrationType.Luminance : CalibrationType.LumFourColor,
                "color", "color.json", calibrationPath)], Cache: cache, FallbackRoi: default, PersistenceEndpoint: null);

        MeasureResultImgModel result = LocalFileCalibrationService.Calibrate(request);

        using (LocalFlowFrame input = LocalFrameFileService.Load(sourcePath))
        {
            using LocalFlowFrameLease inputLease = input.Acquire();
            Assert.Equal(8, input.Metadata.SourceBpp);
            Assert.Equal(source.Data, inputLease.CopyRawToArray());
            if (expectedBpp == 16)
            {
                Assert.Equal(sourceBytes, File.ReadAllBytes(sourcePath));
                Assert.Null(input.ColorCalibration);
            }
            else
            {
                Assert.Equal(8, input.ColorCalibration?.RawBpp);
                Assert.True(input.ColorCalibration?.CanReplay);
            }
        }

        using LocalFlowFrame output = LocalFrameFileService.Load(Assert.IsType<string>(result.FileUrl));
        Assert.Equal(expectedBpp, output.Metadata.SourceBpp);
        Assert.Equal(expectedBpp, output.ColorCalibration!.RawBpp);
        Assert.True(output.ColorCalibration.CanReplay);
        Assert.Equal(acquisition.SensorRoi, output.Metadata.Acquisition?.SensorRoi);
        Assert.Equal(8, output.Metadata.Acquisition?.BitsPerChannel);
        using LocalFlowFrameLease lease = output.Acquire();
        byte[] expected = expectedBpp == 16 ? source.Data.SelectMany(pixel => new byte[] { 0, pixel }).ToArray() : source.Data;
        Assert.Equal(expected, lease.CopyRawToArray());
    }

    [Fact]
    public void PreparedColorKeepsDepthAndCoefficientsFromTheSameRevision()
    {
        using var fixture = new CalibrationFiles();
        string path = fixture.GetPath("selected-color.json");
        File.WriteAllText(path, """{"bpp":16,"a":1}""");
        var firstWrite = File.GetLastWriteTimeUtc(path);
        DeviceCameraCalibrationFile[] files = [new("color", CalibrationType.Luminance, "Y", "selected-color.json", path)];
        using var cache = new LocalCalibrationCacheManager("color-revisions");
        var first = cache.Prepare(files, "luminance");
        File.WriteAllText(path, """{"bpp": 8,"a":2}""");
        File.SetLastWriteTimeUtc(path, firstWrite);
        var second = cache.Prepare(files, "luminance");

        static (int Depth, float Y) Measure(LocalPreparedCalibration plan)
        {
            using var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
                { Width = 1, Height = 1, Channels = 1, SourceBpp = 8, Exposure = [1] }, 1, 0);
            using (var input = frame.Acquire()) Marshal.WriteByte(input.RawPointer, 3);
            plan.Apply(frame, CalibrationOutputMode.FullCie);
            using var result = frame.Acquire();
            return (result.Metadata.SourceBpp, BitConverter.ToSingle(result.CopyCieToArray()));
        }

        Assert.Equal((8, 6f), Measure(second));
        Assert.Equal((16, 768f), Measure(first));
        cache.ReleaseCache();
        Assert.Throws<IOException>(() => Measure(first));
        Assert.Equal((8, 6f), Measure(second));
    }

    [Theory]
    [InlineData(8, false)]
    [InlineData(16, true)]
    public void HistoricalPlanarInputsUsePackedMemoryAndRetainTheirOnDiskColorOrder(int depth, bool sourceCanReplay)
    {
        using var fixture = new CalibrationFiles();
        string sourcePath = fixture.GetPath("planar.cvraw");
        using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(2, 2, depth, 3);
        raw.Exp = [1, 1, 1];
        int[] planar = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
        if (depth == 16) planar = planar.Select(value => value * 257 + 1).ToArray();
        byte[] Encode(IEnumerable<int> samples) => depth == 8
            ? samples.Select(sample => (byte)sample).ToArray()
            : samples.SelectMany(sample => BitConverter.GetBytes((ushort)sample)).ToArray();
        raw.Data = Encode(planar);
        byte[] packed = Encode(Enumerable.Range(0, 4).SelectMany(i => new[] { planar[8 + i], planar[4 + i], planar[i] }));
        Assert.True(CVFileUtil.WriteCVRaw(sourcePath, raw));
        var identity = RawColorTransformV1.Create();
        identity.CalibrationType = (int)CalibrationType.LumFourColor;
        identity.InterleavedBgr = 0;
        identity.Coefficients[0] = identity.Coefficients[4] = identity.Coefficients[8] = 1;
        ColorCalibrationSnapshot.Create(identity, 2, 2, depth, raw.Exp, "original").Save(sourcePath, sourceCanReplay);
        using LocalFlowFrame loaded = LocalFrameFileService.Load(sourcePath);
        using LocalFlowFrame frame = LocalFrameFileService.Load(sourcePath);
        Assert.True(frame.ColorCalibration!.InterleavedBgr);
        Assert.Equal(sourceCanReplay, frame.ColorCalibration.CanReplay);
        using (var input = frame.Acquire()) Assert.Equal(packed, input.CopyRawToArray());

        string colorPath = fixture.GetPath("identity.json");
        File.WriteAllText(colorPath, """{"a":1,"e":1,"i":1}""");
        using var cache = new LocalCalibrationCacheManager("historical-planar");
        var plan = cache.Prepare([new("color", CalibrationType.LumFourColor, "identity", "identity.json", colorPath)], "recalibrated");
        plan.Apply(frame, CalibrationOutputMode.FullCie);
        using (var result = frame.Acquire())
            Assert.Equal(planar.Select(value => (float)value).ToArray(), MemoryMarshal.Cast<byte, float>(result.CopyCieToArray().AsSpan()).ToArray());
        LocalFrameFileService.SaveColorParametersToInput(frame);
        Assert.True(CVFileUtil.Read(sourcePath, out CVCIEFile source));
        using (source)
        {
            Assert.Equal(raw.Data, source.Data);
            var stored = ColorCalibrationSnapshot.Read(sourcePath, source)!;
            Assert.Equal("recalibrated", stored.Template);
            Assert.False(stored.InterleavedBgr);
            Assert.True(stored.CanReplay);
        }

        string outputPath = fixture.GetPath("packed.cvraw");
        LocalFrameFileService.SaveCapture(frame, outputPath);
        LocalFrameFileService.SaveColorParametersToInput(frame);
        Assert.True(CVFileUtil.Read(outputPath, out CVCIEFile output));
        using (output)
        {
            Assert.Equal(packed, output.Data);
            Assert.True(ColorCalibrationSnapshot.Read(outputPath, output)!.InterleavedBgr);
        }
        LocalFrameFileService.SaveCapture(frame, sourcePath);
        LocalFrameFileService.SaveColorParametersToInput(loaded);
        Assert.True(CVFileUtil.Read(sourcePath, out CVCIEFile replaced));
        using (replaced)
        {
            Assert.Equal(packed, replaced.Data);
            Assert.True(ColorCalibrationSnapshot.Read(sourcePath, replaced)!.InterleavedBgr);
        }
        using CVCIEFile resized = RawColorCalibrationTests.CreateRaw(1, 4, depth, 3);
        Assert.True(CVFileUtil.WriteCVRaw(sourcePath, resized));
        byte[] resizedFile = File.ReadAllBytes(sourcePath);
        Assert.Throws<InvalidDataException>(() => LocalFrameFileService.SaveColorParametersToInput(loaded));
        Assert.Equal(resizedFile, File.ReadAllBytes(sourcePath));
    }

    [Fact]
    public void Historical8BitSnapshotReplaysAt8BitsAndFresh16BitCalibrationKeepsTheOriginal()
    {
        using var fixture = new CalibrationFiles();
        string sourcePath = fixture.GetPath("historical.cvraw");
        using CVCIEFile source = RawColorCalibrationTests.CreateRaw(3, 2, 8, 1);
        Assert.True(CVFileUtil.WriteCVRaw(sourcePath, source));
        RawColorTransformV1 transform = RawColorTransformV1.Create();
        transform.CalibrationType = (int)CalibrationType.Luminance;
        transform.Channels = 1;
        transform.Kind = 2;
        transform.Coefficients[0] = 2;
        ColorCalibrationSnapshot.Create(transform, source.Cols, source.Rows, source.Bpp, source.Exp, "historical").Save(sourcePath, true);
        byte[] sourceBytes = File.ReadAllBytes(sourcePath);

        using LocalFlowFrame loaded = LocalFrameFileService.Load(sourcePath);

        Assert.Equal(8, loaded.Metadata.SourceBpp);
        Assert.Equal("historical", loaded.ColorCalibration?.Template);
        Assert.Equal(sourcePath, loaded.CvRawFilePath);
        using LocalFlowFrameLease lease = loaded.Acquire();
        Assert.Equal(source.Data, lease.CopyRawToArray());
        LocalFrameCalibrationService.ReuseColorCalibration(loaded, LocalColorCalibrationOutput.FullCie);
        Assert.Equal(8, loaded.Metadata.SourceBpp);

        string colorPath = fixture.GetPath("fresh.json");
        File.WriteAllText(colorPath, """{"bpp":16,"a":1}""");
        using var cache = new LocalCalibrationCacheManager("historical");
        var plan = cache.Prepare([new("color", CalibrationType.Luminance, "fresh", "fresh.json", colorPath)], "fresh");
        plan.Apply(loaded, CalibrationOutputMode.DeferredColor);
        Assert.Equal(16, loaded.Metadata.SourceBpp);
        Assert.Equal("fresh", loaded.ColorCalibration?.Template);
        LocalFrameFileService.SaveColorParametersToInput(loaded);
        Assert.Equal(sourceBytes, File.ReadAllBytes(sourcePath));
        using var original = LocalFrameFileService.Load(sourcePath);
        Assert.Equal("historical", original.ColorCalibration?.Template);
        string outputPath = fixture.GetPath("promoted.cvraw");
        LocalFrameFileService.SaveCapture(loaded, outputPath);
        using var output = LocalFrameFileService.Load(outputPath);
        Assert.Equal(16, output.ColorCalibration?.RawBpp);
        Assert.True(output.ColorCalibration?.CanReplay);
    }

    [Fact]
    public void BitmapPromotionPreservesInterleavedChannelOrder() => StaTest.Run(() =>
    {
        using var fixture = new CalibrationFiles();
        string path = fixture.GetPath("input.png");
        byte[] pixels = [0, 128, 255, 17, 1, 254];
        BitmapSource bitmap = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Bgr24, null, pixels, pixels.Length);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (FileStream stream = File.Create(path)) encoder.Save(stream);

        using LocalFlowFrame source = LocalFrameFileService.Load(path);
        Assert.Equal(8, source.Metadata.SourceBpp);
        source.ApplyCalibration("working-depth", 0, false, null, (_, _) => default, promoteRaw8To16: true);
        using LocalFlowFrameLease lease = source.Acquire();
        Assert.Equal(16, source.Metadata.SourceBpp);
        Assert.Equal(3, source.Metadata.Channels);
        Assert.Equal(8, source.Metadata.SourceFileBpp);
        Assert.Null(source.Metadata.Acquisition);
        Assert.Equal(new byte[] { 0, 0, 0, 128, 0, 255, 0, 17, 0, 1, 0, 254 }, lease.CopyRawToArray());
    });

    private static void Initialize(SqlSugarClient db) => db.CodeFirst.InitTables(typeof(MeasureBatchModel), typeof(MeasureResultImgModel));

    private static DeviceCalibration CreateOwner(int id, MySqlConnectedEndpoint? endpoint)
    {
        var device = (DeviceCalibration)RuntimeHelpers.GetUninitializedObject(typeof(DeviceCalibration));
        device.SysResourceModel = new SysResourceModel { Id = id, Code = "offline-calibration" };
        device.ResultDatabaseEndpoint = endpoint;
        return device;
    }

    private sealed class CalibrationFiles : IDisposable
    {
        internal static readonly byte[] FileContents = [1, 2, 3];
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("cv-local-file-calibration-");

        internal SqlSugarClient Open(string name) => new(new ConnectionConfig
        {
            ConnectionString = $"Data Source={GetPath(name)};Pooling=False",
            DbType = DbType.Sqlite, InitKeyType = InitKeyType.Attribute, IsAutoCloseConnection = true,
        });

        internal MeasureResultImgModel CreateFileResult()
        {
            string path = GetPath("calibrated.cvraw");
            File.WriteAllBytes(path, FileContents);
            return new MeasureResultImgModel { BatchId = -1, FileUrl = path, DeviceCode = "offline-calibration" };
        }

        internal string GetPath(string name) => Path.Combine(directory.FullName, name);

        public void Dispose()
        {
            string fixturePrefix = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "cv-local-file-calibration-"));
            if (!Path.GetFullPath(directory.FullName).StartsWith(fixturePrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Invalid local file calibration fixture path.");
            directory.Delete(recursive: true);
        }
    }
}
