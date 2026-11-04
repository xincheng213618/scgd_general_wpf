using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Results;
using SqlSugar;

namespace ColorVision.UI.Tests;

public sealed class AlgorithmResultHistoryTests
{
    [Fact]
    public void RepeatedAndMissingBatchIdsUseOneQueryAndPreserveRowsAndCodes() => WpfTestHost.Invoke(() =>
    {
        using var db = CreateDatabase();
        db.CodeFirst.InitTables<MeasureBatchModel>();
        db.Insertable(new[]
        {
            new MeasureBatchModel { Id = 1, Code = "batch-one", Name = "not-the-code" },
            new MeasureBatchModel { Id = 2, Code = null, Name = "no-fallback" },
            new MeasureBatchModel { Id = 3, Code = string.Empty },
        }).ExecuteCommand();
        AlgResultMasterModel[] rows = [
            new() { Id = 9, BatchId = 1, ImgFile = "", TName = "template", Result = "diagnostic", version = "2.0", TotalTime = 17 },
            new() { Id = 8, BatchId = 2 }, new() { Id = 7, BatchId = 1 }, new() { Id = 6, BatchId = 3 },
            new() { Id = 5, BatchId = 99 }, new() { Id = 4, BatchId = 0 },
            new() { Id = 3, BatchId = -2 }, new() { Id = 2, BatchId = null }
        ];
        int queries = 0;
        db.Aop.OnLogExecuting = (_, _) => queries++;

        var views = AlgorithmResultHistory.CreateViews(rows, db);

        Assert.Equal(1, queries);
        Assert.Equal(rows.Select(row => row.Id), views.Select(view => view.Id));
        Assert.Equal(new string?[] { "batch-one", null, "batch-one", "", null, null, null, null }, views.Select(view => view.Batch));
        for (int index = 0; index < rows.Length; index++) Assert.Same(rows[index], views[index].AlgResultMasterModel);
        Assert.Equal("template", views[0].POITemplateName);
        Assert.Equal("diagnostic", views[0].ResultDesc);
        Assert.Equal("2.0", views[0].Version);
        Assert.Equal(17, views[0].TotalTime);

        Assert.Empty(AlgorithmResultHistory.CreateViews([], db));
        AlgorithmResultHistory.CreateViews([new() { Id = 1, BatchId = -2 }], db);
        Assert.Equal(1, queries);
    });

    [Fact]
    public void LargeHistoryChunksDistinctIdsAndReadsFreshCodesForEachQuery() => WpfTestHost.Invoke(() =>
    {
        using var db = CreateDatabase();
        db.CodeFirst.InitTables<MeasureBatchModel>();
        int count = AlgorithmResultHistory.BatchIdsPerQuery + 1;
        db.Insertable(Enumerable.Range(1, count).Select(id => new MeasureBatchModel { Id = id, Code = $"code-{id}" }).ToArray()).ExecuteCommand();
        AlgResultMasterModel[] rows = Enumerable.Range(1, count).Reverse()
            .Select(id => new AlgResultMasterModel { Id = id, BatchId = id }).ToArray();
        int queries = 0;
        db.Aop.OnLogExecuting = (_, _) => queries++;
        var first = AlgorithmResultHistory.CreateViews(rows, db);
        Assert.Equal(2, queries);
        Assert.Equal(rows.Select(row => $"code-{row.BatchId}"), first.Select(view => view.Batch));

        db.Updateable<MeasureBatchModel>().SetColumns(batch => batch.Code == "renamed").Where(batch => batch.Id == count).ExecuteCommand();
        queries = 0;
        var second = AlgorithmResultHistory.CreateViews(rows, db);
        Assert.Equal(2, queries);
        Assert.Equal("renamed", second[0].Batch);
        Assert.Equal($"code-{count}", first[0].Batch);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnavailableBatchMetadataKeepsAlgorithmRows(bool failedQuery) => WpfTestHost.Invoke(() =>
    {
        // The database deliberately has no batch table: metadata failure stays optional.
        using var db = CreateDatabase();
        AlgResultMasterModel[] rows = [new() { Id = 2, BatchId = 1 }, new() { Id = 1, BatchId = 2 }];
        var views = AlgorithmResultHistory.CreateViews(rows, failedQuery ? db : null);
        Assert.Equal(new[] { 2, 1 }, views.Select(view => view.Id));
        Assert.All(views, view => Assert.Null(view.Batch));
        Assert.Null(new ViewResultAlg(rows[0], null).Batch);
    });

    private static SqlSugarClient CreateDatabase() => new(new ConnectionConfig
    {
        ConnectionString = "Data Source=:memory:", DbType = DbType.Sqlite, IsAutoCloseConnection = false
    });
}
