#pragma warning disable CS8604,CS8629
using ColorVision.Common.Utilities;
using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Messages;
using ColorVision.Net;
using ColorVision.Themes.Controls;
using ColorVision.UI.Sorts;
using ColorVision.UI.Views;
using log4net;
using MQTTMessageLib.Calibration;
using MQTTMessageLib.Camera;
using MQTTMessageLib.FileServer;
using Newtonsoft.Json;
using Panuon.WPF.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Templates.Flow;


namespace ColorVision.Engine.Services.Devices.Calibration.Views
{
    /// <summary>
    /// ViewCamera.xaml 的交互逻辑
    /// </summary>
    public partial class ViewCalibration : UserControl, IView
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(ViewCalibration));

        public View View { get; set; }

        public  MQTTCalibration DeviceService => Device.DService;
        public DeviceCalibration Device { get; set; }

        public ViewCalibration(DeviceCalibration device)
        {
            Device = device;
            InitializeComponent();
        }
        public static ViewCalibrationConfig Config => ViewCalibrationConfig.Instance;
        public static ObservableCollection<ViewResultCamera> ViewResults => Config.ViewResults;

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = this;
            View = new View();
            ImageView.SetConfig(Config.ImageViewConfig);
            listView1.ItemsSource = ViewResults;

            if (listView1.View is GridView gridView)
            {
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
                Config.GridViewColumnVisibilitys.CopyToGridView(GridViewColumnVisibilitys);
                Config.GridViewColumnVisibilitys = GridViewColumnVisibilitys;
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
            }

            netFileUtil = new NetFileUtil();
            netFileUtil.handler += NetFileUtil_handler;
            DeviceService.MsgReturnReceived += DeviceService_OnMessageRecved;
        }


        NetFileUtil netFileUtil;
        private IPendingHandler? handler { get; set; }

        private void NetFileUtil_handler(object sender, NetFileEvent arg)
        {
            if (arg.Code == 0)
            {
                if (arg.EventName == FileEvent.FileDownload && arg.FileData.data != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        OpenImage(arg.FileData);
                    });
                }
                handler?.Close();
            }
            else
            {
                handler?.Close();
            }
        }

        string LocalFileName;
        private void DeviceService_OnMessageRecved(MsgReturn arg)
        {
            if (arg.Code == 0)
            {
                switch (arg.EventName)
                {
                    case MQTTCalibrationEventEnum.Event_GetData:
                        int masterId = Convert.ToInt32(arg.Data.MasterId);
                        List<MeasureImgResultModel> resultMaster = null;
                        if (masterId > 0)
                        {
                            resultMaster = new List<MeasureImgResultModel>();
                            MeasureImgResultModel model = MeasureImgResultDao.Instance.GetById(masterId);
                            if (model != null)
                                resultMaster.Add(model);
                        }
                        if (resultMaster != null)
                        {
                            foreach (MeasureImgResultModel result in resultMaster)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    ShowResult(result);
                                });
                            }
                        }
                        break;
                    case MQTTFileServerEventEnum.Event_File_Download:
                        break;
                    case MQTTFileServerEventEnum.Event_File_GetChannel:
                        DeviceGetChannelResult pm_dl_ch = JsonConvert.DeserializeObject<DeviceGetChannelResult>(JsonConvert.SerializeObject(arg.Data));
                        netFileUtil.TaskStartDownloadFile(pm_dl_ch.IsLocal, pm_dl_ch.FileURL, (CVType)pm_dl_ch.FileExtType);
                        break;
                }
            }
            else if (arg.Code == 102)
            {
                switch (arg.EventName)
                {
                    case MQTTFileServerEventEnum.Event_File_Download:
                        DeviceFileUpdownParam pm_dl = JsonConvert.DeserializeObject<DeviceFileUpdownParam>(JsonConvert.SerializeObject(arg.Data));
                        LocalFileName = pm_dl.FileName;
                        if (!string.IsNullOrWhiteSpace(pm_dl.FileName)) netFileUtil.TaskStartDownloadFile(pm_dl.IsLocal, pm_dl.ServerEndpoint, pm_dl.FileName, (CVType)pm_dl.FileExtType);
                        break;
                    case MQTTFileServerEventEnum.Event_File_GetChannel:
                        DeviceGetChannelResult pm_dl_ch = JsonConvert.DeserializeObject<DeviceGetChannelResult>(JsonConvert.SerializeObject(arg.Data));
                        netFileUtil.TaskStartDownloadFile(pm_dl_ch.IsLocal, pm_dl_ch.FileURL, (CVType)pm_dl_ch.FileExtType);
                        break;
                }
            }
            else
            {
                switch (arg.EventName)
                {
                    case MQTTCalibrationEventEnum.Event_GetData:
                        int masterId = Convert.ToInt32(arg.Data.MasterId);
                        List<MeasureImgResultModel> resultMaster = null;
                        if (masterId > 0)
                        {
                            resultMaster = new List<MeasureImgResultModel>();
                            MeasureImgResultModel model = MeasureImgResultDao.Instance.GetById(masterId);
                            if (model != null)
                                resultMaster.Add(model);
                        }
                        if (resultMaster != null)
                        {
                            foreach (MeasureImgResultModel result in resultMaster)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    ShowResult(result);
                                });
                            }
                        }
                        break;
                    case MQTTFileServerEventEnum.Event_File_Download:
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox1.Show("文件下载失败");
                        });
                        break;
                    case MQTTFileServerEventEnum.Event_File_GetChannel:
                        break;
                    case MQTTCameraEventEnum.Event_OpenLive:
                        break;
                }
            }
        }


        public void ShowResult(MeasureImgResultModel model)
        {
            ViewResultCamera result = new(model);
            ViewResults.AddUnique(result);
            if (Config.AutoRefreshView && (!FlowConfig.Instance.FlowRun || FlowConfig.Instance.AutoRefreshView))
            {
                if (listView1.Items.Count > 0) listView1.SelectedIndex = listView1.Items.Count - 1;
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

        private void Button_Click_Export(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex < 0)
            {
                MessageBox1.Show(Application.Current.MainWindow, "您需要先选择数据", "ColorVision");
                return;
            }
            using var dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.Filter = "CSV files (*.csv) | *.csv";
            dialog.FileName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            dialog.RestoreDirectory = true;
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            CsvWriter.WriteToCsv(ViewResults[listView1.SelectedIndex], dialog.FileName);
            if (File.Exists(LocalFileName))
            {
            }
            else
            {
                ImageSource bitmapSource = ImageView.ImageShow.Source;
                ImageUtils.SaveImageSourceToFile(bitmapSource, Path.Combine(Path.GetDirectoryName(dialog.FileName), Path.GetFileNameWithoutExtension(dialog.FileName) + ".png"));
            }
        }


        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listView1.SelectedIndex > -1)
            {
                var data = ViewResults[listView1.SelectedIndex];
                if (File.Exists(data.FileUrl))
                {
                    ImageView.OpenImage(data.FileUrl);
                }
                else
                {
                    if (data.ResultCode == 0 && data.FilePath != null)
                    {
                        FileExtType fileExt = FileExtType.Src;
                        switch (data.FileType)
                        {
                            case CameraFileType.SrcFile:
                                fileExt = FileExtType.Src;
                                break;
                            case CameraFileType.RawFile:
                                fileExt = FileExtType.Raw;
                                break;
                            case CameraFileType.CIEFile:
                                fileExt = FileExtType.CIE;
                                break;
                            default:
                                break;
                        }
                        DeviceService.Open(data.FilePath, fileExt);
                    }
                }
            }
        }

        private void listView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && listView1.SelectedIndex > -1)
            {
                int temp = listView1.SelectedIndex;
                ViewResults.RemoveAt(temp);
            }
        }

        public void OpenImage(CVCIEFile fileData)
        {
            ImageView.OpenImage(fileData.ToWriteableBitmap());
        }


        private void SearchAdvanced_Click(object sender, RoutedEventArgs e)
        {
            ViewResults.Clear();
            List<MeasureImgResultModel> algResults = MeasureImgResultDao.Instance.GetAllDevice(Device.Code, Config.SearchLimit);
            if (!Config.InsertAtBeginning)
                algResults.Reverse();
            foreach (var item in algResults)
            {
                ViewResultCamera algorithmResult = new(item);
                ViewResults.AddUnique(algorithmResult);
            }
        }

        private void Search1_Click(object sender, RoutedEventArgs e)
        {
            AdvanceSearchConfig.Instance.Limit = Config.SearchLimit;
            AdvanceSearch advanceSearch = new AdvanceSearch(MeasureImgResultDao.Instance) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            advanceSearch.Closed += (s, e) =>
            {
                if (Config.InsertAtBeginning)
                    advanceSearch.SearchResults.Reverse();
                ViewResults.Clear();

                foreach (var item in advanceSearch.SearchResults)
                {
                    ViewResultCamera algorithmResult = new ViewResultCamera(item);
                    ViewResults.AddUnique(algorithmResult);
                }
            };
            advanceSearch.Show();
        }

        private void MenuItem_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is ViewResultCamera viewResult)
            {
                ViewResults.Remove(viewResult);
                ImageView.Clear();
            }
        }


        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {
            if (sender is GridViewColumnHeader gridViewColumnHeader && gridViewColumnHeader.Content !=null)
            {
                foreach (var item in GridViewColumnVisibilitys)
                {
                    if (item.ColumnName.ToString() == gridViewColumnHeader.Content.ToString())
                    {
                        string Name = item.ColumnName.ToString();
                        if (Name == Properties.Resources.SerialNumber1)
                        {
                            item.IsSortD = !item.IsSortD;
                            ViewResults.SortByID(item.IsSortD);
                        }
                        else if (Name == Properties.Resources.Duration)
                        {
                            item.IsSortD = !item.IsSortD;
                            ViewResults.SortByCreateTime(item.IsSortD);
                        }
                        else if (Name == Properties.Resources.BatchNumber)
                        {
                            item.IsSortD = !item.IsSortD;
                            ViewResults.SortByBatch(item.IsSortD);
                        }
                        else if (Name == Properties.Resources.File)
                        {
                            item.IsSortD = !item.IsSortD;
                            ViewResults.SortByFilePath(item.IsSortD);
                        }
                    }
                }
            }
        }

        private void GridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            listView1.Height = MainGridRow2.ActualHeight - 32;
            MainGridRow1.Height = new GridLength(1, GridUnitType.Star);
            MainGridRow2.Height = GridLength.Auto;
        }
    }
}
