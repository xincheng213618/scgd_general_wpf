#pragma warning disable CA1822
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Messages;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.ImageEditor.EditorTools.Filters;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using log4net;
using MQTTMessageLib.Camera;
using SqlSugar;
using System;
using System.Collections.Generic;
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
        private readonly ResultImagePlaceholderCache _resultImagePlaceholderCache = new();
        private int _disposeState;
        private int _imageRequestId;
        private bool _messageSubscribed;

        private bool IsDisposed => Volatile.Read(ref _disposeState) != 0;

        public DeviceCamera Device { get; set; }

        public static ViewCameraConfig Config => ViewCameraConfig.Instance;
        public static ObservableCollection<ViewResultImage> ViewResults => Config.ViewResults;

        public ViewCamera(DeviceCamera device)
        {
            Device = device;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (IsDisposed) return;

            this.DataContext = Config;
            AttachDisplayFilterConfig();
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

            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => Delete(), (s, e) => e.CanExecute = listView1.SelectedIndex > -1));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, (s, e) => listView1.SelectAll(), (s, e) => e.CanExecute = true));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, ListViewUtils.Copy, (s, e) => e.CanExecute = true));
        }

        private void AttachDisplayFilterConfig()
        {
            if (ImageView.IEditorToolFactory.GetIEditorTool<DisplayShaderFilterEditorTool>() is DisplayShaderFilterEditorTool filterService)
            {
                filterService.AttachPersistence(Device.DisplayConfig.DisplayShaderFilter, SaveDisplayFilterConfig);
            }
        }

        private static void SaveDisplayFilterConfig()
        {
            ConfigHandler.GetInstance().Save<DisplayConfigManager>();
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
                                int requestId = Interlocked.Increment(ref _imageRequestId);
                                OpenImageOrPlaceholder((string?)arg.Data.ImageTmpFile, null, requestId);
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
                    int masterId = Convert.ToInt32(arg.Data.MasterId);
                    List<MeasureResultImgModel> resultMaster = null;
                    if (masterId > 0)
                    {
                        resultMaster = new List<MeasureResultImgModel>();
                        MeasureResultImgModel model = MeasureImgResultDao.Instance.GetById(masterId);
                        if (model != null)
                            resultMaster.Add(model);
                    }
                    if (resultMaster != null)
                    {
                        foreach (MeasureResultImgModel result in resultMaster)
                        {
                            Application.Current?.Dispatcher.BeginInvoke(() =>
                            {
                                if (IsDisposed) return;
                                ShowResult(result);
                            });
                        }
                    }
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
            if (IsDisposed || sender is not ListView listView) return;

            int requestId = Interlocked.Increment(ref _imageRequestId);
            ViewResultImage? result = listView.SelectedItem as ViewResultImage;
            OpenImageOrPlaceholder(result?.FileUrl, result?.ImgFrameInfo, requestId);
        }

        private void OpenImageOrPlaceholder(string? filePath, string? imgFrameInfo, int requestId)
        {
            if (IsDisposed || requestId != Volatile.Read(ref _imageRequestId)) return;

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                ShowPlaceholderOrClear(imgFrameInfo);
                return;
            }

            if (filePath.Equals(ImageView.Config.FilePath, StringComparison.OrdinalIgnoreCase) && ImageView.ImageShow.Source != null) return;

            if (IsDisposed || requestId != Volatile.Read(ref _imageRequestId)) return;
            if (!File.Exists(filePath))
            {
                ShowPlaceholderOrClear(imgFrameInfo);
                return;
            }
            // CVRawOpen reuses a compatible WriteableBitmap and keeps the current viewport.
            ImageView.OpenImage(filePath);
        }

        private void ShowPlaceholderOrClear(string? imgFrameInfo)
        {
            if (!ResultImageDimensions.TryReadFrameInfo(imgFrameInfo, out int width, out int height))
            {
                ImageView.Clear();
                return;
            }

            if (_resultImagePlaceholderCache.IsCurrent(ImageView.ImageShow.Source, width, height))
            {
                ImageView.ClearAnnotations();
                return;
            }

            ImageView.Clear();
            ImageView.Config.SetImageMetadata(ImageViewPropertyKeys.Cols, width, nameof(ViewCamera), "历史结果坐标空间宽度");
            ImageView.Config.SetImageMetadata(ImageViewPropertyKeys.Rows, height, nameof(ViewCamera), "历史结果坐标空间高度");
            ImageView.Config.SetImageMetadata(ImageViewPropertyKeys.ImageWidth, width, nameof(ViewCamera), "历史结果图像像素宽度");
            ImageView.Config.SetImageMetadata(ImageViewPropertyKeys.ImageHeight, height, nameof(ViewCamera), "历史结果图像像素高度");
            ImageView.SetImageSource(_resultImagePlaceholderCache.GetOrCreate(width, height), enableEditorImageServices: false, configureDefaultLayerController: false);
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
            Interlocked.Increment(ref _imageRequestId);
            if (_messageSubscribed)
            {
                Device.DService.MsgReturnReceived -= DeviceService_OnMessageRecved;
                _messageSubscribed = false;
            }

            DetachResultListView(listView1, listView1_SelectionChanged, listView1_PreviewKeyDown);
            ImageView.Dispose();
            DataContext = null;
            GC.SuppressFinalize(this);
        }
    }
}
