using ColorVision.UI;
using log4net;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProjectKB
{
    public partial class KBProductionStatisticsWindow : Window
    {
        private const int RecordPageSize = 1000;
        private const int SuggestionDisplayLimit = 20;
        private const string AllModelsText = "全部机种";
        private static readonly ILog Log = LogManager.GetLogger(typeof(KBProductionStatisticsWindow));
        private readonly KBProductionDataStore _statisticsStore = KBProductionDataStore.Instance;
        private readonly KBProductionStatisticsWindowState _windowState = ProjectKBConfig.Instance.ProductionStatisticsWindowState ??= new();
        private readonly ObservableCollection<KBHourlyProductionRow> _hourlyRows = [];
        private readonly ObservableCollection<KBDailyProductionRow> _dailyRows = [];
        private readonly ObservableCollection<KBProductionSessionRow> _sessionRows = [];
        private readonly ObservableCollection<KBProductionRecordRow> _recordRows = [];
        private string[] _modelSuggestions = [];
        private string[] _snSuggestions = [];
        private TextBox? _modelEditor;
        private TextBox? _snEditor;
        private int _loadVersion;
        private int _suggestionLoadVersion;
        private int _currentPage = 1;
        private int _totalRecordCount;
        private bool _updatingModelSuggestions;
        private bool _updatingSnSuggestions;
        private bool _restoringState = true;

        public KBProductionStatisticsWindow()
        {
            InitializeComponent();
            RestoreState();
            HourlyGrid.ItemsSource = _hourlyRows;
            DailyGrid.ItemsSource = _dailyRows;
            SessionGrid.ItemsSource = _sessionRows;
            RecordGrid.ItemsSource = _recordRows;
            ConfigureHomeTrendPlot();
            ApplyStatistics(new KBProductionStatistics());
            RenderHomeTrend([], KBProductionPeriodMode.Day, DateTime.Today, DateTime.Today.AddDays(1));
            _restoringState = false;
            UpdatePeriodPresentation();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _ = LoadSuggestionsAsync();
            await RefreshAllAsync(_currentPage);
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            CaptureState();
            await RefreshAllAsync(1);
        }

        private async void Reset_Click(object sender, RoutedEventArgs e)
        {
            PeriodMode.SelectedIndex = 0;
            AnchorDatePicker.SelectedDate = DateTime.Today;
            SetComboBoxText(ModelFilter, _modelEditor, AllModelsText);
            SetComboBoxText(SnFilter, _snEditor, string.Empty);
            ResultFilter.SelectedIndex = 0;
            UpdatePeriodPresentation();
            CaptureState();
            await RefreshAllAsync(1);
        }

        private async void EditProductionSettings_Click(object sender, RoutedEventArgs e)
        {
            SummaryManager.GetInstance().Edit();
            await RefreshAllAsync(_currentPage);
        }

        private void StatisticsTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source == StatisticsTabs)
                CaptureState();
        }

        private void PeriodMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePeriodPresentation();
            CaptureState();
        }

        private void AnchorDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePeriodPresentation();
            CaptureState();
        }

        private async void PreviousPeriod_Click(object sender, RoutedEventArgs e)
        {
            ShiftPeriod(-1);
            await RefreshAllAsync(1);
        }

        private async void NextPeriod_Click(object sender, RoutedEventArgs e)
        {
            ShiftPeriod(1);
            await RefreshAllAsync(1);
        }

        private async void CurrentPeriod_Click(object sender, RoutedEventArgs e)
        {
            AnchorDatePicker.SelectedDate = DateTime.Today;
            await RefreshAllAsync(1);
        }

        private void ShiftPeriod(int offset)
        {
            KBProductionPeriodMode mode = GetSelectedPeriodMode();
            DateTime anchor = AnchorDatePicker.SelectedDate ?? DateTime.Today;
            AnchorDatePicker.SelectedDate = KBProductionPeriod.ShiftAnchor(mode, anchor, offset);
        }

        private async Task RefreshAllAsync(int pageNumber)
        {
            KBProductionQuery query = CreateQuery(pageNumber);
            int loadVersion = ++_loadVersion;
            RefreshButton.IsEnabled = false;
            QueryStatusText.Text = "正在读取生产统计和检测记录...";

            try
            {
                DateTime now = DateTime.Now;
                Task<KBProductionStatistics> statisticsTask = Task.Run(() => _statisticsStore.QueryStatistics(query, now));
                Task<int> countTask = Task.Run(() => _statisticsStore.QueryRecordCount(query));
                Task<IReadOnlyList<KBProductionRecordRow>> recordsTask = Task.Run(() => _statisticsStore.QueryRecords(query));
                await Task.WhenAll(statisticsTask, countTask, recordsTask);
                if (loadVersion != _loadVersion)
                    return;

                _totalRecordCount = await countTask;
                int requestedPage = Math.Max(1, pageNumber);
                int actualPage = Math.Clamp(requestedPage, 1, GetPageCount());
                IReadOnlyList<KBProductionRecordRow> records = await recordsTask;
                if (actualPage != requestedPage)
                {
                    KBProductionQuery correctedQuery = CreateQuery(actualPage);
                    records = await Task.Run(() => _statisticsStore.QueryRecords(correctedQuery));
                    if (loadVersion != _loadVersion)
                        return;
                }

                KBProductionStatistics statistics = await statisticsTask;
                ApplyStatistics(statistics);
                ReplaceRows(_recordRows, records);
                _currentPage = actualPage;
                UpdatePagination();
                RenderHomeTrend(statistics.TrendRows, query.PeriodMode, query.From, query.ToExclusive);
                CaptureState();
                QueryStatusText.Text = _totalRecordCount > RecordPageSize
                    ? $"已查询 {_totalRecordCount:N0} 条记录；第 {_currentPage:N0}/{GetPageCount():N0} 页，本页 {_recordRows.Count:N0} 条"
                    : $"已查询 {_totalRecordCount:N0} 条记录，有效产量 {statistics.ProductionCount:N0}";
            }
            catch (Exception ex)
            {
                if (loadVersion == _loadVersion)
                {
                    QueryStatusText.Text = "查询失败";
                    MessageBox.Show(this, $"读取生产统计失败：{ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                if (loadVersion == _loadVersion)
                    RefreshButton.IsEnabled = true;
            }
        }

        private KBProductionQuery CreateQuery(int pageNumber)
        {
            KBProductionPeriodMode mode = GetSelectedPeriodMode();
            KBProductionPeriodRange range = KBProductionPeriod.GetRange(mode, AnchorDatePicker.SelectedDate ?? DateTime.Today);
            string model = NormalizeModel(ModelFilter.Text);
            string sn = SnFilter.Text.Trim();
            bool? result = ResultFilter.SelectedIndex switch
            {
                1 => true,
                2 => false,
                _ => null,
            };
            return new KBProductionQuery
            {
                From = range.From,
                ToExclusive = range.ToExclusive,
                PeriodMode = mode,
                Model = string.IsNullOrWhiteSpace(model) ? null : model,
                SN = string.IsNullOrWhiteSpace(sn) ? null : sn,
                Result = result,
                PageNumber = Math.Max(1, pageNumber),
                PageSize = RecordPageSize
            };
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
            await RefreshAllAsync(1);
        }

        private async void PreviousPage_Click(object sender, RoutedEventArgs e)
        {
            await RefreshAllAsync(Math.Max(1, _currentPage - 1));
        }

        private async void NextPage_Click(object sender, RoutedEventArgs e)
        {
            await RefreshAllAsync(Math.Min(GetPageCount(), _currentPage + 1));
        }

        private async void LastPage_Click(object sender, RoutedEventArgs e)
        {
            await RefreshAllAsync(GetPageCount());
        }

        private void UpdatePeriodPresentation()
        {
            if (PeriodText == null || PeriodMode == null || AnchorDatePicker == null)
                return;

            KBProductionPeriodMode mode = GetSelectedPeriodMode();
            KBProductionPeriodRange range = KBProductionPeriod.GetRange(mode, AnchorDatePicker.SelectedDate ?? DateTime.Today);
            PeriodText.Text = $"查询范围：{range.ToDisplayText(mode)}";
            PeriodNavigation.Visibility = mode == KBProductionPeriodMode.All ? Visibility.Collapsed : Visibility.Visible;
            CurrentPeriodButton.Content = mode switch
            {
                KBProductionPeriodMode.Week => "本周",
                KBProductionPeriodMode.Month => "本月",
                _ => "今天",
            };
        }

        private KBProductionPeriodMode GetSelectedPeriodMode()
        {
            return PeriodMode.SelectedIndex switch
            {
                1 => KBProductionPeriodMode.Week,
                2 => KBProductionPeriodMode.Month,
                3 => KBProductionPeriodMode.All,
                _ => KBProductionPeriodMode.Day,
            };
        }

        private static int GetPeriodModeIndex(KBProductionPeriodMode mode)
        {
            return mode switch
            {
                KBProductionPeriodMode.Week => 1,
                KBProductionPeriodMode.Month => 2,
                KBProductionPeriodMode.All => 3,
                _ => 0,
            };
        }

        private void RestoreState()
        {
            PeriodMode.SelectedIndex = GetPeriodModeIndex(_windowState.PeriodMode);
            AnchorDatePicker.SelectedDate = _windowState.AnchorDate == default ? DateTime.Today : _windowState.AnchorDate.Date;
            ModelFilter.Text = string.IsNullOrWhiteSpace(_windowState.Model) ? AllModelsText : _windowState.Model;
            SnFilter.Text = _windowState.SN ?? string.Empty;
            ResultFilter.SelectedIndex = Math.Clamp(_windowState.ResultIndex, 0, 2);
            StatisticsTabs.SelectedIndex = Math.Clamp(_windowState.SelectedTabIndex, 0, StatisticsTabs.Items.Count - 1);
            _currentPage = Math.Max(1, _windowState.PageNumber);
        }

        private void CaptureState()
        {
            if (_restoringState || StatisticsTabs == null)
                return;

            _windowState.SelectedTabIndex = Math.Max(0, StatisticsTabs.SelectedIndex);
            _windowState.PeriodMode = GetSelectedPeriodMode();
            _windowState.AnchorDate = (AnchorDatePicker.SelectedDate ?? DateTime.Today).Date;
            _windowState.Model = NormalizeModel(ModelFilter.Text);
            _windowState.SN = SnFilter.Text?.Trim() ?? string.Empty;
            _windowState.ResultIndex = Math.Clamp(ResultFilter.SelectedIndex, 0, 2);
            _windowState.PageNumber = Math.Max(1, _currentPage);
        }

        private void SaveState()
        {
            CaptureState();
            try
            {
                ConfigService.Instance.Save<ProjectKBConfig>();
            }
            catch (Exception ex)
            {
                Log.Warn("Could not save the ProjectKB production-statistics window state.", ex);
            }
        }

        private async Task LoadSuggestionsAsync()
        {
            int version = ++_suggestionLoadVersion;
            try
            {
                Task<IReadOnlyList<string>> modelsTask = Task.Run(_statisticsStore.QueryModels);
                Task<IReadOnlyList<string>> serialNumbersTask = Task.Run(_statisticsStore.QuerySerialNumbers);
                await Task.WhenAll(modelsTask, serialNumbersTask);
                if (version != _suggestionLoadVersion)
                    return;

                _modelSuggestions = [AllModelsText, .. await modelsTask];
                _snSuggestions = (await serialNumbersTask).ToArray();
                UpdateModelSuggestions(ModelFilter.Text, openDropDown: false);
                UpdateSnSuggestions(SnFilter.Text, openDropDown: false);
            }
            catch (Exception ex)
            {
                if (version == _suggestionLoadVersion)
                    Log.Warn("Could not load ProjectKB production-statistics search suggestions.", ex);
            }
        }

        private void ModelFilter_Loaded(object sender, RoutedEventArgs e)
        {
            if (_modelEditor != null)
                _modelEditor.TextChanged -= ModelEditor_TextChanged;
            ModelFilter.ApplyTemplate();
            _modelEditor = ModelFilter.Template.FindName("PART_EditableTextBox", ModelFilter) as TextBox;
            if (_modelEditor != null)
                _modelEditor.TextChanged += ModelEditor_TextChanged;
            UpdateModelSuggestions(ModelFilter.Text, openDropDown: false);
        }

        private void ModelEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_updatingModelSuggestions)
                UpdateModelSuggestions(_modelEditor?.Text, openDropDown: true);
        }

        private void ModelFilter_DropDownOpened(object sender, EventArgs e)
        {
            if (!_updatingModelSuggestions)
                UpdateModelSuggestions(_modelEditor?.Text ?? ModelFilter.Text, openDropDown: false);
        }

        private void ModelFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingModelSuggestions || ModelFilter.SelectedItem is not string selected)
                return;

            _updatingModelSuggestions = true;
            SetComboBoxText(ModelFilter, _modelEditor, selected);
            ModelFilter.IsDropDownOpen = false;
            _updatingModelSuggestions = false;
        }

        private void UpdateModelSuggestions(string? text, bool openDropDown)
        {
            string input = text ?? string.Empty;
            IReadOnlyList<string> matches = KBProductionSuggestionFilter.Filter(_modelSuggestions, input, SuggestionDisplayLimit);
            _updatingModelSuggestions = true;
            ModelFilter.ItemsSource = matches;
            SetComboBoxText(ModelFilter, _modelEditor, input);
            _updatingModelSuggestions = false;
            if (openDropDown && _modelEditor?.IsKeyboardFocusWithin == true)
                ModelFilter.IsDropDownOpen = matches.Count > 0;
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
            SetComboBoxText(SnFilter, _snEditor, selected);
            SnFilter.IsDropDownOpen = false;
            _updatingSnSuggestions = false;
        }

        private void UpdateSnSuggestions(string? text, bool openDropDown)
        {
            string input = text ?? string.Empty;
            IReadOnlyList<string> matches = KBProductionSuggestionFilter.Filter(_snSuggestions, input, SuggestionDisplayLimit);
            _updatingSnSuggestions = true;
            SnFilter.ItemsSource = matches;
            SetComboBoxText(SnFilter, _snEditor, input);
            _updatingSnSuggestions = false;
            if (openDropDown && _snEditor?.IsKeyboardFocusWithin == true)
                SnFilter.IsDropDownOpen = matches.Count > 0;
        }

        private static void SetComboBoxText(ComboBox comboBox, TextBox? editor, string text)
        {
            comboBox.Text = text;
            if (editor == null)
                return;

            editor.Text = text;
            editor.CaretIndex = text.Length;
        }

        private static string NormalizeModel(string? model)
        {
            string value = model?.Trim() ?? string.Empty;
            return string.Equals(value, AllModelsText, StringComparison.Ordinal) ? string.Empty : value;
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
            string chineseFont = ScottPlot.Fonts.Detect("逐条 CT 与累计产量");
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
            ConfigureHomeTrendPresentation(KBProductionPeriodMode.Day);
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

        private void ConfigureHomeTrendPresentation(KBProductionPeriodMode mode)
        {
            if (mode == KBProductionPeriodMode.All)
            {
                HomeTrendPlot.Plot.Title("月产量与平均 CT");
                HomeTrendPlot.Plot.YLabel("产量");
                HomeTrendPlot.Plot.Axes.Right.Label.Text = "平均 CT（秒）";
            }
            else
            {
                HomeTrendPlot.Plot.Title("逐条 CT 与累计产量");
                HomeTrendPlot.Plot.YLabel("CT（秒）");
                HomeTrendPlot.Plot.Axes.Right.Label.Text = "累计产量";
            }
        }

        private void RenderHomeTrend(
            IReadOnlyList<KBProductionTrendPoint> points,
            KBProductionPeriodMode mode,
            DateTime from,
            DateTime toExclusive)
        {
            HomeTrendPlot.Plot.Clear();
            ConfigureHomeTrendPresentation(mode);
            bool hasData = points.Any(item => item.ProductionCount > 0);
            HomeTrendEmptyText.Visibility = hasData ? Visibility.Collapsed : Visibility.Visible;
            if (!hasData)
            {
                HomeTrendPlot.Refresh();
                return;
            }

            if (mode == KBProductionPeriodMode.All)
                RenderHomeMonthlyTrend(points);
            else
                RenderHomeDetailTrend(points, from, toExclusive);
            HomeTrendPlot.Plot.ShowLegend(ScottPlot.Alignment.UpperRight);
            HomeTrendPlot.Refresh();
        }

        private void RenderHomeMonthlyTrend(IReadOnlyList<KBProductionTrendPoint> points)
        {
            var bars = points.Select((item, index) => new ScottPlot.Bar
            {
                Position = index,
                Value = item.ProductionCount,
                Size = 0.68,
                FillColor = ScottPlot.Color.FromHex("#4D8DFF"),
            }).ToArray();
            ScottPlot.Plottables.BarPlot productionPlot = HomeTrendPlot.Plot.Add.Bars(bars);
            productionPlot.LegendText = "产量";

            double[] positions = Enumerable.Range(0, points.Count).Select(index => (double)index).ToArray();
            double[] averageCtSeconds = points
                .Select(item => item.ProductionCount > 0 ? item.AverageCtMilliseconds / 1000d : double.NaN)
                .ToArray();
            ScottPlot.Plottables.Scatter ctPlot = HomeTrendPlot.Plot.Add.Scatter(positions, averageCtSeconds);
            ctPlot.Axes.YAxis = HomeTrendPlot.Plot.Axes.Right;
            ctPlot.LegendText = "平均 CT";
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
            HomeTrendPlot.Plot.Axes.Left.Max = Math.Max(1, points.Max(item => item.ProductionCount) * 1.15);
            HomeTrendPlot.Plot.Axes.Right.Min = 0;
            HomeTrendPlot.Plot.Axes.Right.Max = Math.Max(1, averageCtSeconds.Where(double.IsFinite).DefaultIfEmpty(0).Max() * 1.15);
        }

        private void RenderHomeDetailTrend(
            IReadOnlyList<KBProductionTrendPoint> points,
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
            ctPlot.LegendText = "逐条 CT";
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

        private static void ReplaceRows<T>(ObservableCollection<T> target, IEnumerable<T> source)
        {
            target.Clear();
            foreach (T item in source)
                target.Add(item);
        }

        protected override void OnClosed(EventArgs e)
        {
            ++_loadVersion;
            ++_suggestionLoadVersion;
            if (_modelEditor != null)
                _modelEditor.TextChanged -= ModelEditor_TextChanged;
            if (_snEditor != null)
                _snEditor.TextChanged -= SnEditor_TextChanged;
            SaveState();
            base.OnClosed(e);
        }
    }
}
