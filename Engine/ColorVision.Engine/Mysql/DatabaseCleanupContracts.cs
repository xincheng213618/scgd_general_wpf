using ColorVision.Common.MVVM;
using ColorVision.Engine;
using System;
using System.Collections.Generic;

namespace ColorVision.Database
{
    public sealed class DatabaseCleanupTableInfo : ViewModelBase
    {
        private string _tableName = string.Empty;
        private bool _exists;
        private bool _isSelected;
        private long _rowCount;
        private long _sizeBytes;

        public string TableName
        {
            get => _tableName;
            set
            {
                _tableName = value;
                OnPropertyChanged();
            }
        }

        public bool Exists
        {
            get => _exists;
            set
            {
                _exists = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ExistsDisplay));
                OnPropertyChanged(nameof(RowCountDisplay));
                OnPropertyChanged(nameof(SizeDisplay));
                OnPropertyChanged(nameof(CanSelect));

                if (!value)
                {
                    IsSelected = false;
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                bool nextValue = value && Exists;
                if (_isSelected == nextValue)
                    return;

                _isSelected = nextValue;
                OnPropertyChanged();
            }
        }

        public long RowCount
        {
            get => _rowCount;
            set
            {
                _rowCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RowCountDisplay));
            }
        }

        public long SizeBytes
        {
            get => _sizeBytes;
            set
            {
                _sizeBytes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SizeDisplay));
            }
        }

        public string ExistsDisplay => Exists ? EngineLocalization.Get("存在") : EngineLocalization.Get("未找到");
        public bool CanSelect => Exists;
        public string RowCountDisplay => Exists ? RowCount.ToString("N0") : "-";
        public string SizeDisplay => Exists ? FormatSize(SizeBytes) : "-";

        private static string FormatSize(long bytes)
        {
            if (bytes <= 0)
                return "0 B";

            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int unitIndex = 0;

            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return $"{size:0.##} {units[unitIndex]}";
        }
    }

    public sealed class DatabaseCleanupExecutionResult
    {
        public string StatusMessage { get; set; } = string.Empty;
        public List<string> SummaryLines { get; } = new();
    }

    public sealed class DatabaseCleanupBackupResult
    {
        public string StatusMessage { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
    }

    public sealed class DatabaseCleanupMaintenanceResult
    {
        public DatabaseCleanupBackupResult Backup { get; set; } = new();
        public DatabaseCleanupExecutionResult Cleanup { get; set; } = new();
    }

    public interface IDatabaseCleanupSourceProvider
    {
        string Id { get; }
        string DisplayName { get; }
        string Description { get; }
        int Order { get; }
        IReadOnlyList<DatabaseCleanupTableInfo> LoadTables();
        DatabaseCleanupExecutionResult CleanupHistory(int keepMonths);
        DatabaseCleanupExecutionResult CleanupAll();
    }

    /// <summary>
    /// Optional capability for cleanup sources that can clear an explicit table selection.
    /// </summary>
    public interface IDatabaseCleanupSelectionProvider
    {
        DatabaseCleanupExecutionResult CleanupTables(IReadOnlyCollection<string> tableNames);
    }

    /// <summary>
    /// Optional capability for cleanup sources that can create a recoverable backup.
    /// </summary>
    public interface IDatabaseCleanupBackupProvider
    {
        DatabaseCleanupBackupResult CreateBackup();
    }

    /// <summary>
    /// Optional capability for running backup and cleanup under one source-specific maintenance gate.
    /// </summary>
    public interface IDatabaseCleanupMaintenanceProvider
    {
        DatabaseCleanupMaintenanceResult ExecuteCleanupWithBackup(Func<DatabaseCleanupExecutionResult> cleanupAction);
    }

    /// <summary>
    /// Optional capability for a source-specific, manually triggered data migration.
    /// </summary>
    public interface IDatabaseCleanupMigrationProvider
    {
        string MigrationActionName { get; }
        string MigrationConfirmationMessage { get; }
        DatabaseCleanupExecutionResult ExecuteMigration();
    }
}
