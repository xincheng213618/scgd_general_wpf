using ColorVision.Database;
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
        private const int BulkDecodeBufferLength = 16 * 1024;

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

        public static void SaveViewResultJson(SqlSugarClient db, int id, string? json, ResultSqliteWriteTiming? timing = null)
            => SaveJson(db, ViewResultTableName, ViewResultColumnName, id, json, timing);

        public static void SaveObjectiveTestResultJson(SqlSugarClient db, int id, string? json, ResultSqliteWriteTiming? timing = null)
            => SaveJson(db, ObjectiveResultTableName, ObjectiveResultColumnName, id, json, timing);

        private static void SaveJson(SqlSugarClient db, string tableName, string columnName, int id, string? json,
            ResultSqliteWriteTiming? timing)
        {
            byte[]? payload = Compress(json);
            timing?.CompressionMs = timing.FinishStage();
            SavePayload(db, tableName, columnName, id, payload);
            timing?.PayloadMs = timing.FinishStage();
        }

        public static string? LoadViewResultJson(SqlSugarClient db, int id)
        {
            return LoadPayload(db, ViewResultTableName, ViewResultColumnName, id);
        }

        public static string? LoadObjectiveTestResultJson(SqlSugarClient db, int id)
        {
            return LoadPayload(db, ObjectiveResultTableName, ObjectiveResultColumnName, id);
        }

        public static void LoadObjectiveTestResultJsons(SqlSugarClient db, IEnumerable<ObjectiveTestResultRecord> records, ReadOnlySqliteDatabase? offline = null) =>
            LoadPayloads(db, records, record => record.Id, (record, json) => record.ObjectiveTestResultJson = json,
                ObjectiveResultTableName, ObjectiveResultColumnName, nameof(ObjectiveTestResultRecord.ObjectiveTestResultJson), offline);

        public static void LoadViewResultJsons(SqlSugarClient db, IEnumerable<ProjectARVRReuslt> results, ReadOnlySqliteDatabase? offline = null) =>
            LoadPayloads(db, results, result => result.Id, (result, json) => result.ViewResultJson = json,
                ViewResultTableName, ViewResultColumnName, nameof(ProjectARVRReuslt.ViewResultJson), offline);

        internal static BatchViewPayloads ReadBatchViewPayloads(SqlSugarClient db, IEnumerable<ProjectARVRReuslt> results, ReadOnlySqliteDatabase? offline)
        {
            var payloads = new BatchViewPayloads(results, offline != null);
            ReadPayloadValues(db, payloads.Ids, ViewResultTableName, ViewResultColumnName, nameof(ProjectARVRReuslt.ViewResultJson), offline, payloads.Set);
            return payloads;
        }

        internal static StreamReader OpenGzipText(byte[] payload, bool detectEncodingFromByteOrderMarks)
            => new(new GZipStream(new MemoryStream(payload, writable: false), CompressionMode.Decompress),
                Encoding.UTF8, detectEncodingFromByteOrderMarks);

        private static StreamReader? OpenNonBlankGzipText(byte[] payload, bool detectEncodingFromByteOrderMarks)
        {
            StreamReader? reader = OpenGzipText(payload, detectEncodingFromByteOrderMarks);
            try
            {
                int next = reader.Peek();
                while (next >= 0 && char.IsWhiteSpace((char)next))
                {
                    reader.Read();
                    next = reader.Peek();
                }
                if (next < 0)
                    return null;

                StreamReader result = reader;
                reader = null;
                return result;
            }
            finally
            {
                reader?.Dispose();
            }
        }

        private static void LoadPayloads<T>(SqlSugarClient db, IEnumerable<T> records, Func<T, int> getId, Action<T, string?> assign,
            string tableName, string gzipColumn, string legacyColumn, ReadOnlySqliteDatabase? offline) where T : class
        {
            ArgumentNullException.ThrowIfNull(db);
            ArgumentNullException.ThrowIfNull(records);

            var recordsById = new Dictionary<int, T>();
            foreach (T record in records)
            {
                int id = getId(record);
                if (offline != null || id > 0)
                    recordsById.TryAdd(id, record);
            }
            char[]? decodeBuffer = null;
            ReadPayloadValues(db, recordsById.Keys, tableName, gzipColumn, legacyColumn, offline, (id, payload, legacyText) =>
            {
                if (payload is { Length: > 0 } && recordsById.Count > 1)
                    decodeBuffer ??= new char[BulkDecodeBufferLength];
                assign(recordsById[id], Decompress(payload, detectEncodingFromByteOrderMarks: offline != null, decodeBuffer) ?? legacyText);
            });
        }

        private static void ReadPayloadValues(SqlSugarClient db, IEnumerable<int> recordIds, string tableName, string gzipColumn, string legacyColumn,
            ReadOnlySqliteDatabase? offline, Action<int, byte[]?, string?> readPayload)
        {
            string gzip = offline == null || offline.HasColumn(tableName, gzipColumn) ? $"\"{gzipColumn}\"" : "NULL";
            string legacy = offline?.HasColumn(tableName, legacyColumn) == true ? $"\"{legacyColumn}\"" : "NULL";
            foreach (int[] ids in recordIds.Chunk(500))
            {
                SugarParameter[] parameters = ids
                    .Select((id, index) => new SugarParameter($"@id{index}", id))
                    .ToArray();
                string parameterList = string.Join(", ", parameters.Select(item => item.ParameterName));
                using var reader = db.Ado.GetDataReader(
                    $"SELECT \"Id\", {gzip}, {legacy} FROM \"{tableName}\" WHERE \"Id\" IN ({parameterList});",
                    parameters);

                while (reader.Read())
                {
                    byte[]? payload = ReadBytes(reader.GetValue(1));
                    string? legacyText = payload is not { Length: > 0 } && !reader.IsDBNull(2) ? reader.GetString(2) : null;
                    readPayload(reader.GetInt32(0), payload, legacyText);
                }
            }
        }

        internal sealed class BatchViewPayloads
        {
            private readonly Dictionary<int, Entry> entries = [];
            private readonly bool isOffline;
            private char[]? decodeBuffer;

            private readonly record struct Entry(int RemainingUses, byte[]? Gzip, string? LegacyText);

            internal BatchViewPayloads(IEnumerable<ProjectARVRReuslt> results, bool isOffline)
            {
                this.isOffline = isOffline;
                foreach (ProjectARVRReuslt result in results)
                {
                    if (isOffline || result.Id > 0)
                        entries[result.Id] = new Entry(entries.GetValueOrDefault(result.Id).RemainingUses + 1, null, null);
                }
            }

            internal IEnumerable<int> Ids => entries.Keys;

            internal void Set(int id, byte[]? gzip, string? legacyText)
                => entries[id] = entries[id] with { Gzip = gzip, LegacyText = legacyText };

            internal void Decode(IReadOnlyList<ProjectARVRReuslt> results)
            {
                foreach (ProjectARVRReuslt result in results)
                {
                    if (!entries.TryGetValue(result.Id, out Entry entry) || (entry.Gzip == null && entry.LegacyText == null))
                        continue;
                    if (entry.RemainingUses == 1 && entry.Gzip is { Length: > 0 } gzip
                        && !string.IsNullOrWhiteSpace(result.ProcessTypeFullName) && !string.IsNullOrWhiteSpace(result.ProcessConfigJson))
                    {
                        result.CsvJsonReader = OpenNonBlankGzipText(gzip, isOffline);
                    }
                    else
                    {
                        if (entry.Gzip is { Length: > 0 })
                            decodeBuffer ??= new char[BulkDecodeBufferLength];
                        result.ViewResultJson = Decompress(entry.Gzip, isOffline, decodeBuffer) ?? entry.LegacyText;
                    }
                    entries[result.Id] = entry with { Gzip = null, LegacyText = null };
                }
            }

            internal void Release(IReadOnlyList<ProjectARVRReuslt> results)
            {
                foreach (ProjectARVRReuslt result in results)
                {
                    if (!entries.TryGetValue(result.Id, out Entry entry))
                        continue;
                    if (entry.RemainingUses > 1)
                    {
                        entries[result.Id] = entry with { RemainingUses = entry.RemainingUses - 1 };
                        continue;
                    }
                    entries.Remove(result.Id);
                    result.ViewResultJson = null;
                    result.CsvJsonReader?.Dispose();
                    result.CsvJsonReader = null;
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
                Span<byte> buffer = stackalloc byte[4096];
                ReadOnlySpan<char> remaining = json.AsSpan();
                // UTF-8 needs at most three bytes per UTF-16 code unit.
                if (remaining.Length <= buffer.Length / 3)
                {
                    int written = Encoding.UTF8.GetBytes(remaining, buffer);
                    gzip.Write(buffer[..written]);
                }
                else
                {
                    Encoder encoder = Encoding.UTF8.GetEncoder();
                    bool completed;
                    do
                    {
                        encoder.Convert(remaining, buffer, flush: true, out int charsUsed, out int bytesUsed, out completed);
                        gzip.Write(buffer[..bytesUsed]);
                        remaining = remaining[charsUsed..];
                    }
                    while (!completed);
                }
            }
            return output.ToArray();
        }

        internal static string? Decompress(byte[]? payload, bool detectEncodingFromByteOrderMarks = false, char[]? decodeBuffer = null)
        {
            if (payload == null || payload.Length == 0)
                return null;

            using var reader = OpenGzipText(payload, detectEncodingFromByteOrderMarks);
            if (decodeBuffer is not { Length: > 0 })
                return reader.ReadToEnd();

            // Decode most bulk payloads straight into the shared buffer and allocate only the final string.
            int count = reader.ReadBlock(decodeBuffer, 0, decodeBuffer.Length);
            if (count < decodeBuffer.Length || reader.Peek() < 0)
                return new string(decodeBuffer, 0, count);

            var json = new StringBuilder(count);
            do
            {
                json.Append(decodeBuffer, 0, count);
                count = reader.Read(decodeBuffer, 0, decodeBuffer.Length);
            } while (count > 0);
            return json.ToString();
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

            object? payload = db.Ado.GetScalar(
                $"SELECT \"{columnName}\" FROM \"{tableName}\" WHERE \"Id\" = @id LIMIT 1;",
                new SugarParameter("@id", id));
            return Decompress(payload as byte[]);
        }

        private static byte[]? ReadBytes(object value)
        {
            return value == DBNull.Value ? null : value as byte[];
        }
    }
}
