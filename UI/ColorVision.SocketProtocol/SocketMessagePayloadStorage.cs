using ColorVision.Database;
using SqlSugar;

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
    }
}
