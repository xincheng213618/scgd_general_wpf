using ColorVision.Database;

namespace ColorVision.UI.Tests;

public class MySqlMigrationBackupTableTests
{
    [Fact]
    public void MigrationBackupTableNames_IncludeBuzProductTables()
    {
        Assert.Contains("t_scgd_buz_product_master", MySqlLocalServicesManager.MigrationBackupTableNames);
        Assert.Contains("t_scgd_buz_product_detail", MySqlLocalServicesManager.MigrationBackupTableNames);
    }
}
