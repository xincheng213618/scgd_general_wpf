using ColorVision.Database;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.FlowProcessing.PostProcess;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.FlowDiagnostics;

public sealed record FlowRunHistoryEntry(FlowRunRecord Run, IReadOnlyList<FlowNodeAttempt> Attempts, bool AttemptsTruncated);

public sealed record FlowRunHistoryReadResult(
    FlowRunHistoryEntry Anchor,
    IReadOnlyList<FlowRunHistoryEntry> PreviousRuns,
    string IdentityBasis,
    bool HistoryTruncated,
    bool NodeAttemptsAvailable,
    bool TextWasTruncated);

/// <summary>Reads bounded earlier record IDs under a strict recorded identity, without legacy SN joins or writer initialization.</summary>
public sealed class FlowRunHistoryReader
{
    public const int MaximumRuns = 50;
    public const int MaximumAttemptsPerRun = 10;
    private const int MaximumIdentifierCharacters = 256;
    private const int MaximumNodeIdCharacters = 128;
    private static readonly string IdentityWhitespace = new(Enumerable.Range(char.MinValue, char.MaxValue + 1)
        .Select(value => (char)value).Where(char.IsWhiteSpace).ToArray());
    private static readonly string[] AttemptColumns =
        ["id", "run_record_id", "node_id", "attempt_no", "started_time_utc", "completed_time_utc", "outcome", "error_code"];
    private readonly string _databasePath;

    public FlowRunHistoryReader(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = Path.GetFullPath(databasePath);
    }

    public Task<FlowRunHistoryReadResult?> ReadAsync(int runRecordId, string? nodeId, int maxRuns, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(runRecordId);
        if (maxRuns is < 1 or > MaximumRuns)
            throw new ArgumentOutOfRangeException(nameof(maxRuns), maxRuns, $"The history limit must be between 1 and {MaximumRuns}.");
        if (nodeId != null && (nodeId.Length > MaximumNodeIdCharacters || string.IsNullOrWhiteSpace(nodeId)))
            throw new ArgumentException("The exact node ID must contain 1 to 128 characters and cannot be whitespace only.", nameof(nodeId));
        return Task.Run(() => Read(runRecordId, nodeId, maxRuns, cancellationToken), cancellationToken);
    }

