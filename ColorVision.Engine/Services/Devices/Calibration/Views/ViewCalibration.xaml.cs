#pragma warning disable CS8604,CS8629
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.Draw;
using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.Camera.Views;
using ColorVision.Engine.Services.Msg;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using ColorVision.Net;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using ColorVision.UI.Views;
using CVCommCore.CVImage;
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
using System.Globalization;


namespace ColorVision.Engine.Services.Devices.Calibration.Views
{

    public class ViewCalibrationConfig : ViewModelBase, IConfig
    {
        public static ViewCalibrationConfig Instance => ConfigHandler.GetInstance().GetRequiredService<ViewCalibrationConfig>();

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        public ImageViewConfig ImageViewConfig { get; set; } = new ImageViewConfig();

        public bool IsShowListView { get => _IsShowListView; set { _IsShowListView = value; NotifyPropertyChanged(); } }
        private bool _IsShowListView = true;
    }


    /// <summary>
    /// ViewCamera.xaml 的交互逻辑
    /// </summary>
    public partial class ViewCalibration : UserControl, IView
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(ViewCalibration));

        public View View { get; set; }

        public ObservableCollection<ViewResultCalibration> ViewResults { get; set; } = new ObservableCollection<ViewResultCalibration>();
        public  MQTTCalibration DeviceService => Device.DService;
        public DeviceCalibration Device { get; set; }

        public ViewCalibration(DeviceCalibration device)
        {
            Device = device;
            InitializeComponent();
        }
        public static ViewCameraConfig Config => ViewCameraConfig.Instance;
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = this;
            View = new View();
            ImageView.SetConfig(Config.ImageViewConfig);

            listView1.ItemsSource = ViewResults;

            ComboxPOITemplate.ItemsSource = PoiParam.Params.CreateEmpty();
            ComboxPOITemplate.SelectedIndex = 0;

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

        private MeasureImgResultDao measureImgResultDao = new();
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
                        LocalFileName = pm_dl.FileName;
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
                    case MQTTCalibrationEventEnum.Event_GetData:
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


        public void ShowResult(MeasureImgResultModel model)
        {
            ViewResultCalibration result = new(model);
            ViewResults.AddUnique(result);

            if (listView1.Items.Count > 0) listView1.SelectedIndex = listView1.Items.Count - 1;
            listView1.ScrollIntoView(listView1.SelectedItem);
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



        private void Button_Click_Clear(object sender, RoutedEventArgs e)
        {
            ViewResults.Clear();
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
                        ImageView.Config.FilePath = data.FilePath;

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
                            DeviceService.Open(data.FilePath, fileExt);
                        }
                        else
                        {
                            ImageView.Config.FilePath = localName;
                            ImageView.OpenImage(ImageView.Config.FilePath);
                        }
                    }
                }
                IsLayers = true;
                ComboBoxLayers.Text = "Src";
                IsLayers = false;
            }
        }
        private bool IsLayers;

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

        MeasureImgResultDao MeasureImgResultDao = new();

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            ViewResults.Clear();
            List<MeasureImgResultModel> algResults = MeasureImgResultDao.GetAll();
            foreach (var item in algResults)
            {
                ViewResultCalibration CameraImgResult = new(item);
                ViewResults.AddUnique(CameraImgResult);
            }
        }

        private void SearchAdvanced_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TextBoxId.Text) && string.IsNullOrEmpty(TextBoxBatch.Text) && string.IsNullOrEmpty(TextBoxFile.Text) && SearchTimeSart.SelectedDateTime ==DateTime.MinValue)
            {
                ViewResults.Clear();
                foreach (var item in MeasureImgResultDao.GetAllDevice(Device.Code))
                {
                    ViewResultCalibration algorithmResult = new(item);
                    ViewResults.AddUnique(algorithmResult);
                }
                return;
            }
            else
            {
                ViewResults.Clear();
                List<MeasureImgResultModel> algResults = MeasureImgResultDao.ConditionalQuery(TextBoxId.Text, TextBoxBatch.Text, TextBoxFile.Text, Device.Code, SearchTimeSart.DisplayDateTime,SearchTimeEnd.DisplayDateTime);
                foreach (var item in algResults)
                {
                    ViewResultCalibration algorithmResult = new(item);
                    ViewResults.AddUnique(algorithmResult);
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
        }

        private void MenuItem_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is ViewResultCalibration viewResult)
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

        private void POI_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxPOITemplate.SelectedValue is PoiParam poiParams)
            {
                if (poiParams.Id == -1)
                {
                    ImageView.ImageShow.Clear();
                    return;
                }
                ImageView.ImageShow.Clear();
                PoiParam.LoadPoiDetailFromDB(poiParams);
                foreach (var item in poiParams.PoiPoints)
                {
                    switch (item.PointType)
                    {
                        case RiPointTypes.Circle:
                            DVCircleText Circle = new();
                            Circle.Attribute.Center = new Point(item.PixX, item.PixY);
                            Circle.Attribute.Radius = item.PixWidth;
                            Circle.Attribute.Brush = Brushes.Transparent;
                            Circle.Attribute.Pen = new Pen(Brushes.Red, item.PixWidth / 30);
                            Circle.Attribute.Id = item.Id;
                            Circle.Attribute.Text = item.Name;
                            Circle.Render();
                            ImageView.ImageShow.AddVisual(Circle);
                            break;
                        case RiPointTypes.Rect:
                            DVRectangleText Rectangle = new();
                            Rectangle.Attribute.Rect = new Rect(item.PixX, item.PixY, item.PixWidth, item.PixHeight);
                            Rectangle.Attribute.Brush = Brushes.Transparent;
                            Rectangle.Attribute.Pen = new Pen(Brushes.Red, item.PixWidth / 30);
                            Rectangle.Attribute.Id = item.Id;
                            Rectangle.Attribute.Name = item.Name;
                            Rectangle.Render();
                            ImageView.ImageShow.AddVisual(Rectangle);
                            break;
                        case RiPointTypes.Mask:
                            break;
                    }
               }
            }
        }


        private void ComboBoxLayers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && e.AddedItems[0] is ComboBoxItem comboBoxItem && !IsLayers)
            {
                if (listView1.SelectedIndex > -1)
                {
                    var ViewResult = ViewResults[listView1.SelectedIndex];
                    string ext = Path.GetExtension(ViewResult.FileUrl).ToLower(CultureInfo.CurrentCulture);
                    FileExtType fileExtType = ext.Contains(".cvraw") ? FileExtType.Raw : ext.Contains(".cvsrc") ? FileExtType.Src : FileExtType.CIE;

                    if (comboBoxItem.Content.ToString() == "Src")
                        OpenImage(CVFileUtil.OpenLocalFileChannel(ViewResult.FileUrl, FileExtType.CIE, CVImageChannelType.SRC));
                    if (comboBoxItem.Content.ToString() == "R")
                        OpenImage(CVFileUtil.OpenLocalFileChannel(ViewResult.FileUrl, FileExtType.CIE, CVImageChannelType.RGB_R));
                    if (comboBoxItem.Content.ToString() == "G")
                        OpenImage(CVFileUtil.OpenLocalFileChannel(ViewResult.FileUrl, FileExtType.CIE, CVImageChannelType.RGB_G));
                    if (comboBoxItem.Content.ToString() == "B")
                        OpenImage(CVFileUtil.OpenLocalFileChannel(ViewResult.FileUrl, FileExtType.CIE, CVImageChannelType.RGB_B));
                    if (comboBoxItem.Content.ToString() == "X")
                        OpenImage(CVFileUtil.OpenLocalFileChannel(ViewResult.FileUrl, FileExtType.CIE, CVImageChannelType.CIE_XYZ_X));
                    if (comboBoxItem.Content.ToString() == "Y")
                        OpenImage(CVFileUtil.OpenLocalFileChannel(ViewResult.FileUrl, FileExtType.CIE, CVImageChannelType.CIE_XYZ_Y));
                    if (comboBoxItem.Content.ToString() == "Z")
                        OpenImage(CVFileUtil.OpenLocalFileChannel(ViewResult.FileUrl, FileExtType.CIE, CVImageChannelType.CIE_XYZ_Z));
                }
                else
                {
                    MessageBox1.Show("请先选择您要切换的图像");
                }
            }
        }

        private void ComboxPOITemplate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedValue is PoiParam poiParams)
            {
                if (poiParams.Id == -1)
                {
                    ImageView.ImageShow.Clear();
                    return;
                }
                ImageView.ImageShow.Clear();
                PoiParam.LoadPoiDetailFromDB(poiParams);
                foreach (var item in poiParams.PoiPoints)
                {
                    switch (item.PointType)
                    {
                        case RiPointTypes.Circle:
                            DVCircleText Circle = new();
                            Circle.Attribute.Center = new Point(item.PixX, item.PixY);
                            Circle.Attribute.Radius = item.PixHeight / 2;
                            Circle.Attribute.Brush = Brushes.Transparent;
                            Circle.Attribute.Pen = new Pen(Brushes.Red, item.PixWidth / 30);
                            Circle.Attribute.Id = item.Id;
                            Circle.Attribute.Text = item.Name;
                            Circle.Render();
                            ImageView.ImageShow.AddVisual(Circle);
                            break;
                        case RiPointTypes.Rect:
                            DVRectangleText Rectangle = new();
                            Rectangle.Attribute.Rect = new Rect(item.PixX, item.PixY, item.PixWidth, item.PixHeight);
                            Rectangle.Attribute.Brush = Brushes.Transparent;
                            Rectangle.Attribute.Pen = new Pen(Brushes.Red, item.PixWidth / 30);
                            Rectangle.Attribute.Id = item.Id;
                            Rectangle.Attribute.Name = item.Name;
                            Rectangle.Render();
                            ImageView.ImageShow.AddVisual(Rectangle);
                            break;
                        case RiPointTypes.Mask:
                            break;
                    }
                }
            }
        }
    }
}
