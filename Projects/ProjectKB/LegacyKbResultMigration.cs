using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.IO;

namespace ProjectKB
{
    /// <summary>
    /// One-time onsite migration from the legacy ItemsJson TEXT column to the
    /// lazy GZip payload. Legacy Recipes were not stored, so matching current
    /// Recipes are copied with an explicit reconstructed origin marker.
    /// </summary>
    internal static class LegacyKbResultMigration
    {
        private const int BatchSize = 500;

        public static LegacyKbResultMigrationReport Execute(
            string databasePath,
            IReadOnlyDictionary<string, KBRecipeConfig> currentRecipes)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
            ArgumentNullException.ThrowIfNull(currentRecipes);
            if (!File.Exists(databasePath))
                throw new FileNotFoundException("KB SQLite 数据库文件不存在。", databasePath);

            long beforeBytes = new FileInfo(databasePath).Length;
            int itemsMigrated;
            int residualItemsCleared;
            int recipeSnapshotsRebuilt;
            int recipeSnapshotsUnavailable;
            string quickCheck;

            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false,
                DefaultTimeout = 30,
            }.ToString();

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                ExecuteNonQuery(connection, "PRAGMA busy_timeout = 30000;");
                EnsureColumn(connection, KBResultPayloadStorage.ResultPayloadColumnName, "BLOB");
                EnsureColumn(connection, KBResultPayloadStorage.RecipeSnapshotColumnName, "BLOB");

                (itemsMigrated, residualItemsCleared) = MigrateItems(connection);
                recipeSnapshotsRebuilt = RebuildRecipeSnapshots(connection, currentRecipes);
                recipeSnapshotsUnavailable = Convert.ToInt32(ExecuteScalarInt64(
                    connection,
                    $"SELECT COUNT(*) FROM \"{KBResultPayloadStorage.TableName}\" " +
                    $"WHERE \"{KBResultPayloadStorage.RecipeSnapshotColumnName}\" IS NULL " +
                    $"OR length(\"{KBResultPayloadStorage.RecipeSnapshotColumnName}\") = 0;"));

