#pragma warning disable CS8602,CA1707
using ColorVision.Common.Utilities;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Themes;
using ColorVision.UI;
using FlowEngineLib;
using log4net;
using Panuon.WPF.UI;
using ST.Library.UI.NodeEditor;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.Engine.Templates.Validate;
using ColorVision.Engine.Templates.Compliance;
using ColorVision.Engine.Services.RC;
using FlowEngineLib.Base;
using ColorVision.Engine.Interfaces;

namespace ColorVision.Projects.ProjectHeyuan
{


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
    /// ProjectHeyuanWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ProjectHeyuanWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ProjectHeyuanWindow));

        public ObservableCollection<TempResult> Settings { get; set; } = new ObservableCollection<TempResult>();
        public ObservableCollection<TempResult> Results { get; set; } = new ObservableCollection<TempResult>();

        private FlowEngineLib.FlowEngineControl flowEngine;
        private Timer timer;
        Stopwatch stopwatch = new Stopwatch();

        public ProjectHeyuanWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }
        STNodeEditor STNodeEditorMain = new STNodeEditor();

        private void Window_Initialized(object sender, EventArgs e)
        {
            MQTTConfig mQTTConfig = MQTTSetting.Instance.MQTTConfig;
            MQTTHelper.SetDefaultCfg(mQTTConfig.Host, mQTTConfig.Port, mQTTConfig.UserName, mQTTConfig.UserPwd, false, null);
            flowEngine = new FlowEngineControl(false);

            STNodeEditorMain.LoadAssembly("FlowEngineLib.dll");
            flowEngine.AttachNodeEditor(STNodeEditorMain);

            ListViewSetting.ItemsSource = Settings;
            ListViewResult.ItemsSource = Results;
            ComboBoxSer.ItemsSource = SerialPort.GetPortNames();
            ComboBoxSer.SelectedIndex = 0;

            ListViewMes.ItemsSource = HYMesManager.GetInstance().SerialMsgs;
            FlowTemplate.ItemsSource = TemplateFlow.Params;
            FlowTemplate.SelectionChanged += (s, e) => Refresh();

            List<string> strings = new List<string>() { "White", "Blue", "Red", "Orange" };
            foreach (var item in strings)
            {
                Settings.Add(new TempResult() { Name = item });
            }

            this.DataContext = HYMesManager.GetInstance();

            timer = new Timer(TimeRun, null, 0, 100);
            timer.Change(Timeout.Infinite, 100); // 停止定时器
        }

        public void Refresh()
        {
            if (FlowTemplate.SelectedIndex < 0) return;

            try
            {

                foreach (var item in STNodeEditorMain.Nodes.OfType<CVCommonNode>())
                    item.nodeRunEvent -= UpdateMsg;

                flowEngine.LoadFromBase64(string.Empty);
                flowEngine.LoadFromBase64(TemplateFlow.Params[FlowTemplate.SelectedIndex].Value.DataBase64, MqttRCService.GetInstance().ServiceTokens);

                foreach (var item in STNodeEditorMain.Nodes.OfType<CVCommonNode>())
                    item.nodeRunEvent += UpdateMsg;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                flowEngine.LoadFromBase64(string.Empty);
            }
        }


        private void TimeRun(object? state)
        {
            UpdateMsg(state);
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
                        if (HYMesManager.GetInstance().LastFlowTime == 0 || HYMesManager.GetInstance().LastFlowTime - elapsedMilliseconds < 0)
                        {
                            msg = Msg1 + Environment.NewLine + $"已经执行：{elapsedTime}";
                        }
                        else
                        {
                            long remainingMilliseconds = HYMesManager.GetInstance().LastFlowTime - elapsedMilliseconds;
                            TimeSpan remaining = TimeSpan.FromMilliseconds(remainingMilliseconds);
                            string remainingTime = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}:{elapsed.Milliseconds:D4}";

                            msg = Msg1 + Environment.NewLine + $"已经执行：{elapsedTime}, 上次执行：{HYMesManager.GetInstance().LastFlowTime} ms, 预计还需要：{remainingTime}";
                        }
                        if (flowControl.IsFlowRun)
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

        private void FlowControl_FlowCompleted(object? sender, EventArgs e)
        {
            flowControl.FlowCompleted -= FlowControl_FlowCompleted;
            handler?.Close();

            stopwatch.Stop();
            timer.Change(Timeout.Infinite, 100); // 停止定时器
            HYMesManager.GetInstance().LastFlowTime = stopwatch.ElapsedMilliseconds;
            log.Info($"流程执行Elapsed Time: {stopwatch.ElapsedMilliseconds} ms");

            Application.Current.Dispatcher.Invoke((Action)(() =>
            {
                if (sender is FlowControlData FlowControlData)
                {
                    if (FlowControlData.EventName == "Completed" || FlowControlData.EventName == "Canceled" || FlowControlData.EventName == "OverTime" || FlowControlData.EventName == "Failed")
                    {

                        if (FlowControlData.EventName == "Completed")
                        {
                            Results.Clear();
                            var Batch = BatchResultMasterDao.Instance.GetByCode(FlowControlData.SerialNumber);
                            if (Batch != null)
                            {
                                var resultMaster = AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id);
                                List<PoiResultCIExyuvData> PoiResultCIExyuvDatas = new List<PoiResultCIExyuvData>();
                                List<ComplianceXYZModel> complianceXYZModels = new List<ComplianceXYZModel>();
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
                                    if (item.ImgFileType == AlgorithmResultType.Compliance_Math_CIE_XYZ)
                                    {
                                        var lists = ComplianceXYZDao.Instance.GetAllByPid(item.Id);
                                        complianceXYZModels.AddRange(lists);
                                    }

                                }

                                Results.Clear();
                                if (PoiResultCIExyuvDatas.Count == 4)
                                {
                                    var record = new DataRecord
                                    {
                                        Model = HYMesManager.Config.TestName,
                                        ProductID = HYMesManager.GetInstance().SN,
                                        Date = DateTime.Now,
                                        Time = DateTime.Now.TimeOfDay,
                                    };


                                    List<string> strings = new List<string>() { "White", "Blue", "Red", "Orange" };
                                    for (int i = 0; i < PoiResultCIExyuvDatas.Count; i++)
                                    {
                                        var poiResultCIExyuvData1 = PoiResultCIExyuvDatas[i];

                                        var ValidateSinglesmodel = complianceXYZModels.FirstOrDefault(a => a.Name == poiResultCIExyuvData1.Name);

                                        poiResultCIExyuvData1.POIPointResultModel.Value = ValidateSinglesmodel?.ValidateResult;

                                        TempResult tempResult1 = new TempResult() { Name = poiResultCIExyuvData1.Name };
                                        tempResult1.X = new NumSet() { Value = (float)poiResultCIExyuvData1.x };
                                        tempResult1.Y = new NumSet() { Value = (float)poiResultCIExyuvData1.y };
                                        tempResult1.Lv = new NumSet() { Value = (float)poiResultCIExyuvData1.Y };
                                        tempResult1.Dw = new NumSet() { Value = (float)poiResultCIExyuvData1.Wave };

                                        //if (poiResultCIExyuvData1.ValidateSingles != null)
                                        //{
                                        //    foreach (var item in poiResultCIExyuvData1.ValidateSingles)
                                        //    {
                                        //        if (item.Rule.RType == ValidateRuleType.CIE_x)
                                        //        {
                                        //            tempResult1.Result = tempResult1.Result && item.Result == ValidateRuleResultType.M;
                                        //        }
                                        //        if (item.Rule.RType == ValidateRuleType.CIE_y)
                                        //        {
                                        //            tempResult1.Result = tempResult1.Result && item.Result == ValidateRuleResultType.M;
                                        //        }
                                        //        if (item.Rule.RType == ValidateRuleType.CIE_lv)
                                        //        {
                                        //            tempResult1.Result = tempResult1.Result && item.Result == ValidateRuleResultType.M;
                                        //        }
                                        //        if (item.Rule.RType == ValidateRuleType.Wave)
                                        //        {
                                        //            tempResult1.Result = tempResult1.Result && item.Result == ValidateRuleResultType.M;
                                        //        }
                                        //    }
                                        //}
                                        //else
                                        //{
                                        //    MessageBox.Show(Application.Current.GetActiveWindow(), $"{poiResultCIExyuvData1.Name}，没有配置校验模板", "ColorVision");
                                        //}

                                        Results.Add(tempResult1);
                                    }


                                    var sortedResults = Results.OrderBy(r => strings.IndexOf(r.Name)).ToList();
                                    Results.Clear();
                                    bool IsOK = true;
                                    List<string> ngstring = new List<string>();
                                    foreach (var result in sortedResults)
                                    {
                                        IsOK = IsOK && result.Result;

                                        if (!result.Result)
                                        {
                                            if (result.Name.Contains("White"))
                                                ngstring.Add("errorW");
                                            if (result.Name.Contains("Blue"))
                                                ngstring.Add("errorB");
                                            if (result.Name.Contains("Red"))
                                                ngstring.Add("errorR");
                                            if (result.Name.Contains("Orange"))
                                                ngstring.Add("errorO");
                                        }
                                        log.Info(string.Join(",", ngstring));
                                        Results.Add(result);
                                    }
                                    record.White_x = Results[0].X.Value;
                                    record.White_y = Results[0].Y.Value;
                                    record.White_lv = Results[0].Lv.Value;
                                    record.White_wl = Results[0].Dw.Value;
                                    record.White_Result = Results[0].Result ? "Pass" : "Fail";
                                    record.Blue_x = Results[1].X.Value;
                                    record.Blue_y = Results[1].Y.Value;
                                    record.Blue_lv = Results[1].Lv.Value;
                                    record.Blue_wl = Results[1].Dw.Value;
                                    record.Blue_Result = Results[1].Result ? "Pass" : "Fail";
                                    record.Red_x = Results[2].X.Value;
                                    record.Red_y = Results[2].Y.Value;
                                    record.Red_lv = Results[2].Lv.Value;
                                    record.Red_wl = Results[2].Dw.Value;
                                    record.Red_Result = Results[2].Result ? "Pass" : "Fail";
                                    record.Orange_x = Results[3].X.Value;
                                    record.Orange_y = Results[3].Y.Value;
                                    record.Orange_lv = Results[3].Lv.Value;
                                    record.Orange_wl = Results[3].Dw.Value;
                                    record.Orange_Result = Results[3].Result ? "Pass" : "Fail";
                                    record.Final_Result = IsOK ? "Pass" : "Fail";
                                    if (IsOK)
                                    {
                                        ResultText.Text = "PASS";
                                        ResultText.Foreground = Brushes.Blue;
                                        HYMesManager.GetInstance().UploadMes(Results);
                                    }
                                    else
                                    {
                                        ResultText.Text = "Fail";
                                        ResultText.Foreground = Brushes.Red;
                                        HYMesManager.GetInstance().Results = Results;
                                        if (MessageBox.Show(Application.Current.GetActiveWindow(), "是否NG过站", "Heyuan", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                                        {
                                            log.Info("ng过站");
                                            HYMesManager.GetInstance().UploadNG(string.Join(",", ngstring));
                                        }
                                    }

                                    if (Directory.Exists(HYMesManager.Config.DataPath))
                                    {
                                        string FilePath = HYMesManager.Config.DataPath + "\\" + DateTime.Now.ToString("yyyy-MM-dd") + "_" + HYMesManager.Config.TestName + "_" + Environment.MachineName + ".csv";
                                        CsvHandler csvHandler = new CsvHandler(FilePath);

                                        csvHandler.SaveRecord(record);
                                        // 清空产品编号
                                        TextBoxSn.Text = string.Empty;
                                        // 将焦点移动到产品编号输入框
                                        TextBoxSn.Focus();
                                    }
                                    log.Debug("mes 已经上传");
                                }
                                else
                                {
                                    HYMesManager.GetInstance().UploadNG("流程结果数据错误");
                                    MessageBox.Show(Application.Current.GetActiveWindow(), "流程结果数据错误", "ColorVision");
                                }
                            }
                            else
                            {
                                HYMesManager.GetInstance().UploadNG("找不到批次号");
                                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到批次号", "ColorVision");
                            }
                        }
                        else
                        {
                            HYMesManager.GetInstance().UploadNG("流程运行失败");
                            MessageBox.Show(Application.Current.GetActiveWindow(), "流程运行失败" + FlowControlData.EventName, "ColorVision");
                        }
                    }
                    else
                    {
                        HYMesManager.GetInstance().UploadNG("流程运行失败");
                        MessageBox.Show(Application.Current.GetActiveWindow(), "流程运行失败" + FlowControlData.EventName, "ColorVision");
                    }

                }
                else
                {
                    HYMesManager.GetInstance().UploadNG("流程运行异常");
                    MessageBox.Show(Application.Current.GetActiveWindow(), "1", "ColorVision");
                }
            }));

        }
        

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(HYMesManager.GetInstance().SN))
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "产品编号为空，在运行前请配置产品编号");
                return;
            }

            if (!HYMesManager.Config.IsAutoUploadSn)
            {
                log.Info($"没有勾选自动上传，现在上传SN:{HYMesManager.GetInstance().SN}");
                HYMesManager.GetInstance().UploadSN();
            }


            if (FlowTemplate.SelectedValue is FlowParam flowParam)
            {
                string startNode = flowEngine.GetStartNodeName();
                if (!string.IsNullOrWhiteSpace(startNode))
                {
                    Refresh();
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
            if (!HYMesManager.GetInstance().IsConnect)
            {
                int i = HYMesManager.GetInstance().OpenPort(ComboBoxSer.Text);
            }
            else
            {
                HYMesManager.GetInstance().Close();
            }
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
                HYMesManager.Config.DataPath = dialog.SelectedPath;
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
                    if (item.Model.Code == "Wave")
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

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            WindowLog windowLog = new WindowLog() { Owner = Application.Current.GetActiveWindow() };
            windowLog.Show();
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            Common.Utilities.PlatformHelper.OpenFolder(HYMesConfig.Instance.DataPath);
        }
    }
}
