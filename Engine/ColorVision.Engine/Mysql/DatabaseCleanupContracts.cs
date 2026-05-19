using ColorVision.Common.MVVM;
using System.Collections.Generic;

namespace ColorVision.Database
{
    public sealed class DatabaseCleanupTableInfo : ViewModelBase
    {
        private string _tableName = string.Empty;
        private bool _exists;
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

        public string ExistsDisplay => Exists ? "存在" : "未找到";
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
}