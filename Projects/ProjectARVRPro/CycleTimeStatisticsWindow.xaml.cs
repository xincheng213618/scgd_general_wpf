using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Solution.Editor.AvalonEditor;
using ColorVision.UI;
using ColorVision.Themes;
using log4net;
using Microsoft.Win32;
using Newtonsoft.Json;
using ProjectARVRPro.LegacyARVR;
using SqlSugar;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ProjectARVRPro
{
    public partial class CycleTimeStatisticsWindow : Window
    {
        private const int RecordPageSize = 1000;
        private const int FlowPageSize = 1000;
        private const int SuggestionDisplayLimit = 20;
        private static readonly ILog Log = LogManager.GetLogger(typeof(CycleTimeStatisticsWindow));
        private readonly ViewResultManager? _viewResultManager;
        private readonly ResultStatisticsDataStore _statisticsStore;
        private readonly ResultStatisticsWindowState _windowState;
        private readonly Offline.ArvrOfflineDataSource? _offlineSource;
        private bool _openingOffline;
        private bool _closed;
        private readonly ObservableCollection<ResultStatisticsRecordRow> _recordRows = [];
        private readonly ObservableCollection<ResultStatisticsCombinedRecordRow> _combinedRows = [];
        private readonly ObservableCollection<FlowExecutionRecordRow> _flowRows = [];
        private string[] _snSuggestions = [];
        private string[] _flowNameSuggestions = [];
        private readonly ObservableCollection<ProjectARVRReuslt> _details = [];
        private readonly ObservableCollection<ProjectARVRReuslt> _combinedDetails = [];
        private readonly Dictionary<int, ObjectiveTestResultRecord> _recordCache = [];
        private TextBox? _snEditor;
        private TextBox? _flowNameEditor;
        private int _homeLoadVersion;
        private int _recordLoadVersion;
        private int _combinedLoadVersion;
        private int _flowLoadVersion;
        private int _snIndexVersion;
        private int _flowNameIndexVersion;
        private int _detailLoadVersion;
        private int _combinedDetailLoadVersion;
        private int _currentPage = 1;
        private int _totalRecordCount;
        private int _combinedCurrentPage = 1;
        private int _totalCombinedCount;
        private int _flowCurrentPage = 1;
        private int _totalFlowCount;
        private bool _updatingSnSuggestions;
        private bool _updatingFlowNameSuggestions;
        private bool _flowTabInitialized;
        private bool _windowLoaded;
        private bool _restoringSearchState = true;
        private int _queuedHomeRefreshVersion;
        private string _homeStatus = string.Empty;
        private string _recordStatus = string.Empty;
        private string _combinedStatus = string.Empty;
        private string _snIndexStatus = string.Empty;
        private string _flowStatus = string.Empty;
        private string _flowNameIndexStatus = string.Empty;

        public CycleTimeStatisticsWindow() : this(null) { }

        public CycleTimeStatisticsWindow(Offline.ArvrOfflineDataSource? offlineSource)
        {
            _offlineSource = offlineSource;
            if (offlineSource == null)
            {
                _viewResultManager = ViewResultManager.GetInstance();
                _statisticsStore = ResultStatisticsDataStore.Instance;
                _windowState = ProjectARVRProConfig.Instance.ResultStatisticsWindowState ??= new();
            }
            else
            {
                _statisticsStore = offlineSource.Statistics;
                _windowState = new ResultStatisticsWindowState
                {
                    HomeAnchorDate = offlineSource.LatestDate, RecordAnchorDate = offlineSource.LatestDate,
                    FlowAnchorDate = offlineSource.LatestDate, SelectedTabIndex = 1,
                };
            }
            InitializeComponent();
            this.ApplyCaption();
            if (offlineSource != null)
            {
                Title = $"结果统计 · {offlineSource.Label} · 只读";
                DataSourceText.Text = offlineSource.Description;
                DataSourceText.ToolTip = $"来源：{offlineSource.SourcePath}\n读取副本：{offlineSource.DirectoryPath}\n各数据库是独立快照；图片未随记录自动导入。";
                OfflineMessagesButton.Visibility = Visibility.Visible;
            }
            RestoreSearchState();
            RecordDataGrid.ItemsSource = _recordRows;
            CombinedRecordDataGrid.ItemsSource = _combinedRows;
            FlowDataGrid.ItemsSource = _flowRows;
            DetailList.ItemsSource = _details;
            CombinedDetailList.ItemsSource = _combinedDetails;
            TimelinePanel.DataContext = CreateEmptyTimeline("选择左侧批次后显示整组时间轴。");
            CombinedTimelinePanel.DataContext = CreateEmptyTimeline("选择左侧全批次后显示 L/R 时间轴。");
            RecordDataGrid.SelectionChanged += RecordDataGrid_SelectionChanged;
            CombinedRecordDataGrid.SelectionChanged += CombinedRecordDataGrid_SelectionChanged;
            BuildDetailContextMenu();
            ConfigureHomeTrendPlot();
            ApplyStatistics(new ResultStatistics());
            ApplyCombinedStatistics(new ResultStatisticsCombinedDashboard());
            ApplyCombinedVisibility();
            _restoringSearchState = false;
            UpdateHomePeriodText();
            UpdateRecordPeriodText();
            UpdateCombinedPeriodText();
            UpdateFlowPeriodText();
        }

        private ResultStatisticsRecordRow? SelectedRecordRow => RecordDataGrid.SelectedItem as ResultStatisticsRecordRow;
        private ResultStatisticsCombinedRecordRow? SelectedCombinedRow => CombinedRecordDataGrid.SelectedItem as ResultStatisticsCombinedRecordRow;
        private FlowExecutionRecordRow? SelectedFlowRow => FlowDataGrid.SelectedItem as FlowExecutionRecordRow;
        private bool IsCombinedStatisticsEnabled => EnableCombinedStatisticsCheckBox.IsChecked == true;

        private void StatisticsSettings_Click(object sender, RoutedEventArgs e)
        {
            StatisticsSettingsPopup.IsOpen = !StatisticsSettingsPopup.IsOpen;
        }

        private async void EnableCombinedStatistics_Changed(object sender, RoutedEventArgs e)
        {
            ApplyCombinedVisibility();
            CaptureSearchState();
            if (_restoringSearchState || !_windowLoaded)
                return;

            ++_homeLoadVersion;
            if (IsCombinedStatisticsEnabled)
                await Task.WhenAll(RefreshHomeAsync(), RefreshCombinedRecordsAsync(1));
            else
            {
                ++_combinedLoadVersion;
                ++_combinedDetailLoadVersion;
                if (StatisticsTabs.SelectedItem == CombinedRecordTab)
                    StatisticsTabs.SelectedIndex = 1;
                await RefreshHomeAsync();
            }
        }

        private void ApplyCombinedVisibility()
        {
            if (CombinedRecordTab == null || CombinedSummaryPanel == null || CombinedSummaryLabel == null || CombinedSummaryRow == null)
                return;

            Visibility visibility = IsCombinedStatisticsEnabled ? Visibility.Visible : Visibility.Collapsed;
            CombinedRecordTab.Visibility = visibility;
            CombinedSummaryPanel.Visibility = visibility;
            CombinedSummaryLabel.Visibility = visibility;
            CombinedSummaryRow.Height = IsCombinedStatisticsEnabled ? GridLength.Auto : new GridLength(0);
            StatisticsSettingsButton.Content = IsCombinedStatisticsEnabled ? "统计设置 · L/R 已开启" : "统计设置";
        }

        private async void OpenOfflineData_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Title = "打开现场数据", Filter = "反馈包或 ARVRPro 数据库|*.zip;*.db|所有文件|*.*", CheckFileExists = true };
            if (dialog.ShowDialog(this) == true) await OpenOfflineAsync(dialog.FileName);
        }

        private async void OpenOfflineFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog { Title = "选择包含 ProjectARVRPro.db 或 Database 子目录的资料文件夹" };
            if (dialog.ShowDialog(this) == true) await OpenOfflineAsync(dialog.FolderName);
        }

        private async Task OpenOfflineAsync(string path)
        {
            if (_openingOffline || _closed) return;
            _openingOffline = true;
            string previous = DataSourceText.Text;
            DataSourceText.Text = "正在准备现场只读副本…";
            try
            {
                Offline.ArvrOfflineDataSource source = await Task.Run(() => Offline.ArvrOfflineDataSource.Open(path));
                if (_closed) return;
                new CycleTimeStatisticsWindow(source) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
            }
            catch (Exception ex) { if (!_closed) MessageBox.Show(this, $"打开现场数据失败：{ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { DataSourceText.Text = previous; _openingOffline = false; }
        }

        private void OfflineMessages_Click(object sender, RoutedEventArgs e)
        {
            if (_offlineSource == null) return;
            if (SelectedRecordRow is not ResultStatisticsRecordRow row)
            {
                MessageBox.Show(this, "请先在测试记录中选择一轮测试。", "现场消息");
                return;
            }
            new Offline.OfflineMessagesWindow(_offlineSource, row) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _windowLoaded = true;
            _ = LoadSnSuggestionsAsync();
            var tasks = new List<Task> { RefreshHomeAsync(), RefreshRecordsAsync(1) };
            if (IsCombinedStatisticsEnabled)
                tasks.Add(RefreshCombinedRecordsAsync(1));
            if (StatisticsTabs.SelectedItem == FlowQueryTab)
            {
                _flowTabInitialized = true;
                _ = LoadFlowNameSuggestionsAsync();
                tasks.Add(RefreshFlowsAsync(1));
            }
            await Task.WhenAll(tasks);
        }

        private async void StatisticsTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source != StatisticsTabs)
                return;

            CaptureSearchState();
            if (!_windowLoaded || StatisticsTabs.SelectedItem != FlowQueryTab || _flowTabInitialized)
                return;

            _flowTabInitialized = true;
            _ = LoadFlowNameSuggestionsAsync();
            await RefreshFlowsAsync(1);
        }

        private async void RecordRefresh_Click(object sender, RoutedEventArgs e)
        {
            CaptureSearchState();
            await RefreshRecordsAsync(1);
        }

        private async void CombinedRefresh_Click(object sender, RoutedEventArgs e)
        {
            CaptureSearchState();
            await RefreshCombinedRecordsAsync(1);
        }

        private async void FlowRefresh_Click(object sender, RoutedEventArgs e)
        {
            CaptureSearchState();
            await RefreshFlowsAsync(1);
        }

        private async void Reset_Click(object sender, RoutedEventArgs e)
        {
            RecordPeriodMode.SelectedIndex = 0;
            RecordAnchorDatePicker.SelectedDate = DateTime.Today;
            SnFilter.Text = string.Empty;
            ResultFilter.SelectedIndex = 0;
            UpdateRecordPeriodText();
            CaptureSearchState();
            await RefreshRecordsAsync(1);
        }

        private async void FlowReset_Click(object sender, RoutedEventArgs e)
        {
            FlowPeriodMode.SelectedIndex = 0;
            FlowAnchorDatePicker.SelectedDate = DateTime.Today;
            FlowNameFilter.Text = string.Empty;
            FlowResultFilter.SelectedIndex = 0;
            UpdateFlowPeriodText();
            CaptureSearchState();
            await RefreshFlowsAsync(1);
        }

        private async void CombinedReset_Click(object sender, RoutedEventArgs e)
        {
            CombinedPeriodMode.SelectedIndex = 0;
            CombinedAnchorDatePicker.SelectedDate = DateTime.Today;
            CombinedSnFilter.Text = string.Empty;
            CombinedResultFilter.SelectedIndex = 0;
            UpdateCombinedPeriodText();
            CaptureSearchState();
            await RefreshCombinedRecordsAsync(1);
        }

        private void HomePeriodMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateHomePeriodText();
            QueueHomeRefresh();
        }

        private void RecordPeriodMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateRecordPeriodText();
            CaptureSearchState();
        }

        private void FlowPeriodMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateFlowPeriodText();
            CaptureSearchState();
        }

        private void CombinedPeriodMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateCombinedPeriodText();
            CaptureSearchState();
        }

        private void HomeAnchorDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateHomePeriodText();
            QueueHomeRefresh();
        }

        private void RecordAnchorDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateRecordPeriodText();
            CaptureSearchState();
        }

        private void FlowAnchorDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateFlowPeriodText();
            CaptureSearchState();
        }

        private void CombinedAnchorDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateCombinedPeriodText();
            CaptureSearchState();
        }

        private void HomePreviousPeriod_Click(object sender, RoutedEventArgs e)
        {
            ShiftPeriod(HomePeriodMode, HomeAnchorDatePicker, -1);
        }

        private void HomeNextPeriod_Click(object sender, RoutedEventArgs e)
        {
            ShiftPeriod(HomePeriodMode, HomeAnchorDatePicker, 1);
        }

        private void HomeCurrentPeriod_Click(object sender, RoutedEventArgs e)
        {
            HomeAnchorDatePicker.SelectedDate = DateTime.Today;
            QueueHomeRefresh();
        }

        private async void RecordPreviousPeriod_Click(object sender, RoutedEventArgs e)
        {
            ShiftPeriod(RecordPeriodMode, RecordAnchorDatePicker, -1);
            await RefreshRecordsAsync(1);
        }

        private async void RecordNextPeriod_Click(object sender, RoutedEventArgs e)
        {
            ShiftPeriod(RecordPeriodMode, RecordAnchorDatePicker, 1);
            await RefreshRecordsAsync(1);
        }

        private async void RecordCurrentPeriod_Click(object sender, RoutedEventArgs e)
        {
            RecordAnchorDatePicker.SelectedDate = DateTime.Today;
            CaptureSearchState();
            await RefreshRecordsAsync(1);
        }

        private async void FlowPreviousPeriod_Click(object sender, RoutedEventArgs e)
        {
            ShiftPeriod(FlowPeriodMode, FlowAnchorDatePicker, -1);
            await RefreshFlowsAsync(1);
        }

        private async void FlowNextPeriod_Click(object sender, RoutedEventArgs e)
        {
            ShiftPeriod(FlowPeriodMode, FlowAnchorDatePicker, 1);
            await RefreshFlowsAsync(1);
        }

        private async void FlowCurrentPeriod_Click(object sender, RoutedEventArgs e)
        {
            FlowAnchorDatePicker.SelectedDate = DateTime.Today;
            CaptureSearchState();
            await RefreshFlowsAsync(1);
        }

        private async void CombinedPreviousPeriod_Click(object sender, RoutedEventArgs e)
        {
            ShiftPeriod(CombinedPeriodMode, CombinedAnchorDatePicker, -1);
            await RefreshCombinedRecordsAsync(1);
        }

        private async void CombinedNextPeriod_Click(object sender, RoutedEventArgs e)
        {
            ShiftPeriod(CombinedPeriodMode, CombinedAnchorDatePicker, 1);
            await RefreshCombinedRecordsAsync(1);
        }

        private async void CombinedCurrentPeriod_Click(object sender, RoutedEventArgs e)
        {
            CombinedAnchorDatePicker.SelectedDate = DateTime.Today;
            CaptureSearchState();
            await RefreshCombinedRecordsAsync(1);
        }

        private static void ShiftPeriod(ComboBox modeSelector, DatePicker anchorPicker, int offset)
        {
            ResultStatisticsPeriodMode mode = GetSelectedPeriodMode(modeSelector);
            DateTime anchor = anchorPicker.SelectedDate ?? DateTime.Today;
            anchorPicker.SelectedDate = ResultStatisticsPeriod.ShiftAnchor(mode, anchor, offset);
        }

        private void RestoreSearchState()
        {
            HomePeriodMode.SelectedIndex = GetPeriodModeIndex(_windowState.HomePeriodMode);
            HomeAnchorDatePicker.SelectedDate = NormalizeAnchorDate(_windowState.HomeAnchorDate);
            RecordPeriodMode.SelectedIndex = GetPeriodModeIndex(_windowState.RecordPeriodMode);
            RecordAnchorDatePicker.SelectedDate = NormalizeAnchorDate(_windowState.RecordAnchorDate);
            SnFilter.Text = _windowState.RecordSn ?? string.Empty;
            ResultFilter.SelectedIndex = Math.Clamp(_windowState.RecordResultIndex, 0, 2);
            EnableCombinedStatisticsCheckBox.IsChecked = _windowState.EnableCombinedStatistics;
            CombinedPeriodMode.SelectedIndex = GetPeriodModeIndex(_windowState.CombinedPeriodMode);
            CombinedAnchorDatePicker.SelectedDate = NormalizeAnchorDate(_windowState.CombinedAnchorDate);
            CombinedSnFilter.Text = _windowState.CombinedSn ?? string.Empty;
            CombinedResultFilter.SelectedIndex = Math.Clamp(_windowState.CombinedResultIndex, 0, 2);
            FlowPeriodMode.SelectedIndex = GetPeriodModeIndex(_windowState.FlowPeriodMode);
            FlowAnchorDatePicker.SelectedDate = NormalizeAnchorDate(_windowState.FlowAnchorDate);
            FlowNameFilter.Text = _windowState.FlowName ?? string.Empty;
            FlowResultFilter.SelectedIndex = Math.Clamp(_windowState.FlowResultIndex, 0, 2);
            int selectedTabIndex = Math.Clamp(_windowState.SelectedTabIndex, 0, StatisticsTabs.Items.Count - 1);
            // In builds before the combined page existed, index 2 meant the flow-query page.
            if (!IsCombinedStatisticsEnabled && selectedTabIndex == 2)
                selectedTabIndex = 3;
            StatisticsTabs.SelectedIndex = selectedTabIndex;
        }

        private void CaptureSearchState()
        {
            if (_restoringSearchState || StatisticsTabs == null)
                return;

            _windowState.SelectedTabIndex = Math.Max(0, StatisticsTabs.SelectedIndex);
            _windowState.HomePeriodMode = GetSelectedPeriodMode(HomePeriodMode);
            _windowState.HomeAnchorDate = (HomeAnchorDatePicker.SelectedDate ?? DateTime.Today).Date;
            _windowState.RecordPeriodMode = GetSelectedPeriodMode(RecordPeriodMode);
            _windowState.RecordAnchorDate = (RecordAnchorDatePicker.SelectedDate ?? DateTime.Today).Date;
            _windowState.RecordSn = SnFilter.Text?.Trim() ?? string.Empty;
            _windowState.RecordResultIndex = Math.Clamp(ResultFilter.SelectedIndex, 0, 2);
            _windowState.EnableCombinedStatistics = IsCombinedStatisticsEnabled;
            _windowState.CombinedPeriodMode = GetSelectedPeriodMode(CombinedPeriodMode);
            _windowState.CombinedAnchorDate = (CombinedAnchorDatePicker.SelectedDate ?? DateTime.Today).Date;
            _windowState.CombinedSn = CombinedSnFilter.Text?.Trim() ?? string.Empty;
            _windowState.CombinedResultIndex = Math.Clamp(CombinedResultFilter.SelectedIndex, 0, 2);
            _windowState.FlowPeriodMode = GetSelectedPeriodMode(FlowPeriodMode);
            _windowState.FlowAnchorDate = (FlowAnchorDatePicker.SelectedDate ?? DateTime.Today).Date;
            _windowState.FlowName = FlowNameFilter.Text?.Trim() ?? string.Empty;
            _windowState.FlowResultIndex = Math.Clamp(FlowResultFilter.SelectedIndex, 0, 2);
        }

        private void QueueHomeRefresh()
        {
            CaptureSearchState();
            if (_restoringSearchState || !_windowLoaded)
                return;

            ++_homeLoadVersion;
            int requestVersion = ++_queuedHomeRefreshVersion;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_windowLoaded && requestVersion == _queuedHomeRefreshVersion)
                    _ = RefreshHomeAsync();
            }));
        }

        private static DateTime NormalizeAnchorDate(DateTime value)
        {
            return value == default ? DateTime.Today : value.Date;
        }

        private void UpdateHomePeriodText()
        {
            if (HomePeriodText == null || HomePeriodMode == null || HomeAnchorDatePicker == null || HomeCurrentPeriodButton == null)
                return;

            ResultStatisticsPeriodMode mode = GetSelectedPeriodMode(HomePeriodMode);
            ResultStatisticsPeriodRange range = ResultStatisticsPeriod.GetRange(mode, HomeAnchorDatePicker.SelectedDate ?? DateTime.Today);
            HomePeriodText.Text = $"查询范围：{range.ToDisplayText(mode)}";
            HomeCurrentPeriodButton.Content = GetCurrentPeriodButtonText(mode);
            HomePeriodNavigation.Visibility = mode == ResultStatisticsPeriodMode.All ? Visibility.Collapsed : Visibility.Visible;
        }

        private void UpdateRecordPeriodText()
        {
            if (RecordPeriodText == null || RecordPeriodMode == null || RecordAnchorDatePicker == null || RecordCurrentPeriodButton == null)
                return;

            ResultStatisticsPeriodMode mode = GetSelectedPeriodMode(RecordPeriodMode);
            ResultStatisticsPeriodRange range = ResultStatisticsPeriod.GetRange(mode, RecordAnchorDatePicker.SelectedDate ?? DateTime.Today);
            RecordPeriodText.Text = $"查询范围：{range.ToDisplayText(mode)}";
            RecordCurrentPeriodButton.Content = GetCurrentPeriodButtonText(mode);
            RecordPeriodNavigation.Visibility = mode == ResultStatisticsPeriodMode.All ? Visibility.Collapsed : Visibility.Visible;
        }

        private void UpdateFlowPeriodText()
        {
            if (FlowPeriodText == null || FlowPeriodMode == null || FlowAnchorDatePicker == null || FlowCurrentPeriodButton == null)
                return;

            ResultStatisticsPeriodMode mode = GetSelectedPeriodMode(FlowPeriodMode);
            ResultStatisticsPeriodRange range = ResultStatisticsPeriod.GetRange(mode, FlowAnchorDatePicker.SelectedDate ?? DateTime.Today);
            FlowPeriodText.Text = $"查询范围：{range.ToDisplayText(mode)}";
            FlowCurrentPeriodButton.Content = GetCurrentPeriodButtonText(mode);
            FlowPeriodNavigation.Visibility = mode == ResultStatisticsPeriodMode.All ? Visibility.Collapsed : Visibility.Visible;
        }

        private void UpdateCombinedPeriodText()
        {
            if (CombinedPeriodText == null || CombinedPeriodMode == null || CombinedAnchorDatePicker == null || CombinedCurrentPeriodButton == null)
                return;

            ResultStatisticsPeriodMode mode = GetSelectedPeriodMode(CombinedPeriodMode);
            ResultStatisticsPeriodRange range = ResultStatisticsPeriod.GetRange(mode, CombinedAnchorDatePicker.SelectedDate ?? DateTime.Today);
            CombinedPeriodText.Text = $"查询范围：{range.ToDisplayText(mode)}";
            CombinedCurrentPeriodButton.Content = GetCurrentPeriodButtonText(mode);
            CombinedPeriodNavigation.Visibility = mode == ResultStatisticsPeriodMode.All ? Visibility.Collapsed : Visibility.Visible;
        }

        private static string GetCurrentPeriodButtonText(ResultStatisticsPeriodMode mode)
        {
            return mode switch
            {
                ResultStatisticsPeriodMode.Week => "本周",
                ResultStatisticsPeriodMode.Month => "本月",
                ResultStatisticsPeriodMode.All => "全部",
                _ => "今天",
            };
        }

        private static ResultStatisticsPeriodMode GetSelectedPeriodMode(ComboBox selector)
        {
            return selector.SelectedIndex switch
            {
                1 => ResultStatisticsPeriodMode.Week,
                2 => ResultStatisticsPeriodMode.Month,
                3 => ResultStatisticsPeriodMode.All,
                _ => ResultStatisticsPeriodMode.Day,
            };
        }

        private static int GetPeriodModeIndex(ResultStatisticsPeriodMode mode)
        {
            return mode switch
            {
                ResultStatisticsPeriodMode.Week => 1,
                ResultStatisticsPeriodMode.Month => 2,
                ResultStatisticsPeriodMode.All => 3,
                _ => 0,
            };
        }

        private async Task RefreshHomeAsync()
        {
            ResultStatisticsQuery query = CreateHomeQuery();
            ResultStatisticsPeriodMode mode = GetSelectedPeriodMode(HomePeriodMode);
            int loadVersion = ++_homeLoadVersion;
            _homeStatus = "正在查询统计...";
            UpdateStatusText();

            try
            {
                DateTime now = DateTime.Now;
                Task<ResultStatisticsDashboard> dashboardTask = Task.Run(() => _statisticsStore.QueryDashboard(query, mode, now));
                Task<ResultStatisticsCombinedDashboard>? combinedTask = IsCombinedStatisticsEnabled
                    ? Task.Run(() => _statisticsStore.QueryCombinedDashboard(query, mode, now))
                    : null;
                ResultStatisticsDashboard dashboard = await dashboardTask;
                ResultStatisticsCombinedDashboard? combined = combinedTask == null ? null : await combinedTask;

                if (loadVersion != _homeLoadVersion)
                    return;

                ApplyStatistics(dashboard.Summary);
                ApplyCombinedStatistics(combined ?? new ResultStatisticsCombinedDashboard());
                RenderHomeTrend(combined?.Trend ?? dashboard.Trend, mode, query.From, query.ToExclusive, combined != null);
                _homeStatus = combined == null
                    ? $"已查询 {dashboard.Summary.TotalCount:N0} 条单侧记录"
                    : $"已查询 {dashboard.Summary.TotalCount:N0} 条单侧记录；匹配 {combined.Summary.TotalCount:N0} 个 L/R 全批次";
            }
            catch (Exception ex)
            {
                if (loadVersion != _homeLoadVersion)
                    return;

                ApplyStatistics(new ResultStatistics());
                ApplyCombinedStatistics(new ResultStatisticsCombinedDashboard());
                RenderHomeTrend([], mode, query.From, query.ToExclusive, IsCombinedStatisticsEnabled);
                _homeStatus = "查询失败";
                MessageBox.Show(this, $"读取首页统计失败：{ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (loadVersion == _homeLoadVersion)
                    UpdateStatusText();
            }
        }

        private async Task RefreshRecordsAsync(int pageNumber)
        {
            ResultStatisticsQuery query = CreateRecordQuery(pageNumber);
            int loadVersion = ++_recordLoadVersion;
            RecordRefreshButton.IsEnabled = false;
            _recordStatus = "正在查询批次记录...";
            UpdateStatusText();
            DetailHeader.Text = "流程 CT 明细";
            _details.Clear();
            TimelinePanel.DataContext = CreateEmptyTimeline("正在查询批次记录...");

            try
            {
                Task<int> countTask = Task.Run(() => _statisticsStore.QueryRecordCount(query));
                Task<IReadOnlyList<ResultStatisticsRecordRow>> recordsTask = Task.Run(() => _statisticsStore.QueryRecords(query));
                await Task.WhenAll(countTask, recordsTask);

                if (loadVersion != _recordLoadVersion)
                    return;

                _recordCache.Clear();
                ReplaceItems(_recordRows, await recordsTask);
                if (_recordRows.Count > 0)
                    RecordDataGrid.SelectedIndex = 0;
                _totalRecordCount = await countTask;
                _currentPage = Math.Clamp(pageNumber, 1, Math.Max(1, GetPageCount()));
                UpdatePagination();
                _recordStatus = _totalRecordCount > RecordPageSize
                    ? $"已查询 {_totalRecordCount:N0} 条批次记录；第 {_currentPage:N0}/{GetPageCount():N0} 页，本页 {_recordRows.Count:N0} 条"
                    : $"已查询 {_totalRecordCount:N0} 条批次记录";
            }
            catch (Exception ex)
            {
                if (loadVersion != _recordLoadVersion)
                    return;

                _recordStatus = "查询失败";
                MessageBox.Show(this, $"读取批次记录失败：{ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (loadVersion == _recordLoadVersion)
                {
                    RecordRefreshButton.IsEnabled = true;
                    UpdateStatusText();
                }
            }
        }

        private async Task RefreshCombinedRecordsAsync(int pageNumber)
        {
            if (!IsCombinedStatisticsEnabled)
                return;

            ResultStatisticsQuery query = CreateCombinedQuery(pageNumber);
            int loadVersion = ++_combinedLoadVersion;
            CombinedRefreshButton.IsEnabled = false;
            _combinedStatus = "正在匹配 L/R 全批次...";
            UpdateStatusText();
            CombinedDetailHeader.Text = "L/R 流程 CT 明细";
            _combinedDetails.Clear();
            CombinedTimelinePanel.DataContext = CreateEmptyTimeline("正在查询全批次记录...");
            try
            {
                ResultStatisticsCombinedPage page = await Task.Run(() => _statisticsStore.QueryCombinedRecords(query));
                if (loadVersion != _combinedLoadVersion)
                    return;

                ReplaceItems(_combinedRows, page.Rows);
                _totalCombinedCount = page.TotalCount;
                _combinedCurrentPage = Math.Clamp(pageNumber, 1, Math.Max(1, GetCombinedPageCount()));
                UpdateCombinedPagination();
                if (_combinedRows.Count > 0)
                    CombinedRecordDataGrid.SelectedIndex = 0;
                else
                    CombinedTimelinePanel.DataContext = CreateEmptyTimeline("当前筛选范围没有匹配到相邻的 L→R 全批次。");
                _combinedStatus = _totalCombinedCount > RecordPageSize
                    ? $"已匹配 {_totalCombinedCount:N0} 个 L/R 全批次；第 {_combinedCurrentPage:N0}/{GetCombinedPageCount():N0} 页，本页 {_combinedRows.Count:N0} 条"
                    : $"已匹配 {_totalCombinedCount:N0} 个 L/R 全批次；未配对记录仍保留在“批次记录”";
            }
            catch (Exception ex)
            {
                if (loadVersion != _combinedLoadVersion)
                    return;
                _combinedStatus = "查询失败";
                MessageBox.Show(this, $"读取 L/R 全批次记录失败：{ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (loadVersion == _combinedLoadVersion)
                {
                    CombinedRefreshButton.IsEnabled = true;
                    UpdateStatusText();
                }
            }
        }

        private async Task RefreshFlowsAsync(int pageNumber)
        {
            FlowExecutionQuery query = CreateFlowQuery(pageNumber);
            int loadVersion = ++_flowLoadVersion;
            FlowRefreshButton.IsEnabled = false;
            _flowStatus = "正在查询流程执行记录...";
            UpdateStatusText();

            try
            {
                Task<int> countTask = Task.Run(() => _statisticsStore.QueryFlowExecutionCount(query));
                Task<IReadOnlyList<FlowExecutionRecordRow>> recordsTask = Task.Run(() => _statisticsStore.QueryFlowExecutions(query));
                await Task.WhenAll(countTask, recordsTask);

                if (loadVersion != _flowLoadVersion)
                    return;

                ReplaceItems(_flowRows, await recordsTask);
                if (_flowRows.Count > 0)
                    FlowDataGrid.SelectedIndex = 0;
                _totalFlowCount = await countTask;
                _flowCurrentPage = Math.Clamp(pageNumber, 1, Math.Max(1, GetFlowPageCount()));
                UpdateFlowPagination();
                _flowStatus = _totalFlowCount > FlowPageSize
                    ? $"已查询 {_totalFlowCount:N0} 条流程执行记录；第 {_flowCurrentPage:N0}/{GetFlowPageCount():N0} 页，本页 {_flowRows.Count:N0} 条"
                    : $"已查询 {_totalFlowCount:N0} 条流程执行记录";
            }
            catch (Exception ex)
            {
                if (loadVersion != _flowLoadVersion)
                    return;

                _flowStatus = "查询失败";
                MessageBox.Show(this, $"读取流程执行记录失败：{ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (loadVersion == _flowLoadVersion)
                {
                    FlowRefreshButton.IsEnabled = true;
                    UpdateStatusText();
                }
            }
        }

        private int GetPageCount()
        {
            return Math.Max(1, (int)Math.Ceiling(_totalRecordCount / (double)RecordPageSize));
        }

        private void UpdatePagination()
        {
            int pageCount = GetPageCount();
            PaginationPanel.Visibility = _totalRecordCount > RecordPageSize ? Visibility.Visible : Visibility.Collapsed;
            PageStatusText.Text = $"第 {_currentPage:N0} / {pageCount:N0} 页（每页 {RecordPageSize:N0} 条）";
            FirstPageButton.IsEnabled = _currentPage > 1;
            PreviousPageButton.IsEnabled = _currentPage > 1;
            NextPageButton.IsEnabled = _currentPage < pageCount;
            LastPageButton.IsEnabled = _currentPage < pageCount;
        }

        private int GetFlowPageCount()
        {
            return Math.Max(1, (int)Math.Ceiling(_totalFlowCount / (double)FlowPageSize));
        }

        private void UpdateFlowPagination()
        {
            int pageCount = GetFlowPageCount();
            FlowPaginationPanel.Visibility = _totalFlowCount > FlowPageSize ? Visibility.Visible : Visibility.Collapsed;
            FlowPageStatusText.Text = $"第 {_flowCurrentPage:N0} / {pageCount:N0} 页（每页 {FlowPageSize:N0} 条）";
            FlowFirstPageButton.IsEnabled = _flowCurrentPage > 1;
            FlowPreviousPageButton.IsEnabled = _flowCurrentPage > 1;
            FlowNextPageButton.IsEnabled = _flowCurrentPage < pageCount;
            FlowLastPageButton.IsEnabled = _flowCurrentPage < pageCount;
        }

        private async void FirstPage_Click(object sender, RoutedEventArgs e)
        {
            await RefreshRecordsAsync(1);
        }

        private async void PreviousPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
                await RefreshRecordsAsync(_currentPage - 1);
        }

        private async void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < GetPageCount())
                await RefreshRecordsAsync(_currentPage + 1);
        }

        private async void LastPage_Click(object sender, RoutedEventArgs e)
        {
            await RefreshRecordsAsync(GetPageCount());
        }

        private async void CombinedFirstPage_Click(object sender, RoutedEventArgs e)
        {
            await RefreshCombinedRecordsAsync(1);
        }

        private async void CombinedPreviousPage_Click(object sender, RoutedEventArgs e)
        {
            if (_combinedCurrentPage > 1)
                await RefreshCombinedRecordsAsync(_combinedCurrentPage - 1);
        }

        private async void CombinedNextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_combinedCurrentPage < GetCombinedPageCount())
                await RefreshCombinedRecordsAsync(_combinedCurrentPage + 1);
        }

        private async void CombinedLastPage_Click(object sender, RoutedEventArgs e)
        {
            await RefreshCombinedRecordsAsync(GetCombinedPageCount());
        }

        private async void FlowFirstPage_Click(object sender, RoutedEventArgs e)
        {
            await RefreshFlowsAsync(1);
        }

        private async void FlowPreviousPage_Click(object sender, RoutedEventArgs e)
        {
            if (_flowCurrentPage > 1)
                await RefreshFlowsAsync(_flowCurrentPage - 1);
        }

        private async void FlowNextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_flowCurrentPage < GetFlowPageCount())
                await RefreshFlowsAsync(_flowCurrentPage + 1);
        }

        private async void FlowLastPage_Click(object sender, RoutedEventArgs e)
        {
            await RefreshFlowsAsync(GetFlowPageCount());
        }

        private ResultStatisticsQuery CreateHomeQuery()
        {
            ResultStatisticsPeriodMode mode = GetSelectedPeriodMode(HomePeriodMode);
            ResultStatisticsPeriodRange range = ResultStatisticsPeriod.GetRange(mode, HomeAnchorDatePicker.SelectedDate ?? DateTime.Today);
            return new ResultStatisticsQuery { From = range.From, ToExclusive = range.ToExclusive };
        }

        private ResultStatisticsQuery CreateRecordQuery(int pageNumber)
        {
            ResultStatisticsPeriodMode mode = GetSelectedPeriodMode(RecordPeriodMode);
            ResultStatisticsPeriodRange range = ResultStatisticsPeriod.GetRange(mode, RecordAnchorDatePicker.SelectedDate ?? DateTime.Today);
            bool? result = ResultFilter.SelectedIndex switch
            {
                1 => true,
                2 => false,
                _ => null,
            };
            string sn = SnFilter.Text.Trim();
            return new ResultStatisticsQuery
            {
                From = range.From,
                ToExclusive = range.ToExclusive,
                SN = string.IsNullOrWhiteSpace(sn) ? null : sn,
                Result = result,
                PageNumber = Math.Max(1, pageNumber),
                PageSize = RecordPageSize,
            };
        }

        private ResultStatisticsQuery CreateCombinedQuery(int pageNumber)
        {
            ResultStatisticsPeriodMode mode = GetSelectedPeriodMode(CombinedPeriodMode);
            ResultStatisticsPeriodRange range = ResultStatisticsPeriod.GetRange(mode, CombinedAnchorDatePicker.SelectedDate ?? DateTime.Today);
            bool? result = CombinedResultFilter.SelectedIndex switch
            {
                1 => true,
                2 => false,
                _ => null,
            };
            string sn = CombinedSnFilter.Text.Trim();
            return new ResultStatisticsQuery
            {
                From = range.From,
                ToExclusive = range.ToExclusive,
                SN = string.IsNullOrWhiteSpace(sn) ? null : sn,
                Result = result,
                PageNumber = Math.Max(1, pageNumber),
                PageSize = RecordPageSize,
            };
        }

        private FlowExecutionQuery CreateFlowQuery(int pageNumber)
        {
            ResultStatisticsPeriodMode mode = GetSelectedPeriodMode(FlowPeriodMode);
            ResultStatisticsPeriodRange range = ResultStatisticsPeriod.GetRange(mode, FlowAnchorDatePicker.SelectedDate ?? DateTime.Today);
            bool? result = FlowResultFilter.SelectedIndex switch
            {
                1 => true,
                2 => false,
                _ => null,
            };
            string model = FlowNameFilter.Text.Trim();
            return new FlowExecutionQuery
            {
                From = range.From,
                ToExclusive = range.ToExclusive,
                Model = string.IsNullOrWhiteSpace(model) ? null : model,
                Result = result,
                PageNumber = Math.Max(1, pageNumber),
                PageSize = FlowPageSize,
            };
        }

        private async Task LoadSnSuggestionsAsync()
        {
            int version = ++_snIndexVersion;
            _snIndexStatus = "正在加载 SN，可先手动输入查询";
            UpdateStatusText();
            try
            {
                IReadOnlyList<ResultStatisticsSnSummary> summaries = await Task.Run(_statisticsStore.QuerySnSummaries);
                if (version != _snIndexVersion)
                    return;

                _snSuggestions = summaries.Select(item => item.SN).ToArray();
                UpdateSnSuggestions(SnFilter.Text, openDropDown: false);
                _snIndexStatus = $"可检索 {_snSuggestions.Length:N0} 个 SN，下拉最多显示 {SuggestionDisplayLimit} 个匹配项";
            }
            catch (Exception ex)
            {
                if (version != _snIndexVersion)
                    return;

                _snIndexStatus = "SN 列表加载失败，仍可手动输入";
                Log.Warn("Could not load the ARVRPro SN suggestions.", ex);
            }
            finally
            {
                if (version == _snIndexVersion)
                    UpdateStatusText();
            }
        }

        private void SnFilter_Loaded(object sender, RoutedEventArgs e)
        {
            if (_snEditor != null)
                _snEditor.TextChanged -= SnEditor_TextChanged;
            SnFilter.ApplyTemplate();
            _snEditor = SnFilter.Template.FindName("PART_EditableTextBox", SnFilter) as TextBox;
            if (_snEditor != null)
                _snEditor.TextChanged += SnEditor_TextChanged;
            UpdateSnSuggestions(SnFilter.Text, openDropDown: false);
        }

        private void SnEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_updatingSnSuggestions)
                UpdateSnSuggestions(_snEditor?.Text, openDropDown: true);
        }

        private void SnFilter_DropDownOpened(object sender, EventArgs e)
        {
            if (!_updatingSnSuggestions)
                UpdateSnSuggestions(_snEditor?.Text ?? SnFilter.Text, openDropDown: false);
        }

        private void SnFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingSnSuggestions || SnFilter.SelectedItem is not string selected)
                return;

            _updatingSnSuggestions = true;
            SnFilter.Text = selected;
            if (_snEditor != null)
            {
                _snEditor.Text = selected;
                _snEditor.CaretIndex = selected.Length;
            }
            SnFilter.IsDropDownOpen = false;
            _updatingSnSuggestions = false;
        }

        private void UpdateSnSuggestions(string? text, bool openDropDown)
        {
            string input = text ?? string.Empty;
            IReadOnlyList<string> matches = ResultStatisticsSuggestionFilter.Filter(
                _snSuggestions,
                input,
                SuggestionDisplayLimit);

            _updatingSnSuggestions = true;
            SnFilter.ItemsSource = matches;
            SnFilter.Text = input;
            if (_snEditor != null)
            {
                _snEditor.Text = input;
                _snEditor.CaretIndex = input.Length;
            }
            _updatingSnSuggestions = false;

            if (openDropDown && _snEditor?.IsKeyboardFocusWithin == true)
                SnFilter.IsDropDownOpen = matches.Count > 0;
        }

        private async Task LoadFlowNameSuggestionsAsync()
        {
            int version = ++_flowNameIndexVersion;
            _flowNameIndexStatus = "正在加载流程名，可先手动输入查询";
            UpdateStatusText();
            try
            {
                IReadOnlyList<string> names = await Task.Run(_statisticsStore.QueryFlowNames);
                if (version != _flowNameIndexVersion)
                    return;

                _flowNameSuggestions = names.ToArray();
                UpdateFlowNameSuggestions(FlowNameFilter.Text, openDropDown: false);
                _flowNameIndexStatus = $"可检索 {_flowNameSuggestions.Length:N0} 个流程名，下拉最多显示 {SuggestionDisplayLimit} 个匹配项";
            }
            catch (Exception ex)
            {
                if (version != _flowNameIndexVersion)
                    return;

                _flowNameIndexStatus = "流程名列表加载失败，仍可手动输入";
                Log.Warn("Could not load the ARVRPro flow-name suggestions.", ex);
            }
            finally
            {
                if (version == _flowNameIndexVersion)
                    UpdateStatusText();
            }
        }

        private void FlowNameFilter_Loaded(object sender, RoutedEventArgs e)
        {
            if (_flowNameEditor != null)
                _flowNameEditor.TextChanged -= FlowNameEditor_TextChanged;
            FlowNameFilter.ApplyTemplate();
            _flowNameEditor = FlowNameFilter.Template.FindName("PART_EditableTextBox", FlowNameFilter) as TextBox;
            if (_flowNameEditor != null)
                _flowNameEditor.TextChanged += FlowNameEditor_TextChanged;
            UpdateFlowNameSuggestions(FlowNameFilter.Text, openDropDown: false);
        }

        private void FlowNameEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_updatingFlowNameSuggestions)
                UpdateFlowNameSuggestions(_flowNameEditor?.Text, openDropDown: true);
        }

        private void FlowNameFilter_DropDownOpened(object sender, EventArgs e)
        {
            if (!_updatingFlowNameSuggestions)
                UpdateFlowNameSuggestions(_flowNameEditor?.Text ?? FlowNameFilter.Text, openDropDown: false);
        }

        private void FlowNameFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingFlowNameSuggestions || FlowNameFilter.SelectedItem is not string selected)
                return;

            _updatingFlowNameSuggestions = true;
            FlowNameFilter.Text = selected;
            if (_flowNameEditor != null)
            {
                _flowNameEditor.Text = selected;
                _flowNameEditor.CaretIndex = selected.Length;
            }
            FlowNameFilter.IsDropDownOpen = false;
            _updatingFlowNameSuggestions = false;
        }

        private void UpdateFlowNameSuggestions(string? text, bool openDropDown)
        {
            string input = text ?? string.Empty;
            IReadOnlyList<string> matches = ResultStatisticsSuggestionFilter.Filter(
                _flowNameSuggestions,
                input,
                SuggestionDisplayLimit);

            _updatingFlowNameSuggestions = true;
            FlowNameFilter.ItemsSource = matches;
            FlowNameFilter.Text = input;
            if (_flowNameEditor != null)
            {
                _flowNameEditor.Text = input;
                _flowNameEditor.CaretIndex = input.Length;
            }
            _updatingFlowNameSuggestions = false;

            if (openDropDown && _flowNameEditor?.IsKeyboardFocusWithin == true)
                FlowNameFilter.IsDropDownOpen = matches.Count > 0;
        }

        private void ApplyStatistics(ResultStatistics statistics)
        {
            TotalCountText.Text = statistics.TotalCount.ToString("N0");
            PassCountText.Text = statistics.PassCount.ToString("N0");
            FailCountText.Text = statistics.FailCount.ToString("N0");
            PassRateText.Text = statistics.PassRateText;
            AverageCtText.Text = statistics.AverageCtText;
            CurrentHourCountText.Text = statistics.CurrentHourCount.ToString("N0");
            TodayCountText.Text = statistics.TodayCount.ToString("N0");
        }

        private int GetCombinedPageCount()
        {
            return Math.Max(1, (int)Math.Ceiling(_totalCombinedCount / (double)RecordPageSize));
        }

        private void UpdateCombinedPagination()
        {
            int pageCount = GetCombinedPageCount();
            CombinedPaginationPanel.Visibility = _totalCombinedCount > RecordPageSize ? Visibility.Visible : Visibility.Collapsed;
            CombinedPageStatusText.Text = $"第 {_combinedCurrentPage:N0} / {pageCount:N0} 页（每页 {RecordPageSize:N0} 条）";
            CombinedFirstPageButton.IsEnabled = _combinedCurrentPage > 1;
            CombinedPreviousPageButton.IsEnabled = _combinedCurrentPage > 1;
            CombinedNextPageButton.IsEnabled = _combinedCurrentPage < pageCount;
            CombinedLastPageButton.IsEnabled = _combinedCurrentPage < pageCount;
        }

        private void ApplyCombinedStatistics(ResultStatisticsCombinedDashboard dashboard)
        {
            ResultStatistics statistics = dashboard.Summary;
            CombinedTotalCountText.Text = statistics.TotalCount.ToString("N0");
            CombinedPassCountText.Text = statistics.PassCount.ToString("N0");
            CombinedFailCountText.Text = statistics.FailCount.ToString("N0");
            CombinedPassRateText.Text = statistics.PassRateText;
            CombinedAverageCtText.Text = statistics.AverageCtText;
            CombinedTransitionText.Text = dashboard.AverageTransitionText;
            CombinedTodayCountText.Text = statistics.TodayCount.ToString("N0");
        }

        private void ConfigureHomeTrendPlot()
        {
            ScottPlot.Color background = GetPlotColor("SecondaryRegionBrush", "#FFFFFF");
            ScottPlot.Color foreground = GetPlotColor("GlobalTextBrush", "#20242A");
            ScottPlot.Color border = GetPlotColor("BorderBrush", "#D8DEE9");
            HomeTrendPlot.Plot.FigureBackground.Color = background;
            HomeTrendPlot.Plot.DataBackground.Color = background;
            HomeTrendPlot.Plot.Axes.Color(foreground);
            HomeTrendPlot.Plot.Legend.BackgroundColor = background;
            HomeTrendPlot.Plot.Legend.FontColor = foreground;
            HomeTrendPlot.Plot.Legend.OutlineColor = border;
            string chineseFont = ScottPlot.Fonts.Detect("逐条整组 CT 与累计产量");
            HomeTrendPlot.Plot.Axes.Title.Label.FontName = chineseFont;
            HomeTrendPlot.Plot.Axes.Left.Label.FontName = chineseFont;
            HomeTrendPlot.Plot.Axes.Right.Label.FontName = chineseFont;
            HomeTrendPlot.Plot.Axes.Bottom.Label.FontName = chineseFont;
            HomeTrendPlot.Plot.Axes.Left.TickLabelStyle.FontName = chineseFont;
            HomeTrendPlot.Plot.Axes.Right.TickLabelStyle.FontName = chineseFont;
            HomeTrendPlot.Plot.Axes.Bottom.TickLabelStyle.FontName = chineseFont;
            HomeTrendPlot.Plot.Legend.FontName = chineseFont;
            HomeTrendPlot.Plot.Grid.MajorLineColor = border;
            HomeTrendPlot.Plot.XLabel("时间");
            ConfigureHomeTrendPresentation(ResultStatisticsPeriodMode.Day, false);
        }

        private ScottPlot.Color GetPlotColor(string resourceKey, string fallback)
        {
            if (TryFindResource(resourceKey) is SolidColorBrush brush)
            {
                uint argb = ((uint)brush.Color.A << 24)
                    | ((uint)brush.Color.R << 16)
                    | ((uint)brush.Color.G << 8)
                    | brush.Color.B;
                return ScottPlot.Color.FromARGB(argb);
            }

            return ScottPlot.Color.FromHex(fallback);
        }

        private void ConfigureHomeTrendPresentation(ResultStatisticsPeriodMode mode, bool combined)
        {
            string unit = combined ? "L/R 全批次" : "单侧批次";
            HomeTrendHeading.Text = combined ? "L/R 全批次产量与 CT 趋势" : "单侧批次产量与 CT 趋势";
            if (mode == ResultStatisticsPeriodMode.All)
            {
                HomeTrendPlot.Plot.Title($"月产量与平均{unit} CT");
                HomeTrendPlot.Plot.YLabel("产量（组）");
                HomeTrendPlot.Plot.Axes.Right.Label.Text = "平均 CT（秒）";
            }
            else
            {
                HomeTrendPlot.Plot.Title($"逐条{unit} CT 与累计产量");
                HomeTrendPlot.Plot.YLabel($"{unit} CT（秒）");
                HomeTrendPlot.Plot.Axes.Right.Label.Text = "累计产量（组）";
            }
        }

        private void RenderHomeTrend(
            IReadOnlyList<ResultStatisticsTrendPoint> points,
            ResultStatisticsPeriodMode mode,
            DateTime from,
            DateTime toExclusive,
            bool combined)
        {
            HomeTrendPlot.Plot.Clear();
            ConfigureHomeTrendPresentation(mode, combined);
            bool hasData = points.Any(item => item.TotalCount > 0);
            HomeTrendEmptyText.Visibility = hasData ? Visibility.Collapsed : Visibility.Visible;
            if (!hasData)
            {
                HomeTrendPlot.Refresh();
                return;
            }

            if (mode == ResultStatisticsPeriodMode.All)
                RenderHomeMonthlyTrend(points);
            else
                RenderHomeDetailTrend(points, from, toExclusive);

            HomeTrendPlot.Plot.ShowLegend(ScottPlot.Alignment.UpperRight);
            HomeTrendPlot.Refresh();
        }

        private void RenderHomeMonthlyTrend(IReadOnlyList<ResultStatisticsTrendPoint> points)
        {
            var bars = points.Select((item, index) => new ScottPlot.Bar
            {
                Position = index,
                Value = item.TotalCount,
                Size = 0.68,
                FillColor = ScottPlot.Color.FromHex("#4D8DFF"),
            }).ToArray();
            ScottPlot.Plottables.BarPlot productionPlot = HomeTrendPlot.Plot.Add.Bars(bars);
            productionPlot.LegendText = "产量";

            double[] positions = Enumerable.Range(0, points.Count).Select(index => (double)index).ToArray();
            double[] averageCtSeconds = points
                .Select(item => item.TotalCount > 0 ? item.AverageCtMilliseconds / 1000d : double.NaN)
                .ToArray();
            ScottPlot.Plottables.Scatter ctPlot = HomeTrendPlot.Plot.Add.Scatter(positions, averageCtSeconds);
            ctPlot.Axes.YAxis = HomeTrendPlot.Plot.Axes.Right;
            ctPlot.LegendText = "平均整组 CT";
            ctPlot.Color = ScottPlot.Color.FromHex("#F59E0B");
            ctPlot.LineWidth = 2;
            ctPlot.MarkerSize = 5;

            int tickStep = Math.Max(1, (int)Math.Ceiling(points.Count / 16d));
            List<ScottPlot.Tick> ticks = [];
            for (int index = 0; index < points.Count; index++)
            {
                if (index % tickStep == 0 || index == points.Count - 1)
                    ticks.Add(new ScottPlot.Tick(index, points[index].Label));
            }
            HomeTrendPlot.Plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks.ToArray());
            HomeTrendPlot.Plot.Axes.Bottom.TickLabelStyle.Rotation = points.Count > 16 ? -35 : 0;
            HomeTrendPlot.Plot.Axes.SetLimitsX(-0.7, points.Count - 0.3);
            HomeTrendPlot.Plot.Axes.Left.Min = 0;
            HomeTrendPlot.Plot.Axes.Left.Max = Math.Max(1, points.Max(item => item.TotalCount) * 1.15);
            HomeTrendPlot.Plot.Axes.Right.Min = 0;
            HomeTrendPlot.Plot.Axes.Right.Max = Math.Max(1, averageCtSeconds.Where(double.IsFinite).DefaultIfEmpty(0).Max() * 1.15);
        }

        private void RenderHomeDetailTrend(
            IReadOnlyList<ResultStatisticsTrendPoint> points,
            DateTime from,
            DateTime toExclusive)
        {
            double[] eventTimes = points.Select(item => item.Time.ToOADate()).ToArray();
            double[] ctSeconds = points.Select(item => item.AverageCtMilliseconds / 1000d).ToArray();
            double[] stemXs = new double[points.Count * 3];
            double[] stemYs = new double[points.Count * 3];
            for (int index = 0; index < points.Count; index++)
            {
                int offset = index * 3;
                stemXs[offset] = eventTimes[index];
                stemXs[offset + 1] = eventTimes[index];
                stemXs[offset + 2] = double.NaN;
                stemYs[offset] = 0;
                stemYs[offset + 1] = ctSeconds[index];
                stemYs[offset + 2] = double.NaN;
            }

            ScottPlot.Plottables.Scatter ctPlot = HomeTrendPlot.Plot.Add.Scatter(stemXs, stemYs);
            ctPlot.LegendText = "逐条整组 CT";
            ctPlot.Color = ScottPlot.Color.FromHex("#4D8DFF");
            ctPlot.LineWidth = points.Count > 5_000 ? 0.6f : 1f;
            ctPlot.MarkerSize = 0;

            double rangeStart = from.ToOADate();
            double rangeEnd = toExclusive.ToOADate();
            double[] cumulativeXs = new double[points.Count * 2 + 2];
            double[] cumulativeYs = new double[points.Count * 2 + 2];
            cumulativeXs[0] = rangeStart;
            cumulativeYs[0] = 0;
            for (int index = 0; index < points.Count; index++)
            {
                int offset = index * 2 + 1;
                cumulativeXs[offset] = eventTimes[index];
                cumulativeYs[offset] = index;
                cumulativeXs[offset + 1] = eventTimes[index];
                cumulativeYs[offset + 1] = index + 1;
            }
            cumulativeXs[^1] = rangeEnd;
            cumulativeYs[^1] = points.Count;

            ScottPlot.Plottables.Scatter cumulativePlot = HomeTrendPlot.Plot.Add.Scatter(cumulativeXs, cumulativeYs);
            cumulativePlot.Axes.YAxis = HomeTrendPlot.Plot.Axes.Right;
            cumulativePlot.LegendText = "累计产量";
            cumulativePlot.Color = ScottPlot.Color.FromHex("#F59E0B");
            cumulativePlot.LineWidth = 2;
            cumulativePlot.MarkerSize = 0;

            HomeTrendPlot.Plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.DateTimeAutomatic();
            HomeTrendPlot.Plot.Axes.Bottom.TickLabelStyle.Rotation = -25;
            HomeTrendPlot.Plot.Axes.SetLimitsX(rangeStart, rangeEnd);
            HomeTrendPlot.Plot.Axes.Left.Min = 0;
            HomeTrendPlot.Plot.Axes.Left.Max = Math.Max(1, ctSeconds.DefaultIfEmpty(0).Max() * 1.15);
            HomeTrendPlot.Plot.Axes.Right.Min = 0;
            HomeTrendPlot.Plot.Axes.Right.Max = Math.Max(1, points.Count * 1.05);
        }

        private void UpdateStatusText()
        {
            HomeStatusText.Text = _homeStatus;
            QueryStatusText.Text = string.Join("；", new[] { _recordStatus, _snIndexStatus }.Where(item => !string.IsNullOrWhiteSpace(item)));
            CombinedQueryStatusText.Text = _combinedStatus;
            FlowQueryStatusText.Text = string.Join("；", new[] { _flowStatus, _flowNameIndexStatus }.Where(item => !string.IsNullOrWhiteSpace(item)));
        }

        private static void ReplaceItems<T>(ObservableCollection<T> target, IEnumerable<T> source)
        {
            target.Clear();
            foreach (T item in source)
                target.Add(item);
        }

        private void RecordDataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            SelectDataGridRowAtPointer(RecordDataGrid, e);
        }

        private void FlowDataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!SelectDataGridRowAtPointer(FlowDataGrid, e))
                FlowDataGrid.SelectedItems.Clear();
        }

        private static bool SelectDataGridRowAtPointer(DataGrid dataGrid, MouseButtonEventArgs e)
        {
            DependencyObject? element = dataGrid.InputHitTest(e.GetPosition(dataGrid)) as DependencyObject;
            while (element != null && element is not DataGridRow)
                element = VisualTreeHelper.GetParent(element);

            if (element is DataGridRow row && !row.IsSelected)
            {
                dataGrid.SelectedItems.Clear();
                row.IsSelected = true;
            }

            return element is DataGridRow;
        }

        private void FlowDataGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (SelectedFlowRow == null)
                e.Handled = true;
        }

        private async void RecordDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            await ViewSelectedItemsAsync();
        }

        private async void ViewItems_Click(object sender, RoutedEventArgs e)
        {
            await ViewSelectedItemsAsync();
        }

        private async void FlowDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || !IsEventInsideDataGridRow(e.OriginalSource as DependencyObject))
                return;

            await ViewSelectedFlowResultAsync();
        }

        private static bool IsEventInsideDataGridRow(DependencyObject? element)
        {
            while (element != null && element is not DataGridRow)
                element = VisualTreeHelper.GetParent(element);
            return element is DataGridRow;
        }

        private async void FlowViewTestResult_Click(object sender, RoutedEventArgs e)
        {
            await ViewSelectedFlowResultAsync();
        }

        private async Task ViewSelectedFlowResultAsync()
        {
            (FlowExecutionRecordRow Row, string Json)? loaded = await LoadSelectedFlowResultAsync();
            if (!loaded.HasValue)
                return;

            new TestResultViewWindow(loaded.Value.Json)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog();
        }

        private async void FlowViewJson_Click(object sender, RoutedEventArgs e)
        {
            (FlowExecutionRecordRow Row, string Json)? loaded = await LoadSelectedFlowResultAsync();
            if (!loaded.HasValue)
                return;

            var control = new AvalonEditControll();
            control.SetJsonText(loaded.Value.Json);
            new Window
            {
                Title = $"ViewResultJson - {loaded.Value.Row.Model} - {loaded.Value.Row.SN}",
                Owner = this,
                Content = control,
                Width = 900,
                Height = 650,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog();
        }

        private async Task<(FlowExecutionRecordRow Row, string Json)?> LoadSelectedFlowResultAsync()
        {
            FlowExecutionRecordRow? row = SelectedFlowRow;
            if (row == null)
            {
                MessageBox.Show(this, "请先选择一条流程记录。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                return null;
            }

            try
            {
                var result = new ProjectARVRReuslt
                {
                    Id = row.Id,
                    SN = row.SN,
                    Model = row.Model,
                    CreateTime = row.CreateTime,
                    RunTime = row.RunTimeMilliseconds,
                    Result = row.Result,
                };
                string? viewResultJson = await Task.Run(() => _statisticsStore.LoadViewResultJson(result));
                if (string.IsNullOrEmpty(viewResultJson))
                {
                    MessageBox.Show(this, "该流程没有可查看的测试结果。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                    return null;
                }

                return (row, viewResultJson);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"读取流程测试结果失败：{ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        private async Task ViewSelectedItemsAsync()
        {
            ObjectiveTestResultRecord? record = await LoadSelectedRecordAsync();
            if (record == null)
                return;

            string json = record.ObjectiveTestResultJson ?? string.Empty;
            if (json.Length == 0)
            {
                MessageBox.Show(this, "ObjectiveTestResult 为空。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            new TestResultViewWindow(json)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog();
        }

        private async void ViewJson_Click(object sender, RoutedEventArgs e)
        {
            ObjectiveTestResultRecord? record = await LoadSelectedRecordAsync();
            if (record == null)
                return;

            string json = record.ObjectiveTestResultJson ?? string.Empty;
            if (json.Length == 0)
            {
                MessageBox.Show(this, "ObjectiveTestResult 为空。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var control = new AvalonEditControll();
            control.SetJsonText(json);
            new Window
            {
                Title = $"ObjectiveTestResult Json - {record.SN}",
                Owner = this,
                Content = control,
                Width = 900,
                Height = 650,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog();
        }

        private async void ExportSingleCsv_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetSelectedRecordRow(out ResultStatisticsRecordRow row))
                return;

            var dialog = new SaveFileDialog
            {
                Title = "导出单条 ObjectiveTestResult",
                Filter = "CSV 文件 (*.csv)|*.csv",
                DefaultExt = ".csv",
                AddExtension = true,
                FileName = $"TestResults_{SanitizeFileName(string.IsNullOrWhiteSpace(row.SN) ? "SN" : row.SN)}.csv",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            };
            if (dialog.ShowDialog(this) != true)
                return;

            try
            {
                string fileName = dialog.FileName;
                ObjectiveTestResultRecord? record = await LoadRecordAsync(row);
                if (record == null)
                {
                    MessageBox.Show(this, "该记录已不存在。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                bool useLegacy = _viewResultManager?.Config.UseLegacyARVROutput ?? false;
                ObjectiveTestResult? result = JsonConvert.DeserializeObject<ObjectiveTestResult>(record.ObjectiveTestResultJson ?? string.Empty);
                if (useLegacy && result == null)
                {
                    MessageBox.Show(this, "ObjectiveTestResult 为空，无法导出旧版 CSV。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await Task.Run(() =>
                {
                    if (useLegacy)
                    {
                        LegacyARVRObjectiveTestResult legacyResult = LegacyARVRConverter.ToLegacy(result!);
                        LegacyARVRCsvExporter.ExportToCsv(new List<LegacyARVRObjectiveTestResult> { legacyResult }, fileName);
                    }
                    else
                    {
                        IReadOnlyList<ProjectARVRReuslt> flowResults = _statisticsStore.QueryFlowDetailsForExport(row);
                        IReadOnlyList<ObjectiveTestCsvRow> rows = ProjectARVRResultCsvExporter.CollectRows(flowResults);
                        if (rows.Count > 0)
                            ProjectARVRResultCsvExporter.ExportRows(rows, fileName);
                        else if (result != null)
                            ObjectiveTestResultCsvExporter.ExportToCsv(result, fileName);
                        else
                            throw new InvalidOperationException("没有可导出的单流程结果或聚合结果。");
                    }
                });
                MessageBox.Show(this, $"导出完成：{fileName}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"导出失败：{ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ExportBatchCsv_Click(object sender, RoutedEventArgs e)
        {
            List<ResultStatisticsRecordRow> selectedRows = _recordRows
                .Where(row => RecordDataGrid.SelectedItems.Contains(row))
                .ToList();
            if (selectedRows.Count == 0)
                selectedRows = _recordRows.ToList();
            if (selectedRows.Count == 0)
            {
                MessageBox.Show(this, "当前没有可导出的记录。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "批量导出结果记录",
                Filter = "CSV 文件 (*.csv)|*.csv",
                DefaultExt = ".csv",
                AddExtension = true,
                FileName = $"TestResults_Batch_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            };
            if (dialog.ShowDialog(this) != true)
                return;

            try
            {
                string fileName = dialog.FileName;
                int[] ids = selectedRows.Select(row => row.Id).ToArray();
                List<ObjectiveTestResultRecord> exportedRecords = await Task.Run(() =>
                {
                    Dictionary<int, ObjectiveTestResultRecord> recordsById = _statisticsStore.GetRecords(ids)
                        .ToDictionary(record => record.Id);
                    List<ObjectiveTestResultRecord> records = selectedRows
                        .Where(row => recordsById.ContainsKey(row.Id))
                        .Select(row => recordsById[row.Id])
                        .ToList();
                    Dictionary<int, IReadOnlyList<ProjectARVRReuslt>> flowResultsByRecordId = selectedRows
                        .Where(row => recordsById.ContainsKey(row.Id))
                        .ToDictionary(row => row.Id, row => _statisticsStore.QueryFlowDetailsForExport(row));
                    ObjectiveTestResultBatchCsvExporter.ExportToCsv(records, flowResultsByRecordId, fileName);
                    return records;
                });

                foreach (ObjectiveTestResultRecord record in exportedRecords)
                    _recordCache[record.Id] = record;
                MessageBox.Show(this, $"已导出 {exportedRecords.Count:N0} 条记录：{fileName}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"批量导出失败：{ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool TryGetSelectedRecordRow(out ResultStatisticsRecordRow row)
        {
            if (SelectedRecordRow is ResultStatisticsRecordRow selectedRow)
            {
                row = selectedRow;
                return true;
            }

            row = null!;
            MessageBox.Show(this, "请先选择一条记录。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        private async Task<ObjectiveTestResultRecord?> LoadSelectedRecordAsync()
        {
            return TryGetSelectedRecordRow(out ResultStatisticsRecordRow row)
                ? await LoadRecordAsync(row)
                : null;
        }

        private async Task<ObjectiveTestResultRecord?> LoadRecordAsync(ResultStatisticsRecordRow row)
        {
            if (_recordCache.TryGetValue(row.Id, out ObjectiveTestResultRecord? cached))
                return cached;

            ObjectiveTestResultRecord? record = await Task.Run(() => _statisticsStore.GetRecord(row.Id));
            if (record != null)
                _recordCache[row.Id] = record;
            return record;
        }

        private static string SanitizeFileName(string fileName)
        {
            foreach (char character in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(character, '_');
            return fileName;
        }

        private void RecordDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ResultStatisticsRecordRow? selectedRow = SelectedRecordRow;
            if (selectedRow == null)
            {
                ++_detailLoadVersion;
                _details.Clear();
                DetailHeader.Text = "流程 CT 明细";
                TimelinePanel.DataContext = CreateEmptyTimeline("选择左侧批次后显示整组时间轴。");
                return;
            }

            _ = LoadFlowDetailsAsync(selectedRow);
        }

        private async Task LoadFlowDetailsAsync(ResultStatisticsRecordRow row)
        {
            int loadVersion = ++_detailLoadVersion;
            DetailHeader.Text = $"{row.SN} - 正在读取流程 CT 明细...";
            TimelinePanel.DataContext = CreateEmptyTimeline("正在生成整组时间轴...");
            try
            {
                IReadOnlyList<ProjectARVRReuslt> details = await Task.Run(() => _statisticsStore.QueryFlowDetails(row));
                if (loadVersion != _detailLoadVersion || SelectedRecordRow != row)
                    return;

                ReplaceItems(_details, details);
                double flowMilliseconds = details.Sum(item => Convert.ToDouble(item.RunTime));
                TimelinePanel.DataContext = ResultTimelineBuilder.Build(row, details);
                DetailHeader.Text = $"{row.SN} · CT {row.CycleTimeText} · 运行 {ResultStatisticsCalculator.FormatMilliseconds(flowMilliseconds)} · {details.Count:N0} 个流程";
            }
            catch (Exception ex)
            {
                if (loadVersion != _detailLoadVersion)
                    return;

                _details.Clear();
                DetailHeader.Text = $"{row.SN} - 流程 CT 明细读取失败";
                TimelinePanel.DataContext = CreateEmptyTimeline("时间轴读取失败。");
                MessageBox.Show(this, $"读取流程 CT 明细失败：{ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CombinedRecordDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ResultStatisticsCombinedRecordRow? selectedRow = SelectedCombinedRow;
            if (selectedRow == null)
            {
                ++_combinedDetailLoadVersion;
                _combinedDetails.Clear();
                CombinedDetailHeader.Text = "L/R 流程 CT 明细";
                CombinedTimelinePanel.DataContext = CreateEmptyTimeline("选择左侧全批次后显示 L/R 时间轴。");
                return;
            }

            _ = LoadCombinedFlowDetailsAsync(selectedRow);
        }

        private async Task LoadCombinedFlowDetailsAsync(ResultStatisticsCombinedRecordRow row)
        {
            int loadVersion = ++_combinedDetailLoadVersion;
            CombinedDetailHeader.Text = $"{row.SN} - 正在读取 L/R 流程 CT 明细...";
            CombinedTimelinePanel.DataContext = CreateEmptyTimeline("正在生成 L/R 全批次时间轴...");
            try
            {
                Task<IReadOnlyList<ProjectARVRReuslt>> leftTask = Task.Run(() => _statisticsStore.QueryFlowDetails(row.Left));
                Task<IReadOnlyList<ProjectARVRReuslt>> rightTask = Task.Run(() => _statisticsStore.QueryFlowDetails(row.Right));
                await Task.WhenAll(leftTask, rightTask);
                if (loadVersion != _combinedDetailLoadVersion || SelectedCombinedRow != row)
                    return;

                IReadOnlyList<ProjectARVRReuslt> left = await leftTask;
                IReadOnlyList<ProjectARVRReuslt> right = await rightTask;
                ReplaceItems(_combinedDetails, left.Concat(right));
                CombinedTimelinePanel.DataContext = ResultTimelineBuilder.BuildCombined(row, left, right);
                CombinedDetailHeader.Text = $"{row.SN} · 全批次 CT {row.CycleTimeText} · L→R 等待 {row.TransitionText} · {left.Count + right.Count:N0} 个流程";
            }
            catch (Exception ex)
            {
                if (loadVersion != _combinedDetailLoadVersion)
                    return;
                _combinedDetails.Clear();
                CombinedDetailHeader.Text = $"{row.SN} - L/R 流程 CT 明细读取失败";
                CombinedTimelinePanel.DataContext = CreateEmptyTimeline("L/R 全批次时间轴读取失败。");
                MessageBox.Show(this, $"读取 L/R 流程 CT 明细失败：{ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static ResultTimelinePresentation CreateEmptyTimeline(string note)
        {
            return new ResultTimelinePresentation
            {
                SummaryText = "CT - · 流程 -",
                NoteText = note,
            };
        }

        private void BuildDetailContextMenu()
        {
            DetailList.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, ListViewUtils.Copy, (_, e) => e.CanExecute = DetailList.SelectedItems.Count > 0));

            var openFolderCommand = new RelayCommand(
                _ => OpenFolderAndSelectFile(),
                _ => _offlineSource == null && DetailList.SelectedItem is ProjectARVRReuslt item && File.Exists(item.FileName));
            var batchHistoryCommand = new RelayCommand(
                _ => OpenBatchDataHistory(),
                _ => _offlineSource == null && DetailList.SelectedItem is ProjectARVRReuslt item && item.BatchId > 0);
            var flowExecutionAnalysisCommand = new RelayCommand(
                _ => OpenFlowExecutionAnalysis(),
                _ => DetailList.SelectedItem is ProjectARVRReuslt item && item.BatchId > 0);
            var viewTestResultCommand = new RelayCommand(
                _ => ViewTestResult(),
                _ => DetailList.SelectedItem is ProjectARVRReuslt item && (item.Id > 0 || !string.IsNullOrEmpty(item.ViewResultJson)));

            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(new MenuItem { Command = ApplicationCommands.Copy, Header = "复制" });
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem { Command = openFolderCommand, Header = "OpenFolderAndSelectFile" });
            contextMenu.Items.Add(new MenuItem { Command = batchHistoryCommand, Header = "流程结果查询" });
            contextMenu.Items.Add(new MenuItem { Command = flowExecutionAnalysisCommand, Header = "流程执行分析" });
            contextMenu.Items.Add(new MenuItem { Command = viewTestResultCommand, Header = "查看测试结果" });
            contextMenu.Opened += (_, _) => CommandManager.InvalidateRequerySuggested();

            DetailList.PreviewMouseRightButtonDown += (_, e) =>
            {
                DependencyObject? element = DetailList.InputHitTest(e.GetPosition(DetailList)) as DependencyObject;
                while (element != null && element is not ListViewItem)
                    element = VisualTreeHelper.GetParent(element);

                if (element is ListViewItem targetItem)
                    targetItem.IsSelected = true;
            };

            DetailList.ContextMenu = contextMenu;
        }

        private void OpenFolderAndSelectFile()
        {
            if (_offlineSource != null) return;
            if (DetailList.SelectedItem is ProjectARVRReuslt item && !string.IsNullOrWhiteSpace(item.FileName))
                PlatformHelper.OpenFolderAndSelectFile(item.FileName);
        }

        private void OpenBatchDataHistory()
        {
            if (_offlineSource != null) return;
            MeasureBatchModel? batch = GetSelectedMeasureBatch();
            if (batch == null)
            {
                MessageBox.Show(this, "找不到批次号，请检查流程配置", "ColorVision");
                return;
            }

            var frame = new Frame();
            new Window
            {
                Owner = this,
                Content = new MeasureBatchPage(frame, batch)
            }.Show();
        }

        private async void OpenFlowExecutionAnalysis()
        {
            if (_offlineSource != null)
            {
                if (DetailList.SelectedItem is not ProjectARVRReuslt result) return;
                try
                {
                    string serial = await Task.Run(() => _offlineSource.ResolveFlowSerialNumber(result));
                    if (_closed) return;
                    new FlowExecutionAnalysisWindow(_offlineSource.FlowDatabasePath!, _offlineSource.Label, result.BatchId, serial)
                    {
                        Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    }.Show();
                }
                catch (Exception ex) { if (!_closed) MessageBox.Show(this, ex.Message, "现场节点分析", MessageBoxButton.OK, MessageBoxImage.Information); }
                return;
            }
            MeasureBatchModel? batch = GetSelectedMeasureBatch();
            if (batch == null)
            {
                MessageBox.Show(this, "找不到批次号，请检查流程配置", "ColorVision");
                return;
            }

            new FlowExecutionAnalysisWindow(batch)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.Show();
        }

        private MeasureBatchModel? GetSelectedMeasureBatch()
        {
            if (_offlineSource != null) return null;
            if (DetailList.SelectedItem is not ProjectARVRReuslt item || item.BatchId <= 0)
                return null;

            using var db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = MySqlControl.GetConnectionString(),
                DbType = DbType.MySql,
                IsAutoCloseConnection = true
            });
            return db.Queryable<MeasureBatchModel>().Where(model => model.Id == item.BatchId).First();
        }

        private void ViewTestResult()
        {
            if (DetailList.SelectedItem is not ProjectARVRReuslt item)
                return;

            string? viewResultJson = _statisticsStore.LoadViewResultJson(item);
            if (string.IsNullOrEmpty(viewResultJson))
            {
                MessageBox.Show(this, "ViewResultJson为空", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            new TestResultViewWindow(viewResultJson)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog();
        }

        protected override void OnClosed(EventArgs e)
        {
            _closed = true;
            _windowLoaded = false;
            CaptureSearchState();
            ++_homeLoadVersion;
            ++_recordLoadVersion;
            ++_combinedLoadVersion;
            ++_flowLoadVersion;
            ++_snIndexVersion;
            ++_flowNameIndexVersion;
            ++_detailLoadVersion;
            ++_combinedDetailLoadVersion;
            if (_snEditor != null)
                _snEditor.TextChanged -= SnEditor_TextChanged;
            if (_flowNameEditor != null)
                _flowNameEditor.TextChanged -= FlowNameEditor_TextChanged;
            RecordDataGrid.SelectionChanged -= RecordDataGrid_SelectionChanged;
            CombinedRecordDataGrid.SelectionChanged -= CombinedRecordDataGrid_SelectionChanged;
            base.OnClosed(e);
        }
    }
}
