using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing;
using SqlSugar;
using System.IO;
using Xunit;

namespace ProjectKB.Tests;

public sealed class KBFlowBatchReceiptTests
{
    [Fact]
    public void TerminalWriteUsesTheInsertedDatabaseEvenWhenTheActiveDatabaseChanges()
    {
        using var databases = new TestDatabases();
        var original = new MeasureBatchModel { Code = "flow-a", Name = "SN-A", TId = 21 };
        var unrelated = new MeasureBatchModel { Code = "flow-b", Name = "SN-B", TId = 42 };
        databases.Insert("a", original);
        databases.Insert("b", unrelated);
        Assert.Equal(original.Id, unrelated.Id);

        string active = "a";
        var receipt = new KBFlowBatchReceipt(original, databases.Capture(active));
        active = "b";
        using (var db = databases.Capture("a")())
        {
            db.Updateable<MeasureBatchModel>()
                .SetColumns(row => new MeasureBatchModel { ArchiveStatus = ArchiveStatus.Archived })
                .Where(row => row.Id == original.Id)
                .ExecuteCommand();
        }

        receipt.WriteOutcome(FlowStatus.Completed, 1200, "measurement complete");

        var completed = databases.Read("a", original.Id);
        Assert.Equal(FlowStatus.Completed, completed.FlowStatus);
        Assert.Equal(1200, completed.TotalTime);
        Assert.Equal("measurement complete", completed.Result);
        Assert.Equal(ArchiveStatus.Archived, completed.ArchiveStatus);
        Assert.Equal(21, completed.TId);
        var untouched = databases.Read(active, unrelated.Id);
        Assert.Equal("flow-b", untouched.Code);
        Assert.Equal("SN-B", untouched.Name);
        Assert.Equal(42, untouched.TId);
        Assert.Equal(FlowStatus.Ready, untouched.FlowStatus);
        Assert.Equal(0, untouched.TotalTime);
        Assert.Null(untouched.Result);
    }

    [Fact]
    public void TerminalWriteDoesNotTargetAnIdenticalIdWithADifferentBatchCode()
    {
        using var databases = new TestDatabases();
        var existing = new MeasureBatchModel { Code = "existing-code", Name = "Existing SN" };
        databases.Insert("a", existing);
        var mismatched = new MeasureBatchModel { Id = existing.Id, Code = "different-code" };
        var receipt = new KBFlowBatchReceipt(mismatched, databases.Capture("a"));

        Assert.Throws<InvalidOperationException>(() => receipt.WriteOutcome(FlowStatus.Failed, 1, "unexpected"));

        var untouched = databases.Read("a", existing.Id);
        Assert.Equal("existing-code", untouched.Code);
        Assert.Equal(FlowStatus.Ready, untouched.FlowStatus);
        Assert.Null(untouched.Result);
    }

    private sealed class TestDatabases : IDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("CV-kb-batch-receipt-");

        internal Func<SqlSugarClient> Capture(string name)
        {
            string path = Path.Combine(directory.FullName, $"{name}.db");
            return () => new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={path};Pooling=False",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
            });
        }

        internal void Insert(string name, MeasureBatchModel batch)
        {
            using var db = Capture(name)();
            db.CodeFirst.InitTables<MeasureBatchModel>();
            batch.Id = db.Insertable(batch).ExecuteReturnIdentity();
        }

        internal MeasureBatchModel Read(string name, int id)
        {
            using var db = Capture(name)();
            return db.Queryable<MeasureBatchModel>().Single(row => row.Id == id);
        }

        public void Dispose()
        {
            foreach (FileInfo file in directory.EnumerateFiles())
                file.Delete();
            directory.Delete();
        }
    }
}
