using ColorVision.Device;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using ColorVision.Solution;
using ColorVision.Template;
using log4net;
using MQTTMessageLib.Algorithm;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using Panuon.WPF.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Algorithm
{
    /// <summary>
    /// AlgorithmDisplayControl.xaml 的交互逻辑
    /// </summary>
    public partial class AlgorithmDisplayControl : UserControl
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(AlgorithmDisplayControl));

        public DeviceAlgorithm Device { get; set; }

        public AlgorithmService Service { get => Device.Service; }

        public AlgorithmView View { get => Device.View; }

        private IPendingHandler? handler { get; set; }

        private ResultService resultService { get; set; }

        private Dictionary<string, string> fileCache;


        public AlgorithmDisplayControl(DeviceAlgorithm device)
        {
            Device = device;
            InitializeComponent();

            Service.OnAlgorithmEvent += Service_OnAlgorithmEvent;
            View.OnCurSelectionChanged += View_OnCurSelectionChanged;

            fileCache = new Dictionary<string, string>();
        }

        private void View_OnCurSelectionChanged(PoiResult data)
        {
            doOpen(data.ImgFileName);
        }

        private void Service_OnAlgorithmEvent(object sender, AlgorithmEvent arg)
        {
            switch (arg.EventName)
            {
                case MQTTAlgorithmEventEnum.Event_GetCIEFiles:
                    List<string> data = JsonConvert.DeserializeObject<List<string>>(JsonConvert.SerializeObject(arg.Data));
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CB_CIEImageFiles.ItemsSource = data;
                        CB_CIEImageFiles.SelectedIndex = 0;
                    });
                    break;

                case MQTTAlgorithmEventEnum.Event_POI_GetData:
                    string rawDataMsg = JsonConvert.SerializeObject(arg.Data);
                    MQTTPOIGetDataResult poiResp = JsonConvert.DeserializeObject<MQTTPOIGetDataResult>(rawDataMsg);
                    var poiDbResults = resultService.PoiPointSelectByBatchCode(arg.SerialNumber);
                    if (poiResp!=null)
                        ShowResult(arg.SerialNumber, poiDbResults, rawDataMsg, poiResp);
                    break;
                case MQTTAlgorithmEventEnum.Event_UploadCIEFile:
                    handler?.Close();
                    Service.GetCIEFiles();
                    break;
            }
        }

        private void ShowResult(string serialNumber, List<POIPointResultModel> poiDbResults, string rawMsg, MQTTPOIGetDataResult response)
        {
            Application.Current.Dispatcher.Invoke(() => { _ShowResult(serialNumber, poiDbResults, rawMsg, response); });
        }

        private void _ShowResult(string serialNumber, List<POIPointResultModel> poiDbResults, string rawMsg, MQTTPOIGetDataResult response)
        {
            switch (response.ResultType)
            {
                case AlgorithmResultType.POI_XY_UV:
                    ShowResultCIExyuv(serialNumber, response.POITemplateName, response.POIImgFileName, response.HasRecord, poiDbResults, rawMsg);
                    break;
                case AlgorithmResultType.POI_Y:
                    ShowResultCIEY(serialNumber, response.POITemplateName, response.POIImgFileName, response.HasRecord, poiDbResults, rawMsg);
                    break;
            }
            //if (!CB_CIEImageFiles.Text.Equals(response.POIImgFileName, StringComparison.Ordinal))
            //{
            //    CB_CIEImageFiles.Text = response.POIImgFileName;
            //    doOpen(response.POIImgFileName);
            //}
            handler?.Close();
        }

        private List<POIResultCIEY>? ShowResultCIEY(string serialNumber, string templateName, string POIImgFileName, bool hasRecord, List<POIPointResultModel> poiDbResults, string rawMsg)
        {
            List<POIResultCIEY> poiResultData = null;
            if (hasRecord)
            {
                MQTTPOIGetDataCIEYResult response = JsonConvert.DeserializeObject<MQTTPOIGetDataCIEYResult>(rawMsg);
                if (response!=null)
                    poiResultData = response.Results;
            }
            else
            {
                poiResultData = new List<POIResultCIEY>();
                foreach (var item in poiDbResults)
                {
                    poiResultData.Add(new POIResultCIEY(new POIPoint(item.PoiId ?? 0, item.Pid ?? 0, item.PoiName, (POIPointTypes)(item.PoiType ?? 0), item.PoiX ?? 0, item.PoiY ?? 0  , item.PoiWidth ?? 0, item.PoiHeight ?? 0),
                       JsonConvert.DeserializeObject<POIDataCIEY>(item.Value??string.Empty)));
                }
            }
            if (poiResultData!=null)
                Device.View.PoiDataDraw(serialNumber, templateName, POIImgFileName, poiResultData);
            return poiResultData;
        }

        private List<POIResultCIExyuv>? ShowResultCIExyuv(string serialNumber, string templateName, string POIImgFileName, bool hasRecord, List<POIPointResultModel> poiDbResults, string rawMsg)
        {
            List<POIResultCIExyuv> poiResultData = null;
            if (hasRecord)
            {
                poiResultData = JsonConvert.DeserializeObject<MQTTPOIGetDataCIExyuvResult>(rawMsg)?.Results;
            }
            else
            {
                poiResultData = new List<POIResultCIExyuv>();
                foreach (var item in poiDbResults)
                {
                    poiResultData.Add(new POIResultCIExyuv(new POIPoint(item.PoiId??0, item.Pid ?? 0, item.PoiName, (POIPointTypes)(item.PoiType??0), item.PoiX??0, item.PoiY ?? 0, item.PoiWidth ?? 0, item.PoiHeight ?? 0),
                       JsonConvert.DeserializeObject<POIDataCIExyuv>(item.Value ?? string.Empty)));
                }
            }
            if (poiResultData!=null)
                Device.View.PoiDataDraw(serialNumber, templateName, POIImgFileName, poiResultData);
            return poiResultData;
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
                MessageBox.Show("请先选择关注点模板");
                return;
            }
            string sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            Service.GetData(TemplateControl.GetInstance().PoiParams[ComboxPoiTemplate.SelectedIndex].Value.ID, -1, CB_CIEImageFiles.Text, ComboxPoiTemplate.Text);
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
                MessageBox.Show("请先选择MTF模板");
                return;
            }
            if (ComboxPoiTemplate.SelectedIndex == -1)
            {
                MessageBox.Show("请先选择关注点模板");
                return;
            }

            var ss = Service.MTF(TemplateControl.GetInstance().PoiParams[ComboxPoiTemplate.SelectedIndex].Value.ID, ImageFile.Text, TemplateControl.GetInstance().MTFParams[ComboxMTFTemplate.SelectedIndex].Value);
            Helpers.SendCommand(ss,"MTF");
        }

        private void SFR_Clik(object sender, RoutedEventArgs e)
        {
            if (ComboxSFRTemplate.SelectedIndex == -1)
            {
                MessageBox.Show("请先选择SFR模板");
                return;
            }
            if (ComboxPoiTemplate.SelectedIndex == -1)
            {
                MessageBox.Show("请先选择关注点模板");
                return;
            }

            var msg = Service.SFR(TemplateControl.GetInstance().PoiParams[ComboxPoiTemplate.SelectedIndex].Value.ID, ImageFile.Text, TemplateControl.GetInstance().SFRParams[ComboxSFRTemplate.SelectedIndex].Value);
            Helpers.SendCommand(msg, "SFR");

        }

        private void Ghost_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxGhostTemplate.SelectedIndex == -1)
            {
                MessageBox.Show("请先选择Ghost模板");
                return;
            }
            if (ComboxPoiTemplate.SelectedIndex == -1)
            {
                MessageBox.Show("请先选择关注点模板");
                return;
            }



            var msg = Service.Ghost(TemplateControl.GetInstance().PoiParams[ComboxPoiTemplate.SelectedIndex].Value.ID, ImageFile.Text, TemplateControl.GetInstance().GhostParams[ComboxGhostTemplate.SelectedIndex].Value);
            Helpers.SendCommand(msg, "Ghost");
        }

        private void Distortion_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxDistortionTemplate.SelectedIndex == -1)
            {
                MessageBox.Show("请先选择Distortion模板");
                return;
            }
            if (ComboxPoiTemplate.SelectedIndex == -1)
            {
                MessageBox.Show("请先选择关注点模板");
                return;
            }
            var msg = Service.Distortion(TemplateControl.GetInstance().PoiParams[ComboxPoiTemplate.SelectedIndex].Value.ID, ImageFile.Text, TemplateControl.GetInstance().DistortionParams[ComboxDistortionTemplate.SelectedIndex].Value);
            Helpers.SendCommand(msg, "Distortion");
        }


        private void FOV_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxFOVTemplate.SelectedIndex == -1)
            {
                MessageBox.Show("请先选择FOV模板");
                return;
            }
            if (ComboxPoiTemplate.SelectedIndex == -1)
            {
                MessageBox.Show("请先选择关注点模板");
                return;
            }

            var msg = Service.FOV(TemplateControl.GetInstance().PoiParams[ComboxPoiTemplate.SelectedIndex].Value.ID, ImageFile.Text, TemplateControl.GetInstance().FOVParams[ComboxFOVTemplate.SelectedIndex].Value);
            Helpers.SendCommand(msg, "FOV");
        }


        private void Open_File(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.InitialDirectory = Environment.CurrentDirectory;
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
            openFileDialog.InitialDirectory = Environment.CurrentDirectory;
            openFileDialog.Filter = "CVCIE files (*.cvcie) | *.cvcie";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Service.UploadCIEFile(System.IO.Path.GetFileName(openFileDialog.FileName));

                Task t = new(() => { Task_StartUpload(openFileDialog.FileName); });
                t.Start();

                handler = PendingBox.Show(Application.Current.MainWindow, "", "上传", true);
                handler.Cancelling += delegate
                {
                    t.Dispose();
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
            doOpen(CB_CIEImageFiles.Text);
        }

        private void doOpen(string fileName)
        {
            if(fileCache.ContainsKey(fileName))
            {
                byte[] data = CVFileUtils.ReadBinaryFile(fileCache[fileName]);
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    View.OpenImage(data);
                });
                handler?.Close();
                handler = null;
            }
            else
            {
                Service.Open(fileName);
                Task t = new(() => { Task_Start(fileName); });
                t.Start();
            }
        }

        private void Task_Start(string fileName)
        {
            DealerSocket client = null;
            try
            {
                client = new DealerSocket(Device.Config.Endpoint);
                List<byte[]> data = client.ReceiveMultipartBytes();
                if (data.Count == 1)
                {
                    string fullFileName = SolutionControl.GetInstance().SolutionConfig.CachePath + "\\" + fileName;
                    CVFileUtils.WriteBinaryFile(fullFileName, data[0]);
                    fileCache.Add(fileName, fullFileName);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        View.OpenImage(data[0]);
                    });
                }
                client?.Close();
                client?.Dispose();
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                client?.Close();
                client?.Dispose();
            }

            handler?.Close();
        }

        private void Task_StartUpload(string fileName)
        {
            DealerSocket client = new DealerSocket(Device.Config.Endpoint);
            var message = new List<byte[]>();
            message.Add(CVFileUtils.ReadBinaryFile(fileName));
            client.TrySendMultipartBytes(TimeSpan.FromMilliseconds(3000), message);
            client.Close();
            client.Dispose();
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
                    MessageBox.Show("数据库连接失败，请先连接数据库在操作");
                    return;
                }
                switch (button.Tag?.ToString() ?? string.Empty)
                {
                    case "FocusParm":
                        windowTemplate = new WindowTemplate(TemplateType.PoiParam) { Title = "关注点设置" };
                        TemplateAbb(windowTemplate, TemplateControl.PoiParams);
                        break;
                    case "MTFParam":
                        windowTemplate = new WindowTemplate(TemplateType.MTFParam) { Title = "MTF算法设置" };
                        TemplateAbb(windowTemplate, TemplateControl.MTFParams);
                        break;
                    case "SFRParam":
                        windowTemplate = new WindowTemplate(TemplateType.SFRParam) { Title = "SFR算法设置" };
                        TemplateAbb(windowTemplate, TemplateControl.SFRParams);
                        break;
                    case "FOVParam":
                        windowTemplate = new WindowTemplate(TemplateType.FOVParam) { Title = "FOV算法设置" };
                        TemplateAbb(windowTemplate, TemplateControl.FOVParams);
                        break;
                    case "GhostParam":
                        windowTemplate = new WindowTemplate(TemplateType.GhostParam) { Title = "Ghost算法设置" };
                        TemplateAbb(windowTemplate, TemplateControl.GhostParams);
                        break;
                    case "DistortionParam":
                        windowTemplate = new WindowTemplate(TemplateType.DistortionParam) { Title = "Distortion算法设置" };
                        TemplateAbb(windowTemplate, TemplateControl.DistortionParams);
                        break;
                    default:
                        HandyControl.Controls.Growl.Info("开发中");
                        break;
                }
            }
        }
        private void TemplateAbb<T>(WindowTemplate windowTemplate, ObservableCollection<Template<T>> keyValuePairs) where T : ParamBase
        {
            windowTemplate.Owner = Window.GetWindow(this);
            windowTemplate.ListConfigs.Clear();
            foreach (var item in keyValuePairs)
            {
                if (item.Value is PoiParam poiParam)
                {
                    item.Tag = $"{poiParam.Width}*{poiParam.Height}{(GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql ? "" : $"_{poiParam.PoiPoints.Count}")}";
                }

                windowTemplate.ListConfigs.Add(item);
            }
            windowTemplate.ShowDialog();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            WindowSolution windowSolution = new WindowSolution() { Owner = Window.GetWindow(this) };
            windowSolution.Show();
        }
    }
}
