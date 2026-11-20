using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Results;
using FlowEngineLib.Base;
using SqlSugar;
using System.Diagnostics;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class LocalImageDatabaseAffinityTests
{
    [Fact]
    public void LocalImageWritesAndPublishesFromTheBatchDatabaseAfterReconnect() => StaTest.Run(() =>
    {
        using var files = new FusionFixture();
        string imagePath = files.Add("source.png", value: 123);
        MySqlConnectedEndpoint? current = null;
        var original = new MySqlConnectedEndpoint(candidate => ReferenceEquals(candidate, current), () => Connect("original.db"));
        var replacement = new MySqlConnectedEndpoint(candidate => ReferenceEquals(candidate, current), () => Connect("replacement.db"));
        current = original;
        using (var db = original.CreateDbClient()) Init(db);
        using (var db = replacement.CreateDbClient()) Init(db);
        const string code = "same-code";
        var originalBatch = new MeasureBatchModel { Code = code, Name = "original" };
        FlowMeasurementBatch bound = FlowMeasurementBatch.Insert(originalBatch, original);
        using (var db = replacement.CreateDbClient())
            Assert.Equal(originalBatch.Id, db.Insertable(new MeasureBatchModel { Code = code, Name = "replacement" }).ExecuteReturnIdentity());

        var action = new CVStartCFC(code);
        bound.Bind(action.RuntimeResources);
        current = replacement;
        ResultMessage? message = null;
        using var subscription = ResultMessageBus.Default.Subscribe(value =>
        {
            if (value.SerialNumber == code) message = value;
        });
        try
        {
            LocalImageResultData result = new TestImageNode { ImageFileUrl = imagePath }.Run(action);
            Assert.True(result.HasRaw);
            using var db = original.CreateDbClient();
            MeasureResultImgModel saved = Assert.Single(db.Queryable<MeasureResultImgModel>().ToList());
            Assert.Equal((result.MasterId, originalBatch.Id, imagePath), (saved.Id, saved.BatchId, saved.FileUrl));
            using var other = replacement.CreateDbClient();
            Assert.Empty(other.Queryable<MeasureResultImgModel>().ToList());
            Assert.NotNull(message);
            Assert.Same(original, message.DatabaseEndpoint);
        }
        finally { action.RuntimeResources.Dispose(); }

        SqlSugarClient Connect(string name) => new(new ConnectionConfig
        {
            DbType = DbType.Sqlite,
            ConnectionString = $"Data Source={Path.Combine(files.Directory, name)};Pooling=False",
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute,
        });
    });

    [Fact]
    public void LegacyBoundBatchDoesNotFallBackToTheSameCodeInAnotherDatabase()
    {
        const string code = "repeated-code";
        const string original = "Data Source=synthetic-original;Pooling=False";
        var action = new CVStartCFC(code);
        new FlowMeasurementBatch(code, 5, original, Stopwatch.GetTimestamp()).Bind(action.RuntimeResources);
        try
        {
            Assert.Equal(5, FlowMeasurementBatch.ResolveId(action, Endpoint(original), _ => throw new InvalidOperationException("Unexpected lookup.")));
            Assert.Null(FlowMeasurementBatch.ResolveId(action, Endpoint("Data Source=synthetic-replacement;Pooling=False"),
                _ => throw new InvalidOperationException("Must not search another database.")));
        }
        finally { action.RuntimeResources.Dispose(); }

        static MySqlConnectedEndpoint Endpoint(string connectionString) => new(_ => true, () => new SqlSugarClient(new ConnectionConfig
        {
            DbType = DbType.Sqlite,
            ConnectionString = connectionString,
            IsAutoCloseConnection = true,
        }));
    }

    private static void Init(SqlSugarClient db)
    {
        db.CodeFirst.InitTables<MeasureBatchModel>();
        db.CodeFirst.InitTables<MeasureResultImgModel>();
    }

    private sealed class TestImageNode : TestMessageBoxNode
    {
        public LocalImageResultData Run(CVStartCFC action) => Assert.IsType<LocalImageResultData>(ExecuteLocal(action).Data);
    }
}
