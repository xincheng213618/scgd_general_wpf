using System.Collections.ObjectModel;
using System.Windows;

namespace ProjectKB
{
    public partial class KBProductionStatisticsWindow : Window
    {
        private const string AllModelsText = "全部机种";
        private readonly ObservableCollection<KBHourlyProductionRow> _hourlyRows = [];
        private readonly ObservableCollection<KBDailyProductionRow> _dailyRows = [];
        private readonly ObservableCollection<KBProductionSessionRow> _sessionRows = [];
        private int _loadVersion;

        public KBProductionStatisticsWindow()
        {
            InitializeComponent();
            StartDatePicker.SelectedDate = DateTime.Today;
            EndDatePicker.SelectedDate = DateTime.Today;
            HourlyGrid.ItemsSource = _hourlyRows;
            DailyGrid.ItemsSource = _dailyRows;
            SessionGrid.ItemsSource = _sessionRows;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadModelsAsync();
            await RefreshAsync();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await RefreshAsync();
        }

        private async void EditProductionSettings_Click(object sender, RoutedEventArgs e)
        {
            SummaryManager.GetInstance().Edit();
            await RefreshAsync();
        }

        private async Task LoadModelsAsync()
        {
            string? selectedModel = ModelFilter.SelectedItem as string;
            IReadOnlyList<string> models = await Task.Run(KBProductionDataStore.Instance.QueryModels);
            ModelFilter.Items.Clear();
            ModelFilter.Items.Add(AllModelsText);
            foreach (string model in models)
                ModelFilter.Items.Add(model);

            ModelFilter.SelectedItem = selectedModel != null && ModelFilter.Items.Contains(selectedModel)
                ? selectedModel
                : AllModelsText;
        }

        private async Task RefreshAsync()
        {
            DateTime from = (StartDatePicker.SelectedDate ?? DateTime.Today).Date;
            DateTime endDate = (EndDatePicker.SelectedDate ?? from).Date;
            if (endDate < from)
            {
                MessageBox.Show(this, "结束日期不能早于开始日期。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DateTime toExclusive = endDate.AddDays(1);
            string? selectedModel = ModelFilter.SelectedItem as string;
            string? model = string.Equals(selectedModel, AllModelsText, StringComparison.Ordinal) ? null : selectedModel;
            int loadVersion = ++_loadVersion;
            RefreshButton.IsEnabled = false;
            QuerySummaryText.Text = "正在读取生产数据...";

            try
            {
                KBProductionStatistics statistics = await Task.Run(() =>
                    KBProductionDataStore.Instance.QueryStatistics(from, toExclusive, model, DateTime.Now));
                if (loadVersion != _loadVersion)
                    return;

                ApplyStatistics(statistics);
                QuerySummaryText.Text = $"{from:yyyy/MM/dd} - {endDate:yyyy/MM/dd} · {model ?? AllModelsText}";
            }
            catch (Exception ex)
            {
                if (loadVersion == _loadVersion)
                {
                    QuerySummaryText.Text = "统计失败";
                    MessageBox.Show(this, $"读取生产统计失败：{ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                if (loadVersion == _loadVersion)
                    RefreshButton.IsEnabled = true;
            }
        }

        private void ApplyStatistics(KBProductionStatistics statistics)
        {
            TargetProductionText.Text = statistics.TargetProduction.ToString("N0");
            ProductionCountText.Text = statistics.ProductionCount.ToString("N0");
            GoodDefectiveText.Text = $"{statistics.GoodCount:N0} / {statistics.DefectiveCount:N0}";
            GoodRateText.Text = statistics.GoodRateText;
            ExecutionFailureText.Text = statistics.ExecutionFailureCount.ToString("N0");
            AverageCtText.Text = statistics.AverageCtText;
            CtRangeText.Text = $"{statistics.MinimumCtText} - {statistics.MaximumCtText}";
            CurrentHourProductionText.Text = statistics.CurrentHourProduction.ToString("N0");
            TodayProductionText.Text = statistics.TodayProduction.ToString("N0");
            TotalRunsText.Text = statistics.TotalRuns.ToString("N0");

            ReplaceRows(_hourlyRows, statistics.HourlyRows);
            ReplaceRows(_dailyRows, statistics.DailyRows);
            ReplaceRows(_sessionRows, statistics.SessionRows);
        }

        private static void ReplaceRows<T>(ObservableCollection<T> target, IEnumerable<T> source)
        {
            target.Clear();
            foreach (T item in source)
                target.Add(item);
        }
    }
}
