using ColorVision.Database;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.SMU.Views;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.SMU.Local;

internal static class LocalSmuResultService
{
    internal const int ResultType = 200;
    internal static SMUResultModel CreateMeasurement(LocalSmuCapture capture, string code) => new()
    {
        DeviceCode = code, CreateDate = capture.CapturedAt, ChannelType = capture.Parameters.Channel,
        IsSourceV = capture.Parameters.IsSourceV, SrcValue = (float)capture.Parameters.MeasureValue,
        LimitValue = (float)capture.Parameters.LimitValue, VResult = (float)capture.V, IResult = (float)capture.I,
        ResultCode = 0, TotalTime = capture.TotalTime
    };
    internal static SmuScanModel CreateScan(LocalSmuCapture capture, string code) => new()
    {
        DeviceCode = code, CreateDate = capture.CapturedAt, ChannelType = capture.Parameters.Channel,
        IsSourceV = capture.Parameters.IsSourceV, SrcBegin = (float)capture.Parameters.BeginValue,
        SrcEnd = (float)capture.Parameters.EndValue, LimitValue = (float)capture.Parameters.LimitValue,
        Points = capture.Voltages.Length, VResult = JsonConvert.SerializeObject(capture.Voltages),
        IResult = JsonConvert.SerializeObject(capture.Currents.Select(current => current / 1000)), ResultCode = 0, TotalTime = capture.TotalTime
    };
    internal static int Save(LocalSmuCapture capture, string code, CVStartCFC? action = null, int zIndex = 0)
    {
        if (!(action?.PersistResults ?? MySqlSetting.IsConnect)) return 0;
        int? batchId = action == null ? null : (BatchResultMasterDao.Instance.GetByNameOrCode(action.SerialNumber)
            ?? throw new InvalidOperationException($"找不到流程批次：{action.SerialNumber}")).Id;
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = DbType.MySql, IsAutoCloseConnection = true });
        int id;
        if (capture.IsScan)
        {
            var model = CreateScan(capture, code);
            model.BatchId = batchId; model.ZIndex = zIndex;
            id = db.Insertable(model).ExecuteReturnIdentity();
        }
        else
        {
            var model = CreateMeasurement(capture, code);
            model.BatchId = batchId; model.ZIndex = zIndex;
            id = db.Insertable(model).ExecuteReturnIdentity();
        }
        if (id <= 0) throw new InvalidOperationException("保存本地源表结果失败。");
        return id;
    }
    internal static ViewResultSMU CreateView(LocalSmuCapture capture, string code, int id)
    {
        var view = capture.IsScan ? new ViewResultSMU(CreateScan(capture, code)) : new ViewResultSMU(CreateMeasurement(capture, code));
        view.Id = id; view.ChannelType = capture.Parameters.Channel;
        return view;
    }
    internal static JObject Response(LocalSmuCapture capture, int id)
    {
        if (!capture.IsScan) return JObject.FromObject(new { MasterId = id, MasterResultType = ResultType,
            capture.Parameters.Channel, capture.Parameters.IsSourceV, capture.V, capture.I });
        double[] scanValues = new double[capture.Voltages.Length];
        // Preserve the service's scan contract: I arrays and current-source ScanList are in A.
        for (int index = 0; index < scanValues.Length; index++) scanValues[index] = capture.Parameters.NativeSource(capture.Parameters.BeginValue
            + (capture.Parameters.EndValue - capture.Parameters.BeginValue) * index / (scanValues.Length - 1));
        return JObject.FromObject(new { MasterId = id, MasterResultType = ResultType, capture.Parameters.Channel,
            VList = capture.Voltages, IList = capture.Currents.Select(current => current / 1000), ScanList = scanValues });
    }
}
