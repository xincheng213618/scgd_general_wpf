using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using SqlSugar;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.FlowProcessing.Monitoring;

internal static class LiveIvlReader
{
    internal static Task<LiveIvlSnapshot> ReadAsync(MeasureBatchModel selectedBatch, CancellationToken token) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        var createClient = BatchResultMasterDao.Instance.TryGetOwnedDatabaseFactory(selectedBatch)
            ?? throw new InvalidOperationException(EngineLocalization.Get("找不到当前批次所属的结果数据库连接。"));
        using var db = createClient();
        db.Ado.CommandTimeOut = 5;
        int batchId = selectedBatch.Id;
        var batch = db.Queryable<MeasureBatchModel>().Where(p => p.Id == batchId).First()
            ?? throw new InvalidOperationException(EngineLocalization.Get("找不到所选批次，请检查批次 ID。"));
        int limit = LiveIvlData.MaximumRows;
        var smu = db.Queryable<SMUResultModel>().Where(p => p.BatchId == batchId && (p.ResultCode == null || p.ResultCode == 0))
            .OrderBy(p => p.Id, OrderByType.Desc).Take(limit).ToList();
        token.ThrowIfCancellationRequested();
        var scans = db.Queryable<SmuScanModel>().Where(p => p.BatchId == batchId && (p.ResultCode == null || p.ResultCode == 0))
            .OrderBy(p => p.Id, OrderByType.Desc).Take(20).ToList();
        var spectrum = db.Queryable<SpectumResultEntity>().Where(p => p.BatchId == batchId && !p.DataType && (p.ResultCode == null || p.ResultCode == 0))
            .OrderBy(p => p.Id, OrderByType.Desc).Take(limit)
            .Select(p => new LiveIvlOpticalRow { Id = p.Id, SmuId = p.SmuDataId, Device = p.DeviceCode, Node = p.Zindex,
                Time = p.CreateDate, Voltage = p.VResult, Current = p.IResult, Luminance = p.fPh }).ToList();
        token.ThrowIfCancellationRequested();
        var camera = db.Queryable<PoiPointResultModel, AlgResultMasterModel>((p, m) => new JoinQueryInfos(JoinType.Inner, p.Pid == m.Id))
            .Where((p, m) => m.BatchId == batchId && m.ImgFileType == ViewResultAlgType.POI_XYZ && (m.ResultCode == null || m.ResultCode == 0))
            .OrderBy((p, m) => p.Id, OrderByType.Desc).Take(limit)
            .Select((p, m) => new LiveIvlOpticalRow { Id = p.Id, SmuId = m.SMUDataID, Device = m.DeviceCode, Node = m.Zindex,
                Poi = p.PoiName, Value = p.Value, Time = m.CreateDate, Voltage = m.VResult, Current = m.IResult }).ToList();
        foreach (var row in camera)
        {
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(row.Value)) continue;
            try
            {
                if (Newtonsoft.Json.Linq.JObject.Parse(row.Value)["Y"] == null) continue;
                row.Luminance = new PoiResultCIExyuvData(new PoiPointResultModel { Value = row.Value }).Y;
            }
            catch (Newtonsoft.Json.JsonException) { row.Luminance = null; }
            catch (NullReferenceException) { row.Luminance = null; }
        }
        token.ThrowIfCancellationRequested();
        return LiveIvlData.Build(batch, smu, scans, camera, spectrum,
            smu.Count == limit || scans.Count == 20 || camera.Count == limit || spectrum.Count == limit);
    }, token);
}
