using ColorVision.Database;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

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

    private List<T> Read<T>(Func<ISugarQueryable<T>, ISugarQueryable<T>> filter, Func<List<T>> live,
        SqlSugarClient? readClient = null) where T : class, new()
    {
        if (_database == null) return live();
        string table = typeof(T).Name;
        if (!_database.HasTable(table)) return [];
        using var ownedClient = readClient == null ? _database.OpenClient() : null;
        return filter(_database.Query<T>(readClient ?? ownedClient!)).ToList();
    }

    internal (List<FlowNodeRecord> Records, List<FlowNodeMessage> Messages, FlowRunRecord? Run, List<FlowExecutionEvent> Events)
        GetRunDetails(int batchId, string? serialNumber)
    {
        // Share the offline connection for this page load while retaining each query's existing scope.
        using var readClient = _database?.OpenClient();
        List<FlowNodeRecord> records = GetByRun(batchId, serialNumber, readClient);
        List<FlowNodeMessage> messages = GetMessagesByRun(batchId, serialNumber, readClient);
        FlowRunRecord? run = GetFlowRun(batchId, serialNumber, readClient);
        List<FlowExecutionEvent> events = run == null ? [] : GetExecutionEvents(run.Id, readClient);
        return (records, messages, run, events);
    }

    internal bool FlushPendingWrites(TimeSpan timeout) => IsReadOnly || FlowNodeRecordDataBaseHelper.FlushPendingWrites(timeout);
    internal List<FlowNodeRecord> GetByRun(int batchId, string? serialNumber, SqlSugarClient? readClient = null) => Read<FlowNodeRecord>(
        q => q.Where(x => x.BatchId == batchId).WhereIF(!string.IsNullOrWhiteSpace(serialNumber), x => x.SerialNumber == serialNumber).OrderBy(x => x.StartTime),
        () => FlowNodeRecordDataBaseHelper.GetByRun(batchId, serialNumber), readClient);
    internal FlowNodeRecord? GetLatestRecord() => Read<FlowNodeRecord>(
        q => q.OrderByDescending(x => x.StartTime).Take(1), () => One(FlowNodeRecordDataBaseHelper.GetLatestRecord())).FirstOrDefault();
    internal FlowNodeRecord? GetLastByNodeId(string nodeId) => _database == null
        ? FlowNodeRecordDataBaseHelper.GetLastByNodeId(nodeId) : GetByNodeId(nodeId, 1).FirstOrDefault();
    internal List<FlowNodeRecord> GetByNodeId(string nodeId, int limit) => Read<FlowNodeRecord>(
        q => q.Where(x => x.NodeId == nodeId).OrderByDescending(x => x.StartTime).Take(limit), () => FlowNodeRecordDataBaseHelper.GetByNodeId(nodeId, limit));
    internal List<FlowRunNavigationSummary> GetRecentRunSummaries(int batchLimit, CancellationToken cancellationToken)
        => ReadRunNavigation(db => FlowRunNavigationReader.ReadRecent(db, batchLimit, cancellationToken));

    internal List<FlowRunNavigationSummary> GetRunSummariesForFlowRuns(IReadOnlyList<FlowRunRecord> runs, CancellationToken cancellationToken)
    {
        FlowRunRecord[] namedRuns = runs.Where(run => !string.IsNullOrWhiteSpace(run.SerialNumber)).ToArray();
        if (namedRuns.Length == 0)
            return [];

        string[] serials = namedRuns.Select(run => run.SerialNumber!).Distinct(StringComparer.Ordinal).ToArray();
        // Runs with explicit batches cannot match a node run elsewhere. Legacy runs
        // still need every matching serial to find the latest node run.
        int[]? batchIds = namedRuns.Any(run => !run.BatchId.HasValue)
            ? null : namedRuns.Select(run => run.BatchId!.Value).Distinct().ToArray();
        return ReadRunNavigation(db => FlowRunNavigationReader.ReadBySerials(db, serials, batchIds, cancellationToken));
    }

    private List<FlowRunNavigationSummary> ReadRunNavigation(Func<SqlSugarClient, List<FlowRunNavigationSummary>> read)
    {
        if (_database == null)
            return FlowNodeRecordDataBaseHelper.ReadRunNavigation(read);
        using var db = _database.OpenClient();
        return read(db);
    }

    internal List<FlowNodeMessage> GetMessagesByRun(int batchId, string? serialNumber, SqlSugarClient? readClient = null) => Read<FlowNodeMessage>(
        q => q.Where(x => x.BatchId == batchId).WhereIF(!string.IsNullOrWhiteSpace(serialNumber), x => x.SerialNumber == serialNumber).OrderBy(x => x.SendTime),
        () => FlowNodeRecordDataBaseHelper.GetMessagesByRun(batchId, serialNumber), readClient);
    internal List<FlowNodeMessage> GetHistoryMessagesByNodeId(string nodeId, IEnumerable<int> batchIds)
    {
        int[] ids = batchIds.Distinct().ToArray();
        return Read<FlowNodeMessage>(q => q.Where(x => x.NodeId == nodeId && ids.Contains(x.BatchId)).OrderByDescending(x => x.SendTime),
            () => FlowNodeRecordDataBaseHelper.GetHistoryMessagesByNodeId(nodeId, ids));
    }
    internal FlowNodeMessagePayloads GetMessagePayloads(int id)
    {
        if (_database == null)
            return FlowNodeRecordDataBaseHelper.GetMessagePayloads(id);

        var (send, recv) = _database.ReadPayloadPair(FlowNodeMessagePayloadStorage.TableName, FlowNodeMessagePayloadStorage.IdColumnName, id,
            (FlowNodeMessagePayloadStorage.SendLegacyColumnName, FlowNodeMessagePayloadStorage.SendGzipColumnName),
            (FlowNodeMessagePayloadStorage.RecvLegacyColumnName, FlowNodeMessagePayloadStorage.RecvGzipColumnName));
        return new(send, recv);
    }
    internal FlowNodeComparisonHistory GetNodeComparisonHistory(FlowExecutionAnalysisSession session, FlowNodeRecord record)
    {
        FlowRunRecord? run = session.Run;
        if (run == null && !string.IsNullOrWhiteSpace(record.SerialNumber))
            run = GetUniqueLegacyFlowRun(record.SerialNumber);
        bool isCurrentRun = run != null && (!run.BatchId.HasValue || run.BatchId == record.BatchId)
            && string.Equals(run.SerialNumber ?? string.Empty, record.SerialNumber ?? string.Empty, StringComparison.Ordinal);
        var identity = !isCurrentRun ? default : new FlowIdentity(run!.TemplateId, run.FlowKey, run.FlowName);
        if (identity.IsEmpty || string.IsNullOrWhiteSpace(record.NodeId))
            return new([record], session.GetMessages(record), false);

        // Keep Batch + SN together: batch numbers and SNs can each be reused.
        List<FlowRunRecord> runs = GetFlowRuns(identity);
        runs.Add(run!); // Include the opened run even when it is outside the recent-history limit.
        int[] batchIds = runs.Where(item => item.BatchId.HasValue).Select(item => item.BatchId!.Value).Distinct().ToArray();
        string[] legacySerials = runs.Where(item => !item.BatchId.HasValue && !string.IsNullOrWhiteSpace(item.SerialNumber))
            .Select(item => item.SerialNumber!).Distinct(StringComparer.Ordinal).ToArray();
        List<FlowNodeRecord> records = Read<FlowNodeRecord>(
            q => FlowNodeRecordDataBaseHelper.SelectNodeRecordsForRuns(q, record.NodeId, batchIds, legacySerials),
            () => FlowNodeRecordDataBaseHelper.GetNodeRecordsForRuns(record.NodeId, batchIds, legacySerials));
        records.AddRange(session.Records.Where(item => item.NodeId == record.NodeId));
        FlowNodeRecord[] selectedRecords = FlowNodeComparisonHistory.SelectRunRecords(record.NodeId, runs, records);
        List<FlowNodeMessage> messages = GetHistoryMessagesByNodeId(record.NodeId, selectedRecords.Select(item => item.BatchId));
        messages.AddRange(session.Messages.Where(item => item.NodeId == record.NodeId));
        return FlowNodeComparisonHistory.FromSelectedRecords(record.NodeId, selectedRecords, messages);
    }
    internal FlowRunRecord? GetFlowRun(int batchId, string? serialNumber, SqlSugarClient? readClient = null) => Read<FlowRunRecord>(
        q => FlowNodeRecordDataBaseHelper.SelectFlowRun(q, batchId, serialNumber),
        () => One(FlowNodeRecordDataBaseHelper.GetFlowRun(batchId, serialNumber)), readClient).FirstOrDefault();
    internal List<FlowExecutionEvent> GetExecutionEvents(int runId, SqlSugarClient? readClient = null) => Read<FlowExecutionEvent>(
        q => q.Where(x => x.RunRecordId == runId).OrderBy(x => x.SequenceNo),
        () => FlowNodeRecordDataBaseHelper.GetExecutionEvents(runId), readClient);
    internal List<FlowRunRecord> GetSameFlowRuns(FlowExecutionAnalysisSession session)
    {
        FlowRunRecord? run = session.Run ?? GetUniqueLegacyFlowRun(session.SerialNumber);
        if (run == null)
            return [];
        var identity = new FlowIdentity(run.TemplateId, run.FlowKey, run.FlowName);
        return identity.IsEmpty ? [] : GetFlowRuns(identity);
    }

    private FlowRunRecord? GetUniqueLegacyFlowRun(string? serialNumber)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
            return null;

        List<FlowRunRecord> candidates = Read<FlowRunRecord>(
            q => FlowNodeRecordDataBaseHelper.SelectLegacyFlowRuns(q, serialNumber),
            () => FlowNodeRecordDataBaseHelper.GetLegacyFlowRuns(serialNumber));
        return candidates.Count == 1 ? candidates[0] : null;
    }
    internal List<FlowRunRecord> GetFlowRuns(FlowIdentity identity) => Read<FlowRunRecord>(
        q => FlowNodeRecordDataBaseHelper.ApplyFlowIdentityFilter(q, identity)
            .OrderByDescending(x => x.CompletedTime).Take(500),
        () => FlowNodeRecordDataBaseHelper.GetFlowRuns(identity));

    private static List<T> One<T>(T? value) where T : class => value == null ? [] : [value];
}
