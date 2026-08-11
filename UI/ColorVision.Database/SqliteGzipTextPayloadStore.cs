using SqlSugar;
using System;
using System.Collections.Generic;
using System.Data;

namespace ColorVision.Database
{
    /// <summary>
    /// Stores a GZip text payload in columns that are intentionally not mapped by list entities.
    /// Callers can participate in an existing SqlSugar transaction.
    /// </summary>
    public static class SqliteGzipTextPayloadStore
    {
        public static void EnsureSchema(
            SqlSugarClient db,
            string tableName,
            string gzipColumnName,
            string lengthColumnName,
            string? previewColumnName = null)
        {
            ArgumentNullException.ThrowIfNull(db);
            ValidateIdentifier(tableName);
            ValidateIdentifier(gzipColumnName);
            ValidateIdentifier(lengthColumnName);
            if (previewColumnName != null)
                ValidateIdentifier(previewColumnName);

            EnsureColumn(db, tableName, gzipColumnName, "BLOB NULL");
            EnsureColumn(db, tableName, lengthColumnName, "INTEGER NULL");
            if (previewColumnName != null)
                EnsureColumn(db, tableName, previewColumnName, "TEXT NULL");
        }

        public static void Save(
            SqlSugarClient db,
            string tableName,
            string idColumnName,
            long id,
            string gzipColumnName,
            string lengthColumnName,
            string? text,
            string? previewColumnName = null,
            int previewCharacters = 256)
        {
            ArgumentNullException.ThrowIfNull(db);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
            ValidateIdentifier(tableName);
            ValidateIdentifier(idColumnName);
            ValidateIdentifier(gzipColumnName);
            ValidateIdentifier(lengthColumnName);
            if (previewColumnName != null)
                ValidateIdentifier(previewColumnName);

            GzipTextPayload payload = GzipTextPayloadCodec.Encode(text);
            string previewAssignment = previewColumnName == null
                ? string.Empty
                : $", \"{previewColumnName}\" = @preview";
            var parameters = new List<SugarParameter>
            {
                new("@payload", payload.CompressedBytes ?? (object)DBNull.Value, System.Data.DbType.Binary),
                new("@length", payload.Utf8Length ?? (object)DBNull.Value),
                new("@id", id),
            };
            if (previewColumnName != null)
                parameters.Add(new SugarParameter("@preview", GzipTextPayloadCodec.CreatePreview(text, previewCharacters) ?? (object)DBNull.Value));

            int updated = db.Ado.ExecuteCommand(
                $"UPDATE \"{tableName}\" SET \"{gzipColumnName}\" = @payload, " +
                $"\"{lengthColumnName}\" = @length{previewAssignment} " +
                $"WHERE \"{idColumnName}\" = @id;",
                parameters.ToArray());
            if (updated != 1)
                throw new InvalidOperationException($"{tableName} Id={id} 的压缩载荷写入失败。");
        }

        public static string? Load(
            SqlSugarClient db,
            string tableName,
            string idColumnName,
            long id,
            string gzipColumnName,
            string lengthColumnName,
            int maximumUtf8Bytes = GzipTextPayloadCodec.DefaultMaximumUtf8Bytes)
        {
            ArgumentNullException.ThrowIfNull(db);
            if (id <= 0)
                return null;
            ValidateIdentifier(tableName);
            ValidateIdentifier(idColumnName);
            ValidateIdentifier(gzipColumnName);
            ValidateIdentifier(lengthColumnName);

            DataTable result = db.Ado.GetDataTable(
                $"SELECT \"{gzipColumnName}\", \"{lengthColumnName}\" FROM \"{tableName}\" " +
                $"WHERE \"{idColumnName}\" = @id LIMIT 1;",
                new SugarParameter("@id", id));
            if (result.Rows.Count == 0)
                return null;

            object gzipValue = result.Rows[0][gzipColumnName];
            object lengthValue = result.Rows[0][lengthColumnName];
            byte[]? bytes = gzipValue == DBNull.Value ? null : gzipValue as byte[];
            int? length = lengthValue == DBNull.Value ? null : Convert.ToInt32(lengthValue);
            return GzipTextPayloadCodec.Decode(bytes, length, maximumUtf8Bytes);
        }

        private static void EnsureColumn(SqlSugarClient db, string tableName, string columnName, string columnDefinition)
        {
            DataTable columns = db.Ado.GetDataTable($"PRAGMA table_info(\"{tableName}\");");
            foreach (DataRow row in columns.Rows)
            {
                if (string.Equals(Convert.ToString(row["name"]), columnName, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            db.Ado.ExecuteCommand(
                $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {columnDefinition};");
        }

        internal static void ValidateIdentifier(string identifier)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
            foreach (char character in identifier)
            {
                if (!(char.IsAsciiLetterOrDigit(character) || character == '_'))
                    throw new ArgumentException($"无效的 SQLite 标识符：{identifier}", nameof(identifier));
            }
        }
    }
}
