using ColorVision.Database;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.FlowDiagnostics;

public sealed record FlowRecordedTemplateReadResult(
    FlowRunRecord Run,
    FlowTemplateSnapshot? Snapshot,
    bool TextWasTruncated);

public sealed record FlowRecordedTemplatePairReadResult(
    FlowRecordedTemplateReadResult? Baseline,
    FlowRecordedTemplateReadResult? Current,
    string IdentityBasis)
{
    public string SourceKey { get; init; } = string.Empty;
}

/// <summary>Reads and verifies the exact recorded template bytes without constructing execution nodes or initializing a writer.</summary>
public sealed class FlowRecordedTemplateReader
{
    public const int MaximumSnapshotBytes = 8 * 1024 * 1024;
    private const int MaximumIdentifierCharacters = 256;
    private const int MaximumHashCharacters = 65;
    private static readonly string IdentityWhitespace = new(Enumerable.Range(char.MinValue, char.MaxValue + 1)
        .Select(value => (char)value).Where(char.IsWhiteSpace).ToArray());
    private readonly string _databasePath;
    private readonly string _sourceKey;

    public FlowRecordedTemplateReader(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = Path.GetFullPath(databasePath);
        var normalizedPath = Path.TrimEndingDirectorySeparator(_databasePath)
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).ToUpperInvariant();
        _sourceKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath))).ToLowerInvariant();
    }

    public Task<FlowRecordedTemplateReadResult?> ReadAsync(int runRecordId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(runRecordId);
        return Task.Run(() => Read(runRecordId, cancellationToken), cancellationToken);
    }

    public Task<FlowRecordedTemplatePairReadResult> ReadPairAsync(int baselineRunRecordId, int currentRunRecordId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baselineRunRecordId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(currentRunRecordId);
        return Task.Run(() => ReadPair(baselineRunRecordId, currentRunRecordId, cancellationToken), cancellationToken);
    }

    private FlowRecordedTemplateReadResult? Read(int runRecordId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = OpenDatabase();
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction(deferred: true);
        return ReadCore(database, connection, transaction, runRecordId, cancellationToken);
    }

    private FlowRecordedTemplatePairReadResult ReadPair(int baselineRunRecordId, int currentRunRecordId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = OpenDatabase();
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction(deferred: true);
        var baseline = ReadCore(database, connection, transaction, baselineRunRecordId, cancellationToken);
        var current = baselineRunRecordId == currentRunRecordId
            ? baseline : ReadCore(database, connection, transaction, currentRunRecordId, cancellationToken);
        var basis = "unavailable";
        if (baseline != null && current != null)
        {
            using var command = Command(connection, transaction, """
                SELECT CASE
                    WHEN b.flow_key IS NOT NULL AND trim(b.flow_key,$whitespace)<>''
                        AND c.flow_key IS NOT NULL AND trim(c.flow_key,$whitespace)<>''
                    THEN CASE WHEN trim(b.flow_key,$whitespace) COLLATE BINARY=trim(c.flow_key,$whitespace) COLLATE BINARY
                        THEN 'flow_key' ELSE 'different' END
                    WHEN (b.flow_key IS NULL OR trim(b.flow_key,$whitespace)='')
                        AND (c.flow_key IS NULL OR trim(c.flow_key,$whitespace)='')
                        AND b.template_id>0 AND b.template_id=c.template_id THEN 'legacy_template_id'
                    ELSE 'unavailable' END
                FROM FlowRunRecord b JOIN FlowRunRecord c ON c.id=$currentId WHERE b.id=$id LIMIT 1
                """, baselineRunRecordId);
            command.Parameters.AddWithValue("$currentId", currentRunRecordId);
            command.Parameters.AddWithValue("$whitespace", IdentityWhitespace);
            cancellationToken.ThrowIfCancellationRequested();
            basis = (string?)command.ExecuteScalar() ?? "unavailable";
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new(baseline, current, basis) { SourceKey = _sourceKey };
    }

    private ReadOnlySqliteDatabase OpenDatabase()
    {
        var database = new ReadOnlySqliteDatabase(_databasePath);
        database.RequireColumns("FlowRunRecord", "id", "template_id", "flow_key", "flow_name", "run_key",
            "template_revision", "content_hash", "snapshot_id");
        return database;
    }

    private static FlowRecordedTemplateReadResult? ReadCore(ReadOnlySqliteDatabase database, SqliteConnection connection,
        SqliteTransaction transaction, int runRecordId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var truncated = false;
        FlowRunRecord run;
        string? runHash;
        using (var command = Command(connection, transaction, $"""
            SELECT id,template_id,{Identifier("flow_key")},{Identifier("flow_name")},{Identifier("run_key")},
                template_revision,{FlowJournalHashSql.Value("content_hash")},snapshot_id
            FROM FlowRunRecord WHERE id=$id LIMIT 1
            """, runRecordId))
        using (var reader = command.ExecuteReader())
        {
            if (!reader.Read())
                return null;
            runHash = String(reader, 6);
            run = new FlowRunRecord
            {
                Id = reader.GetInt32(0), TemplateId = reader.GetInt32(1),
                FlowKey = Bound(String(reader, 2), MaximumIdentifierCharacters, ref truncated),
                FlowName = Bound(String(reader, 3), MaximumIdentifierCharacters, ref truncated),
                RunKey = Bound(String(reader, 4), MaximumIdentifierCharacters, ref truncated),
                TemplateRevision = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                ContentHash = Bound(runHash, MaximumHashCharacters, ref truncated),
                SnapshotId = reader.IsDBNull(7) ? null : reader.GetInt64(7),
                CompletedTime = default,
            };
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (run.SnapshotId is not long snapshotId)
            return new(run, null, truncated);
        if (snapshotId <= 0)
            throw new InvalidDataException("The recorded run has an invalid snapshot reference.");

        database.RequireColumns("FlowTemplateSnapshot", "id", "template_id", "flow_key", "template_revision",
            "content_hash", "content", "content_length", "captured_time_utc");
        FlowTemplateSnapshot snapshot;
        string? snapshotHash;
        long declaredLength;
        long actualLength;
        using (var command = Command(connection, transaction, $"""
            SELECT s.id,s.template_id,{Identifier("s.flow_key")},s.template_revision,
                {FlowJournalHashSql.Value("s.content_hash")},s.content_length,s.captured_time_utc,
                typeof(s.content),length(s.content),
                CASE WHEN r.flow_key IS NOT NULL AND trim(r.flow_key,$identityWhitespace)<>''
                    AND s.flow_key IS NOT NULL AND trim(s.flow_key,$identityWhitespace)<>''
                    THEN CASE WHEN trim(r.flow_key,$identityWhitespace) COLLATE BINARY
                        =trim(s.flow_key,$identityWhitespace) COLLATE BINARY THEN 1 ELSE 0 END
                    ELSE NULL END
            FROM FlowTemplateSnapshot s JOIN FlowRunRecord r ON r.id=$runId WHERE s.id=$id LIMIT 1
            """, snapshotId))
        {
            command.Parameters.AddWithValue("$runId", runRecordId);
            command.Parameters.AddWithValue("$identityWhitespace", IdentityWhitespace);
            using var reader = command.ExecuteReader();
            if (!reader.Read())
                return new(run, null, truncated);
            declaredLength = reader.GetInt64(5);
            if (reader.GetString(7) != "blob" || reader.IsDBNull(8))
                throw new InvalidDataException("The recorded template content is not a BLOB.");
            actualLength = reader.GetInt64(8);
            if (declaredLength < 0 || declaredLength != actualLength)
                throw new InvalidDataException("The recorded template length does not match its stored content.");
            if (actualLength > MaximumSnapshotBytes)
                throw new InvalidDataException("The recorded template exceeds the 8 MiB read limit.");
            var sameFlowKey = !reader.IsDBNull(9) && reader.GetInt32(9) == 1;
            if (!reader.IsDBNull(9) && !sameFlowKey)
                throw new InvalidDataException("The recorded run and template refer to different flow keys.");
            if (!sameFlowKey && run.TemplateId != reader.GetInt32(1))
                throw new InvalidDataException("The legacy recorded run and template refer to different template IDs.");
            snapshotHash = String(reader, 4);
            if (!IsHash(runHash) || !IsHash(snapshotHash)
                || !string.Equals(runHash, snapshotHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The recorded run and template hashes are invalid or do not match.");
            }
            snapshot = new FlowTemplateSnapshot
            {
                Id = reader.GetInt64(0), TemplateId = reader.GetInt32(1),
                FlowKey = Bound(String(reader, 2), MaximumIdentifierCharacters, ref truncated),
                TemplateRevision = reader.IsDBNull(3) ? null : reader.GetInt32(3), ContentHash = snapshotHash!,
                ContentLength = checked((int)declaredLength), CapturedTimeUtc = reader.GetDateTime(6),
            };
        }

        cancellationToken.ThrowIfCancellationRequested();
        using (var command = Command(connection, transaction, $"""
            SELECT CASE WHEN typeof(content)='blob' AND content_length=$length AND length(content)=$length
                AND length(content)<={MaximumSnapshotBytes} THEN content ELSE NULL END
            FROM FlowTemplateSnapshot WHERE id=$id LIMIT 1
            """, snapshotId))
        {
            command.Parameters.AddWithValue("$length", declaredLength);
            using var reader = command.ExecuteReader();
            if (!reader.Read() || reader.IsDBNull(0))
                throw new InvalidDataException("The recorded template could not be read within its verified length limit.");
            var content = new byte[(int)actualLength];
            var offset = 0;
            while (offset < content.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = checked((int)reader.GetBytes(0, offset, content, offset, Math.Min(81_920, content.Length - offset)));
                if (count <= 0)
                    throw new InvalidDataException("The recorded template content ended before its verified length.");
                offset += count;
            }
            cancellationToken.ThrowIfCancellationRequested();
            var actualHash = Convert.ToHexString(SHA256.HashData(content));
            if (!string.Equals(actualHash, snapshotHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The recorded template SHA256 does not match its stored hash.");
            snapshot.Content = content;
        }
        return new(run, snapshot, truncated);
    }

    private static SqliteCommand Command(SqliteConnection connection, SqliteTransaction transaction, string sql, long id)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = 2;
        command.CommandText = sql;
        command.Parameters.AddWithValue("$id", id);
        return command;
    }

    private static bool IsHash(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
    private static string Identifier(string column) => $"substr({column},1,{MaximumIdentifierCharacters + 1})";
    private static string? String(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static string? Bound(string? value, int maximum, ref bool truncated)
    {
        if (value == null || value.Length <= maximum)
            return value;
        truncated = true;
        if (char.IsHighSurrogate(value[maximum - 1]) && char.IsLowSurrogate(value[maximum]))
            maximum--;
        return value[..maximum];
    }
}
