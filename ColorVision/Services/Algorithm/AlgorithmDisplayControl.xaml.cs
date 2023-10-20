using ColorVision.Device.Algorithm;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using ColorVision.Template;
using log4net;
using MQTTMessageLib.Algorithm;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using Panuon.WPF.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Device.POI
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

        private IPendingHandler handler { get; set; }

        private ResultService resultService { get; set; }


        public AlgorithmDisplayControl(DeviceAlgorithm device)
        {
            Device = device;
            InitializeComponent();

            Service.OnAlgorithmEvent += Service_OnAlgorithmEvent;
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
            switch (response.ResultType)
            {
                case POIResultType.XY_UV:
                    ShowResultCIExyuv(serialNumber, response.HasRecord, poiDbResults, rawMsg);
                    break;
                case POIResultType.Y:
                    ShowResultCIEY(serialNumber, response.HasRecord, poiDbResults, rawMsg);
                    break;
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!CB_CIEImageFiles.Text.Equals(response.POIImgFileName, StringComparison.Ordinal))
                {
                    CB_CIEImageFiles.Text = response.POIImgFileName;
                    doOpen(response.POIImgFileName);
                }
            });
        }

        private List<POIResultCIEY> ShowResultCIEY(string serialNumber, bool hasRecord, List<POIPointResultModel> poiDbResults, string rawMsg)
        {
            List<POIResultCIEY> poiResultData;
            if (hasRecord)
            {
                MQTTPOIGetDataCIEYResult response = JsonConvert.DeserializeObject<MQTTPOIGetDataCIEYResult>(rawMsg);
                poiResultData = response.Results;
            }
            else
            {
                poiResultData = new List<POIResultCIEY>();
                foreach (var item in poiDbResults)
                {
                    poiResultData.Add(new POIResultCIEY(new POIPoint((int)item.PoiId, (int)item.Pid, item.Name, (POIPointTypes)item.Type, (int)item.PixX, (int)item.PixY, (int)item.PixWidth, (int)item.PixHeight),
                       JsonConvert.DeserializeObject<POIDataCIEY>(item.Value)));
                }
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                Device.View.PoiDataDraw(serialNumber, poiResultData);
            });

            return poiResultData;
        }

        private List<POIResultCIExyuv> ShowResultCIExyuv(string serialNumber,bool hasRecord, List<POIPointResultModel> poiDbResults, string rawMsg)
        {
            List<POIResultCIExyuv> poiResultData;
            if (hasRecord)
            {
                poiResultData = JsonConvert.DeserializeObject<MQTTPOIGetDataCIExyuvResult>(rawMsg).Results;
            }
            else
            {
                poiResultData = new List<POIResultCIExyuv>();
                foreach (var item in poiDbResults)
                {
                    poiResultData.Add(new POIResultCIExyuv(new POIPoint((int)item.PoiId, (int)item.Pid, item.Name, (POIPointTypes)item.Type, (int)item.PixX, (int)item.PixY, (int)item.PixWidth, (int)item.PixHeight),
                       JsonConvert.DeserializeObject<POIDataCIExyuv>(item.Value)));
                }
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                Device.View.PoiDataDraw(serialNumber, poiResultData);
            });
            return poiResultData;
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
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
            ViewGridManager.GetInstance().ViewMaxChangedEvent += (e) =>
            {
                List<KeyValuePair<string, int>> KeyValues = new List<KeyValuePair<string, int>>();
                KeyValues.Add(new KeyValuePair<string, int>(Properties.Resource.WindowSingle, -2));
                KeyValues.Add(new KeyValuePair<string, int>(Properties.Resource.WindowHidden, -1));
                for (int i = 0; i < e; i++)
                {
                    KeyValues.Add(new KeyValuePair<string, int>((i + 1).ToString(), i));
                }
                ComboxView.ItemsSource = KeyValues;
                ComboxView.SelectedValue = View.View.ViewIndex;
            };
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
            //var Batch = ServiceControl.GetInstance().GetResultBatch(sn);
            Service.GetData(TemplateControl.GetInstance().PoiParams[ComboxPoiTemplate.SelectedIndex].Value.ID, -1, CB_CIEImageFiles.Text, ComboxPoiTemplate.Text);
        }

        private void Algorithm_INI(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                 var msg = Service.Init();

                Helpers.SendCommand(button, msg);

            }
        }

        private void Algorithm_GET(object sender, RoutedEventArgs e)
        {
            Service.GetAllSnID();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var a = resultService.PoiSelectByBatchID(10);
            Device.View.PoiDataDraw(a);
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
            doOpen(CB_CIEImageFiles.Text);
        }

        private void doOpen(string fileName)
        {
            Service.Open(fileName);
            Task t = new(() => { Task_Start(); });
            t.Start();

            handler = PendingBox.Show(Application.Current.MainWindow, "", "打开图片", true);
            handler.Cancelling += delegate
            {
                handler?.Close();
            };
        }

        private void Task_Start()
        {
            DealerSocket client = null;
            try
            {
                client = new DealerSocket(Device.Config.Endpoint);
                List<byte[]> data = client.ReceiveMultipartBytes();
                if (data.Count == 1)
                {
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

        private static byte[] readFile(string path)
        {
            FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            BinaryReader binaryReader = new BinaryReader(fileStream);
            //获取文件长度
            long length = fileStream.Length;
            byte[] bytes = new byte[length];
            //读取文件中的内容并保存到字节数组中
            binaryReader.Read(bytes, 0, bytes.Length);
            return bytes;
        }

        private void Task_StartUpload(string fileName)
        {
            DealerSocket client = new DealerSocket(Device.Config.Endpoint);
            var message = new List<byte[]>();
            message.Add(readFile(fileName));
            client.TrySendMultipartBytes(TimeSpan.FromMilliseconds(3000), message);
            client.Close();
            client.Dispose();
        }
    }
}
