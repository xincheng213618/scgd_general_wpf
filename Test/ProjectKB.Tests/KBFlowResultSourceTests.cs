using ColorVision.Engine;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ProjectKB;
using SqlSugar;
using System.IO;
using Xunit;

namespace ProjectKB.Tests;

public sealed class KBFlowResultSourceTests
{
    [Fact]
    public void LiveBatchReadsOriginalTemplatePointsAndImageAfterTheActiveDatabaseChanges()
    {
        using var original = new BatchDatabase("original");
        using var replacement = new BatchDatabase("replacement");
        Assert.Equal(original.BatchId, replacement.BatchId);
        Assert.Equal(original.PointMasterId, replacement.PointMasterId);

        BatchDatabase active = original;
        var source = new KBFlowResultSource(active.OpenDatabase, original.BatchId, original.Code);
        active = replacement;

        var measurements = Assert.IsType<KBFlowMeasurements>(source.ReadMeasurements());
        var (keyboard, templateJson) = Assert.Single(measurements.Keyboards);
        Assert.Equal("original-keyboard.png", keyboard.ResultImagFile);
        Assert.Equal("{\"source\":\"original\"}", templateJson);
        var (type, point) = Assert.Single(measurements.Points);
        Assert.Equal(ViewResultAlgType.POI_Y, type);
        Assert.Equal("original", point.PoiName);
        Assert.Equal("original-photo.png", Assert.Single(source.GetImages(original.BatchId)).FileUrl);

        var unrelated = new KBFlowResultSource(active.OpenDatabase, original.BatchId, original.Code);
        Assert.Null(unrelated.ReadMeasurements());
        Assert.Empty(unrelated.GetImages(original.BatchId));
        Assert.Empty(source.GetImages(original.BatchId + 1));
    }

    private sealed class BatchDatabase : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"cv-kb-flow-results-{Guid.NewGuid():N}.db");

        public BatchDatabase(string code)
        {
            Code = code;
            using var db = OpenDatabase();
            db.CodeFirst.InitTables(typeof(MeasureBatchModel), typeof(AlgResultMasterModel), typeof(ModMasterModel),
                typeof(PoiPointResultModel), typeof(MeasureResultImgModel));
            BatchId = db.Insertable(new MeasureBatchModel { Code = Code, Name = Code, CreateDate = DateTime.Now }).ExecuteReturnIdentity();
            db.Insertable(new AlgResultMasterModel
            {
                BatchId = BatchId, TName = "shared-template", ImgFileType = ViewResultAlgType.KB_Raw,
                ResultImagFile = $"{code}-keyboard.png", CreateDate = DateTime.Now,
            }).ExecuteCommand();
            db.Insertable(new ModMasterModel { Name = "shared-template", Pid = 150, JsonVal = $"{{\"source\":\"{code}\"}}" }).ExecuteCommand();
            PointMasterId = db.Insertable(new AlgResultMasterModel
            {
                BatchId = BatchId, ImgFileType = ViewResultAlgType.POI_Y, CreateDate = DateTime.Now,
            }).ExecuteReturnIdentity();
            db.Insertable(new PoiPointResultModel
            {
                Pid = PointMasterId, PoiId = 1, PoiName = code, PoiX = 0, PoiY = 0, PoiWidth = 4, PoiHeight = 4,
                Value = "{\"Y\":3}",
            }).ExecuteCommand();
            db.Insertable(new MeasureResultImgModel { BatchId = BatchId, FileUrl = $"{code}-photo.png" }).ExecuteCommand();
        }

        public string Code { get; }
        public int BatchId { get; }
        public int PointMasterId { get; }

        public SqlSugarClient OpenDatabase() => new(new ConnectionConfig
        {
            ConnectionString = $"Data Source={_path};Pooling=False",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute,
        });

        public void Dispose() => File.Delete(_path);
    }
}
