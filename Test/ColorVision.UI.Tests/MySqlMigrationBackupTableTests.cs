using ColorVision.Database;

namespace ColorVision.UI.Tests;

public class MySqlMigrationBackupTableTests
{
    [Fact]
    public void MigrationBackupTableNames_ContainOnlyServiceConfiguration()
    {
        Assert.Contains("t_scgd_algorithm_poi_template_master", MySqlLocalServicesManager.ServiceSettingTableNames);
        Assert.Contains("t_scgd_sys_resource", MySqlLocalServicesManager.ServiceConfigurationTableNames);
        Assert.Contains("t_scgd_camera_license", MySqlLocalServicesManager.ServiceConfigurationTableNames);
        Assert.Equal(MySqlLocalServicesManager.ServiceConfigurationTableNames, MySqlLocalServicesManager.MigrationBackupTableNames);
        Assert.Empty(MySqlLocalServicesManager.ServiceSettingTableNames.Intersect(
            MySqlLocalServicesManager.MigrationBackupTableNames,
            StringComparer.OrdinalIgnoreCase));
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