    private FlowRunHistoryReadResult? Read(int runRecordId, string? nodeId, int maxRuns, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = new ReadOnlySqliteDatabase(_databasePath);
        database.RequireColumns("FlowRunRecord", "id", "template_id", "flow_key", "flow_name", "run_key", "status",
            "final_outcome", "elapsed_ms", "started_time_utc", "completed_time_utc", "recovered_time_utc",
            "template_revision", "content_hash", "snapshot_id");
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction(deferred: true);
        ProjectedRun anchor;
        using (var command = Command(connection, transaction, $"SELECT {RunColumns("r")} FROM FlowRunRecord r WHERE r.id=$id LIMIT 1", runRecordId, usesIdentityWhitespace: true))
        using (var reader = command.ExecuteReader())
        {
            if (!reader.Read())
                return null;
            anchor = ReadRun(reader);
        }
        var basis = anchor.HasFlowKey ? "flow_key" : anchor.Run.TemplateId > 0 ? "legacy_template_id" : "unavailable";
        var previous = new List<ProjectedRun>();
        if (basis != "unavailable")
        {
            var identityFilter = basis == "flow_key"
                ? "h.flow_key IS NOT NULL AND trim(h.flow_key,$whitespace)<>'' AND trim(h.flow_key,$whitespace) COLLATE BINARY=trim(a.flow_key,$whitespace) COLLATE BINARY"
                : "h.template_id=a.template_id AND (h.flow_key IS NULL OR trim(h.flow_key,$whitespace)='')";
            using var command = Command(connection, transaction, $"""
                SELECT {RunColumns("h")} FROM FlowRunRecord h JOIN FlowRunRecord a ON a.id=$id
                WHERE h.id<$id AND {identityFilter} ORDER BY h.id DESC LIMIT {maxRuns + 1}
                """, runRecordId, usesIdentityWhitespace: true);
            cancellationToken.ThrowIfCancellationRequested();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                previous.Add(ReadRun(reader));
            }
        }
        var historyTruncated = previous.Count > maxRuns;
        if (historyTruncated)
            previous.RemoveAt(maxRuns);
        var attemptsAvailable = nodeId != null && database.HasTable("FlowNodeAttempt")
            && AttemptColumns.All(column => database.HasColumn("FlowNodeAttempt", column));
        var textTruncated = anchor.TextWasTruncated || previous.Any(item => item.TextWasTruncated);
        FlowRunHistoryEntry Entry(ProjectedRun item)
        {
            if (!attemptsAvailable)
                return new(item.Run, Array.Empty<FlowNodeAttempt>(), false);
            var attempts = ReadAttempts(connection, transaction, item.Run.Id, nodeId!, cancellationToken);
            var truncated = attempts.Count > MaximumAttemptsPerRun;
            if (truncated)
                attempts.RemoveAt(MaximumAttemptsPerRun);
            textTruncated |= attempts.Any(attempt => attempt.TextWasTruncated);
            return new(item.Run, Array.AsReadOnly(attempts.OrderBy(attempt => attempt.Attempt.Id)
                .Select(attempt => attempt.Attempt).ToArray()), truncated);
        }
        var anchorEntry = Entry(anchor);
        var entries = previous.Select(Entry).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        return new(anchorEntry, Array.AsReadOnly(entries), basis, historyTruncated, attemptsAvailable, textTruncated);
    }

    private static string RunColumns(string alias) => $"""
        {alias}.id,{alias}.template_id,{Identifier(alias + ".flow_key")},{Identifier(alias + ".flow_name")},
        {Identifier(alias + ".run_key")},{alias}.status,{alias}.final_outcome,{alias}.elapsed_ms,{alias}.started_time_utc,
        {alias}.completed_time_utc,{alias}.recovered_time_utc,{alias}.template_revision,
        {FlowJournalHashSql.Value(alias + ".content_hash")},{alias}.snapshot_id,
        CASE WHEN {alias}.flow_key IS NOT NULL AND trim({alias}.flow_key,$whitespace)<>'' THEN 1 ELSE 0 END,
        {FlowJournalHashSql.Invalid(alias + ".content_hash")}
        """;

    private sealed record ProjectedRun(FlowRunRecord Run, bool HasFlowKey, bool TextWasTruncated);

    private static ProjectedRun ReadRun(SqliteDataReader reader)
    {
        var truncated = reader.GetInt32(15) == 1;
        var run = new FlowRunRecord
        {
            Id = reader.GetInt32(0), TemplateId = reader.GetInt32(1),
            FlowKey = Text(reader, 2, MaximumIdentifierCharacters, ref truncated),
            FlowName = Text(reader, 3, MaximumIdentifierCharacters, ref truncated),
            RunKey = Text(reader, 4, MaximumIdentifierCharacters, ref truncated), Status = (FlowStatus)reader.GetInt32(5),
            FinalOutcome = reader.IsDBNull(6) ? null : (FlowFinalOutcome)reader.GetInt32(6), ElapsedMs = reader.GetInt64(7),
            StartedTimeUtc = Date(reader, 8), CompletedTimeUtc = Date(reader, 9), RecoveredTimeUtc = Date(reader, 10),
            TemplateRevision = reader.IsDBNull(11) ? null : reader.GetInt32(11), ContentHash = HashText(reader, 12, ref truncated),
            SnapshotId = reader.IsDBNull(13) ? null : reader.GetInt64(13), CompletedTime = default,
        };
        return new(run, reader.GetInt32(14) == 1, truncated);
    }

    private sealed record ProjectedAttempt(FlowNodeAttempt Attempt, bool TextWasTruncated);

    private static List<ProjectedAttempt> ReadAttempts(SqliteConnection connection, SqliteTransaction transaction,
        int runId, string nodeId, CancellationToken cancellationToken)
    {
        using var command = Command(connection, transaction, $"""
            SELECT id,run_record_id,substr(node_id,1,{MaximumNodeIdCharacters + 1}),attempt_no,started_time_utc,completed_time_utc,
                {Identifier("outcome")},{Identifier("error_code")} FROM FlowNodeAttempt
            WHERE run_record_id=$id AND node_id COLLATE BINARY=$nodeId COLLATE BINARY ORDER BY id DESC LIMIT {MaximumAttemptsPerRun + 1}
            """, runId);
        command.Parameters.AddWithValue("$nodeId", nodeId);
        cancellationToken.ThrowIfCancellationRequested();
        using var reader = command.ExecuteReader();
        var attempts = new List<ProjectedAttempt>();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var truncated = false;
            var attempt = new FlowNodeAttempt
            {
                Id = reader.GetInt64(0), RunRecordId = reader.GetInt32(1),
                NodeId = Text(reader, 2, MaximumNodeIdCharacters, ref truncated) ?? string.Empty,
                AttemptNo = reader.GetInt32(3), StartedTimeUtc = reader.GetDateTime(4), CompletedTimeUtc = Date(reader, 5),
                Outcome = Text(reader, 6, MaximumIdentifierCharacters, ref truncated),
                ErrorCode = Text(reader, 7, MaximumIdentifierCharacters, ref truncated),
            };
            attempts.Add(new(attempt, truncated));
        }
        return attempts;
    }

    private static SqliteCommand Command(SqliteConnection connection, SqliteTransaction transaction, string sql, int id, bool usesIdentityWhitespace = false)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = 2;
        command.CommandText = sql;
        command.Parameters.AddWithValue("$id", id);
        if (usesIdentityWhitespace)
            command.Parameters.AddWithValue("$whitespace", IdentityWhitespace);
        return command;
    }

    private static string Identifier(string column) => $"substr({column},1,{MaximumIdentifierCharacters + 1})";
    private static DateTime? Date(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    private static string? HashText(SqliteDataReader reader, int ordinal, ref bool truncated)
    {
        if (reader.IsDBNull(ordinal))
            return null;
        var text = reader.GetString(ordinal);
        if (text.Length == 64 && text.All(char.IsAsciiHexDigit))
            return text;
        truncated = true;
        return null;
    }
    private static string? Text(SqliteDataReader reader, int ordinal, int maximum, ref bool truncated)
    {
        if (reader.IsDBNull(ordinal))
            return null;
        var text = reader.GetString(ordinal);
        if (text.Length <= maximum)
            return text;
        truncated = true;
        if (char.IsHighSurrogate(text[maximum - 1]) && char.IsLowSurrogate(text[maximum]))
            maximum--;
        return text[..maximum];
    }
}
