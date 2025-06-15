#pragma warning disable CS8602,CA1707
using ColorVision.Common.Utilities;
using ColorVision.Engine.Abstractions;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates.Compliance;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.JND;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.Engine.Templates.Validate;
using ColorVision.Themes;
using FlowEngineLib;
using FlowEngineLib.Base;
using log4net;
using Panuon.WPF.UI;
using ST.Library.UI.NodeEditor;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

        public STNodeEditor STNodeEditorMain { get; set; }
        private FlowEngineLib.FlowEngineControl flowEngine;
        private Timer timer;
        Stopwatch stopwatch = new Stopwatch();

        private void Window_Initialized(object sender, EventArgs e)
        {
            MQTTConfig mQTTConfig = MQTTSetting.Instance.MQTTConfig;
            MQTTHelper.SetDefaultCfg(mQTTConfig.Host, mQTTConfig.Port, mQTTConfig.UserName, mQTTConfig.UserPwd, false, null);
            flowEngine = new FlowEngineControl(false);

            STNodeEditorMain = new STNodeEditor();
            STNodeEditorMain.LoadAssembly("FlowEngineLib.dll");
            flowEngine.AttachNodeEditor(STNodeEditorMain);

            ListViewResult.ItemsSource = Results;

            FlowTemplate.ItemsSource = TemplateFlow.Params;
            FlowTemplate.SelectionChanged += (s, e) =>
            {
                if (FlowTemplate.SelectedIndex > -1)
                {
                    var tokens = MqttRCService.GetInstance().ServiceTokens;
                    foreach (var item in STNodeEditorMain.Nodes)
                    {
                        if (item is CVCommonNode algorithmNode)
                        {
                            algorithmNode.nodeRunEvent -= UpdateMsg;
                        }
                    }
                    flowEngine.LoadFromBase64(TemplateFlow.Params[FlowTemplate.SelectedIndex].Value.DataBase64, tokens);
                    foreach (var item in STNodeEditorMain.Nodes)
                    {
                        if (item is CVCommonNode algorithmNode)
                        {
                            algorithmNode.nodeRunEvent += UpdateMsg;
                        }
                    }
                }
            };

            this.DataContext = ProjectShiYuanConfig.Instance;

            timer = new Timer(TimeRun, null, 0, 100);
            timer.Change(Timeout.Infinite, 100); // 停止定时器
        }

        private void TimeRun(object? state)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (handler != null)
                    {
                        long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                        TimeSpan elapsed = TimeSpan.FromMilliseconds(elapsedMilliseconds);

                        string elapsedTime = $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}:{elapsed.Milliseconds:D4}";
                        string msg;

                        if (ProjectShiYuanConfig.Instance.LastFlowTime == 0)
                        {
                            msg = Msg1 + Environment.NewLine + $"已经执行：{elapsedTime}";
                        }
                        else
                        {
                            long remainingMilliseconds = ProjectShiYuanConfig.Instance.LastFlowTime - elapsedMilliseconds;
                            TimeSpan remaining = TimeSpan.FromMilliseconds(remainingMilliseconds);

                            string remainingTime = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}:{elapsed.Milliseconds:D4}";

                            msg = Msg1 + Environment.NewLine + $"已经执行：{elapsedTime}, 上次执行：{ProjectShiYuanConfig.Instance.LastFlowTime} ms, 预计还需要：{remainingTime}";
                        }

                        handler.UpdateMessage(msg);
                    }
                }
                catch
                {

                }

            });
        }


        string Msg1;
        private void UpdateMsg(object? sender)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (handler != null)
                    {
                        long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                        TimeSpan elapsed = TimeSpan.FromMilliseconds(elapsedMilliseconds);
                        string elapsedTime = $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}:{elapsed.Milliseconds:D4}";
                        string msg;
                        if (ProjectShiYuanConfig.Instance.LastFlowTime == 0 || ProjectShiYuanConfig.Instance.LastFlowTime - elapsedMilliseconds < 0)
                        {
                            msg = Msg1 + Environment.NewLine + $"已经执行：{elapsedTime}";
                        }
                        else
                        {
                            long remainingMilliseconds = ProjectShiYuanConfig.Instance.LastFlowTime - elapsedMilliseconds;
                            TimeSpan remaining = TimeSpan.FromMilliseconds(remainingMilliseconds);
                            string remainingTime = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}:{elapsed.Milliseconds:D4}";

                            msg = Msg1 + Environment.NewLine + $"已经执行：{elapsedTime}, 上次执行：{ProjectShiYuanConfig.Instance.LastFlowTime} ms, 预计还需要：{remainingTime}";
                        }
                        handler.UpdateMessage(msg);
                    }
                }
                catch
                {

                }
            });
        }

        private void UpdateMsg(object sender, FlowEngineNodeRunEventArgs e)
        {
            if (sender is CVCommonNode algorithmNode)
            {
                if (e != null)
                {
                    Msg1 = algorithmNode.Title;
                    UpdateMsg(sender);
                }
            }
        }


        private FlowControl flowControl;

        private IPendingHandler handler;

        private void FlowControl_FlowCompleted(object? sender, FlowControlData  flowControlData)
        {
            flowControl.FlowCompleted -= FlowControl_FlowCompleted;
            handler?.Close();
            stopwatch.Stop();
            timer.Change(Timeout.Infinite, 100); // 停止定时器


            if (flowControlData.EventName == "Completed")
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                bool sucess = true;
                ProjectShiYuanConfig.Instance.LastFlowTime = stopwatch.ElapsedMilliseconds;
                log.Info($"流程执行Elapsed Time: {stopwatch.ElapsedMilliseconds} ms");
                var Batch = BatchResultMasterDao.Instance.GetByCode(flowControlData.SerialNumber);
                if (Batch != null)
                {
                    var resultMaster = AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id);
                    foreach (var item in resultMaster)
                    {
                        if (item.ImgFileType == AlgorithmResultType.Compliance_Math_JND)
                        {
                            var complianceJNDModels = ComplianceJNDDao.Instance.GetAllByPid(item.Id);
                            log.Info($"获取JDN信息：  Id{item.Id},nums{complianceJNDModels.Count}");
                            ListViewJNDresult.ItemsSource = complianceJNDModels;

                            foreach (var item222 in complianceJNDModels)
                            {
                                sucess = sucess && item222.Validate;
                            }
                        }
                    }

                    List<PoiResultCIExyuvData> PoiResultCIExyuvDatas = new List<PoiResultCIExyuvData>();
                    foreach (var item in resultMaster)
                    {
                        if (item.ImgFileType == AlgorithmResultType.POI_XYZ)
                        {
                            List<PoiPointResultModel> POIPointResultModels = PoiPointResultDao.Instance.GetAllByPid(item.Id);

                            foreach (var pointResultModel in POIPointResultModels)
                            {
                                PoiResultCIExyuvData poiResultCIExyuvData = new PoiResultCIExyuvData(pointResultModel);
                                PoiResultCIExyuvDatas.Add(poiResultCIExyuvData);
                            }

                        }

                        if (item.ImgFileType == AlgorithmResultType.OLED_JND_CalVas)
                        {
                            ObservableCollection<ViewRsultJND> ViewRsultJNDs = new ObservableCollection<ViewRsultJND>();
                            foreach (var model in PoiPointResultDao.Instance.GetAllByPid(item.Id))
                            {
                                ViewRsultJNDs.Add(new ViewRsultJND(model));
                            }
                            if (Directory.Exists(ProjectShiYuanConfig.Instance.DataPath))
                            {
                                string FilePath = ProjectShiYuanConfig.Instance.DataPath + "\\" + timestamp + "_" + ProjectShiYuanConfig.Instance.SN + "_JND" + ".csv";
                                ViewRsultJND.SaveCsv(ViewRsultJNDs, FilePath);
                            }

                            if (Directory.Exists(ProjectShiYuanConfig.Instance.DataPath))
                            {
                                string sourceFile = item.ImgFile;
                                // 获取目标目录路径
                                string destinationDirectory = ProjectShiYuanConfig.Instance.DataPath;
                                // 获取源文件的文件名
                                string fileName = Path.GetFileName(sourceFile);
                                // 构造目标文件的完整路径
                                string destinationFile = Path.Combine(destinationDirectory, fileName);
                                try
                                {
                                    // 复制文件到目标路径
                                    File.Copy(sourceFile, destinationFile, true);
                                    log.Info(sourceFile + "JND输入文件复制成功");
                                }
                                catch (Exception ex)
                                {
                                    log.Info(sourceFile + "文件复制失败");
                                }
                            }
                        }
                    }

                    ListViewResult.ItemsSource = PoiResultCIExyuvDatas;

                    if (Directory.Exists(ProjectShiYuanConfig.Instance.DataPath))
                    {
                        foreach (var item in STNodeEditorMain.Nodes)
                        {
                            if (item is FlowEngineLib.Node.Algorithm.TPAlgorithmNode tapnode)
                            {
                                if (File.Exists(tapnode.ImgFileName))
                                {
                                    string sourceFile = tapnode.ImgFileName;
                                    // 获取目标目录路径
                                    string destinationDirectory = ProjectShiYuanConfig.Instance.DataPath;
                                    // 获取源文件的文件名
                                    string fileName = Path.GetFileNameWithoutExtension(sourceFile) + "_" + timestamp + Path.GetExtension(sourceFile);
                                    // 构造目标文件的完整路径
                                    string destinationFile = Path.Combine(destinationDirectory, fileName);
                                    try
                                    {
                                        // 复制文件到目标路径
                                        File.Copy(sourceFile, destinationFile, true);
                                        log.Info(sourceFile + "JNDCAD文件复制成功");
                                    }
                                    catch (Exception ex)
                                    {
                                        log.Info(sourceFile + "文件复制失败");
                                    }

                                }
                            }
                        }

                        string FilePath = ProjectShiYuanConfig.Instance.DataPath + "\\" + timestamp + "_" + ProjectShiYuanConfig.Instance.SN + "_POI" + ".csv";
                        PoiResultCIExyuvData.SaveCsv(new ObservableCollection<PoiResultCIExyuvData>(PoiResultCIExyuvDatas), FilePath);

                    }

                    if (sucess)
                    {

                        ResultText.Text = "OK";
                        ResultText.Foreground = Brushes.Red;

                        string h_gap = "C:\\Windows\\System32\\pic\\h_gap.tif";
                        if (File.Exists(h_gap))
                        {
                            File.Copy(h_gap, ProjectShiYuanConfig.Instance.DataPath + "\\" + timestamp + "_" + ProjectShiYuanConfig.Instance.SN + "_h_gap_1" + ".tif", true);
                            BitmapImage bitmapImage = new BitmapImage(new Uri(h_gap));
                            HImage hImage = bitmapImage.ToHImage();

                            int ret = OpenCVMediaHelper.M_PseudoColor(hImage, out HImage hImageProcessed, 0, 65535, ColormapTypes.COLORMAP_JET, 1);
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (ret == 0)
                                {
                                    var image = hImageProcessed.ToWriteableBitmap();
                                    image.SaveImageSourceToFile(ProjectShiYuanConfig.Instance.DataPath + "\\" + timestamp + "_" + ProjectShiYuanConfig.Instance.SN + "_h_gap" + ".tif");
                                    OpenCVMediaHelper.M_FreeHImageData(hImageProcessed.pData);
                                    hImageProcessed.pData = IntPtr.Zero;

                                }
                            });
                        }

                        string g_gap = "C:\\Windows\\System32\\pic\\v_gap.tif";
                        if (File.Exists(g_gap))
                        {
                            File.Copy(g_gap, ProjectShiYuanConfig.Instance.DataPath + "\\" + timestamp + "_" + ProjectShiYuanConfig.Instance.SN + "_v_gap_1" + ".tif", true);
                            BitmapImage bitmapImage = new BitmapImage(new Uri(g_gap));
                            HImage hImage = bitmapImage.ToHImage();

                            int ret = OpenCVMediaHelper.M_PseudoColor(hImage, out HImage hImageProcessed, 0, 65535, ColormapTypes.COLORMAP_JET, 1);
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (ret == 0)
                                {
                                    var image = hImageProcessed.ToWriteableBitmap();
                                    image.SaveImageSourceToFile(ProjectShiYuanConfig.Instance.DataPath + "\\" + timestamp + "_" + ProjectShiYuanConfig.Instance.SN + "_v_gap" + ".tif");
                                    OpenCVMediaHelper.M_FreeHImageData(hImageProcessed.pData);
                                    hImageProcessed.pData = IntPtr.Zero;

                                }
                            });
                        }
                        string luminance = "C:\\Windows\\System32\\pic\\luminance.tif";
                        if (File.Exists(luminance))
                        {
                            File.Copy(luminance, ProjectShiYuanConfig.Instance.DataPath + "\\" + timestamp + "_" + ProjectShiYuanConfig.Instance.SN + "luminance_1" + ".tif", true);
                        }
                    }
                    else
                    {

                        ResultText.Text = "NG";
                        ResultText.Foreground = Brushes.Blue;
                    }





                }
                else
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), "找不到批次号", "ColorVision");
                }
            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "流程运行失败" + flowControlData.EventName, "ColorVision");
            }
        }
        

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (FlowTemplate.SelectedValue is FlowParam flowParam)
            {
                string startNode = flowEngine.GetStartNodeName();
                if (!string.IsNullOrWhiteSpace(startNode))
                {
                    flowControl ??= new Engine.Templates.Flow.FlowControl(MQTTControl.GetInstance(), flowEngine);

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
                    stopwatch.Reset();
                    stopwatch.Start();
                    flowControl.Start(sn);
                    timer.Change(0, 100); // 启动定时器
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
                comboBox.ItemsSource = TemplateComplyParam.CIEParams.GetValue("Comply.CIE");
            }
        }

        private void ListViewResult_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedItem is PoiResultCIExyuvData poiResultCIExyuvData)
            {
                List<string> header = new() { "规则", "结果", "值", "Max", "Min" };
                List<string> bdHeader = new() { "Rule.RType", "Result", "Value", "Rule.Max", "Rule.Min" };


                if (ListViewValue.View is GridView gridView)
                {
                    gridView.Columns.Clear();
                    for (int i = 0; i < header.Count; i++)
                        gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                }

            }
           
        }


        private void Open_Click(object sender, RoutedEventArgs e)
        {
            Common.Utilities.PlatformHelper.OpenFolder(ProjectShiYuanConfig.Instance.DataPath);
        }

        private void ListViewJND_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedItem is ComplianceJNDModel complianceJNDModel)
            {
                List<string> header = new() { "规则", "结果","值"  , "Max" ,"Min"};
                List<string> bdHeader = new() { "Rule.RType", "Result" , "Value", "Rule.Max", "Rule.Min" };


                if (ListViewJNDValue.View is GridView gridView)
                {
                    gridView.Columns.Clear();
                    for (int i = 0; i < header.Count; i++)
                        gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                    ListViewJNDValue.ItemsSource = complianceJNDModel.ValidateSingles;
                }

            }
        }
    }
}
