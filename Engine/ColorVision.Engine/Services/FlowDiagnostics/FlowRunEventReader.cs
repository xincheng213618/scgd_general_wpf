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

public sealed record FlowRunEventPage(
    int RunRecordId,
    IReadOnlyList<FlowExecutionEvent> Events,
    bool HasMore,
    bool TextWasTruncated,
    string SourceKey)
{
    public long? AnchorSequenceNo { get; init; }
}

/// <summary>Reads earlier windows of one exact run's journal events without initializing writers or loading payloads.</summary>
public sealed class FlowRunEventReader
{
    public const int MaximumEvents = 100;
    public const int MaximumIdentifierCharacters = 256;
    public const int MaximumDiagnosticCharacters = 2048;
    private readonly string _databasePath;
    private readonly string _sourceKey;

    public FlowRunEventReader(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = Path.GetFullPath(databasePath);
        var normalizedPath = Path.TrimEndingDirectorySeparator(_databasePath)
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).ToUpperInvariant();
        _sourceKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath))).ToLowerInvariant();
    }

    public Task<FlowRunEventPage?> ReadPageAsync(int runRecordId, int maxEvents, long? beforeEventId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(runRecordId);
        if (maxEvents is < 1 or > MaximumEvents)
            throw new ArgumentOutOfRangeException(nameof(maxEvents), maxEvents, "The event limit must be between 1 and 100.");
        if (beforeEventId is <= 0)
            throw new ArgumentOutOfRangeException(nameof(beforeEventId), beforeEventId, "The continuation event ID must be positive.");
        return Task.Run(() => ReadPage(runRecordId, maxEvents, beforeEventId, cancellationToken), cancellationToken);
    }

    private FlowRunEventPage? ReadPage(int runRecordId, int maxEvents, long? beforeEventId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = new ReadOnlySqliteDatabase(_databasePath);
        database.RequireColumns("FlowRunRecord", "id");
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction(deferred: true);
        using (var command = Command(connection, transaction, "SELECT id FROM FlowRunRecord WHERE id=$run LIMIT 1", runRecordId))
        {
            if (command.ExecuteScalar() == null)
                return null;
        }
        cancellationToken.ThrowIfCancellationRequested();
        database.RequireColumns("FlowExecutionEvent", "id", "run_record_id", "sequence_no", "event_type",
            "occurred_time_utc", "node_id", "attempt_id", "code", "message");
        long? anchorSequenceNo = null;
        if (beforeEventId.HasValue)
        {
            using var command = Command(connection, transaction,
                "SELECT sequence_no,typeof(sequence_no) FROM FlowExecutionEvent WHERE run_record_id=$run AND id=$event LIMIT 1", runRecordId);
            command.Parameters.AddWithValue("$event", beforeEventId.Value);
            using var reader = command.ExecuteReader();
            if (!reader.Read())
                throw new ArgumentException("The continuation event no longer belongs to this run. Restart without a cursor.", nameof(beforeEventId));
            anchorSequenceNo = Sequence(reader, 0, 1);
        }
        var boundary = beforeEventId.HasValue
            ? " AND (sequence_no<$sequence OR (sequence_no=$sequence AND id<$event) OR typeof(sequence_no)<>'integer')" : string.Empty;
        using var events = Command(connection, transaction, $"""
            SELECT id,run_record_id,sequence_no,{Identifier("event_type")},occurred_time_utc,{Identifier("node_id")},
                attempt_id,{Identifier("code")},substr(message,1,{MaximumDiagnosticCharacters + 1}),typeof(sequence_no),
                CASE WHEN {WasTruncated("event_type", MaximumIdentifierCharacters)} OR {WasTruncated("node_id", MaximumIdentifierCharacters)}
                    OR {WasTruncated("code", MaximumIdentifierCharacters)} OR {WasTruncated("message", MaximumDiagnosticCharacters)} THEN 1 ELSE 0 END
            FROM FlowExecutionEvent WHERE run_record_id=$run{boundary}
            ORDER BY sequence_no DESC,id DESC LIMIT {maxEvents + 1}
            """, runRecordId);
        if (beforeEventId.HasValue)
        {
            events.Parameters.AddWithValue("$sequence", anchorSequenceNo!.Value);
            events.Parameters.AddWithValue("$event", beforeEventId.Value);
        }
        cancellationToken.ThrowIfCancellationRequested();
        var rows = new List<(FlowExecutionEvent Event, bool Truncated)>();
        using (var reader = events.ExecuteReader())
        {
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                rows.Add(ReadEvent(reader));
            }
        }
        var hasMore = rows.Count > maxEvents;
        if (hasMore) rows.RemoveAt(maxEvents);
        rows.Reverse();
        cancellationToken.ThrowIfCancellationRequested();
        return new(runRecordId, Array.AsReadOnly(rows.Select(row => row.Event).ToArray()), hasMore,
            rows.Any(row => row.Truncated), _sourceKey) { AnchorSequenceNo = anchorSequenceNo };
    }

    private static (FlowExecutionEvent Event, bool Truncated) ReadEvent(SqliteDataReader reader)
    {
        var truncated = reader.GetInt32(10) != 0;
        try
        {
            var value = new FlowExecutionEvent
            {
                Id = reader.GetInt64(0), RunRecordId = reader.GetInt32(1), SequenceNo = Sequence(reader, 2, 9),
                EventType = Text(reader, 3, MaximumIdentifierCharacters, ref truncated) ?? string.Empty,
                OccurredTimeUtc = reader.GetDateTime(4), NodeId = Text(reader, 5, MaximumIdentifierCharacters, ref truncated),
                AttemptId = reader.IsDBNull(6) ? null : reader.GetInt64(6),
                Code = Text(reader, 7, MaximumIdentifierCharacters, ref truncated),
                Message = Text(reader, 8, MaximumDiagnosticCharacters, ref truncated),
            };
            return (value, truncated);
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            throw new InvalidDataException("The recorded event contains invalid evidence fields.", exception);
        }
    }

    private static long Sequence(SqliteDataReader reader, int valueOrdinal, int typeOrdinal)
    {
        if (reader.GetString(typeOrdinal) != "integer")
            throw new InvalidDataException("The recorded event sequence must be stored as an SQLite integer.");
        return reader.GetInt64(valueOrdinal);
    }

    private static SqliteCommand Command(SqliteConnection connection, SqliteTransaction transaction, string sql, int runRecordId)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = 2;
        command.CommandText = sql;
        command.Parameters.AddWithValue("$run", runRecordId);
        return command;
    }

    private static string Identifier(string column) => $"substr({column},1,{MaximumIdentifierCharacters + 1})";
    private static string WasTruncated(string column, int maximum) =>
        $"length(CAST({column} AS BLOB))<>length(CAST(substr({column},1,{maximum + 1}) AS BLOB))";

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
