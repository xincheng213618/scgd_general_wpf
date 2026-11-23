using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.FileIO;
using cvColorVision;
using SqlSugar;
using System.IO;
using System.Runtime.CompilerServices;

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
