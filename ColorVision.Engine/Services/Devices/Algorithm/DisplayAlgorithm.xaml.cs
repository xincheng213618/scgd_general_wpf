#pragma warning disable CS8604,CS0168,CS8629,CA1822,CS8602
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.BuildPoi;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.Distortion;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.FocusPoints;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.Ghost;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.LedCheck;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.LedCheck2;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.LEDStripDetection;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.POIRevise;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.PoiOutput;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.SFR;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates;
using ColorVision.Engine.Services.Msg;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.POIFilters;
using ColorVision.Net;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using CVCommCore;
using CVCommCore.CVAlgorithm;
using log4net;
using MQTTMessageLib.FileServer;
using Panuon.WPF.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Algorithm
{

    /// <summary>
    /// DisplayAlgorithm.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayAlgorithm : UserControl,IDisPlayControl
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(DisplayAlgorithm));

        public DeviceAlgorithm Device { get; set; }

        public MQTTAlgorithm Service { get => Device.DService; }

        public AlgorithmView View { get => Device.View; }
        public string DisPlayName => Device.Config.Name;
        private IPendingHandler? handler { get; set; }
        private NetFileUtil netFileUtil;

        public DisplayAlgorithm(DeviceAlgorithm device)
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

        
        public ObservableCollection<IAlgorithm> Algorithms { get; set; } = new ObservableCollection<IAlgorithm>();
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(IAlgorithm).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type, Device) is IAlgorithm  algorithm)
                    {
                        Algorithms.Add(algorithm);
                    }
                }
            }
            CB_Algorithms.ItemsSource = Algorithms;
            CB_Algorithms.SelectionChanged += (s, e) =>
            {
                if (CB_Algorithms.SelectedItem is IAlgorithm algorithm)
                {
                    CB_StackPanel.Children.Clear();
                    CB_StackPanel.Children.Add(algorithm.GetUserControl());
                }
            };


            DataContext = Device;
            ComboxPoiTemplate.ItemsSource = PoiParam.Params;
            ComboxPoiTemplate.SelectedIndex = 0;

            ComboxGhostTemplate.ItemsSource = TemplateGhostParam.Params;
            ComboxGhostTemplate.SelectedIndex = 0;

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

            ComboxLEDStripDetectionTemplate.ItemsSource = TemplateLEDStripDetectionParam.Params;
            ComboxLEDStripDetectionTemplate.SelectedIndex = 0;
            ComboxPoiFilter.ItemsSource = TemplatePOIFilterParam.Params.CreateEmpty();
            ComboxPoiFilter.SelectedIndex = 0;

            ComboxPoiOutput.ItemsSource = TemplatePoiOutputParam.Params.CreateEmpty();
            ComboxPoiOutput.SelectedIndex = 0;

            ComboxPoiCal.ItemsSource = TemplatePoiReviseParam.Params.CreateEmpty();
            ComboxPoiCal.SelectedIndex = 0;


            ComboxLedCheck2Template.ItemsSource = TemplateThirdParty.Params.GetValue("LedCheck2");
            ComboxLedCheck2Template.SelectedIndex = 0;

            ComboxPoiTemplate3.ItemsSource = PoiParam.Params.CreateEmpty(); ;
            ComboxPoiTemplate3.SelectedIndex = 0;

            ComboxCVOLEDCOLOR.ItemsSource = from e1 in Enum.GetValues(typeof(CVOLEDCOLOR)).Cast<CVOLEDCOLOR>()
                                            select new KeyValuePair<string, CVOLEDCOLOR>(e1.ToString(), e1);
            ComboxCVOLEDCOLOR.SelectedIndex = 0;



            this.AddViewConfig(View, ComboxView);
            this.ApplyChangedSelectedColor(DisPlayBorder);


            void UpdateCB_SourceImageFiles()
            {
                CB_SourceImageFiles.ItemsSource = ServiceManager.GetInstance().DeviceServices.Where(item => item is DeviceCamera || item is DeviceCalibration);
                CB_SourceImageFiles.SelectedIndex = 0;
            }
            ServiceManager.GetInstance().DeviceServices.CollectionChanged += (s, e) => UpdateCB_SourceImageFiles();
            UpdateCB_SourceImageFiles();
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
            if (!IsTemplateSelected(ComboxPoiFilter, "需要选择关注点过滤模板")) return;

            if (ComboxPoiTemplate.SelectedValue is not PoiParam poiParam) return;
            if (ComboxPoiFilter.SelectedValue is not POIFilterParam pOIFilterParam) return;
            if (ComboxPoiCal.SelectedValue is not PoiReviseParam pOICalParam) return;
            if (ComboxPoiOutput.SelectedValue is not PoiOutputParam poiOutputParam) return;


            if (!GetAlgSN(out string sn, out string imgFileName, out FileExtType fileExtType)) return;

            string type = string.Empty;
            string code = string.Empty;
            if (CB_SourceImageFiles.SelectedItem is DeviceService deviceService)
            {
                type = deviceService.ServiceTypes.ToString();
                code = deviceService.Code;
            }
            Service.POI(code, type, imgFileName, poiParam, pOIFilterParam, pOICalParam, poiOutputParam, sn);
            handler = PendingBox.Show(Application.Current.MainWindow, "", "计算关注点", true);
            handler.Cancelling += delegate
            {
                handler?.Close();
            };
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
                var pm = TemplateGhostParam.Params[ComboxGhostTemplate.SelectedIndex].Value;

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
                bool? isSN = AlgBatchSelect.IsSelected;
                bool? isRaw = AlgRawSelect.IsSelected;

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
            string type = string.Empty;
            string code = string.Empty;
            if (CB_SourceImageFiles.SelectedItem is DeviceService deviceService)
            {
                type = deviceService.ServiceTypes.ToString();
                code = deviceService.Code;
            }
            Service.Open(code, type, fileName, extType);
        }


        private void MenuItem_Template(object sender, RoutedEventArgs e)
        {
            if (sender is Control button)
            {
                switch (button.Tag?.ToString() ?? string.Empty)
                {
                    case "GhostParam":
                        new WindowTemplate(new TemplateGhostParam(), ComboxGhostTemplate.SelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                        break;
                    case "DistortionParam":
                        new WindowTemplate(new TemplateDistortionParam(),ComboxDistortionTemplate.SelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                        break;
                    case "LedCheckParam":
                        new WindowTemplate(new TemplateLedCheckParam(), ComboxLedCheckTemplate.SelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                        break;
                    case "LedCheck2Param":
                        new WindowTemplate(new TemplateThirdParty("LedCheck2"), ComboxLedCheck2Template.SelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
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
                    case "POIFilter":
                        new WindowTemplate(new TemplatePOIFilterParam(), ComboxPoiFilter.SelectedIndex-1) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                        break;
                    case "PoiRevise":
                        new WindowTemplate(new TemplatePoiReviseParam(), ComboxPoiCal.SelectedIndex -1) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                        break;
                    case "PoiOutput":
                        new WindowTemplate(new TemplatePoiOutputParam(), ComboxPoiOutput.SelectedIndex - 1) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                        break;
                    default:
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

            if (ComboxPoiTemplate1.SelectedValue is not PoiParam poiParam) return;
            if (ComboxLedCheckTemplate.SelectedValue is not LedCheckParam ledCheckParam) return;

            if (GetAlgSN(out string sn, out string imgFileName, out FileExtType fileExtType))
            {
                string type = string.Empty;
                string code = string.Empty;
                if (CB_SourceImageFiles.SelectedItem is DeviceService deviceService)
                {
                    type = deviceService.ServiceTypes.ToString();
                    code = deviceService.Code;
                    MsgRecord ss = Service.LedCheck(code, type, imgFileName, fileExtType, sn, ledCheckParam, poiParam);
                    ServicesHelper.SendCommand(ss, "正在计算灯珠");
                }
            }
        }


        private void LedCheck2_Click(object sender, RoutedEventArgs e)
        {
            if (!IsTemplateSelected(ComboxLedCheck2Template, "请先选择灯珠检测模板")) return;
            if (!IsTemplateSelected(ComboxPoiTemplate3, "请先选择关注点模板")) return;

            if (ComboxLedCheck2Template.SelectedValue is not ModThirdPartyParam ledCheck2Param) return;
            if (ComboxCVOLEDCOLOR.SelectedValue is not CVOLEDCOLOR color) return;
            if (ComboxPoiTemplate3.SelectedValue is not PoiParam poiParam) return;



            if (GetAlgSN(out string sn, out string imgFileName, out FileExtType fileExtType))
            {
                string type = string.Empty;
                string code = string.Empty;
                if (CB_SourceImageFiles.SelectedItem is DeviceService deviceService)
                {
                    type = deviceService.ServiceTypes.ToString();
                    code = deviceService.Code;
                    MsgRecord ss = Service.LedCheck2(code, type, imgFileName, fileExtType, sn, ledCheck2Param, poiParam, color);
                    ServicesHelper.SendCommand(ss, "正在计算灯珠检测2");
                }
            }
        }

        private void Button_Click_RawRefresh(object sender, RoutedEventArgs e)
        {
            if (CB_SourceImageFiles.SelectedItem is not DeviceService deviceService) return;

            Service.GetRawFiles(deviceService.Code, deviceService.ServiceTypes.ToString());
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
                var lEDStripDetectionParam  = TemplateLEDStripDetectionParam.Params[ComboxLEDStripDetectionTemplate.SelectedIndex].Value;

                string type = string.Empty;
                string code = string.Empty;
                if (CB_SourceImageFiles.SelectedItem is DeviceService deviceService)
                {
                    type = deviceService.ServiceTypes.ToString();
                    code = deviceService.Code;
                }
                MsgRecord ss = Service.LEDStripDetection(code, type, imgFileName, fileExtType, lEDStripDetectionParam.Id, ComboxLedCheckTemplate.Text, sn);
                ServicesHelper.SendCommand(ss, "正在计算灯带检测");
            }
        }

    }
}
