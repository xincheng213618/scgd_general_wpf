using ColorVision.Engine.Messages;
using Microsoft.Data.Sqlite;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class MsgRecordDatabaseReloadTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"ColorVision-MsgReload-{Guid.NewGuid():N}");

    [Fact]
    public void ReloadSwitchesQueriesAndWritesFromDatabaseAToEmptyDatabaseB()
    {
        WpfTestHost.Invoke(() => { });
        Directory.CreateDirectory(_tempRoot);
        MsgRecordManagerConfig configA = CreateConfig("A");
        MsgRecordManagerConfig configB = CreateConfig("B");
        var notifier = new TestConfigReloadNotifier();
        MsgRecordManagerConfig current = configA;

        var runningRecord = new MsgRecord();
        MsgRecordDataBaseHelper.Insert(runningRecord, configA);
        Assert.Equal(1, CountRows(configA.SqliteDbPath));

        using var manager = new MessagesListManager(() => current, notifier, registerDatabaseBrowser: false);
        manager.LoadAll();
        Assert.Single(manager.MsgRecords);

        current = configB;
        notifier.RaiseConfigsReloaded();

        Assert.Same(configB, manager.Config);
        Assert.Empty(manager.MsgRecords);
        Assert.True(File.Exists(configB.SqliteDbPath));
        Assert.Equal(0, CountRows(configB.SqliteDbPath));

        runningRecord.MsgRecordState = MsgRecordState.Sended;
        Assert.Equal((long)MsgRecordState.Sended, ReadState(configA.SqliteDbPath, runningRecord.Id));
        Assert.Equal(0, CountRows(configB.SqliteDbPath));

        MsgRecordDataBaseHelper.Insert(new MsgRecord(), configB);
        manager.LoadAll();

        Assert.Single(manager.MsgRecords);
        Assert.Equal(1, CountRows(configA.SqliteDbPath));
        Assert.Equal(1, CountRows(configB.SqliteDbPath));
    }

    [Fact]
    public void EquivalentPathsShareOneNormalizedInitializationKey()
    {
        Directory.CreateDirectory(_tempRoot);
        string path = Path.Combine(_tempRoot, "same", "..", "same", "MsgRecords.db");
        string first = MsgRecordDataBaseHelper.EnsureDatabaseInitialized(path);
        string second = MsgRecordDataBaseHelper.EnsureDatabaseInitialized(Path.GetFullPath(path).ToUpperInvariant());

        Assert.Equal(first, second, ignoreCase: true);
        Assert.True(File.Exists(first));
    }

    private MsgRecordManagerConfig CreateConfig(string name)
    {
        return new MsgRecordManagerConfig { DirectoryPath = Path.Combine(_tempRoot, name) };
    }

    private static long CountRows(string databasePath)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM MsgRecord";
        return (long)command.ExecuteScalar()!;
    }

    private static long ReadState(string databasePath, int id)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT MsgRecordState FROM MsgRecord WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);
        return (long)command.ExecuteScalar()!;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private sealed class TestConfigReloadNotifier : IConfigReloadNotifier
    {
        public event EventHandler? ConfigsReloaded;

        public void RaiseConfigsReloaded() => ConfigsReloaded?.Invoke(this, EventArgs.Empty);
    }
}
