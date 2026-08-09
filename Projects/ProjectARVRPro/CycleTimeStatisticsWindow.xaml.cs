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
using System.Collections.Specialized;
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
        private const int SnSuggestionDisplayLimit = 20;
        private static readonly ILog Log = LogManager.GetLogger(typeof(CycleTimeStatisticsWindow));
        private readonly ViewResultManager _viewResultManager = ViewResultManager.GetInstance();
        private readonly ResultStatisticsDataStore _statisticsStore = ResultStatisticsDataStore.Instance;
        private readonly ObservableCollection<ResultStatisticsRecordRow> _recordRows = [];
        private string[] _snSuggestions = [];
        private readonly ObservableCollection<ProjectARVRReuslt> _details = [];
        private readonly Dictionary<int, ObjectiveTestResultRecord> _recordCache = [];
        private CopilotDynamicContextSession? _copilotContextSession;
        private TextBox? _snEditor;
        private int _copilotPublishQueued;
        private int _loadVersion;
        private int _snIndexVersion;
        private int _detailLoadVersion;
        private int _currentPage = 1;
        private int _totalRecordCount;
        private bool _queryAllDates;
        private bool _settingDateRange;
        private bool _updatingSnSuggestions;
        private string _queryStatus = string.Empty;
        private string _snIndexStatus = string.Empty;

        public CycleTimeStatisticsWindow()
        {
            InitializeComponent();
            StartDatePicker.SelectedDate = DateTime.Today;
            EndDatePicker.SelectedDate = DateTime.Today;
            RecordDataGrid.ItemsSource = _recordRows;
            DetailList.ItemsSource = _details;
            RecordDataGrid.SelectionChanged += RecordDataGrid_SelectionChanged;
            _recordRows.CollectionChanged += RecordRows_CollectionChanged;
            BuildDetailContextMenu();
            RegisterCopilotContext();
            ApplyStatistics(new ResultStatistics());
        }

        private ResultStatisticsRecordRow? SelectedRecordRow => RecordDataGrid.SelectedItem as ResultStatisticsRecordRow;

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _ = LoadSnSuggestionsAsync();
            await RefreshAsync(1);
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await RefreshAsync(1);
        }

        private async void Reset_Click(object sender, RoutedEventArgs e)
        {
            SetDateRange(DateTime.Today, DateTime.Today, queryAllDates: false);
            SnFilter.Text = string.Empty;
            ResultFilter.SelectedIndex = 0;
            await RefreshAsync(1);
        }

        private async void QuickRange_Click(object sender, RoutedEventArgs e)
        {
            DateTime today = DateTime.Today;
            string range = (sender as FrameworkElement)?.Tag as string ?? "Today";
            switch (range)
            {
                case "Week":
                    int daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
                    SetDateRange(today.AddDays(-daysSinceMonday), today, queryAllDates: false);
                    break;
                case "Month":
                    SetDateRange(new DateTime(today.Year, today.Month, 1), today, queryAllDates: false);
                    break;
                case "All":
                    SetDateRange(null, null, queryAllDates: true);
                    break;
                default:
                    SetDateRange(today, today, queryAllDates: false);
                    break;
            }

            SnFilter.Text = string.Empty;
            ResultFilter.SelectedIndex = 0;
            await RefreshAsync(1);
        }

        private void SetDateRange(DateTime? start, DateTime? end, bool queryAllDates)
        {
            _settingDateRange = true;
            StartDatePicker.SelectedDate = start;
            EndDatePicker.SelectedDate = end;
            _queryAllDates = queryAllDates;
            _settingDateRange = false;
        }

        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_settingDateRange)
                _queryAllDates = false;
        }

        private async Task RefreshAsync(int pageNumber)
        {
            if (!TryCreateQuery(pageNumber, out ResultStatisticsQuery query))
                return;

            int loadVersion = ++_loadVersion;
            RefreshButton.IsEnabled = false;
            _queryStatus = "正在查询统计和记录...";
            UpdateStatusText();
            DetailHeader.Text = "流程 CT 明细";
            _details.Clear();

            try
            {
                DateTime now = DateTime.Now;
                Task<ResultStatistics> statisticsTask = Task.Run(() => _statisticsStore.QueryStatistics(query, now));
                Task<IReadOnlyList<ResultStatisticsRecordRow>> recordsTask = Task.Run(() => _statisticsStore.QueryRecords(query));
                await Task.WhenAll(statisticsTask, recordsTask);

                if (loadVersion != _loadVersion)
                    return;

                ResultStatistics statistics = await statisticsTask;
                _recordCache.Clear();
                ReplaceItems(_recordRows, await recordsTask);
                if (_recordRows.Count > 0)
                    RecordDataGrid.SelectedIndex = 0;
                ApplyStatistics(statistics);
                _totalRecordCount = statistics.TotalCount;
                _currentPage = Math.Clamp(pageNumber, 1, Math.Max(1, GetPageCount()));
                UpdatePagination();
                _queryStatus = _totalRecordCount > RecordPageSize
                    ? $"已查询 {_totalRecordCount:N0} 条批次记录；第 {_currentPage:N0}/{GetPageCount():N0} 页，本页 {_recordRows.Count:N0} 条"
                    : $"已查询 {_totalRecordCount:N0} 条批次记录";
            }
            catch (Exception ex)
            {
                if (loadVersion != _loadVersion)
                    return;

                _queryStatus = "查询失败";
                MessageBox.Show(this, $"读取结果统计失败：{ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (loadVersion == _loadVersion)
                {
                    RefreshButton.IsEnabled = true;
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

        private async void FirstPage_Click(object sender, RoutedEventArgs e)
        {
            await RefreshAsync(1);
        }

        private async void PreviousPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
                await RefreshAsync(_currentPage - 1);
        }

        private async void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < GetPageCount())
                await RefreshAsync(_currentPage + 1);
        }

        private async void LastPage_Click(object sender, RoutedEventArgs e)
        {
            await RefreshAsync(GetPageCount());
        }

        private bool TryCreateQuery(int pageNumber, out ResultStatisticsQuery query)
        {
            DateTime from;
            DateTime toExclusive;
            if (_queryAllDates)
            {
                from = DateTime.MinValue;
                toExclusive = DateTime.MaxValue;
            }
            else
            {
                from = (StartDatePicker.SelectedDate ?? DateTime.Today).Date;
                DateTime end = (EndDatePicker.SelectedDate ?? from).Date;
                if (end < from)
                {
                    query = null!;
                    MessageBox.Show(this, "结束日期不能早于开始日期。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                    return false;
                }
                toExclusive = end == DateTime.MaxValue.Date ? DateTime.MaxValue : end.AddDays(1);
            }

            bool? result = ResultFilter.SelectedIndex switch
            {
                1 => true,
                2 => false,
                _ => null,
            };
            string sn = SnFilter.Text.Trim();
            query = new ResultStatisticsQuery
            {
                From = from,
                ToExclusive = toExclusive,
                SN = string.IsNullOrWhiteSpace(sn) ? null : sn,
                Result = result,
                PageNumber = Math.Max(1, pageNumber),
                PageSize = RecordPageSize,
            };
            return true;
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
                _snIndexStatus = $"可检索 {_snSuggestions.Length:N0} 个 SN，下拉最多显示 {SnSuggestionDisplayLimit} 个匹配项";
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
                SnSuggestionDisplayLimit);

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

        private void UpdateStatusText()
        {
            HomeStatusText.Text = _queryStatus;
            QueryStatusText.Text = string.Join("；", new[] { _queryStatus, _snIndexStatus }.Where(item => !string.IsNullOrWhiteSpace(item)));
        }

        private static void ReplaceItems<T>(ObservableCollection<T> target, IEnumerable<T> source)
        {
            target.Clear();
            foreach (T item in source)
                target.Add(item);
        }

        private void RecordDataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject? element = RecordDataGrid.InputHitTest(e.GetPosition(RecordDataGrid)) as DependencyObject;
            while (element != null && element is not DataGridRow)
                element = VisualTreeHelper.GetParent(element);

            if (element is DataGridRow row && !row.IsSelected)
            {
                RecordDataGrid.SelectedItems.Clear();
                row.IsSelected = true;
            }
        }

        private async void RecordDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            await ViewSelectedItemsAsync();
        }

        private async void ViewItems_Click(object sender, RoutedEventArgs e)
        {
            await ViewSelectedItemsAsync();
        }

        private async Task ViewSelectedItemsAsync()
        {
            ObjectiveTestResultRecord? record = await LoadSelectedRecordAsync();
            if (record == null)
                return;

            new TestResultViewWindow(record.ObjectiveTestResultJson)
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

            var control = new AvalonEditControll();
            control.SetJsonText(record.ObjectiveTestResultJson);
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

                ObjectiveTestResult? result = JsonConvert.DeserializeObject<ObjectiveTestResult>(record.ObjectiveTestResultJson);
                if (result == null)
                {
                    MessageBox.Show(this, "ObjectiveTestResult 为空，无法导出。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                bool useLegacy = _viewResultManager.Config.UseLegacyARVROutput;
                await Task.Run(() =>
                {
                    if (useLegacy)
                    {
                        LegacyARVRObjectiveTestResult legacyResult = LegacyARVRConverter.ToLegacy(result);
                        LegacyARVRCsvExporter.ExportToCsv(new List<LegacyARVRObjectiveTestResult> { legacyResult }, fileName);
                    }
                    else
                    {
                        ObjectiveTestResultCsvExporter.ExportToCsv(result, fileName);
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
                    ObjectiveTestResultBatchCsvExporter.ExportToCsv(records, fileName);
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

        private async void RecordDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _copilotContextSession?.Activate();
            ResultStatisticsRecordRow? selectedRow = SelectedRecordRow;
            if (selectedRow == null)
            {
                ++_detailLoadVersion;
                _details.Clear();
                DetailHeader.Text = "流程 CT 明细";
                QueueCopilotContextPublish();
                return;
            }

            _ = LoadFlowDetailsAsync(selectedRow);
            if (!_recordCache.ContainsKey(selectedRow.Id))
            {
                try
                {
                    await LoadRecordAsync(selectedRow);
                }
                catch (Exception ex)
                {
                    Log.Debug($"Could not load the selected ARVRPro statistics record for Copilot context: {ex.Message}");
                }
            }

            QueueCopilotContextPublish();
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
                DetailHeader.Text = $"{row.SN} - 整组 CT {row.CycleTimeText} - {details.Count:N0} 个流程，流程耗时合计 {ResultStatisticsCalculator.FormatMilliseconds(flowMilliseconds)}";
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

        private void RecordRows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            QueueCopilotContextPublish();
        }

        private void RegisterCopilotContext()
        {
            try
            {
                _copilotContextSession = ProjectARVRCopilotContextHub.Shared.Register(
                    CaptureCopilotSnapshotAsync,
                    typeof(CycleTimeStatisticsWindow).Assembly.GetName().Version?.ToString());
            }
            catch (Exception ex)
            {
                Log.Warn("Could not register the ARVRPro result-statistics Copilot context; the statistics window will continue to operate.", ex);
            }
        }

        private async Task<CopilotProjectResultContextSnapshot?> CaptureCopilotSnapshotAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Dispatcher.CheckAccess())
            {
                return await Dispatcher.InvokeAsync(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return CaptureCopilotSnapshot();
                });
            }

            return CaptureCopilotSnapshot();
        }

        private CopilotProjectResultContextSnapshot CaptureCopilotSnapshot()
        {
            ObjectiveTestResultRecord[] records = _recordRows.Select(CreateContextRecord).ToArray();
            ObjectiveTestResultRecord? selected = SelectedRecordRow is ResultStatisticsRecordRow row
                ? CreateContextRecord(row)
                : null;
            return ProjectARVRCopilotSnapshotFactory.CreateForObjectiveResultRecords(
                "ARVRPro result statistics",
                records,
                selected);
        }

        private ObjectiveTestResultRecord CreateContextRecord(ResultStatisticsRecordRow row)
        {
            if (_recordCache.TryGetValue(row.Id, out ObjectiveTestResultRecord? record))
                return record;

            return new ObjectiveTestResultRecord
            {
                Id = row.Id,
                ResultId = row.ResultId,
                BatchId = row.BatchId,
                SN = row.SN,
                LastModel = row.LastModel,
                LastFlowStatus = "Completed",
                Msg = row.Msg,
                LastResult = row.Result,
                TotalResult = row.Result,
                CreateTime = row.StartTime,
                UpdateTime = row.EndTime,
            };
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            _copilotContextSession?.Activate();
            PublishCopilotContext();
        }

        private void QueueCopilotContextPublish()
        {
            if (Interlocked.Exchange(ref _copilotPublishQueued, 1) != 0)
                return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                Interlocked.Exchange(ref _copilotPublishQueued, 0);
                PublishCopilotContext();
            }));
        }

        private void PublishCopilotContext()
        {
            if (_copilotContextSession?.IsCurrent != true || !IsActive)
                return;

            try
            {
                var item = CopilotBusinessContextBuilder.BuildProjectResultContextItem(CaptureCopilotSnapshot());
                CopilotBusinessContextCoordinator.Publish(CopilotBusinessContextBundle.FromItem(
                    ProjectARVRCopilotAgentExtension.SourceId,
                    item));
            }
            catch (Exception ex)
            {
                Log.Debug($"Could not publish the active ARVRPro result-statistics context to Copilot: {ex.Message}");
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
                _ => DetailList.SelectedItem is ProjectARVRReuslt item && !string.IsNullOrEmpty(item.ViewResultJson));

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
            if (DetailList.SelectedItem is not ProjectARVRReuslt item || string.IsNullOrEmpty(item.ViewResultJson))
                return;

            new TestResultViewWindow(item.ViewResultJson)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog();
        }

        protected override void OnClosed(EventArgs e)
        {
            ++_loadVersion;
            ++_snIndexVersion;
            ++_detailLoadVersion;
            if (_snEditor != null)
                _snEditor.TextChanged -= SnEditor_TextChanged;
            RecordDataGrid.SelectionChanged -= RecordDataGrid_SelectionChanged;
            _recordRows.CollectionChanged -= RecordRows_CollectionChanged;
            bool wasCurrent = _copilotContextSession?.IsCurrent == true;
            _copilotContextSession?.Dispose();
            _copilotContextSession = null;
            if (wasCurrent)
                CopilotLiveContextRegistry.Clear(ProjectARVRCopilotAgentExtension.SourceId);
            base.OnClosed(e);
        }
    }
}
