using Newtonsoft.Json;
using SqlSugar;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ProjectKB
{
    /// <summary>
    /// Stores large result details outside the normal SqlSugar entity mapping.
    /// Items and Recipe snapshots deliberately use independent columns so either
    /// payload can evolve or be inspected without loading the other one.
    /// </summary>
    internal static class KBResultPayloadStorage
    {
        private static readonly object DatabaseMaintenanceGate = new();

        internal const string TableName = "KBItemMaster";
        internal const string LegacyItemsColumnName = "ItemsJson";
        internal const string ResultPayloadColumnName = "ResultPayloadGzip";
        internal const string RecipeSnapshotColumnName = "RecipeSnapshotGzip";

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
            EnsureColumn(db, ResultPayloadColumnName, "BLOB");
            EnsureColumn(db, RecipeSnapshotColumnName, "BLOB");
        }

        public static void SaveResult(SqlSugarClient db, KBItemMaster item)
        {
            ArgumentNullException.ThrowIfNull(db);
            ArgumentNullException.ThrowIfNull(item);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(item.Id);

            string resultPayloadJson = SerializeResultPayload(item.Items);
            byte[]? resultPayload = Compress(resultPayloadJson);
            string? recipeSnapshotJson = item.RecipeSnapshot == null
                ? null
                : JsonConvert.SerializeObject(item.RecipeSnapshot);
            byte[]? recipeSnapshotPayload = Compress(recipeSnapshotJson);

            db.Ado.ExecuteCommand(
                $"UPDATE \"{TableName}\" SET \"{ResultPayloadColumnName}\" = @result, \"{RecipeSnapshotColumnName}\" = @recipe WHERE \"Id\" = @id;",
                new SugarParameter("@result", resultPayload ?? (object)DBNull.Value, System.Data.DbType.Binary),
                new SugarParameter("@recipe", recipeSnapshotPayload ?? (object)DBNull.Value, System.Data.DbType.Binary),
                new SugarParameter("@id", item.Id));
            item.IsResultPayloadLoaded = true;
        }

        public static void LoadResult(SqlSugarClient db, KBItemMaster item)
        {
            ArgumentNullException.ThrowIfNull(db);
            ArgumentNullException.ThrowIfNull(item);
            if (item.IsResultPayloadLoaded)
                return;

            if (item.Id <= 0)
            {
                item.IsResultPayloadLoaded = true;
                return;
            }

            DataTable table = db.Ado.GetDataTable(
                $"SELECT \"{ResultPayloadColumnName}\", \"{RecipeSnapshotColumnName}\", \"{LegacyItemsColumnName}\" " +
                $"FROM \"{TableName}\" WHERE \"Id\" = @id LIMIT 1;",
                new SugarParameter("@id", item.Id));
            if (table.Rows.Count == 0)
            {
                item.IsResultPayloadLoaded = true;
                return;
            }

            DataRow row = table.Rows[0];
            string? resultPayloadJson = Decompress(ReadBytes(row[ResultPayloadColumnName]));
            if (!string.IsNullOrEmpty(resultPayloadJson))
            {
                item.Items = DeserializeResultPayload(resultPayloadJson);
            }
            else
            {
                string? legacyItemsJson = ReadText(row[LegacyItemsColumnName]);
                item.Items = string.IsNullOrEmpty(legacyItemsJson)
                    ? new ObservableCollection<KBItem>()
                    : JsonConvert.DeserializeObject<ObservableCollection<KBItem>>(legacyItemsJson)
                        ?? new ObservableCollection<KBItem>();
            }

            string? recipeSnapshotJson = Decompress(ReadBytes(row[RecipeSnapshotColumnName]));
            item.RecipeSnapshot = string.IsNullOrWhiteSpace(recipeSnapshotJson)
                ? null
                : JsonConvert.DeserializeObject<KBRecipeSnapshot>(recipeSnapshotJson);
            item.IsResultPayloadLoaded = true;
        }

        public static KBRecipeSnapshot? LoadRecipeSnapshot(SqlSugarClient db, int id)
        {
            ArgumentNullException.ThrowIfNull(db);
            if (id <= 0)
                return null;

            DataTable table = db.Ado.GetDataTable(
                $"SELECT \"{RecipeSnapshotColumnName}\" FROM \"{TableName}\" WHERE \"Id\" = @id LIMIT 1;",
                new SugarParameter("@id", id));
            if (table.Rows.Count == 0)
                return null;

            string? json = Decompress(ReadBytes(table.Rows[0][RecipeSnapshotColumnName]));
            return string.IsNullOrWhiteSpace(json)
                ? null
                : JsonConvert.DeserializeObject<KBRecipeSnapshot>(json);
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

        internal static string SerializeResultPayload(IEnumerable<KBItem> items)
        {
            ArgumentNullException.ThrowIfNull(items);
            return JsonConvert.SerializeObject(new KBResultPayload
            {
                Items = new ObservableCollection<KBItem>(items),
            });
        }

        internal static ObservableCollection<KBItem> DeserializeResultPayload(string json)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(json);
            KBResultPayload payload = JsonConvert.DeserializeObject<KBResultPayload>(json)
                ?? throw new InvalidDataException("KB 结果载荷为空。");
            if (payload.SchemaVersion <= 0 || payload.SchemaVersion > KBResultPayload.CurrentSchemaVersion)
                throw new InvalidDataException($"不支持的 KB 结果载荷版本：{payload.SchemaVersion}。");
            return payload.Items ?? new ObservableCollection<KBItem>();
        }

        private static void EnsureColumn(SqlSugarClient db, string columnName, string sqliteType)
        {
            DataTable columns = db.Ado.GetDataTable($"PRAGMA table_info(\"{TableName}\");");
            bool exists = columns.Rows.Cast<DataRow>()
                .Any(row => string.Equals(Convert.ToString(row["name"]), columnName, StringComparison.OrdinalIgnoreCase));
            if (!exists)
                db.Ado.ExecuteCommand($"ALTER TABLE \"{TableName}\" ADD COLUMN \"{columnName}\" {sqliteType} NULL;");
        }

        private static byte[]? ReadBytes(object value)
        {
            return value == DBNull.Value ? null : value as byte[];
        }

        private static string? ReadText(object value)
        {
            return value == DBNull.Value ? null : Convert.ToString(value);
        }
    }

    internal sealed class KBResultPayload
    {
        internal const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public ObservableCollection<KBItem> Items { get; set; } = new();
    }
}
