using ColorVision.Database;
using System.Windows;

namespace ProjectLUX;

public partial class ResultStatisticsWindow : Window
{
    private const int PageSize = 1000;
    private readonly ResultStatisticsDataStore _store = ResultStatisticsDataStore.Instance;
    private int _page = 1;
    private int _pageCount = 1;
    private int _loadVersion;
    private readonly bool _refreshOnLoad;

    public ResultStatisticsWindow() : this(refreshOnLoad: true) { }

    internal ResultStatisticsWindow(bool refreshOnLoad)
    {
        _refreshOnLoad = refreshOnLoad;
        InitializeComponent();
        Anchor.SelectedDate = DateTime.Today;
        Closed += (_, _) => _loadVersion++;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_refreshOnLoad) await RefreshAsync(1);
    }
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync(1);
    private async void Previous_Click(object sender, RoutedEventArgs e) => await RefreshAsync(Math.Max(1, _page - 1));
    private async void Next_Click(object sender, RoutedEventArgs e) => await RefreshAsync(Math.Min(_pageCount, _page + 1));

    private async Task RefreshAsync(int page)
    {
        int version = ++_loadVersion;
        var mode = (ResultStatisticsPeriodMode)Math.Max(0, Period.SelectedIndex);
        var range = ResultStatisticsPeriod.GetRange(mode, Anchor.SelectedDate ?? DateTime.Today);
        var query = new ResultStatisticsQuery
        {
            From = range.From,
            ToExclusive = range.ToExclusive,
            SN = SnFilter.Text,
            Result = ResultFilter.SelectedIndex switch { 1 => true, 2 => false, _ => null },
            PageNumber = page,
            PageSize = PageSize,
        };
        Status.Text = "正在读取统计...";
        try
        {
            var data = await Task.Run(() => ResultJsonPayloadStorage.RunDatabaseMaintenance(() =>
                (Dashboard: _store.QueryDashboard(query, mode, DateTime.Now),
                 Records: _store.QueryRecords(query), Count: _store.QueryRecordCount(query))));
            if (version != _loadVersion) return;
            SummaryPanel.DataContext = data.Dashboard.Summary;
            Trend.ItemsSource = data.Dashboard.Trend;
            Records.ItemsSource = data.Records;
            _page = page;
            _pageCount = Math.Max(1, (data.Count + PageSize - 1) / PageSize);
            PageText.Text = $"{_page} / {_pageCount} 页 · 共 {data.Count:N0} 条";
            Status.Text = $"{range.ToDisplayText(mode)} · 最短 CT {data.Dashboard.Summary.MinimumCtText} · 最长 CT {data.Dashboard.Summary.MaximumCtText}";
        }
        catch (Exception ex)
        {
            if (version != _loadVersion) return;
            SummaryPanel.DataContext = null;
            Records.ItemsSource = null;
            Trend.ItemsSource = null;
            Status.Text = $"读取统计失败：{ex.Message}";
        }
    }

    private async void ViewResult_Click(object sender, RoutedEventArgs e)
    {
        if (Records.SelectedItem is not ResultStatisticsRecordRow row) return;
        try
        {
            var record = await Task.Run(() => ResultJsonPayloadStorage.RunDatabaseMaintenance(() => _store.GetRecord(row.Id)));
            if (!IsLoaded || Records.SelectedItem != row) return;
            if (string.IsNullOrEmpty(record?.ObjectiveTestResultJson))
            {
                MessageBox.Show(this, "该记录没有可查看的测试结果。", "ColorVision");
                return;
            }
            new TestResultViewWindow(record.ObjectiveTestResultJson)
            {
                Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog();
        }
        catch (Exception ex)
        {
            if (IsLoaded) MessageBox.Show(this, $"读取结果失败：{ex.Message}", "ColorVision");
        }
    }

    private void Maintenance_Click(object sender, RoutedEventArgs e)
    {
        DatabaseCleanupWindow.OpenWindow(this, new LuxSqliteCleanupProvider());
    }
}
