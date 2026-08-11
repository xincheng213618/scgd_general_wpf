using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;

namespace ColorVision.Database
{
    /// <summary>
    /// One-time, verified migration from a legacy TEXT column to GZip BLOB columns.
    /// Runtime storage never calls this class and never falls back to the legacy column.
    /// </summary>
    public static class SqliteGzipTextMigration
    {
        public static SqliteGzipTextMigrationReport Execute(
            string databasePath,
            IReadOnlyList<SqliteGzipTextMigrationSpec> specifications,
            int batchSize = 500)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
            ArgumentNullException.ThrowIfNull(specifications);
            if (!File.Exists(databasePath))
                throw new FileNotFoundException("SQLite 数据库文件不存在。", databasePath);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

            long beforeBytes = SqliteFileMaintenance.GetTotalStorageBytes(databasePath);
            SqliteFileMaintenance.EnsureVacuumFreeSpace(databasePath, beforeBytes);
            var tableReports = new List<SqliteGzipTextMigrationTableReport>(specifications.Count);

            using (SqliteConnection connection = SqliteFileMaintenance.OpenConnection(databasePath, SqliteOpenMode.ReadWrite))
            {
                string sourceCheck = ExecuteScalarString(connection, "PRAGMA quick_check;");
                if (!string.Equals(sourceCheck, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"迁移前数据库完整性检查未通过：{sourceCheck}");

                foreach (SqliteGzipTextMigrationSpec specification in specifications)
                    tableReports.Add(MigrateTable(connection, specification, batchSize));

                SqliteFileMaintenance.CheckpointWal(connection, "迁移后");
                try
                {
                    SqliteFileMaintenance.ExecuteNonQuery(connection, "VACUUM;");
                    SqliteFileMaintenance.CheckpointWal(connection, "VACUUM 后");
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "旧 TEXT 已迁移并清空，但 SQLite 空间释放失败。请关闭相关业务窗口后重新执行迁移，以再次运行 VACUUM。",
                        ex);
                }

                string finalCheck = ExecuteScalarString(connection, "PRAGMA quick_check;");
                if (!string.Equals(finalCheck, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"迁移后的 SQLite 完整性检查未通过：{finalCheck}");
            }

            return new SqliteGzipTextMigrationReport(
                tableReports,
                beforeBytes,
                SqliteFileMaintenance.GetTotalStorageBytes(databasePath),
                "ok");
        }

        private static SqliteGzipTextMigrationTableReport MigrateTable(
            SqliteConnection connection,
            SqliteGzipTextMigrationSpec specification,
            int batchSize)
        {
            ValidateSpecification(specification);
            EnsureColumn(connection, specification.TableName, specification.GzipColumnName, "BLOB NULL");
            EnsureColumn(connection, specification.TableName, specification.Utf8LengthColumnName, "INTEGER NULL");
            if (specification.PreviewColumnName != null)
                EnsureColumn(connection, specification.TableName, specification.PreviewColumnName, "TEXT NULL");

            if (!ColumnExists(connection, specification.TableName, specification.LegacyTextColumnName))
                return new SqliteGzipTextMigrationTableReport(specification.TableName, 0, 0, false);

            int migrated = MigratePendingRows(connection, specification, batchSize);
            long pending = ExecuteScalarInt64(
                connection,
                $"SELECT COUNT(*) FROM {Quote(specification.TableName)} " +
                $"WHERE {Quote(specification.LegacyTextColumnName)} IS NOT NULL " +
                $"AND {Quote(specification.GzipColumnName)} IS NULL;");
            if (pending != 0)
                throw new InvalidDataException($"{specification.TableName} 仍有 {pending:N0} 条旧文本未迁移。");

            int residualCleared = ClearVerifiedResidualRows(connection, specification, batchSize);
            long remainingLegacy = ExecuteScalarInt64(
                connection,
                $"SELECT COUNT(*) FROM {Quote(specification.TableName)} " +
                $"WHERE {Quote(specification.LegacyTextColumnName)} IS NOT NULL;");
            if (remainingLegacy != 0)
                throw new InvalidDataException($"{specification.TableName} 仍有 {remainingLegacy:N0} 条旧字段未清理。");

            return new SqliteGzipTextMigrationTableReport(
                specification.TableName,
                migrated,
                residualCleared,
                true);
        }

