using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace ColorVision.Engine.Templates.Flow.Versioning
{
    /// <summary>
    /// SQLite sidecar store. It owns a dedicated connection so an in-memory
    /// database remains alive for its full lifetime and all operations are
    /// serialized on that connection.
    /// </summary>
    public sealed class SqliteFlowRevisionStore :
        IFlowRevisionStore,
        IDisposable
    {
        private const string SelectColumns = """
            flow_key, revision, parent_revision, base_binary_hash,
            source, is_published, semantic_hash, layout_hash, binary_hash,
            full_snapshot, semantic_document, author, message,
            external_version, rollback_of_revision, created_time_utc
            """;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = false,
        };

        private readonly object sync = new();
        private readonly SqliteConnection connection;
        private bool disposed;

        public SqliteFlowRevisionStore(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException(
                    "SQLite 连接字符串不能为空。",
                    nameof(connectionString));
            }

            connection = new SqliteConnection(connectionString);
            connection.Open();
            ConfigureConnection();
            EnsureSchema();
        }

        private void ConfigureConnection()
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                PRAGMA busy_timeout = 5000;
                PRAGMA journal_mode = WAL;
                """;
            command.ExecuteNonQuery();
        }

        public FlowRevision? GetHead(string flowKey)
        {
            string key = FlowRevisionStoreRules.NormalizeFlowKey(flowKey);
            lock (sync)
            {
                ThrowIfDisposed();
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = $"""
                    SELECT {SelectColumns}
                    FROM flow_definition_revision
                    WHERE flow_key = $flow_key
                    ORDER BY revision DESC
                    LIMIT 1;
                    """;
                command.Parameters.AddWithValue("$flow_key", key);
                return ReadSingle(command);
            }
        }

        public FlowRevision? GetRevision(string flowKey, int revision)
        {
            string key = FlowRevisionStoreRules.NormalizeFlowKey(flowKey);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revision);
            lock (sync)
            {
                ThrowIfDisposed();
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = $"""
                    SELECT {SelectColumns}
                    FROM flow_definition_revision
                    WHERE flow_key = $flow_key AND revision = $revision
                    LIMIT 1;
                    """;
                command.Parameters.AddWithValue("$flow_key", key);
                command.Parameters.AddWithValue("$revision", revision);
                return ReadSingle(command);
            }
        }

        public FlowRevision? FindByBinaryHash(
            string flowKey,
            string binaryHash)
        {
            string key = FlowRevisionStoreRules.NormalizeFlowKey(flowKey);
            string hash = FlowRevisionStoreRules.NormalizeHash(
                binaryHash,
                nameof(binaryHash));
            lock (sync)
            {
                ThrowIfDisposed();
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = $"""
                    SELECT {SelectColumns}
                    FROM flow_definition_revision
                    WHERE flow_key = $flow_key AND binary_hash = $binary_hash
                    ORDER BY revision DESC
                    LIMIT 1;
                    """;
                command.Parameters.AddWithValue("$flow_key", key);
                command.Parameters.AddWithValue("$binary_hash", hash);
                return ReadSingle(command);
            }
        }

        public FlowRevision? FindByContentHashes(
            string flowKey,
            string binaryHash,
            string semanticHash,
            string layoutHash)
        {
            string key = FlowRevisionStoreRules.NormalizeFlowKey(flowKey);
            string normalizedBinaryHash =
                FlowRevisionStoreRules.NormalizeHash(
                    binaryHash,
                    nameof(binaryHash));
            string normalizedSemanticHash =
                FlowRevisionStoreRules.NormalizeHash(
                    semanticHash,
                    nameof(semanticHash));
            string normalizedLayoutHash =
                FlowRevisionStoreRules.NormalizeHash(
                    layoutHash,
                    nameof(layoutHash));
            lock (sync)
            {
                ThrowIfDisposed();
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = $"""
                    SELECT {SelectColumns}
                    FROM flow_definition_revision
                    WHERE flow_key = $flow_key
                        AND binary_hash = $binary_hash
                        AND semantic_hash = $semantic_hash
                        AND layout_hash = $layout_hash
                    ORDER BY revision DESC
                    LIMIT 1;
                    """;
                command.Parameters.AddWithValue("$flow_key", key);
                command.Parameters.AddWithValue(
                    "$binary_hash",
                    normalizedBinaryHash);
                command.Parameters.AddWithValue(
                    "$semantic_hash",
                    normalizedSemanticHash);
                command.Parameters.AddWithValue(
                    "$layout_hash",
                    normalizedLayoutHash);
                return ReadSingle(command);
            }
        }

        public IReadOnlyList<FlowRevision> List(string flowKey)
        {
            string key = FlowRevisionStoreRules.NormalizeFlowKey(flowKey);
            lock (sync)
            {
                ThrowIfDisposed();
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = $"""
                    SELECT {SelectColumns}
                    FROM flow_definition_revision
                    WHERE flow_key = $flow_key
                    ORDER BY revision;
                    """;
                command.Parameters.AddWithValue("$flow_key", key);
                using SqliteDataReader reader = command.ExecuteReader();
                var results = new List<FlowRevision>();
                while (reader.Read())
                    results.Add(ReadRevision(reader));
                return results;
            }
        }

        public FlowRevision Append(FlowRevisionAppendRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            lock (sync)
            {
                ThrowIfDisposed();
                using SqliteTransaction transaction =
                    connection.BeginTransaction();
                try
                {
                    FlowRevision? head = GetHead(transaction, request.FlowKey);
                    FlowRevision revision =
                        FlowRevisionStoreRules.CreateRevision(request, head);
                    Insert(transaction, revision);
                    transaction.Commit();
                    return revision.DeepClone();
                }
                catch (SqliteException ex)
                    when (ex.SqliteErrorCode == 19
                        || ex.SqliteErrorCode == 5
                        || ex.SqliteErrorCode == 6)
                {
                    transaction.Rollback();
                    FlowRevision? actual = GetHead(request.FlowKey);
                    throw new FlowRevisionConflictException(
                        FlowRevisionStoreRules.NormalizeFlowKey(
                            request.FlowKey),
                        request.Condition,
                        actual);
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                    return;
                disposed = true;
                connection.Dispose();
            }
        }

        private void EnsureSchema()
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS flow_definition_revision
                (
                    flow_key TEXT NOT NULL,
                    revision INTEGER NOT NULL,
                    parent_revision INTEGER NULL,
                    base_binary_hash TEXT NULL,
                    source TEXT NOT NULL,
                    is_published INTEGER NOT NULL,
                    semantic_hash TEXT NOT NULL,
                    layout_hash TEXT NOT NULL,
                    binary_hash TEXT NOT NULL,
                    full_snapshot BLOB NOT NULL,
                    semantic_document TEXT NOT NULL,
                    author TEXT NULL,
                    message TEXT NULL,
                    external_version TEXT NULL,
                    rollback_of_revision INTEGER NULL,
                    created_time_utc TEXT NOT NULL,
                    PRIMARY KEY (flow_key, revision)
                );

                CREATE INDEX IF NOT EXISTS
                    idx_flow_definition_revision_binary_hash
                ON flow_definition_revision(flow_key, binary_hash);

                CREATE INDEX IF NOT EXISTS
                    idx_flow_definition_revision_content_hashes
                ON flow_definition_revision
                    (flow_key, binary_hash, semantic_hash, layout_hash);

                CREATE INDEX IF NOT EXISTS
                    idx_flow_definition_revision_published
                ON flow_definition_revision(flow_key, is_published, revision);
                """;
            command.ExecuteNonQuery();
        }

        private FlowRevision? GetHead(
            SqliteTransaction transaction,
            string flowKey)
        {
            string key = FlowRevisionStoreRules.NormalizeFlowKey(flowKey);
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"""
                SELECT {SelectColumns}
                FROM flow_definition_revision
                WHERE flow_key = $flow_key
                ORDER BY revision DESC
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("$flow_key", key);
            return ReadSingle(command);
        }

        private void Insert(
            SqliteTransaction transaction,
            FlowRevision revision)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO flow_definition_revision
                (
                    flow_key, revision, parent_revision, base_binary_hash,
                    source, is_published, semantic_hash, layout_hash,
                    binary_hash, full_snapshot, semantic_document, author,
                    message, external_version, rollback_of_revision,
                    created_time_utc
                )
                VALUES
                (
                    $flow_key, $revision, $parent_revision, $base_binary_hash,
                    $source, $is_published, $semantic_hash, $layout_hash,
                    $binary_hash, $full_snapshot, $semantic_document, $author,
                    $message, $external_version, $rollback_of_revision,
                    $created_time_utc
                );
                """;
            command.Parameters.AddWithValue("$flow_key", revision.FlowKey);
            command.Parameters.AddWithValue("$revision", revision.Revision);
            AddNullable(
                command,
                "$parent_revision",
                revision.ParentRevision);
            AddNullable(
                command,
                "$base_binary_hash",
                revision.BaseBinaryHash);
            command.Parameters.AddWithValue(
                "$source",
                revision.Source.ToString());
            command.Parameters.AddWithValue(
                "$is_published",
                revision.IsPublished ? 1 : 0);
            command.Parameters.AddWithValue(
                "$semantic_hash",
                revision.SemanticHash);
            command.Parameters.AddWithValue(
                "$layout_hash",
                revision.LayoutHash);
            command.Parameters.AddWithValue(
                "$binary_hash",
                revision.BinaryHash);
            command.Parameters.Add(
                "$full_snapshot",
                SqliteType.Blob).Value = revision.FullSnapshot;
            command.Parameters.AddWithValue(
                "$semantic_document",
                JsonSerializer.Serialize(
                    revision.SemanticDocument,
                    JsonOptions));
            AddNullable(command, "$author", revision.Author);
            AddNullable(command, "$message", revision.Message);
            AddNullable(
                command,
                "$external_version",
                revision.ExternalVersion);
            AddNullable(
                command,
                "$rollback_of_revision",
                revision.RollbackOfRevision);
            command.Parameters.AddWithValue(
                "$created_time_utc",
                revision.CreatedTimeUtc.ToString(
                    "O",
                    CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();
        }

        private static void AddNullable(
            SqliteCommand command,
            string name,
            object? value)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        private static FlowRevision? ReadSingle(SqliteCommand command)
        {
            using SqliteDataReader reader = command.ExecuteReader();
            return reader.Read() ? ReadRevision(reader) : null;
        }

        private static FlowRevision ReadRevision(SqliteDataReader reader)
        {
            string sourceText = reader.GetString(4);
            if (!Enum.TryParse(
                sourceText,
                ignoreCase: false,
                out FlowRevisionSource source))
            {
                throw new InvalidOperationException(
                    $"无法识别的流程版本来源：{sourceText}");
            }

            FlowSemanticDocument? semanticDocument =
                JsonSerializer.Deserialize<FlowSemanticDocument>(
                    reader.GetString(10),
                    JsonOptions);
            if (semanticDocument == null)
                throw new InvalidOperationException("流程语义快照为空。");

            return new FlowRevision
            {
                FlowKey = reader.GetString(0),
                Revision = reader.GetInt32(1),
                ParentRevision = reader.IsDBNull(2)
                    ? null
                    : reader.GetInt32(2),
                BaseBinaryHash = reader.IsDBNull(3)
                    ? null
                    : reader.GetString(3),
                Source = source,
                IsPublished = reader.GetInt32(5) != 0,
                SemanticHash = reader.GetString(6),
                LayoutHash = reader.GetString(7),
                BinaryHash = reader.GetString(8),
                FullSnapshot = (byte[])reader.GetValue(9),
                SemanticDocument = semanticDocument,
                Author = reader.IsDBNull(11)
                    ? null
                    : reader.GetString(11),
                Message = reader.IsDBNull(12)
                    ? null
                    : reader.GetString(12),
                ExternalVersion = reader.IsDBNull(13)
                    ? null
                    : reader.GetString(13),
                RollbackOfRevision = reader.IsDBNull(14)
                    ? null
                    : reader.GetInt32(14),
                CreatedTimeUtc = DateTime.Parse(
                    reader.GetString(15),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
            };
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }
    }
}
