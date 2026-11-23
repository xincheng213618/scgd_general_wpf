using ColorVision.Database;
using ColorVision.Engine;
using MySqlConnector;
using System.IO;
using System.Windows;

namespace ColorVision.UI.Tests;

public sealed class MySqlConnectedEndpointTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(8);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OlderValidationCannotPublishSuccessOrFailureAfterANewerConnection(bool oldFails)
    {
        _ = WpfTestHost.Invoke(() => Application.Current.Dispatcher);
        using var control = new MySqlControl();
        List<bool> published = [];
        control.MySqlConnectChanged += (_, _) =>
        {
            Assert.True(Application.Current.Dispatcher.CheckAccess());
            published.Add(control.IsConnect);
        };

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        Task<MySqlConnectionAttempt> older = control.ConnectAsync(Config("synthetic_first"), endpoint =>
        {
            Assert.Equal("synthetic_first", DatabaseName(endpoint));
            started.SetResult();
            if (!release.Wait(TestTimeout)) throw new TimeoutException();
            if (oldFails) throw new IOException("Synthetic connection failure.");
        });

        try
        {
            await started.Task.WaitAsync(TestTimeout);
            MySqlConnectionAttempt newer = await control.ConnectAsync(Config("synthetic_next"), endpoint =>
                Assert.Equal("synthetic_next", DatabaseName(endpoint))).WaitAsync(TestTimeout);
            MySqlConnectedEndpoint current = Assert.IsType<MySqlConnectedEndpoint>(control.ConnectedEndpoint);
            release.Set();
            MySqlConnectionAttempt obsolete = await older.WaitAsync(TestTimeout);

            Assert.Equal(MySqlConnectOutcome.Connected, newer.Outcome);
            Assert.True(newer.IsCurrent);
            Assert.Equal(MySqlConnectOutcome.Superseded, obsolete.Outcome);
            Assert.False(obsolete.IsCurrent);
            Assert.False(MySqlInitializer.ShouldRepairLocalConnection(obsolete));
            Assert.Same(current, control.ConnectedEndpoint);
            Assert.Equal(new[] { false, false, true }, published);
        }
        finally
        {
            release.Set();
            await older.WaitAsync(TestTimeout);
        }
    }

    [Fact]
    public async Task RetainedEndpointKeepsItsConnectionTargetAfterEditingAndReconnecting()
    {
        _ = WpfTestHost.Invoke(() => Application.Current.Dispatcher);
        using var control = new MySqlControl();
        MySqlConfig config = Config("synthetic_first");
        await control.ConnectAsync(config, _ => { }).WaitAsync(TestTimeout);
        MySqlConnectedEndpoint original = Assert.IsType<MySqlConnectedEndpoint>(control.ConnectedEndpoint);

        config.Database = "synthetic_second";
        Assert.Equal("synthetic_first", DatabaseName(original));
        Assert.True(original.IsCurrent);
        await control.ConnectAsync(config, _ => { }).WaitAsync(TestTimeout);
        MySqlConnectedEndpoint second = Assert.IsType<MySqlConnectedEndpoint>(control.ConnectedEndpoint);
        Assert.Equal("synthetic_second", DatabaseName(second));
        Assert.False(original.IsCurrent);
        Assert.Equal("synthetic_first", DatabaseName(original));

        config.Database = "synthetic_first";
        await control.ConnectAsync(config, _ => { }).WaitAsync(TestTimeout);
        MySqlConnectedEndpoint reconnected = Assert.IsType<MySqlConnectedEndpoint>(control.ConnectedEndpoint);
        Assert.Equal("synthetic_first", DatabaseName(reconnected));
        Assert.NotSame(original, reconnected);
        Assert.True(reconnected.IsCurrent);
        Assert.False(original.IsCurrent);
    }

    [Fact]
    public async Task OnlyTheCurrentLocalFailureCanTriggerStartupRepair()
    {
        _ = WpfTestHost.Invoke(() => Application.Current.Dispatcher);
        using var control = new MySqlControl();
        MySqlConnectionAttempt failed = await control.ConnectAsync(Config("synthetic_local"), _ =>
            throw new IOException("Synthetic local failure.")).WaitAsync(TestTimeout);

        Assert.Equal(MySqlConnectOutcome.Failed, failed.Outcome);
        Assert.Null(control.ConnectedEndpoint);
        Assert.True(MySqlInitializer.ShouldRepairLocalConnection(failed));
        await control.ConnectAsync(Config("synthetic_replacement"), _ => { }).WaitAsync(TestTimeout);
        Assert.False(MySqlInitializer.ShouldRepairLocalConnection(failed));
    }

    private static MySqlConfig Config(string database) => new() { Host = "localhost", Database = database };

    private static string DatabaseName(MySqlConnectedEndpoint endpoint)
    {
        using var db = endpoint.CreateDbClient();
        return new MySqlConnectionStringBuilder(db.CurrentConnectionConfig.ConnectionString).Database;
    }
}
