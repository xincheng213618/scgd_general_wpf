using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing;
using SqlSugar;
using System.Diagnostics;

namespace ProjectARVRPro;

internal sealed class ArvrFlowBatchReceipt
{
    // The supported older host can start by code but does not expose the owned-batch API.
    private static readonly Func<BatchResultMasterDao, MeasureBatchModel, Func<SqlSugarClient>>? InsertOwnedBatch =
        typeof(BatchResultMasterDao).GetMethod(nameof(BatchResultMasterDao.InsertForFlow), [typeof(MeasureBatchModel)])
            ?.CreateDelegate<Func<BatchResultMasterDao, MeasureBatchModel, Func<SqlSugarClient>>>();
    private static readonly Func<FlowControl, string, MeasureBatchModel, CancellationToken, Task<bool>>? StartOwnedBatch =
        typeof(FlowControl).GetMethod(nameof(FlowControl.TryStartWithBatchAsync),
                [typeof(string), typeof(MeasureBatchModel), typeof(CancellationToken)])
            ?.CreateDelegate<Func<FlowControl, string, MeasureBatchModel, CancellationToken, Task<bool>>>();
    private static readonly Func<FlowControl, string, int, string, long, CancellationToken, Task<bool>>? StartLegacyBatch =
        typeof(FlowControl).GetMethod(nameof(FlowControl.TryStartWithBatchAsync),
                [typeof(string), typeof(int), typeof(string), typeof(long), typeof(CancellationToken)])
            ?.CreateDelegate<Func<FlowControl, string, int, string, long, CancellationToken, Task<bool>>>();

    private readonly string? _legacyConnectionString;
    private readonly long _insertStartedAt;

    internal ArvrFlowBatchReceipt(MeasureBatchModel batch, Func<SqlSugarClient> openDatabase, string? legacyConnectionString = null,
        long insertStartedAt = 0)
    {
        if (batch.Id <= 0 || string.IsNullOrWhiteSpace(batch.Code))
            throw new InvalidOperationException("创建 ARVR 流程批次未返回有效的批次 ID 和批次号。");

        Batch = batch;
        BatchId = batch.Id;
        Code = batch.Code;
        OpenDatabase = openDatabase;
        _legacyConnectionString = legacyConnectionString;
        _insertStartedAt = insertStartedAt;
    }

    internal MeasureBatchModel Batch { get; }
    internal int BatchId { get; }
    internal string Code { get; }
    internal Func<SqlSugarClient> OpenDatabase { get; }

    internal static ArvrFlowBatchReceipt Insert(MeasureBatchModel batch, long insertStartedAt)
    {
        if (InsertOwnedBatch != null && StartOwnedBatch != null)
            return new(batch, InsertOwnedBatch(BatchResultMasterDao.Instance, batch));

        string connectionString = MySqlControl.GetConnectionString();
        SqlSugarClient OpenLegacyDatabase() => new(new ConnectionConfig
        {
            ConnectionString = connectionString,
            DbType = DbType.MySql,
            IsAutoCloseConnection = true,
        });

        using var db = OpenLegacyDatabase();
        batch.Id = DatabaseCommandTiming.Execute(db, "ARVR.CreateBatch",
            () => db.Insertable(batch).ExecuteReturnIdentity(), batch.Code);
        return new(batch, OpenLegacyDatabase, connectionString, insertStartedAt);
    }

    internal Task<bool> StartAsync(FlowControl control, CancellationToken cancellationToken = default)
    {
        if (_legacyConnectionString == null)
            return StartOwnedBatch!(control, Code, Batch, cancellationToken);

        if (!string.Equals(_legacyConnectionString, MySqlControl.GetConnectionString(), StringComparison.Ordinal))
            throw new InvalidOperationException("流程批次创建后数据库连接已更改，请重新启动 ARVR 检测。");

        return StartLegacyBatch != null
            ? StartLegacyBatch(control, Code, BatchId, _legacyConnectionString, _insertStartedAt, cancellationToken)
            : control.TryStartAsync(Code, cancellationToken);
    }

    internal void WriteOutcome()
    {
        using var db = OpenDatabase();
        var status = Batch.FlowStatus;
        int duration = Batch.TotalTime;
        string? outcome = Batch.Result;
        int updated = DatabaseCommandTiming.Execute(db, "ARVR.FinalizeBatch",
            () => db.Updateable<MeasureBatchModel>()
                .SetColumns(batch => new MeasureBatchModel { FlowStatus = status, TotalTime = duration, Result = outcome })
                .Where(batch => batch.Id == BatchId && batch.Code == Code).ExecuteCommand(), Code);
        if (updated != 1)
            throw new InvalidOperationException($"找不到可回写的 ARVR 流程批次：{BatchId}。");
    }
}
