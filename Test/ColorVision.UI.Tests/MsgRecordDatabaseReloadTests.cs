using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
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

        Assert.Equal(configB.DirectoryPath, manager.Config.DirectoryPath);
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

    [Fact]
    public void FailedDatabasePreparationKeepsConfigRowsAndPathOnA()
    {
        WpfTestHost.Invoke(() => { });
        Directory.CreateDirectory(_tempRoot);
        MsgRecordManagerConfig configA = CreateConfig("A-failure");
        MsgRecordDataBaseHelper.Insert(new MsgRecord(), configA);
        string blockedParent = Path.Combine(_tempRoot, "blocked-parent");
        File.WriteAllText(blockedParent, "not a directory");
        var configB = new MsgRecordManagerConfig { DirectoryPath = Path.Combine(blockedParent, "B") };
        var notifier = new TestConfigReloadNotifier();
        MsgRecordManagerConfig current = configA;

        using var manager = new MessagesListManager(() => current, notifier, registerDatabaseBrowser: false);
        manager.LoadAll();
        string activePathA = manager.CaptureDatabasePath();

        current = configB;
        Exception? reloadException = Record.Exception(notifier.RaiseConfigsReloaded);

        Assert.Null(reloadException);
        Assert.Equal(configA.DirectoryPath, manager.Config.DirectoryPath);
        Assert.Equal(Path.GetFullPath(configA.SqliteDbPath), activePathA, ignoreCase: true);
        Assert.Equal(activePathA, manager.CaptureDatabasePath(), ignoreCase: true);
        Assert.Single(manager.MsgRecords);
        Assert.Equal(1, manager.TotalCount);
        Assert.False(File.Exists(configB.SqliteDbPath));
    }

    [Fact]
    public void QueuedInsertKeepsCapturedDatabaseAAfterReloadToB()
    {
        WpfTestHost.Invoke(() => { });
        Directory.CreateDirectory(_tempRoot);
        MsgRecordManagerConfig configA = CreateConfig("A-queued");
        MsgRecordManagerConfig configB = CreateConfig("B-queued");
        var notifier = new TestConfigReloadNotifier();
        MsgRecordManagerConfig current = configA;

        using var manager = new MessagesListManager(() => current, notifier, registerDatabaseBrowser: false);
        string queuedPath = manager.CaptureDatabasePath();
        Action queuedInsert = MsgRecordDataBaseHelper.CreateInsertAction(new MsgRecord(), queuedPath);

        current = configB;
        notifier.RaiseConfigsReloaded();
        queuedInsert();

        Assert.Equal(Path.GetFullPath(configA.SqliteDbPath), queuedPath, ignoreCase: true);
        Assert.Equal(Path.GetFullPath(configB.SqliteDbPath), manager.CaptureDatabasePath(), ignoreCase: true);
        Assert.Equal(1, CountRows(configA.SqliteDbPath));
        Assert.Equal(0, CountRows(configB.SqliteDbPath));
    }

    [Fact]
    public async Task ListeningMqttInsertFromOldAGenerationCannotReenterViewAfterAToBToA()
    {
        WpfTestHost.Invoke(() => { });
        Directory.CreateDirectory(_tempRoot);
        MsgRecordManagerConfig configA = CreateConfig("A-cycle");
        MsgRecordManagerConfig configB = CreateConfig("B-cycle");
        MsgRecordManagerConfig reloadedA = CreateConfig("A-cycle");
        var notifier = new TestConfigReloadNotifier();
        MsgRecordManagerConfig current = configA;
        using var queuedInsertEntered = new ManualResetEventSlim();
        using var releaseQueuedInsert = new ManualResetEventSlim();

        using var manager = new MessagesListManager(() => current, notifier, registerDatabaseBrowser: false);
        manager.StartListening();
        Task queuedInsert = MQTTServiceBase.QueueMessageRecordInsert(
            new MsgRecord(),
            manager,
            insert => Task.Run(() =>
            {
                queuedInsertEntered.Set();
                releaseQueuedInsert.Wait();
                insert();
            }));

        Assert.True(queuedInsertEntered.Wait(TimeSpan.FromSeconds(5)));
        current = configB;
        notifier.RaiseConfigsReloaded();
        current = reloadedA;
        notifier.RaiseConfigsReloaded();
        Assert.Equal(Path.GetFullPath(reloadedA.SqliteDbPath), manager.CaptureDatabasePath(), ignoreCase: true);
        Assert.Empty(manager.MsgRecords);
        Assert.Equal(0, manager.TotalCount);

        releaseQueuedInsert.Set();
        await queuedInsert.WaitAsync(TimeSpan.FromSeconds(5));
        WpfTestHost.Invoke(() => { });

        Assert.Equal(1, CountRows(configA.SqliteDbPath));
        Assert.Equal(0, CountRows(configB.SqliteDbPath));
        Assert.Empty(manager.MsgRecords);
        Assert.Equal(0, manager.TotalCount);
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
