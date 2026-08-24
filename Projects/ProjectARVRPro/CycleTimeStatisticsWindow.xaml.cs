using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Solution.Editor.AvalonEditor;
using ColorVision.UI;
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
        private readonly ViewResultManager _viewResultManager = ViewResultManager.GetInstance();
        private readonly ResultStatisticsDataStore _statisticsStore = ResultStatisticsDataStore.Instance;
        private readonly ResultStatisticsWindowState _windowState = ProjectARVRProConfig.Instance.ResultStatisticsWindowState ??= new();
        private readonly ObservableCollection<ResultStatisticsRecordRow> _recordRows = [];
        private readonly ObservableCollection<FlowExecutionRecordRow> _flowRows = [];
        private string[] _snSuggestions = [];
        private string[] _flowNameSuggestions = [];
        private readonly ObservableCollection<ProjectARVRReuslt> _details = [];
        private readonly Dictionary<int, ObjectiveTestResultRecord> _recordCache = [];
        private TextBox? _snEditor;
        private TextBox? _flowNameEditor;
        private int _homeLoadVersion;
        private int _recordLoadVersion;
        private int _flowLoadVersion;
        private int _snIndexVersion;
        private int _flowNameIndexVersion;
        private int _detailLoadVersion;
        private int _currentPage = 1;
        private int _totalRecordCount;
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
        private string _snIndexStatus = string.Empty;
        private string _flowStatus = string.Empty;
        private string _flowNameIndexStatus = string.Empty;

        public CycleTimeStatisticsWindow()
        {
            InitializeComponent();
            RestoreSearchState();
            RecordDataGrid.ItemsSource = _recordRows;
            FlowDataGrid.ItemsSource = _flowRows;
            DetailList.ItemsSource = _details;
            RecordDataGrid.SelectionChanged += RecordDataGrid_SelectionChanged;
            BuildDetailContextMenu();
            ConfigureHomeTrendPlot();
            ApplyStatistics(new ResultStatistics());
            _restoringSearchState = false;
            UpdateHomePeriodText();
            UpdateRecordPeriodText();
            UpdateFlowPeriodText();
        }

        private ResultStatisticsRecordRow? SelectedRecordRow => RecordDataGrid.SelectedItem as ResultStatisticsRecordRow;
        private FlowExecutionRecordRow? SelectedFlowRow => FlowDataGrid.SelectedItem as FlowExecutionRecordRow;

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _windowLoaded = true;
            _ = LoadSnSuggestionsAsync();
            var tasks = new List<Task> { RefreshHomeAsync(), RefreshRecordsAsync(1) };
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

        private void HomePreviousPeriod_Click(object sender, RoutedEventArgs e)
        {
            ShiftPeriod(HomePeriodMode, HomeAnchorDatePicker, -1);
        }

        private void HomeNextPeriod_Click(object sender, RoutedEventArgs e)
        {
            ShiftPeriod(HomePeriodMode, HomeAnchorDatePicker, 1);
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
            FlowPeriodMode.SelectedIndex = GetPeriodModeIndex(_windowState.FlowPeriodMode);
            FlowAnchorDatePicker.SelectedDate = NormalizeAnchorDate(_windowState.FlowAnchorDate);
            FlowNameFilter.Text = _windowState.FlowName ?? string.Empty;
            FlowResultFilter.SelectedIndex = Math.Clamp(_windowState.FlowResultIndex, 0, 2);
            StatisticsTabs.SelectedIndex = Math.Clamp(_windowState.SelectedTabIndex, 0, StatisticsTabs.Items.Count - 1);
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
            _windowState.FlowPeriodMode = GetSelectedPeriodMode(FlowPeriodMode);
            _windowState.FlowAnchorDate = (FlowAnchorDatePicker.SelectedDate ?? DateTime.Today).Date;
            _windowState.FlowName = FlowNameFilter.Text?.Trim() ?? string.Empty;
            _windowState.FlowResultIndex = Math.Clamp(FlowResultFilter.SelectedIndex, 0, 2);
        }

        private void SaveSearchState()
        {
            CaptureSearchState();
            try
            {
                ConfigService.Instance.Save<ProjectARVRProConfig>();
            }
            catch (Exception ex)
            {
                Log.Warn("Could not save the ARVRPro result-statistics search state.", ex);
            }
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
            if (HomePeriodText == null || HomePeriodMode == null || HomeAnchorDatePicker == null)
                return;

            ResultStatisticsPeriodMode mode = GetSelectedPeriodMode(HomePeriodMode);
            ResultStatisticsPeriodRange range = ResultStatisticsPeriod.GetRange(mode, HomeAnchorDatePicker.SelectedDate ?? DateTime.Today);
            HomePeriodText.Text = $"查询范围：{range.ToDisplayText(mode)}";
            HomePeriodNavigation.Visibility = mode == ResultStatisticsPeriodMode.All ? Visibility.Collapsed : Visibility.Visible;
        }

        private void UpdateRecordPeriodText()
        {
            if (RecordPeriodText == null || RecordPeriodMode == null || RecordAnchorDatePicker == null)
                return;

            ResultStatisticsPeriodMode mode = GetSelectedPeriodMode(RecordPeriodMode);
            ResultStatisticsPeriodRange range = ResultStatisticsPeriod.GetRange(mode, RecordAnchorDatePicker.SelectedDate ?? DateTime.Today);
            RecordPeriodText.Text = $"查询范围：{range.ToDisplayText(mode)}";
            RecordPeriodNavigation.Visibility = mode == ResultStatisticsPeriodMode.All ? Visibility.Collapsed : Visibility.Visible;
        }

        private void UpdateFlowPeriodText()
        {
            if (FlowPeriodText == null || FlowPeriodMode == null || FlowAnchorDatePicker == null)
                return;

            ResultStatisticsPeriodMode mode = GetSelectedPeriodMode(FlowPeriodMode);
            ResultStatisticsPeriodRange range = ResultStatisticsPeriod.GetRange(mode, FlowAnchorDatePicker.SelectedDate ?? DateTime.Today);
            FlowPeriodText.Text = $"查询范围：{range.ToDisplayText(mode)}";
            FlowPeriodNavigation.Visibility = mode == ResultStatisticsPeriodMode.All ? Visibility.Collapsed : Visibility.Visible;
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
                ResultStatisticsDashboard dashboard = await Task.Run(() => _statisticsStore.QueryDashboard(query, mode, now));

                if (loadVersion != _homeLoadVersion)
                    return;

                ApplyStatistics(dashboard.Summary);
                RenderHomeTrend(dashboard.Trend, mode, query.From, query.ToExclusive);
                _homeStatus = $"已查询 {dashboard.Summary.TotalCount:N0} 条记录";
            }
            catch (Exception ex)
            {
                if (loadVersion != _homeLoadVersion)
                    return;

                ApplyStatistics(new ResultStatistics());
                RenderHomeTrend([], mode, query.From, query.ToExclusive);
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
            FailRateText.Text = statistics.FailRateText;
            AverageCtText.Text = statistics.AverageCtText;
            CurrentHourCountText.Text = statistics.CurrentHourCount.ToString("N0");
            TodayCountText.Text = statistics.TodayCount.ToString("N0");
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
            ConfigureHomeTrendPresentation(ResultStatisticsPeriodMode.Day);
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

        private void ConfigureHomeTrendPresentation(ResultStatisticsPeriodMode mode)
        {
            if (mode == ResultStatisticsPeriodMode.All)
            {
                HomeTrendPlot.Plot.Title("月产量与平均整组 CT");
                HomeTrendPlot.Plot.YLabel("产量（组）");
                HomeTrendPlot.Plot.Axes.Right.Label.Text = "平均 CT（秒）";
            }
            else
            {
                HomeTrendPlot.Plot.Title("逐条整组 CT 与累计产量");
                HomeTrendPlot.Plot.YLabel("整组 CT（秒）");
                HomeTrendPlot.Plot.Axes.Right.Label.Text = "累计产量（组）";
            }
        }

        private void RenderHomeTrend(
            IReadOnlyList<ResultStatisticsTrendPoint> points,
            ResultStatisticsPeriodMode mode,
            DateTime from,
            DateTime toExclusive)
        {
            HomeTrendPlot.Plot.Clear();
            ConfigureHomeTrendPresentation(mode);
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

                bool useLegacy = _viewResultManager.Config.UseLegacyARVROutput;
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
                return;
            }

            _ = LoadFlowDetailsAsync(selectedRow);
        }

        private async Task LoadFlowDetailsAsync(ResultStatisticsRecordRow row)
        {
            int loadVersion = ++_detailLoadVersion;
            DetailHeader.Text = $"{row.SN} - 正在读取流程 CT 明细...";
            try
            {
                IReadOnlyList<ProjectARVRReuslt> details = await Task.Run(() => _statisticsStore.QueryFlowDetails(row));
                if (loadVersion != _detailLoadVersion || SelectedRecordRow != row)
                    return;

                ReplaceItems(_details, details);
                double flowMilliseconds = details.Sum(item => Convert.ToDouble(item.RunTime));
                DetailHeader.Text = $"{row.SN} - 整组 CT {row.CycleTimeText}（含切图）- {details.Count:N0} 个流程，流程耗时合计 {ResultStatisticsCalculator.FormatMilliseconds(flowMilliseconds)}";
            }
            catch (Exception ex)
            {
                if (loadVersion != _detailLoadVersion)
                    return;

                _details.Clear();
                DetailHeader.Text = $"{row.SN} - 流程 CT 明细读取失败";
                MessageBox.Show(this, $"读取流程 CT 明细失败：{ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BuildDetailContextMenu()
        {
            DetailList.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, ListViewUtils.Copy, (_, e) => e.CanExecute = DetailList.SelectedItems.Count > 0));

            var openFolderCommand = new RelayCommand(
                _ => OpenFolderAndSelectFile(),
                _ => DetailList.SelectedItem is ProjectARVRReuslt item && File.Exists(item.FileName));
            var batchHistoryCommand = new RelayCommand(
                _ => OpenBatchDataHistory(),
                _ => DetailList.SelectedItem is ProjectARVRReuslt item && item.BatchId > 0);
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
            if (DetailList.SelectedItem is ProjectARVRReuslt item && !string.IsNullOrWhiteSpace(item.FileName))
                PlatformHelper.OpenFolderAndSelectFile(item.FileName);
        }

        private void OpenBatchDataHistory()
        {
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

        private void OpenFlowExecutionAnalysis()
        {
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
            _windowLoaded = false;
            SaveSearchState();
            ++_homeLoadVersion;
            ++_recordLoadVersion;
            ++_flowLoadVersion;
            ++_snIndexVersion;
            ++_flowNameIndexVersion;
            ++_detailLoadVersion;
            if (_snEditor != null)
                _snEditor.TextChanged -= SnEditor_TextChanged;
            if (_flowNameEditor != null)
                _flowNameEditor.TextChanged -= FlowNameEditor_TextChanged;
            RecordDataGrid.SelectionChanged -= RecordDataGrid_SelectionChanged;
            base.OnClosed(e);
        }
    }
}
