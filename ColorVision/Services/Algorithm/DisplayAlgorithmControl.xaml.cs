#pragma warning disable CS8604,CS0168,CS8629,CA1822,CS8602
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using ColorVision.Net;
using ColorVision.Services.Device;
using ColorVision.Solution;
using ColorVision.Templates;
using log4net;
using MQTTMessageLib.Algorithm;
using MQTTMessageLib.FileServer;
using Newtonsoft.Json;
using Panuon.WPF.UI;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Algorithm
{
    /// <summary>
    /// DisplayAlgorithmControl.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayAlgorithmControl : UserControl
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(DisplayAlgorithmControl));

        public DeviceAlgorithm Device { get; set; }

        public AlgorithmService Service { get => Device.DeviceService; }

        public AlgorithmView View { get => Device.View; }

        private IPendingHandler? handler { get; set; }

        private ResultService resultService { get; set; }

        private NetFileUtil netFileUtil;


        public DisplayAlgorithmControl(DeviceAlgorithm device)
        {
            Device = device;
            InitializeComponent();

            netFileUtil = new NetFileUtil(SolutionManager.GetInstance().CurrentSolution.FullName + "\\Cache");
            netFileUtil.handler += NetFileUtil_handler;
            Service.OnMessageRecved += Service_OnAlgorithmEvent;
            View.OnCurSelectionChanged += View_OnCurSelectionChanged;

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
                Application.Current.Dispatcher.Invoke(() =>
                {
                    //MessageBox.IsShow(Application.Current.MainWindow, "文件打开失败", "ColorVision");
                });
            }
        }

        private void View_OnCurSelectionChanged(AlgorithmResult data)
        {
            switch (data.ResultType)
            {
                case AlgorithmResultType.POI_XY_UV:
                case AlgorithmResultType.POI_Y:
                    doOpen(data.ImgFileName, FileExtType.CIE);
                    break;
                case AlgorithmResultType.SFR:
                case AlgorithmResultType.MTF:
                case AlgorithmResultType.FOV:
                case AlgorithmResultType.Distortion:
                    doOpenLocal(data.ImgFileName, FileExtType.Src);
                    break;
                case AlgorithmResultType.Ghost:
                    doOpenLocal(data.ImgFileName, FileExtType.Tif);
                    //doOpen(data.ImgFileName, FileExtType.Src);
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
                        CB_RawImageFiles.ItemsSource = data.Files;
                        CB_RawImageFiles.SelectedIndex = 0;
                    });
                    break;
                case FileExtType.Src:
                    break;
                case FileExtType.CIE:
                    Application.Current.Dispatcher.Invoke(() =>
                    {
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

        private void Service_OnAlgorithmEvent(object sender, MessageRecvArgs arg)
        {
            switch (arg.EventName)
            {
                case MQTTFileServerEventEnum.Event_File_List_All:
                    DeviceListAllFilesParam data = JsonConvert.DeserializeObject<DeviceListAllFilesParam>(JsonConvert.SerializeObject(arg.Data));
                    DoShowFileList(data);
                    break;

                case MQTTAlgorithmEventEnum.Event_POI_GetData:
                case MQTTAlgorithmEventEnum.Event_FOV_GetData:
                case MQTTAlgorithmEventEnum.Event_MTF_GetData:
                case MQTTAlgorithmEventEnum.Event_SFR_GetData:
                case MQTTAlgorithmEventEnum.Event_Ghost_GetData:
                case MQTTAlgorithmEventEnum.Event_LedCheck_GetData:
                case MQTTAlgorithmEventEnum.Event_Distortion_GetData:
                    ShowResultFromDB(arg.SerialNumber, Convert.ToInt32(arg.Data.MasterId));
                    break;
                case MQTTFileServerEventEnum.Event_File_Upload:
                    DeviceFileUpdownParam pm_up = JsonConvert.DeserializeObject<DeviceFileUpdownParam>(JsonConvert.SerializeObject(arg.Data));
                    FileUpload(pm_up);
                    break;
                case MQTTFileServerEventEnum.Event_File_Download:
                    DeviceFileUpdownParam pm_dl = JsonConvert.DeserializeObject<DeviceFileUpdownParam>(JsonConvert.SerializeObject(arg.Data));
                    FileDownload(pm_dl);
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

        private void ShowResultFromDB(string serialNumber, int masterId)
        {
            List<AlgResultMasterModel> resultMaster = null;
            if (masterId > 0)
            {
                resultMaster = new List<AlgResultMasterModel>();
                AlgResultMasterModel model = resultService.GetAlgResultById(masterId);
                resultMaster.Add(model);
            }
            else
            {
                resultMaster = resultService.GetAlgResultBySN(serialNumber);
            }
           
            foreach (AlgResultMasterModel result in resultMaster)
            {
                Process(result);
            }
            handler?.Close();
        }

        public void Process(AlgResultMasterModel result)
        {
            switch (result.ImgFileType)
            {
                case AlgorithmResultType.POI_XY_UV:
                case AlgorithmResultType.POI_Y:
                case AlgorithmResultType.POI:
                    LoadResultPOIFromDB(result);
                    break;
                case AlgorithmResultType.Distortion:
                case AlgorithmResultType.Ghost:
                case AlgorithmResultType.LedCheck:
                case AlgorithmResultType.FOV:
                case AlgorithmResultType.MTF:
                case AlgorithmResultType.SFR:
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Device.View.AlgResultMasterModelDataDraw(result);
                    });
                    break;
            }
        }

        private void LoadResultPOIFromDB(AlgResultMasterModel result)
        {
            var details = resultService.GetPOIByPid(result.Id);
            switch (result.ImgFileType)
            {
                case AlgorithmResultType.POI_XY_UV:
                    var results = BuildPOIResultCIExyuv(details);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Device.View.PoiDataDraw(result, results);
                    });
                    break;
                case AlgorithmResultType.POI_Y:
                    var results_y = BuildPOIResultCIEY(details);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Device.View.PoiDataDraw(result, results_y);
                    });
                    break;
                case AlgorithmResultType.POI:
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Device.View.AlgResultDataDraw(result);
                    });
                    break;
            }
        }

        private List<POIResultCIExyuv> BuildPOIResultCIExyuv(List<POIPointResultModel> details)
        {
            List<POIResultCIExyuv> results = new List<POIResultCIExyuv>();
            foreach (POIPointResultModel detail in details)
            {
                POIResultCIExyuv result = new POIResultCIExyuv(
                                            new POIPoint((int)detail.PoiId, -1, detail.PoiName, (POIPointTypes)detail.PoiType, (int)detail.PoiX, (int)detail.PoiY, (int)detail.PoiWidth, (int)detail.PoiHeight),
                                            JsonConvert.DeserializeObject<POIDataCIExyuv>(detail.Value));

                results.Add(result);
            }
            return results;
        }

        private List<MQTTMessageLib.Algorithm.POIResultCIEY> BuildPOIResultCIEY(List<POIPointResultModel> details)
        {
            List<POIResultCIEY> results = new List<POIResultCIEY>();
            foreach (POIPointResultModel detail in details)
            {
                POIResultCIEY result = new POIResultCIEY(
                                        new POIPoint((int)detail.PoiId, -1, detail.PoiName, (POIPointTypes)detail.PoiType, (int)detail.PoiX, (int)detail.PoiY, (int)detail.PoiWidth, (int)detail.PoiHeight),
                                        JsonConvert.DeserializeObject<POIDataCIEY>(detail.Value));

                results.Add(result);
            }

            return results;
        }


        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Device;
            ComboxPoiTemplate.ItemsSource = TemplateControl.GetInstance().PoiParams;
            ComboxPoiTemplate.SelectedIndex = 0;

            ComboxMTFTemplate.ItemsSource = TemplateControl.GetInstance().MTFParams;
            ComboxMTFTemplate.SelectedIndex = 0;

            ComboxSFRTemplate.ItemsSource = TemplateControl.GetInstance().SFRParams;
            ComboxSFRTemplate.SelectedIndex = 0;

            ComboxGhostTemplate.ItemsSource = TemplateControl.GetInstance().GhostParams;
            ComboxGhostTemplate.SelectedIndex = 0;

            ComboxFOVTemplate.ItemsSource = TemplateControl.GetInstance().FOVParams;
            ComboxFOVTemplate.SelectedIndex = 0;

            ComboxDistortionTemplate.ItemsSource = TemplateControl.GetInstance().DistortionParams;
            ComboxDistortionTemplate.SelectedIndex = 0;

            ComboxLedCheckTemplate.ItemsSource = TemplateControl.GetInstance().LedCheckParams;  
            ComboxLedCheckTemplate.SelectedIndex = 0;

            ComboxFocusPointsTemplate.ItemsSource = TemplateControl.GetInstance().FocusPointsParams;
            ComboxFocusPointsTemplate.SelectedIndex = 0;


            ViewGridManager.GetInstance().AddView(Device.View);
            ViewMaxChangedEvent(ViewGridManager.GetInstance().ViewMax);
            ViewGridManager.GetInstance().ViewMaxChangedEvent += ViewMaxChangedEvent;

            void ViewMaxChangedEvent(int max)
            {
                List<KeyValuePair<string, int>> KeyValues = new List<KeyValuePair<string, int>>();
                KeyValues.Add(new KeyValuePair<string, int>(Properties.Resource.WindowSingle, -2));
                KeyValues.Add(new KeyValuePair<string, int>(Properties.Resource.WindowHidden, -1));
                for (int i = 0; i < max; i++)
                {
                    KeyValues.Add(new KeyValuePair<string, int>((i + 1).ToString(), i));
                }
                ComboxView.ItemsSource = KeyValues;
                ComboxView.SelectedValue = View.View.ViewIndex;
            }
            View.View.ViewIndexChangedEvent += (e1, e2) =>
            {
                ComboxView.SelectedIndex = e2 + 2;
            };
            ComboxView.SelectionChanged += (s, e) =>
            {
                if (ComboxView.SelectedItem is KeyValuePair<string, int> KeyValue)
                {
                    View.View.ViewIndex = KeyValue.Value;
                    ViewGridManager.GetInstance().SetViewIndex(View, KeyValue.Value);
                }
            };
            View.View.ViewIndex = -1;

            resultService = new ResultService();
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
            var pm = TemplateControl.GetInstance().PoiParams[ComboxPoiTemplate.SelectedIndex].Value;
            Service.POI(imgFileName, pm.ID, ComboxPoiTemplate.Text, sn);
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
                Helpers.SendCommand(button, msg);
            }
        }
        private void MTF_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxMTFTemplate.SelectedIndex==-1)
            {
                MessageBox.Show(Application.Current.MainWindow, "请先选择MTF模板", "ColorVision");
                return;
            }
            if (ComboxPoiTemplate.SelectedIndex == -1)
            {
                MessageBox.Show(Application.Current.MainWindow, "请先选择关注点模板", "ColorVision");
                return;
            }
            string sn = string.Empty;
            string imgFileName = ImageFile.Text;
            FileExtType fileExtType = FileExtType.Tif;

            if (GetAlgSN(ref sn, ref imgFileName, ref fileExtType))
            {
                var pm = TemplateControl.GetInstance().MTFParams[ComboxMTFTemplate.SelectedIndex].Value;
                var poi_pm = TemplateControl.GetInstance().PoiParams[ComboxPoiTemplate.SelectedIndex].Value;
                var ss = Service.MTF(imgFileName, fileExtType, pm.ID, ComboxMTFTemplate.Text, sn, poi_pm.ID, ComboxPoiTemplate.Text);
                Helpers.SendCommand(ss, "MTF");
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
                var pm = TemplateControl.GetInstance().SFRParams[ComboxSFRTemplate.SelectedIndex].Value;
                var msg = Service.SFR(imgFileName, fileExtType, pm.ID, ComboxSFRTemplate.Text, sn);
                Helpers.SendCommand(msg, "SFR");
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
                var pm = TemplateControl.GetInstance().GhostParams[ComboxGhostTemplate.SelectedIndex].Value;
                var msg = Service.Ghost(imgFileName, fileExtType, pm.ID, ComboxGhostTemplate.Text, sn);
                Helpers.SendCommand(msg, "Ghost");
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
                var pm = TemplateControl.GetInstance().DistortionParams[ComboxDistortionTemplate.SelectedIndex].Value;
                var msg = Service.Distortion(imgFileName, fileExtType, pm.ID, ComboxDistortionTemplate.Text, sn);
                Helpers.SendCommand(msg, "Distortion");
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

            if (GetAlgSN(ref sn,ref imgFileName ,ref fileExtType))
            {
                var pm = TemplateControl.GetInstance().FOVParams[ComboxFOVTemplate.SelectedIndex].Value;
                var msg = Service.FOV(imgFileName, fileExtType, pm.ID, ComboxFOVTemplate.Text, sn);
                Helpers.SendCommand(msg, "FOV");
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
            Service.GetCIEFiles();
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
            if (string.IsNullOrEmpty(localName) || !System.IO.File.Exists(localName))
            {
                Service.Open(fileName, extType);
            }
            else
            {
                netFileUtil.OpenLocalFile(localName, extType);
            }
        }

        TemplateControl TemplateControl { get; set; }

        private void MenuItem_Template(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                TemplateControl= TemplateControl.GetInstance();
                SoftwareConfig SoftwareConfig = GlobalSetting.GetInstance().SoftwareConfig;
                WindowTemplate windowTemplate;
                if (SoftwareConfig.IsUseMySql && !SoftwareConfig.MySqlControl.IsConnect)
                {
                    MessageBox.Show(Application.Current.MainWindow, "数据库连接失败，请先连接数据库在操作", "ColorVision");
                    return;
                }
                switch (button.Tag?.ToString() ?? string.Empty)
                {

                    case "MTFParam":
                        windowTemplate = new WindowTemplate(TemplateType.MTFParam, false);
                        windowTemplate.Owner = Window.GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                    case "SFRParam":
                        windowTemplate = new WindowTemplate(TemplateType.SFRParam, false);
                        windowTemplate.Owner = Window.GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                    case "FOVParam":
                        windowTemplate = new WindowTemplate(TemplateType.FOVParam, false);
                        windowTemplate.Owner = Window.GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                    case "GhostParam":
                        windowTemplate = new WindowTemplate(TemplateType.GhostParam, false);
                        windowTemplate.Owner = Window.GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                    case "DistortionParam":
                        windowTemplate = new WindowTemplate(TemplateType.DistortionParam, false);
                        windowTemplate.Owner = Window.GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                    case "LedCheckParam":
                        windowTemplate = new WindowTemplate(TemplateType.LedCheckParam, false);
                        windowTemplate.Owner = Window.GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                    case "FocusPointsParam":
                        windowTemplate = new WindowTemplate(TemplateType.FocusPointsParam,false);
                        windowTemplate.Owner = Window.GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                    case "CalibrationUpload":
                        CalibrationUploadWindow calibrationUpload = new CalibrationUploadWindow();
                        calibrationUpload.Owner = Window.GetWindow(this);
                        calibrationUpload.ShowDialog();
                        break;
                    case "FocusParm":
                        windowTemplate = new WindowTemplate(TemplateType.PoiParam);
                        windowTemplate.Owner = Window.GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                    default:
                        HandyControl.Controls.Growl.Info("开发中");
                        break;
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            WindowSolution windowSolution = new WindowSolution() { Owner = Window.GetWindow(this) };
            windowSolution.Show();
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
                var pm = TemplateControl.GetInstance().FocusPointsParams[ComboxFocusPointsTemplate.SelectedIndex].Value;
                var ss = Service.FocusPoints(ImageFile.Text, fileExtType, pm.ID, ComboxFocusPointsTemplate.Text, sn);
                Helpers.SendCommand(ss, "FocusPoints");
            }
        }

        private void LedCheck_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxLedCheckTemplate.SelectedIndex == -1)
            {
                MessageBox.Show(Application.Current.MainWindow, "请先选择灯珠检测模板", "ColorVision");
                return;
            }

            string sn = string.Empty;
            string imgFileName = ImageFile.Text;
            FileExtType fileExtType = FileExtType.Tif;

            if (GetAlgSN(ref sn, ref imgFileName, ref fileExtType))
            {
                var pm = TemplateControl.GetInstance().LedCheckParams[ComboxLedCheckTemplate.SelectedIndex].Value;
                var ss = Service.LedCheck(ImageFile.Text, fileExtType, pm.ID, ComboxLedCheckTemplate.Text, sn);
                Helpers.SendCommand(ss, "正在计算灯珠");
            }
        }

        private void Button_Click_RawRefresh(object sender, RoutedEventArgs e)
        {
            Service.GetRawFiles();
        }

        private void Button_Click_RawUpload(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click_RawOpen(object sender, RoutedEventArgs e)
        {
            handler = PendingBox.Show(Application.Current.MainWindow, "", "打开图片", true);
            handler.Cancelling += delegate
            {
                handler?.Close();
            };
            doOpen(CB_RawImageFiles.Text, FileExtType.Raw);
        }
    }
}
