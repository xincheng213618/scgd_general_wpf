#pragma warning disable CS8604,CS0168,CS8629,CA1822,CS8602
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.BuildPoi;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.Distortion;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.FocusPoints;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.FOV;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.Ghost;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.LedCheck;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.LEDStripDetection;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.MTF;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.SFR;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Templates;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam;
using ColorVision.Engine.Services.Msg;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using ColorVision.Net;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using CVCommCore;
using CVCommCore.CVAlgorithm;
using log4net;
using MQTTMessageLib.FileServer;
using Newtonsoft.Json;
using Panuon.WPF.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    /// <summary>
    /// DisplayAlgorithmControl.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayAlgorithmControl : UserControl,IDisPlayControl
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(DisplayAlgorithmControl));

        public DeviceAlgorithm Device { get; set; }

        public MQTTAlgorithm Service { get => Device.DService; }

        public AlgorithmView View { get => Device.View; }
        public string DisPlayName => Device.Config.Name;

        private IPendingHandler? handler { get; set; }

        private NetFileUtil netFileUtil;

        public DisplayAlgorithmControl(DeviceAlgorithm device)
        {
            Device = device;
            InitializeComponent();

            netFileUtil = new NetFileUtil();
            netFileUtil.handler += NetFileUtil_handler;
        }

        private void NetFileUtil_handler(object sender, NetFileEvent arg)
        {
            if (arg.Code == 0 && arg.FileData.data != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    View.OpenImage(arg.FileData);
                });
            }

            handler?.Close();
        }


        private void Service_OnAlgorithmEvent(MsgReturn arg)
        {

            switch (arg.EventName)
            {
                case MQTTFileServerEventEnum.Event_File_List_All:
                    DeviceListAllFilesParam data = JsonConvert.DeserializeObject<DeviceListAllFilesParam>(JsonConvert.SerializeObject(arg.Data));
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
                    break;
                case MQTTFileServerEventEnum.Event_File_Upload:
                    DeviceFileUpdownParam pm_up = JsonConvert.DeserializeObject<DeviceFileUpdownParam>(JsonConvert.SerializeObject(arg.Data));
                    netFileUtil.TaskStartUploadFile(pm_up.IsLocal, pm_up.ServerEndpoint, pm_up.FileName);
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

            ComboxLEDStripDetectionTemplate.ItemsSource = LEDStripDetectionParam.Params;
            ComboxLEDStripDetectionTemplate.SelectedIndex = 0;

            this.AddViewConfig(View, ComboxView);
            this.ApplyChangedSelectedColor(DisPlayBorder);


            void UpdateCB_SourceImageFiles()
            {
                CB_SourceImageFiles.ItemsSource = ServiceManager.GetInstance().DeviceServices.Where(item => item is DeviceCamera || item is DeviceCalibration);
                CB_SourceImageFiles.SelectedIndex = 0;
            }
            ServiceManager.GetInstance().DeviceServices.CollectionChanged += (s, e) => UpdateCB_SourceImageFiles();

            UpdateCB_SourceImageFiles();
            Service.MsgReturnReceived += Service_OnAlgorithmEvent;



            void UpdateUI(DeviceStatusType status)
            {
                void SetVisibility(UIElement element, Visibility visibility){ if (element.Visibility != visibility) element.Visibility = visibility; };
                void HideAllButtons()
                {
                    SetVisibility(ButtonUnauthorized, Visibility.Collapsed);
                    SetVisibility(TextBlockUnknow, Visibility.Collapsed);
                    SetVisibility(StackPanelContent, Visibility.Collapsed);
                }
                // Default state
                HideAllButtons();

                switch (status)
                {
                    case DeviceStatusType.Unauthorized:
                        SetVisibility(ButtonUnauthorized, Visibility.Visible);
                        break;
                    case DeviceStatusType.Unknown:
                        SetVisibility(TextBlockUnknow, Visibility.Visible);
                        break;
                    default:
                        SetVisibility(StackPanelContent, Visibility.Visible);
                        break;
                }
            }
            UpdateUI(Device.DService.DeviceStatus);
            Device.DService.DeviceStatusChanged += UpdateUI;



        }
       public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }

        private bool IsTemplateSelected(ComboBox comboBox, string errorMessage)
        {
            if (comboBox.SelectedIndex == -1)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), errorMessage, "ColorVision");
                return false;
            }
            return true;
        }

        private void PoiClick(object sender, RoutedEventArgs e)
        {
            if (!IsTemplateSelected(ComboxPoiTemplate, "请先选择关注点模板")) return;

            if (GetAlgSN(out string sn, out string imgFileName, out FileExtType fileExtType))
            {
                string type = string.Empty;
                string code = string.Empty;
                if (CB_SourceImageFiles.SelectedItem is DeviceService deviceService)
                {
                    type = deviceService.ServiceTypes.ToString();
                    code = deviceService.Code;
                }
                var pm = PoiParam.Params[ComboxPoiTemplate.SelectedIndex].Value;
                Service.POI(code, type, imgFileName, pm.Id, ComboxPoiTemplate.Text, sn);
                handler = PendingBox.Show(Application.Current.MainWindow, "", "计算关注点", true);
                handler.Cancelling += delegate
                {
                    handler?.Close();
                };
            }
        }

        private void MTF_Click(object sender, RoutedEventArgs e)
        {
            if (!IsTemplateSelected(ComboxMTFTemplate, "请先选择MTF模板")) return;
            if (!IsTemplateSelected(ComboxPoiTemplate2, "请先选择关注点模板")) return;
            if (GetAlgSN(out string sn, out string imgFileName, out FileExtType fileExtType))
            {
                string type = string.Empty;
                string code = string.Empty;
                if (CB_SourceImageFiles.SelectedItem is DeviceService deviceService)
                {
                    type = deviceService.ServiceTypes.ToString();
                    code = deviceService.Code;
                }
                var pm = MTFParam.MTFParams[ComboxMTFTemplate.SelectedIndex].Value;
                var poi_pm = PoiParam.Params[ComboxPoiTemplate2.SelectedIndex].Value;
                var ss = Service.MTF(code, type, imgFileName, fileExtType, pm.Id, ComboxMTFTemplate.Text, sn, poi_pm.Id, ComboxPoiTemplate2.Text);
                ServicesHelper.SendCommand(ss, "MTF");
            }
        }

        private void SFR_Clik(object sender, RoutedEventArgs e)
        {
            if (!IsTemplateSelected(ComboxSFRTemplate, "请先选择SFR模板")) return;
            if (GetAlgSN(out string sn, out string imgFileName, out FileExtType fileExtType))
            {
                string type = string.Empty;
                string code = string.Empty;
                if (CB_SourceImageFiles.SelectedItem is DeviceService deviceService)
                {
                    type = deviceService.ServiceTypes.ToString();
                    code = deviceService.Code;
                }

                var pm = SFRParam.SFRParams[ComboxSFRTemplate.SelectedIndex].Value;
                MsgRecord msg  =Service.SFR(code, type, imgFileName, fileExtType, pm.Id, ComboxSFRTemplate.Text, sn);
                ServicesHelper.SendCommand(msg, "SFR");
            }
        }


        private void BuildPoi_Click(object sender, RoutedEventArgs e)
        {
            if (!IsTemplateSelected(ComboxBuildPoiTemplate, "请先选择BuildPoi模板")) return;

            if (GetAlgSN(out string sn, out string imgFileName, out FileExtType fileExtType))
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


                string type = string.Empty;
                string code = string.Empty;
                if (CB_SourceImageFiles.SelectedItem is DeviceService deviceService)
                {
                    type = deviceService.ServiceTypes.ToString();
                    code = deviceService.Code;
                }
                MsgRecord msg = Service.BuildPoi(POILayoutReq, Params, code, type, imgFileName, pm.Id, ComboxBuildPoiTemplate.Text, sn);
                ServicesHelper.SendCommand(msg, "BuildPoi");
            }
        }

        private void Ghost_Click(object sender, RoutedEventArgs e)
        {
            if (!IsTemplateSelected(ComboxGhostTemplate, "请先选择Ghost模板"))  return;
            if (GetAlgSN(out string sn, out string imgFileName, out FileExtType fileExtType))
            {
                var pm = GhostParam.GhostParams[ComboxGhostTemplate.SelectedIndex].Value;

                string type = string.Empty;
                string code = string.Empty;
                if (CB_SourceImageFiles.SelectedItem is DeviceService deviceService)
                {
                    type = deviceService.ServiceTypes.ToString();
                    code = deviceService.Code;
                }
                MsgRecord msg = Service.Ghost(code, type, imgFileName, fileExtType, pm.Id, ComboxGhostTemplate.Text, sn);
                ServicesHelper.SendCommand(msg, "Ghost");
            }
        }

        private void Distortion_Click(object sender, RoutedEventArgs e)
        {
            if (!IsTemplateSelected(ComboxDistortionTemplate, "请先选择Distortion模板")) return;
            if (GetAlgSN(out string sn, out string imgFileName, out FileExtType fileExtType))
            {
                var pm = DistortionParam.DistortionParams[ComboxDistortionTemplate.SelectedIndex].Value;

                string type = string.Empty;
                string code = string.Empty;
                if (CB_SourceImageFiles.SelectedItem is DeviceService deviceService)
                {
                    type = deviceService.ServiceTypes.ToString();
                    code = deviceService.Code;
                }
                MsgRecord msg = Service.Distortion(code, type, imgFileName, fileExtType, pm.Id, ComboxDistortionTemplate.Text, sn);
                ServicesHelper.SendCommand(msg, "Distortion");
            }
        }

        private bool GetAlgSN(out string sn, out string imgFileName, out FileExtType fileExtType)
        {
            sn = string.Empty;
            fileExtType = FileExtType.Tif;
            imgFileName = string.Empty;

            if (POITabItem.IsSelected)
            {
                if (BatchSelect.IsChecked ==true)
                {
                    sn = BatchCode.Text;
                    return true;
                }
                else
                {
                    imgFileName = CB_CIEImageFiles.Text;
                    fileExtType = FileExtType.CIE;
                    return true;
                }
            }
            else
            {
                bool? isSN = AlgBatchSelect.IsChecked;
                bool? isRaw = AlgRawSelect.IsChecked;

                if (isSN == true)
                {
                    if (string.IsNullOrWhiteSpace(AlgBatchCode.Text))
                    {
                        MessageBox1.Show(Application.Current.MainWindow, "批次号不能为空，请先输入批次号", "ColorVision");
                        return false;
                    }
                    sn = AlgBatchCode.Text;
                }
                else if (isRaw == true)
                {
                    imgFileName = CB_RawImageFiles.Text;
                    fileExtType = FileExtType.Raw;
                }
                else
                {
                    imgFileName = ImageFile.Text;
                }
                if (string.IsNullOrWhiteSpace(imgFileName))
                {
                    MessageBox1.Show(Application.Current.MainWindow, "图像文件不能为空，请先选择图像文件", "ColorVision");
                    return false;
                }
                return true;
            }
        }

        private void FOV_Click(object sender, RoutedEventArgs e)
        {
            if (!IsTemplateSelected(ComboxFOVTemplate, "请先选择FOV模板"))  return;

            if (GetAlgSN(out string sn, out string imgFileName, out FileExtType fileExtType))
            {
                var pm = FOVParam.FOVParams[ComboxFOVTemplate.SelectedIndex].Value;
                string type = string.Empty;
                string code = string.Empty;
                if (CB_SourceImageFiles.SelectedItem is DeviceService deviceService)
                {
                    type = deviceService.ServiceTypes.ToString();
                    code = deviceService.Code;
                }
                MsgRecord msg = Service.FOV(type, code, imgFileName, fileExtType, pm.Id, ComboxFOVTemplate.Text, sn);
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
            string type = string.Empty;
            string code = string.Empty;
            if (CB_SourceImageFiles.SelectedItem is DeviceService deviceService)
            {
                type = deviceService.ServiceTypes.ToString();
                code = deviceService.Code;
            }
            Service.GetCIEFiles(code, type);
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
                MessageBox1.Show("请先选中图片");
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
                string type = string.Empty;
                string code = string.Empty;
                if (CB_SourceImageFiles.SelectedItem is DeviceService deviceService)
                {
                    type = deviceService.ServiceTypes.ToString();
                    code = deviceService.Code;
                }
                Service.Open(code, type, fileName, extType);
            }
            else
            {
                netFileUtil.OpenLocalFile(localName, extType);
            }
        }


        private void MenuItem_Template(object sender, RoutedEventArgs e)
        {
            if (sender is Control button)
            {
                if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
                {
                    MessageBox1.Show(Application.Current.MainWindow, "数据库连接失败，请先连接数据库在操作", "ColorVision");
                    return;
                }
                switch (button.Tag?.ToString() ?? string.Empty)
                {
                    case "MTFParam":
                        new WindowTemplate(new TemplateMTFParam(), ComboxMTFTemplate.SelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                        break;
                    case "SFRParam":
                        new WindowTemplate(new TemplateSFRParam(), ComboxSFRTemplate.SelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                        break;
                    case "FOVParam":
                        new WindowTemplate(new TemplateFOVParam(), ComboxFOVTemplate.SelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                        break;
                    case "GhostParam":
                        new WindowTemplate(new TemplateGhostParam(), ComboxGhostTemplate.SelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                        break;
                    case "DistortionParam":
                        new WindowTemplate(new TemplateDistortionParam(),ComboxDistortionTemplate.SelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                        break;
                    case "LedCheckParam":
                        new WindowTemplate(new TemplateLedCheckParam(), ComboxLedCheckTemplate.SelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                        break;
                    case "FocusPointsParam":
                        new WindowTemplate(new TemplateFocusPointsParam(),ComboxFocusPointsTemplate.SelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                        break;
                    case "FocusParm":
                        new WindowTemplate(new TemplatePOI()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
                        break;
                    case "BuildPOIParmam":
                        new WindowTemplate(new TemplateBuildPOIParam(),ComboxBuildPoiTemplate.SelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                        break;
                    case "LEDStripDetection":
                        new WindowTemplate(new TemplateLEDStripDetectionParam(), ComboxLEDStripDetectionTemplate.SelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
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
            if (!IsTemplateSelected(ComboxFocusPointsTemplate, "请先选择FocusPoints模板")) return;

            if (GetAlgSN(out string sn, out string imgFileName, out FileExtType fileExtType))
            {
                var pm = FocusPointsParam.FocusPointsParams[ComboxFocusPointsTemplate.SelectedIndex].Value;

                string type = string.Empty;
                string code = string.Empty;
                if (CB_SourceImageFiles.SelectedItem is DeviceService deviceService)
                {
                    type = deviceService.ServiceTypes.ToString();
                    code = deviceService.Code;
                }
                MsgRecord msg = Service.FocusPoints(code, type, imgFileName, fileExtType, pm.Id, ComboxFocusPointsTemplate.Text, sn);
                ServicesHelper.SendCommand(msg, "FocusPoints");
            }
        }

        private void LedCheck_Click(object sender, RoutedEventArgs e)
        {
            if (!IsTemplateSelected(ComboxLedCheckTemplate, "请先选择灯珠检测模板")) return;
            if (!IsTemplateSelected(ComboxPoiTemplate1, "请先选择关注点模板")) return;

            if (GetAlgSN(out string sn, out string imgFileName, out FileExtType fileExtType))
            {
                var pm = LedCheckParam.LedCheckParams[ComboxLedCheckTemplate.SelectedIndex].Value;
                var poi_pm = PoiParam.Params[ComboxPoiTemplate1.SelectedIndex].Value;

                string type = string.Empty;
                string code = string.Empty;
                if (CB_SourceImageFiles.SelectedItem is DeviceService deviceService)
                {
                    type = deviceService.ServiceTypes.ToString();
                    code = deviceService.Code;
                }
                MsgRecord ss = Service.LedCheck(code, type, imgFileName, fileExtType, pm.Id, ComboxLedCheckTemplate.Text, sn, poi_pm.Id, ComboxPoiTemplate1.Text);
                ServicesHelper.SendCommand(ss, "正在计算灯珠");
            }
        }

        private void Button_Click_RawRefresh(object sender, RoutedEventArgs e)
        {
            string type = string.Empty;
            string code = string.Empty;
            if (CB_SourceImageFiles.SelectedItem is DeviceService deviceService)
            {
                type = deviceService.ServiceTypes.ToString();
                code = deviceService.Code;
            }
            Service.GetRawFiles(code, type);
        }

        private void Button_Click_RawOpen(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CB_RawImageFiles.Text))
            {
                MessageBox1.Show("请先选中图片");
                return;
            }
            handler = PendingBox.Show(Application.Current.MainWindow, "", "打开图片", true);
            handler.Cancelling += delegate
            {
                handler?.Close();
            };
            doOpen(CB_RawImageFiles.Text, FileExtType.Raw);
        }

        private void LEDStripDetection_Click(object sender, RoutedEventArgs e)
        {
            if (!IsTemplateSelected(ComboxLEDStripDetectionTemplate, "请先选择灯带检测模板"))  return;
            if (GetAlgSN(out string sn, out string imgFileName, out FileExtType fileExtType))
            {
                var pm = LEDStripDetectionParam.Params[ComboxLEDStripDetectionTemplate.SelectedIndex].Value;

                string type = string.Empty;
                string code = string.Empty;
                if (CB_SourceImageFiles.SelectedItem is DeviceService deviceService)
                {
                    type = deviceService.ServiceTypes.ToString();
                    code = deviceService.Code;
                }
                MsgRecord ss = Service.LEDStripDetection(code, type, imgFileName, fileExtType, pm.Id, ComboxLedCheckTemplate.Text, sn);
                ServicesHelper.SendCommand(ss, "正在计算灯带检测");
            }
        }
    }
}
