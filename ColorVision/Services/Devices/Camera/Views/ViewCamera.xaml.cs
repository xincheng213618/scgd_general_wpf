#pragma warning disable CS8604,CS8629
using ColorVision.Common.Sorts;
using ColorVision.Common.Utilities;
using ColorVision.Draw;
using ColorVision.Draw.Ruler;
using ColorVision.Media;
using ColorVision.Net;
using ColorVision.Services.Dao;
using ColorVision.Services.Export;
using ColorVision.Services.Msg;
using ColorVision.Services.Templates;
using ColorVision.Services.Templates.POI;
using ColorVision.Solution;
using MQTTMessageLib.Camera;
using MQTTMessageLib.FileServer;
using Newtonsoft.Json;
using Panuon.WPF.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Services.Devices.Camera.Views
{
    /// <summary>
    /// ViewCamera.xaml 的交互逻辑
    /// </summary>
    public partial class ViewCamera : UserControl, IView
    {
        public View View { get; set; }
        public ObservableCollection<ViewResultCamera> ViewResultCameras { get; set; } = new ObservableCollection<ViewResultCamera>();
        public MQTTCamera DeviceService{ get; set; }
        public DeviceCamera Device { get; set; }
        public ViewCamera(DeviceCamera device)
        {
            Device = device;
            this.DeviceService = device.DeviceService;
            InitializeComponent();
        }

        public ObservableCollection<TemplateModel<PoiParam>> ComboxPOITemplates { get; set; }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            View= new View();
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

            ComboxPOITemplates = new ObservableCollection<TemplateModel<PoiParam>>();
            ComboxPOITemplates.Insert(0, new TemplateModel<PoiParam>("Empty", new PoiParam() { Id=-1}));

            foreach (var item in TemplateControl.GetInstance().PoiParams)
                ComboxPOITemplates.Add(item);

            TemplateControl.GetInstance().PoiParams.CollectionChanged += (s, e) =>
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        if (e.NewItems != null)
                            foreach (TemplateModel<PoiParam> newItem in e.NewItems)
                                ComboxPOITemplates.Add(newItem);
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        if (e.OldItems != null)
                            foreach (TemplateModel<PoiParam> newItem in e.OldItems)
                                ComboxPOITemplates.Remove(newItem);
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        ComboxPOITemplates.Clear();
                        ComboxPOITemplates.Insert(0, new TemplateModel<PoiParam>("Empty", new PoiParam()) { Id = -1 });
                        break;
                }
            };
            ComboxPOITemplate.ItemsSource = ComboxPOITemplates;
            ComboxPOITemplate.SelectedIndex = 0;

            if (listView1.View is GridView gridView)
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);


            ComboBoxLayers.ItemsSource  =from e1 in Enum.GetValues(typeof(ImageLayer)).Cast<ImageLayer>()
                                         select new KeyValuePair<string, ImageLayer>(e1.ToString(), e1);
            netFileUtil = new NetFileUtil(SolutionManager.GetInstance().CurrentSolution.FullPath + "\\Cache");
            netFileUtil.handler += NetFileUtil_handler;
            DeviceService.OnMessageRecved += DeviceService_OnMessageRecved;
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
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(Application.Current.MainWindow, "文件打开失败", "ColorVision");
                });
            }
        }

        private MeasureImgResultDao measureImgResultDao = new MeasureImgResultDao();
        string LocalFileName;
        private void DeviceService_OnMessageRecved(object sender, MessageRecvArgs arg)
        {
            if (arg.ResultCode == 0)
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
            else if (arg.ResultCode == 102)
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
                            MessageBox.Show("文件下载失败");
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
                MessageBox.Show(Application.Current.MainWindow, "您需要先选择数据", "ColorVision");
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
                if (File.Exists(data.FileUrl))
                {
                    LocalFileName = data.FileUrl;
                    var FileData = netFileUtil.OpenLocalCVFile(data.FileUrl);
                    OpenImage(FileData);
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
                        if (string.IsNullOrEmpty(localName) || !System.IO.File.Exists(localName))
                        {
                            MsgRecord msgRecord = DeviceService.DownloadFile(data.FilePath, fileExt);
                        }
                        else
                        {
                            var FileData = netFileUtil.OpenLocalCVFile(localName, fileExt);
                            OpenImage(FileData);
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
            ImageView.OpenImage(fileData);
        }

        public void ShowResult(MeasureImgResultModel model)
        {
            ViewResultCamera result = new ViewResultCamera(model);
            ViewResultCameras.AddUnique(result);

            if (listView1.Items.Count > 0) listView1.SelectedIndex = listView1.Items.Count - 1;
            listView1.ScrollIntoView(listView1.SelectedItem);
        }


        MeasureImgResultDao MeasureImgResultDao = new MeasureImgResultDao();

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            ViewResultCameras.Clear();
            List<MeasureImgResultModel> algResults = MeasureImgResultDao.GetAll();
            foreach (var item in algResults)
            {
                ViewResultCamera CameraImgResult = new ViewResultCamera(item);
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
                    ViewResultCamera algorithmResult = new ViewResultCamera(item);
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
                    ViewResultCamera algorithmResult = new ViewResultCamera(item);
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

        private void Order_Click(object sender, RoutedEventArgs e)
        {
            OrderPopup.IsOpen = true;
        }

        private void Radio_Checked(object sender, RoutedEventArgs e)
        {
            if (RadioID?.IsChecked == true)
            {
                ViewResultCameras.SortByID(RadioUp?.IsChecked == false);
            }

            if (RadioBatch?.IsChecked == true)
            {
                ViewResultCameras.SortByBatch(RadioUp?.IsChecked == false);
            }

            if (RadioFilePath?.IsChecked == true)
            {
                ViewResultCameras.SortByFilePath(RadioUp?.IsChecked == false);
            }

            if (RadioCreateTime?.IsChecked == true)
            {
                ViewResultCameras.SortByCreateTime(RadioUp?.IsChecked == false);
            }

            OrderPopup.IsOpen = false;
        }


        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {
            if (sender is GridViewColumnHeader gridViewColumnHeader && gridViewColumnHeader.Content !=null)
            {
                foreach (var item in GridViewColumnVisibilitys)
                {
                    if (item.ColumnName.ToString() == gridViewColumnHeader.Content.ToString())
                    {
                        switch (item.ColumnName)
                        {
                            case "序号":
                                item.IsSortD = !item.IsSortD;
                                ViewResultCameras.SortByID(item.IsSortD);
                                break;
                            case "测量时间":
                                item.IsSortD = !item.IsSortD;
                                ViewResultCameras.SortByCreateTime(item.IsSortD);
                                break;
                            case "批次号":
                                item.IsSortD = !item.IsSortD;
                                ViewResultCameras.SortByBatch(item.IsSortD);
                                break;
                            case "图像数据文件":
                                item.IsSortD = !item.IsSortD;
                                ViewResultCameras.SortByFilePath(item.IsSortD);
                                break;
                            default:
                                break;
                        }
                        break;
                    }
                }
            }
        }


        private void ComboBoxLayers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
           if (sender is ComboBox comboBox && comboBox.SelectedValue is ImageLayer imageLayer)
            {
                if (listView1.SelectedIndex > -1)
                {
                    var ViewResultCamera = ViewResultCameras[listView1.SelectedIndex];
                    switch (imageLayer)
                    {
                        case ImageLayer.Src:
                            DeviceService.GetChannel(ViewResultCamera.Id, CVImageChannelType.SRC);
                            break;
                        case ImageLayer.R:
                            DeviceService.GetChannel(ViewResultCamera.Id, CVImageChannelType.RGB_R);
                            break;
                        case ImageLayer.G:
                            DeviceService.GetChannel(ViewResultCamera.Id, CVImageChannelType.RGB_G);
                            break;
                        case ImageLayer.B:
                            DeviceService.GetChannel(ViewResultCamera.Id, CVImageChannelType.RGB_B);
                            break;
                        case ImageLayer.X:
                            DeviceService.GetChannel(ViewResultCamera.Id, CVImageChannelType.CIE_XYZ_X);
                            break;
                        case ImageLayer.Y:
                            DeviceService.GetChannel(ViewResultCamera.Id, CVImageChannelType.CIE_XYZ_Y);
                            break;
                        case ImageLayer.Z:
                            DeviceService.GetChannel(ViewResultCamera.Id, CVImageChannelType.CIE_XYZ_Z);
                            break;
                        default:
                            break;
                    }

                }
                else
                {
                    MessageBox.Show("请先选择您要切换的图像");
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
                TemplateControl.GetInstance().LoadPoiDetailFromDB(poiParams);
                foreach (var item in poiParams.PoiPoints)
                {
                    switch (item.PointType)
                    {
                        case RiPointTypes.Circle:
                            DrawingVisualCircleWord Circle = new DrawingVisualCircleWord();
                            Circle.Attribute.Center = new Point(item.PixX, item.PixY);
                            Circle.Attribute.Radius = item.PixHeight/2;
                            Circle.Attribute.Brush = Brushes.Transparent;
                            Circle.Attribute.Pen = new Pen(Brushes.Red, item.PixWidth / 30);
                            Circle.Attribute.ID = item.ID;
                            Circle.Attribute.Text = item.Name;
                            Circle.Render();
                            ImageView.ImageShow.AddVisual(Circle);
                            break;
                        case RiPointTypes.Rect:
                            DrawingVisualRectangleWord Rectangle = new DrawingVisualRectangleWord();
                            Rectangle.Attribute.Rect = new Rect(item.PixX, item.PixY, item.PixWidth, item.PixHeight);
                            Rectangle.Attribute.Brush = Brushes.Transparent;
                            Rectangle.Attribute.Pen = new Pen(Brushes.Red, item.PixWidth / 30);
                            Rectangle.Attribute.ID = item.ID;
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


        private void MenuItem_Export_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is ViewResultCamera viewCamera)
            {
                if (File.Exists(viewCamera.FileUrl))
                {
                    ExportCVCIE exportCamera = new ExportCVCIE(viewCamera.FileUrl) { Icon = Device.Icon };
                    exportCamera.Owner = Application.Current.GetActiveWindow();
                    exportCamera.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    exportCamera.ShowDialog();
                }
                else
                {
                    MessageBox.Show(WindowHelpers.GetActiveWindow(), "找不到原始文件", "ColorVision");
                }
            }
        }

        private void CalculPOI_Click(object sender, RoutedEventArgs e)
        {
            Window window = new Window();
            window.Show();
        }
    }
}
