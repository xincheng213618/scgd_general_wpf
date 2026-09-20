using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;

namespace ColorVision.Database
{
    /// <summary>Local document storage shared by template/configuration owners. Payload schemas belong to their owners.</summary>
    public sealed class LocalTemplateStore
    {
        public static string DefaultDatabasePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision", "Config", "ColorVision.Local.db");
        public string DatabasePath { get; }

        public LocalTemplateStore(string databasePath) => DatabasePath = Path.GetFullPath(databasePath);

        private SqliteConnection Open()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
            var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false }.ToString());
            try
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    PRAGMA busy_timeout=5000;
                    PRAGMA journal_mode=WAL;
                    CREATE TABLE IF NOT EXISTS local_templates (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        kind TEXT NOT NULL,
                        name TEXT NOT NULL,
                        schema_version INTEGER NOT NULL,
                        payload TEXT NOT NULL,
                        sort_order INTEGER NOT NULL DEFAULT 0
                    );
                    CREATE INDEX IF NOT EXISTS ix_local_templates_kind ON local_templates(kind, sort_order, id);
                    """;
                command.ExecuteNonQuery();
                return connection;
            }
            catch { connection.Dispose(); throw; }
        }

        public IReadOnlyList<LocalTemplateDocument> List(string kind)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT id,name,schema_version,payload FROM local_templates WHERE kind=$kind ORDER BY sort_order,id";
            command.Parameters.AddWithValue("$kind", kind);
            using var reader = command.ExecuteReader();
            var result = new List<LocalTemplateDocument>();
            while (reader.Read()) result.Add(new(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3)));
            return result;
        }

        public LocalTemplateDocument Read(string kind, int id)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT id,name,schema_version,payload FROM local_templates WHERE kind=$kind AND id=$id";
            command.Parameters.AddWithValue("$kind", kind);
            command.Parameters.AddWithValue("$id", id);
            using var reader = command.ExecuteReader();
            if (!reader.Read()) throw new InvalidDataException($"找不到本地模板：{kind}/{id}");
            return new(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3));
        }

        public int Save(string kind, int? id, string name, int schemaVersion, string payload)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = id.HasValue
                ? "UPDATE local_templates SET name=$name,schema_version=$version,payload=$payload WHERE kind=$kind AND id=$id RETURNING id"
                : "INSERT INTO local_templates(kind,name,schema_version,payload,sort_order) VALUES($kind,$name,$version,$payload,COALESCE((SELECT MAX(sort_order)+1 FROM local_templates WHERE kind=$kind),0)) RETURNING id";
            command.Parameters.AddWithValue("$kind", kind);
            if (id.HasValue) command.Parameters.AddWithValue("$id", id.Value);
            command.Parameters.AddWithValue("$name", name);
            command.Parameters.AddWithValue("$version", schemaVersion);
            command.Parameters.AddWithValue("$payload", payload);
            return checked(Convert.ToInt32(command.ExecuteScalar() ?? throw new InvalidDataException("模板已被删除，无法保存。")));
        }

        public void Delete(string kind, int id)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM local_templates WHERE kind=$kind AND id=$id";
            command.Parameters.AddWithValue("$kind", kind);
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        /// <summary>Atomically replaces the document only if it still matches the caller's read.</summary>
        public bool TryUpdate(string kind, LocalTemplateDocument expected, string name, int schemaVersion, string payload)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE local_templates SET name=$name,schema_version=$version,payload=$payload
                WHERE kind=$kind AND id=$id AND name=$previousName
                    AND schema_version=$previousVersion AND payload=$previousPayload
                """;
            command.Parameters.AddWithValue("$kind", kind);
            command.Parameters.AddWithValue("$id", expected.Id);
            command.Parameters.AddWithValue("$name", name);
            command.Parameters.AddWithValue("$version", schemaVersion);
            command.Parameters.AddWithValue("$payload", payload);
            command.Parameters.AddWithValue("$previousName", expected.Name);
            command.Parameters.AddWithValue("$previousVersion", expected.SchemaVersion);
            command.Parameters.AddWithValue("$previousPayload", expected.Payload);
            return command.ExecuteNonQuery() == 1;
        }

        public bool TryDelete(string kind, LocalTemplateDocument expected)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM local_templates WHERE kind=$kind AND id=$id AND schema_version=$version AND payload=$payload";
            command.Parameters.AddWithValue("$kind", kind);
            command.Parameters.AddWithValue("$id", expected.Id);
            command.Parameters.AddWithValue("$version", expected.SchemaVersion);
            command.Parameters.AddWithValue("$payload", expected.Payload);
            return command.ExecuteNonQuery() == 1;
        }

        public void SwapOrder(string kind, int first, int second)
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT id,sort_order FROM local_templates WHERE kind=$kind AND id IN ($first,$second)";
            command.Parameters.AddWithValue("$kind", kind);
            command.Parameters.AddWithValue("$first", first);
            command.Parameters.AddWithValue("$second", second);
            var orders = new Dictionary<int, int>();
            using (var reader = command.ExecuteReader())
                while (reader.Read()) orders.Add(reader.GetInt32(0), reader.GetInt32(1));
            if (orders.Count != 2) throw new InvalidDataException("待排序的模板不存在。");
            command.CommandText = "UPDATE local_templates SET sort_order=CASE id WHEN $first THEN $secondOrder ELSE $firstOrder END WHERE kind=$kind AND id IN ($first,$second)";
            command.Parameters.AddWithValue("$firstOrder", orders[first]);
            command.Parameters.AddWithValue("$secondOrder", orders[second]);
            command.ExecuteNonQuery();
            transaction.Commit();
        }
    }

    public sealed record LocalTemplateDocument(int Id, string Name, int SchemaVersion, string Payload);
}
