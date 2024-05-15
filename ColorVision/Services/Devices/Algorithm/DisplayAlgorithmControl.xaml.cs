#pragma warning disable CS8604,CS0168,CS8629,CA1822,CS8602
using ColorVision.Common.Utilities;
using ColorVision.Extension;
using ColorVision.MySql;
using ColorVision.Net;
using ColorVision.Services.Devices.Algorithm.Dao;
using ColorVision.Services.Devices.Algorithm.Templates;
using ColorVision.Services.Devices.Algorithm.Views;
using ColorVision.Services.Devices.Calibration;
using ColorVision.Services.Devices.Camera;
using ColorVision.Services.Msg;
using ColorVision.Services.Templates;
using ColorVision.Services.Templates.POI;
using ColorVision.Themes;
using ColorVision.UI;
using CVCommCore.CVAlgorithm;
using log4net;
using MQTTMessageLib.FileServer;
using Newtonsoft.Json;
using Panuon.WPF.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Services.Devices.Algorithm
{
    /// <summary>
    /// DisplayAlgorithmControl.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayAlgorithmControl : UserControl,IDisPlayControl
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(DisplayAlgorithmControl));

        public DeviceAlgorithm Device { get; set; }

        public MQTTAlgorithm Service { get => Device.MQTTService; }

        public AlgorithmView View { get => Device.View; }

        private IPendingHandler? handler { get; set; }

        private NetFileUtil netFileUtil;


        public DisplayAlgorithmControl(DeviceAlgorithm device)
        {
            Device = device;
            InitializeComponent();

            netFileUtil = new NetFileUtil();
            netFileUtil.handler += NetFileUtil_handler;
            Service.MsgReturnReceived += Service_OnAlgorithmEvent;
            View.OnCurSelectionChanged += View_OnCurSelectionChanged;
            PreviewMouseDown += UserControl_PreviewMouseDown;
        }
        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Parent is StackPanel stackPanel)
            {
                if (stackPanel.Tag is IDisPlayControl disPlayControl)
                    disPlayControl.IsSelected = false;
                stackPanel.Tag = this;
                IsSelected = true;
            }
        }

        private void NetFileUtil_handler(object sender, NetFileEvent arg)
        {
            if (arg.Code == 0 && arg.FileData.data != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    View.OpenImage(arg.FileData);
                });
                handler?.Close();
            }
            else
            {
                handler?.Close();
            }
        }

        private void View_OnCurSelectionChanged(AlgorithmResult data)
        {
            switch (data.ResultType)
            {
                case MQTTMessageLib.Algorithm.AlgorithmResultType.POI_XY_UV:
                case MQTTMessageLib.Algorithm.AlgorithmResultType.POI_Y:
                    doOpen(data.FilePath, FileExtType.CIE);
                    break;
                case MQTTMessageLib.Algorithm.AlgorithmResultType.SFR:
                case MQTTMessageLib.Algorithm.AlgorithmResultType.MTF:
                case MQTTMessageLib.Algorithm.AlgorithmResultType.FOV:
                case MQTTMessageLib.Algorithm.AlgorithmResultType.Distortion:
                    doOpenLocal(data.FilePath, FileExtType.Src);
                    break;
                case MQTTMessageLib.Algorithm.AlgorithmResultType.Ghost:
                    doOpenLocal(data.FilePath, FileExtType.Tif);
                    break;
                default:
                    break;
            }
        }

        private void doOpenLocal(string localName, FileExtType extType)
        {
            netFileUtil.OpenLocalFile(localName, extType);
        }

        private void DoShowFileList(DeviceListAllFilesParam data)
        {
            switch (data.FileExtType)
            {
                case FileExtType.Raw:
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        data.Files.Reverse();
                        CB_RawImageFiles.ItemsSource = data.Files;
                        CB_RawImageFiles.SelectedIndex = 0;
                    });
                    break;
                case FileExtType.Src:
                    break;
                case FileExtType.CIE:
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        data.Files.Reverse();
                        CB_CIEImageFiles.ItemsSource = data.Files;
                        CB_CIEImageFiles.SelectedIndex = 0;
                    });
                    break;
                case FileExtType.Calibration:
                    break;
                case FileExtType.Tif:
                    break;
                default:
                    break;
            }
        }

        private void Service_OnAlgorithmEvent(MsgReturn arg)
        {
            switch (arg.EventName)
            {
                case MQTTFileServerEventEnum.Event_File_List_All:
                    DeviceListAllFilesParam data = JsonConvert.DeserializeObject<DeviceListAllFilesParam>(JsonConvert.SerializeObject(arg.Data));
                    DoShowFileList(data);
                    break;
                case MQTTFileServerEventEnum.Event_File_Upload:
                    DeviceFileUpdownParam pm_up = JsonConvert.DeserializeObject<DeviceFileUpdownParam>(JsonConvert.SerializeObject(arg.Data));
                    FileUpload(pm_up);
                    break;
                case MQTTFileServerEventEnum.Event_File_Download:
                    DeviceFileUpdownParam pm_dl = JsonConvert.DeserializeObject<DeviceFileUpdownParam>(JsonConvert.SerializeObject(arg.Data));
                    if (pm_dl != null)
                    {
                        if (!string.IsNullOrWhiteSpace(pm_dl.FileName)) netFileUtil.TaskStartDownloadFile(pm_dl.IsLocal, pm_dl.ServerEndpoint, pm_dl.FileName, FileExtType.CIE);
                    }
                    break;
                default:
                    List<AlgResultMasterModel> resultMaster = null;
                    if (arg.Data.MasterId > 0)
                    {
                        resultMaster = new List<AlgResultMasterModel>();
                        int MasterId = arg.Data.MasterId;
                        AlgResultMasterModel model = AlgResultMasterDao.Instance.GetById(MasterId);
                        resultMaster.Add(model);
                    }
                    else
                    {
                        resultMaster = AlgResultMasterDao.Instance.GetAllByBatchCode(arg.SerialNumber);
                    }

                    foreach (AlgResultMasterModel result in resultMaster)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Device.View.AlgResultMasterModelDataDraw(result);
                        });
                    }
                    handler?.Close();
                    break;
            }
        }
        private void FileUpload(DeviceFileUpdownParam param)
        {
            if (!string.IsNullOrWhiteSpace(param.FileName)) netFileUtil.TaskStartUploadFile(param.IsLocal, param.ServerEndpoint, param.FileName);
        }
        private void FileDownload(DeviceFileUpdownParam param)
        {
            if (!string.IsNullOrWhiteSpace(param.FileName)) netFileUtil.TaskStartDownloadFile(param.IsLocal, param.ServerEndpoint, param.FileName, FileExtType.CIE);
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;
            ComboxPoiTemplate.ItemsSource = PoiParam.Params;
            ComboxPoiTemplate.SelectedIndex = 0;

            ComboxMTFTemplate.ItemsSource = MTFParam.MTFParams;
            ComboxMTFTemplate.SelectedIndex = 0;

            ComboxPoiTemplate2.ItemsSource = PoiParam.Params;
            ComboxPoiTemplate2.SelectedIndex = 0;

            ComboxSFRTemplate.ItemsSource = SFRParam.SFRParams;
            ComboxSFRTemplate.SelectedIndex = 0;

            ComboxGhostTemplate.ItemsSource = GhostParam.GhostParams;
            ComboxGhostTemplate.SelectedIndex = 0;

            ComboxFOVTemplate.ItemsSource = FOVParam.FOVParams;
            ComboxFOVTemplate.SelectedIndex = 0;

            ComboxDistortionTemplate.ItemsSource = DistortionParam.DistortionParams;
            ComboxDistortionTemplate.SelectedIndex = 0;

            ComboxLedCheckTemplate.ItemsSource = LedCheckParam.LedCheckParams;  
            ComboxLedCheckTemplate.SelectedIndex = 0;

            ComboxPoiTemplate1.ItemsSource = PoiParam.Params.CreateEmpty();
            ComboxPoiTemplate1.SelectedIndex = 0;

            ComboxFocusPointsTemplate.ItemsSource = FocusPointsParam.FocusPointsParams;
            ComboxFocusPointsTemplate.SelectedIndex = 0;

            ComboxBuildPoiTemplate.ItemsSource = BuildPOIParam.BuildPOIParams;
            ComboxBuildPoiTemplate.SelectedIndex = 0;

            this.AddViewConfig(View, ComboxView);

            SelectChanged += (s, e) =>
            {
                DisPlayBorder.BorderBrush = IsSelected ? ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#5649B0" : "#A79CF1") : ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#EAEAEA" : "#151515");
            };
            ThemeManager.Current.CurrentUIThemeChanged += (s) =>
            {
                DisPlayBorder.BorderBrush = IsSelected ? ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#5649B0" : "#A79CF1") : ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#EAEAEA" : "#151515");
            };
            ServiceManager.GetInstance().DeviceServices.CollectionChanged += (s, e) => GetImageDevices(); 
            GetImageDevices();
        }
        public class ImageDevice : ParamBase
        {
            public string DeviceCode { get; set; }
            public string DeviceType { get; set; }
        }

        public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }

        private void GetImageDevices()
        {
            ObservableCollection<TemplateModel<ImageDevice>> deves = new();
            foreach (var item in ServiceManager.GetInstance().DeviceServices)
            {
                if (item is DeviceCamera camera)
                {
                    TemplateModel<ImageDevice> model = new()
                    {
                        Value = new ImageDevice() { Name = item.Name, DeviceCode = item.Code, DeviceType = "Camera" },
                    };
                    deves.Add(model);
                }else if (item is DeviceCalibration cali)
                {
                    TemplateModel<ImageDevice> model = new()
                    {
                        Value = new ImageDevice() { Name = item.Name, DeviceCode = item.Code, DeviceType = "Calibration" },
                    };
                    deves.Add(model);
                }
            }
            CB_SourceImageFiles.ItemsSource = deves;
            CB_SourceImageFiles.SelectedIndex = 0;
        }
        private void PoiClick(object sender, RoutedEventArgs e)
        {
            if (ComboxPoiTemplate.SelectedIndex ==-1)
            {
                MessageBox.Show(Application.Current.MainWindow, "请先选择关注点模板", "ColorVision");
                return;
            }
            string sn = null;
            string imgFileName = CB_CIEImageFiles.Text;
            bool? isSN = BatchSelect.IsChecked;
            if (isSN.HasValue && isSN.Value) {
                if (string.IsNullOrWhiteSpace(BatchCode.Text))
                {
                    MessageBox.Show(Application.Current.MainWindow, "批次号不能为空，请先输入批次号", "ColorVision");
                    return;
                }
                sn = BatchCode.Text;
                imgFileName = "";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(imgFileName))
                {
                    MessageBox.Show(Application.Current.MainWindow, "图像文件不能为空，请先选择图像文件", "ColorVision");
                    return;
                }
            }
            var pm = PoiParam.Params[ComboxPoiTemplate.SelectedIndex].Value;
            TemplateModel<ImageDevice> imageDevice = (TemplateModel<ImageDevice>)CB_SourceImageFiles.SelectedItem;
            if (imageDevice != null) Service.POI(imageDevice.Value.DeviceCode, imageDevice.Value.DeviceType, imgFileName, pm.Id, ComboxPoiTemplate.Text, sn);
            else Service.POI(string.Empty, string.Empty, imgFileName, pm.Id, ComboxPoiTemplate.Text, sn);
            handler = PendingBox.Show(Application.Current.MainWindow, "", "计算关注点", true);
            handler.Cancelling += delegate
            {
                handler?.Close();
            };
        }

        private void Algorithm_INI(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var msg = Service.Init();
                ServicesHelper.SendCommand(button, msg);
            }
        }
        private void MTF_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxMTFTemplate.SelectedIndex==-1)
            {
                MessageBox.Show(Application.Current.MainWindow, "请先选择MTF模板", "ColorVision");
                return;
            }
            if (ComboxPoiTemplate2.SelectedIndex == -1)
            {
                MessageBox.Show(Application.Current.MainWindow, "请先选择关注点模板", "ColorVision");
                return;
            }
            string sn = string.Empty;
            string imgFileName = ImageFile.Text;
            FileExtType fileExtType = FileExtType.Tif;

            if (GetAlgSN(ref sn, ref imgFileName, ref fileExtType))
            {
                var pm = MTFParam.MTFParams[ComboxMTFTemplate.SelectedIndex].Value;
                var poi_pm = PoiParam.Params[ComboxPoiTemplate2.SelectedIndex].Value;
                TemplateModel<ImageDevice> imageDevice = (TemplateModel<ImageDevice>)CB_SourceImageFiles.SelectedItem;

                MsgRecord ss = null;
                if(imageDevice!=null) ss = Service.MTF(imageDevice.Value.DeviceCode, imageDevice.Value.DeviceType, imgFileName, fileExtType, pm.Id, ComboxMTFTemplate.Text, sn, poi_pm.Id, ComboxPoiTemplate2.Text);
                else ss = Service.MTF(string.Empty, string.Empty, imgFileName, fileExtType, pm.Id, ComboxMTFTemplate.Text, sn, poi_pm.Id, ComboxPoiTemplate2.Text);
                ServicesHelper.SendCommand(ss, "MTF");
            }
        }

        private void SFR_Clik(object sender, RoutedEventArgs e)
        {
            if (ComboxSFRTemplate.SelectedIndex == -1)
            {
                MessageBox.Show(Application.Current.MainWindow, "请先选择SFR模板", "ColorVision");
                return;
            }

            string sn = string.Empty;
            string imgFileName = ImageFile.Text;
            FileExtType fileExtType = FileExtType.Tif;

            if (GetAlgSN(ref sn, ref imgFileName, ref fileExtType))
            {
                var pm = SFRParam.SFRParams[ComboxSFRTemplate.SelectedIndex].Value;
                MsgRecord msg = null;
                TemplateModel<ImageDevice> imageDevice = (TemplateModel<ImageDevice>)CB_SourceImageFiles.SelectedItem;
                if (imageDevice != null) msg = Service.SFR(imageDevice.Value.DeviceCode, imageDevice.Value.DeviceType, imgFileName, fileExtType, pm.Id, ComboxSFRTemplate.Text, sn);
                else msg = Service.SFR(string.Empty, string.Empty, imgFileName, fileExtType, pm.Id, ComboxSFRTemplate.Text, sn);
                ServicesHelper.SendCommand(msg, "SFR");
            }
        }


        private void BuildPoi_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxBuildPoiTemplate.SelectedIndex == -1)
            {
                MessageBox.Show(Application.Current.MainWindow, "请先选择BuildPoi模板", "ColorVision");
                return;
            }

            string sn = string.Empty;
            string imgFileName = ImageFile.Text;
            FileExtType fileExtType = FileExtType.Tif;

            if (GetAlgSN(ref sn, ref imgFileName, ref fileExtType))
            {
                var pm = BuildPOIParam.BuildPOIParams[ComboxBuildPoiTemplate.SelectedIndex].Value;
                var Params = new Dictionary<string, object>();
                POIPointTypes POILayoutReq;
                if ((bool)CircleChecked.IsChecked)
                {
                    Params.Add("LayoutCenterX", centerX.Text);
                    Params.Add("LayoutCenterY", centerY.Text);
                    Params.Add("LayoutWidth", int.Parse(radius.Text) * 2);
                    Params.Add("LayoutHeight", int.Parse(radius.Text) * 2);
                    POILayoutReq = POIPointTypes.Circle;
                }
                else if ((bool)RectChecked.IsChecked)
                {
                    Params.Add("LayoutCenterX", rect_centerX.Text);
                    Params.Add("LayoutCenterY", rect_centerY.Text);
                    Params.Add("LayoutWidth", width.Text);
                    Params.Add("LayoutHeight", height.Text);
                    POILayoutReq = POIPointTypes.Rect;
                }
                else//四边形
                {
                    Params.Add("LayoutPolygonX1", Mask_X1.Text);
                    Params.Add("LayoutPolygonY1", Mask_Y1.Text);
                    Params.Add("LayoutPolygonX2", Mask_X2.Text);
                    Params.Add("LayoutPolygonY2", Mask_Y2.Text);
                    Params.Add("LayoutPolygonX3", Mask_X3.Text);
                    Params.Add("LayoutPolygonY3", Mask_Y3.Text);
                    Params.Add("LayoutPolygonX4", Mask_X4.Text);
                    Params.Add("LayoutPolygonY4", Mask_Y4.Text);
                    POILayoutReq = POIPointTypes.Mask;
                }
                TemplateModel<ImageDevice> imageDevice = (TemplateModel<ImageDevice>)CB_SourceImageFiles.SelectedItem;
                MsgRecord msg = null;
                if (imageDevice != null) msg = Service.BuildPoi(POILayoutReq, Params, imageDevice.Value.DeviceCode, imageDevice.Value.DeviceType, imgFileName, pm.Id, ComboxBuildPoiTemplate.Text, sn);
                else msg = Service.BuildPoi(POILayoutReq, Params, string.Empty, string.Empty, imgFileName, pm.Id, ComboxBuildPoiTemplate.Text, sn);
                ServicesHelper.SendCommand(msg, "BuildPoi");
            }
        }

        private void Ghost_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxGhostTemplate.SelectedIndex == -1)
            {
                MessageBox.Show(Application.Current.MainWindow, "请先选择Ghost模板", "ColorVision");
                return;
            }
            string sn = string.Empty;
            string imgFileName = ImageFile.Text;
            FileExtType fileExtType = FileExtType.Tif;

            if (GetAlgSN(ref sn, ref imgFileName, ref fileExtType))
            {
                var pm = GhostParam.GhostParams[ComboxGhostTemplate.SelectedIndex].Value;
                TemplateModel<ImageDevice> imageDevice = (TemplateModel<ImageDevice>)CB_SourceImageFiles.SelectedItem;
                MsgRecord msg = null;
                if (imageDevice != null) msg= Service.Ghost(imageDevice.Value.DeviceCode, imageDevice.Value.DeviceType, imgFileName, fileExtType, pm.Id, ComboxGhostTemplate.Text, sn);
                else msg = Service.Ghost(string.Empty, string.Empty, imgFileName, fileExtType, pm.Id, ComboxGhostTemplate.Text, sn);
                ServicesHelper.SendCommand(msg, "Ghost");
            }
        }

        private void Distortion_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxDistortionTemplate.SelectedIndex == -1)
            {
                MessageBox.Show(Application.Current.MainWindow, "请先选择Distortion模板", "ColorVision");
                return;
            }
            string sn = string.Empty;
            string imgFileName = ImageFile.Text;
            FileExtType fileExtType = FileExtType.Tif;

            if (GetAlgSN(ref sn, ref imgFileName, ref fileExtType))
            {
                var pm = DistortionParam.DistortionParams[ComboxDistortionTemplate.SelectedIndex].Value;
                TemplateModel<ImageDevice> imageDevice = (TemplateModel<ImageDevice>)CB_SourceImageFiles.SelectedItem;
                MsgRecord msg = null;
                if (imageDevice != null) msg= Service.Distortion(imageDevice.Value.DeviceCode, imageDevice.Value.DeviceType, imgFileName, fileExtType, pm.Id, ComboxDistortionTemplate.Text, sn);
                else msg = Service.Distortion(string.Empty, string.Empty, imgFileName, fileExtType, pm.Id, ComboxDistortionTemplate.Text, sn);
                ServicesHelper.SendCommand(msg, "Distortion");
            }
        }

        private bool GetAlgSN(ref string sn, ref string imgFileName,ref FileExtType fileExtType)
        {
            bool? isSN = AlgBatchSelect.IsChecked;
            bool? isRaw = AlgRawSelect.IsChecked;
            if (isSN.HasValue && isSN.Value)
            {
                if (string.IsNullOrWhiteSpace(AlgBatchCode.Text))
                {
                    MessageBox.Show(Application.Current.MainWindow, "批次号不能为空，请先输入批次号", "ColorVision");
                    return false;
                }
                sn = AlgBatchCode.Text;
                imgFileName = string.Empty;
            }
            else if (isRaw.HasValue && isRaw.Value) {
                imgFileName = CB_RawImageFiles.Text;
                fileExtType = FileExtType.Raw;
                sn = string.Empty;
            }
            else
            {
                imgFileName = ImageFile.Text;
                fileExtType = FileExtType.Tif;
                sn = string.Empty;
            }
            if (string.IsNullOrWhiteSpace(imgFileName))
            {
                MessageBox.Show(Application.Current.MainWindow, "图像文件不能为空，请先选择图像文件", "ColorVision");
                return false;
            }
            return true;
        }

        private void FOV_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxFOVTemplate.SelectedIndex == -1)
            {
                MessageBox.Show(Application.Current.MainWindow, "请先选择FOV模板", "ColorVision");
                return;
            }

            string sn = string.Empty;
            string imgFileName = ImageFile.Text;
            FileExtType fileExtType = FileExtType.Tif;

            if (GetAlgSN(ref sn, ref imgFileName, ref fileExtType))
            {
                var pm = FOVParam.FOVParams[ComboxFOVTemplate.SelectedIndex].Value;
                TemplateModel<ImageDevice> imageDevice = (TemplateModel<ImageDevice>)CB_SourceImageFiles.SelectedItem;
                MsgRecord msg = null;
                if (imageDevice != null) msg = Service.FOV(imageDevice.Value.DeviceCode, imageDevice.Value.DeviceType, imgFileName, fileExtType, pm.Id, ComboxFOVTemplate.Text, sn);
                else msg = Service.FOV(string.Empty, string.Empty, imgFileName, fileExtType, pm.Id, ComboxFOVTemplate.Text, sn);
                ServicesHelper.SendCommand(msg, "FOV");
            }
        }



        private void Open_File(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png,*.tif) | *.jpg; *.jpeg; *.png;*.tif";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ImageFile.Text = openFileDialog.FileName;
            }
        }

        private void Button_Click_Refresh(object sender, RoutedEventArgs e)
        {
            TemplateModel<ImageDevice> imageDevice = (TemplateModel<ImageDevice>)CB_SourceImageFiles.SelectedItem;
            if (imageDevice != null) Service.GetCIEFiles(imageDevice.Value.DeviceCode, imageDevice.Value.DeviceType);
            else Service.GetCIEFiles(string.Empty, string.Empty);
        }

        private void Button_Click_Upload(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "CVCIE files (*.cvcie) | *.cvcie";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Service.UploadCIEFile(openFileDialog.FileName);
                handler = PendingBox.Show(Application.Current.MainWindow, "", "上传", true);
                handler.Cancelling += delegate
                {
                    handler?.Close();
                };
            }
        }

        private void Button_Click_Open(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CB_CIEImageFiles.Text))
            {
                MessageBox.Show("请先选中图片");
                return;
            }
            handler = PendingBox.Show(Application.Current.MainWindow, "", "打开图片", true);
            handler.Cancelling += delegate
            {
                handler?.Close();
            };
            doOpen(CB_CIEImageFiles.Text, FileExtType.CIE);
        }

        private void doOpen(string fileName, FileExtType extType)
        {
            string localName = netFileUtil.GetCacheFileFullName(fileName);
            if (!System.IO.File.Exists(localName))
            {
                TemplateModel<ImageDevice> imageDevice = (TemplateModel<ImageDevice>)CB_SourceImageFiles.SelectedItem;
                if (imageDevice != null) Service.Open(imageDevice.Value.DeviceCode, imageDevice.Value.DeviceType, fileName, extType);
                else Service.Open(string.Empty, string.Empty, fileName, extType);
            }
            else
            {
                netFileUtil.OpenLocalFile(localName, extType);
            }
        }

        TemplateControl TemplateControl { get; set; }

        private void MenuItem_Template(object sender, RoutedEventArgs e)
        {
            if (sender is Control button)
            {
                TemplateControl= TemplateControl.GetInstance();
                if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
                {
                    MessageBox.Show(Application.Current.MainWindow, "数据库连接失败，请先连接数据库在操作", "ColorVision");
                    return;
                }
                switch (button.Tag?.ToString() ?? string.Empty)
                {
                    case "MTFParam":
                        new WindowTemplate(new TemplateMTFParam(), false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                        break;
                    case "SFRParam":
                        new WindowTemplate(new TemplateSFRParam(), false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                        break;
                    case "FOVParam":
                        new WindowTemplate(new TemplateFOVParam(), false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                        break;
                    case "GhostParam":
                        new WindowTemplate(new TemplateGhostParam(), false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                        break;
                    case "DistortionParam":
                        new WindowTemplate(new TemplateDistortionParam(), false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                        break;
                    case "LedCheckParam":
                        new WindowTemplate(new TemplateLedCheckParam(), false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                        break;
                    case "FocusPointsParam":
                        new WindowTemplate(new TemplateFocusPointsParam(), false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                        break;
                    case "FocusParm":
                        new WindowTemplate(new TemplatePOI(),false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
                        break;
                    case "BuildPOIParmam":
                        new WindowTemplate(new TemplateBuildPOIParam(), false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                        break;
                    default:
                        HandyControl.Controls.Growl.Info("开发中");
                        break;
                }
            }
        }

        private void Grid_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ToggleButton0.IsChecked = !ToggleButton0.IsChecked;
        }

        private void FocusPoints_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxFocusPointsTemplate.SelectedIndex == -1)
            {
                MessageBox.Show(Application.Current.MainWindow, "请先选择FocusPoints模板", "ColorVision");
                return;
            }

            string sn = string.Empty;
            string imgFileName = ImageFile.Text;
            FileExtType fileExtType = FileExtType.Tif;

            if (GetAlgSN(ref sn, ref imgFileName, ref fileExtType))
            {
                var pm = FocusPointsParam.FocusPointsParams[ComboxFocusPointsTemplate.SelectedIndex].Value;
                TemplateModel<ImageDevice> imageDevice = (TemplateModel<ImageDevice>)CB_SourceImageFiles.SelectedItem;
                MsgRecord ss = null;
                if (imageDevice != null) ss = Service.FocusPoints(imageDevice.Value.DeviceCode, imageDevice.Value.DeviceType, ImageFile.Text, fileExtType, pm.Id, ComboxFocusPointsTemplate.Text, sn);
                else ss = Service.FocusPoints(string.Empty, string.Empty, ImageFile.Text, fileExtType, pm.Id, ComboxFocusPointsTemplate.Text, sn);
                ServicesHelper.SendCommand(ss, "FocusPoints");
            }
        }

        private void LedCheck_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxLedCheckTemplate.SelectedIndex == -1)
            {
                MessageBox.Show(Application.Current.MainWindow, "请先选择灯珠检测模板", "ColorVision");
                return;
            }

            if (ComboxPoiTemplate1.SelectedIndex == -1)
            {
                MessageBox.Show(Application.Current.MainWindow, "请先选择关注点模板", "ColorVision");
                return;
            }

            string sn = string.Empty;
            string imgFileName = ImageFile.Text;
            FileExtType fileExtType = FileExtType.Tif;

            if (GetAlgSN(ref sn, ref imgFileName, ref fileExtType))
            {
                var pm = LedCheckParam.LedCheckParams[ComboxLedCheckTemplate.SelectedIndex].Value;
                var poi_pm = PoiParam.Params[ComboxPoiTemplate1.SelectedIndex].Value;
                TemplateModel<ImageDevice> imageDevice = (TemplateModel<ImageDevice>)CB_SourceImageFiles.SelectedItem;
                MsgRecord ss = null;
                if (imageDevice != null) ss = Service.LedCheck(imageDevice.Value.DeviceCode, imageDevice.Value.DeviceType, ImageFile.Text, fileExtType, pm.Id, ComboxLedCheckTemplate.Text, sn, poi_pm.Id, ComboxPoiTemplate1.Text);
                else ss = Service.LedCheck(string.Empty, string.Empty, ImageFile.Text, fileExtType, pm.Id, ComboxLedCheckTemplate.Text, sn, poi_pm.Id, ComboxPoiTemplate1.Text);
                ServicesHelper.SendCommand(ss, "正在计算灯珠");
            }
        }

        private void Button_Click_RawRefresh(object sender, RoutedEventArgs e)
        {
            TemplateModel<ImageDevice> imageDevice = (TemplateModel<ImageDevice>)CB_SourceImageFiles.SelectedItem;
            if (imageDevice != null) Service.GetRawFiles(imageDevice.Value.DeviceCode, imageDevice.Value.DeviceType);
            else Service.GetRawFiles(string.Empty, string.Empty);
        }

        private void Button_Click_RawOpen(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CB_RawImageFiles.Text))
            {
                MessageBox.Show("请先选中图片");
                return;
            }
            handler = PendingBox.Show(Application.Current.MainWindow, "", "打开图片", true);
            handler.Cancelling += delegate
            {
                handler?.Close();
            };
            doOpen(CB_RawImageFiles.Text, FileExtType.Raw);
        }

    }
}
