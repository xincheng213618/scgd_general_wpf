using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing;
using SqlSugar;
using System.Diagnostics;

namespace ProjectKB;

internal sealed class KBFlowBatchReceipt
{
    // ProjectKB can also run on hosts that predate the bound-batch API.
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

    private readonly MeasureBatchModel _batch;
    private readonly Func<SqlSugarClient> _openDatabase;
    private readonly string? _legacyConnectionString;
    private readonly long _batchInsertStartedAt;

    internal KBFlowBatchReceipt(MeasureBatchModel batch, Func<SqlSugarClient> openDatabase, string? legacyConnectionString = null,
        long batchInsertStartedAt = 0)
    {
        if (batch.Id <= 0 || string.IsNullOrWhiteSpace(batch.Code))
            throw new InvalidOperationException("创建键盘检测流程批次未返回有效的批次 ID 和批次号。");

        _batch = batch;
        _openDatabase = openDatabase;
        _legacyConnectionString = legacyConnectionString;
        _batchInsertStartedAt = batchInsertStartedAt;
        BatchId = batch.Id;
        Code = batch.Code;
        Source = new(openDatabase, BatchId, Code);
    }

    internal int BatchId { get; }
    internal string Code { get; }
    internal KBFlowResultSource Source { get; }

    internal static KBFlowBatchReceipt Insert(MeasureBatchModel batch)
    {
        if (InsertOwnedBatch != null && StartOwnedBatch != null)
            return new(batch, InsertOwnedBatch(BatchResultMasterDao.Instance, batch));

        string connectionString = MySqlControl.GetConnectionString();
        SqlSugarClient OpenDatabase() => new(new ConnectionConfig
        {
            ConnectionString = connectionString,
            DbType = DbType.MySql,
            IsAutoCloseConnection = true,
        });

        using var db = OpenDatabase();
        long insertStartedAt = Stopwatch.GetTimestamp();
        batch.Id = db.Insertable(batch).ExecuteReturnIdentity();
        return new(batch, OpenDatabase, connectionString, insertStartedAt);
    }

    internal Task<bool> StartAsync(FlowControl control, CancellationToken cancellationToken)
    {
        if (_legacyConnectionString == null)
            return StartOwnedBatch!(control, Code, _batch, cancellationToken);

        if (!string.Equals(_legacyConnectionString, MySqlControl.GetConnectionString(), StringComparison.Ordinal))
            throw new InvalidOperationException("流程批次创建后数据库连接已更改，请重新启动键盘检测。");

        return StartLegacyBatch != null
            ? StartLegacyBatch(control, Code, BatchId, _legacyConnectionString, _batchInsertStartedAt, cancellationToken)
            : control.TryStartAsync(Code, cancellationToken);
    }

    internal void WriteOutcome(FlowStatus status, long elapsedMilliseconds, string? outcome)
    {
        using var db = _openDatabase();
        int duration = (int)Math.Clamp(elapsedMilliseconds, 0, int.MaxValue);
        int updated = db.Updateable<MeasureBatchModel>()
            .SetColumns(batch => new MeasureBatchModel { FlowStatus = status, TotalTime = duration, Result = outcome })
            .Where(batch => batch.Id == BatchId && batch.Code == Code)
            .ExecuteCommand();
        if (updated != 1)
            throw new InvalidOperationException($"找不到可回写的键盘检测流程批次：{BatchId}。");
    }
}
