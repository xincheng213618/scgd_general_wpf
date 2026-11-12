using ColorVision.Database;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.FlowProcessing.PostProcess;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.FlowDiagnostics;

public sealed record FlowIncidentInspectionResult(
    FlowIncident Incident,
    FlowRunRecord? Run,
    IReadOnlyList<FlowNodeAttempt> Attempts,
    IReadOnlyList<FlowExecutionEvent> Events,
    bool AttemptsTruncated,
    bool EventsTruncated,
    bool LinkedAttemptMissing,
    bool TextWasTruncated)
{
    public bool RunContentHashInvalid { get; init; }
}

public sealed record FlowIncidentQueryPage(
    IReadOnlyList<FlowIncident> Incidents,
    bool HasMore,
    bool TextWasTruncated,
    string SourceKey)
{
    public DateTime? AnchorDetectedTimeUtc { get; init; }
    public IReadOnlyDictionary<long, string> DetectedTimeFingerprints { get; init; } =
        new ReadOnlyDictionary<long, string>(new Dictionary<long, string>());
    public string? AnchorDetectedTimeFingerprint { get; init; }
}

/// <summary>Inspects one retained incident without initializing diagnostics writers or reading private payloads.</summary>
public sealed class FlowIncidentInspectionReader
{
    public const int MaximumAttempts = 50;
    public const int MaximumEvents = 100;
    public const int MaximumResults = 100;
    public const int MaximumIdentifierCharacters = 256;
    public const int MaximumDiagnosticCharacters = 2048;

    private readonly string _databasePath;
    private readonly string _sourceKey;

