using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Impl.SolutionImpl.Export;
using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Msg;
using ColorVision.Net;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Draw.Ruler;
using ColorVision.UI.Sorts;
using ColorVision.UI.Views;
using log4net;
using MQTTMessageLib.Camera;
using MQTTMessageLib.FileServer;
using Newtonsoft.Json;
using Panuon.WPF.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ColorVision.Engine.Services.Devices.Camera.Views
{
    public class ViewCameraConfig : ViewModelBase, IConfig
    {
        public static ViewCameraConfig Instance => ConfigHandler.GetInstance().GetRequiredService<ViewCameraConfig>();

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        public ImageViewConfig ImageViewConfig { get; set; } = new ImageViewConfig();

        public bool IsShowListView { get => _IsShowListView; set { _IsShowListView = value; NotifyPropertyChanged(); } }
        private bool _IsShowListView = true;

        public bool AutoRefreshView { get => _AutoRefreshView; set { _AutoRefreshView = value; NotifyPropertyChanged(); } }
        private bool _AutoRefreshView;
    }


    /// <summary>
    /// ViewCamera.xaml 的交互逻辑
    /// </summary>
    public partial class ViewCamera : UserControl, IView
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(App));

        public View View { get; set; }
        public ObservableCollection<ViewResultCamera> ViewResultCameras { get; set; } = new ObservableCollection<ViewResultCamera>();
        public DeviceCamera Device { get; set; }

        public static ViewCameraConfig Config => ViewCameraConfig.Instance;

        public ViewCamera(DeviceCamera device)
        {
            Device = device;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = this ;
            View = new View();
            ImageView.SetConfig(ViewCameraConfig.Instance.ImageViewConfig);

            ImageView.ToolBarTop.ToolBarScaleRuler.ScalRuler.ActualLength = Device.Config.ScaleFactor;
            ImageView.ToolBarTop.ToolBarScaleRuler.ScalRuler.PhysicalUnit = Device.Config.ScaleFactorUnit;
            ImageView.ToolBarTop.ToolBarScaleRuler.ScalRuler.PropertyChanged += (s, e) =>
            {
                if (s is DrawingVisualScaleHost host)
                {
                    if (e.PropertyName == "ActualLength")
                    {
                        Device.Config.ScaleFactor = host.ActualLength;
                        Device.SaveConfig();
                    }
                    else if (e.PropertyName == "PhysicalUnit")
                    {
                        Device.Config.ScaleFactorUnit = host.PhysicalUnit;
                        Device.SaveConfig();
                    }
                }
            };

            listView1.ItemsSource = ViewResultCameras;

            if (listView1.View is GridView gridView)
            {
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
                ViewCameraConfig.Instance.GridViewColumnVisibilitys.CopyToGridView(GridViewColumnVisibilitys);
                ViewCameraConfig.Instance.GridViewColumnVisibilitys = GridViewColumnVisibilitys;
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
            }

            netFileUtil = new NetFileUtil();
            netFileUtil.handler += NetFileUtil_handler;
            Device.DService.MsgReturnReceived += DeviceService_OnMessageRecved;
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

        private MeasureImgResultDao measureImgResultDao = new();
        private void DeviceService_OnMessageRecved(MsgReturn arg)
        {
            if (arg.Code == 0)
            {
                switch (arg.EventName)
                {
                    case MQTTCameraEventEnum.Event_GetData:
                        int masterId = Convert.ToInt32(arg.Data.MasterId);
                        List<MeasureImgResultModel> resultMaster = null;
                        if (masterId > 0)
                        {
                            resultMaster = new List<MeasureImgResultModel>();
                            MeasureImgResultModel model = measureImgResultDao.GetById(masterId);
                            if (model != null)
                                resultMaster.Add(model);
                        }
                        else
                        {
                            resultMaster = measureImgResultDao.GetAllByBatchCode(arg.SerialNumber);
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
                        netFileUtil.TaskStartDownloadFile(pm_dl_ch);
                        break;
                }
            }
            else if (arg.Code == 102)
            {
                switch (arg.EventName)
                {
                    case MQTTFileServerEventEnum.Event_File_Upload:
                        DeviceFileUpdownParam pm_up = JsonConvert.DeserializeObject<DeviceFileUpdownParam>(JsonConvert.SerializeObject(arg.Data));
                        if (!string.IsNullOrWhiteSpace(pm_up.FileName)) netFileUtil.TaskStartUploadFile(pm_up.IsLocal, pm_up.ServerEndpoint, pm_up.FileName);
                        break;
                    case MQTTFileServerEventEnum.Event_File_Download:
                        DeviceFileUpdownParam pm_dl = JsonConvert.DeserializeObject<DeviceFileUpdownParam>(JsonConvert.SerializeObject(arg.Data));
                        if (!string.IsNullOrWhiteSpace(pm_dl.FileName)) netFileUtil.TaskStartDownloadFile(pm_dl.IsLocal, pm_dl.ServerEndpoint, pm_dl.FileName, pm_dl.FileExtType);
                        break;
                    case MQTTFileServerEventEnum.Event_File_GetChannel:
                        DeviceGetChannelResult pm_dl_ch = JsonConvert.DeserializeObject<DeviceGetChannelResult>(JsonConvert.SerializeObject(arg.Data));
                        netFileUtil.TaskStartDownloadFile(pm_dl_ch);
                        break;
                }
            }
            else
            {
                switch (arg.EventName)
                {
                    case MQTTCameraEventEnum.Event_GetData:
                        int masterId = Convert.ToInt32(arg.Data.MasterId);
                        List<MeasureImgResultModel> resultMaster = null;
                        if (masterId > 0)
                        {
                            resultMaster = new List<MeasureImgResultModel>();
                            MeasureImgResultModel model = measureImgResultDao.GetById(masterId);
                            if (model != null)
                                resultMaster.Add(model);
                        }
                        else
                        {
                            resultMaster = measureImgResultDao.GetAllByBatchCode(arg.SerialNumber);
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
            //dialog.Filter = "files (*.csv) | *.csv";
            dialog.FileName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            dialog.RestoreDirectory = true;
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            dialog.FileName = dialog.FileName + ".csv";
            CsvWriter.WriteToCsv(ViewResultCameras[listView1.SelectedIndex], dialog.FileName);
        }



        private void Button_Click_Clear(object sender, RoutedEventArgs e)
        {
            ViewResultCameras.Clear();
        }


        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listView1.SelectedIndex > -1)
            {
                var data = ViewResultCameras[listView1.SelectedIndex];
                if (string.IsNullOrWhiteSpace(data.FileUrl)) return;

                if (File.Exists(data.FileUrl))
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            var fileInfo = new FileInfo(data.FileUrl);
                            log.Warn($"fileInfo.Length{fileInfo.Length}");
                            if (fileInfo.Length > 0)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    ImageView.OpenImage(data.FileUrl);
                                });
                            }
                        }
                        catch(Exception ex)
                        {
                            log.Warn("文件还在写入");
                            await Task.Delay(Device.Config.ViewImageReadDelay);
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                ImageView.OpenImage(data.FileUrl);
                            });
                        }


                    });

                }
                else
                {
                    if (data.ResultCode == 0 && data.FilePath != null)
                    {
                        string localName = netFileUtil.GetCacheFileFullName(data.FilePath);
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
                        if (string.IsNullOrEmpty(localName) || !File.Exists(localName))
                        {
                            ImageView.Config.FilePath = localName;
                            MsgRecord msgRecord = Device.DService.DownloadFile(data.FilePath, fileExt);
                        }
                        else
                        {
                            ImageView.Config.FilePath = localName;
                            ImageView.OpenImage(ImageView.Config.FilePath);
                        }
                    }
                }
            }
        }

        private void listView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && listView1.SelectedIndex > -1)
            {
                int temp = listView1.SelectedIndex;
                ViewResultCameras.RemoveAt(temp);
            }
        }
        public void OpenImage(CVCIEFile fileData)
        {
            ImageView.OpenImage(fileData.ToWriteableBitmap());
        }

        public void ShowResult(MeasureImgResultModel model)
        {
            ViewResultCamera result = new(model);
            ViewResultCameras.AddUnique(result);

            if (Config.AutoRefreshView)
            {
                if (listView1.Items.Count > 0) listView1.SelectedIndex = listView1.Items.Count - 1;
                listView1.ScrollIntoView(listView1.SelectedItem);
            }
        }


        MeasureImgResultDao MeasureImgResultDao = new();

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            ViewResultCameras.Clear();
            List<MeasureImgResultModel> algResults = MeasureImgResultDao.GetAll();
            foreach (var item in algResults)
            {
                ViewResultCamera CameraImgResult = new(item);
                ViewResultCameras.AddUnique(CameraImgResult);
            }
        }

        private void SearchAdvanced_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TextBoxId.Text) && string.IsNullOrEmpty(TextBoxBatch.Text) && string.IsNullOrEmpty(TextBoxFile.Text) && string.IsNullOrWhiteSpace(TbDeviceCode.Text) && SearchTimeSart.SelectedDateTime ==DateTime.MinValue)
            {
                ViewResultCameras.Clear();
                foreach (var item in MeasureImgResultDao.GetAll())
                {
                    ViewResultCamera algorithmResult = new(item);
                    ViewResultCameras.AddUnique(algorithmResult);
                }
                return;
            }
            else
            {
                ViewResultCameras.Clear();
                List<MeasureImgResultModel> algResults = MeasureImgResultDao.ConditionalQuery(TextBoxId.Text, TextBoxBatch.Text, TextBoxFile.Text, TbDeviceCode.Text, SearchTimeSart.DisplayDateTime,SearchTimeEnd.DisplayDateTime);
                foreach (var item in algResults)
                {
                    ViewResultCamera algorithmResult = new(item);
                    ViewResultCameras.AddUnique(algorithmResult);
                }

            }
        }

        private void Search1_Click(object sender, RoutedEventArgs e)
        {
            SearchTimeSart.SelectedDateTime = DateTime.MinValue;
            SearchTimeEnd.SelectedDateTime = DateTime.Now;

            SerchPopup.IsOpen = true;
            TextBoxId.Text = string.Empty;
            TextBoxBatch.Text = string.Empty;
            TextBoxFile.Text = string.Empty;
            TbDeviceCode.Text = string.Empty;
        }

        private void MenuItem_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is ViewResultCamera viewResult)
            {
                ViewResultCameras.Remove(viewResult);
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
                            ViewResultCameras.SortByID(item.IsSortD);
                        }
                        else if (Name == Properties.Resources.CreateTime)
                        {
                            item.IsSortD = !item.IsSortD;
                            ViewResultCameras.SortByCreateTime(item.IsSortD);
                        }
                        else if (Name == Properties.Resources.BatchNumber)
                        {
                            item.IsSortD = !item.IsSortD;
                            ViewResultCameras.SortByBatch(item.IsSortD);
                        }
                        else if (Name == Properties.Resources.File)
                        {
                            item.IsSortD = !item.IsSortD;
                            ViewResultCameras.SortByFilePath(item.IsSortD);
                        }
                    }
                }
            }
        }

        private void MenuItem_Export_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is ViewResultCamera viewCamera)
            {
                if (File.Exists(viewCamera.FileUrl))
                {
                    ExportCVCIE exportCamera = new(viewCamera.FileUrl) { Icon = Device.Icon };
                    exportCamera.Owner = Application.Current.GetActiveWindow();
                    exportCamera.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    exportCamera.ShowDialog();
                }
                else
                {
                    MessageBox1.Show(WindowHelpers.GetActiveWindow(), "找不到原始文件", "ColorVision");
                }
            }
        }

        private void MenuItem_ExportFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is ViewResultCamera viewCamera)
            {
                string FilePath = viewCamera.FileUrl;

                if (File.Exists(FilePath))
                {
                    if (CVFileUtil.IsCIEFile(FilePath))
                    {
                        int index = CVFileUtil.ReadCIEFileHeader(FilePath, out var meta);
                        if (index > 0)
                        {
                            if (!File.Exists(meta.srcFileName))
                                meta.srcFileName = Path.Combine(Path.GetDirectoryName(FilePath) ?? string.Empty, meta.srcFileName);
                        }

                        System.Windows.Forms.FolderBrowserDialog dialog = new();
                        dialog.UseDescriptionForTitle = true;
                        dialog.Description = "选择要保存到得位置";
                        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            if (string.IsNullOrEmpty(dialog.SelectedPath))
                            {
                                MessageBox.Show("文件夹路径不能为空", "提示");
                                return;
                            }
                            string savePath = dialog.SelectedPath;
                            // Copy the file to the new location
                            string newFilePath = Path.Combine(savePath, Path.GetFileName(FilePath));
                            File.Copy(FilePath, newFilePath, true);

                            // If srcFileName exists, copy it to the new location as well
                            if (File.Exists(meta.srcFileName))
                            {
                                string newSrcFilePath = Path.Combine(savePath, Path.GetFileName(meta.srcFileName));
                                File.Copy(meta.srcFileName, newSrcFilePath, true);
                            }
                        }

                    }
                    else
                    {
                        MessageBox1.Show(WindowHelpers.GetActiveWindow(), "目前支持CVRAW图像", "ColorVision");
                    }
                }
                else
                {
                    MessageBox1.Show(WindowHelpers.GetActiveWindow(), "找不到原始文件", "ColorVision");
                }
            }
        }
    }
}
