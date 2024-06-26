﻿#pragma warning disable CS8604,CS8629
using ColorVision.Common.Utilities;
using ColorVision.Draw;
using ColorVision.Draw.Ruler;
using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Services.Msg;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.UIExport.SolutionExports.Export;
using ColorVision.Net;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using ColorVision.UI.Views;
using cvColorVision;
using CVCommCore.CVAlgorithm;
using CVCommCore.CVImage;
using log4net;
using MQTTMessageLib.Camera;
using MQTTMessageLib.FileServer;
using Newtonsoft.Json;
using Panuon.WPF.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Engine.Services.Devices.Camera.Views
{
    public class ViewCameraConfig : IConfig
    {
        public static ViewCameraConfig Instance => ConfigHandler.GetInstance().GetRequiredService<ViewCameraConfig>();

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();
    }


    /// <summary>
    /// ViewCamera.xaml 的交互逻辑
    /// </summary>
    public partial class ViewCamera : UserControl, IView
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(App));


        public View View { get; set; }
        public ObservableCollection<ViewResultCamera> ViewResultCameras { get; set; } = new ObservableCollection<ViewResultCamera>();
        public MQTTCamera DeviceService{ get; set; }
        public DeviceCamera Device { get; set; }
        public ViewCamera(DeviceCamera device)
        {
            Device = device;
            DeviceService = device.DeviceService;
            InitializeComponent();
        }

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


            ComboxPOITemplate.ItemsSource = PoiParam.Params.CreateEmpty();
            ComboxPOITemplate.SelectedIndex = 0;

            if (listView1.View is GridView gridView)
            {
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
                ViewCameraConfig.Instance.GridViewColumnVisibilitys.CopyToGridView(GridViewColumnVisibilitys);
                ViewCameraConfig.Instance.GridViewColumnVisibilitys = GridViewColumnVisibilitys;
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
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(Application.Current.MainWindow, "文件打开失败", "ColorVision");
                });
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
                    ImageView.FilePath = LocalFileName;
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
                        if (string.IsNullOrEmpty(localName) || !File.Exists(localName))
                        {
                            ImageView.FilePath = localName;
                            MsgRecord msgRecord = DeviceService.DownloadFile(data.FilePath, fileExt);
                        }
                        else
                        {
                            ImageView.FilePath = localName;
                            var FileData = netFileUtil.OpenLocalCVFile(localName, fileExt);
                            OpenImage(FileData);
                        }
                    }
                }

                ComboBoxLayers.Text = "Src";
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
            ViewResultCamera result = new(model);
            ViewResultCameras.AddUnique(result);

            if (listView1.Items.Count > 0) listView1.SelectedIndex = listView1.Items.Count - 1;
            listView1.ScrollIntoView(listView1.SelectedItem);
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


        private void ComboBoxLayers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
           if (sender is ComboBox comboBox )
            {
                if (listView1.SelectedIndex > -1)
                {
                    var ViewResultCamera = ViewResultCameras[listView1.SelectedIndex];
                    if (comboBox.Text == "Src")
                        DeviceService.GetChannel(ViewResultCamera.Id, CVImageChannelType.SRC);
                    if (comboBox.Text == "R")
                        DeviceService.GetChannel(ViewResultCamera.Id, CVImageChannelType.RGB_R);
                    if (comboBox.Text == "G")
                        DeviceService.GetChannel(ViewResultCamera.Id, CVImageChannelType.RGB_G);
                    if (comboBox.Text == "B")
                        DeviceService.GetChannel(ViewResultCamera.Id, CVImageChannelType.RGB_B);
                    if (comboBox.Text == "X")
                        DeviceService.GetChannel(ViewResultCamera.Id, CVImageChannelType.CIE_XYZ_X);
                    if (comboBox.Text == "Y")
                        DeviceService.GetChannel(ViewResultCamera.Id, CVImageChannelType.CIE_XYZ_Y);
                    if (comboBox.Text == "Z")
                        DeviceService.GetChannel(ViewResultCamera.Id, CVImageChannelType.CIE_XYZ_Z);
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
                PoiParam.LoadPoiDetailFromDB(poiParams);
                foreach (var item in poiParams.PoiPoints)
                {
                    switch (item.PointType)
                    {
                        case RiPointTypes.Circle:
                            DrawingVisualCircleWord Circle = new();
                            Circle.Attribute.Center = new Point(item.PixX, item.PixY);
                            Circle.Attribute.Radius = item.PixHeight/2;
                            Circle.Attribute.Brush = Brushes.Transparent;
                            Circle.Attribute.Pen = new Pen(Brushes.Red, item.PixWidth / 30);
                            Circle.Attribute.ID = item.Id;
                            Circle.Attribute.Text = item.Name;
                            Circle.Render();
                            ImageView.ImageShow.AddVisual(Circle);
                            break;
                        case RiPointTypes.Rect:
                            DrawingVisualRectangleWord Rectangle = new();
                            Rectangle.Attribute.Rect = new Rect(item.PixX, item.PixY, item.PixWidth, item.PixHeight);
                            Rectangle.Attribute.Brush = Brushes.Transparent;
                            Rectangle.Attribute.Pen = new Pen(Brushes.Red, item.PixWidth / 30);
                            Rectangle.Attribute.ID = item.Id;
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
                    ExportCVCIE exportCamera = new(viewCamera.FileUrl) { Icon = Device.Icon };
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
            if (!ImageView.IsCVCIE)
            {
                MessageBox.Show("仅对CVCIE图像支持");
                return;
            }
            if (ComboxPOITemplate.SelectedValue is not PoiParam poiParams)
            {
                MessageBox.Show("需要配置关注点");
                return;
            }

            if (poiParams.Id == -1)
            {
                ImageView.ImageShow.Clear();
                return;
            }
            PoiParam.LoadPoiDetailFromDB(poiParams);

            ObservableCollection<PoiResultCIExyuvData> PoiResultCIExyuvDatas = new ObservableCollection<PoiResultCIExyuvData>();
            int result = ConvertXYZ.CM_SetFilter(ConvertXYZ.Handle, poiParams.DatumArea.Filter.Enable , poiParams.DatumArea.Filter.Threshold);
            log.Info($"CM_SetFilter: {result}");
            result = ConvertXYZ.CM_SetFilterNoArea(ConvertXYZ.Handle, poiParams.DatumArea.Filter.NoAreaEnable, poiParams.DatumArea.Filter.Threshold);
            result = ConvertXYZ.CM_SetFilterXYZ(ConvertXYZ.Handle, poiParams.DatumArea.Filter.XYZEnable, (int)poiParams.DatumArea.Filter.XYZType, poiParams.DatumArea.Filter.Threshold);

            foreach (var item in poiParams.PoiPoints)
            {
                POIPoint pOIPoint = new POIPoint() { Id = item.Id, Name = item.Name, PixelX = (int)item.PixX, PixelY = (int)item.PixY, PointType = (POIPointTypes)item.PointType, Height = (int)item.PixHeight, Width = (int)item.PixWidth };
                var sss = GetCVCIE(pOIPoint);
                PoiResultCIExyuvDatas.Add(sss);
            }
            WindowCVCIE windowCIE = new WindowCVCIE(PoiResultCIExyuvDatas) { Owner = Application.Current.GetActiveWindow() };
            windowCIE.Show();
        }

        public static PoiResultCIExyuvData GetCVCIE(POIPoint pOIPoint)
        {
            int x = pOIPoint.PixelX; int y = pOIPoint.PixelY; int rect = pOIPoint.Width; int rect2 = pOIPoint.Height;
            PoiResultCIExyuvData poiResultCIExyuvData = new PoiResultCIExyuvData();
            poiResultCIExyuvData.Point = pOIPoint;
            float dXVal = 0;
            float dYVal = 0;
            float dZVal = 0;
            float dx = 0;
            float dy = 0;
            float du = 0;
            float dv = 0;
            float CCT = 0;
            float Wave = 0;

            _ = pOIPoint.PointType switch
            {
                POIPointTypes.SolidPoint => ConvertXYZ.CM_GetXYZxyuvCircle(ConvertXYZ.Handle, x, y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, 1),
                POIPointTypes.Rect => ConvertXYZ.CM_GetXYZxyuvRect(ConvertXYZ.Handle, x, y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, rect, rect2),
                POIPointTypes.None or POIPointTypes.Circle or POIPointTypes.Mask or _ => ConvertXYZ.CM_GetXYZxyuvCircle(ConvertXYZ.Handle, x, y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv,(int)(rect/2)),
            };
            poiResultCIExyuvData.u = du;
            poiResultCIExyuvData.v = dv;
            poiResultCIExyuvData.x = dx;
            poiResultCIExyuvData.y = dy;
            poiResultCIExyuvData.X = dXVal;
            poiResultCIExyuvData.Y = dYVal;
            poiResultCIExyuvData.Z = dZVal;
            poiResultCIExyuvData.CCT = CCT;
            poiResultCIExyuvData.Wave = Wave;
            return poiResultCIExyuvData;
        }

        private void CalculMTF_Click(object sender, RoutedEventArgs e)
        {
            if (!ImageView.IsCVCIE)
            {
                MessageBox.Show("仅对CVCIE图像支持");
                return;
            }
            if (ComboxPOITemplate.SelectedValue is not PoiParam poiParams)
            {
                MessageBox.Show("需要配置关注点");
                return;
            }




        }
    }
}
