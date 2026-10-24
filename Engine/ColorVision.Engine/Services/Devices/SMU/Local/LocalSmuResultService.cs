using ColorVision.Database;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.SMU.Views;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System;
using System.Linq;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.Devices.SMU.Local;

internal static class LocalSmuResultService
{
    private static readonly object SchemaSync = new();
    private static readonly HashSet<string> InitializedSchemas = [];
    internal const int ResultType = 200;
    internal static SMUResultModel CreateMeasurement(LocalSmuCapture capture, string code) => new()
    {
        LocalDetails = capture.Details == null ? null : JsonConvert.SerializeObject(capture.Details),
        DeviceCode = code, CreateDate = capture.CapturedAt, ChannelType = capture.Parameters.Channel,
        IsSourceV = capture.Parameters.IsSourceV, SrcValue = (float)capture.Parameters.MeasureValue,
        LimitValue = (float)capture.Parameters.LimitValue, VResult = (float)capture.V, IResult = (float)capture.I,
        ResultCode = 0, TotalTime = capture.TotalTime
    };
    internal static SmuScanModel CreateScan(LocalSmuCapture capture, string code) => new()
    {
        LocalDetails = capture.Details == null ? null : JsonConvert.SerializeObject(capture.Details),
        DeviceCode = code, CreateDate = capture.CapturedAt, ChannelType = capture.Parameters.Channel,
        IsSourceV = capture.Parameters.IsSourceV, SrcBegin = (float)capture.Parameters.BeginValue,
        SrcEnd = (float)capture.Parameters.EndValue, LimitValue = (float)capture.Parameters.LimitValue,
        Points = capture.Voltages.Length, VResult = JsonConvert.SerializeObject(capture.Voltages),
        IResult = JsonConvert.SerializeObject(capture.Currents.Select(current => current / 1000)), ResultCode = 0, TotalTime = capture.TotalTime
    };
    internal static int Save(LocalSmuCapture capture, string code, CVStartCFC? action = null, int zIndex = 0)
    {
        if (!capture.HasMeasurements) return 0;
        if (!(action?.PersistResults ?? MySqlSetting.IsConnect)) return 0;
        int? batchId = action == null ? null : (BatchResultMasterDao.Instance.GetByNameOrCode(action.SerialNumber)
            ?? throw new InvalidOperationException($"找不到流程批次：{action.SerialNumber}")).Id;
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = DbType.MySql, IsAutoCloseConnection = true });
        bool hasDetails = capture.Details != null;
        if (hasDetails)
        {
            lock (SchemaSync)
            {
                string connection = db.CurrentConnectionConfig.ConnectionString;
                if (!InitializedSchemas.Contains(connection))
                {
                    try { db.CodeFirst.InitTables<SmuLocalDetailsModel>(); }
                    catch (Exception ex) { throw new InvalidOperationException("初始化本地源表详情表失败，请核对数据库建表权限或使用数据库工具初始化表。", ex); }
                    InitializedSchemas.Add(connection);
                }
            }
        }
        if (hasDetails) db.Ado.BeginTran();
        int id;
        try
        {
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
            if (hasDetails)
            {
                if (db.Insertable(new SmuLocalDetailsModel { IsScan = capture.IsScan, ResultId = id,
                    Details = JsonConvert.SerializeObject(capture.Details) }).ExecuteCommand() != 1)
                    throw new InvalidOperationException("保存本地源表模式详情失败。");
                db.Ado.CommitTran();
            }
            return id;
        }
        catch (Exception failure)
        {
            if (hasDetails)
            {
                try { db.Ado.RollbackTran(); }
                catch (Exception rollback) { throw new AggregateException("保存本地源表结果失败，事务回滚也失败。", failure, rollback); }
            }
            throw;
        }
    }
    internal static IReadOnlyDictionary<int, string> ReadDetails(SqlSugarClient db, bool scan, int[] ids)
    {
        Dictionary<int, string> result = [];
        if (ids.Length == 0 || !db.DbMaintenance.IsAnyTable(SmuLocalDetailsModel.TableName, false)) return result;
        foreach (int[] chunk in ids.Distinct().Chunk(500))
            foreach (var row in db.Queryable<SmuLocalDetailsModel>().Where(item => item.IsScan == scan && chunk.Contains(item.ResultId)).OrderBy(item => item.Id).ToList())
                result[row.ResultId] = row.Details;
        return result;
    }
    internal static ViewResultSMU CreateView(LocalSmuCapture capture, string code, int id)
    {
        var view = capture.IsScan ? new ViewResultSMU(CreateScan(capture, code)) : new ViewResultSMU(CreateMeasurement(capture, code));
        view.Id = id; view.ChannelType = capture.Parameters.Channel;
        return view;
    }
    internal static JObject Response(LocalSmuCapture capture, int id)
    {
        if (capture.Details is { } details)
            return JObject.FromObject(new { MasterId = id, MasterResultType = ResultType, capture.Parameters.Channel,
                capture.Parameters.IsSourceV, capture.HasMeasurements, VList = capture.Voltages,
                IList = capture.Currents.Select(value => value / 1000), ScanList = details.SourceValues.Select(capture.Parameters.NativeSource), CurrentUnit = "A", details.Resistance,
                Power = capture.Voltages.Zip(capture.Currents, (v, i) => v * i / 1000).ToArray(), Details = details });
        if (!capture.IsScan) return JObject.FromObject(new { MasterId = id, MasterResultType = ResultType,
            capture.Parameters.Channel, capture.Parameters.IsSourceV, capture.V, capture.I });
        double[] scanValues = new double[capture.Voltages.Length];
        // Preserve the service's scan contract: I arrays and current-source ScanList are in A.
        for (int index = 0; index < scanValues.Length; index++) scanValues[index] = capture.Parameters.NativeSource(capture.Parameters.SourceValues?[index]
            ?? capture.Parameters.BeginValue + (capture.Parameters.EndValue - capture.Parameters.BeginValue) * index / (scanValues.Length - 1));
        return JObject.FromObject(new { MasterId = id, MasterResultType = ResultType, capture.Parameters.Channel,
            VList = capture.Voltages, IList = capture.Currents.Select(current => current / 1000), ScanList = scanValues });
    }
}
