using ColorVision.SocketProtocol;
using Microsoft.Data.Sqlite;
using SqlSugar;
using System.IO;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class SocketMessageAsyncProjectionTests
{
    [Theory]
    [InlineData(OrderByType.Asc)]
    [InlineData(OrderByType.Desc)]
    public void CommitAndReturnDoNotWaitForTheUiAndRowsKeepCommitOrder(OrderByType order)
    {
        WithManager(order, (manager, path) =>
        {
            Task producer = Task.Run(() =>
            {
                manager.AddMessage(Message("one"));
                manager.AddMessage(Message("two"));
                manager.AddMessage(Message("three"));
            });
            // Intentionally do not pump the UI: the old Dispatcher.Invoke path blocks here.
            Assert.True(producer.Wait(TimeSpan.FromSeconds(5)), "Protocol-side persistence waited for the UI.");
            Assert.Empty(manager.Messages);
            Assert.Equal(3L, CountRows(path));

            PumpDispatcher();
            string[] expected = order == OrderByType.Asc ? ["one", "two", "three"] : ["three", "two", "one"];
            Assert.Equal(expected, manager.Messages.Select(row => row.MsgID));
            Assert.All(manager.Messages, row => Assert.True(row.Id > 0));
        });
    }

    [Fact]
    public void QueryDoesNotDuplicateAlreadyCommittedPendingRows()
    {
        WithManager(OrderByType.Desc, (manager, _) =>
        {
            manager.AddMessage(Message("before-query"));
            manager.LoadAll();
            manager.AddMessage(Message("after-query"));
            PumpDispatcher();
            Assert.Equal(new[] { "after-query", "before-query" }, manager.Messages.Select(row => row.MsgID));
        });
    }

    [Fact]
    public void ClearSuppressesOldPendingRowsButKeepsDatabaseAndFutureMessages()
    {
        WithManager(OrderByType.Desc, (manager, path) =>
        {
            manager.AddMessage(Message("before-clear"));
            manager.MessagesClearCommand.Execute(null);
            manager.AddMessage(Message("after-clear"));
            PumpDispatcher();
            Assert.Equal("after-clear", Assert.Single(manager.Messages).MsgID);
            Assert.Equal(2L, CountRows(path));
            manager.LoadAll();
            Assert.Equal(2, manager.Messages.Count);
        });
    }

    [Fact]
    public void DisposeSuppressesPendingUiCallbacksWithoutUndoingTheCommit()
    {
        WithManager(OrderByType.Desc, (manager, path) =>
        {
            manager.AddMessage(Message("persisted"));
            manager.Dispose();
            PumpDispatcher();
            Assert.Empty(manager.Messages);
            Assert.Equal(1L, CountRows(path));
        });
    }

    [Fact]
    public void ShutdownDispatcherDoesNotPreventMessagePersistence()
    {
        WithManager(OrderByType.Desc, (manager, path) =>
        {
            Dispatcher.CurrentDispatcher.InvokeShutdown();
            manager.AddMessage(Message("persisted-without-ui"));
            Assert.Empty(manager.Messages);
            Assert.Equal(1L, CountRows(path));
        });
    }

    private static SocketMessage Message(string id) => new()
    {
        ClientEndPoint = "127.0.0.1:1",
        MsgID = id,
        EventName = "TestOnly",
        Content = "{\"value\":\"测试\"}",
        Direction = SocketMessageDirection.Received,
        MessageTime = DateTime.Now,
    };

    private static long CountRows(string path)
    {
        using var db = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM SocketMessage";
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static void WithManager(OrderByType order, Action<SocketMessageManager, string> action)
    {
        StaTest.Run(() =>
        {
            string directory = Directory.CreateTempSubdirectory("ColorVision-SocketProjection-").FullName;
            string path = Path.Combine(directory, "messages.db");
            try
            {
                using var manager = new SocketMessageManager(path, new() { OrderByType = order }, Dispatcher.CurrentDispatcher);
                action(manager, path);
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
                SqliteConnection.ClearAllPools();
                Directory.Delete(directory, recursive: true);
            }
        }, TimeSpan.FromSeconds(15), "Socket projection test did not finish.");
    }
}
