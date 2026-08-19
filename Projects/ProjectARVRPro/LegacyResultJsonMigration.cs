using Microsoft.Data.Sqlite;
using System.IO;
using System.Text.RegularExpressions;

namespace ProjectARVRPro
{
    /// <summary>
    /// 临时现场迁移工具：所有现场数据库完成迁移后可整体删除。
    /// 长期运行时代码不依赖本类，也不会读取旧 TEXT 字段。
    /// </summary>
    internal static class LegacyResultJsonMigration
    {
        private const int BatchSize = 500;
        private const string ViewTable = "ARVRReuslt";
        private const string ViewLegacyColumn = "ViewResultJson";
        private const string ViewFileNameColumn = "FileName";
        private const string ObjectiveTable = "ObjectiveTestResultRecord";
        private const string ObjectiveLegacyColumn = "ObjectiveTestResultJson";

        public static LegacyResultJsonMigrationReport Execute(string databasePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
            if (!File.Exists(databasePath))
                throw new FileNotFoundException("ARVRPro SQLite 数据库文件不存在。", databasePath);

            long beforeBytes = new FileInfo(databasePath).Length;
            int viewMigrated;
            int objectiveMigrated;
            int viewResidualCleared;
            int objectiveResidualCleared;
            bool viewFileNameMadeNullable;
            string quickCheck;

            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false,
                DefaultTimeout = 30,
            }.ToString();

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                ExecuteNonQuery(connection, "PRAGMA busy_timeout = 30000;");
                EnsureBlobColumn(connection, ViewTable, ResultJsonPayloadStorage.ViewResultColumnName);
                EnsureBlobColumn(connection, ObjectiveTable, ResultJsonPayloadStorage.ObjectiveResultColumnName);

                (viewMigrated, viewResidualCleared) = MigrateTable(
                    connection,
                    ViewTable,
                    ViewLegacyColumn,
                    ResultJsonPayloadStorage.ViewResultColumnName);
                (objectiveMigrated, objectiveResidualCleared) = MigrateTable(
                    connection,
                    ObjectiveTable,
                    ObjectiveLegacyColumn,
                    ResultJsonPayloadStorage.ObjectiveResultColumnName);
                viewFileNameMadeNullable = EnsureViewFileNameNullable(connection);

                CheckpointWal(connection, "迁移后");
                try
                {
                    ExecuteNonQuery(connection, "VACUUM;");
                    CheckpointWal(connection, "VACUUM 后");
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "旧 JSON 已迁移并清空，但 SQLite 空间释放失败。请停止 ARVR 测试后重新执行迁移按钮以再次运行 VACUUM。",
                        ex);
                }

                quickCheck = ExecuteScalarString(connection, "PRAGMA quick_check;");
                if (!string.Equals(quickCheck, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"迁移后的 SQLite 完整性检查未通过：{quickCheck}");
            }

