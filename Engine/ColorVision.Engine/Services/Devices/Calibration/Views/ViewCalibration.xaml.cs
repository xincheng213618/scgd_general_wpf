#pragma warning disable CS8604,CS8629,CS8602
#pragma warning disable CS8601
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Results;
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using log4net;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;


namespace ColorVision.Engine.Services.Devices.Calibration.Views
{
    /// <summary>
    /// ViewCamera.xaml 的交互逻辑
    /// </summary>
    public partial class ViewCalibration : UserControl, IDisposable, IActiveDocumentStatusProvider
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewCalibration));
        private bool _isInitialized;
        private bool _isInitializing;
        private volatile bool _isDisposed;
        private readonly IDisposable _resultSubscription;
        private CancellationTokenSource? _historyQueryCancellation;

        public  MQTTCalibration DeviceService => Device.DService;
        public DeviceCalibration Device { get; set; }

        public event EventHandler? StatusBarItemsChanged;

        public IEnumerable<StatusBarMeta> GetActiveStatusBarItems() =>
            !_isDisposed && ImageView != null ? ImageView.GetActiveStatusBarItems() : [];

        private void ImageView_StatusBarItemsChanged(object? sender, EventArgs e) => StatusBarItemsChanged?.Invoke(this, e);

        public ViewCalibration(DeviceCalibration device) : this(device, deferInitialization: false)
        {
        }

        internal ViewCalibration(DeviceCalibration device, bool deferInitialization)
        {
            Device = device;
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
                Loaded -= View_Loaded;
                IsVisibleChanged -= View_IsVisibleChanged;
                stopwatch.Stop();
                log.Info($"Device view initialized. View={nameof(ViewCalibration)}, Duration={stopwatch.ElapsedMilliseconds}ms.");
            }
            finally
            {
                _isInitializing = false;
            }
        }
        public static ViewCalibrationConfig Config => ViewCalibrationConfig.Instance;
        public ObservableCollection<ViewResultImage> ViewResults { get; } = new ObservableCollection<ViewResultImage>();

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (_isInitialized || _isDisposed)
                return;

            _isInitialized = true;
            ImageView.StatusBarItemsChanged += ImageView_StatusBarItemsChanged;
            listView1.ItemsSource = ViewResults;

            if (listView1.View is GridView gridView)
            {
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
                Config.GridViewColumnVisibilitys.CopyToGridView(GridViewColumnVisibilitys);
                Config.GridViewColumnVisibilitys = GridViewColumnVisibilitys;
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
            }
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => Delete(), (s, e) => e.CanExecute = listView1.SelectedIndex > -1));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, (s, e) => listView1.SelectAll(), (s, e) => e.CanExecute = true));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, ListViewUtils.Copy, (s, e) => e.CanExecute = true));
        }

        private void Delete()
        {
            CancelHistoryQuery();
            if (listView1.SelectedItems.Count == listView1.Items.Count)
                ViewResults.Clear();
            else
            {
                listView1.SelectedIndex = -1;
                foreach (var item in listView1.SelectedItems.Cast<ViewResultImage>().ToList())
                    ViewResults.Remove(item);
            }
        }

        private void ClearList_Click(object sender, RoutedEventArgs e)
        {
            CancelHistoryQuery();
            ViewResults.Clear();
        }
        internal async Task ProcessResultAsync(ResultMessage message)
        {
            int masterId = message.Data.MasterId;
            if (message.Route != ResultRoutes.Calibration
                || message.ResultKind != ResultKinds.Image
                || message.DatabaseEndpoint is not { } endpoint
                || masterId <= 0)
                return;

            DeviceCalibration owner = Device;
            if (message.SourceDevice != null && !ReferenceEquals(message.SourceDevice, owner)) return;
            MQTTCalibration service = owner.DService;
            if (!CanApply()) return;
            try
            {
                MeasureResultImgModel? model = await Task.Run(() => CanApply() ? MeasureImgResultDao.ReadResult(endpoint, masterId) : null).ConfigureAwait(false);
                if (model == null) return;
                await Dispatcher.InvokeAsync(() =>
                {
                    if (CanApply() && MatchesDevice(model.DeviceCode))
                        ShowResult(model);
                }).Task.ConfigureAwait(false);
            }
            catch (Exception error)
            {
                if (CanApply()) log.Warn($"无法显示校正结果。MasterId={masterId}, Error={error.GetType().Name}");
            }

            bool CanApply() => !_isDisposed && !owner.IsDisposed && ReferenceEquals(Device, owner) && ReferenceEquals(owner.DService, service)
                && owner.ViewRegistration.IsVisible && string.Equals(owner.Config.Code, message.DeviceCode, StringComparison.Ordinal)
                && ReferenceEquals(owner.ResultDatabaseEndpoint, endpoint) && endpoint.IsCurrent;
            // Older rows may omit device_code. Local flow rows can carry the linked camera code.
            bool MatchesDevice(string? storedCode) => string.IsNullOrEmpty(storedCode)
                || string.Equals(storedCode, message.DeviceCode, StringComparison.Ordinal)
                || (!string.IsNullOrWhiteSpace(owner.Config.CameraCode)
                    && string.Equals(storedCode, owner.PhyCamera?.DeviceCamera?.Code, StringComparison.Ordinal));
        }


        public void ShowResult(MeasureResultImgModel model)
        {
            if (_isDisposed || !Device.ViewRegistration.IsVisible)
                return;

            EnsureInitialized();
            ViewResultImage result = new(model);
            if (Config.InsertAtBeginning)
                ViewResults.Insert(0, result);
            else
                ViewResults.Add(result);


            if (Config.AutoRefreshView)
            {
                if (listView1.Items.Count > 0) listView1.SelectedIndex = Config.InsertAtBeginning ? 0 : listView1.Items.Count - 1;
                listView1.ScrollIntoView(listView1.SelectedItem);
            }
        }

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0&& listView1.View is GridView gridView)
                GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, GridViewColumnVisibilitys);
        }

        private void Button_Click_ShowResultGrid(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton button)
            {
                Visibility visibility = button.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                listView1.Visibility = visibility;
            }
        }


        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isDisposed)
                return;

            if (listView1.SelectedItem is not ViewResultImage data)
            {
                ImageView.Clear();
                return;
            }

            if (string.IsNullOrWhiteSpace(data.FileUrl))
                ImageView.Clear();
            else
                ImageView.OpenImage(data.FileUrl);
        }

        private void listView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && listView1.SelectedIndex > -1)
            {
                CancelHistoryQuery();
                int temp = listView1.SelectedIndex;
                ViewResults.RemoveAt(temp);
            }
        }

        public void OpenImage(CVCIEFile fileData)
        {
            if (_isDisposed)
                return;

            EnsureInitialized();
            ImageView.Clear();
            ImageView.OpenImage(fileData.ToWriteableBitmap());
        }


        private async void SearchAdvanced_Click(object sender, RoutedEventArgs e)
        {
            try { await LoadRecentHistoryAsync(); }
            catch (Exception error) { ReportHistoryFailure(error); }
        }

        internal async Task LoadRecentHistoryAsync()
        {
            var target = CaptureHistoryTarget();
            var order = Config.OrderByType;
            int count = Config.Count;
            CancellationToken cancellation = BeginHistoryQuery();
            bool CanApply() => !cancellation.IsCancellationRequested && OwnsHistoryTarget(target)
                && Config.OrderByType == order && Config.Count == count;

            try
            {
                var results = await Task.Run(() =>
                {
                    cancellation.ThrowIfCancellationRequested();
                    RequireHistoryTarget(target);
                    using var db = target.Endpoint.CreateDbClient();
                    RequireHistoryTarget(target);
                    var query = CalibrationResultHistory.Query(db, target.DeviceCode).OrderBy(row => row.Id, order);
                    var rows = count > 0 ? query.Take(count).ToList() : query.ToList();
                    return MaterializeHistoryRows(rows, target, cancellation).ToArray();
                }, cancellation);

                if (!CanApply()) return;
                ViewResults.Clear();
                foreach (var result in results) ViewResults.Add(result);
            }
            catch (Exception) when (!CanApply()) { }
            finally { FinishHistoryQuery(cancellation); }
        }


        private void Search1_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var query = CreateHistoryQuery(out var finishQuery);
                using var db = query.Db;
                try
                {
                    var window = new GenericQueryWindow(query)
                    {
                        Owner = Application.Current.GetActiveWindow(),
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    };
                    window.ShowDialog();
                }
                finally { finishQuery(); }
            }
            catch (Exception error) { ReportHistoryFailure(error); }
        }

        internal GenericQuery<MeasureResultImgModel, ViewResultImage> CreateHistoryQuery(out Action finishQuery)
        {
            var target = CaptureHistoryTarget();
            var db = target.Endpoint.CreateDbClient();
            CancellationToken cancellation = default;
            finishQuery = () => FinishHistoryQuery(cancellation);
            try
            {
                var query = new GenericQuery<MeasureResultImgModel, ViewResultImage>(db, ViewResults, row => CreateHistoryResult(row, target.Endpoint))
                {
                    IsReadOnly = true,
                    CreateBaseQuery = client =>
                    {
                        RequireHistoryTarget(target);
                        return CalibrationResultHistory.Query(client, target.DeviceCode);
                    },
                    BackgroundQueryClientFactory = () =>
                    {
                        RequireHistoryTarget(target);
                        return target.Endpoint.CreateDbClient();
                    },
                };
                query.PreQuery += (_, _) =>
                {
                    RequireHistoryTarget(target);
                    cancellation = BeginHistoryQuery();
                    CancellationToken requestCancellation = cancellation;
                    query.BatchConverter = rows => MaterializeHistoryRows(rows, target, requestCancellation);
                    query.CanApplyBackgroundResults = () => !requestCancellation.IsCancellationRequested && OwnsHistoryTarget(target);
                };
                return query;
            }
            catch
            {
                db.Dispose();
                throw;
            }
        }

        private HistoryTarget CaptureHistoryTarget()
        {
            Dispatcher.VerifyAccess();
            DeviceCalibration owner = Device;
            ObjectDisposedException.ThrowIf(_isDisposed || owner.IsDisposed, this);
            if (owner.ResultDatabaseEndpoint is not { IsCurrent: true } endpoint)
                throw new InvalidOperationException(EngineLocalization.Get("数据库未连接，无法查询校准结果。"));
            var target = new HistoryTarget(owner, endpoint, owner.Code, owner.Config, owner.DService,
                owner.CaptureConfigurationTarget(), ReferenceEquals(owner.ViewRegistration.Current, this));
            RequireHistoryTarget(target);
            return target;
        }

        private bool OwnsHistoryTarget(HistoryTarget target) => !_isDisposed && !target.Owner.IsDisposed && !target.Service.IsDisposed
            && ReferenceEquals(Device, target.Owner) && ReferenceEquals(target.Owner.Config, target.Config)
            && ReferenceEquals(target.Owner.DService, target.Service) && target.ConfigurationTarget.IsCurrent(target.Owner)
            && !string.IsNullOrWhiteSpace(target.DeviceCode) && string.Equals(target.Owner.Code, target.DeviceCode, StringComparison.Ordinal)
            && ReferenceEquals(target.Owner.ResultDatabaseEndpoint, target.Endpoint) && target.Endpoint.IsCurrent
            && target.Owner.ViewRegistration.IsVisible && (!target.Docked || ReferenceEquals(target.Owner.ViewRegistration.Current, this));

        private void RequireHistoryTarget(HistoryTarget target)
        {
            if (!OwnsHistoryTarget(target))
                throw new InvalidOperationException(EngineLocalization.Get("校准设备或数据库已变更，无法查询历史结果。"));
        }

        private IEnumerable<ViewResultImage> MaterializeHistoryRows(IEnumerable<MeasureResultImgModel> rows, HistoryTarget target, CancellationToken cancellation)
        {
            foreach (var row in rows)
            {
                cancellation.ThrowIfCancellationRequested();
                RequireHistoryTarget(target);
                yield return CreateHistoryResult(row, target.Endpoint);
            }
        }

        private static ViewResultImage CreateHistoryResult(MeasureResultImgModel row, MySqlConnectedEndpoint endpoint) =>
            new(row) { DatabaseEndpoint = endpoint };

        private CancellationToken BeginHistoryQuery()
        {
            CancelHistoryQuery();
            _historyQueryCancellation = new();
            return _historyQueryCancellation.Token;
        }

        private void FinishHistoryQuery(CancellationToken cancellation)
        {
            if (_historyQueryCancellation?.Token == cancellation) CancelHistoryQuery();
        }

        private void CancelHistoryQuery()
        {
            var previous = _historyQueryCancellation;
            _historyQueryCancellation = null;
            if (previous == null) return;
            previous.Cancel();
            previous.Dispose();
        }

        private void ReportHistoryFailure(Exception error)
        {
            if (_isDisposed) return;
            log.Warn("Calibration history query failed; existing results retained.", error);
            MessageBox1.Show(Application.Current.GetActiveWindow(), EngineLocalization.Get("校准历史查询失败，当前列表已保留。详情请查看日志。"), "ColorVision");
        }

        private sealed record HistoryTarget(DeviceCalibration Owner, MySqlConnectedEndpoint Endpoint, string DeviceCode,
            ConfigCalibration Config, MQTTCalibration Service, DeviceConfigurationTarget ConfigurationTarget, bool Docked);

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {
            e.Handled = ViewResults.SortByGridViewColumn<ViewResultImage>(sender, GridViewColumnVisibilitys, Properties.Resources.ResourceManager);
        }

        private void GridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            listView1.Height = MainGridRow2.ActualHeight - 32;
            MainGridRow1.Height = new GridLength(1, GridUnitType.Star);
            MainGridRow2.Height = GridLength.Auto;
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            CancelHistoryQuery();
            Loaded -= View_Loaded;
            IsVisibleChanged -= View_IsVisibleChanged;

            _resultSubscription.Dispose();

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
            if (ImageView != null)
            {
                ImageView.StatusBarItemsChanged -= ImageView_StatusBarItemsChanged;
                ImageView.Dispose();
            }
            DataContext = null;
            GC.SuppressFinalize(this);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex < 0)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), "您需要先选择数据", "ColorVision");
                return;
            }
            using var dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.Filter = "CSV files (*.csv) | *.csv";
            dialog.FileName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            dialog.RestoreDirectory = true;
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            CsvWriter.WriteToCsv(ViewResults[listView1.SelectedIndex], dialog.FileName);
            ImageSource bitmapSource = ImageView.ImageShow.Source;
            ImageUtils.SaveImageSourceToFile(bitmapSource, Path.Combine(Path.GetDirectoryName(dialog.FileName), Path.GetFileNameWithoutExtension(dialog.FileName) + ".png"));
        }
    }
}
