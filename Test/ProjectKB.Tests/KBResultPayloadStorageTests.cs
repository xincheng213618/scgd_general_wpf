using ColorVision.Engine.Templates.Jsons.KB;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using SqlSugar;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using Xunit;

namespace ProjectKB.Tests;

public class KBResultPayloadStorageTests
{
    [Fact]
    public void ResultAndRecipePayloadsRoundTripWithoutBeingSelectedByListQueries()
    {
        Assert.Equal("ResultPayloadGzip", KBResultPayloadStorage.ResultPayloadColumnName);
        Assert.Equal("RecipeSnapshotGzip", KBResultPayloadStorage.RecipeSnapshotColumnName);

        string databasePath = CreateDatabasePath();
        try
        {
            using SqlSugarClient db = CreateDbClient(databasePath);
            db.CodeFirst.InitTables<KBItemMaster>();
            KBResultPayloadStorage.EnsureSchema(db);

            var result = new KBItemMaster
            {
                Model = "MODEL-A",
                SN = "SN-1",
                Items = new ObservableCollection<KBItem>
                {
                    new() { Name = "A", Lv = 12.5, Lc = 0.18, Result = false },
                },
                RecipeSnapshot = KBRecipeSnapshot.Capture("MODEL-A", new KBRecipeConfig
                {
                    MinKeyLv = 10,
                    MaxKeyLv = 20,
                    MinKeyLc = 5,
                    MaxKeyLc = 30,
                }),
                IsResultPayloadLoaded = true,
            };
            result.Id = db.Insertable(result).ExecuteReturnIdentity();
            KBResultPayloadStorage.SaveResult(db, result);

            string listSql = db.Queryable<KBItemMaster>().ToSqlString();
            Assert.Contains("NULL AS", listSql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(KBResultPayloadStorage.ResultPayloadColumnName, listSql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(KBResultPayloadStorage.RecipeSnapshotColumnName, listSql, StringComparison.OrdinalIgnoreCase);

            KBItemMaster metadata = db.Queryable<KBItemMaster>().Where(item => item.Id == result.Id).Single();
            Assert.Empty(metadata.Items);
            Assert.Null(metadata.RecipeSnapshot);
            Assert.False(metadata.IsResultPayloadLoaded);

            KBResultPayloadStorage.LoadResult(db, metadata);

            KBItem restoredItem = Assert.Single(metadata.Items);
            Assert.Equal("A", restoredItem.Name);
            Assert.Equal(12.5, restoredItem.Lv);
            Assert.NotNull(metadata.RecipeSnapshot);
            Assert.Equal(KBRecipeSnapshotOrigin.CapturedAtRun, metadata.RecipeSnapshot!.Origin);
            Assert.Equal(10, metadata.RecipeSnapshot.Recipe.MinKeyLv);

            DataTable payloads = db.Ado.GetDataTable(
                $"SELECT length(\"{KBResultPayloadStorage.ResultPayloadColumnName}\") AS ResultBytes, " +
                $"length(\"{KBResultPayloadStorage.RecipeSnapshotColumnName}\") AS RecipeBytes, " +
                $"\"{KBResultPayloadStorage.LegacyItemsColumnName}\" AS Legacy " +
                $"FROM \"{KBResultPayloadStorage.TableName}\" WHERE \"Id\" = {result.Id};");
            Assert.True(Convert.ToInt64(payloads.Rows[0]["ResultBytes"]) > 0);
            Assert.True(Convert.ToInt64(payloads.Rows[0]["RecipeBytes"]) > 0);
            Assert.Equal(string.Empty, Convert.ToString(payloads.Rows[0]["Legacy"]));
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public void LegacyMigrationCompressesItemsRebuildsMatchingRecipeAndIsIdempotent()
    {
        string databasePath = CreateDatabasePath();
        try
        {
            int matchingId;
            int unknownId;
            using (SqlSugarClient db = CreateDbClient(databasePath))
            {
                db.CodeFirst.InitTables<KBItemMaster>();
                string itemsJson = JsonConvert.SerializeObject(new[]
                {
                    new KBItem { Name = "A", Lv = 8.5, Lc = 0.12, Result = true },
                });
                matchingId = db.Insertable(new KBItemMaster
                {
                    Model = " MODEL-A ",
                    SN = "SN-MATCH",
                    LegacyItemsJson = itemsJson,
                }).ExecuteReturnIdentity();
                unknownId = db.Insertable(new KBItemMaster
                {
                    Model = "UNKNOWN",
                    SN = "SN-UNKNOWN",
                    LegacyItemsJson = itemsJson,
                }).ExecuteReturnIdentity();
            }

            var recipes = new Dictionary<string, KBRecipeConfig>(StringComparer.OrdinalIgnoreCase)
            {
                [" model-a "] = new KBRecipeConfig { MinKeyLv = 7, MaxKeyLv = 15 },
            };

            LegacyKbResultMigrationReport first = LegacyKbResultMigration.Execute(databasePath, recipes);

            Assert.Equal(2, first.ItemsRowsMigrated);
            Assert.Equal(1, first.RecipeSnapshotsRebuilt);
            Assert.Equal(1, first.RecipeSnapshotsUnavailable);
            Assert.Equal("ok", first.IntegrityCheck, ignoreCase: true);

            using (SqlSugarClient db = CreateDbClient(databasePath))
            {
                KBResultPayloadStorage.EnsureSchema(db);
                KBItemMaster matching = db.Queryable<KBItemMaster>().Where(item => item.Id == matchingId).Single();
                KBResultPayloadStorage.LoadResult(db, matching);
                Assert.Single(matching.Items);
                Assert.NotNull(matching.RecipeSnapshot);
                Assert.Equal(KBRecipeSnapshotOrigin.RebuiltFromCurrentRecipe, matching.RecipeSnapshot!.Origin);
                Assert.Equal(7, matching.RecipeSnapshot.Recipe.MinKeyLv);

                KBItemMaster unknown = db.Queryable<KBItemMaster>().Where(item => item.Id == unknownId).Single();
                KBResultPayloadStorage.LoadResult(db, unknown);
                Assert.Single(unknown.Items);
                Assert.Null(unknown.RecipeSnapshot);

                DataTable legacy = db.Ado.GetDataTable(
                    $"SELECT COUNT(*) AS Count FROM \"{KBResultPayloadStorage.TableName}\" " +
                    $"WHERE \"{KBResultPayloadStorage.LegacyItemsColumnName}\" <> '';" );
                Assert.Equal(0L, Convert.ToInt64(legacy.Rows[0]["Count"]));
            }

            recipes["UNKNOWN"] = new KBRecipeConfig { MinKeyLv = 3, MaxKeyLv = 12 };
            LegacyKbResultMigrationReport second = LegacyKbResultMigration.Execute(databasePath, recipes);
            Assert.Equal(0, second.ItemsRowsMigrated);
            Assert.Equal(1, second.RecipeSnapshotsRebuilt);
            Assert.Equal(0, second.RecipeSnapshotsUnavailable);

            using (SqlSugarClient db = CreateDbClient(databasePath))
            {
                KBItemMaster unknown = db.Queryable<KBItemMaster>().Where(item => item.Id == unknownId).Single();
                KBResultPayloadStorage.LoadResult(db, unknown);
                Assert.Equal(KBRecipeSnapshotOrigin.RebuiltFromCurrentRecipe, unknown.RecipeSnapshot!.Origin);
                Assert.Equal(3, unknown.RecipeSnapshot.Recipe.MinKeyLv);
            }

            LegacyKbResultMigrationReport third = LegacyKbResultMigration.Execute(databasePath, recipes);
            Assert.Equal(0, third.ItemsRowsMigrated);
            Assert.Equal(0, third.RecipeSnapshotsRebuilt);
            Assert.Equal(0, third.RecipeSnapshotsUnavailable);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    private static string CreateDatabasePath()
    {
        return Path.Combine(Path.GetTempPath(), $"ProjectKB-Payload-{Guid.NewGuid():N}.db");
    }

    private static SqlSugarClient CreateDbClient(string databasePath)
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={databasePath};Pooling=False;Default Timeout=5",
            DbType = SqlSugar.DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute,
        });
    }

    private static void DeleteDatabase(string databasePath)
    {
        SqliteConnection.ClearAllPools();
        foreach (string suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            string path = databasePath + suffix;
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
