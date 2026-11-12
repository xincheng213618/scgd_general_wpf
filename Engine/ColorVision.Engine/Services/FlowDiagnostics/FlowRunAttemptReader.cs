using ColorVision.Database;
using ColorVision.Engine.FlowProcessing.Diagnostics;
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

public sealed record FlowRunAttemptPage(
    int RunRecordId,
    IReadOnlyList<FlowNodeAttempt> Attempts,
    bool HasMore,
    bool TextWasTruncated,
    string SourceKey)
{
    public IReadOnlyList<long> OutcomeTruncatedAttemptIds { get; init; } = Array.Empty<long>();
}

/// <summary>Reads retained attempts for one exact run without initializing writers or joining private legacy evidence.</summary>
public sealed class FlowRunAttemptReader
{
    public const int MaximumAttempts = 100;
    public const int MaximumNodeIdCharacters = 128;
    public const int MaximumIdentifierCharacters = 256;
    public const int MaximumDiagnosticCharacters = 2048;
    private readonly string _databasePath;
    private readonly string _sourceKey;

    public FlowRunAttemptReader(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = Path.GetFullPath(databasePath);
        var normalizedPath = Path.TrimEndingDirectorySeparator(_databasePath)
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).ToUpperInvariant();
        _sourceKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath))).ToLowerInvariant();
    }

    public Task<FlowRunAttemptPage?> ReadPageAsync(int runRecordId, string? nodeId, int maxAttempts, long? beforeAttemptId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(runRecordId);
        if (nodeId != null && (nodeId.Length > MaximumNodeIdCharacters || string.IsNullOrWhiteSpace(nodeId)))
            throw new ArgumentException("The exact node ID must contain 1 to 128 characters and cannot be whitespace only.", nameof(nodeId));
        if (maxAttempts is < 1 or > MaximumAttempts)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "The attempt limit must be between 1 and 100.");
        if (beforeAttemptId is <= 0)
            throw new ArgumentOutOfRangeException(nameof(beforeAttemptId), beforeAttemptId, "The continuation attempt ID must be positive.");
        return Task.Run(() => ReadPage(runRecordId, nodeId, maxAttempts, beforeAttemptId, cancellationToken), cancellationToken);
    }

    private FlowRunAttemptPage? ReadPage(int runRecordId, string? nodeId, int maxAttempts, long? beforeAttemptId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = new ReadOnlySqliteDatabase(_databasePath);
        database.RequireColumns("FlowRunRecord", "id");
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction(deferred: true);
        using (var command = Command(connection, transaction, "SELECT id FROM FlowRunRecord WHERE id=$run LIMIT 1", runRecordId, nodeId, beforeAttemptId))
        {
            if (command.ExecuteScalar() == null) return null;
        }
        cancellationToken.ThrowIfCancellationRequested();
        database.RequireColumns("FlowNodeAttempt", "id", "run_record_id", "node_id", "attempt_no", "started_time_utc",
            "completed_time_utc", "outcome", "error_code", "error_message");
        var nodeFilter = nodeId == null ? string.Empty : " AND node_id COLLATE BINARY=$node COLLATE BINARY";
        if (beforeAttemptId.HasValue)
        {
            using var command = Command(connection, transaction,
                $"SELECT id,typeof(id) FROM FlowNodeAttempt WHERE run_record_id=$run AND id=$before{nodeFilter} LIMIT 1",
                runRecordId, nodeId, beforeAttemptId);
            using var reader = command.ExecuteReader();
            if (!reader.Read())
                throw new ArgumentException("The continuation attempt no longer belongs to this run and node filter. Restart without a cursor.", nameof(beforeAttemptId));
            AttemptId(reader, 0, 1);
        }
        using var query = Command(connection, transaction, $"""
            SELECT id,run_record_id,{Projection("node_id", MaximumNodeIdCharacters)},attempt_no,started_time_utc,completed_time_utc,
                {Projection("outcome", MaximumIdentifierCharacters)},{Projection("error_code", MaximumIdentifierCharacters)},
                {Projection("error_message", MaximumDiagnosticCharacters)},typeof(id),
                CASE WHEN {WasTruncated("node_id", MaximumNodeIdCharacters)} OR {WasTruncated("outcome", MaximumIdentifierCharacters)}
                    OR {WasTruncated("error_code", MaximumIdentifierCharacters)} OR {WasTruncated("error_message", MaximumDiagnosticCharacters)} THEN 1 ELSE 0 END,
                CASE WHEN {WasTruncated("outcome", MaximumIdentifierCharacters)} THEN 1 ELSE 0 END
            FROM FlowNodeAttempt WHERE run_record_id=$run{nodeFilter}{(beforeAttemptId.HasValue ? " AND (id<$before OR typeof(id)<>'integer')" : string.Empty)}
            ORDER BY id DESC LIMIT {maxAttempts + 1}
            """, runRecordId, nodeId, beforeAttemptId);
        cancellationToken.ThrowIfCancellationRequested();
        var rows = new List<(FlowNodeAttempt Attempt, bool Truncated, bool OutcomeTruncated)>();
        using (var reader = query.ExecuteReader())
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                rows.Add(ReadAttempt(reader));
            }
        var hasMore = rows.Count > maxAttempts;
        if (hasMore) rows.RemoveAt(maxAttempts);
        rows.Reverse();
        cancellationToken.ThrowIfCancellationRequested();
        return new(runRecordId, Array.AsReadOnly(rows.Select(row => row.Attempt).ToArray()), hasMore,
            rows.Any(row => row.Truncated), _sourceKey)
        {
            OutcomeTruncatedAttemptIds = Array.AsReadOnly(rows.Where(row => row.OutcomeTruncated).Select(row => row.Attempt.Id).ToArray()),
        };
    }

    private static (FlowNodeAttempt Attempt, bool Truncated, bool OutcomeTruncated) ReadAttempt(SqliteDataReader reader)
    {
        try
        {
            var outcomeTruncated = reader.GetInt32(11) != 0;
            var outcome = Text(reader, 6, MaximumIdentifierCharacters, ref outcomeTruncated);
            var truncated = reader.GetInt32(10) != 0 || outcomeTruncated;
            var attempt = new FlowNodeAttempt
            {
                Id = AttemptId(reader, 0, 9), RunRecordId = reader.GetInt32(1),
                NodeId = Text(reader, 2, MaximumNodeIdCharacters, ref truncated) ?? string.Empty, AttemptNo = reader.GetInt32(3),
                StartedTimeUtc = reader.GetDateTime(4), CompletedTimeUtc = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                Outcome = outcome, ErrorCode = Text(reader, 7, MaximumIdentifierCharacters, ref truncated),
                ErrorMessage = Text(reader, 8, MaximumDiagnosticCharacters, ref truncated),
            };
            return (attempt, truncated, outcomeTruncated);
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            throw new InvalidDataException("The retained attempt contains invalid diagnostic metadata.", exception);
        }
    }

    private static long AttemptId(SqliteDataReader reader, int valueOrdinal, int typeOrdinal)
    {
        if (reader.GetString(typeOrdinal) != "integer")
            throw new InvalidDataException("A retained attempt ID must be stored as a positive SQLite integer.");
        long id = reader.GetInt64(valueOrdinal);
        if (id <= 0) throw new InvalidDataException("A retained attempt ID must be stored as a positive SQLite integer.");
        return id;
    }

    private static SqliteCommand Command(SqliteConnection connection, SqliteTransaction transaction, string sql,
        int runRecordId, string? nodeId, long? beforeAttemptId)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = 2;
        command.CommandText = sql;
        command.Parameters.AddWithValue("$run", runRecordId);
        if (nodeId != null) command.Parameters.AddWithValue("$node", nodeId);
        if (beforeAttemptId.HasValue) command.Parameters.AddWithValue("$before", beforeAttemptId.Value);
        return command;
    }

    private static string Projection(string column, int maximum) => $"substr({column},1,{maximum + 1})";
    private static string WasTruncated(string column, int maximum) =>
        $"length(CAST({column} AS BLOB))<>length(CAST({Projection(column, maximum)} AS BLOB))";
    private static string? Text(SqliteDataReader reader, int ordinal, int maximum, ref bool truncated)
    {
        if (reader.IsDBNull(ordinal)) return null;
        var value = reader.GetString(ordinal);
        if (value.Length <= maximum) return value;
        truncated = true;
        if (char.IsHighSurrogate(value[maximum - 1]) && char.IsLowSurrogate(value[maximum])) maximum--;
        return value[..maximum];
    }
}
