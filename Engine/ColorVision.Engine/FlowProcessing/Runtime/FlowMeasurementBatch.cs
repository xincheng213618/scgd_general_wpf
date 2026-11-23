using ColorVision.Common.Utilities;
using ColorVision.Database;
using FlowEngineLib.Base;
using SqlSugar;
using System;
using System.Diagnostics;

namespace ColorVision.Engine.FlowProcessing;

internal sealed class FlowMeasurementBatch(string serialNumber, int batchId, string connectionString, long batchInsertStartedAt,
    MySqlConnectedEndpoint? databaseEndpoint = null)
{
    private const string ResourceKey = "ColorVision.Engine.FlowMeasurementBatch";

    internal static FlowMeasurementBatch CapturePersisted(string serialNumber, int batchId, string connectionString, long batchInsertStartedAt)
    {
        var endpoint = MySqlControl.GetInstance().ConnectedEndpoint;
        if (endpoint?.IsCurrent != true || !MatchesTarget(endpoint, connectionString)) endpoint = null;
        return new FlowMeasurementBatch(serialNumber, batchId, connectionString, batchInsertStartedAt, endpoint);
    }

    internal void Bind(FlowRuntimeResources resources) => resources.Set(ResourceKey, this);

    internal static Func<SqlSugarClient> InsertForFlow(MeasureBatchModel batch, MySqlConnectedEndpoint? endpoint)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentException.ThrowIfNullOrWhiteSpace(batch.Code);
        if (batch.Id > 0 || !string.Equals(batch.Code, Tool.SanitizeFileName(batch.Code), StringComparison.Ordinal))
            throw new ArgumentException("流程批次必须尚未保存，并使用规范化的流程编号。", nameof(batch));
        if (endpoint?.IsCurrent != true)
            throw new InvalidOperationException("创建流程批次需要已验证的 MySQL 连接。");

        batch.FlowStartReceipt = Insert(batch, endpoint);
        return endpoint.CreateDbClient;
    }

    internal static FlowMeasurementBatch Insert(MeasureBatchModel batch, MySqlConnectedEndpoint endpoint)
    {
        long startedAt = Stopwatch.GetTimestamp();
        using var db = endpoint.CreateDbClient();
        batch.Id = db.Insertable(batch).ExecuteReturnIdentity();
        if (batch.Id <= 0) throw new InvalidOperationException("创建流程批次失败，数据库未返回有效批次 ID。");
        batch.DatabaseEndpoint = endpoint;
        return new(batch.Code!, batch.Id, db.CurrentConnectionConfig.ConnectionString, startedAt, endpoint);
    }

    internal static void UpdateOutcome(MeasureBatchModel batch)
    {
        if (batch.Id <= 0 || batch.DatabaseEndpoint is not { } endpoint) return;

        // Once a batch exists, its terminal state belongs to the same database even after a reconnect.
        using var db = endpoint.CreateDbClient();
        db.Updateable(batch).UpdateColumns(item => new { item.FlowStatus, item.TotalTime, item.Result }).ExecuteCommand();
    }

    internal static bool TryGetBoundEndpoint(CVStartCFC action, out MySqlConnectedEndpoint? endpoint)
        => TryGetBoundBatchEndpoint(action, out endpoint) && endpoint != null;

    // A receipt can exist without a verified endpoint. It cannot be associated with a later connection.
    internal static bool TryGetBoundBatchEndpoint(CVStartCFC action, out MySqlConnectedEndpoint? endpoint)
    {
        bool bound = action.RuntimeResources.TryGet<FlowMeasurementBatch>(ResourceKey, out var batch);
        endpoint = bound ? batch.Endpoint : null;
        return bound;
    }

    internal bool MatchesPersistedBatch(MeasureBatchModel batch, string currentSerialNumber) =>
        batch.Id == Id && ReferenceEquals(batch.DatabaseEndpoint, Endpoint)
        && string.Equals(batch.Code, currentSerialNumber, StringComparison.Ordinal)
        && MatchesIdentity(currentSerialNumber);

    public static int? ResolveId(CVStartCFC action, Func<string, int?>? lookup = null)
    {
        if (action.RuntimeResources.TryGet<FlowMeasurementBatch>(ResourceKey, out var batch))
        {
            if (!string.Equals(batch.ConnectionString, MySqlControl.GetConnectionString(), StringComparison.Ordinal)) return null;
            bool matches = batch.MatchesIdentity(action.SerialNumber);
            if (batch.Endpoint is { } endpoint)
                return endpoint.IsCurrent && matches ? batch.Id : null;
            if (matches) return batch.Id;
        }

        lookup ??= static serialNumber => BatchResultMasterDao.Instance.GetByNameOrCode(serialNumber)?.Id;
        return lookup(action.SerialNumber);
    }

    internal static int? ResolveId(CVStartCFC action, MySqlConnectedEndpoint endpoint, Func<string, int?> lookup)
    {
        if (action.RuntimeResources.TryGet<FlowMeasurementBatch>(ResourceKey, out var batch))
        {
            bool matches = batch.MatchesIdentity(action.SerialNumber);
            if (batch.Endpoint != null)
                return matches && ReferenceEquals(batch.Endpoint, endpoint) ? batch.Id : null;
            if (!MatchesTarget(endpoint, batch.ConnectionString)) return null;
            if (matches) return batch.Id;
        }

        return lookup(action.SerialNumber);
    }

    private int Id { get; } = batchId;
    private MySqlConnectedEndpoint? Endpoint { get; } = databaseEndpoint;

    private bool MatchesIdentity(string currentSerialNumber) => Id > 0
        && MySqlLocalServicesManager.CanReuseBatchFrom(batchInsertStartedAt)
        && string.Equals(serialNumber, currentSerialNumber, StringComparison.Ordinal);

    private string ConnectionString { get; } = connectionString;

    private static bool MatchesTarget(MySqlConnectedEndpoint endpoint, string expectedConnectionString)
    {
        using var db = endpoint.CreateDbClient();
        return string.Equals(expectedConnectionString, db.CurrentConnectionConfig.ConnectionString, StringComparison.Ordinal);
    }
}
