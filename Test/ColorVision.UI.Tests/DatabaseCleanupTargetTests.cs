using ColorVision.Database;
using System.Collections.Concurrent;
using System.Windows.Input;

namespace ColorVision.UI.Tests;

public sealed class DatabaseCleanupTargetTests
{
    [Fact]
    public async Task RefreshDiscardsStatisticsIfTheConnectionChangesDuringTheRead()
    {
        await WpfTestHost.Invoke(async () =>
        {
            var source = new SwitchingProvider();
            source.Original.OnLoad = () => source.Active = source.ReplacementTarget;
            var viewModel = new DatabaseCleanupSourceViewModel(source,
                (_, _) => throw new InvalidOperationException("Unpublished statistics cannot ask for confirmation."),
                (_, _) => { });

            await viewModel.RefreshAsync();

            Assert.Equal(["statistics"], source.Original.Calls.ToArray());
            Assert.Empty(source.Replacement.Calls);
            Assert.Empty(source.Calls);
            Assert.Empty(viewModel.Tables);
            Assert.Empty(viewModel.Description);
            Assert.False(viewModel.BackupCommand.CanExecute(null));
            Assert.False(viewModel.CleanupAllCommand.CanExecute(null));
            viewModel.CleanupAllCommand.Execute(null);

            await viewModel.RefreshAsync();

            Assert.Equal(source.Replacement.Description, viewModel.Description);
            Assert.Single(viewModel.Tables);
            Assert.Equal(["statistics"], source.Replacement.Calls.ToArray());
            Assert.True(viewModel.CleanupAllCommand.CanExecute(null));
        });
    }

