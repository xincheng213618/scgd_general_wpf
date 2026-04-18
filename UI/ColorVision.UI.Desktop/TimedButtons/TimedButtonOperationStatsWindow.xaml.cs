using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ColorVision.UI.Desktop.TimedButtons
{
    internal sealed class TimedButtonOperationStatsViewItem
    {
        public required string OperationKey { get; init; }
        public int SuccessCount { get; init; }
        public int WarmupCount { get; init; }
        public double WarmupElapsedMs { get; init; }
        public double LastElapsedMs { get; init; }
        public double AverageElapsedMs { get; init; }
        public double BestElapsedMs { get; init; }
        public double WorstElapsedMs { get; init; }
        public DateTime LastCompletedAt { get; init; }

        public string WarmupElapsedText => WarmupCount > 0 ? TimedButtonOperationTextFormatter.FormatDuration(WarmupElapsedMs) : "-";
        public string LastElapsedText => SuccessCount > 0 ? TimedButtonOperationTextFormatter.FormatDuration(LastElapsedMs) : "-";
        public string AverageElapsedText => SuccessCount > 0 ? TimedButtonOperationTextFormatter.FormatDuration(AverageElapsedMs) : "-";
        public string BestElapsedText => SuccessCount > 0 ? TimedButtonOperationTextFormatter.FormatDuration(BestElapsedMs) : "-";
        public string WorstElapsedText => SuccessCount > 0 ? TimedButtonOperationTextFormatter.FormatDuration(WorstElapsedMs) : "-";
        public string LastCompletedAtText => LastCompletedAt == default ? "-" : LastCompletedAt.ToString("yyyy-MM-dd HH:mm:ss");
    }

    internal sealed class TimedButtonOperationStatsExportRow
    {
        public string OperationKey { get; set; } = string.Empty;
        public string WarmupElapsed { get; set; } = string.Empty;
        public string LastElapsed { get; set; } = string.Empty;
        public string AverageElapsed { get; set; } = string.Empty;
        public string BestElapsed { get; set; } = string.Empty;
        public string WorstElapsed { get; set; } = string.Empty;
        public int SuccessCount { get; set; }
        public int WarmupCount { get; set; }
        public string LastCompletedAt { get; set; } = string.Empty;
    }

    public partial class TimedButtonOperationStatsWindow : Window
    {
        private readonly ObservableCollection<TimedButtonOperationStatsViewItem> _items = new ObservableCollection<TimedButtonOperationStatsViewItem>();
        private readonly string? _initialOperationKey;

        public TimedButtonOperationStatsWindow(string? initialOperationKey = null)
        {
            _initialOperationKey = initialOperationKey?.Trim();
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            StatsDataGrid.ItemsSource = _items;
            TimedButtonOperationStatsManager.StatsChanged += TimedButtonOperationStatsManager_StatsChanged;
            if (!string.IsNullOrWhiteSpace(_initialOperationKey))
            {
                SearchTextBox.Text = _initialOperationKey;
            }
            LoadEntries();
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            TimedButtonOperationStatsManager.StatsChanged -= TimedButtonOperationStatsManager_StatsChanged;
        }

        private void TimedButtonOperationStatsManager_StatsChanged(object? sender, TimedButtonOperationStatsChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(LoadEntries));
        }

        private void LoadEntries()
        {
            string keyword = SearchTextBox.Text?.Trim() ?? string.Empty;
            DateTime? startDate = StartDatePicker.SelectedDate?.Date;
            DateTime? endDateExclusive = EndDatePicker.SelectedDate?.Date.AddDays(1);
            IReadOnlyList<TimedButtonOperationStatsEntry> allEntries = TimedButtonOperationStatsManager.GetAll();

            IEnumerable<TimedButtonOperationStatsEntry> filteredEntries = allEntries;
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                filteredEntries = filteredEntries.Where(item => item.OperationKey.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            }

            if (startDate.HasValue)
            {
                filteredEntries = filteredEntries.Where(item => item.Stats.LastCompletedAt >= startDate.Value);
            }

            if (endDateExclusive.HasValue)
            {
                filteredEntries = filteredEntries.Where(item => item.Stats.LastCompletedAt < endDateExclusive.Value);
            }

            List<TimedButtonOperationStatsViewItem> viewItems = filteredEntries
                .OrderByDescending(item => item.Stats.LastCompletedAt)
                .ThenBy(item => item.OperationKey, StringComparer.Ordinal)
                .Select(item => new TimedButtonOperationStatsViewItem
                {
                    OperationKey = item.OperationKey,
                    SuccessCount = item.Stats.SuccessCount,
                    WarmupCount = item.Stats.WarmupCount,
                    WarmupElapsedMs = item.Stats.WarmupElapsedMs,
                    LastElapsedMs = item.Stats.LastElapsedMs,
                    AverageElapsedMs = item.Stats.AverageElapsedMs,
                    BestElapsedMs = item.Stats.BestElapsedMs,
                    WorstElapsedMs = item.Stats.WorstElapsedMs,
                    LastCompletedAt = item.Stats.LastCompletedAt
                })
                .ToList();

            _items.Clear();
            foreach (TimedButtonOperationStatsViewItem viewItem in viewItems)
            {
                _items.Add(viewItem);
            }

            int totalSuccess = viewItems.Sum(item => item.SuccessCount);
            int totalWarmup = viewItems.Sum(item => item.WarmupCount);
            SummaryText.Text = $"共 {allEntries.Count} 个操作，当前显示 {viewItems.Count} 条，稳定样本 {totalSuccess}，预热样本 {totalWarmup}";

            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            DeleteSelectedButton.IsEnabled = StatsDataGrid.SelectedItems.Count > 0;
            ClearButton.IsEnabled = _items.Count > 0;
            ExportButton.IsEnabled = _items.Count > 0;
        }

        private void Query_Click(object sender, RoutedEventArgs e)
        {
            LoadEntries();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadEntries();
        }

        public void ApplySearchFilter(string? operationKey)
        {
            SearchTextBox.Text = operationKey?.Trim() ?? string.Empty;
            LoadEntries();
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            LoadEntries();
            e.Handled = true;
        }

        private void StatsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtonStates();
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = string.Empty;
            StartDatePicker.SelectedDate = null;
            EndDatePicker.SelectedDate = null;
            LoadEntries();
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_items.Count == 0)
            {
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "CSV 文件|*.csv",
                FileName = $"TimedButtonStats_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            List<TimedButtonOperationStatsExportRow> rows = _items
                .Select(item => new TimedButtonOperationStatsExportRow
                {
                    OperationKey = item.OperationKey,
                    WarmupElapsed = item.WarmupElapsedText,
                    LastElapsed = item.LastElapsedText,
                    AverageElapsed = item.AverageElapsedText,
                    BestElapsed = item.BestElapsedText,
                    WorstElapsed = item.WorstElapsedText,
                    SuccessCount = item.SuccessCount,
                    WarmupCount = item.WarmupCount,
                    LastCompletedAt = item.LastCompletedAtText
                })
                .ToList();

            CsvWriter.WriteToCsv(rows, dialog.FileName);
            MessageBox.Show(Application.Current.GetActiveWindow(), $"已导出 {_items.Count} 条耗时统计到:\n{dialog.FileName}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            List<TimedButtonOperationStatsViewItem> selectedItems = StatsDataGrid.SelectedItems
                .OfType<TimedButtonOperationStatsViewItem>()
                .ToList();

            if (selectedItems.Count == 0)
            {
                return;
            }

            string message = selectedItems.Count == 1
                ? $"确定删除操作 {selectedItems[0].OperationKey} 的耗时统计吗？"
                : $"确定删除当前选中的 {selectedItems.Count} 条耗时统计吗？";

            MessageBoxResult result = MessageBox.Show(Application.Current.GetActiveWindow(), message, "ColorVision", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            int deletedCount = 0;
            foreach (TimedButtonOperationStatsViewItem selectedItem in selectedItems)
            {
                if (TimedButtonOperationStatsManager.Delete(selectedItem.OperationKey))
                {
                    deletedCount++;
                }
            }

            if (deletedCount > 0)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"已删除 {deletedCount} 条耗时统计。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (_items.Count == 0)
            {
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                Application.Current.GetActiveWindow(),
                $"确定清空全部 {_items.Count} 条耗时统计吗？",
                "ColorVision",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            int cleared = TimedButtonOperationStatsManager.Clear();
            MessageBox.Show(Application.Current.GetActiveWindow(), $"已清空 {cleared} 条耗时统计。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}