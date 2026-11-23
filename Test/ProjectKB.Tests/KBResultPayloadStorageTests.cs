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
                ImageWidth = 9680,
                ImageHeight = 5460,
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
            Assert.Equal(9680, metadata.ImageWidth);
            Assert.Equal(5460, metadata.ImageHeight);

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

    [Fact]
    public void FullCleanupKeepsResultIdsMonotonicAndPreservesTodaysOpenSession()
    {
        string databasePath = CreateDatabasePath();
        try
        {
            using var manager = new ViewResultManager(new ViewResultManagerConfig(), CreateDbClient(databasePath));
            using SqlSugarClient db = CreateDbClient(databasePath);

            DateTime today = DateTime.Today;
            int currentSessionId = db.Insertable(new KBProductionSession { StartTime = today }).ExecuteReturnIdentity();
            db.Insertable(new KBProductionSession { StartTime = today, EndTime = today.AddHours(1) }).ExecuteCommand();
            db.Insertable(new KBProductionSession { StartTime = today.AddDays(-1) }).ExecuteCommand();
            var original = new KBItemMaster { SN = "old", Items = [new KBItem { Name = "old key" }] };
            manager.Save(original);
            KBItemMaster stale = db.Queryable<KBItemMaster>().Single();

            new KbSqliteCleanupProvider(databasePath).CleanupAll();

            Assert.Empty(db.Queryable<KBItemMaster>().ToList());
            Assert.Equal(currentSessionId, db.Queryable<KBProductionSession>().Single().Id);
            manager.LoadAll();
            var replacement = new KBItemMaster { SN = "new", Items = [new KBItem { Name = "new key" }] };
            manager.Save(replacement);

            Assert.True(replacement.Id > original.Id);
            KBResultPayloadStorage.LoadResult(db, stale);
            Assert.Equal("old", stale.SN);
            Assert.Empty(stale.Items);
            manager.Save(stale);
            Assert.False(manager.UpdateImageDimensions(stale, 1920, 1080));
            Assert.Null(stale.ImageWidth);
            Assert.Null(stale.ImageHeight);
            Assert.Same(replacement, Assert.Single(manager.ViewResluts));

            KBItemMaster current = db.Queryable<KBItemMaster>().Single();
            KBResultPayloadStorage.LoadResult(db, current);
            Assert.Equal("new", current.SN);
            Assert.Equal("new key", Assert.Single(current.Items).Name);
            Assert.Null(current.ImageWidth);
            Assert.Null(current.ImageHeight);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SessionDeleteFailureRollsBackResultDelete(bool historyOnly)
    {
        string databasePath = CreateDatabasePath();
        try
        {
            using SqlSugarClient db = CreateDbClient(databasePath);
            db.CodeFirst.InitTables<KBItemMaster, KBProductionSession>();
            DateTime old = DateTime.Today.AddYears(-1);
            int sessionId = db.Insertable(new KBProductionSession { StartTime = old, EndTime = old.AddHours(1) }).ExecuteReturnIdentity();
            int resultId = db.Insertable(new KBItemMaster { SN = "retained", CreateTime = old, ProductionSessionId = sessionId }).ExecuteReturnIdentity();
            db.Ado.ExecuteCommand("""
                CREATE TRIGGER reject_session_delete BEFORE DELETE ON "KBProductionSession"
                BEGIN
                    SELECT RAISE(ABORT, 'KB session delete rejected');
                END;
                """);
            var provider = new KbSqliteCleanupProvider(databasePath);

            void Cleanup()
            {
                if (historyOnly)
                    provider.CleanupHistory(3);
                else
                    provider.CleanupAll();
            }

            Exception failure = Assert.ThrowsAny<Exception>(Cleanup);
            Assert.Contains("KB session delete rejected", failure.ToString(), StringComparison.Ordinal);
            Assert.Equal(resultId, db.Queryable<KBItemMaster>().Single().Id);
            Assert.Equal(sessionId, db.Queryable<KBProductionSession>().Single().Id);

            db.Ado.ExecuteCommand("DROP TRIGGER reject_session_delete;");
            Cleanup();
            Assert.Empty(db.Queryable<KBItemMaster>().ToList());
            Assert.Empty(db.Queryable<KBProductionSession>().ToList());
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