        private static int MigratePendingRows(
            SqliteConnection connection,
            SqliteGzipTextMigrationSpec specification,
            int batchSize)
        {
            int migrated = 0;
            long lastId = 0;
            while (true)
            {
                List<LegacyTextRow> rows = ReadPendingBatch(connection, specification, batchSize, lastId);
                if (rows.Count == 0)
                    return migrated;

                var encodedRows = new List<EncodedTextRow>(rows.Count);
                foreach (LegacyTextRow row in rows)
                {
                    GzipTextPayload payload = GzipTextPayloadCodec.Encode(row.Text);
                    string? restored = GzipTextPayloadCodec.Decode(payload.CompressedBytes, payload.Utf8Length);
                    if (!string.Equals(restored, row.Text, StringComparison.Ordinal))
                        throw new InvalidDataException($"{specification.TableName} Id={row.Id} 的压缩回读校验失败。");
                    encodedRows.Add(new EncodedTextRow(
                        row.Id,
                        row.Text,
                        payload.CompressedBytes!,
                        payload.Utf8Length!.Value,
                        specification.PreviewColumnName == null
                            ? null
                            : GzipTextPayloadCodec.CreatePreview(row.Text, specification.PreviewCharacters)));
                }

                using SqliteTransaction transaction = connection.BeginTransaction();
                foreach (EncodedTextRow row in encodedRows)
                {
                    using SqliteCommand command = CreateMigrationUpdate(connection, transaction, specification, row);
                    if (command.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException(
                            $"{specification.TableName} Id={row.Id} 在迁移期间发生变化，本批次不会提交。");
                }
                transaction.Commit();
                migrated += encodedRows.Count;
                lastId = rows[^1].Id;
            }
        }

        private static int ClearVerifiedResidualRows(
            SqliteConnection connection,
            SqliteGzipTextMigrationSpec specification,
            int batchSize)
        {
            int cleared = 0;
            long lastId = 0;
            while (true)
            {
                List<ResidualTextRow> rows = ReadResidualBatch(connection, specification, batchSize, lastId);
                if (rows.Count == 0)
                    return cleared;

                foreach (ResidualTextRow row in rows)
                {
                    string? restored;
                    try
                    {
                        restored = GzipTextPayloadCodec.Decode(row.Payload, row.Utf8Length);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException(
                            $"{specification.TableName} Id={row.Id} 已有压缩载荷损坏，旧文本已保留。",
                            ex);
                    }
                    if (!string.Equals(restored, row.Text, StringComparison.Ordinal))
                        throw new InvalidDataException(
                            $"{specification.TableName} Id={row.Id} 已有压缩载荷与旧文本不一致，旧文本已保留。");
                }

                using SqliteTransaction transaction = connection.BeginTransaction();
                foreach (ResidualTextRow row in rows)
                {
                    using SqliteCommand command = connection.CreateCommand();
                    command.Transaction = transaction;
                    string previewAssignment = specification.PreviewColumnName == null
                        ? string.Empty
                        : $", {Quote(specification.PreviewColumnName)} = $preview";
                    command.CommandText =
                        $"UPDATE {Quote(specification.TableName)} " +
                        $"SET {Quote(specification.LegacyTextColumnName)} = NULL{previewAssignment} " +
                        $"WHERE {Quote(specification.IdColumnName)} = $id " +
                        $"AND {Quote(specification.LegacyTextColumnName)} = $legacy " +
                        $"AND {Quote(specification.GzipColumnName)} = $payload " +
                        $"AND {Quote(specification.Utf8LengthColumnName)} = $length;";
                    command.Parameters.AddWithValue("$id", row.Id);
                    command.Parameters.AddWithValue("$legacy", row.Text);
                    command.Parameters.Add("$payload", SqliteType.Blob).Value = row.Payload;
                    command.Parameters.AddWithValue("$length", row.Utf8Length);
                    if (specification.PreviewColumnName != null)
                        command.Parameters.AddWithValue(
                            "$preview",
                            GzipTextPayloadCodec.CreatePreview(row.Text, specification.PreviewCharacters) ?? (object)DBNull.Value);
                    if (command.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException(
                            $"{specification.TableName} Id={row.Id} 在残留清理期间发生变化，本批次不会提交。");
                }
                transaction.Commit();
                cleared += rows.Count;
                lastId = rows[^1].Id;
            }
        }

        private static SqliteCommand CreateMigrationUpdate(
            SqliteConnection connection,
            SqliteTransaction transaction,
            SqliteGzipTextMigrationSpec specification,
            EncodedTextRow row)
        {
            SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            string previewAssignment = specification.PreviewColumnName == null
                ? string.Empty
                : $", {Quote(specification.PreviewColumnName)} = $preview";
            command.CommandText =
                $"UPDATE {Quote(specification.TableName)} SET " +
                $"{Quote(specification.GzipColumnName)} = $payload, " +
                $"{Quote(specification.Utf8LengthColumnName)} = $length, " +
                $"{Quote(specification.LegacyTextColumnName)} = NULL{previewAssignment} " +
                $"WHERE {Quote(specification.IdColumnName)} = $id " +
                $"AND {Quote(specification.GzipColumnName)} IS NULL " +
                $"AND {Quote(specification.LegacyTextColumnName)} = $legacy;";
            command.Parameters.Add("$payload", SqliteType.Blob).Value = row.Payload;
            command.Parameters.AddWithValue("$length", row.Utf8Length);
            command.Parameters.AddWithValue("$id", row.Id);
            command.Parameters.AddWithValue("$legacy", row.Text);
            if (specification.PreviewColumnName != null)
                command.Parameters.AddWithValue("$preview", row.Preview ?? (object)DBNull.Value);
            return command;
        }

        private static List<LegacyTextRow> ReadPendingBatch(
            SqliteConnection connection,
            SqliteGzipTextMigrationSpec specification,
            int batchSize,
            long lastId)
        {
            var rows = new List<LegacyTextRow>(batchSize);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                $"SELECT {Quote(specification.IdColumnName)}, {Quote(specification.LegacyTextColumnName)} " +
                $"FROM {Quote(specification.TableName)} " +
                $"WHERE {Quote(specification.IdColumnName)} > $lastId " +
                $"AND {Quote(specification.LegacyTextColumnName)} IS NOT NULL " +
                $"AND {Quote(specification.GzipColumnName)} IS NULL " +
                $"ORDER BY {Quote(specification.IdColumnName)} LIMIT $limit;";
            command.Parameters.AddWithValue("$lastId", lastId);
            command.Parameters.AddWithValue("$limit", batchSize);
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
                rows.Add(new LegacyTextRow(reader.GetInt64(0), reader.GetString(1)));
            return rows;
        }

        private static List<ResidualTextRow> ReadResidualBatch(
            SqliteConnection connection,
            SqliteGzipTextMigrationSpec specification,
            int batchSize,
            long lastId)
        {
            var rows = new List<ResidualTextRow>(batchSize);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                $"SELECT {Quote(specification.IdColumnName)}, {Quote(specification.LegacyTextColumnName)}, " +
                $"{Quote(specification.GzipColumnName)}, {Quote(specification.Utf8LengthColumnName)} " +
                $"FROM {Quote(specification.TableName)} " +
                $"WHERE {Quote(specification.IdColumnName)} > $lastId " +
                $"AND {Quote(specification.LegacyTextColumnName)} IS NOT NULL " +
                $"AND {Quote(specification.GzipColumnName)} IS NOT NULL " +
                $"ORDER BY {Quote(specification.IdColumnName)} LIMIT $limit;";
            command.Parameters.AddWithValue("$lastId", lastId);
            command.Parameters.AddWithValue("$limit", batchSize);
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (reader.IsDBNull(3))
                    throw new InvalidDataException(
                        $"{specification.TableName} Id={reader.GetInt64(0)} 已有压缩载荷但缺少 UTF-8 长度，旧文本已保留。");
                rows.Add(new ResidualTextRow(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetFieldValue<byte[]>(2),
                    reader.GetInt32(3)));
            }
            return rows;
        }

        private static void EnsureColumn(
            SqliteConnection connection,
            string tableName,
            string columnName,
            string definition)
        {
            if (!ColumnExists(connection, tableName, columnName))
                SqliteFileMaintenance.ExecuteNonQuery(
                    connection,
                    $"ALTER TABLE {Quote(tableName)} ADD COLUMN {Quote(columnName)} {definition};");
        }

        private static bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({Quote(tableName)});";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
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

        private static void ValidateSpecification(SqliteGzipTextMigrationSpec specification)
        {
            ArgumentNullException.ThrowIfNull(specification);
            SqliteGzipTextPayloadStore.ValidateIdentifier(specification.TableName);
            SqliteGzipTextPayloadStore.ValidateIdentifier(specification.IdColumnName);
            SqliteGzipTextPayloadStore.ValidateIdentifier(specification.LegacyTextColumnName);
            SqliteGzipTextPayloadStore.ValidateIdentifier(specification.GzipColumnName);
            SqliteGzipTextPayloadStore.ValidateIdentifier(specification.Utf8LengthColumnName);
            if (specification.PreviewColumnName != null)
                SqliteGzipTextPayloadStore.ValidateIdentifier(specification.PreviewColumnName);
            if (specification.PreviewCharacters <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(specification),
                    "PreviewCharacters 必须大于零。");
        }

        private static string Quote(string identifier) => $"\"{identifier}\"";

        private sealed record LegacyTextRow(long Id, string Text);
        private sealed record EncodedTextRow(long Id, string Text, byte[] Payload, int Utf8Length, string? Preview);
        private sealed record ResidualTextRow(long Id, string Text, byte[] Payload, int Utf8Length);
    }

    public sealed record SqliteGzipTextMigrationSpec(
        string TableName,
        string IdColumnName,
        string LegacyTextColumnName,
        string GzipColumnName,
        string Utf8LengthColumnName,
        string? PreviewColumnName = null,
        int PreviewCharacters = 256);

    public sealed record SqliteGzipTextMigrationTableReport(
        string TableName,
        int MigratedRows,
        int ResidualRowsCleared,
        bool LegacyColumnExists);

    public sealed record SqliteGzipTextMigrationReport(
        IReadOnlyList<SqliteGzipTextMigrationTableReport> Tables,
        long BeforeBytes,
        long AfterBytes,
        string IntegrityCheck);
}
