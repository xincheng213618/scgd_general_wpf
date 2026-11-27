using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text;

namespace ColorVision.Engine.Templates.Flow.Search
{
    public sealed class SqliteFlowNodeSearchIndex :
        IFlowNodeSearchIndex,
        IDisposable
    {
        private readonly object sync = new();
        private readonly SqliteConnection connection;
        private bool disposed;

        public SqliteFlowNodeSearchIndex(string connectionString)
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

        public void ReplaceRevision(
            string flowKey,
            int revision,
            IReadOnlyCollection<FlowNodeSearchDocument> nodes)
        {
            string key = FlowSearchSafety.NormalizeFlowKey(flowKey);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revision);
            ArgumentNullException.ThrowIfNull(nodes);

            IReadOnlyList<FlowNodeSearchEntry> stable =
                FlowNodeSearchIndexer.Build(key, revision, nodes);

            lock (sync)
            {
                ThrowIfDisposed();
                using SqliteTransaction transaction =
                    connection.BeginTransaction();
                try
                {
                    using (SqliteCommand delete = connection.CreateCommand())
                    {
                        delete.Transaction = transaction;
                        delete.CommandText = """
                            DELETE FROM flow_node_search_index
                            WHERE flow_key = $flow_key
                                AND revision = $revision;
                            """;
                        delete.Parameters.AddWithValue("$flow_key", key);
                        delete.Parameters.AddWithValue("$revision", revision);
                        delete.ExecuteNonQuery();
                    }

                    InsertRevision(transaction, key, revision, stable);
                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public IReadOnlyList<FlowNodeSearchEntry> Search(
            FlowNodeSearchQuery query)
        {
            FlowNodeSearchQuery normalized =
                FlowSearchSafety.NormalizeQuery(query);
            lock (sync)
            {
                ThrowIfDisposed();
                using SqliteCommand command = connection.CreateCommand();
                bool searchLatestAcrossFlows = normalized.LatestOnly
                    && normalized.Revision == null
                    && normalized.FlowKey == null
                    && normalized.NodeTypeKey == null;
                var sql = new StringBuilder();
                if (searchLatestAcrossFlows)
                {
                    // Seek to the next flow key through the existing (flow_key, revision) index.
                    sql.AppendLine("""
                        WITH RECURSIVE flow_keys(key) AS (
                            SELECT MIN(flow_key) FROM flow_node_search_index
                            UNION ALL
                            SELECT (
                                SELECT flow_key FROM flow_node_search_index
                                WHERE flow_key > flow_keys.key
                                ORDER BY flow_key LIMIT 1
                            )
                            FROM flow_keys
                            WHERE key IS NOT NULL
                        )
                        """);
                }
                sql.AppendLine("""
                    SELECT
                        flow_key, revision, source_node_guid, node_path,
                        node_type_key, display_name, title, template_name,
                        device_code, service_code, tags, search_text
                    """);
                if (searchLatestAcrossFlows)
                {
                    // Drive the lookup from flow keys so MAX is computed once per flow.
                    sql.AppendLine("""
                        FROM flow_keys CROSS JOIN flow_node_search_index
                        WHERE flow_key = flow_keys.key AND revision = (
                            SELECT MAX(revision) FROM flow_node_search_index
                            WHERE flow_key = flow_keys.key
                        )
                        """);
                }
                else
                {
                    sql.AppendLine("FROM flow_node_search_index WHERE 1 = 1");
                }
                if (normalized.FlowKey != null)
                {
                    sql.AppendLine(" AND flow_key = $flow_key");
                    command.Parameters.AddWithValue(
                        "$flow_key",
                        normalized.FlowKey);
                }
                if (normalized.Revision != null)
                {
                    sql.AppendLine(" AND revision = $revision");
                    command.Parameters.AddWithValue(
                        "$revision",
                        normalized.Revision.Value);
                }
                else if (normalized.LatestOnly && !searchLatestAcrossFlows)
                {
                    sql.AppendLine("""
                         AND revision = (
                            SELECT MAX(latest.revision)
                            FROM flow_node_search_index AS latest
                            WHERE latest.flow_key =
                                flow_node_search_index.flow_key
                         )
                        """);
                }
                if (normalized.NodeTypeKey != null)
                {
                    sql.AppendLine(" AND node_type_key = $node_type_key");
                    command.Parameters.AddWithValue(
                        "$node_type_key",
                        normalized.NodeTypeKey);
                }
                if (normalized.Text != null)
                {
                    sql.AppendLine(
                        @" AND search_text LIKE $text ESCAPE '\'");
                    command.Parameters.AddWithValue(
                        "$text",
                        $"%{EscapeLike(normalized.Text)}%");
                }
                sql.AppendLine();
                sql.Append("ORDER BY revision DESC, flow_key, node_path");
                if (searchLatestAcrossFlows)
                    sql.Append(", flow_node_search_index.rowid");
                sql.AppendLine().AppendLine("LIMIT $limit;");
                command.Parameters.AddWithValue("$limit", normalized.Limit);
                command.CommandText = sql.ToString();

                using SqliteDataReader reader = command.ExecuteReader();
                var results = new List<FlowNodeSearchEntry>();
                while (reader.Read())
                {
                    results.Add(new FlowNodeSearchEntry
                    {
                        FlowKey = reader.GetString(0),
                        Revision = reader.GetInt32(1),
                        SourceNodeGuid = Guid.ParseExact(
                            reader.GetString(2),
                            "N"),
                        NodePath = reader.GetString(3),
                        NodeTypeKey = reader.GetString(4),
                        DisplayName = GetNullableString(reader, 5),
                        Title = GetNullableString(reader, 6),
                        TemplateName = GetNullableString(reader, 7),
                        DeviceCode = GetNullableString(reader, 8),
                        ServiceCode = GetNullableString(reader, 9),
                        Tags = reader.GetString(10),
                        SearchText = reader.GetString(11),
                    });
                }
                return results;
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
                CREATE TABLE IF NOT EXISTS flow_node_search_index
                (
                    flow_key TEXT NOT NULL,
                    revision INTEGER NOT NULL,
                    source_node_guid TEXT NOT NULL,
                    node_path TEXT NOT NULL,
                    node_type_key TEXT NOT NULL,
                    display_name TEXT NULL,
                    title TEXT NULL,
                    template_name TEXT NULL,
                    device_code TEXT NULL,
                    service_code TEXT NULL,
                    tags TEXT NOT NULL,
                    search_text TEXT NOT NULL,
                    PRIMARY KEY
                    (
                        flow_key,
                        revision,
                        source_node_guid,
                        node_path
                    )
                );

                CREATE INDEX IF NOT EXISTS idx_flow_node_search_flow_revision
                ON flow_node_search_index(flow_key, revision);

                CREATE INDEX IF NOT EXISTS idx_flow_node_search_type
                ON flow_node_search_index(node_type_key);
                """;
            command.ExecuteNonQuery();
        }

        private void InsertRevision(
            SqliteTransaction transaction,
            string flowKey,
            int revision,
            IReadOnlyList<FlowNodeSearchEntry> entries)
        {
            if (entries.Count == 0)
                return;

            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO flow_node_search_index
                (
                    flow_key, revision, source_node_guid, node_path,
                    node_type_key, display_name, title, template_name,
                    device_code, service_code, tags, search_text
                )
                VALUES
                (
                    $flow_key, $revision, $source_node_guid, $node_path,
                    $node_type_key, $display_name, $title, $template_name,
                    $device_code, $service_code, $tags, $search_text
                );
                """;
            command.Parameters.AddWithValue("$flow_key", flowKey);
            command.Parameters.AddWithValue("$revision", revision);
            SqliteParameter sourceNodeGuid = command.Parameters.Add("$source_node_guid", SqliteType.Text);
            SqliteParameter nodePath = command.Parameters.Add("$node_path", SqliteType.Text);
            SqliteParameter nodeTypeKey = command.Parameters.Add("$node_type_key", SqliteType.Text);
            SqliteParameter displayName = command.Parameters.Add("$display_name", SqliteType.Text);
            SqliteParameter title = command.Parameters.Add("$title", SqliteType.Text);
            SqliteParameter templateName = command.Parameters.Add("$template_name", SqliteType.Text);
            SqliteParameter deviceCode = command.Parameters.Add("$device_code", SqliteType.Text);
            SqliteParameter serviceCode = command.Parameters.Add("$service_code", SqliteType.Text);
            SqliteParameter tags = command.Parameters.Add("$tags", SqliteType.Text);
            SqliteParameter searchText = command.Parameters.Add("$search_text", SqliteType.Text);

            foreach (FlowNodeSearchEntry entry in entries)
            {
                sourceNodeGuid.Value = entry.SourceNodeGuid.ToString("N");
                nodePath.Value = entry.NodePath;
                nodeTypeKey.Value = entry.NodeTypeKey;
                displayName.Value = (object?)entry.DisplayName ?? DBNull.Value;
                title.Value = (object?)entry.Title ?? DBNull.Value;
                templateName.Value = (object?)entry.TemplateName ?? DBNull.Value;
                deviceCode.Value = (object?)entry.DeviceCode ?? DBNull.Value;
                serviceCode.Value = (object?)entry.ServiceCode ?? DBNull.Value;
                tags.Value = entry.Tags;
                searchText.Value = entry.SearchText;
                command.ExecuteNonQuery();
            }
        }

        private static string? GetNullableString(
            SqliteDataReader reader,
            int ordinal)
        {
            return reader.IsDBNull(ordinal)
                ? null
                : reader.GetString(ordinal);
        }

        private static string EscapeLike(string value)
        {
            return value
                .Replace(@"\", @"\\", StringComparison.Ordinal)
                .Replace("%", @"\%", StringComparison.Ordinal)
                .Replace("_", @"\_", StringComparison.Ordinal);
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }
    }
}
