#pragma warning disable CS8602,CA1707
using ColorVision.Common.Utilities;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.DAO;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.POI;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Services.Flow;
using ColorVision.Engine.Templates.POI.Comply;
using ColorVision.Themes;
using CVCommCore;
using FlowEngineLib;
using log4net;
using Panuon.WPF.UI;
using ST.Library.UI.NodeEditor;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.Projects.ProjectShiYuan
{

    public sealed class ConnectConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isconnect)
            {
                return isconnect ? "已经连接":"未连接";
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("Converting from a string to a memory size is not supported.");
        }
    }


    public sealed class ColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isconnect)
            {
                return isconnect ? Brushes.Blue : Brushes.Red;
            }
            return Brushes.Black; ;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("Converting from a string to a memory size is not supported.");
        }
    }



    public class DataRecord
    {
        public int SequenceNumber { get; set; }
        public string Model { get; set; }
        public string ProductID { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan Time { get; set; }
        public double White_x { get; set; }
        public double White_y { get; set; }
        public double White_lv { get; set; }
        public double White_wl { get; set; }
        public string White_Result { get; set; }
        public double Red_x { get; set; }
        public double Red_y { get; set; }
        public double Red_lv { get; set; }
        public double Red_wl { get; set; }
        public string Red_Result { get; set; }
        public double Orange_x { get; set; }
        public double Orange_y { get; set; }
        public double Orange_lv { get; set; }
        public double Orange_wl { get; set; }
        public string Orange_Result { get; set; }
        public double Blue_x { get; set; }
        public double Blue_y { get; set; }
        public double Blue_lv { get; set; }
        public double Blue_wl { get; set; }
        public string Blue_Result { get; set; }
        public string Final_Result { get; set; }
    }

    public class CsvHandler
    {
        private string _filePath;
        private int _currentSequenceNumber;

        public CsvHandler(string filePath)
        {
            _filePath = filePath;
            _currentSequenceNumber = GetLastSequenceNumber();
        }

        private int GetLastSequenceNumber()
        {
            if (!File.Exists(_filePath))
            {
                return 0;
            }

            using (var reader = new StreamReader(_filePath))
            {
                string line;
                string lastLine = null;
                while ((line = reader.ReadLine()) != null)
                {
                    lastLine = line;
                }

                if (lastLine != null)
                {
                    var values = lastLine.Split(',');
                    if (int.TryParse(values[0], out int sequenceNumber))
                    {
                        return sequenceNumber;
                    }
                }
            }

            return 0;
        }

        public void SaveRecord(DataRecord record)
        {
            record.SequenceNumber = ++_currentSequenceNumber;

            using (var writer = new StreamWriter(_filePath, true))
            {
                if (new FileInfo(_filePath).Length == 0)
                {
                    // Write header if file is empty
                    writer.WriteLine("SequenceNumber,Model,ProductID,Date,Time,White_x,White_y,White_lv(cd),White_wl(nm),White_Result,Red_x,Red_y,Red_lv(cd),Red_wl(nm),Red_Result,Orange_x,Orange_y,Orange_lv(cd),Orange_wl(nm),Orange_Result,Blue_x,Blue_y,Blue_lv(cd),Blue_wl(nm),Blue_Result,Final_Result");
                }

                writer.WriteLine($"{record.SequenceNumber},{record.Model},{record.ProductID},{record.Date.ToString("yyyy-MM-dd-HH-mm-ss")},{record.Time},{record.White_x},{record.White_y},{record.White_lv},{record.White_wl},{record.White_Result},{record.Red_x},{record.Red_y},{record.Red_lv},{record.Red_wl},{record.Red_Result},{record.Orange_x},{record.Orange_y},{record.Orange_lv},{record.Orange_wl},{record.Orange_Result},{record.Blue_x},{record.Blue_y},{record.Blue_lv},{record.Blue_wl},{record.Blue_Result},{record.Final_Result}");
            }
        }
    }


    /// <summary>
    /// ShiyuanProjectWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ShiyuanProjectWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ShiyuanProjectWindow));
        public ShiyuanProjectWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }



        public ObservableCollection<TempResult> Settings { get; set; } = new ObservableCollection<TempResult>();
        public ObservableCollection<TempResult> Results { get; set; } = new ObservableCollection<TempResult>();


        private FlowEngineLib.FlowEngineControl flowEngine;
        private void Window_Initialized(object sender, EventArgs e)
        {
            MQTTConfig mQTTConfig = MQTTSetting.Instance.MQTTConfig;
            MQTTHelper.SetDefaultCfg(mQTTConfig.Host, mQTTConfig.Port, mQTTConfig.UserName, mQTTConfig.UserPwd, false, null);
            flowEngine = new FlowEngineControl(false);

            STNodeEditor STNodeEditorMain = new STNodeEditor();
            STNodeEditorMain.LoadAssembly("FlowEngineLib.dll");
            flowEngine.AttachNodeEditor(STNodeEditorMain);

            ListViewResult.ItemsSource = Results;

            FlowTemplate.ItemsSource = FlowParam.Params;
            FlowTemplate.SelectionChanged += (s, e) =>
            {
                if (FlowTemplate.SelectedIndex > -1)
                {
                    var tokens = ServiceManager.GetInstance().ServiceTokens;
                    flowEngine.LoadFromBase64(FlowParam.Params[FlowTemplate.SelectedIndex].Value.DataBase64, tokens);
                }
            };

            this.DataContext = ProjectShiYuanConfig.Instance;

        }
        private Engine.Services.Flow.FlowControl flowControl;

        private IPendingHandler handler;

        private void FlowControl_FlowCompleted(object? sender, EventArgs e)
        {
            flowControl.FlowCompleted -= FlowControl_FlowCompleted;
            handler?.Close();
            if (sender is FlowControlData FlowControlData)
            {
                if (FlowControlData.EventName == "Completed" || FlowControlData.EventName == "Canceled" || FlowControlData.EventName == "OverTime" || FlowControlData.EventName == "Failed")
                {
                    if (FlowControlData.EventName == "Completed")
                    {
                        var Batch = BatchResultMasterDao.Instance.GetByCode(FlowControlData.SerialNumber);
                        if (Batch != null)
                        {
                            var resultMaster = AlgResultMasterDao.Instance.GetAllByBatchid(Batch.Id);
                            List<PoiResultCIExyuvData> PoiResultCIExyuvDatas = new List<PoiResultCIExyuvData>();
                            foreach (var item in resultMaster)
                            {
                                List<PoiPointResultModel> POIPointResultModels = PoiPointResultDao.Instance.GetAllByPid(item.Id);

                                foreach (var pointResultModel in POIPointResultModels)
                                {
                                    PoiResultCIExyuvData poiResultCIExyuvData = new PoiResultCIExyuvData(pointResultModel);
                                    PoiResultCIExyuvDatas.Add(poiResultCIExyuvData);
                                }
                            }
                            ListViewResult.ItemsSource = PoiResultCIExyuvDatas;
                        }
                        else
                        {
                            MessageBox.Show(Application.Current.GetActiveWindow(), "找不到批次号", "ColorVision");
                        }
                    }
                    else
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), "流程运行失败" + FlowControlData.EventName, "ColorVision");
                    }
                }
                else
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), "流程运行失败" + FlowControlData.EventName, "ColorVision");
                }

            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "流程运行异常", "ColorVision");
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (FlowTemplate.SelectedValue is FlowParam flowParam)
            {
                string startNode = flowEngine.GetStartNodeName();
                if (!string.IsNullOrWhiteSpace(startNode))
                {
                    flowControl ??= new Engine.Services.Flow.FlowControl(MQTTControl.GetInstance(), flowEngine);

                    handler = PendingBox.Show(Application.Current.MainWindow, "TTL:" + "0", "流程运行", true);

                    flowControl.FlowData += (s, e) =>
                    {
                        if (s is FlowControlData msg)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                handler?.UpdateMessage("TTL: " + msg.Params.TTL.ToString());
                            });
                        }
                    };
                    flowControl.FlowCompleted += FlowControl_FlowCompleted;
                    string sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
                    flowControl.Start(sn);
                    string name = string.Empty;
                    BeginNewBatch(sn, name);
                }
                else
                {
                    MessageBox.Show(WindowHelpers.GetActiveWindow(), "找不到完整流程，运行失败", "ColorVision");
                }
            }
            else
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "流程为空，请选择流程运行", "ColorVision");
            }
        }

        public static void BeginNewBatch(string sn, string name)
        {
            BatchResultMasterModel batch = new();
            batch.Name = string.IsNullOrEmpty(name) ? sn : name;
            batch.Code = sn;
            batch.CreateDate = DateTime.Now;
            batch.TenantId = 0;
            BatchResultMasterDao.Instance.Save(batch);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            HYMesManager.GetInstance().UploadSN();
        }

        private void SelectDataPath_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new();
            dialog.UseDescriptionForTitle = true;
            dialog.Description = "为新项目选择位置";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (string.IsNullOrEmpty(dialog.SelectedPath))
                {
                    MessageBox.Show("文件夹路径不能为空", "提示");
                    return;
                }
                ProjectShiYuanConfig.Instance.DataPath = dialog.SelectedPath;
            }
        }

        private void UploadSN(object sender, RoutedEventArgs e)
        {
            HYMesManager.GetInstance().UploadSN();
        }

        private void ValidateTemplate_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.Tag is TempResult tempResult && comboBox.SelectedValue is ValidateParam validateParam)
            {
                foreach (var item in validateParam.ValidateSingles)
                {
                    if (item.Model.Code == "CIE_x")
                    {
                        tempResult.X = new NumSet() { ValMin = item.ValMin, ValMax = item.ValMax };
                    }
                    if (item.Model.Code == "CIE_y")
                    {
                        tempResult.Y = new NumSet() { ValMin = item.ValMin, ValMax = item.ValMax };
                    }
                    if (item.Model.Code == "CIE_lv")
                    {
                        tempResult.Lv = new NumSet() { ValMin = item.ValMin, ValMax = item.ValMax };
                    }
                    if (item.Model.Code == "CIE_dw")
                    {
                        tempResult.Dw = new NumSet() { ValMin = item.ValMin, ValMax = item.ValMax };
                    }
                }
            }
        }

        private void ValidateTemplate_Initialized(object sender, EventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                comboBox.ItemsSource = TemplateComplyParam.Params.GetValue("Comply.CIE");
            }
        }

        private void ListViewResult_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedItem is PoiResultCIExyuvData poiResultCIExyuvData)
            {
                List<string> header = new() { "规则" , "结果" };
                List<string> bdHeader = new() {"Rule.RType" , "Result" };


                if (ListViewValue.View is GridView gridView)
                {
                    gridView.Columns.Clear();
                    for (int i = 0; i < header.Count; i++)
                        gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                    ListViewValue.ItemsSource = poiResultCIExyuvData.ValidateSingles;
                }

            }
            
        }
    }
}
