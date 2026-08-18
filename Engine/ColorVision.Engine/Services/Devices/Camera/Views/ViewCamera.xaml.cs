#pragma warning disable CA1822
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Results;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using log4net;
using MQTTMessageLib.Camera;
using SqlSugar;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
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
    public partial class ViewCamera : UserControl, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(App));
        private int _disposeState;
        private bool _messageSubscribed;
        private IDisposable? _localResultSubscription;

        private bool IsDisposed => Volatile.Read(ref _disposeState) != 0;

        public DeviceCamera Device { get; set; }

        public static ViewCameraConfig Config => ViewCameraConfig.Instance;
        public ObservableCollection<ViewResultImage> ViewResults { get; } = new ObservableCollection<ViewResultImage>();

        public ViewCamera(DeviceCamera device)
        {
            Device = device;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (IsDisposed) return;

            this.DataContext = Config;
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
            if (!_messageSubscribed)
            {
                Device.DService.MsgReturnReceived += DeviceService_OnMessageRecved;
                _messageSubscribed = true;
            }
            _localResultSubscription ??= ResultMessageBus.Default.Subscribe(LocalResultPublished);

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
            if (IsDisposed || arg.DeviceCode != Device.Config.Code) return;

            if (arg.Code == 102)
            {
                switch (arg.EventName)
                {
                    case "AutoFocus":
                        try
                        {
                            Application.Current?.Dispatcher.Invoke(() =>
                            {
                                if (IsDisposed) return;

                                Device.Config.MotorConfig.Position = arg.Data.Position;
                                OpenImage((string?)arg.Data.ImageTmpFile);
                            });
                        }
                        catch (Exception ex)
                        {
                            log.Error(ex);
                        }
                        break;
                    default:
                        break;
                }

                return;
            }

            switch (arg.EventName)
            {
                case MQTTCameraEventEnum.Event_GetData:
                    if (arg.Data == null) return;
                    ShowPersistedResult(Convert.ToInt32(arg.Data.MasterId));
                    break;
            }
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
                MessageBox1.Show(Application.Current.MainWindow, Properties.Resources.SelectDataFirst, "ColorVision");
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


        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsDisposed) return;

            if (listView1.SelectedItem is ViewResultImage result)
                OpenImage(result.FileUrl);
            else
                ImageView.Clear();
        }

        private void LocalResultPublished(ResultMessage message)
        {
            if (IsDisposed
                || message.Route != ResultRoutes.Camera
                || message.ResultKind != ResultKinds.Image
                || !string.Equals(message.DeviceCode, Device.Config.Code, StringComparison.Ordinal))
                return;

            ShowPersistedResult(message.Data.MasterId);
        }

        private void ShowPersistedResult(int masterId)
        {
            if (IsDisposed || masterId <= 0) return;
            MeasureResultImgModel? model = MeasureImgResultDao.Instance.GetById(masterId);
            if (model == null) return;
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (!IsDisposed)
                    ShowResult(model);
            });
        }

        private void OpenImage(string? filePath)
        {
            if (IsDisposed) return;
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

        public void ShowResult(MeasureResultImgModel model)
        {
            if (IsDisposed) return;

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
        private void Search_Click(object sender, RoutedEventArgs e)
        {
            SearchAll();
        }

        public void SearchAll()
        {
            ViewResults.Clear();
            using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

            var query = Db.Queryable<MeasureResultImgModel>();
            query = query.OrderBy(x => x.Id, Config.OrderByType);
            var dbList = Config.Count > 0 ? query.Take(Config.Count).ToList() : query.ToList();
            foreach (var item in dbList)
            {
                ViewResultImage ViewResultAlg = new(item);
                ViewResults.Add(ViewResultAlg);
            }
        }



        private void Search1_Click(object sender, RoutedEventArgs e)
        {
            var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

            GenericQuery<MeasureResultImgModel, ViewResultImage> genericQuery = new GenericQuery<MeasureResultImgModel, ViewResultImage>(Db, ViewResults, t => new ViewResultImage(t));
            GenericQueryWindow genericQueryWindow = new GenericQueryWindow(genericQuery) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }; ;
            genericQueryWindow.ShowDialog();
            Db.Dispose();
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

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(DisposeCore);
                return;
            }

            DisposeCore();
        }

        private void DisposeCore()
        {
            if (_messageSubscribed)
            {
                Device.DService.MsgReturnReceived -= DeviceService_OnMessageRecved;
                _messageSubscribed = false;
            }
            _localResultSubscription?.Dispose();
            _localResultSubscription = null;

            DetachResultListView(listView1, listView1_SelectionChanged, listView1_PreviewKeyDown);
            ImageView.Dispose();
            DataContext = null;
            GC.SuppressFinalize(this);
        }
    }
}
