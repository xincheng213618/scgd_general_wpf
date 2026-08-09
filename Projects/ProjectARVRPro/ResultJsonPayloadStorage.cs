using SqlSugar;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ProjectARVRPro
{
    /// <summary>
    /// 长期运行时的结果载荷存储。两个 BLOB 均位于原结果表中，但不映射到列表实体，
    /// 只有明确查看或导出结果时才按 Id 读取并解压。
    /// </summary>
    internal static class ResultJsonPayloadStorage
    {
        private static readonly object DatabaseMaintenanceGate = new();

        internal const string ViewResultColumnName = "ViewResultJsonGzip";
        internal const string ObjectiveResultColumnName = "ObjectiveTestResultJsonGzip";

        private const string ViewResultTableName = "ARVRReuslt";
        private const string ObjectiveResultTableName = "ObjectiveTestResultRecord";

        internal static T RunDatabaseMaintenance<T>(Func<T> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            lock (DatabaseMaintenanceGate)
                return action();
        }

        internal static void RunDatabaseMaintenance(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            lock (DatabaseMaintenanceGate)
                action();
        }

        public static void EnsureSchema(SqlSugarClient db)
        {
            ArgumentNullException.ThrowIfNull(db);
            EnsureBlobColumn(db, ViewResultTableName, ViewResultColumnName);
            EnsureBlobColumn(db, ObjectiveResultTableName, ObjectiveResultColumnName);
        }

        public static void SaveViewResultJson(SqlSugarClient db, int id, string? json)
        {
            SavePayload(db, ViewResultTableName, ViewResultColumnName, id, Compress(json));
        }

        public static void SaveObjectiveTestResultJson(SqlSugarClient db, int id, string? json)
        {
            SavePayload(db, ObjectiveResultTableName, ObjectiveResultColumnName, id, Compress(json));
        }

        public static string? LoadViewResultJson(SqlSugarClient db, int id)
        {
            return LoadPayload(db, ViewResultTableName, ViewResultColumnName, id);
        }

        public static string? LoadObjectiveTestResultJson(SqlSugarClient db, int id)
        {
            return LoadPayload(db, ObjectiveResultTableName, ObjectiveResultColumnName, id);
        }

        public static void LoadObjectiveTestResultJsons(SqlSugarClient db, IEnumerable<ObjectiveTestResultRecord> records)
        {
            ArgumentNullException.ThrowIfNull(db);
            ArgumentNullException.ThrowIfNull(records);

            Dictionary<int, ObjectiveTestResultRecord> recordsById = records
                .Where(item => item.Id > 0)
                .GroupBy(item => item.Id)
                .ToDictionary(group => group.Key, group => group.First());
            if (recordsById.Count == 0)
                return;

            foreach (int[] ids in recordsById.Keys.Chunk(500))
            {
                SugarParameter[] parameters = ids
                    .Select((id, index) => new SugarParameter($"@id{index}", id))
                    .ToArray();
                string parameterList = string.Join(", ", parameters.Select(item => item.ParameterName));
                DataTable table = db.Ado.GetDataTable(
                    $"SELECT \"Id\", \"{ObjectiveResultColumnName}\" FROM \"{ObjectiveResultTableName}\" WHERE \"Id\" IN ({parameterList});",
                    parameters);

                foreach (DataRow row in table.Rows)
                {
                    int id = Convert.ToInt32(row["Id"]);
                    if (recordsById.TryGetValue(id, out ObjectiveTestResultRecord? record))
                        record.ObjectiveTestResultJson = Decompress(ReadBytes(row[ObjectiveResultColumnName]));
                }
            }
        }

        internal static byte[]? Compress(string? json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                byte[] utf8 = Encoding.UTF8.GetBytes(json);
                gzip.Write(utf8, 0, utf8.Length);
            }
            return output.ToArray();
        }

        internal static string? Decompress(byte[]? payload)
        {
            if (payload == null || payload.Length == 0)
                return null;

            using var input = new MemoryStream(payload, writable: false);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
            return reader.ReadToEnd();
        }

        private static void EnsureBlobColumn(SqlSugarClient db, string tableName, string columnName)
        {
            DataTable columns = db.Ado.GetDataTable($"PRAGMA table_info(\"{tableName}\");");
            bool exists = columns.Rows.Cast<DataRow>()
                .Any(row => string.Equals(Convert.ToString(row["name"]), columnName, StringComparison.OrdinalIgnoreCase));
            if (!exists)
                db.Ado.ExecuteCommand($"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" BLOB NULL;");
        }

        private static void SavePayload(SqlSugarClient db, string tableName, string columnName, int id, byte[]? payload)
        {
            ArgumentNullException.ThrowIfNull(db);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

            if (payload == null)
            {
                db.Ado.ExecuteCommand(
                    $"UPDATE \"{tableName}\" SET \"{columnName}\" = NULL WHERE \"Id\" = @id;",
                    new SugarParameter("@id", id));
                return;
            }

            db.Ado.ExecuteCommand(
                $"UPDATE \"{tableName}\" SET \"{columnName}\" = @payload WHERE \"Id\" = @id;",
                new SugarParameter("@payload", payload, System.Data.DbType.Binary),
                new SugarParameter("@id", id));
        }

        private static string? LoadPayload(SqlSugarClient db, string tableName, string columnName, int id)
        {
            ArgumentNullException.ThrowIfNull(db);
            if (id <= 0)
                return null;

            DataTable table = db.Ado.GetDataTable(
                $"SELECT \"{columnName}\" FROM \"{tableName}\" WHERE \"Id\" = @id LIMIT 1;",
                new SugarParameter("@id", id));
            return table.Rows.Count == 0 ? null : Decompress(ReadBytes(table.Rows[0][columnName]));
        }

        private static byte[]? ReadBytes(object value)
        {
            return value == DBNull.Value ? null : value as byte[];
        }
    }
}
