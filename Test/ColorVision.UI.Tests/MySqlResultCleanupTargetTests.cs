using ColorVision.Database;
using SqlSugar;

namespace ColorVision.UI.Tests;

public sealed class MySqlResultCleanupTargetTests
{
    [Fact]
    public void CapturedTargetRetainsItsIdentityAndRejectsAllOperationsAfterReconnect()
    {
        MySqlConnectedEndpoint? current = null;
        int clientsCreated = 0;
        MySqlConnectedEndpoint MakeEndpoint(string server, int port, string database) => new(
            endpoint => ReferenceEquals(endpoint, current),
            () =>
            {
                clientsCreated++;
                return new SqlSugarClient(new ConnectionConfig
                {
                    ConnectionString = $"Server={server};Port={port};Database={database};User ID=synthetic-user;Password=synthetic-password",
                    DbType = DbType.MySql,
                    IsAutoCloseConnection = true,
                });
            });

        var first = MakeEndpoint("first.invalid", 3391, "result_a");
        var second = MakeEndpoint("second.invalid", 3392, "result_b");
        IDatabaseCleanupTargetProvider provider = new MySqlResultCleanupProvider(() => current);
        Assert.Null(provider.CaptureCurrentTarget());
        current = first;
        DatabaseCleanupTarget snapshot = Assert.IsType<DatabaseCleanupTarget>(provider.CaptureCurrentTarget());
        Assert.Contains("first.invalid:3391", snapshot.Description);
        Assert.Contains("result_a", snapshot.Description);
        Assert.DoesNotContain("synthetic-user", snapshot.Description);
        Assert.DoesNotContain("synthetic-password", snapshot.Description);
        Assert.True(snapshot.IsCurrent);
        Assert.Equal(1, clientsCreated);

        current = second;
        Assert.False(snapshot.IsCurrent);
        Assert.Contains("result_a", snapshot.Provider.Description);
        Assert.Throws<InvalidOperationException>(() => snapshot.Provider.LoadTables());
        Assert.Throws<InvalidOperationException>(() => snapshot.Provider.CleanupHistory(3));
        Assert.Throws<InvalidOperationException>(() => snapshot.Provider.CleanupAll());
        Assert.Throws<InvalidOperationException>(() => ((IDatabaseCleanupSelectionProvider)snapshot.Provider)
            .CleanupTables(MySqlResultCleanupProvider.ResultTableNames));
        Assert.Throws<InvalidOperationException>(() => ((IDatabaseCleanupBackupProvider)snapshot.Provider).CreateBackup());
        Assert.Throws<InvalidOperationException>(() => ((IDatabaseCleanupOptimizationProvider)snapshot.Provider).ExecuteOptimization());
        Assert.Throws<InvalidOperationException>(() => ((IDatabaseCleanupMaintenanceProvider)snapshot.Provider)
            .ExecuteCleanupWithBackup(() => throw new Exception("Cleanup must never be called for a stale target.")));
        Assert.Equal(1, clientsCreated);

        DatabaseCleanupTarget replacement = Assert.IsType<DatabaseCleanupTarget>(provider.CaptureCurrentTarget());
        Assert.Contains("second.invalid:3392", replacement.Description);
        Assert.Contains("result_b", replacement.Description);
        Assert.True(replacement.IsCurrent);
        Assert.Equal(2, clientsCreated);
    }

    [Fact]
    public void ReconnectingDuringBackupPreventsTheCleanupCallback()
    {
        MySqlConnectedEndpoint? current = null;
        MySqlConnectedEndpoint original = new(endpoint => ReferenceEquals(endpoint, current),
            () => throw new InvalidOperationException("This regression must not connect to MySQL."));
        MySqlConnectedEndpoint replacement = new(endpoint => ReferenceEquals(endpoint, current),
            () => throw new InvalidOperationException("This regression must not connect to MySQL."));
        current = original;
        var provider = new MySqlResultCleanupProvider(() => current);
        bool cleanupInvoked = false;

        Assert.Throws<InvalidOperationException>(() => provider.ExecuteCleanupWithBackup(endpoint =>
        {
            Assert.Same(original, endpoint);
            current = replacement;
            return new DatabaseCleanupBackupResult { FilePath = "synthetic.sql" };
        }, () =>
        {
            cleanupInvoked = true;
            return new DatabaseCleanupExecutionResult();
        }));
        Assert.False(cleanupInvoked);
    }
}
