using ColorVision.Engine;
using Microsoft.Data.Sqlite;
using SqlSugar;
using System.IO;

namespace ProjectARVRPro.Tests;

public sealed class ArvrBatchHistoryTests
{
    [Theory]
    [InlineData("expected-code", "expected-code", "expected-name", true)]
    [InlineData("expected-code", "stale-code", "expected-name", false)]
    [InlineData("", "stored-code", "expected-name", true)]
    [InlineData("", "stored-code", "another-name", false)]
    public void StoredBatchIdentityMustMatchBeyondTheNumericId(string resultCode, string batchCode, string batchName, bool matches)
    {
        var result = new ProjectARVRReuslt { BatchId = 7, Code = resultCode, SN = "expected-name" };
        var batch = new MeasureBatchModel { Id = 7, Code = batchCode, Name = batchName };
        Assert.Same(matches ? batch : null, ArvrBatchHistory.Resolve(result, id =>
        {
            Assert.Equal(7, id);
            return batch;
        }));
    }

    [Fact]
    public void DatabaseKeyDistinguishesTargetsAndDoesNotDependOnCredentials()
    {
        static SqlSugarClient Open(string host, int port, string schema, string user) => new(new ConnectionConfig
        {
            ConnectionString = $"Server={host};Port={port};Database={schema};User ID={user};Password=synthetic-password",
            DbType = DbType.MySql,
            IsAutoCloseConnection = true,
        });

        string? key = ArvrBatchHistory.GetDatabaseKey(() => Open("original.invalid", 3317, "results", "operator"));
        Assert.NotNull(key);
        Assert.Equal(key, ArvrBatchHistory.GetDatabaseKey(() => Open("ORIGINAL.INVALID", 3317, "results", "reviewer")));
        Assert.NotEqual(key, ArvrBatchHistory.GetDatabaseKey(() => Open("replacement.invalid", 3317, "results", "operator")));
        Assert.NotEqual(key, ArvrBatchHistory.GetDatabaseKey(() => Open("original.invalid", 3318, "results", "operator")));
        Assert.NotEqual(key, ArvrBatchHistory.GetDatabaseKey(() => Open("original.invalid", 3317, "other_results", "operator")));
        Assert.Null(ArvrBatchHistory.GetDatabaseKey(() => Open("original.invalid,replacement.invalid", 3317, "results", "operator")));
    }

    [Fact]
    public void ReplayOnlyReusesAStoredResultFromTheSameDatabaseAndBatchEvenAfterReopen()
    {
        string path = Path.Combine(Path.GetTempPath(), $"cv-arvr-replay-{Guid.NewGuid():N}.db");
        try
        {
            const string flow = "defect-inspection";
            var batch = new MeasureBatchModel { Id = 17, Code = "shared-batch", Name = "shared-product" };
            const string originalKey = "DB-A";
            const string replacementKey = "DB-B";
            ProjectARVRReuslt MakeResult(string? key, bool passed) => new()
            {
                BatchId = batch.Id, Code = batch.Code, Model = flow, SN = batch.Name, BatchDatabaseKey = key, Result = passed,
            };

            using (var write = new ViewResultManager(path, new ViewResultManagerConfig()))
            {
                write.Save(MakeResult(originalKey, false), readMeasureResults: id => [new MeasureResultImgModel
                {
                    BatchId = id, ImgFrameInfo = "{\"width\":14,\"height\":18}",
                }]);
                write.Save(MakeResult(replacementKey, true), readMeasureResults: _ => []);
                write.Save(MakeResult(null, true), readMeasureResults: _ => []);
            }

            using var read = new ViewResultManager(path, new ViewResultManagerConfig { Count = 1 });
            var original = Assert.IsType<ProjectARVRReuslt>(read.FindByOwnedBatch(batch, flow, originalKey));
            Assert.False(original.Result);
            Assert.Equal(14, original.ImageWidth);
            Assert.Equal(18, original.ImageHeight);
            var replacement = Assert.IsType<ProjectARVRReuslt>(read.FindByOwnedBatch(batch, flow, replacementKey));
            Assert.True(replacement.Result);
            Assert.Null(replacement.ImageWidth);
            Assert.Null(read.FindByOwnedBatch(batch, flow, null));
            Assert.Null(read.FindByOwnedBatch(batch, "other-flow", originalKey));
            Assert.Null(read.FindByOwnedBatch(new MeasureBatchModel { Id = batch.Id, Code = "different-code" }, flow, originalKey));
        }
        finally
        {
            using var pool = new SqliteConnection($"Data Source={path};Default Timeout=5");
            SqliteConnection.ClearPool(pool);
            File.Delete(path);
            File.Delete(path + "-wal");
            File.Delete(path + "-shm");
        }
    }
}
