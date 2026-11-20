using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing;
using SqlSugar;
using System.ComponentModel;
using System.IO;
using System.Windows.Threading;

namespace ProjectARVRPro.Tests;

public sealed class FlowBatchFinalizationTests
{
    [Fact]
    public async Task DeferredBatchOutcomeUsesTheOriginalDatabaseAndPreservesArchiveStatus()
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("ARVR-batch-database-");
        try
        {
            ConnectionConfig original = Database("original.db");
            ConnectionConfig replacement = Database("replacement.db");
            int id = Seed(original, "original");
            Assert.Equal(id, Seed(replacement, "replacement"));
            var batch = new MeasureBatchModel { Id = id, Code = "original", FlowStatus = FlowStatus.Completed, TotalTime = 75, Result = "original outcome" };
            var receipt = new ArvrFlowBatchReceipt(batch, () => new SqlSugarClient(original));
            var finalization = new FlowBatchFinalization();
            await finalization.RunAsync(receipt.WriteOutcome);

            MeasureBatchModel updated = Read(original, id);
            Assert.Equal(("original", "original outcome", FlowStatus.Completed, ArchiveStatus.Archived, 75),
                (updated.Code, updated.Result, updated.FlowStatus, updated.ArchiveStatus, updated.TotalTime));
            MeasureBatchModel untouched = Read(replacement, id);
            Assert.Equal(("replacement", "replacement", FlowStatus.Ready), (untouched.Code, untouched.Result, untouched.FlowStatus));

            var mismatchedReceipt = new ArvrFlowBatchReceipt(batch, () => new SqlSugarClient(replacement));
            await Assert.ThrowsAsync<InvalidOperationException>(() => finalization.RunAsync(mismatchedReceipt.WriteOutcome));
            untouched = Read(replacement, id);
            Assert.Equal(("replacement", "replacement", FlowStatus.Ready), (untouched.Code, untouched.Result, untouched.FlowStatus));

            ConnectionConfig Database(string name) => new()
            {
                DbType = DbType.Sqlite,
                ConnectionString = $"Data Source={Path.Combine(directory.FullName, name)};Pooling=False",
                InitKeyType = InitKeyType.Attribute,
                IsAutoCloseConnection = true,
            };
        }
        finally
        {
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }

        static int Seed(ConnectionConfig connection, string code)
        {
            using var db = new SqlSugarClient(connection);
            db.CodeFirst.InitTables<MeasureBatchModel>();
            return db.Insertable(new MeasureBatchModel { Code = code, Result = code, ArchiveStatus = ArchiveStatus.Archived }).ExecuteReturnIdentity();
        }

        static MeasureBatchModel Read(ConnectionConfig connection, int id)
        {
            using var db = new SqlSugarClient(connection);
            return Assert.IsType<MeasureBatchModel>(db.Queryable<MeasureBatchModel>().First(batch => batch.Id == id));
        }
    }

    [Fact]
    public Task DispatcherCanRespondAndDrainThePendingWriteBeforeResumingFinalization() =>
        ArvrDrawingOverlayCompatibilityTests.RunAsyncOnStaThread(async () =>
        {
            var finalization = new FlowBatchFinalization();
            finalization.BeginCompletion();
            TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using ManualResetEventSlim release = new();
            TimeSpan watchdog = TimeSpan.FromSeconds(10);
            int writes = 0;
            Task completed = finalization.RunAsync(() =>
            {
                Interlocked.Increment(ref writes);
                entered.SetResult();
                Assert.True(release.Wait(watchdog));
            });

            try
            {
                await entered.Task.WaitAsync(watchdog);
                Assert.False(completed.IsCompleted);
                Task pending = Assert.IsAssignableFrom<Task>(finalization.PendingWrite);
                bool dispatcherResponded = await Dispatcher.CurrentDispatcher.InvokeAsync(() => true);
                Assert.True(dispatcherResponded);
                await Assert.ThrowsAsync<InvalidOperationException>(() => finalization.RunAsync(() => Interlocked.Increment(ref writes)));

                CancelEventArgs intercepted = new() { Cancel = true };
                finalization.OnClosing(intercepted);
                Assert.False(finalization.CloseRequested);

                CancelEventArgs close = new();
                finalization.OnClosing(close);
                Assert.True(close.Cancel);
                Assert.True(finalization.CloseRequested);

                release.Set();
                // Closing on the dispatcher waits for the command, even though the
                // finalization continuation cannot run until the close handler returns.
                pending.WaitAsync(watchdog).GetAwaiter().GetResult();
                Assert.False(completed.IsCompleted);
            }
            finally
            {
                release.Set();
                await completed;
            }

            Assert.Equal(1, writes);
            Assert.Null(finalization.PendingWrite);
            Assert.True(finalization.IsCompleting);
            CancelEventArgs repeatedClose = new();
            finalization.OnClosing(repeatedClose);
            Assert.True(repeatedClose.Cancel);
            Assert.True(finalization.EndCompletion());
            Assert.False(finalization.EndCompletion());

            CancelEventArgs replay = new();
            finalization.OnClosing(replay);
            Assert.False(replay.Cancel);
            Assert.False(finalization.CloseRequested);
        });

    [Fact]
    public Task FailedWriteLeavesTheCompletionGuardInPlaceUntilResultCleanupEnds() =>
        ArvrDrawingOverlayCompatibilityTests.RunAsyncOnStaThread(async () =>
        {
            var finalization = new FlowBatchFinalization();
            finalization.BeginCompletion();
            var failure = new InvalidOperationException("Database write failed.");

            Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(
                () => finalization.RunAsync(() => throw failure)));
            Assert.Null(finalization.PendingWrite);
            Assert.True(finalization.IsCompleting);
            Assert.False(finalization.EndCompletion());
            Assert.False(finalization.IsCompleting);

            int nextBatchWrites = 0;
            finalization.BeginCompletion();
            await finalization.RunAsync(() => Interlocked.Increment(ref nextBatchWrites));
            Assert.Equal(1, nextBatchWrites);
            Assert.Null(finalization.PendingWrite);
            Assert.False(finalization.EndCompletion());
        });
}
