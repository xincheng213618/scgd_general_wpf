using ColorVision.Database;
using SqlSugar;
using System.Data;

namespace ColorVision.SocketProtocol
{
    /// <summary>
    /// Socket 正文的唯一长期运行时存储入口。列表实体不映射压缩列，
    /// 只有明确查看、复制或重发时才按 Id 读取并解压。
    /// </summary>
    public static class SocketMessagePayloadStorage
    {
        private static readonly object DatabaseMaintenanceGate = new();

        public const string TableName = "SocketMessage";
        public const string IdColumnName = "id";
        public const string LegacyContentColumnName = "Content";
        public const string GzipColumnName = "ContentGzip";
        public const string Utf8LengthColumnName = "ContentUtf8Length";
        public const string PreviewColumnName = "ContentPreview";
        public const int PreviewCharacters = 96;
        private const string ConnectionIdColumnName = nameof(SocketMessage.ClientConnectionId);
        private const string MessageIdentityPredicate = $"\"{IdColumnName}\" = @id AND \"{ConnectionIdColumnName}\" IS @connectionId";

        public static T RunDatabaseMaintenance<T>(Func<T> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            lock (DatabaseMaintenanceGate)
                return action();
        }

        public static void RunDatabaseMaintenance(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            lock (DatabaseMaintenanceGate)
                action();
        }

        public static void EnsureSchema(SqlSugarClient db)
        {
            ArgumentNullException.ThrowIfNull(db);
            SqliteGzipTextPayloadStore.EnsureSchema(
                db,
                TableName,
                GzipColumnName,
                Utf8LengthColumnName,
                PreviewColumnName);
            db.Ado.ExecuteCommand(
                $"CREATE INDEX IF NOT EXISTS \"IX_{TableName}_MessageTime\" " +
                $"ON \"{TableName}\" (\"MessageTime\");");
        }

        internal static void EnsureConnectionIdentityColumn(SqlSugarClient db)
        {
            DataTable columns = db.Ado.GetDataTable($"PRAGMA table_info(\"{TableName}\");");
            if (columns.Rows.Count == 0) return;
            foreach (DataRow column in columns.Rows)
                if (string.Equals(Convert.ToString(column["name"]), ConnectionIdColumnName, StringComparison.OrdinalIgnoreCase)) return;

            db.Ado.ExecuteCommand($"ALTER TABLE \"{TableName}\" ADD COLUMN \"{ConnectionIdColumnName}\" TEXT NULL;");
        }

        public static void Save(SqlSugarClient db, int id, string? content)
        {
            SqliteGzipTextPayloadStore.Save(
                db,
                TableName,
                IdColumnName,
                id,
                GzipColumnName,
                Utf8LengthColumnName,
                content,
                PreviewColumnName,
                PreviewCharacters);
        }

        public static string? Load(SqlSugarClient db, int id)
        {
            return SqliteGzipTextPayloadStore.Load(
                db,
                TableName,
                IdColumnName,
                id,
                GzipColumnName,
                Utf8LengthColumnName);
        }

        internal static string? Load(SqlSugarClient db, SocketMessage message)
        {
            var ado = db.Ado;
            byte[]? gzip;
            int? length;
            try
            {
                using var reader = ado.GetDataReader(
                    $"SELECT \"{GzipColumnName}\", \"{Utf8LengthColumnName}\" FROM \"{TableName}\" WHERE {MessageIdentityPredicate} LIMIT 1;",
                    CreateIdentityParameters(message));
                if (!reader.Read()) return null;

                gzip = reader.GetValue(0) as byte[];
                length = reader.IsDBNull(1) ? null : Convert.ToInt32(reader.GetValue(1));
            }
            catch
            {
                if (db.CurrentConnectionConfig.IsAutoCloseConnection && ado.Transaction == null)
                    ado.Close();
                throw;
            }
            return GzipTextPayloadCodec.Decode(gzip, length);
        }

        internal static void Delete(SqlSugarClient db, SocketMessage message) =>
            db.Ado.ExecuteCommand($"DELETE FROM \"{TableName}\" WHERE {MessageIdentityPredicate};", CreateIdentityParameters(message));

        private static SugarParameter[] CreateIdentityParameters(SocketMessage message) =>
        [
            new("@id", message.Id),
            new("@connectionId", message.ClientConnectionId ?? (object)DBNull.Value),
        ];
    }
}
