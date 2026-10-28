using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Themes.Controls;
using ColorVision.UI.Sorts;
using ScottPlot;
using ScottPlot.Plottables;
using SqlSugar;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace ColorVision.Engine.Services.Devices.SMU.Views
{
    public partial class ViewSMU : UserControl, IDisposable
    {
        public ObservableCollection<ViewResultSMU> ViewResults { get; set; } = new();
        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new();
        private bool _initializationStarted;
        private bool _isInitialized;
        private bool _isDisposed;
        private bool _updatingPoints;
        private ViewResultSMU? _activeResult;
        private ViewResultSMU? _latestResult;
        private Marker? _pointMarker;

        public ViewSMU() : this(deferInitialization: false) { }

        internal ViewSMU(bool deferInitialization)
        {
            Loaded += View_Loaded;
            IsVisibleChanged += View_IsVisibleChanged;
            if (!deferInitialization) EnsureInitialized();
        }

        private void View_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsVisible) EnsureInitialized();
        }

        private void View_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsLoaded && IsVisible) EnsureInitialized();
        }

        internal void EnsureInitialized()
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(EnsureInitialized); return; }
            if (_initializationStarted || _isDisposed) return;
            _initializationStarted = true;
            Loaded -= View_Loaded;
            IsVisibleChanged -= View_IsVisibleChanged;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            // Bind before XAML can inherit the dock host's configuration.
            DataContext = ViewSMUConfig.Instance;
            InitializeComponent();
            // A deferred dock shell may already have raised Initialized.
            UserControl_Initialized(this, EventArgs.Empty);
            log4net.LogManager.GetLogger(typeof(ViewSMU)).Info($"Device view initialized. View={nameof(ViewSMU)}, Duration={stopwatch.ElapsedMilliseconds}ms.");
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (_isInitialized) return;
            _isInitialized = true;
            listView1.ItemsSource = ViewResults;
            ViewResults.CollectionChanged += ViewResults_CollectionChanged;
            if (listView1.View is GridView gridView)
            {
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
                ViewSMUConfig.Instance.GridViewColumnVisibilitys.CopyToGridView(GridViewColumnVisibilitys);
                ViewSMUConfig.Instance.GridViewColumnVisibilitys = GridViewColumnVisibilitys;
                GridViewColumnVisibility.AdjustGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
            }
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (_, _) => Delete(), (_, e) => e.CanExecute = listView1.SelectedItems.Count > 0));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, (_, _) => listView1.SelectAll(), (_, e) => e.CanExecute = true));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, ListViewUtils.Copy, (_, e) => e.CanExecute = listView1.SelectedItems.Count > 0));
            PresentResult(null);
        }

        public void AddViewResultSMU(ViewResultSMU result)
        {
            if (_isDisposed) return;
            EnsureInitialized();
            _latestResult = result;
            if (ViewSMUConfig.Instance.InsertAtBeginning)
                ViewResults.Insert(0, result);
            else
                ViewResults.Add(result);
            if (FollowLatest.IsChecked == true || listView1.SelectedItem == null)
            {
                listView1.SelectedItem = result;
                listView1.ScrollIntoView(result);
            }
        }

        private void ViewResults_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_isDisposed) return;
            RecordCount.Text = EngineLocalization.Format($"{ViewResults.Count} 条");
            EmptyState.Visibility = ViewResults.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (_activeResult != null && !ViewResults.Contains(_activeResult)) PresentResult(null);
        }

        public void Dispose()
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(Dispose); return; }
            if (_isDisposed) return;
            _isDisposed = true;
            Loaded -= View_Loaded;
            IsVisibleChanged -= View_IsVisibleChanged;
            ViewResults.CollectionChanged -= ViewResults_CollectionChanged;
            if (ReferenceEquals(ViewSMUConfig.Instance.GridViewColumnVisibilitys, GridViewColumnVisibilitys))
                ViewSMUConfig.Instance.GridViewColumnVisibilitys = new ObservableCollection<GridViewColumnVisibility>(GridViewColumnVisibilitys.Select(column =>
                    new GridViewColumnVisibility { ColumnName = column.ColumnName, IsVisible = column.IsVisible, IsSortD = column.IsSortD }));
            if (listView1 != null)
            {
                listView1.SelectionChanged -= Result_SelectionChanged;
                listView1.PreviewKeyDown -= Result_PreviewKeyDown;
                BindingOperations.ClearAllBindings(listView1);
                listView1.ItemsSource = null;
                listView1.ContextMenu = null;
                listView1.CommandBindings.Clear();
            }
            if (listView2 != null)
            {
                listView2.SelectionChanged -= Point_SelectionChanged;
                listView2.ItemsSource = null;
            }
            ViewResults.Clear();
            _activeResult = null;
            _latestResult = null;
            _pointMarker = null;
            if (_isInitialized)
            {
                MetricBand.DataContext = null;
                wpfplot1.Plot.Clear();
            }
            DataContext = null;
            Content = null;
            GC.SuppressFinalize(this);
        }

        private void Result_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            var result = e.AddedItems.OfType<ViewResultSMU>().LastOrDefault() ?? listView1.SelectedItem as ViewResultSMU;
            if (result != null && !ReferenceEquals(result, _latestResult)) FollowLatest.IsChecked = false;
            PresentResult(result);
        }

        private void PresentResult(ViewResultSMU? result)
        {
            _activeResult = result;
            bool scan = result?.HasCurve == true;
            ChartTitle.Text = result?.ResultKind == "脉冲" ? EngineLocalization.Get("脉冲采样点 · V–I") : EngineLocalization.Get("I–V 特性");
            PlotNote.Text = result == null ? string.Empty : $"{result.SourceDescription} · {result.ProtectionDisplay} · " +
                (result.ResultKind == "脉冲" ? EngineLocalization.Get("仅显示脉冲采样点，不代表完整时域波形。") : EngineLocalization.Get("保留采集顺序；选择右侧数据点可定位读数。"));
            ScanPanel.Visibility = scan ? Visibility.Visible : Visibility.Collapsed;
            PointInfo.Visibility = scan ? Visibility.Collapsed : Visibility.Visible;
            HistorySplitter.Visibility = scan && ViewSMUConfig.Instance.IsShowListView ? Visibility.Visible : Visibility.Collapsed;
            DetailRow.Height = scan ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
            _updatingPoints = true;
            listView2.ItemsSource = result?.SMUDatas;
            listView2.SelectedIndex = result?.SMUDatas.Count > 0 ? result.SMUDatas.Count - 1 : -1;
            if (listView2.SelectedItem != null) listView2.ScrollIntoView(listView2.SelectedItem);
            _updatingPoints = false;
            string time = result?.CreateTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? EngineLocalization.Get("时间未记录");
            SelectionCaption.Text = result == null ? EngineLocalization.Get("选择一条记录查看测量详情") :
                EngineLocalization.Format($"{result.ResultKindDisplay} · 通道 {result.ChannelType} · {result.SourceModeDisplay} · {time} · 用时 {result.TotalTime?.ToString() ?? "—"} ms");
            PointExplanation.Text = result == null ? EngineLocalization.Get("单点测量直接显示读数；扫描结果显示 I–V 特性。") :
                result.ResultKind == "脉冲" ? EngineLocalization.Get("当前显示脉冲采样读数；不代表示波器采集的完整时域波形。") :
                result.Type == "Scan" ? EngineLocalization.Get("本次扫描少于两个点，直接显示读数。") : EngineLocalization.Get("单点测量 · 上方为本次实测值，下方可查看和比较历史读数。");
            SettingsCaption.Text = result == null ? string.Empty : EngineLocalization.Format($"源设置：{result.SourceDescription}   ·   {result.ProtectionDisplay}");
            UpdateReadings();
            DrawPlot();
        }

        private void UpdateReadings()
        {
            MetricBand.DataContext = listView2.SelectedItem as SMUData;
            PointPosition.Text = _activeResult == null ? string.Empty : EngineLocalization.Format($"{listView2.SelectedIndex + 1} / {_activeResult.PointCount} 点");
        }

        private void Point_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || _updatingPoints) return;
            if (listView2.SelectedItem != null) listView2.ScrollIntoView(listView2.SelectedItem);
            UpdateReadings();
            RefreshPointMarker();
        }

        private void DrawPlot()
        {
            wpfplot1.Plot.Clear();
            _pointMarker = null;
            if (_activeResult?.HasCurve != true) { wpfplot1.Refresh(); return; }
            bool currentAxis = AxisChoice.SelectedIndex == 1;
            var results = CompareScans.IsChecked == true ? listView1.SelectedItems.OfType<ViewResultSMU>().Where(item => item.HasCurve).ToArray() : [_activeResult];
            Color[] colors = [Color.FromHex("#2878C8"), Color.FromHex("#C77B22"), Color.FromHex("#27856B"), Color.FromHex("#8D65B5")];
            for (int index = 0; index < results.Length; index++)
            {
                ViewResultSMU result = results[index];
                double[] xs = result.SMUDatas.Select(point => currentAxis ? point.Current : point.Voltage).ToArray();
                double[] ys = result.SMUDatas.Select(point => currentAxis ? point.Voltage : point.Current).ToArray();
                var curve = wpfplot1.Plot.Add.Scatter(xs, ys);
                curve.Color = colors[index % colors.Length];
                curve.LineWidth = ReferenceEquals(result, _activeResult) ? 2 : 1;
                curve.MarkerSize = result.PointCount <= 100 ? 4 : 0;
                curve.LegendText = $"{result.CreateTime:HH:mm:ss} · {result.ChannelType} · {result.SourceMode}";
            }
            wpfplot1.Plot.Legend.IsVisible = results.Length > 1;
            wpfplot1.Plot.XLabel(currentAxis ? EngineLocalization.Get("电流 (mA)") : EngineLocalization.Get("电压 (V)"));
            wpfplot1.Plot.YLabel(currentAxis ? EngineLocalization.Get("电压 (V)") : EngineLocalization.Get("电流 (mA)"));
            string font = Fonts.Detect("电压 电流");
            wpfplot1.Plot.Axes.Left.Label.FontName = font;
            wpfplot1.Plot.Axes.Bottom.Label.FontName = font;
            wpfplot1.Plot.Legend.FontName = font;
            wpfplot1.Plot.Axes.AutoScale();
            RefreshPointMarker();
        }

        private void RefreshPointMarker()
        {
            if (_pointMarker != null) wpfplot1.Plot.Remove(_pointMarker);
            _pointMarker = null;
            if (_activeResult?.HasCurve == true && listView2.SelectedItem is SMUData point && double.IsFinite(point.Voltage) && double.IsFinite(point.Current))
            {
                bool currentAxis = AxisChoice.SelectedIndex == 1;
                _pointMarker = wpfplot1.Plot.Add.Marker(currentAxis ? point.Current : point.Voltage, currentAxis ? point.Voltage : point.Current);
                _pointMarker.MarkerSize = 10;
                _pointMarker.Color = Color.FromHex("#E28B24");
                _pointMarker.MarkerShape = MarkerShape.FilledCircle;
            }
            wpfplot1.Refresh();
        }

        private void AxisChoice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitialized) DrawPlot();
        }

        private void CompareScans_Click(object sender, RoutedEventArgs e) => DrawPlot();

        private void FollowLatest_Click(object sender, RoutedEventArgs e)
        {
            if (FollowLatest.IsChecked == true && _latestResult != null)
            {
                listView1.SelectedItem = _latestResult;
                listView1.ScrollIntoView(listView1.SelectedItem);
            }
        }

        private void HistorySplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            ViewSMUConfig.Instance.Height = HistoryRow.ActualHeight;
            // GridSplitter replaces the binding when it writes row sizes.
            MultiBinding binding = new() { Converter = (IMultiValueConverter)Resources["HistoryHeight"] };
            binding.Bindings.Add(new Binding("Visibility") { Source = ScanPanel });
            binding.Bindings.Add(new Binding("Height") { Source = DataContext });
            binding.Bindings.Add(new Binding("Visibility") { Source = HistoryPane });
            HistoryRow.SetBinding(RowDefinition.HeightProperty, binding);
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu menu && listView1.View is GridView grid && menu.Items.Count == 0)
                GridViewColumnVisibility.GenContentMenuGridViewColumn(menu, grid.Columns, GridViewColumnVisibilitys);
        }

        private void Delete()
        {
            var selected = listView1.SelectedItems.Cast<ViewResultSMU>().ToArray();
            foreach (var result in selected) ViewResults.Remove(result);
            if (_latestResult != null && !ViewResults.Contains(_latestResult))
                _latestResult = ViewResults.MaxBy(result => result.CreateTime);
            if (listView1.SelectedItem == null) listView1.SelectedItem = _latestResult;
        }

        private void Delete_Click(object sender, RoutedEventArgs e) => Delete();
        private void Clear_Click(object sender, RoutedEventArgs e) => ClearResults();

        internal void ClearResults()
        {
            EnsureInitialized();
            _latestResult = null;
            ViewResults.Clear();
            PresentResult(null);
        }

        private void Result_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete) { Delete(); e.Handled = true; }
        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {
            var selected = listView1.SelectedItems.Cast<ViewResultSMU>().ToArray();
            var active = _activeResult;
            e.Handled = ViewResults.SortByGridViewColumn<ViewResultSMU>(sender, GridViewColumnVisibilitys, Properties.Resources.ResourceManager);
            foreach (var result in selected) if (!listView1.SelectedItems.Contains(result)) listView1.SelectedItems.Add(result);
            PresentResult(active);
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var selected = listView1.SelectedItems.Cast<ViewResultSMU>().ToArray();
            if (selected.Length == 0) { MessageBox1.Show(Properties.Resources.SelectDataFirst); return; }
            using var dialog = new System.Windows.Forms.SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv", FileName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"), RestoreDirectory = true
            };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            try { ExportToCsv(selected, dialog.FileName); MessageBox1.Show(Properties.Resources.ExportSucceeded); }
            catch (Exception ex) { MessageBox1.Show(EngineLocalization.Format($"导出失败：{ex.Message}")); }
        }

        internal static void ExportToCsv(ViewResultSMU[] results, string fileName)
        {
            using var writer = new StreamWriter(fileName, false, new UTF8Encoding(true));
            writer.WriteLine("记录ID,时间,批次ID,类型,通道,源模式,源值或起点,扫描终点,保护限值,用时(ms),点序号,电压(V),电流(mA),电阻(Ω),功率(W)");
            foreach (var result in results)
                foreach (var point in result.SMUDatas)
                    writer.WriteLine(string.Join(",", result.Id, result.CreateTime?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                        result.BatchID, result.ResultKind, result.ChannelType, result.SourceMode,
                        result.LimitStart.ToString("G9", CultureInfo.InvariantCulture), result.Type == "Scan" ? result.LimitEnd.ToString("G9", CultureInfo.InvariantCulture) : string.Empty,
                        result.ProtectionLimit?.ToString("G9", CultureInfo.InvariantCulture), result.TotalTime, point.Index,
                        point.Voltage.ToString("G17", CultureInfo.InvariantCulture), point.Current.ToString("G17", CultureInfo.InvariantCulture),
                        point.Resistance?.ToString("G17", CultureInfo.InvariantCulture), point.Power.ToString("G17", CultureInfo.InvariantCulture)));
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = DbType.MySql, IsAutoCloseConnection = true });
                var points = db.Queryable<SMUResultModel>().ToList();
                var pointDetails = Local.LocalSmuResultService.ReadDetails(db, false, points.Select(item => item.Id).ToArray());
                var scans = db.Queryable<SmuScanModel>().ToList();
                var scanDetails = Local.LocalSmuResultService.ReadDetails(db, true, scans.Select(item => item.Id).ToArray());
                foreach (var item in points) if (pointDetails.TryGetValue(item.Id, out string? details)) item.LocalDetails = details;
                foreach (var item in scans) if (scanDetails.TryGetValue(item.Id, out string? details)) item.LocalDetails = details;
                var results = points.Select(item => new ViewResultSMU(item)).Concat(scans.Select(item => new ViewResultSMU(item))).OrderBy(item => item.CreateTime).ToArray();
                ClearResults();
                foreach (var result in results) ViewResults.Add(result);
                _latestResult = results.LastOrDefault();
                listView1.SelectedItem = _latestResult;
            }
            catch (Exception ex) { MessageBox1.Show(EngineLocalization.Format($"查询源表历史失败：{ex.Message}")); }
        }
    }
}
