using ColorVision.Engine.Messages;
using Microsoft.Data.Sqlite;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class MsgRecordDatabaseTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TerminalStateIsPersistedWithoutWaitingForSqliteOnTheReplyThread(bool settledBeforeInsert)
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("ColorVision-MsgRecord-");
        string dbPath = Path.Combine(directory.FullName, "messages.db");
        string connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        Task? persistence = null;
        var request = new MsgSend { MsgID = Guid.NewGuid().ToString(), EventName = "GetData" };
        MsgRecord record = WpfTestHost.Invoke(() => new MsgRecord
        {
            MsgID = request.MsgID,
            MsgSend = request,
            MsgRecordState = MsgRecordState.Sended,
            SendTime = DateTime.Now,
        });
        var response = new MsgReturn { MsgID = request.MsgID, EventName = request.EventName, Code = 0, Message = "synthetic response" };
        try
        {
            if (settledBeforeInsert)
                await Task.Run(() => record.CompleteWithResponse(response)).WaitAsync(TestTimeout);

            persistence = MsgRecordDataBaseHelper.InsertAndTrackTerminal(record, dbPath);
            using var db = new SqliteConnection(connectionString);
            db.Open();
            if (!settledBeforeInsert)
            {
                // Hold SQLite's writer lock while the response takes its normal WPF notification path.
                using var blocker = db.BeginTransaction(deferred: false);
                await Task.Run(() => record.CompleteWithResponse(response)).WaitAsync(TestTimeout);
                Assert.Equal(MsgRecordState.Success, record.MsgRecordState);
                Assert.False(persistence.IsCompleted);
                blocker.Rollback();
            }

            await persistence.WaitAsync(TestTimeout);
            AssertStoredRecord(db, record, MsgRecordState.Success, response.Message);
        }
        finally
        {
            if (!MsgRecord.IsTerminal(record.MsgRecordState))
                WpfTestHost.Invoke(() => record.MsgRecordState = MsgRecordState.Timeout);
            if (persistence != null) await persistence.WaitAsync(TestTimeout);
            using var pool = new SqliteConnection(connectionString);
            SqliteConnection.ClearPool(pool);
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }

    private static void AssertStoredRecord(SqliteConnection db, MsgRecord original, MsgRecordState expected, string response)
    {
        using var command = db.CreateCommand();
        command.CommandText = "SELECT id, MsgRecordState, MsgReturnJson, ReciveTime FROM MsgRecord WHERE MsgID = $id";
        command.Parameters.AddWithValue("$id", original.MsgID);
        using var rows = command.ExecuteReader();
        Assert.True(rows.Read());
        Assert.Equal(original.Id, rows.GetInt32(0));
        Assert.Equal((int)expected, rows.GetInt32(1));
        Assert.Contains(response, rows.GetString(2));
        Assert.Equal(original.ReciveTime, rows.GetDateTime(3), TimeSpan.FromMilliseconds(1));
        Assert.False(rows.Read());
    }
}