            long afterBytes = new FileInfo(databasePath).Length;
            return new LegacyResultJsonMigrationReport(
                viewMigrated,
                objectiveMigrated,
                viewResidualCleared,
                objectiveResidualCleared,
                viewFileNameMadeNullable,
                beforeBytes,
                afterBytes,
                quickCheck);
        }

        private static bool EnsureViewFileNameNullable(SqliteConnection connection)
        {
            List<TableColumn> columns = ReadTableColumns(connection, ViewTable);
            TableColumn? fileNameColumn = columns.FirstOrDefault(column =>
                string.Equals(column.Name, ViewFileNameColumn, StringComparison.OrdinalIgnoreCase));
            if (fileNameColumn == null || !fileNameColumn.IsNotNull)
                return false;

            string createTableSql = ReadCreateTableSql(connection, ViewTable);
            string nullableCreateSql = MakeColumnNullable(createTableSql, ViewFileNameColumn);
            string temporaryTable = $"__{ViewTable}_FileNameNullable";
            string createTemporaryTableSql = ReplaceCreateTableName(nullableCreateSql, temporaryTable);
            List<string> schemaObjects = ReadSchemaObjectSql(connection, ViewTable);
            string columnList = string.Join(", ", columns.Select(column => QuoteIdentifier(column.Name)));
            long sourceRows = ExecuteScalarInt64(connection, $"SELECT COUNT(*) FROM {QuoteIdentifier(ViewTable)};");

            using SqliteTransaction transaction = connection.BeginTransaction();
            try
            {
                ExecuteNonQuery(connection, transaction, $"DROP TABLE IF EXISTS {QuoteIdentifier(temporaryTable)};");
                ExecuteNonQuery(connection, transaction, createTemporaryTableSql);
                ExecuteNonQuery(
                    connection,
                    transaction,
                    $"INSERT INTO {QuoteIdentifier(temporaryTable)} ({columnList}) " +
                    $"SELECT {columnList} FROM {QuoteIdentifier(ViewTable)};");

                long copiedRows = ExecuteScalarInt64(
                    connection,
                    transaction,
                    $"SELECT COUNT(*) FROM {QuoteIdentifier(temporaryTable)};");
                if (copiedRows != sourceRows)
                    throw new InvalidDataException($"{ViewTable} 表结构迁移行数不一致：原表 {sourceRows:N0} 行，新表 {copiedRows:N0} 行。");

                ExecuteNonQuery(connection, transaction, $"DROP TABLE {QuoteIdentifier(ViewTable)};");
                ExecuteNonQuery(
                    connection,
                    transaction,
                    $"ALTER TABLE {QuoteIdentifier(temporaryTable)} RENAME TO {QuoteIdentifier(ViewTable)};");
                foreach (string schemaObjectSql in schemaObjects)
                    ExecuteNonQuery(connection, transaction, schemaObjectSql);

                List<TableColumn> migratedColumns = ReadTableColumns(connection, transaction, ViewTable);
                TableColumn? migratedFileName = migratedColumns.FirstOrDefault(column =>
                    string.Equals(column.Name, ViewFileNameColumn, StringComparison.OrdinalIgnoreCase));
                if (migratedFileName == null || migratedFileName.IsNotNull)
                    throw new InvalidDataException($"{ViewTable}.{ViewFileNameColumn} 可空约束迁移校验失败。");

                long migratedRows = ExecuteScalarInt64(
                    connection,
                    transaction,
                    $"SELECT COUNT(*) FROM {QuoteIdentifier(ViewTable)};");
                if (migratedRows != sourceRows)
                    throw new InvalidDataException($"{ViewTable} 表结构迁移完成后行数不一致：迁移前 {sourceRows:N0} 行，迁移后 {migratedRows:N0} 行。");

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private static List<TableColumn> ReadTableColumns(
            SqliteConnection connection,
            string tableName)
        {
            return ReadTableColumns(connection, transaction: null, tableName);
        }

        private static List<TableColumn> ReadTableColumns(
            SqliteConnection connection,
            SqliteTransaction? transaction,
            string tableName)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"PRAGMA table_info({QuoteIdentifier(tableName)});";
            using SqliteDataReader reader = command.ExecuteReader();
            var columns = new List<TableColumn>();
            while (reader.Read())
                columns.Add(new TableColumn(reader.GetString(1), reader.GetInt32(3) != 0));
            return columns;
        }

        private static string ReadCreateTableSql(SqliteConnection connection, string tableName)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = $table;";
            command.Parameters.AddWithValue("$table", tableName);
            return Convert.ToString(command.ExecuteScalar())
                ?? throw new InvalidDataException($"无法读取 {tableName} 的建表语句。");
        }

        private static List<string> ReadSchemaObjectSql(SqliteConnection connection, string tableName)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT sql FROM sqlite_master " +
                "WHERE tbl_name = $table AND type IN ('index', 'trigger') AND sql IS NOT NULL " +
                "ORDER BY CASE type WHEN 'index' THEN 0 ELSE 1 END, name;";
            command.Parameters.AddWithValue("$table", tableName);
            using SqliteDataReader reader = command.ExecuteReader();
            var statements = new List<string>();
            while (reader.Read())
                statements.Add(reader.GetString(0));
            return statements;
        }

        private static string MakeColumnNullable(string createTableSql, string columnName)
        {
            int bodyStart = createTableSql.IndexOf('(');
            int bodyEnd = createTableSql.LastIndexOf(')');
            if (bodyStart < 0 || bodyEnd <= bodyStart)
                throw new InvalidDataException($"无法解析 {ViewTable} 的建表语句。");

            int segmentStart = bodyStart + 1;
            int nestedDepth = 0;
            char quoteEnd = '\0';
            for (int index = segmentStart; index <= bodyEnd; index++)
            {
                char current = index < bodyEnd ? createTableSql[index] : ',';
                if (quoteEnd != '\0')
                {
                    if (current == quoteEnd)
                    {
                        if (index + 1 < bodyEnd && createTableSql[index + 1] == quoteEnd && quoteEnd != ']')
                        {
                            index++;
                            continue;
                        }
                        quoteEnd = '\0';
                    }
                    continue;
                }

                quoteEnd = current switch
                {
                    '\'' => '\'',
                    '"' => '"',
                    '`' => '`',
                    '[' => ']',
                    _ => '\0',
                };
                if (quoteEnd != '\0')
                    continue;
                if (current == '(')
                {
                    nestedDepth++;
                    continue;
                }
                if (current == ')')
                {
                    nestedDepth--;
                    continue;
                }
                if (current != ',' || nestedDepth != 0)
                    continue;

                string segment = createTableSql[segmentStart..index];
                if (string.Equals(ReadLeadingIdentifier(segment), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    string nullableSegment = Regex.Replace(
                        segment,
                        @"\bNOT\s+NULL\b",
                        string.Empty,
                        RegexOptions.IgnoreCase);
                    if (string.Equals(nullableSegment, segment, StringComparison.Ordinal))
                        throw new InvalidDataException($"{ViewTable}.{columnName} 标记为 NOT NULL，但建表语句中未找到对应约束。");
                    return createTableSql[..segmentStart] + nullableSegment + createTableSql[index..];
                }
                segmentStart = index + 1;
            }

            throw new InvalidDataException($"{ViewTable} 建表语句中未找到 {columnName} 列。");
        }

        private static string ReadLeadingIdentifier(string columnDefinition)
        {
            int start = 0;
            while (start < columnDefinition.Length && char.IsWhiteSpace(columnDefinition[start]))
                start++;
            if (start >= columnDefinition.Length)
                return string.Empty;

            char first = columnDefinition[start];
            char endQuote = first switch
            {
                '"' => '"',
                '`' => '`',
                '[' => ']',
                _ => '\0',
            };
            if (endQuote != '\0')
            {
                int end = columnDefinition.IndexOf(endQuote, start + 1);
                return end < 0 ? string.Empty : columnDefinition[(start + 1)..end];
            }

            int index = start;
            while (index < columnDefinition.Length && !char.IsWhiteSpace(columnDefinition[index]))
                index++;
            return columnDefinition[start..index];
        }

        private static string ReplaceCreateTableName(string createTableSql, string tableName)
        {
            int bodyStart = createTableSql.IndexOf('(');
            if (bodyStart < 0)
                throw new InvalidDataException($"无法解析 {ViewTable} 的建表语句。");

            string header = createTableSql[..bodyStart];
            Match match = Regex.Match(
                header,
                @"\A(?<prefix>\s*CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?).+?\s*\z",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!match.Success)
                throw new InvalidDataException($"无法替换 {ViewTable} 建表语句中的表名。");

            return match.Groups["prefix"].Value + QuoteIdentifier(tableName) + createTableSql[bodyStart..];
        }

        private static string QuoteIdentifier(string identifier)
        {
            return $"\"{identifier.Replace("\"", "\"\"")}\"";
        }

        private static (int Migrated, int ResidualCleared) MigrateTable(
            SqliteConnection connection,
            string tableName,
            string legacyColumn,
            string gzipColumn)
        {
            if (!ColumnExists(connection, tableName, legacyColumn))
                return (0, 0);

            int migrated = 0;
            while (true)
            {
                List<LegacyJsonRow> rows = ReadPendingBatch(connection, tableName, legacyColumn, gzipColumn);
                if (rows.Count == 0)
                    break;

                var compressedRows = new List<CompressedJsonRow>(rows.Count);
                foreach (LegacyJsonRow row in rows)
                {
                    byte[] compressed = ResultJsonPayloadStorage.Compress(row.Json)
                        ?? throw new InvalidDataException($"{tableName} Id={row.Id} 的旧 JSON 无法压缩。");
                    string? restored = ResultJsonPayloadStorage.Decompress(compressed);
                    if (!string.Equals(restored, row.Json, StringComparison.Ordinal))
                        throw new InvalidDataException($"{tableName} Id={row.Id} 的压缩回读校验失败，旧字段未清理。");
                    compressedRows.Add(new CompressedJsonRow(row.Id, row.Json, compressed));
                }

                using SqliteTransaction transaction = connection.BeginTransaction();
                using SqliteCommand command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText =
                    $"UPDATE \"{tableName}\" " +
                    $"SET \"{gzipColumn}\" = $payload, \"{legacyColumn}\" = NULL " +
                    $"WHERE \"Id\" = $id AND \"{gzipColumn}\" IS NULL " +
                    $"AND \"{legacyColumn}\" = $legacy AND \"{legacyColumn}\" <> '';";
                SqliteParameter payloadParameter = command.Parameters.Add("$payload", SqliteType.Blob);
                SqliteParameter idParameter = command.Parameters.Add("$id", SqliteType.Integer);
                SqliteParameter legacyParameter = command.Parameters.Add("$legacy", SqliteType.Text);

                foreach (CompressedJsonRow row in compressedRows)
                {
                    payloadParameter.Value = row.Payload;
                    idParameter.Value = row.Id;
                    legacyParameter.Value = row.Json;
                    if (command.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException($"{tableName} Id={row.Id} 在迁移期间发生变化，已停止且本批次不会提交。");
                    migrated++;
                }
                transaction.Commit();
            }

            long pending = ExecuteScalarInt64(
                connection,
                $"SELECT COUNT(*) FROM \"{tableName}\" " +
                $"WHERE \"{legacyColumn}\" IS NOT NULL AND \"{legacyColumn}\" <> '' AND \"{gzipColumn}\" IS NULL;");
            if (pending != 0)
                throw new InvalidDataException($"{tableName} 仍有 {pending:N0} 条旧 JSON 未迁移，旧字段未统一清理。");

            int residualCleared = ClearVerifiedResidualRows(connection, tableName, legacyColumn, gzipColumn);
            long remainingLegacyValues = ExecuteScalarInt64(
                connection,
                $"SELECT COUNT(*) FROM \"{tableName}\" WHERE \"{legacyColumn}\" IS NOT NULL;");
            if (remainingLegacyValues != 0)
                throw new InvalidDataException($"{tableName} 仍有 {remainingLegacyValues:N0} 条旧字段未清理。");

            return (migrated, residualCleared);
        }

        private static int ClearVerifiedResidualRows(
            SqliteConnection connection,
            string tableName,
            string legacyColumn,
            string gzipColumn)
        {
            int cleared = 0;
            while (true)
            {
                List<ResidualJsonRow> rows = ReadResidualBatch(connection, tableName, legacyColumn, gzipColumn);
                if (rows.Count == 0)
                    return cleared;

                foreach (ResidualJsonRow row in rows.Where(item => item.Json.Length > 0))
                {
                    if (row.Payload == null || row.Payload.Length == 0)
                        throw new InvalidDataException($"{tableName} Id={row.Id} 已有压缩字段为空，旧 JSON 已保留。");

                    string? restored;
                    try
                    {
                        restored = ResultJsonPayloadStorage.Decompress(row.Payload);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException($"{tableName} Id={row.Id} 已有压缩字段损坏，旧 JSON 已保留。", ex);
                    }

                    if (!string.Equals(restored, row.Json, StringComparison.Ordinal))
                        throw new InvalidDataException($"{tableName} Id={row.Id} 已有压缩字段与旧 JSON 不一致，旧 JSON 已保留。");
                }

                using SqliteTransaction transaction = connection.BeginTransaction();
                using SqliteCommand command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText =
                    $"UPDATE \"{tableName}\" SET \"{legacyColumn}\" = NULL " +
                    $"WHERE \"Id\" = $id AND \"{legacyColumn}\" = $legacy " +
                    $"AND (($payload IS NULL AND \"{gzipColumn}\" IS NULL) OR \"{gzipColumn}\" = $payload);";
                SqliteParameter idParameter = command.Parameters.Add("$id", SqliteType.Integer);
                SqliteParameter legacyParameter = command.Parameters.Add("$legacy", SqliteType.Text);
                SqliteParameter payloadParameter = command.Parameters.Add("$payload", SqliteType.Blob);

                foreach (ResidualJsonRow row in rows)
                {
                    idParameter.Value = row.Id;
                    legacyParameter.Value = row.Json;
                    payloadParameter.Value = row.Payload ?? (object)DBNull.Value;
                    if (command.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException($"{tableName} Id={row.Id} 在残留清理期间发生变化，已停止且本批次不会提交。");
                    cleared++;
                }
                transaction.Commit();
            }
        }

        private static List<LegacyJsonRow> ReadPendingBatch(
            SqliteConnection connection,
            string tableName,
            string legacyColumn,
            string gzipColumn)
        {
            var rows = new List<LegacyJsonRow>(BatchSize);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                $"SELECT \"Id\", \"{legacyColumn}\" FROM \"{tableName}\" " +
                $"WHERE \"{legacyColumn}\" IS NOT NULL AND \"{legacyColumn}\" <> '' AND \"{gzipColumn}\" IS NULL " +
                $"ORDER BY \"Id\" LIMIT {BatchSize};";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
                rows.Add(new LegacyJsonRow(reader.GetInt32(0), reader.GetString(1)));
            return rows;
        }

        private static List<ResidualJsonRow> ReadResidualBatch(
            SqliteConnection connection,
            string tableName,
            string legacyColumn,
            string gzipColumn)
        {
            var rows = new List<ResidualJsonRow>(BatchSize);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                $"SELECT \"Id\", \"{legacyColumn}\", \"{gzipColumn}\" FROM \"{tableName}\" " +
                $"WHERE \"{legacyColumn}\" IS NOT NULL AND (\"{legacyColumn}\" = '' OR \"{gzipColumn}\" IS NOT NULL) " +
                $"ORDER BY \"Id\" LIMIT {BatchSize};";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                byte[]? payload = reader.IsDBNull(2) ? null : reader.GetFieldValue<byte[]>(2);
                rows.Add(new ResidualJsonRow(reader.GetInt32(0), reader.GetString(1), payload));
            }
            return rows;
        }

        private static void EnsureBlobColumn(SqliteConnection connection, string tableName, string columnName)
        {
            if (!ColumnExists(connection, tableName, columnName))
                ExecuteNonQuery(connection, $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" BLOB NULL;");
        }

        private static bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{tableName}\");";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static int ExecuteNonQuery(SqliteConnection connection, string sql)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            return command.ExecuteNonQuery();
        }

        private static int ExecuteNonQuery(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string sql)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            return command.ExecuteNonQuery();
        }

        private static void CheckpointWal(SqliteConnection connection, string stage)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            using SqliteDataReader reader = command.ExecuteReader();
            if (reader.Read() && reader.GetInt32(0) != 0)
                throw new InvalidOperationException($"{stage}无法截断 SQLite WAL，请停止 ARVR 查询后重试迁移。");
        }

        private static long ExecuteScalarInt64(SqliteConnection connection, string sql)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt64(command.ExecuteScalar());
        }

        private static long ExecuteScalarInt64(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string sql)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            return Convert.ToInt64(command.ExecuteScalar());
        }

        private static string ExecuteScalarString(SqliteConnection connection, string sql)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToString(command.ExecuteScalar()) ?? string.Empty;
        }

        private sealed record LegacyJsonRow(int Id, string Json);
        private sealed record CompressedJsonRow(int Id, string Json, byte[] Payload);
        private sealed record ResidualJsonRow(int Id, string Json, byte[]? Payload);
        private sealed record TableColumn(string Name, bool IsNotNull);
    }

    internal sealed record LegacyResultJsonMigrationReport(
        int ViewResultRowsMigrated,
        int ObjectiveResultRowsMigrated,
        int ViewResidualRowsCleared,
        int ObjectiveResidualRowsCleared,
        bool ViewFileNameMadeNullable,
        long BeforeBytes,
        long AfterBytes,
        string IntegrityCheck);
}
