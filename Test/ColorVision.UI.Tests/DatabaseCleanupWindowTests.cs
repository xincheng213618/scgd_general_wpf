using ColorVision.Database;
using ColorVision.Engine;

namespace ColorVision.UI.Tests;

public class DatabaseCleanupWindowTests
{
    [Fact]
    public void ValidateCleanupTableNames_RejectsTablesOutsideWhitelist()
    {
        Assert.Throws<ArgumentException>(() =>
            MySqlResultCleanupProvider.ValidateCleanupTableNames(["t_scgd_algorithm_result_detail_future_vendor"]));
    }

    [Fact]
    public void ValidateCleanupTableNames_NormalizesDuplicatesAndUsesDependencyOrder()
    {
        var result = MySqlResultCleanupProvider.ValidateCleanupTableNames(
        [
            "t_scgd_algorithm_result_master",
            "T_SCGD_ALGORITHM_RESULT_DETAIL_SFR",
            "t_scgd_algorithm_result_detail_sfr"
        ]);

        Assert.Equal(
        [
            "t_scgd_algorithm_result_detail_sfr",
            "t_scgd_algorithm_result_master"
        ], result);
    }

    [Fact]
    public void HistoryCleanupSafety_FindsUnknownDetailTablesBeforeAnyDeletion()
    {
        string[] existingTables =
        [
            "t_scgd_algorithm_result_master",
            "t_scgd_algorithm_result_detail_sfr",
            "t_scgd_algorithm_result_detail_future_vendor",
            "t_scgd_measure_result_future_vendor",
            "unrelated_business_table",
            "T_SCGD_MEASURE_RESULT_FUTURE_VENDOR"
        ];

        var unknown = MySqlResultCleanupProvider.FindUnknownDetailTables(existingTables);

        Assert.Equal(
        [
            "t_scgd_algorithm_result_detail_future_vendor",
            "t_scgd_measure_result_future_vendor"
        ], unknown);
    }

    [Fact]
    public void ExecuteBackupAndCleanup_UsesOptionalMaintenanceProviderAsOneCombination()
    {
        var provider = new TestMaintenanceProvider();
        int cleanupCalls = 0;

        var result = DatabaseCleanupSourceViewModel.ExecuteBackupAndCleanup(
            provider,
            provider,
            () =>
            {
                cleanupCalls++;
                return new DatabaseCleanupExecutionResult { StatusMessage = "cleaned" };
            });

        Assert.Equal(1, provider.MaintenanceCalls);
        Assert.Equal(0, provider.StandaloneBackupCalls);
        Assert.Equal(1, cleanupCalls);
        Assert.Equal("atomic.sql", result.Backup.FilePath);
        Assert.Equal("cleaned", result.Cleanup.StatusMessage);
    }

    [Fact]
    public void TableSelection_IsOnlyAvailableForExistingTables()
    {
        var table = new DatabaseCleanupTableInfo { TableName = "sample" };

        table.IsSelected = true;
        Assert.False(table.IsSelected);

        table.Exists = true;
        table.IsSelected = true;
        Assert.True(table.IsSelected);

        table.Exists = false;
        Assert.False(table.IsSelected);
    }

    [Fact]
    public async Task SourceViewModel_EnablesBackupAndMultiTableSelectionCapabilities()
    {
        var viewModel = new DatabaseCleanupSourceViewModel(new TestCleanupProvider());

        await viewModel.RefreshAsync();
        viewModel.SelectAllCommand.Execute(null);

        Assert.True(viewModel.SupportsBackup);
        Assert.False(viewModel.BackupBeforeCleanup);
        viewModel.BackupBeforeCleanup = true;
        Assert.True(viewModel.BackupBeforeCleanup);
        Assert.True(viewModel.SupportsTableCleanup);
        Assert.Equal(2, viewModel.ExistingTableCount);
        Assert.Equal(2, viewModel.SelectedTableCount);
        Assert.Equal(EngineLocalization.Format($"已选择 {2:N0} 张表"), viewModel.SelectionSummary);
        Assert.False(viewModel.Tables.Single(table => !table.Exists).IsSelected);
    }

    private sealed class TestCleanupProvider : IDatabaseCleanupSourceProvider, IDatabaseCleanupSelectionProvider, IDatabaseCleanupBackupProvider
    {
        public string Id => "test";
        public string DisplayName => "测试数据源";
        public string Description => "测试";
        public int Order => 0;

        public IReadOnlyList<DatabaseCleanupTableInfo> LoadTables()
        {
            return
            [
                new DatabaseCleanupTableInfo { TableName = "table_a", Exists = true, RowCount = 10, SizeBytes = 1024 },
                new DatabaseCleanupTableInfo { TableName = "table_b", Exists = true, RowCount = 20, SizeBytes = 2048 },
                new DatabaseCleanupTableInfo { TableName = "table_missing", Exists = false }
            ];
        }

        public DatabaseCleanupExecutionResult CleanupHistory(int keepMonths) => throw new NotSupportedException();
        public DatabaseCleanupExecutionResult CleanupAll() => throw new NotSupportedException();
        public DatabaseCleanupExecutionResult CleanupTables(IReadOnlyCollection<string> tableNames) => throw new NotSupportedException();
        public DatabaseCleanupBackupResult CreateBackup() => throw new NotSupportedException();
    }

    private sealed class TestMaintenanceProvider : IDatabaseCleanupBackupProvider, IDatabaseCleanupMaintenanceProvider
    {
        public int MaintenanceCalls { get; private set; }
        public int StandaloneBackupCalls { get; private set; }

        public DatabaseCleanupBackupResult CreateBackup()
        {
            StandaloneBackupCalls++;
            throw new InvalidOperationException("Standalone backup must not be used when maintenance capability is available.");
        }

        public DatabaseCleanupMaintenanceResult ExecuteCleanupWithBackup(Func<DatabaseCleanupExecutionResult> cleanupAction)
        {
            MaintenanceCalls++;
            return new DatabaseCleanupMaintenanceResult
            {
                Backup = new DatabaseCleanupBackupResult { FilePath = "atomic.sql", StatusMessage = "backed up" },
                Cleanup = cleanupAction()
            };
        }
    }
}
