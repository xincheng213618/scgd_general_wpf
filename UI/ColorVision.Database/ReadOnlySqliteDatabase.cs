using Microsoft.Data.Sqlite;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ColorVision.Database
{
    /// <summary>A file-scoped reader. Opening it never runs application schema migration or starts writers.</summary>
    public sealed class ReadOnlySqliteDatabase
    {
        private readonly Dictionary<string, HashSet<string>> _schema = new(StringComparer.OrdinalIgnoreCase);
        public string FilePath { get; }
        public string ConnectionString { get; }

        public ReadOnlySqliteDatabase(string filePath)
        {
            FilePath = Path.GetFullPath(filePath);
            if (!File.Exists(FilePath)) throw new FileNotFoundException("找不到现场数据库。", FilePath);
            ConnectionString = new SqliteConnectionStringBuilder
            {
                DataSource = FilePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false,
            }.ToString();
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var tables = connection.CreateCommand();
            tables.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
            var names = new List<string>();
            using (var reader = tables.ExecuteReader())
                while (reader.Read()) names.Add(reader.GetString(0));
            foreach (string table in names)
            {
                using var columns = connection.CreateCommand();
                columns.CommandText = $"PRAGMA table_info({Quote(table)})";
                using var reader = columns.ExecuteReader();
                var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (reader.Read()) found.Add(reader.GetString(1));
                _schema.Add(table, found);
            }
        }

        public bool HasTable(string table) => _schema.ContainsKey(table);
        public bool HasColumn(string table, string column) => _schema.TryGetValue(table, out var columns) && columns.Contains(column);
        public void RequireColumns(string table, params string[] columns)
        {
            if (!HasTable(table)) throw new InvalidDataException($"现场数据库缺少 {table} 表：{FilePath}");
            string[] missing = columns.Where(column => !HasColumn(table, column)).ToArray();
            if (missing.Length > 0) throw new InvalidDataException($"{table} 缺少必要字段：{string.Join("、", missing)}。未修改数据库。");
        }

        public SqlSugarClient OpenClient()
        {
            var db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = ConnectionString, DbType = DbType.Sqlite, IsAutoCloseConnection = false,
            });
            try
            {
                db.Ado.ExecuteCommand("PRAGMA query_only=ON; PRAGMA busy_timeout=5000;");
                return db;
            }
            catch { db.Dispose(); throw; }
        }

        // NULL projections make optional fields from newer application versions unavailable without altering the source.
        public string SelectSql<T>() where T : class, new()
        {
            string table = typeof(T).GetCustomAttribute<SugarTable>()?.TableName ?? typeof(T).Name;
            if (!HasTable(table)) throw new InvalidDataException($"现场数据库未包含 {table} 表。");
            var columns = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.CanWrite && property.GetIndexParameters().Length == 0)
                .Select(property => (Property: property, Column: property.GetCustomAttribute<SugarColumn>()))
                .Where(item => item.Column?.IsIgnore != true)
                .Select(item => string.IsNullOrEmpty(item.Column?.ColumnName) ? item.Property.Name : item.Column.ColumnName)
                .Select(column => HasColumn(table, column) ? Quote(column) : $"NULL AS {Quote(column)}");
            return $"SELECT {string.Join(",", columns)} FROM {Quote(table)}";
        }

        public ISugarQueryable<T> Query<T>(SqlSugarClient db) where T : class, new() => db.SqlQueryable<T>(SelectSql<T>());

        public string? ReadPayload(string table, string idColumn, int id, string legacyColumn, string gzipColumn)
        {
            RequireColumns(table, idColumn);
            string legacy = HasColumn(table, legacyColumn) ? Quote(legacyColumn) : "NULL";
            string gzip = HasColumn(table, gzipColumn) ? Quote(gzipColumn) : "NULL";
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {gzip}, {legacy} FROM {Quote(table)} WHERE {Quote(idColumn)}=@id LIMIT 1";
            command.Parameters.AddWithValue("@id", id);
            using var reader = command.ExecuteReader();
            if (!reader.Read()) return null;
            if (!reader.IsDBNull(0) && reader.GetValue(0) is byte[] bytes && bytes.Length > 0)
            {
                using var input = new MemoryStream(bytes, writable: false);
                using var stream = new GZipStream(input, CompressionMode.Decompress);
                using var text = new StreamReader(stream, Encoding.UTF8);
                return text.ReadToEnd();
            }
            return reader.IsDBNull(1) ? null : reader.GetString(1);
        }

        private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
