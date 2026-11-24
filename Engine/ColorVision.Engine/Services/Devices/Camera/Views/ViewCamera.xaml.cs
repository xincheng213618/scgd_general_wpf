#pragma warning disable CA1822
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Database;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates.Jsons.FOV2;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using log4net;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace ColorVision.Engine.Services.Devices.Camera.Views
{

    /// <summary>
    /// ViewCamera.xaml 的交互逻辑
    /// </summary>
    public partial class ViewCamera : UserControl, IDisposable, IActiveDocumentStatusProvider
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(App));
        private int _disposeState;
        private bool _messageSubscribed;
        private IDisposable? _resultSubscription;
        private bool _initializationStarted;
        private LocalCameraPreview? localPreview;
        private int localPreviewResultId = -1;
        private DeviceCamera? localPreviewOwner;
        private MySqlConnectedEndpoint? localPreviewEndpoint;
        private bool updatingLocalSelection;
        private long _previewSelectionVersion;
        private long _historyRevision;
        private bool _isInitialized;
        internal bool IsContentInitialized => _isInitialized;

        private bool IsDisposed => Volatile.Read(ref _disposeState) != 0;

        public DeviceCamera Device { get; set; }

        public static ViewCameraConfig Config => ViewCameraConfig.Instance;
        private readonly ViewCameraConfig _historyConfig;
        private readonly ResultHistoryCollection<ViewResultImage> _viewResults;
        private readonly MQTTCamera _service;
        private readonly Func<MySqlConnectedEndpoint, int, MeasureResultImgModel?> _readResult;
        private readonly Func<MySqlConnectedEndpoint, OrderByType, int, List<MeasureResultImgModel>> _readHistory;
        private readonly object _resultQueueLock = new();
        private readonly Queue<ResultNotice> _resultQueue = new();
        private bool _resultDrainScheduled;
        public ObservableCollection<ViewResultImage> ViewResults => _viewResults;

        public event EventHandler? StatusBarItemsChanged;

        public IEnumerable<StatusBarMeta> GetActiveStatusBarItems() =>
            !IsDisposed && ImageView != null ? ImageView.GetActiveStatusBarItems() : [];

        private void ImageView_StatusBarItemsChanged(object? sender, EventArgs e)
        {
            if (ImageView.ViewBitmapSource != null)
                HistoryImageError.Visibility = Visibility.Collapsed;
            StatusBarItemsChanged?.Invoke(this, e);
        }

        public ViewCamera(DeviceCamera device) : this(device, false) { }

        internal ViewCamera(DeviceCamera device, bool deferInitialization,
            Func<MySqlConnectedEndpoint, int, MeasureResultImgModel?>? readResult = null,
            Func<MySqlConnectedEndpoint, OrderByType, int, List<MeasureResultImgModel>>? readHistory = null)
        {
            _historyConfig = Config;
            _viewResults = new ResultHistoryCollection<ViewResultImage>(result => result.Id, _historyConfig.MaxHistoryCount);
            _viewResults.CollectionChanged += ViewResults_CollectionChanged;
            _historyConfig.PropertyChanged += HistoryConfig_PropertyChanged;
            Device = device;
            _service = device.DService;
            _readResult = readResult ?? MeasureImgResultDao.ReadResult;
            _readHistory = readHistory ?? ReadConnectedHistory;
            _service.MsgReturnReceived += DeviceService_OnMessageRecved;
            _messageSubscribed = true;
            _resultSubscription = ResultMessageBus.Default.Subscribe(ResultPublished);
            Loaded += View_Loaded;
            IsVisibleChanged += View_IsVisibleChanged;
            if (!deferInitialization)
                EnsureInitialized();
        }

        private void ViewResults_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => _historyRevision++;

        private void View_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsVisible) EnsureInitialized();
        }

        private void HistoryConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (IsDisposed || e.PropertyName != nameof(ResultViewConfig.MaxHistoryCount)) return;
            void ApplyLimit()
            {
                if (!IsDisposed) _viewResults.MaxCount = _historyConfig.MaxHistoryCount;
            }
            if (Dispatcher.CheckAccess()) ApplyLimit();
            else Dispatcher.BeginInvoke(ApplyLimit);
        }

        private void Result_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is ListViewItem { DataContext: ViewResultImage result } item)
            {
                result.ContextMenu.PlacementTarget = item;
                result.ContextMenu.IsOpen = true;
                e.Handled = true;
            }
        }

        private void View_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsLoaded && IsVisible) EnsureInitialized();
        }

        internal void EnsureInitialized()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(EnsureInitialized);
                return;
            }
            if (IsDisposed || _initializationStarted) return;
            _initializationStarted = true;
            Loaded -= View_Loaded;
            IsVisibleChanged -= View_IsVisibleChanged;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            // A deferred dock shell already inherits its host's context when XAML bindings are created.
            DataContext = Config;
            InitializeComponent();
            // A registered shell may already have raised FrameworkElement.Initialized.
            UserControl_Initialized(this, EventArgs.Empty);
            log.Info($"Device view initialized. View={nameof(ViewCamera)}, Duration={stopwatch.ElapsedMilliseconds}ms.");
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (IsDisposed || _isInitialized) return;
            _isInitialized = true;
            FovImageViewRunner.BindCamera(ImageView, Device);
            ImageView.StatusBarItemsChanged += ImageView_StatusBarItemsChanged;

            if (ImageView.EditorContext.IEditorToolFactory.GetIEditorTool<ToolReferenceLine>() is ToolReferenceLine toolReferenceLine)
            {
                toolReferenceLine.ReferenceLine = new ReferenceLine(Device.DisplayConfig.ReferenceLineParam);
            }

            listView1.ItemsSource = ViewResults;

            if (listView1.View is GridView gridView)
            {
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
                ViewCameraConfig.Instance.GridViewColumnVisibilitys.CopyToGridView(GridViewColumnVisibilitys);
                ViewCameraConfig.Instance.GridViewColumnVisibilitys = GridViewColumnVisibilitys;
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
            }

            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => Delete(), (s, e) => e.CanExecute = listView1.SelectedIndex > -1));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, (s, e) => listView1.SelectAll(), (s, e) => e.CanExecute = true));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, ListViewUtils.Copy, (s, e) => e.CanExecute = true));
        }

        private void Delete()
        {
            if (listView1.SelectedItems.Count == listView1.Items.Count)
                ViewResults.Clear();
            else
            {
                listView1.SelectedIndex = -1;
                foreach (var item in listView1.SelectedItems.Cast<ViewResultImage>().ToList())
                    ViewResults.Remove(item);
            }
        }

        private void ClearList_Click(object sender, RoutedEventArgs e) => ViewResults.Clear();

        private void DeviceService_OnMessageRecved(MsgReturn arg)
        {
            DeviceCamera owner = Device;
            if (arg.Code != 102 || arg.EventName != "AutoFocus" || !OwnsAutoFocus()) return;

            try
            {
                Dispatcher.Invoke(() =>
                {
                    if (!OwnsAutoFocus()) return;
                    owner.Config.MotorConfig.Position = arg.Data.Position;
                    OpenAutoFocusImage((string?)arg.Data.ImageTmpFile);
                });
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }

            bool OwnsAutoFocus() => CanApplyResult(owner, serviceOnly: true) && arg.DeviceCode == owner.Config.Code;
        }

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0 && listView1.View is GridView gridView)
                GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, GridViewColumnVisibilitys);
        }
        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {
            e.Handled = ViewResults.SortByGridViewColumn<ViewResultImage>(sender, GridViewColumnVisibilitys, Properties.Resources.ResourceManager);
        }

        private void Button_Click_Export(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex < 0)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), Properties.Resources.SelectDataFirst, "ColorVision");
                return;
            }
            using var dialog = new System.Windows.Forms.SaveFileDialog();
            //dialog.Filter = "files (*.csv) | *.csv";
            dialog.FileName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            dialog.RestoreDirectory = true;
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            dialog.FileName = dialog.FileName + ".csv";
            CsvWriter.WriteToCsv(ViewResults[listView1.SelectedIndex], dialog.FileName);
        }


        private async void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsDisposed || updatingLocalSelection || !ReferenceEquals(e.Source, listView1)) return;
            long version = BeginPreviewSelection();
            FocusChart.ClearCompletedAutoFocus();

            if (listView1.SelectedItem is not ViewResultImage result)
            {
                ImageView.Clear();
                return;
            }

            if (GetCachedResultPreview(result) is { } preview)
            {
                preview.Show(ImageView);
                return;
            }

            await OpenHistoryImageAsync(result, version);
        }

        private async Task OpenHistoryImageAsync(ViewResultImage result, long version)
        {
            string path = result.FileUrl;
            Task open = ImageView.ImageOpenCompletion;
            long imageRevision = ImageView.ImageRevision;
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                    throw new FileNotFoundException("The camera result has no image path.");
                if (!ImageView.TryOpenImage(path))
                    throw new NotSupportedException($"Unsupported camera result image format: {Path.GetExtension(path)}");

                open = ImageView.ImageOpenCompletion;
                imageRevision = ImageView.ImageRevision;
                await open;
                if (ImageView.ViewBitmapSource == null)
                    throw new InvalidDataException("The camera result did not publish an image.");
            }
            catch (Exception error)
            {
                if (!OwnsHistoryPreview(result, version) || !ReferenceEquals(ImageView.ImageOpenCompletion, open)
                    || !ImageView.IsCurrentImageRevision(imageRevision)) return;

                log.Warn($"相机结果图像打开失败：resultId={result.Id}, deviceCode={result.DeviceCode}, file={path}", error);
                ImageView.Clear();
                if (!OwnsHistoryPreview(result, version)) return;
                HistoryImageError.Text = string.Format(CultureInfo.CurrentCulture, EngineLocalization.Get("Camera_ResultImageOpenFailed"), result.Id);
                HistoryImageError.Visibility = Visibility.Visible;
            }
        }

        private long BeginPreviewSelection()
        {
            HistoryImageError.Visibility = Visibility.Collapsed;
            return ++_previewSelectionVersion;
        }

        private bool OwnsHistoryPreview(ViewResultImage result, long version) => !IsDisposed && version == _previewSelectionVersion
            && !Device.DisplayConfig.IsLocalVideoOpen && ReferenceEquals(listView1.SelectedItem, result);

        private void ResultPublished(ResultMessage message)
        {
            DeviceCamera owner = Device;
            bool serviceOnly = message.SourceService != null;
            if (message.Route != ResultRoutes.Camera
                || message.ResultKind != ResultKinds.Image
                || !string.Equals(message.DeviceCode, owner.Config.Code, StringComparison.Ordinal)
                || (message.SourceDevice != null && !ReferenceEquals(message.SourceDevice, owner))
                || (serviceOnly && (message.SourceDevice == null || !ReferenceEquals(message.SourceService, _service))))
                return;

            MeasureResultImgModel? attachment = serviceOnly ? null : message.Attachment as MeasureResultImgModel;
            EnqueueResult(new ResultNotice(owner, message.DeviceCode, message.Data.MasterId, attachment, serviceOnly, message.DatabaseEndpoint));
        }

        private bool CanApplyResult(DeviceCamera owner, bool serviceOnly) => !IsDisposed && !owner.IsDisposed
            && ReferenceEquals(Device, owner) && owner.ViewRegistration.IsVisible
            && (!serviceOnly || (ReferenceEquals(owner.DService, _service) && !owner.RoutesLocally));

        private void EnqueueResult(ResultNotice notice)
        {
            if (!CanApplyResult(notice)) return;
            lock (_resultQueueLock)
            {
                if (IsDisposed) return;
                _resultQueue.Enqueue(notice);
                if (_resultDrainScheduled) return;
                _resultDrainScheduled = true;
            }
            _ = Task.Run(DrainResultsAsync);
        }

        private async Task DrainResultsAsync()
        {
            while (true)
            {
                ResultNotice notice;
                lock (_resultQueueLock)
                {
                    if (!_resultQueue.TryDequeue(out notice))
                    {
                        _resultDrainScheduled = false;
                        return;
                    }
                }

                try
                {
                    if (!CanApplyResult(notice)) continue;
                    if ((notice.Attachment ?? _readResult(notice.Endpoint!, notice.MasterId)) is not { } model) continue;
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (CanApplyResult(notice)) ShowResult(model, notice.Endpoint, notice.ServiceOnly);
                    }).Task.ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    if (!IsDisposed) log.Error($"Unable to present camera result. MasterId={notice.MasterId}, Error={error.GetType().Name}");
                }
            }
        }

        private bool CanApplyResult(ResultNotice notice) => CanApplyResult(notice.Owner, notice.ServiceOnly)
            && string.Equals(notice.DeviceCode, notice.Owner.Config.Code, StringComparison.Ordinal)
            && (notice.Attachment == null
                ? notice.MasterId > 0 && notice.Owner.OwnsResultDatabase(notice.Endpoint)
                : notice.Endpoint == null || notice.Owner.OwnsResultDatabase(notice.Endpoint));

        private readonly record struct ResultNotice(DeviceCamera Owner, string DeviceCode, int MasterId,
            MeasureResultImgModel? Attachment, bool ServiceOnly, MySqlConnectedEndpoint? Endpoint);

        private void OpenAutoFocusImage(string? filePath)
        {
            if (IsDisposed) return;
            EnsureInitialized();
            BeginPreviewSelection();
            FocusChart.ClearCompletedAutoFocus();
            if (string.IsNullOrWhiteSpace(filePath))
                ImageView.Clear();
            else
                ImageView.OpenImage(filePath);
        }

        private void listView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && listView1.SelectedIndex > -1)
            {
                int temp = listView1.SelectedIndex;
                ViewResults.RemoveAt(temp);
            }
        }

        internal void ShowLocalResult(MeasureResultImgModel? model, LocalCameraPreview preview, bool forceDisplay, bool focusFrame,
            MySqlConnectedEndpoint? databaseEndpoint = null)
        {
            if (IsDisposed || !Device.ViewRegistration.IsVisible) return;
            bool keepVideo = Device.DisplayConfig.IsLocalVideoOpen;
            if (keepVideo && model == null) return;
            EnsureInitialized();
            localPreview = preview;
            localPreviewOwner = Device;
            localPreviewEndpoint = databaseEndpoint;
            int resultId = model?.Id ?? -1;
            if (localPreviewResultId != resultId)
            {
                localPreviewResultId = resultId;
                CommandManager.InvalidateRequerySuggested();
            }
            bool showPreview = !keepVideo && (forceDisplay || Config.AutoRefreshView);
            updatingLocalSelection = true;
            try
            {
                if (model != null)
                {
                    ShowResult(model, databaseEndpoint, fromRemoteCapture: false);
                    if (showPreview)
                        listView1.SelectedItem = ViewResults.FirstOrDefault(item => item.Id == model.Id);
                }
            }
            finally { updatingLocalSelection = false; }
            if (showPreview)
            {
                BeginPreviewSelection();
                if (!focusFrame) FocusChart.ClearCompletedAutoFocus();
                preview.Show(ImageView);
                ImageView.UpdateZoomAndScale();
            }
        }

        public void ShowResult(MeasureResultImgModel model) => ShowResult(model, databaseEndpoint: null, fromRemoteCapture: false);

        private void ShowResult(MeasureResultImgModel model, MySqlConnectedEndpoint? databaseEndpoint, bool fromRemoteCapture)
        {
            if (IsDisposed || !Device.ViewRegistration.IsVisible
                || (databaseEndpoint != null && !Device.OwnsResultDatabase(databaseEndpoint))) return;
            EnsureInitialized();

            bool keepVideo = Device.DisplayConfig.IsLocalVideoOpen;
            bool previousSelectionUpdate = updatingLocalSelection;
            updatingLocalSelection = previousSelectionUpdate || keepVideo;
            try
            {
                ViewResultImage? previous = ViewResults.FirstOrDefault(item => item.Id == model.Id);
                if (previous != null)
                {
                    if (previous.FileUrl == (model.FileUrl ?? string.Empty) && previous.IsRemoteCapture == fromRemoteCapture
                        && ReferenceEquals(previous.DatabaseEndpoint, databaseEndpoint)) return;
                    ViewResults.Remove(previous);
                }
                ViewResultImage result = CreateResultRow(model, databaseEndpoint, fromRemoteCapture);
                if (Config.InsertAtBeginning)
                    ViewResults.Insert(0, result);
                else
                    ViewResults.Add(result);

                // A delayed persisted notification for an older capture must not replace the latest snapshot.
                if (!keepVideo && Config.AutoRefreshView
                    && (fromRemoteCapture || !ReferenceEquals(databaseEndpoint, localPreviewEndpoint) || model.Id >= localPreviewResultId)
                    && _viewResults.ContainsId(model.Id))
                {
                    listView1.SelectedItem = result;
                    listView1.ScrollIntoView(result);
                }
            }
            finally { updatingLocalSelection = previousSelectionUpdate; }
        }

        private ViewResultImage CreateResultRow(MeasureResultImgModel model, MySqlConnectedEndpoint? databaseEndpoint, bool fromRemoteCapture = false)
        {
            ViewResultImage result = new(model) { DatabaseEndpoint = databaseEndpoint, IsRemoteCapture = fromRemoteCapture };
            result.OpenCVCIECommand = new RelayCommand(async _ =>
            {
                Window? owner = Window.GetWindow(this);
                if (GetCachedResultPreview(result) is { } preview)
                    ImageViewWindow.Show(view => preview.Show(view), owner);
                else
                    await ImageViewWindow.OpenFileAsync(result.FileUrl, owner);
            }, _ => GetCachedResultPreview(result) != null || ImageViewWindow.CanOpenFile(result.FileUrl));
            return result;
        }

        private LocalCameraPreview? GetCachedResultPreview(ViewResultImage result) =>
            !IsDisposed && !result.IsRemoteCapture && localPreview is { } preview && result.Id == localPreviewResultId
            && ReferenceEquals(Device, localPreviewOwner) && ReferenceEquals(result.DatabaseEndpoint, localPreviewEndpoint)
            && (string.IsNullOrEmpty(result.DeviceCode) || string.Equals(result.DeviceCode, Device.Config.Code, StringComparison.Ordinal)) ? preview : null;

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            SearchAll();
        }

        public void SearchAll()
        {
            if (GetHistoryEndpoint() is not { } endpoint) return;
            List<MeasureResultImgModel> rows = _readHistory(endpoint, Config.OrderByType, Config.Count);
            ViewResults.Clear();
            _viewResults.QueryResults.AddRange(rows.Select(row => CreateResultRow(row, endpoint)));
        }

        internal Func<Task> PrepareCaptureHistoryRefresh(MySqlConnectedEndpoint endpoint, Func<bool> canApply)
        {
            Dispatcher.VerifyAccess();
            DeviceCamera owner = Device;
            long resultsRevision = _historyRevision;
            long selectionRevision = _previewSelectionVersion;
            OrderByType order = Config.OrderByType;
            int count = Config.Count;
            bool OwnsHistory() => canApply() && CanApplyResult(owner, serviceOnly: true) && owner.OwnsResultDatabase(endpoint)
                && resultsRevision == _historyRevision && selectionRevision == _previewSelectionVersion
                && order == Config.OrderByType && count == Config.Count;
            return async () =>
            {
                if (!OwnsHistory()) return;
                List<MeasureResultImgModel> rows = await Task.Run<List<MeasureResultImgModel>>(() => endpoint.IsCurrent ? _readHistory(endpoint, order, count) : []);
                if (!OwnsHistory()) return;

                ViewResults.Clear();
                _viewResults.QueryResults.AddRange(rows.Select(row => CreateResultRow(row, endpoint)));
            };
        }

        private static List<MeasureResultImgModel> ReadConnectedHistory(MySqlConnectedEndpoint endpoint, OrderByType order, int count)
        {
            using var db = endpoint.CreateDbClient();
            return ReadHistory(db, order, count);
        }

        private static List<MeasureResultImgModel> ReadHistory(ISqlSugarClient db, OrderByType order, int count)
        {
            var query = db.Queryable<MeasureResultImgModel>().OrderBy(x => x.Id, order);
            return count > 0 ? query.Take(count).ToList() : query.ToList();
        }


        private void Search1_Click(object sender, RoutedEventArgs e)
        {
            if (GetHistoryEndpoint() is not { } endpoint) return;
            using var db = endpoint.CreateDbClient();
            GenericQuery<MeasureResultImgModel, ViewResultImage> genericQuery = new(db, _viewResults.QueryResults, row => CreateResultRow(row, endpoint));
            GenericQueryWindow genericQueryWindow = new(genericQuery) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            genericQueryWindow.ShowDialog();
        }

        private MySqlConnectedEndpoint? GetHistoryEndpoint()
        {
            var endpoint = MySqlControl.GetInstance().ConnectedEndpoint;
            if (endpoint?.IsCurrent == true) return endpoint;
            MessageBox1.Show(Window.GetWindow(this), EngineLocalization.Get("数据库未连接，无法查询相机结果。"), "ColorVision");
            return null;
        }


        private void GridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            listView1.Height = MainGridRow2.ActualHeight - 32;
            MainGridRow1.Height = new GridLength(1, GridUnitType.Star);
            MainGridRow2.Height = GridLength.Auto;
        }

        internal static void DetachResultListView(ListView listView, SelectionChangedEventHandler selectionChangedHandler, KeyEventHandler previewKeyDownHandler)
        {
            listView.SelectionChanged -= selectionChangedHandler;
            listView.PreviewKeyDown -= previewKeyDownHandler;
            BindingOperations.ClearAllBindings(listView);
            listView.ItemsSource = null;
            listView.ContextMenu = null;
            listView.CommandBindings.Clear();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposeState, 1) != 0) return;

            lock (_resultQueueLock) _resultQueue.Clear();

            if (!Dispatcher.CheckAccess()) Dispatcher.Invoke(DisposeCore);
            else DisposeCore();
            GC.SuppressFinalize(this);
        }

        private void DisposeCore()
        {
            FocusChart?.Reset();
            _historyConfig.PropertyChanged -= HistoryConfig_PropertyChanged;
            _viewResults.CollectionChanged -= ViewResults_CollectionChanged;
            Loaded -= View_Loaded;
            IsVisibleChanged -= View_IsVisibleChanged;
            if (_messageSubscribed)
            {
                _service.MsgReturnReceived -= DeviceService_OnMessageRecved;
                _messageSubscribed = false;
            }
            _resultSubscription?.Dispose();
            _resultSubscription = null;

            if (ReferenceEquals(Config.GridViewColumnVisibilitys, GridViewColumnVisibilitys))
                Config.GridViewColumnVisibilitys = new ObservableCollection<GridViewColumnVisibility>(GridViewColumnVisibilitys.Select(column =>
                    new GridViewColumnVisibility { ColumnName = column.ColumnName, IsVisible = column.IsVisible, IsSortD = column.IsSortD }));
            if (listView1 != null)
                DetachResultListView(listView1, listView1_SelectionChanged, listView1_PreviewKeyDown);
            ViewResults.Clear();
            localPreview = null;
            localPreviewOwner = null;
            localPreviewEndpoint = null;
            if (ImageView != null)
            {
                ImageView.StatusBarItemsChanged -= ImageView_StatusBarItemsChanged;
                ImageView.Dispose();
            }
            DataContext = null;
        }
    }
}
