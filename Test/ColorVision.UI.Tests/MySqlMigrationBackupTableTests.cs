using ColorVision.Database;

namespace ColorVision.UI.Tests;

public class MySqlMigrationBackupTableTests
{
    [Fact]
    public void MigrationBackupTableNames_ContainServiceSettingsAndConfiguration()
    {
        string[] expectedTables = MySqlLocalServicesManager.ServiceSettingTableNames
            .Concat(MySqlLocalServicesManager.ServiceConfigurationTableNames)
            .ToArray();

        Assert.Equal(expectedTables, MySqlLocalServicesManager.MigrationBackupTableNames);
        Assert.Equal(9, MySqlLocalServicesManager.MigrationBackupTableNames.Count);
    }

    [Fact]
    public void ResultTableNames_AreSeparateFromResetPreservedTables()
    {
        Assert.Contains("t_scgd_measure_batch", MySqlResultCleanupProvider.ResultTableNames);
        Assert.Contains("t_scgd_algorithm_result_master", MySqlResultCleanupProvider.ResultTableNames);
        Assert.Empty(MySqlResultCleanupProvider.ResultTableNames.Intersect(
            MySqlLocalServicesManager.MigrationBackupTableNames,
            StringComparer.OrdinalIgnoreCase));
    }
}
