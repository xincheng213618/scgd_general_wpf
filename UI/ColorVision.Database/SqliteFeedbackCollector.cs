using ColorVision.UI;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ColorVision.Database
{
    /// <summary>Exports persisted diagnostic records without initializing or modifying their source database.</summary>
    public abstract class SqliteFeedbackCollector : IFeedbackLogCollector, IFeedbackLogTimeRangeCollector
    {
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract int Order { get; }
        protected abstract string DatabasePath { get; }
        protected abstract string DatabaseFileName { get; }
        public bool IsSelectedByDefault => true;
        public int RecentDays { get; set; } = 7;
        public string? LogDirectory => Path.GetDirectoryName(DatabasePath);

        protected abstract void ExportTables(SqliteFeedbackExport context);

        public IEnumerable<(string EntryPath, string FilePath)> CollectFiles() => CollectFiles(DatabasePath, DateTimeOffset.Now);

        // Explicit paths and clocks let tests exercise collection without touching application data or singletons.
        internal IReadOnlyList<(string EntryPath, string FilePath)> CollectFiles(string sourcePath, DateTimeOffset now)
        {
            string outputPath = Path.Combine(Path.GetTempPath(), $"ColorVision_Feedback_{Guid.NewGuid():N}.db");
            string statusPath = Path.ChangeExtension(outputPath, ".json");
            var files = new List<(string, string)>();
            var tableCounts = new Dictionary<string, long>();
            var missingTables = new List<string>();
            string? error = null;
            try
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(RecentDays);
                if (!File.Exists(sourcePath))
                    throw new FileNotFoundException("源数据库不存在，尚未产生记录或当前未启用此功能。", sourcePath);

                using (var export = new SqliteFeedbackExport(sourcePath, outputPath, now, RecentDays, tableCounts, missingTables))
                {
                    ExportTables(export);
                    export.Complete();
                }
                string check = SqliteFileMaintenance.QuickCheck(outputPath);
                if (!string.Equals(check, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"导出数据库完整性检查失败：{check}");
                files.Add(($"Database/{DatabaseFileName}", outputPath));
            }
            catch (Exception ex)
            {
                error = $"{ex.GetType().Name}: {ex.Message}";
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
            }

            File.WriteAllText(statusPath, JsonConvert.SerializeObject(new
            {
                Database = DatabaseFileName,
                RecentDays,
                From = now.AddDays(-Math.Max(1, RecentDays)),
                To = now,
                Status = error != null ? "error" : missingTables.Count > 0 ? "partial" : "ok",
                Tables = tableCounts,
                MissingTables = missingTables,
                Error = error,
                Notes = "按记录时间筛选，包含关联的运行、节点、消息和结果；关联记录可能早于起始时间。保留原始字段及压缩正文，不收集字段引用的图片文件。各数据库为独立读取快照，只包含读取时已提交的记录。"
            }, Formatting.Indented));
            files.Add(($"Database/{DatabaseFileName}.export.json", statusPath));
            return files;
        }
    }

    /// <summary>
    /// A single read snapshot of an attached, read-only source. INSERT SELECT copies only selected rows,
    /// including unmapped payload BLOBs; it never copies the entire history or runs source migrations.
    /// SQL predicates are supplied by the owning modules, using src for the source row and main for exported tables.
    /// </summary>
    public sealed class SqliteFeedbackExport : IDisposable
    {
        private readonly SqliteConnection connection;
        private readonly SqliteTransaction transaction;
        private readonly Dictionary<string, string> tables = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> columns = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, long> tableCounts;
        private readonly List<string> missingTables;
        private readonly DateTimeOffset now;
        private readonly int days;

        internal SqliteFeedbackExport(string sourcePath, string outputPath, DateTimeOffset now, int days,
            Dictionary<string, long> tableCounts, List<string> missingTables)
        {
            this.now = now;
            this.days = days;
            this.tableCounts = tableCounts;
            this.missingTables = missingTables;
            // Opening main with a file URI enables URI handling for subsequent ATTACH statements too.
            connection = SqliteFileMaintenance.OpenConnection(new Uri(Path.GetFullPath(outputPath)).AbsoluteUri, SqliteOpenMode.ReadWriteCreate);
            try
            {
                using (var attach = connection.CreateCommand())
                {
                    attach.CommandText = "ATTACH DATABASE @source AS source;";
                    // mode=ro rejects all writes to the source, while retaining normal WAL visibility.
                    attach.Parameters.AddWithValue("@source", new Uri(Path.GetFullPath(sourcePath)).AbsoluteUri + "?mode=ro");
                    attach.ExecuteNonQuery();
                }
                transaction = connection.BeginTransaction(deferred: true);
                using var command = CreateCommand("SELECT name, sql FROM source.sqlite_schema WHERE type = 'table' AND name NOT LIKE 'sqlite_%';");
                using var reader = command.ExecuteReader();
                while (reader.Read())
                    tables.Add(reader.GetString(0), reader.GetString(1));
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        public bool HasTable(string table) => tables.ContainsKey(table);

        public bool HasColumn(string table, string column)
        {
            if (!HasTable(table)) return false;
            if (!columns.TryGetValue(table, out var names))
            {
                names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using var command = CreateCommand($"PRAGMA source.table_info({Quote(table)});");
                using var reader = command.ExecuteReader();
                while (reader.Read()) names.Add(reader.GetString(1));
                columns.Add(table, names);
            }
            return names.Contains(column);
        }

        public string Recent(string table, string alias, string[] localColumns, params string[] utcColumns)
        {
            var conditions = new List<string>();
            foreach (string column in localColumns.Where(column => HasColumn(table, column)))
                conditions.Add($"julianday({alias}.{Quote(column)}) BETWEEN julianday(@localFrom) AND julianday(@localTo)");
            foreach (string column in utcColumns.Where(column => HasColumn(table, column)))
                conditions.Add($"julianday({alias}.{Quote(column)}) BETWEEN julianday(@utcFrom) AND julianday(@utcTo)");
            if (HasTable(table) && conditions.Count == 0)
                throw new InvalidDataException($"{table} 缺少可识别的记录时间字段，无法按时间导出。");
            return conditions.Count == 0 ? "0" : "(" + string.Join(" OR ", conditions) + ")";
        }

        public void ExportTable(string table, string predicate)
        {
            if (!tables.TryGetValue(table, out string? createSql))
            {
                missingTables.Add(table);
                return;
            }
            using (var create = CreateCommand(createSql)) create.ExecuteNonQuery();
            using (var copy = CreateCommand($"INSERT INTO main.{Quote(table)} SELECT src.* FROM source.{Quote(table)} AS src WHERE {predicate};"))
                copy.ExecuteNonQuery();
            // Retain query indexes before other tables join this exported table. Triggers are not copied.
            var indexes = new List<string>();
            using (var schema = CreateCommand("SELECT sql FROM source.sqlite_schema WHERE type = 'index' AND tbl_name = @table COLLATE NOCASE AND sql IS NOT NULL;"))
            {
                schema.Parameters.AddWithValue("@table", table);
                using var reader = schema.ExecuteReader();
                while (reader.Read()) indexes.Add(reader.GetString(0));
            }
            foreach (string sql in indexes)
            {
                using var index = CreateCommand(sql);
                index.ExecuteNonQuery();
            }
            using var count = CreateCommand($"SELECT COUNT(*) FROM main.{Quote(table)};");
            tableCounts.Add(table, Convert.ToInt64(count.ExecuteScalar(), CultureInfo.InvariantCulture));
        }

        internal void Complete() => transaction.Commit();

        private SqliteCommand CreateCommand(string sql)
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.Parameters.AddWithValue("@localFrom", FormatDate(now.AddDays(-days).DateTime));
            command.Parameters.AddWithValue("@localTo", FormatDate(now.DateTime));
            command.Parameters.AddWithValue("@utcFrom", FormatDate(now.AddDays(-days).UtcDateTime));
            command.Parameters.AddWithValue("@utcTo", FormatDate(now.UtcDateTime));
            return command;
        }

        private static string FormatDate(DateTime value) => value.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture);
        private static string Quote(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";

        public void Dispose()
        {
            transaction.Dispose();
            connection.Dispose();
        }
    }
}
