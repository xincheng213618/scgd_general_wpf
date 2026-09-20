using ColorVision.Database;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Diagnostics;

/// <summary>Scopes every analysis query, including history and lazy payloads, to one source.</summary>
internal sealed class FlowAnalysisDataSource
{
    private readonly ReadOnlySqliteDatabase? _database;
    internal bool IsReadOnly => _database != null;
    internal string Label { get; }

    internal FlowAnalysisDataSource(string? path = null, string? label = null)
    {
        Label = label ?? string.Empty;
        if (path != null)
        {
            _database = new ReadOnlySqliteDatabase(path);
            _database.RequireColumns("FlowNodeRecord", "id", "batch_id", "serial_number", "node_id", "start_time", "elapsed_ms");
        }
    }

    private List<T> Read<T>(Func<ISugarQueryable<T>, ISugarQueryable<T>> filter, Func<List<T>> live) where T : class, new()
    {
        if (_database == null) return live();
        string table = typeof(T).Name;
        if (!_database.HasTable(table)) return [];
        using var db = _database.OpenClient();
        return filter(_database.Query<T>(db)).ToList();
    }

    internal bool FlushPendingWrites(TimeSpan timeout) => IsReadOnly || FlowNodeRecordDataBaseHelper.FlushPendingWrites(timeout);
    internal List<FlowNodeRecord> GetByRun(int batchId, string? serialNumber) => Read<FlowNodeRecord>(
        q => q.Where(x => x.BatchId == batchId).WhereIF(!string.IsNullOrWhiteSpace(serialNumber), x => x.SerialNumber == serialNumber).OrderBy(x => x.StartTime),
        () => FlowNodeRecordDataBaseHelper.GetByRun(batchId, serialNumber));
    internal List<FlowNodeRecord> GetByBatchIds(List<int> ids) => Read<FlowNodeRecord>(
        q => q.Where(x => ids.Contains(x.BatchId)).OrderBy(x => x.StartTime), () => FlowNodeRecordDataBaseHelper.GetByBatchIds(ids));
    internal List<FlowNodeRecord> GetBySerialNumbers(IEnumerable<string> serialNumbers)
    {
        string[] serials = serialNumbers.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
        if (serials.Length == 0) return [];
        return Read<FlowNodeRecord>(q => q.Where(x => serials.Contains(x.SerialNumber)).OrderBy(x => x.StartTime),
            () => FlowNodeRecordDataBaseHelper.GetBySerialNumbers(serials));
    }
    internal FlowNodeRecord? GetLatestRecord() => Read<FlowNodeRecord>(
        q => q.OrderByDescending(x => x.StartTime).Take(1), () => One(FlowNodeRecordDataBaseHelper.GetLatestRecord())).FirstOrDefault();
    internal FlowNodeRecord? GetLastByNodeId(string nodeId) => _database == null
        ? FlowNodeRecordDataBaseHelper.GetLastByNodeId(nodeId) : GetByNodeId(nodeId, 1).FirstOrDefault();
    internal List<FlowNodeRecord> GetByNodeId(string nodeId, int limit) => Read<FlowNodeRecord>(
        q => q.Where(x => x.NodeId == nodeId).OrderByDescending(x => x.StartTime).Take(limit), () => FlowNodeRecordDataBaseHelper.GetByNodeId(nodeId, limit));
    internal List<int> GetDistinctBatchIds(int limit)
    {
        if (_database == null) return FlowNodeRecordDataBaseHelper.GetDistinctBatchIds(limit);
        using var db = _database.OpenClient();
        return _database.Query<FlowNodeRecord>(db).GroupBy(x => x.BatchId).OrderByDescending(x => x.BatchId).Select(x => x.BatchId).Take(limit).ToList();
    }
    internal List<FlowNodeMessage> GetMessagesByRun(int batchId, string? serialNumber) => Read<FlowNodeMessage>(
        q => q.Where(x => x.BatchId == batchId).WhereIF(!string.IsNullOrWhiteSpace(serialNumber), x => x.SerialNumber == serialNumber).OrderBy(x => x.SendTime),
        () => FlowNodeRecordDataBaseHelper.GetMessagesByRun(batchId, serialNumber));
    internal List<FlowNodeMessage> GetHistoryMessagesByNodeId(string nodeId, IEnumerable<int> batchIds)
    {
        int[] ids = batchIds.Distinct().ToArray();
        return Read<FlowNodeMessage>(q => q.Where(x => x.NodeId == nodeId && ids.Contains(x.BatchId)).OrderByDescending(x => x.SendTime),
            () => FlowNodeRecordDataBaseHelper.GetHistoryMessagesByNodeId(nodeId, ids));
    }
    internal FlowNodeMessagePayloads GetMessagePayloads(int id) => _database == null
        ? FlowNodeRecordDataBaseHelper.GetMessagePayloads(id)
        : new(_database.ReadPayload("FlowNodeMessage", "id", id, "send_payload", "send_payload_gzip"),
            _database.ReadPayload("FlowNodeMessage", "id", id, "recv_payload", "recv_payload_gzip"));
    internal FlowRunRecord? GetFlowRun(int batchId, string? serialNumber) => Read<FlowRunRecord>(
        q => q.Where(x => x.BatchId == batchId).WhereIF(!string.IsNullOrWhiteSpace(serialNumber), x => x.SerialNumber == serialNumber).OrderByDescending(x => x.StartedTimeUtc).Take(1),
        () => One(FlowNodeRecordDataBaseHelper.GetFlowRun(batchId, serialNumber))).FirstOrDefault();
    internal List<FlowExecutionEvent> GetExecutionEvents(int runId) => Read<FlowExecutionEvent>(
        q => q.Where(x => x.RunRecordId == runId).OrderBy(x => x.SequenceNo), () => FlowNodeRecordDataBaseHelper.GetExecutionEvents(runId));
    internal List<FlowRunRecord> GetSameFlowRuns(string? serialNumber)
    {
        if (_database == null) return FlowNodeRecordDataBaseHelper.GetSameFlowRuns(serialNumber);
        FlowRunRecord? run = Read<FlowRunRecord>(q => q.Where(x => x.SerialNumber == serialNumber).OrderByDescending(x => x.CompletedTime).Take(1), () => []).FirstOrDefault();
        return run == null ? [] : GetFlowRuns(new FlowIdentity(run.TemplateId, run.FlowKey, run.FlowName));
    }
    internal List<FlowRunRecord> GetFlowRuns(FlowIdentity identity)
    {
        return Read<FlowRunRecord>(q =>
        {
            if (identity.FlowKey is string key)
                q = q.Where(x => x.FlowKey == key || ((x.FlowKey == null || x.FlowKey == "") && identity.TemplateId > 0 && x.TemplateId == identity.TemplateId));
            else if (identity.TemplateId > 0) q = q.Where(x => x.TemplateId == identity.TemplateId);
            else q = q.Where(x => x.TemplateId <= 0 && (x.FlowKey == null || x.FlowKey == "") && x.FlowName == identity.FlowName);
            return q.OrderByDescending(x => x.CompletedTime).Take(500);
        }, () => FlowNodeRecordDataBaseHelper.GetFlowRuns(identity));
    }
    private static List<T> One<T>(T? value) where T : class => value == null ? [] : [value];
}
