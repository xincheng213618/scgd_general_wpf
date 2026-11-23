using ColorVision.Engine;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using SqlSugar;

namespace ProjectKB;

internal sealed class KBFlowResultSource(Func<SqlSugarClient> openDatabase, int batchId, string batchCode)
{
    internal KBFlowMeasurements? ReadMeasurements()
    {
        using var db = openDatabase();
        if (!ContainsOwnedBatch(db)) return null;

        var results = db.Queryable<AlgResultMasterModel>().Where(row => row.BatchId == batchId).ToList();
        var keyboards = new List<(AlgResultMasterModel Result, string? TemplateJson)>();
        var points = new List<(ViewResultAlgType Type, PoiPointResultModel Point)>();
        foreach (var result in results)
        {
            if (result.ImgFileType is ViewResultAlgType.KB or ViewResultAlgType.KB_Raw)
            {
                var template = db.Queryable<ModMasterModel>().First(row => row.Name == result.TName && row.Pid == 150);
                keyboards.Add((result, template?.JsonVal));
            }
            else if (result.ImgFileType is ViewResultAlgType.POI_Y or ViewResultAlgType.POI_Y_V2)
            {
                points.AddRange(db.Queryable<PoiPointResultModel>().Where(row => row.Pid == result.Id).ToList()
                    .Select(point => (result.ImgFileType, point)));
            }
        }

        return new KBFlowMeasurements(keyboards, points);
    }

    internal IReadOnlyList<MeasureResultImgModel> GetImages(int requestedBatchId)
    {
        if (requestedBatchId != batchId || batchId <= 0) return [];

        using var db = openDatabase();
        if (!ContainsOwnedBatch(db)) return [];

        return db.Queryable<MeasureResultImgModel>().Where(row => row.BatchId == batchId).OrderBy(row => row.Id).ToList();
    }

    private bool ContainsOwnedBatch(SqlSugarClient db) => batchId > 0 && !string.IsNullOrWhiteSpace(batchCode)
        && db.Queryable<MeasureBatchModel>().Any(row => row.Id == batchId && row.Code == batchCode);
}

internal sealed record KBFlowMeasurements(
    IReadOnlyList<(AlgResultMasterModel Result, string? TemplateJson)> Keyboards,
    IReadOnlyList<(ViewResultAlgType Type, PoiPointResultModel Point)> Points);
