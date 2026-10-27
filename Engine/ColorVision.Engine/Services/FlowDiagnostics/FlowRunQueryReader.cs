using ColorVision.Database;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.FlowProcessing.PostProcess;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.FlowDiagnostics;

public sealed record FlowRunQueryPage(IReadOnlyList<FlowRunRecord> Runs, bool HasMore, bool TextWasTruncated, string SourceKey);

/// <summary>Discovers retained run IDs without initializing writers, reading payloads, or inferring execution order from dates.</summary>
public sealed class FlowRunQueryReader
{
    public const int MaximumRuns = 100;
    public const int MaximumIdentifierCharacters = 256;
    private static readonly string IdentityWhitespace = new(Enumerable.Range(char.MinValue, char.MaxValue + 1)
        .Select(value => (char)value).Where(char.IsWhiteSpace).ToArray());
    private readonly string _databasePath;
    private readonly string _sourceKey;

    public FlowRunQueryReader(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = Path.GetFullPath(databasePath);
        var normalizedPath = Path.TrimEndingDirectorySeparator(_databasePath)
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).ToUpperInvariant();
        _sourceKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath))).ToLowerInvariant();
    }

    public Task<FlowRunQueryPage> ReadPageAsync(string? flowKey, int maxResults, int? beforeRunRecordId, CancellationToken cancellationToken)
    {
        if (flowKey != null && (flowKey.Length > MaximumIdentifierCharacters || string.IsNullOrWhiteSpace(flowKey)))
            throw new ArgumentException("The exact normalized FlowKey must contain 1 to 256 characters.", nameof(flowKey));
        flowKey = flowKey?.Trim();
        if (maxResults is < 1 or > MaximumRuns)
            throw new ArgumentOutOfRangeException(nameof(maxResults), maxResults, "The run limit must be between 1 and 100.");
        if (beforeRunRecordId is <= 0)
            throw new ArgumentOutOfRangeException(nameof(beforeRunRecordId), beforeRunRecordId, "The continuation run ID must be positive.");
        return Task.Run(() => ReadPage(flowKey, maxResults, beforeRunRecordId, cancellationToken), cancellationToken);
    }

    private FlowRunQueryPage ReadPage(string? flowKey, int maxResults, int? beforeRunRecordId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = new ReadOnlySqliteDatabase(_databasePath);
        database.RequireColumns("FlowRunRecord", "id", "template_id", "flow_key", "flow_name", "status",
            "final_outcome", "elapsed_ms", "started_time_utc", "completed_time_utc", "recovered_time_utc",
            "template_revision", "content_hash", "snapshot_id");
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction(deferred: true);
        var identityFilter = flowKey == null ? string.Empty : " AND trim(flow_key,$whitespace) COLLATE BINARY=$flowKey COLLATE BINARY";
        if (beforeRunRecordId.HasValue)
        {
            using var command = Command(connection, transaction,
                $"SELECT id FROM FlowRunRecord WHERE id=$before{identityFilter} LIMIT 1", flowKey, beforeRunRecordId);
            if (command.ExecuteScalar() == null)
                throw new ArgumentException("The continuation run no longer belongs to this query. Restart without a cursor.", nameof(beforeRunRecordId));
        }
        using var query = Command(connection, transaction, $"""
            SELECT id,template_id,{Identifier("trim(flow_key,$whitespace)")},{Identifier("flow_name")},status,
                final_outcome,elapsed_ms,started_time_utc,completed_time_utc,recovered_time_utc,template_revision,
                {FlowJournalHashSql.Value("content_hash")},snapshot_id,{FlowJournalHashSql.Invalid("content_hash")},typeof(id),
                CASE WHEN {WasTruncated("trim(flow_key,$whitespace)")} OR {WasTruncated("flow_name")} THEN 1 ELSE 0 END
            FROM FlowRunRecord WHERE 1=1{identityFilter}{(beforeRunRecordId.HasValue ? " AND id<$before" : string.Empty)}
            ORDER BY id DESC LIMIT {maxResults + 1}
            """, flowKey, beforeRunRecordId);
        cancellationToken.ThrowIfCancellationRequested();
        var rows = new List<(FlowRunRecord Run, bool Truncated)>();
        using (var reader = query.ExecuteReader())
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                rows.Add(ReadRun(reader));
            }
        var hasMore = rows.Count > maxResults;
        if (hasMore) rows.RemoveAt(maxResults);
        cancellationToken.ThrowIfCancellationRequested();
        return new(Array.AsReadOnly(rows.Select(row => row.Run).ToArray()), hasMore, rows.Any(row => row.Truncated), _sourceKey);
    }

    private static (FlowRunRecord Run, bool Truncated) ReadRun(SqliteDataReader reader)
    {
        try
        {
            if (reader.GetString(14) != "integer" || reader.GetInt64(0) is <= 0 or > int.MaxValue)
                throw new InvalidDataException("A retained run ID must be a positive SQLite integer within the supported run ID range.");
            var truncated = reader.GetInt32(13) != 0 || reader.GetInt32(15) != 0;
            var hash = reader.IsDBNull(11) ? null : reader.GetString(11);
            if (hash != null && (hash.Length != 64 || !hash.All(char.IsAsciiHexDigit)))
            {
                hash = null;
                truncated = true;
            }
            var run = new FlowRunRecord
            {
                Id = reader.GetInt32(0), TemplateId = reader.GetInt32(1),
                FlowKey = Text(reader, 2, ref truncated), FlowName = Text(reader, 3, ref truncated),
                Status = (FlowStatus)reader.GetInt32(4), FinalOutcome = reader.IsDBNull(5) ? null : (FlowFinalOutcome)reader.GetInt32(5),
                ElapsedMs = reader.GetInt64(6), StartedTimeUtc = Date(reader, 7), CompletedTimeUtc = Date(reader, 8),
                RecoveredTimeUtc = Date(reader, 9), TemplateRevision = reader.IsDBNull(10) ? null : reader.GetInt32(10),
                ContentHash = hash, SnapshotId = reader.IsDBNull(12) ? null : reader.GetInt64(12), CompletedTime = default,
            };
            return (run, truncated);
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            throw new InvalidDataException("The retained run contains invalid diagnostic metadata.", exception);
        }
    }

    private static SqliteCommand Command(SqliteConnection connection, SqliteTransaction transaction, string sql, string? flowKey, int? beforeRunRecordId)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = 2;
        command.CommandText = sql;
        command.Parameters.AddWithValue("$whitespace", IdentityWhitespace);
        if (flowKey != null) command.Parameters.AddWithValue("$flowKey", flowKey);
        if (beforeRunRecordId.HasValue) command.Parameters.AddWithValue("$before", beforeRunRecordId.Value);
        return command;
    }

    private static DateTime? Date(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    private static string Identifier(string column) => $"substr({column},1,{MaximumIdentifierCharacters + 1})";
    private static string WasTruncated(string column) => $"length(CAST({column} AS BLOB))<>length(CAST({Identifier(column)} AS BLOB))";
    private static string? Text(SqliteDataReader reader, int ordinal, ref bool truncated)
    {
        if (reader.IsDBNull(ordinal)) return null;
        var value = reader.GetString(ordinal);
        if (value.Length <= MaximumIdentifierCharacters) return value;
        truncated = true;
        var maximum = MaximumIdentifierCharacters;
        if (char.IsHighSurrogate(value[maximum - 1]) && char.IsLowSurrogate(value[maximum])) maximum--;
        return value[..maximum];
    }
}