    public FlowIncidentInspectionReader(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = Path.GetFullPath(databasePath);
        var normalizedPath = Path.TrimEndingDirectorySeparator(_databasePath)
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).ToUpperInvariant();
        _sourceKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath))).ToLowerInvariant();
    }

    public Task<FlowIncidentInspectionResult?> ReadAsync(long incidentId, int maxEvents, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(incidentId);
        if (maxEvents is < 1 or > MaximumEvents)
            throw new ArgumentOutOfRangeException(nameof(maxEvents), maxEvents, $"The event limit must be between 1 and {MaximumEvents}.");
        return Task.Run(() => Read(incidentId, maxEvents, cancellationToken), cancellationToken);
    }

    public Task<FlowIncidentQueryPage> QueryAsync(string state, int maxResults, long? beforeIncidentId, CancellationToken cancellationToken)
    {
        var normalizedState = (state ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedState is not ("active" or "open" or "acknowledged" or "resolved" or "all"))
            throw new ArgumentException("The incident state must be active, open, acknowledged, resolved, or all.", nameof(state));
        if (maxResults is < 1 or > MaximumResults)
            throw new ArgumentOutOfRangeException(nameof(maxResults), maxResults, $"The result limit must be between 1 and {MaximumResults}.");
        if (beforeIncidentId is <= 0)
            throw new ArgumentOutOfRangeException(nameof(beforeIncidentId), beforeIncidentId, "The continuation incident ID must be positive.");
        return Task.Run(() => QueryPage(normalizedState, maxResults, beforeIncidentId, cancellationToken), cancellationToken);
    }

    private FlowIncidentQueryPage QueryPage(string state, int maxResults, long? beforeIncidentId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = new ReadOnlySqliteDatabase(_databasePath);
        RequireIncidentColumns(database);
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction(deferred: true);
        object? anchorDate = null;
        DateTime? anchorDetectedTimeUtc = null;
        string? anchorDetectedTimeFingerprint = null;
        if (beforeIncidentId.HasValue)
        {
            using var anchor = connection.CreateCommand();
            anchor.Transaction = transaction;
            anchor.CommandTimeout = 2;
            anchor.CommandText = "SELECT detected_time_utc FROM FlowIncident WHERE id=$id LIMIT 1";
            anchor.Parameters.AddWithValue("$id", beforeIncidentId.Value);
            using var anchorReader = anchor.ExecuteReader();
            if (!anchorReader.Read())
                throw new ArgumentException("The continuation incident no longer exists. Restart the incident query without a cursor.", nameof(beforeIncidentId));
            if (anchorReader.IsDBNull(0))
                throw new InvalidDataException("The continuation incident has no detected timestamp. The database was not modified.");
            anchorDate = anchorReader.GetValue(0);
            anchorDetectedTimeUtc = anchorReader.GetDateTime(0);
            anchorDetectedTimeFingerprint = Fingerprint(anchorDate);
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = 2;
        var stateFilter = state switch
        {
            "all" => "1=1",
            "active" => "(state IS NULL OR state<>$state)",
            _ => "state=$state",
        };
        if (state != "all")
            command.Parameters.AddWithValue("$state", state switch
            {
                "active" or "resolved" => "Resolved",
                "acknowledged" => "Acknowledged",
                _ => "Open",
            });
        var anchorFilter = beforeIncidentId.HasValue
            ? " AND (detected_time_utc<$anchorDate OR (detected_time_utc=$anchorDate AND id<$anchorId))"
            : string.Empty;
        if (beforeIncidentId.HasValue)
        {
            // Rebind the stored SQLite value unchanged; DateTime formatting can change lexical tie boundaries.
            command.Parameters.AddWithValue("$anchorDate", anchorDate!);
            command.Parameters.AddWithValue("$anchorId", beforeIncidentId.Value);
        }
        command.CommandText = $"""
            SELECT {IncidentColumns} FROM FlowIncident WHERE {stateFilter}{anchorFilter}
            ORDER BY detected_time_utc DESC,id DESC LIMIT {maxResults + 1}
            """;
        cancellationToken.ThrowIfCancellationRequested();
        using var reader = command.ExecuteReader();
        var rows = new List<Projected<FlowIncident>>();
        var fingerprints = new Dictionary<long, string>();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var incident = ReadIncident(reader);
            rows.Add(incident);
            fingerprints[incident.Value.Id] = Fingerprint(reader.GetValue(8));
        }
        var hasMore = rows.Count > maxResults;
        if (hasMore)
        {
            fingerprints.Remove(rows[maxResults].Value.Id);
            rows.RemoveAt(maxResults);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new FlowIncidentQueryPage(Array.AsReadOnly(rows.Select(item => item.Value).ToArray()), hasMore,
            rows.Any(item => item.TextWasTruncated), _sourceKey)
        {
            AnchorDetectedTimeUtc = anchorDetectedTimeUtc,
            DetectedTimeFingerprints = new ReadOnlyDictionary<long, string>(fingerprints),
            AnchorDetectedTimeFingerprint = anchorDetectedTimeFingerprint,
        };
    }

    private FlowIncidentInspectionResult? Read(long incidentId, int maxEvents, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = new ReadOnlySqliteDatabase(_databasePath);
        RequireIncidentColumns(database);
        database.RequireColumns("FlowRunRecord", "id", "template_id", "flow_key", "flow_name", "run_key", "status",
            "final_outcome", "elapsed_ms", "started_time_utc", "completed_time", "completed_time_utc", "last_heartbeat_utc",
            "recovered_time_utc", "recovery_reason", "template_revision", "content_hash", "snapshot_id");
        database.RequireColumns("FlowNodeAttempt", "id", "run_record_id", "node_id", "attempt_no", "invocation_id",
            "started_time_utc", "completed_time_utc", "outcome", "error_code", "error_message");
        database.RequireColumns("FlowExecutionEvent", "id", "run_record_id", "sequence_no", "event_type",
            "occurred_time_utc", "node_id", "attempt_id", "code", "message");

        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        // Keep every exact association on the same read snapshot, even while a live writer advances the journal.
        using var transaction = connection.BeginTransaction(deferred: true);
        var incident = Query(connection, transaction, $"""
            SELECT {IncidentColumns} FROM FlowIncident WHERE id=$id LIMIT 1
            """, incidentId, ReadIncident, cancellationToken).SingleOrDefault();
        if (incident == null)
            return null;

        var runContentHashInvalid = false;
        var run = Query(connection, transaction, $"""
            SELECT id,template_id,{Identifier("flow_key")},{Identifier("flow_name")},{Identifier("run_key")},status,
                final_outcome,elapsed_ms,started_time_utc,completed_time,completed_time_utc,last_heartbeat_utc,
                recovered_time_utc,{Diagnostic("recovery_reason")},template_revision,
                {FlowJournalHashSql.Value("content_hash")},snapshot_id,{FlowJournalHashSql.Invalid("content_hash")}
            FROM FlowRunRecord WHERE id=$id LIMIT 1
            """, incident.Value.RunRecordId, reader =>
            {
                runContentHashInvalid = reader.GetInt32(17) == 1;
                return ReadRun(reader);
            }, cancellationToken).SingleOrDefault();
        var attempts = Query(connection, transaction, $"""
            SELECT {AttemptColumns} FROM FlowNodeAttempt WHERE run_record_id=$id
            ORDER BY started_time_utc DESC,id DESC LIMIT {MaximumAttempts + 1}
            """, incident.Value.RunRecordId, ReadAttempt, cancellationToken);
        var attemptsTruncated = attempts.Count > MaximumAttempts;
        if (attemptsTruncated)
            attempts.RemoveAt(MaximumAttempts);

        var linkedAttemptMissing = false;
        if (incident.Value.AttemptId is long attemptId && !attempts.Any(item => item.Value.Id == attemptId))
        {
            var linked = Query(connection, transaction, $"""
                SELECT {AttemptColumns} FROM FlowNodeAttempt WHERE run_record_id=$id AND id=$attemptId LIMIT 1
                """, incident.Value.RunRecordId, ReadAttempt, cancellationToken, attemptId).SingleOrDefault();
            linkedAttemptMissing = linked == null;
            if (linked != null)
            {
                if (attempts.Count == MaximumAttempts)
                    attempts.RemoveAt(attempts.Count - 1);
                attempts.Add(linked);
            }
        }

        var events = Query(connection, transaction, $"""
            SELECT id,run_record_id,sequence_no,{Identifier("event_type")},occurred_time_utc,{Identifier("node_id")},
                attempt_id,{Identifier("code")},{Diagnostic("message")}
            FROM FlowExecutionEvent WHERE run_record_id=$id ORDER BY sequence_no DESC,id DESC LIMIT {maxEvents + 1}
            """, incident.Value.RunRecordId, ReadEvent, cancellationToken);
        var eventsTruncated = events.Count > maxEvents;
        if (eventsTruncated)
            events.RemoveAt(maxEvents);
        cancellationToken.ThrowIfCancellationRequested();
        return new FlowIncidentInspectionResult(
            incident.Value,
            run?.Value,
            Array.AsReadOnly(attempts.OrderBy(item => item.Value.StartedTimeUtc).ThenBy(item => item.Value.Id).Select(item => item.Value).ToArray()),
            Array.AsReadOnly(events.OrderBy(item => item.Value.SequenceNo).ThenBy(item => item.Value.Id).Select(item => item.Value).ToArray()),
            attemptsTruncated,
            eventsTruncated,
            linkedAttemptMissing,
            incident.TextWasTruncated || run?.TextWasTruncated == true
                || attempts.Any(item => item.TextWasTruncated) || events.Any(item => item.TextWasTruncated))
        {
            RunContentHashInvalid = runContentHashInvalid,
        };
    }

    private static string Identifier(string column) => $"substr({column},1,{MaximumIdentifierCharacters + 1})";
    private static string Diagnostic(string column) => $"substr({column},1,{MaximumDiagnosticCharacters + 1})";
    private static string Fingerprint(object value)
    {
        var representation = value switch
        {
            string text => text,
            byte[] bytes => Convert.ToHexString(bytes),
            DateTime date => date.ToString("O", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture),
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            value.GetType().FullName + ":" + representation))).ToLowerInvariant();
    }
    private static void RequireIncidentColumns(ReadOnlySqliteDatabase database) =>
        database.RequireColumns("FlowIncident", "id", "run_record_id", "attempt_id", "node_id", "kind", "severity",
            "state", "summary", "detected_time_utc", "acknowledged_time_utc", "resolved_time_utc");
    private static string IncidentColumns => $"""
        id,run_record_id,attempt_id,{Identifier("node_id")},{Identifier("kind")},{Identifier("severity")},
        {Identifier("state")},{Diagnostic("summary")},detected_time_utc,acknowledged_time_utc,resolved_time_utc
        """;
    private static string AttemptColumns => $"""
        id,run_record_id,{Identifier("node_id")},attempt_no,{Identifier("invocation_id")},started_time_utc,
        completed_time_utc,{Identifier("outcome")},{Identifier("error_code")},{Diagnostic("error_message")}
        """;

    private sealed record Projected<T>(T Value, bool TextWasTruncated);

    private static List<Projected<T>> Query<T>(SqliteConnection connection, SqliteTransaction transaction,
        string sql, long id, Func<SqliteDataReader, Projected<T>> project, CancellationToken cancellationToken, long? attemptId = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = 2;
        command.CommandText = sql;
        command.Parameters.AddWithValue("$id", id);
        if (attemptId.HasValue)
            command.Parameters.AddWithValue("$attemptId", attemptId.Value);
        using var reader = command.ExecuteReader();
        var rows = new List<Projected<T>>();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rows.Add(project(reader));
        }
        return rows;
    }

    private static Projected<FlowIncident> ReadIncident(SqliteDataReader reader)
    {
        var truncated = false;
        var value = new FlowIncident
        {
            Id = reader.GetInt64(0), RunRecordId = reader.GetInt32(1), AttemptId = NullableInt64(reader, 2),
            NodeId = Text(reader, 3, MaximumIdentifierCharacters, ref truncated),
            Kind = Text(reader, 4, MaximumIdentifierCharacters, ref truncated) ?? string.Empty,
            Severity = Text(reader, 5, MaximumIdentifierCharacters, ref truncated) ?? string.Empty,
            State = Text(reader, 6, MaximumIdentifierCharacters, ref truncated) ?? string.Empty,
            Summary = Text(reader, 7, MaximumDiagnosticCharacters, ref truncated) ?? string.Empty,
            DetectedTimeUtc = reader.GetDateTime(8), AcknowledgedTimeUtc = NullableDateTime(reader, 9),
            ResolvedTimeUtc = NullableDateTime(reader, 10),
        };
        return new(value, truncated);
    }

    private static Projected<FlowRunRecord> ReadRun(SqliteDataReader reader)
    {
        var truncated = false;
        var value = new FlowRunRecord
        {
            Id = reader.GetInt32(0), TemplateId = reader.GetInt32(1),
            FlowKey = Text(reader, 2, MaximumIdentifierCharacters, ref truncated),
            FlowName = Text(reader, 3, MaximumIdentifierCharacters, ref truncated),
            RunKey = Text(reader, 4, MaximumIdentifierCharacters, ref truncated), Status = (FlowStatus)reader.GetInt32(5),
            FinalOutcome = reader.IsDBNull(6) ? null : (FlowFinalOutcome)reader.GetInt32(6), ElapsedMs = reader.GetInt64(7),
            StartedTimeUtc = NullableDateTime(reader, 8), CompletedTime = reader.GetDateTime(9),
            CompletedTimeUtc = NullableDateTime(reader, 10), LastHeartbeatUtc = NullableDateTime(reader, 11),
            RecoveredTimeUtc = NullableDateTime(reader, 12),
            RecoveryReason = Text(reader, 13, MaximumDiagnosticCharacters, ref truncated),
            TemplateRevision = reader.IsDBNull(14) ? null : reader.GetInt32(14),
            ContentHash = Text(reader, 15, MaximumIdentifierCharacters, ref truncated), SnapshotId = NullableInt64(reader, 16),
        };
        return new(value, truncated);
    }

    private static Projected<FlowNodeAttempt> ReadAttempt(SqliteDataReader reader)
    {
        var truncated = false;
        var value = new FlowNodeAttempt
        {
            Id = reader.GetInt64(0), RunRecordId = reader.GetInt32(1),
            NodeId = Text(reader, 2, MaximumIdentifierCharacters, ref truncated) ?? string.Empty, AttemptNo = reader.GetInt32(3),
            InvocationId = Text(reader, 4, MaximumIdentifierCharacters, ref truncated) ?? string.Empty,
            StartedTimeUtc = reader.GetDateTime(5), CompletedTimeUtc = NullableDateTime(reader, 6),
            Outcome = Text(reader, 7, MaximumIdentifierCharacters, ref truncated),
            ErrorCode = Text(reader, 8, MaximumIdentifierCharacters, ref truncated),
            ErrorMessage = Text(reader, 9, MaximumDiagnosticCharacters, ref truncated),
        };
        return new(value, truncated);
    }

    private static Projected<FlowExecutionEvent> ReadEvent(SqliteDataReader reader)
    {
        var truncated = false;
        var value = new FlowExecutionEvent
        {
            Id = reader.GetInt64(0), RunRecordId = reader.GetInt32(1), SequenceNo = reader.GetInt64(2),
            EventType = Text(reader, 3, MaximumIdentifierCharacters, ref truncated) ?? string.Empty,
            OccurredTimeUtc = reader.GetDateTime(4), NodeId = Text(reader, 5, MaximumIdentifierCharacters, ref truncated),
            AttemptId = NullableInt64(reader, 6), Code = Text(reader, 7, MaximumIdentifierCharacters, ref truncated),
            Message = Text(reader, 8, MaximumDiagnosticCharacters, ref truncated),
        };
        return new(value, truncated);
    }

    private static long? NullableInt64(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
    private static DateTime? NullableDateTime(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);

    private static string? Text(SqliteDataReader reader, int ordinal, int maximumCharacters, ref bool truncated)
    {
        if (reader.IsDBNull(ordinal))
            return null;
        var text = reader.GetString(ordinal);
        if (text.Length <= maximumCharacters)
            return text;
        truncated = true;
        var length = maximumCharacters;
        if (char.IsHighSurrogate(text[length - 1]) && char.IsLowSurrogate(text[length]))
            length--;
        return text[..length];
    }
}