                CheckpointWal(connection, "迁移后");
                try
                {
                    ExecuteNonQuery(connection, "VACUUM;");
                    CheckpointWal(connection, "VACUUM 后");
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "旧 ItemsJson 已迁移并清空，但 SQLite 空间释放失败。请停止 KB 测试后重新执行迁移。",
                        ex);
                }

                quickCheck = ExecuteScalarString(connection, "PRAGMA quick_check;");
                if (!string.Equals(quickCheck, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"迁移后的 SQLite 完整性检查未通过：{quickCheck}");
            }

            long afterBytes = new FileInfo(databasePath).Length;
            return new LegacyKbResultMigrationReport(
                itemsMigrated,
                residualItemsCleared,
                recipeSnapshotsRebuilt,
                recipeSnapshotsUnavailable,
                beforeBytes,
                afterBytes,
                quickCheck);
        }

        private static (int Migrated, int ResidualCleared) MigrateItems(SqliteConnection connection)
        {
            if (!ColumnExists(connection, KBResultPayloadStorage.LegacyItemsColumnName))
                return (0, 0);

            int migrated = 0;
            while (true)
            {
                List<LegacyItemsRow> rows = ReadPendingItemsBatch(connection);
                if (rows.Count == 0)
                    break;

                var compressedRows = new List<CompressedItemsRow>(rows.Count);
                foreach (LegacyItemsRow row in rows)
                {
                    ObservableCollection<KBItem>? items;
                    try
                    {
                        items = JsonConvert.DeserializeObject<ObservableCollection<KBItem>>(row.Json);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException($"KBItemMaster Id={row.Id} 的旧 ItemsJson 无法反序列化，旧字段已保留。", ex);
                    }
                    if (items == null)
                        throw new InvalidDataException($"KBItemMaster Id={row.Id} 的旧 ItemsJson 为空对象，旧字段已保留。");

                    string resultPayloadJson = KBResultPayloadStorage.SerializeResultPayload(items);
                    byte[] compressed = KBResultPayloadStorage.Compress(resultPayloadJson)
                        ?? throw new InvalidDataException($"KBItemMaster Id={row.Id} 的旧 ItemsJson 无法压缩。");
                    VerifyCompressedPayload(row.Id, resultPayloadJson, compressed);
                    compressedRows.Add(new CompressedItemsRow(row.Id, row.Json, compressed));
                }

                using SqliteTransaction transaction = connection.BeginTransaction();
                using SqliteCommand command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText =
                    $"UPDATE \"{KBResultPayloadStorage.TableName}\" " +
                    $"SET \"{KBResultPayloadStorage.ResultPayloadColumnName}\" = $payload, " +
                    $"\"{KBResultPayloadStorage.LegacyItemsColumnName}\" = '' " +
                    $"WHERE \"Id\" = $id AND \"{KBResultPayloadStorage.ResultPayloadColumnName}\" IS NULL " +
                    $"AND \"{KBResultPayloadStorage.LegacyItemsColumnName}\" = $legacy " +
                    $"AND \"{KBResultPayloadStorage.LegacyItemsColumnName}\" <> '';";
                SqliteParameter payloadParameter = command.Parameters.Add("$payload", SqliteType.Blob);
                SqliteParameter idParameter = command.Parameters.Add("$id", SqliteType.Integer);
                SqliteParameter legacyParameter = command.Parameters.Add("$legacy", SqliteType.Text);

                foreach (CompressedItemsRow row in compressedRows)
                {
                    payloadParameter.Value = row.Payload;
                    idParameter.Value = row.Id;
                    legacyParameter.Value = row.Json;
                    if (command.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException($"KBItemMaster Id={row.Id} 在迁移期间发生变化，本批次未提交。");
                    migrated++;
                }
                transaction.Commit();
            }

            long pending = ExecuteScalarInt64(
                connection,
                $"SELECT COUNT(*) FROM \"{KBResultPayloadStorage.TableName}\" " +
                $"WHERE \"{KBResultPayloadStorage.LegacyItemsColumnName}\" <> '' " +
                $"AND \"{KBResultPayloadStorage.ResultPayloadColumnName}\" IS NULL;");
            if (pending != 0)
                throw new InvalidDataException($"KBItemMaster 仍有 {pending:N0} 条旧 ItemsJson 未迁移。");

            int residualCleared = ClearVerifiedResidualItems(connection);
            long remainingLegacy = ExecuteScalarInt64(
                connection,
                $"SELECT COUNT(*) FROM \"{KBResultPayloadStorage.TableName}\" " +
                $"WHERE \"{KBResultPayloadStorage.LegacyItemsColumnName}\" <> '';" );
            if (remainingLegacy != 0)
                throw new InvalidDataException($"KBItemMaster 仍有 {remainingLegacy:N0} 条旧 ItemsJson 未清空。");

            return (migrated, residualCleared);
        }

        private static int ClearVerifiedResidualItems(SqliteConnection connection)
        {
            int cleared = 0;
            while (true)
            {
                List<ResidualItemsRow> rows = ReadResidualItemsBatch(connection);
                if (rows.Count == 0)
                    return cleared;

                foreach (ResidualItemsRow row in rows)
                {
                    if (row.Payload == null || row.Payload.Length == 0)
                        throw new InvalidDataException($"KBItemMaster Id={row.Id} 已有压缩字段为空，旧 ItemsJson 已保留。");

                    try
                    {
                        VerifyPayloadMatchesLegacy(row.Id, row.Json, row.Payload);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException($"KBItemMaster Id={row.Id} 的压缩字段损坏，旧 ItemsJson 已保留。", ex);
                    }
                }

                using SqliteTransaction transaction = connection.BeginTransaction();
                using SqliteCommand command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText =
                    $"UPDATE \"{KBResultPayloadStorage.TableName}\" " +
                    $"SET \"{KBResultPayloadStorage.LegacyItemsColumnName}\" = '' " +
                    $"WHERE \"Id\" = $id AND \"{KBResultPayloadStorage.LegacyItemsColumnName}\" = $legacy " +
                    $"AND \"{KBResultPayloadStorage.ResultPayloadColumnName}\" = $payload;";
                SqliteParameter idParameter = command.Parameters.Add("$id", SqliteType.Integer);
                SqliteParameter legacyParameter = command.Parameters.Add("$legacy", SqliteType.Text);
                SqliteParameter payloadParameter = command.Parameters.Add("$payload", SqliteType.Blob);

                foreach (ResidualItemsRow row in rows)
                {
                    idParameter.Value = row.Id;
                    legacyParameter.Value = row.Json;
                    payloadParameter.Value = row.Payload!;
                    if (command.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException($"KBItemMaster Id={row.Id} 在残留清理期间发生变化，本批次未提交。");
                    cleared++;
                }
                transaction.Commit();
            }
        }

        private static int RebuildRecipeSnapshots(
            SqliteConnection connection,
            IReadOnlyDictionary<string, KBRecipeConfig> currentRecipes)
        {
            var recipes = new Dictionary<string, KBRecipeConfig>(StringComparer.OrdinalIgnoreCase);
            foreach ((string name, KBRecipeConfig recipe) in currentRecipes)
            {
                string normalizedName = name.Trim();
                if (normalizedName.Length > 0 && recipe != null)
                    recipes[normalizedName] = recipe;
            }
            if (recipes.Count == 0)
                return 0;

            DateTime snapshotTime = DateTime.Now;
            List<string> models = ReadModelsWithoutRecipeSnapshot(connection);
            int rebuilt = 0;
            foreach (string model in models)
            {
                string recipeName = model.Trim();
                if (!recipes.TryGetValue(recipeName, out KBRecipeConfig? recipe))
                    continue;

                KBRecipeSnapshot snapshot = KBRecipeSnapshot.Capture(
                    recipeName,
                    recipe,
                    KBRecipeSnapshotOrigin.RebuiltFromCurrentRecipe,
                    snapshotTime);
                byte[] payload = KBResultPayloadStorage.Compress(JsonConvert.SerializeObject(snapshot))
                    ?? throw new InvalidDataException($"Recipe {model} 的快照无法压缩。");

                using SqliteCommand command = connection.CreateCommand();
                command.CommandText =
                    $"UPDATE \"{KBResultPayloadStorage.TableName}\" " +
                    $"SET \"{KBResultPayloadStorage.RecipeSnapshotColumnName}\" = $snapshot " +
                    $"WHERE \"Model\" = $model AND (\"{KBResultPayloadStorage.RecipeSnapshotColumnName}\" IS NULL " +
                    $"OR length(\"{KBResultPayloadStorage.RecipeSnapshotColumnName}\") = 0);";
                command.Parameters.Add("$snapshot", SqliteType.Blob).Value = payload;
                command.Parameters.AddWithValue("$model", model);
                rebuilt += command.ExecuteNonQuery();
            }
            return rebuilt;
        }

        private static List<LegacyItemsRow> ReadPendingItemsBatch(SqliteConnection connection)
        {
            var rows = new List<LegacyItemsRow>(BatchSize);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                $"SELECT \"Id\", \"{KBResultPayloadStorage.LegacyItemsColumnName}\" " +
                $"FROM \"{KBResultPayloadStorage.TableName}\" " +
                $"WHERE \"{KBResultPayloadStorage.LegacyItemsColumnName}\" <> '' " +
                $"AND \"{KBResultPayloadStorage.ResultPayloadColumnName}\" IS NULL " +
                $"ORDER BY \"Id\" LIMIT {BatchSize};";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
                rows.Add(new LegacyItemsRow(reader.GetInt32(0), reader.GetString(1)));
            return rows;
        }

        private static List<ResidualItemsRow> ReadResidualItemsBatch(SqliteConnection connection)
        {
            var rows = new List<ResidualItemsRow>(BatchSize);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                $"SELECT \"Id\", \"{KBResultPayloadStorage.LegacyItemsColumnName}\", " +
                $"\"{KBResultPayloadStorage.ResultPayloadColumnName}\" FROM \"{KBResultPayloadStorage.TableName}\" " +
                $"WHERE \"{KBResultPayloadStorage.LegacyItemsColumnName}\" <> '' " +
                $"AND \"{KBResultPayloadStorage.ResultPayloadColumnName}\" IS NOT NULL " +
                $"ORDER BY \"Id\" LIMIT {BatchSize};";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                byte[]? payload = reader.IsDBNull(2) ? null : reader.GetFieldValue<byte[]>(2);
                rows.Add(new ResidualItemsRow(reader.GetInt32(0), reader.GetString(1), payload));
            }
            return rows;
        }

        private static List<string> ReadModelsWithoutRecipeSnapshot(SqliteConnection connection)
        {
            var models = new List<string>();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                $"SELECT DISTINCT \"Model\" FROM \"{KBResultPayloadStorage.TableName}\" " +
                $"WHERE \"Model\" IS NOT NULL AND TRIM(\"Model\") <> '' " +
                $"AND (\"{KBResultPayloadStorage.RecipeSnapshotColumnName}\" IS NULL " +
                $"OR length(\"{KBResultPayloadStorage.RecipeSnapshotColumnName}\") = 0);";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
                models.Add(reader.GetString(0));
            return models;
        }

        private static void EnsureColumn(SqliteConnection connection, string columnName, string sqliteType)
        {
            if (!ColumnExists(connection, columnName))
            {
                ExecuteNonQuery(
                    connection,
                    $"ALTER TABLE \"{KBResultPayloadStorage.TableName}\" ADD COLUMN \"{columnName}\" {sqliteType} NULL;");
            }
        }

        private static void VerifyPayloadMatchesLegacy(int id, string legacyJson, byte[] payload)
        {
            ObservableCollection<KBItem> legacyItems = JsonConvert.DeserializeObject<ObservableCollection<KBItem>>(legacyJson)
                ?? throw new InvalidDataException($"KBItemMaster Id={id} 的旧 ItemsJson 为空对象。");
            string expectedPayloadJson = KBResultPayloadStorage.SerializeResultPayload(legacyItems);
            VerifyCompressedPayload(id, expectedPayloadJson, payload);
        }

        private static void VerifyCompressedPayload(int id, string expectedPayloadJson, byte[] payload)
        {
            string? restoredPayloadJson = KBResultPayloadStorage.Decompress(payload);
            if (!string.Equals(restoredPayloadJson, expectedPayloadJson, StringComparison.Ordinal))
                throw new InvalidDataException($"KBItemMaster Id={id} 的压缩回读校验失败，旧 ItemsJson 已保留。");
        }

        private static bool ColumnExists(SqliteConnection connection, string columnName)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{KBResultPayloadStorage.TableName}\");";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static int ExecuteNonQuery(SqliteConnection connection, string sql)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            return command.ExecuteNonQuery();
        }

        private static void CheckpointWal(SqliteConnection connection, string stage)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            using SqliteDataReader reader = command.ExecuteReader();
            if (reader.Read() && reader.GetInt32(0) != 0)
                throw new InvalidOperationException($"{stage}无法截断 SQLite WAL，请停止 KB 查询和测试后重试。");
        }

        private static long ExecuteScalarInt64(SqliteConnection connection, string sql)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt64(command.ExecuteScalar());
        }

        private static string ExecuteScalarString(SqliteConnection connection, string sql)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToString(command.ExecuteScalar()) ?? string.Empty;
        }

        private sealed record LegacyItemsRow(int Id, string Json);
        private sealed record CompressedItemsRow(int Id, string Json, byte[] Payload);
        private sealed record ResidualItemsRow(int Id, string Json, byte[]? Payload);
    }

    internal sealed record LegacyKbResultMigrationReport(
        int ItemsRowsMigrated,
        int ResidualItemsRowsCleared,
        int RecipeSnapshotsRebuilt,
        int RecipeSnapshotsUnavailable,
        long BeforeBytes,
        long AfterBytes,
        string IntegrityCheck);
}
