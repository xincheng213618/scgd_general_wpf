using Microsoft.Data.Sqlite;
using System.IO;

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
                beforeBytes,
                afterBytes,
                quickCheck);
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

        private static string ExecuteScalarString(SqliteConnection connection, string sql)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToString(command.ExecuteScalar()) ?? string.Empty;
        }

        private sealed record LegacyJsonRow(int Id, string Json);
        private sealed record CompressedJsonRow(int Id, string Json, byte[] Payload);
        private sealed record ResidualJsonRow(int Id, string Json, byte[]? Payload);
    }

    internal sealed record LegacyResultJsonMigrationReport(
        int ViewResultRowsMigrated,
        int ObjectiveResultRowsMigrated,
        int ViewResidualRowsCleared,
        int ObjectiveResidualRowsCleared,
        long BeforeBytes,
        long AfterBytes,
        string IntegrityCheck);
}
