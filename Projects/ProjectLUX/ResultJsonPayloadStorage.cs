using SqlSugar;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ProjectLUX
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
                    $"SELECT \"Id\", {PayloadProjection(db, ObjectiveResultTableName, ObjectiveResultColumnName)} FROM \"{ObjectiveResultTableName}\" WHERE \"Id\" IN ({parameterList});",
                    parameters);

                foreach (DataRow row in table.Rows)
                {
                    int id = Convert.ToInt32(row["Id"]);
                    if (recordsById.TryGetValue(id, out ObjectiveTestResultRecord? record))
                        record.ObjectiveTestResultJson = ReadPayload(row, ObjectiveResultColumnName);
                }
            }
        }

        public static void LoadViewResultJsons(SqlSugarClient db, IEnumerable<ProjectLUXReuslt> results)
        {
            ArgumentNullException.ThrowIfNull(db);
            ArgumentNullException.ThrowIfNull(results);

            Dictionary<int, ProjectLUXReuslt> resultsById = results
                .Where(item => item.Id > 0)
                .GroupBy(item => item.Id)
                .ToDictionary(group => group.Key, group => group.First());
            if (resultsById.Count == 0)
                return;

            foreach (int[] ids in resultsById.Keys.Chunk(500))
            {
                SugarParameter[] parameters = ids
                    .Select((id, index) => new SugarParameter($"@id{index}", id))
                    .ToArray();
                string parameterList = string.Join(", ", parameters.Select(item => item.ParameterName));
                DataTable table = db.Ado.GetDataTable(
                    $"SELECT \"Id\", {PayloadProjection(db, ViewResultTableName, ViewResultColumnName)} FROM \"{ViewResultTableName}\" WHERE \"Id\" IN ({parameterList});",
                    parameters);

                foreach (DataRow row in table.Rows)
                {
                    int id = Convert.ToInt32(row["Id"]);
                    if (resultsById.TryGetValue(id, out ProjectLUXReuslt? result))
                        result.ViewResultJson = ReadPayload(row, ViewResultColumnName);
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

            string legacyColumn = LegacyColumnName(columnName);
            string clearLegacy = HasColumn(db, tableName, legacyColumn) ? $", \"{legacyColumn}\" = NULL" : string.Empty;
            int changed = db.Ado.ExecuteCommand(
                $"UPDATE \"{tableName}\" SET \"{columnName}\" = @payload{clearLegacy} WHERE \"Id\" = @id;",
                new SugarParameter("@payload", (object?)payload ?? DBNull.Value, System.Data.DbType.Binary),
                new SugarParameter("@id", id));
            if (changed != 1)
                throw new InvalidOperationException($"结果正文写入失败：table={tableName}, id={id}, affectedRows={changed}");
        }

        private static string? LoadPayload(SqlSugarClient db, string tableName, string columnName, int id)
        {
            ArgumentNullException.ThrowIfNull(db);
            if (id <= 0)
                return null;

            DataTable table = db.Ado.GetDataTable(
                $"SELECT {PayloadProjection(db, tableName, columnName)} FROM \"{tableName}\" WHERE \"Id\" = @id LIMIT 1;",
                new SugarParameter("@id", id));
            return table.Rows.Count == 0 ? null : ReadPayload(table.Rows[0], columnName);
        }

        private static string LegacyColumnName(string columnName) => columnName == ViewResultColumnName
            ? "ViewResultJson" : "ObjectiveTestResultJson";

        private static bool HasColumn(SqlSugarClient db, string tableName, string columnName) =>
            db.Ado.GetDataTable($"PRAGMA table_info(\"{tableName}\");").Rows.Cast<DataRow>()
                .Any(row => string.Equals(Convert.ToString(row["name"]), columnName, StringComparison.OrdinalIgnoreCase));

        private static string PayloadProjection(SqlSugarClient db, string tableName, string columnName)
        {
            string legacy = LegacyColumnName(columnName);
            return HasColumn(db, tableName, legacy)
                ? $"\"{columnName}\", \"{legacy}\" AS LegacyPayload"
                : $"\"{columnName}\", NULL AS LegacyPayload";
        }

        private static string? ReadPayload(DataRow row, string columnName)
        {
            // A corrupt compressed value must report an error, never silently fall back to stale TEXT.
            if (row[columnName] != DBNull.Value)
                return Decompress((byte[])row[columnName]);
            return row["LegacyPayload"] == DBNull.Value ? null : Convert.ToString(row["LegacyPayload"]);
        }
    }
}
