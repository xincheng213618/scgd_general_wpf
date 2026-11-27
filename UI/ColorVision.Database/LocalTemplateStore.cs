using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;

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
                        sort_order INTEGER NOT NULL DEFAULT 0,
                        created_utc TEXT NULL
                    );
                    CREATE INDEX IF NOT EXISTS ix_local_templates_kind ON local_templates(kind, sort_order, id);
                    """;
                command.ExecuteNonQuery();
                command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('local_templates') WHERE name='created_utc'";
                if (Convert.ToInt32(command.ExecuteScalar()) == 0)
                {
                    // Recheck under a write transaction when two owners open a legacy database together.
                    using var transaction = connection.BeginTransaction();
                    command.Transaction = transaction;
                    if (Convert.ToInt32(command.ExecuteScalar()) == 0)
                    {
                        command.CommandText = "ALTER TABLE local_templates ADD COLUMN created_utc TEXT NULL";
                        command.ExecuteNonQuery();
                    }
                    transaction.Commit();
                }
                return connection;
            }
            catch { connection.Dispose(); throw; }
        }

        public IReadOnlyList<LocalTemplateDocument> List(string kind)
        {
            using var connection = Open();
            return List(connection, kind, null);
        }

        private static List<LocalTemplateDocument> List(SqliteConnection connection, string kind, SqliteTransaction? transaction)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT id,name,schema_version,payload,created_utc FROM local_templates WHERE kind=$kind ORDER BY sort_order,id";
            command.Parameters.AddWithValue("$kind", kind);
            using var reader = command.ExecuteReader();
            var result = new List<LocalTemplateDocument>();
            while (reader.Read()) result.Add(ReadDocument(reader));
            return result;
        }

        public LocalTemplateDocument Read(string kind, int id) => Find(kind, id) ?? throw new InvalidDataException($"找不到本地模板：{kind}/{id}");

        public LocalTemplateDocument ReadByName(string kind, string name)
        {
            using var connection = Open();
            return ReadByName(connection, null, kind, name);
        }

        private static LocalTemplateDocument ReadByName(SqliteConnection connection, SqliteTransaction? transaction, string kind, string name)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT id,name,schema_version,payload,created_utc FROM local_templates WHERE kind=$kind AND name=$name LIMIT 2";
            command.Parameters.AddWithValue("$kind", kind);
            command.Parameters.AddWithValue("$name", name);
            using var reader = command.ExecuteReader();
            if (!reader.Read()) throw new InvalidOperationException($"找不到本地模板：{kind}/{name}");
            var document = ReadDocument(reader);
            if (reader.Read()) throw new InvalidOperationException($"本地模板名称不唯一：{kind}/{name}");
            return document;
        }

        public void UpdateByName(string kind, string name, Func<LocalTemplateDocument, string> updatePayload)
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction(deferred: false);
            var document = ReadByName(connection, transaction, kind, name);
            string payload = updatePayload(document);
            Save(connection, transaction, kind, document.Id, document.Name, document.SchemaVersion, payload);
            transaction.Commit();
        }

        /// <summary>Returns null only when the document is missing; storage failures still throw.</summary>
        public LocalTemplateDocument? Find(string kind, int id)
        {
            using var connection = Open();
            return Find(connection, kind, id, null);
        }

        private static LocalTemplateDocument? Find(SqliteConnection connection, string kind, int id, SqliteTransaction? transaction)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT id,name,schema_version,payload,created_utc FROM local_templates WHERE kind=$kind AND id=$id";
            command.Parameters.AddWithValue("$kind", kind);
            command.Parameters.AddWithValue("$id", id);
            using var reader = command.ExecuteReader();
            return reader.Read() ? ReadDocument(reader) : null;
        }

        private static LocalTemplateDocument ReadDocument(SqliteDataReader reader) => new(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3))
        {
            CreatedUtc = !reader.IsDBNull(4) && DateTime.TryParse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date) ? date : null
        };

        public int Save(string kind, int? id, string name, int schemaVersion, string payload)
        {
            using var connection = Open();
            return Save(connection, null, kind, id, name, schemaVersion, payload);
        }

        private static int Save(SqliteConnection connection, SqliteTransaction? transaction, string kind,
            int? id, string name, int schemaVersion, string payload)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = id.HasValue
                ? "UPDATE local_templates SET name=$name,schema_version=$version,payload=$payload WHERE kind=$kind AND id=$id RETURNING id"
                : "INSERT INTO local_templates(kind,name,schema_version,payload,sort_order,created_utc) VALUES($kind,$name,$version,$payload,COALESCE((SELECT MAX(sort_order)+1 FROM local_templates WHERE kind=$kind),0),$created) RETURNING id";
            command.Parameters.AddWithValue("$kind", kind);
            if (id.HasValue) command.Parameters.AddWithValue("$id", id.Value);
            else command.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$name", name);
            command.Parameters.AddWithValue("$version", schemaVersion);
            command.Parameters.AddWithValue("$payload", payload);
            return checked(Convert.ToInt32(command.ExecuteScalar() ?? throw new InvalidDataException("模板已被删除，无法保存。")));
        }

        /// <summary>Inserts a document under an existing parent and checks competing documents in the same transaction.</summary>
        public int TryInsertChild(string kind, int parentId, string name, int schemaVersion, string payload,
            Func<LocalTemplateDocument, bool> matchesParent, Func<LocalTemplateDocument, bool> conflicts, Func<bool> isCurrent)
        {
            if (!isCurrent()) return 0;
            using var connection = Open();
            using var transaction = connection.BeginTransaction(deferred: false);
            LocalTemplateDocument? parent = Find(connection, kind, parentId, transaction);
            if (parent == null || !matchesParent(parent)) return 0;
            foreach (var document in List(connection, kind, transaction))
                if (document.Id != parentId && conflicts(document)) return 0;
            if (!isCurrent()) return 0;

            int id = Save(connection, transaction, kind, null, name, schemaVersion, payload);
            if (!isCurrent()) return 0;
            transaction.Commit();
            return id;
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
            return TryUpdate(connection, null, kind, expected, name, schemaVersion, payload);
        }

        private static bool TryUpdate(SqliteConnection connection, SqliteTransaction? transaction, string kind,
            LocalTemplateDocument expected, string name, int schemaVersion, string payload)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
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

        /// <summary>Updates payloads together. A missing document or a rejected payload rolls back the batch.</summary>
        public bool TryUpdateBatch(string kind, IReadOnlyList<int> ids, Func<LocalTemplateDocument, string?> updatePayload, Func<bool> isCurrent)
        {
            if (!isCurrent()) return false;
            using var connection = Open();
            using var transaction = connection.BeginTransaction(deferred: false);
            foreach (int id in ids)
            {
                var document = Find(connection, kind, id, transaction);
                if (document == null) return false;
                string? payload = updatePayload(document);
                if (payload == null) return false;
                if (!string.Equals(payload, document.Payload, StringComparison.Ordinal)
                    && !TryUpdate(connection, transaction, kind, document, document.Name, document.SchemaVersion, payload)) return false;
            }
            if (!isCurrent()) return false;
            transaction.Commit();
            return true;
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

        /// <summary>Deletes a matching document and optional related documents of the same kind in one transaction.</summary>
        public bool TryDelete(string kind, int id, Func<LocalTemplateDocument, bool> matches,
            Func<LocalTemplateDocument, bool>? isRelated, Func<bool> isCurrent)
        {
            if (!isCurrent()) return false;
            using var connection = Open();
            // Reserve the write lock before checking identity or selecting related rows.
            using var transaction = connection.BeginTransaction(deferred: false);
            LocalTemplateDocument? document = Find(connection, kind, id, transaction);
            if (document == null || !matches(document)) return false;

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM local_templates WHERE kind=$kind AND id=$id";
            command.Parameters.AddWithValue("$kind", kind);
            var documentId = command.Parameters.AddWithValue("$id", id);
            if (isRelated != null)
            {
                foreach (var candidate in List(connection, kind, transaction))
                {
                    if (candidate.Id == id || !isRelated(candidate)) continue;
                    documentId.Value = candidate.Id;
                    if (command.ExecuteNonQuery() != 1) return false;
                }
            }
            documentId.Value = id;
            if (command.ExecuteNonQuery() != 1 || !isCurrent()) return false;
            transaction.Commit();
            return true;
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

    public sealed record LocalTemplateDocument(int Id, string Name, int SchemaVersion, string Payload)
    {
        public DateTime? CreatedUtc { get; init; }
    }
}
