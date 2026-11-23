using LocalizedText = global::ColorVision.Engine.EngineLocalization;
#pragma warning disable CA1822,CS8601,CS8604,CS8622,CS8625
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Services.Results;
using ColorVision.ImageEditor;
using ColorVision.UI.Sorts;
using log4net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Engine.Services.Devices.Algorithm.Views
{

    /// <summary>
    /// ViewSpectrum.xaml 的交互逻辑
    /// </summary>
    public partial class AlgorithmView : UserControl,IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(AlgorithmView));
        private bool _isInitialized;
        private bool _isInitializing;
        private volatile bool _isDisposed;
        private readonly AlgorithmResultPresenter _resultPresenter = new(clearImageOnSelection: false);
        private readonly IDisposable? _resultSubscription;
        private readonly ResultImagePlaceholderCache _resultImagePlaceholderCache = new();
        private readonly ViewAlgorithmConfig _historyConfig;
        private readonly ResultHistoryCollection<ViewResultAlg> _viewResults;

        private DeviceAlgorithm? Device { get; }

        public ImageView ImageView { get; set; }

        public ListView ListView { get; set; }

        public TextBox SideTextBox { get; set; }

        public AlgorithmView() : this(null)
        {
        }

        public AlgorithmView(DeviceAlgorithm? device) : this(device, deferInitialization: false)
        {
        }

        internal AlgorithmView(DeviceAlgorithm? device, bool deferInitialization)
        {
            _historyConfig = Config;
            _viewResults = new ResultHistoryCollection<ViewResultAlg>(result => result.Id, _historyConfig.MaxHistoryCount);
            _historyConfig.PropertyChanged += HistoryConfig_PropertyChanged;
            Device = device;
            if (Device != null)
                _resultSubscription = ResultMessageBus.Default.Subscribe(message => _ = ProcessResultAsync(message));

            if (deferInitialization)
            {
                Loaded += View_Loaded;
                IsVisibleChanged += View_IsVisibleChanged;
            }
            else
                EnsureInitialized();
        }

        private void View_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsVisible)
                EnsureInitialized();
        }

        private void View_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsLoaded && IsVisible)
                EnsureInitialized();
        }

        internal void EnsureInitialized()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(EnsureInitialized);
                return;
            }

            if (_isInitialized || _isInitializing || _isDisposed)
                return;

            _isInitializing = true;
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                // A deferred dock shell must own its context before loading child bindings.
                DataContext = Config;
                InitializeComponent();
                // A deferred shell may already have raised WPF Initialized before its XAML was loaded.
                UserControl_Initialized(this, EventArgs.Empty);
                CommandBindings.Add(new CommandBinding(AlgorithmResultDataSaver.SaveCommand, SaveSideDataCommand_Executed, SaveSideDataCommand_CanExecute));
                Loaded -= View_Loaded;
                IsVisibleChanged -= View_IsVisibleChanged;
                stopwatch.Stop();
                log.Info($"Device view initialized. View={nameof(AlgorithmView)}, Duration={stopwatch.ElapsedMilliseconds}ms.");
            }
            finally
            {
                _isInitializing = false;
            }
        }
        public ViewAlgorithmConfig Config => ViewAlgorithmConfig.Instance;


        public ViewResultContext ViewResultContext { get; set; }
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (_isInitialized || _isDisposed)
                return;

            _isInitialized = true;
            ImageView = new ImageView();
            ListView = listViewSide;
            SideTextBox = TextBoxside;
            Grid1.Children.Add(ImageView);
            if (listView1.View is GridView gridView)
            {
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
                Config.GridViewColumnVisibilitys.CopyToGridView(GridViewColumnVisibilitys);
                Config.GridViewColumnVisibilitys = GridViewColumnVisibilitys;
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
            }

            listView1.ItemsSource = ViewResults;
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => Delete(), (s, e) => e.CanExecute = listView1.SelectedIndex > -1));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, (s, e) => listView1.SelectAll(), (s, e) => e.CanExecute = true));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, ListViewUtils.Copy, (s, e) => e.CanExecute = true));

            ViewResultContext = new ViewResultContext();
            ViewResultContext.SideTextBox = SideTextBox;
            ViewResultContext.ImageView = ImageView;
            ViewResultContext.LeftGridViewColumnVisibilitys = LeftGridViewColumnVisibilitys;
            ViewResultContext.ListView = ListView;

        }

        private void Delete()
        {
            if (listView1.SelectedItems.Count == listView1.Items.Count)
                ViewResults.Clear();
            else
            {
                listView1.SelectedIndex = -1;
                foreach (var item in listView1.SelectedItems.Cast<ViewResultAlg>().ToList())
                    ViewResults.Remove(item);
            }
        }



        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0 && listView1.View is GridView gridView)
            {
                GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, GridViewColumnVisibilitys);
            }
        }

        public ObservableCollection<ViewResultAlg> ViewResults => _viewResults;

        private void HistoryConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isDisposed || e.PropertyName != nameof(ResultViewConfig.MaxHistoryCount)) return;
            void ApplyLimit()
            {
                if (!_isDisposed) _viewResults.MaxCount = _historyConfig.MaxHistoryCount;
            }
            if (Dispatcher.CheckAccess()) ApplyLimit();
            else Dispatcher.BeginInvoke(ApplyLimit);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex < 0) return;

  
            if (listView1.SelectedIndex < 0 ||listView1.Items[listView1.SelectedIndex] is not ViewResultAlg result)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.SelectDataFirst, "ColorVision");
                return;
            }
            else
            {
                using var dialog = new System.Windows.Forms.SaveFileDialog();
                dialog.Filter = "CSV files (*.csv) | *.csv";
                dialog.FileName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
                dialog.RestoreDirectory = true;
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                using StreamWriter file = new(dialog.FileName, true, Encoding.UTF8);
                WriteHistoryCsv(file, ViewResults);
                ImageSource bitmapSource = ImageView.ImageShow.Source;
                ImageUtils.SaveImageSourceToFile(bitmapSource, Path.Combine( Path.GetDirectoryName(dialog.FileName),Path.GetFileNameWithoutExtension(dialog.FileName) + ".png"));
            }
        }

        internal static void WriteHistoryCsv(TextWriter writer, IEnumerable<ViewResultAlg> results)
        {
            (string Header, Func<ViewResultAlg, string?> Value)[] columns =
            [
                (Properties.Resources.SerialNumber1, static result => result.Id.ToString(CultureInfo.InvariantCulture)),
                (Properties.Resources.CreateTime, static result => result.CreateTime?.ToString(CultureInfo.CurrentCulture)),
                ("ResultType", static result => result.ResultType.ToString()),
                ("ResultDesc", static result => result.ResultDesc),
                (Properties.Resources.File, static result => result.FilePath),
                (Properties.Resources.Template, static result => result.POITemplateName),
                ("img_result", static result => result.ResultImagFile),
                (Properties.Resources.Duration, static result => result.TotalTime.ToString(CultureInfo.InvariantCulture)),
                (Properties.Resources.BatchNumber, static result => result.Batch)
            ];

            using var csv = new CsvHelper.CsvWriter(writer, CultureInfo.InvariantCulture, leaveOpen: true);
            foreach (var column in columns)
                csv.WriteField(column.Header);
            csv.NextRecord();
            foreach (var result in results)
            {
                foreach (var column in columns)
                    csv.WriteField(column.Value(result));
                csv.NextRecord();
            }
        }

        public void AddAlgResultMasterModel(AlgResultMasterModel result)
        {
            if (!_isDisposed && Device?.ViewRegistration.IsVisible != false && result != null)
                AddAlgorithmResult(new ViewResultAlg(result));
        }

        private void AddAlgorithmResult(ViewResultAlg viewResult)
        {
            AlgResultMasterModel result = viewResult.AlgResultMasterModel;
            if (!_isDisposed && Device?.ViewRegistration.IsVisible != false)
            {
                if (_viewResults.ContainsId(result.Id))
                {
                    ViewResultAlg existing = ViewResults.First(item => item.Id == result.Id);
                    if (result.DatabaseEndpoint == null || ReferenceEquals(existing.AlgResultMasterModel?.DatabaseEndpoint, result.DatabaseEndpoint))
                        return;
                    ViewResults.Remove(existing);
                }
                EnsureInitialized();

                if (Config.InsertAtBeginning)
                    ViewResults.Insert(0, viewResult);
                else
                    ViewResults.Add(viewResult);

                if (Config.AutoRefreshView && _viewResults.ContainsId(result.Id))
                {
                    listView1.SelectedItem = viewResult;
                    listView1.ScrollIntoView(viewResult);
                }
                if (Config.AutoSaveSideData && result.DatabaseEndpoint?.IsCurrent != false)
                    AlgorithmResultDataSaver.TrySave(ViewResultContext, viewResult, Config.SaveSideDataDirPath);
            }
        }

        internal async Task ProcessResultAsync(ResultMessage message)
        {
            bool isLocalFlow = message.Route == ResultRoutes.LocalFlow;
            if ((!isLocalFlow && message.Route != ResultRoutes.Algorithm)
                || message.ResultKind != ResultKinds.Algorithm
                || message.DatabaseEndpoint is not { } endpoint
                || message.Data.MasterId <= 0
                || Device is not { } device
                || (message.SourceDevice != null && !ReferenceEquals(message.SourceDevice, device)))
                return;

            MQTTAlgorithm service = device.DService;
            if (!CanApply()) return;
            try
            {
                ViewResultAlg? result = await Task.Run(() =>
                {
                    if (!CanApply()) return null;
                    using var db = endpoint.CreateDbClient();
                    var model = db.Queryable<AlgResultMasterModel>().First(result => result.Id == message.Data.MasterId);
                    if (model == null || !CanApply()) return null;
                    model.DatabaseEndpoint = endpoint;
                    return message.SourceDevice == null
                        ? new ViewResultAlg(model, message.SerialNumber)
                        : AlgorithmResultHistory.CreateViews([model], db, endpoint)[0];
                }).ConfigureAwait(false);
                if (result == null)
                {
                    if (CanApply()) log.Debug($"找不到算法主结果。ResultId={message.Data.MasterId}");
                    return;
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    if (CanApply())
                        AddAlgorithmResult(result);
                }).Task.ConfigureAwait(false);
            }
            catch (Exception error)
            {
                if (CanApply()) log.Warn($"算法结果读取失败。ResultId={message.Data.MasterId}", error);
            }

            bool CanApply() => !_isDisposed && !device.IsDisposed && ReferenceEquals(device.DService, service)
                && device.ViewRegistration.IsVisible && endpoint.IsCurrent
                && (isLocalFlow || string.Equals(message.DeviceCode, device.Config.Code, StringComparison.Ordinal))
                && (ReferenceEquals(device.ResultDatabaseEndpoint, endpoint)
                    || (message.SourceDevice == null && device.ResultDatabaseEndpoint == null));
        }

        public void RefreshResultListView()
        {
            if (_isDisposed)
                return;

            EnsureInitialized();
            if (listView1.Items.Count > 0) listView1.SelectedIndex = Config.InsertAtBeginning? 0: listView1.Items.Count - 1;
            listView1.ScrollIntoView(listView1.SelectedItem);
        }


        private async void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isDisposed || !ReferenceEquals(e.Source, listView1) || ViewResultContext == null)
                return;

            await _resultPresenter.ShowAsync(ViewResultContext, listView1.SelectedItem as ViewResultAlg, PrepareResultImageSurface);
        }

        private void PrepareResultImageSurface(ViewResultAlg result)
        {
            ImageView.ImageShow.Clear();

            if (File.Exists(result.FilePath))
            {
                // The result handler may reuse the current bitmap when it opens a compatible CVRAW file.
                return;
            }

            if (!AlgorithmResultImageDimensions.TryRecoverFromMeasureResults(result, out int width, out int height))
            {
                ImageView.Clear();
                log.Warn($"算法结果图像不存在且没有可恢复尺寸，已清除旧底图：resultId={result.Id}, file={result.FilePath}");
                return;
            }

            ShowResultImagePlaceholder(width, height);
        }

        private void ShowResultImagePlaceholder(int width, int height)
        {
            var placeholder = _resultImagePlaceholderCache.GetOrCreate(width, height);
            if (_resultImagePlaceholderCache.IsCurrent(ImageView.ImageShow.Source, width, height))
                return;

            ImageView.Clear();
            ImageView.Config.SetImageMetadata(ImageViewPropertyKeys.Cols, width, nameof(AlgorithmView), LocalizedText.Get("历史算法结果坐标空间宽度"));
            ImageView.Config.SetImageMetadata(ImageViewPropertyKeys.Rows, height, nameof(AlgorithmView), LocalizedText.Get("历史算法结果坐标空间高度"));
            ImageView.Config.SetImageMetadata(ImageViewPropertyKeys.ImageWidth, width, nameof(AlgorithmView), LocalizedText.Get("历史算法结果图像像素宽度"));
            ImageView.Config.SetImageMetadata(ImageViewPropertyKeys.ImageHeight, height, nameof(AlgorithmView), LocalizedText.Get("历史算法结果图像像素高度"));
            ImageView.SetImageSource(placeholder, enableEditorImageServices: false, configureDefaultLayerController: false);
            ImageView.UpdateZoomAndScale();
        }

        private void listView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && listView1.SelectedIndex > -1)
            {
                int temp = listView1.SelectedIndex;
                ViewResults.RemoveAt(temp);
            }
        }


        private void GridSplitter_DragCompleted1(object sender, DragCompletedEventArgs e)
        {
            listViewSide.Width = ListCol2.ActualWidth;
            SideTextBox.Width = ListCol2.ActualWidth;
            ListCol1.Width = new GridLength(1, GridUnitType.Star);
            ListCol2.Width = GridLength.Auto;
        }
        private void GridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (ListRow2.ActualHeight > 32)
            {
                listView1.Height = ListRow2.ActualHeight - 32;
                ListRow2.Height = GridLength.Auto;
                ListRow1.Height = new GridLength(1, GridUnitType.Star);

            }
        }


        private void Button_Delete_Click(object sender, RoutedEventArgs e) => ViewResults.Clear();
        public ObservableCollection<GridViewColumnVisibility> LeftGridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        private void ContextMenu1_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && listViewSide.View is GridView gridView && LeftGridViewColumnVisibilitys.Count ==0)
                GridViewColumnVisibility.GenContentMenuGridViewColumnZero(contextMenu, gridView.Columns, LeftGridViewColumnVisibilitys);
        }
        public void SideSave(ViewResultAlg result,string selectedPath)
        {
            EnsureInitialized();
            AlgorithmResultDataSaver.Save(ViewResultContext, result, selectedPath);
        }

        private void SideSave_Click(object sender, RoutedEventArgs e)
        {
            AlgorithmResultDataSaver.PromptAndSave(ViewResultContext, listView1.SelectedItems.Cast<ViewResultAlg>());
        }

        private void SaveSideDataCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _isInitialized && !_isDisposed && e.Parameter is ViewResultAlg result && AlgorithmResultDataSaver.CanSave(result);
            e.Handled = true;
        }

        private void SaveSideDataCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Parameter is ViewResultAlg result)
                AlgorithmResultDataSaver.PromptAndSave(ViewResultContext, new[] { result });
            e.Handled = true;
        }

        private void AlgorithmResult_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is ListViewItem { DataContext: ViewResultAlg result } item)
            {
                AlgorithmResultDataSaver.EnsureContextMenu(result);
                result.ContextMenu.PlacementTarget = item;
                result.ContextMenu.IsOpen = true;
                e.Handled = true;
            }
        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {
            e.Handled = ViewResults.SortByGridViewColumn<ViewResultAlg>(sender, GridViewColumnVisibilitys, Properties.Resources.ResourceManager);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _resultPresenter.Invalidate();
            _historyConfig.PropertyChanged -= HistoryConfig_PropertyChanged;
            Loaded -= View_Loaded;
            IsVisibleChanged -= View_IsVisibleChanged;

            _resultSubscription?.Dispose();

            if (ReferenceEquals(Config.GridViewColumnVisibilitys, GridViewColumnVisibilitys))
                Config.GridViewColumnVisibilitys = new ObservableCollection<GridViewColumnVisibility>(GridViewColumnVisibilitys.Select(column =>
                    new GridViewColumnVisibility { ColumnName = column.ColumnName, IsVisible = column.IsVisible, IsSortD = column.IsSortD }));
            if (listView1 != null)
            {
                listView1.SelectionChanged -= listView1_SelectionChanged;
                listView1.PreviewKeyDown -= listView1_PreviewKeyDown;
                listView1.ItemsSource = null;
                listView1.CommandBindings.Clear();
            }
            if (listViewSide != null)
                listViewSide.ItemsSource = null;
            ViewResults.Clear();
            if (ImageView != null)
            {
                ImageView.Dispose();
                Grid1?.Children.Remove(ImageView);
            }
            DataContext = null;

            GC.SuppressFinalize(this);
        }

        private void Inquire_Click(object sender, RoutedEventArgs e)
        {
            if (GetHistoryEndpoint() is not { } endpoint) return;
            using var db = endpoint.CreateDbClient();
            var query = db.Queryable<AlgResultMasterModel>();
            query = query.OrderBy(x => x.Id, Config.OrderByType);
            var dbList = Config.Count > 0 ? query.Take(Config.Count).ToList() : query.ToList();
            var results = AlgorithmResultHistory.CreateViews(dbList, db, endpoint);
            ViewResults.Clear();
            _viewResults.QueryResults.AddRange(results);
        }

        private void SearchAdvanced_Click(object sender, RoutedEventArgs e)
        {
            if (GetHistoryEndpoint() is not { } endpoint) return;
            using var db = endpoint.CreateDbClient();
            GenericQuery<AlgResultMasterModel, ViewResultAlg> genericQuery = new(db, _viewResults.QueryResults, t => new ViewResultAlg(t))
            {
                BatchConverter = rows => AlgorithmResultHistory.CreateViews(rows, db, endpoint)
            };
            GenericQueryWindow genericQueryWindow = new(genericQuery) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            genericQueryWindow.ShowDialog();
        }

        private static MySqlConnectedEndpoint? GetHistoryEndpoint()
        {
            var endpoint = MySqlControl.GetInstance().ConnectedEndpoint;
            if (endpoint?.IsCurrent == true) return endpoint;
            MessageBox.Show(LocalizedText.Get("数据库未连接，无法查询算法结果。"), "ColorVision");
            return null;
        }
    }
}