    [Theory]
    [InlineData("history")]
    [InlineData("selected")]
    [InlineData("all")]
    [InlineData("optimization")]
    public async Task YesInTheConfirmationDialogCannotUseAReplacedTarget(string operation)
    {
        await WpfTestHost.Invoke(async () =>
        {
            var source = new SwitchingProvider();
            int confirmations = 0;
            var viewModel = new DatabaseCleanupSourceViewModel(source, (message, _) =>
            {
                Assert.StartsWith(source.Original.Description, message);
                Assert.DoesNotContain(source.Replacement.Description, message);
                confirmations++;
                source.Active = source.ReplacementTarget;
                return true;
            }, (_, _) => { });
            await viewModel.RefreshAsync();
            viewModel.BackupBeforeCleanup = true;
            Assert.Single(viewModel.Tables).IsSelected = true;
            Assert.Equal(source.Original.Description, viewModel.Description);

            ICommand command = operation switch
            {
                "selected" => viewModel.CleanupSelectedCommand,
                "history" => viewModel.CleanupHistoryCommand,
                "optimization" => viewModel.OptimizationCommand,
                _ => viewModel.CleanupAllCommand
            };
            Assert.True(command.CanExecute(null));
            command.Execute(null);

            Assert.Equal(1, confirmations);
            Assert.Equal(["statistics"], source.Original.Calls.ToArray());
            Assert.Empty(source.Replacement.Calls);
            Assert.Empty(source.Calls);
            Assert.Empty(viewModel.Tables);
            Assert.False(command.CanExecute(null));
            Assert.False(viewModel.BackupCommand.CanExecute(null));
            command.Execute(null);
            Assert.Equal(1, confirmations);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BackupAndCleanupUseTheConfirmedPlanAndRecheckAfterBackup(bool switchDuringBackup)
    {
        await WpfTestHost.Invoke(async () =>
        {
            var source = new SwitchingProvider();
            if (switchDuringBackup)
                source.Original.OnBackup = () => source.Active = source.ReplacementTarget;
            var messageShown = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            DatabaseCleanupSourceViewModel? viewModel = null;
            viewModel = new DatabaseCleanupSourceViewModel(source, (_, _) =>
            {
                viewModel!.BackupBeforeCleanup = false;
                return true;
            }, (message, _) => messageShown.TrySetResult(message));
            await viewModel.RefreshAsync();
            viewModel.BackupBeforeCleanup = true;

            viewModel.CleanupHistoryCommand.Execute(null);

            string message = await messageShown.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Contains(source.Original.Description, message);
            Assert.Empty(source.Replacement.Calls);
            Assert.Empty(source.Calls);
            Assert.Equal(1, source.CaptureCount);
            if (switchDuringBackup)
            {
                Assert.Equal(["statistics", "backup"], source.Original.Calls.ToArray());
                Assert.Empty(viewModel.Tables);
            }
            else
            {
                Assert.Equal(["statistics", "backup", "history:3", "statistics"], source.Original.Calls.ToArray());
                Assert.Equal(source.Original.Description, viewModel.Description);
                Assert.Single(viewModel.Tables);
            }
        });
    }

    private sealed class SwitchingProvider : RecordingProvider, IDatabaseCleanupTargetProvider
    {
        private DatabaseCleanupTarget? _active;
        public RecordingProvider Original { get; } = new("mysql-a.example:3311 / previous_db");
        public RecordingProvider Replacement { get; } = new("mysql-b.example:3322 / replacement_db");
        public DatabaseCleanupTarget ReplacementTarget { get; }
        public int CaptureCount { get; private set; }
        public DatabaseCleanupTarget? Active { get => Volatile.Read(ref _active); set => Volatile.Write(ref _active, value); }

        public SwitchingProvider() : base("dynamic provider")
        {
            var original = new DatabaseCleanupTarget(Original, Original.Description, () => ReferenceEquals(Active?.Provider, Original));
            ReplacementTarget = new DatabaseCleanupTarget(Replacement, Replacement.Description, () => ReferenceEquals(Active?.Provider, Replacement));
            Active = original;
        }

        public DatabaseCleanupTarget? CaptureCurrentTarget()
        {
            CaptureCount++;
            return Active;
        }
    }

    private class RecordingProvider(string description) : IDatabaseCleanupSourceProvider, IDatabaseCleanupSelectionProvider,
        IDatabaseCleanupBackupProvider, IDatabaseCleanupMaintenanceProvider, IDatabaseCleanupOptimizationProvider
    {
        public string Id => description;
        public string DisplayName => "test database source";
        public string Description => description;
        public int Order => 1;
        public string OptimizationActionName => "Optimize";
        public string OptimizationConfirmationMessage => "Optimize confirmed target?";
        public ConcurrentQueue<string> Calls { get; } = new();
        public Action? OnLoad { get; set; }
        public Action? OnBackup { get; set; }

        public IReadOnlyList<DatabaseCleanupTableInfo> LoadTables()
        {
            Calls.Enqueue("statistics");
            OnLoad?.Invoke();
            return [new DatabaseCleanupTableInfo { TableName = "test_measurements", Exists = true, RowCount = 12 }];
        }

        public DatabaseCleanupExecutionResult CleanupHistory(int keepMonths) => Record($"history:{keepMonths}");
        public DatabaseCleanupExecutionResult CleanupAll() => Record("all");
        public DatabaseCleanupExecutionResult CleanupTables(IReadOnlyCollection<string> tableNames) => Record("selected");
        public DatabaseCleanupExecutionResult ExecuteOptimization() => Record("optimization");

        public DatabaseCleanupBackupResult CreateBackup()
        {
            Calls.Enqueue("backup");
            OnBackup?.Invoke();
            return new DatabaseCleanupBackupResult { FilePath = "fixture.sql", StatusMessage = "backup recorded" };
        }

        public DatabaseCleanupMaintenanceResult ExecuteCleanupWithBackup(Func<DatabaseCleanupExecutionResult> cleanupAction) =>
            new() { Backup = CreateBackup(), Cleanup = cleanupAction() };

        private DatabaseCleanupExecutionResult Record(string call)
        {
            Calls.Enqueue(call);
            return new DatabaseCleanupExecutionResult { StatusMessage = call };
        }
    }
}
