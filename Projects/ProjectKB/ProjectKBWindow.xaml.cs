using ColorVision.Themes;
using log4net;
using System.Windows;
using System.Linq;
using ColorVision.Common.MVVM;
using System.Windows.Input;
using ColorVision.Engine.MQTT;
using System.Diagnostics;
using ST.Library.UI.NodeEditor;
using FlowEngineLib;
using ColorVision.Engine.Services;
using ColorVision.Engine.Templates.Flow;
using FlowEngineLib.Base;
using Panuon.WPF.UI;
using ColorVision.Common.Utilities;
using System.Reflection.Emit;
using ColorVision;
using ColorVision.Engine.Services.DAO;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Templates.Compliance;
using ColorVision.Engine.Templates.JND;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using MQTTMessageLib.Algorithm;
using System.Collections.ObjectModel;
using System.IO;
using ColorVision.Engine.Templates.Jsons.KB;
using Newtonsoft.Json;
using ColorVision.Engine.MySql.ORM;
using Org.BouncyCastle.Asn1.BC;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ListView;
using LiveChartsCore.Kernel;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Net;
using System.Globalization;
using System.Text;
using Org.BouncyCastle.Bcpg.OpenPgp;
using static NPOI.HSSF.Util.HSSFColor;
using System.Windows.Media;

namespace ProjectKB
{
    class KBvalue
    {
        public double Y { get; set; }
    }

    public class KBItem : ViewModelBase
    {
        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;

        public double Lv { get => _Lv; set { _Lv = value; NotifyPropertyChanged(); } }
        private double _Lv;
        public double Cx { get => _Cx; set { _Cx = value; NotifyPropertyChanged(); } }
        private double _Cx;
        public double Cy { get => _Cy; set { _Cy = value; NotifyPropertyChanged(); } }
        private double _Cy;
        public double Lc { get => _Lc; set { _Lc = value; NotifyPropertyChanged(); } }
        private double _Lc;
    }


    public class KBItemMaster : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(KBItemMaster));

        public static void SaveCsv(ObservableCollection<KBItemMaster> KBItems, string FileName)
        {
             var csvBuilder = new StringBuilder();
            List<string> properties = new()
    {
        "Id","Model", "SerialNumber", "POISet", "AvgLv", "MinLv", "MaxLv", "LvUniformity",
        "DarkestKey", "BrightestKey", "ColorDifference", "NbrFailedPts", "LvFailures",
        "LocalContrastFailures", "DarkKeyLocalContrast", "BrightKeyLocalContrast",
        "LocalDarkestKey", "LocalBrightestKey", "StrayLight", "Result", "DateTime",
        "ESC", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
        "HOME", "END", "DELETE", "calculator", "(", ")", "MOON", "~", "1", "2", "3", "4",
        "5", "6", "7", "8", "9", "0", "-", "=", "Backspace", "Num lock", "NUM /",
        "NUM *", "NUM -", "Tab", "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "[",
        "]", "\\", "Num 7", "Num 8", "Num 9", "Num +", "Capslk", "A", "S", "D", "F", "G",
        "H", "J", "K", "L", ";", "R68", "Enter", "Num 4", "Num 5", "Num 6", "L-Shift",
        "Z", "X", "C", "V", "B", "N", "M", "comma", ".", "/", "R-Shift", "Num 1", "Num 2",
        "Num 3", "Num enter", "L-ctrl", "Fn", "Win", "L-alt", "Space", "R-alt", "R-ctrl",
        "Pgup", "Up", "Pgdn", "Num 0", "Num .", "LEFT", "DN", "RIGHT", "LimitProfile",
        "MinKeyLv", "MaxKeyLv", "MinAvgLv", "MaxAvgLv", "MinLvUniformity",
        "MaxDarkLocalContrast", "MaxBrightLocalContrast", "MaxNbrFailedPoints",
        "MaxColorDifference", "MaxStrayLight", "MinInterKeyUniformity",
        "MinInterKeyColorUniformity"
    };

            // 写入列头
            csvBuilder.AppendLine(string.Join(",", properties));

            // 写入数据行
            foreach (var item in KBItems)
            {
                if (item.SN.Contains(',') || item.SN.Contains('"'))
                {
                    item.SN = $"\"{item.SN.Replace("\"", "\"\"")}\"";
                }
                List<string> values = new()
                {
                    item.Id.ToString(),
                    item.Model,
                    item.SN,
                    "",
                    item.AvgLv.ToString(CultureInfo.InvariantCulture),
                    item.MinLv.ToString(CultureInfo.InvariantCulture),
                    item.MaxLv.ToString(CultureInfo.InvariantCulture),
                    item.LvUniformity.ToString(CultureInfo.InvariantCulture),
                    item.DrakestKey.ToString(CultureInfo.InvariantCulture),
                    item.BrightestKey.ToString(CultureInfo.InvariantCulture),
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    item.Result.ToString(),
                    item.DateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                };
                csvBuilder.AppendLine(string.Join(",", values));
            }
            log.Info(csvBuilder.ToString());
            File.WriteAllText(FileName, csvBuilder.ToString(), Encoding.UTF8);
            
        }

        public KBItemMaster()
        {
            DateTime = DateTime.Now;
        }


        public ObservableCollection<KBItem> Items { get; set; } = new ObservableCollection<KBItem>();

        public int Id { get => _Id; set { _Id = value; NotifyPropertyChanged(); } }
        private int _Id;

        public string Model { get => _Model; set { _Model = value; NotifyPropertyChanged(); } }
        private string _Model =string.Empty;

        public string SN { get => _SN; set { _SN = value; NotifyPropertyChanged(); } }
        private string _SN;
        public string Exposure { get => _Exposure; set { _Exposure = value; NotifyPropertyChanged(); } }
        private string _Exposure;

        public double AvgLv { get => _AvgLv; set { _AvgLv = value; NotifyPropertyChanged(); } }
        private double _AvgLv;

        public double AvgC1 { get => _AvgC1; set { _AvgC1 = value; NotifyPropertyChanged(); } }
        private double _AvgC1;

        public double AvgC2 { get => _AvgC2; set { _AvgC2 = value; NotifyPropertyChanged(); } }
        private double _AvgC2;

        public double MinLv { get => _MinLv; set { _MinLv = value; NotifyPropertyChanged(); } }
        private double _MinLv;

        public double MaxLv { get => _MaxLv; set { _MaxLv = value; NotifyPropertyChanged(); } }
        private double _MaxLv;

        public string DrakestKey { get => _DrakestKey; set { _DrakestKey = value; NotifyPropertyChanged(); } }
        private string _DrakestKey = string.Empty;

        public string BrightestKey { get => _BrightestKey; set { _BrightestKey = value; NotifyPropertyChanged(); } }
        private string _BrightestKey = string.Empty;

        public int NbrFailPoints { get => _NbrFailPoints; set { _NbrFailPoints = value; NotifyPropertyChanged(); } }
        private int _NbrFailPoints;

        public double LvUniformity { get => _LvUniformity; set { _LvUniformity = value; NotifyPropertyChanged(); } }
        private double _LvUniformity;

        public double ColorUniformity { get => _ColorUniformity; set { _ColorUniformity = value; NotifyPropertyChanged(); } }
        private double _ColorUniformity;

        public bool Result { get => _Result; set { _Result = value; NotifyPropertyChanged(); } }
        private bool _Result;

        public DateTime DateTime { get => __DateTime; set { __DateTime = value; NotifyPropertyChanged(); } }
        private DateTime __DateTime = DateTime.Now;
    }

    /// <summary>
    /// Interaction logic for ProjectKBWindow.xaml
    /// </summary>
    public partial class ProjectKBWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ProjectKBWindow));
        public ObservableCollection<KBItemMaster> ViewResluts { get; set; } = new ObservableCollection<KBItemMaster>();


        public ProjectKBWindow()
        {
            InitializeComponent();
            this.ApplyCaption(false);
        }

        public STNodeEditor STNodeEditorMain { get; set; }
        private FlowEngineControl flowEngine;
        private Timer timer;
        Stopwatch stopwatch = new Stopwatch();
        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = ProjectKBConfig.Instance;
            listView1.ItemsSource = ViewResluts;

            MQTTConfig mQTTConfig = MQTTSetting.Instance.MQTTConfig;
            MQTTHelper.SetDefaultCfg(mQTTConfig.Host, mQTTConfig.Port, mQTTConfig.UserName, mQTTConfig.UserPwd, false, null);
            flowEngine = new FlowEngineControl(false);

            STNodeEditorMain = new STNodeEditor();
            STNodeEditorMain.LoadAssembly("FlowEngineLib.dll");
            flowEngine.AttachNodeEditor(STNodeEditorMain);

            FlowTemplate.SelectionChanged += (s, e) =>
            {
                if (FlowTemplate.SelectedIndex > -1)
                {
                    var tokens = ServiceManager.GetInstance().ServiceTokens;
                    foreach (var item in STNodeEditorMain.Nodes)
                    {
                        if (item is CVCommonNode algorithmNode)
                        {
                            algorithmNode.nodeRunEvent -= UpdateMsg;
                        }
                    }
                    flowEngine.LoadFromBase64(FlowParam.Params[FlowTemplate.SelectedIndex].Value.DataBase64, tokens);
                    foreach (var item in STNodeEditorMain.Nodes)
                    {
                        if (item is CVCommonNode algorithmNode)
                        {
                            algorithmNode.nodeRunEvent += UpdateMsg;
                        }
                    }
                }
            };

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

                        if (ProjectKBConfig.Instance.LastFlowTime == 0)
                        {
                            msg = Msg1 + Environment.NewLine + $"已经执行：{elapsedTime}";
                        }
                        else
                        {
                            long remainingMilliseconds = ProjectKBConfig.Instance.LastFlowTime - elapsedMilliseconds;
                            TimeSpan remaining = TimeSpan.FromMilliseconds(remainingMilliseconds);

                            string remainingTime = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}:{elapsed.Milliseconds:D4}";

                            msg = Msg1 + Environment.NewLine + $"已经执行：{elapsedTime}, 上次执行：{DisplayFlowConfig.Instance.LastFlowTime} ms, 预计还需要：{remainingTime}";
                        }

                        handler.UpdateMessage(msg);
                    }
                }
                catch
                {

                }

            });
        }

        IPendingHandler handler { get; set; }

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
                        if (DisplayFlowConfig.Instance.LastFlowTime == 0 || DisplayFlowConfig.Instance.LastFlowTime - elapsedMilliseconds < 0)
                        {
                            msg = Msg1 + Environment.NewLine + $"已经执行：{elapsedTime}";
                        }
                        else
                        {
                            long remainingMilliseconds = DisplayFlowConfig.Instance.LastFlowTime - elapsedMilliseconds;
                            TimeSpan remaining = TimeSpan.FromMilliseconds(remainingMilliseconds);
                            string remainingTime = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}:{elapsed.Milliseconds:D4}";

                            msg = Msg1 + Environment.NewLine + $"已经执行：{elapsedTime}, 上次执行：{DisplayFlowConfig.Instance.LastFlowTime} ms, 预计还需要：{remainingTime}";
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



        private void TestClick(object sender, RoutedEventArgs e)
        {
            RunTemplate();
        }

        public void RunTemplate()
        {
            if (FlowTemplate.SelectedValue is FlowParam flowParam)
            {
                string startNode = flowEngine.GetStartNodeName();
                if (!string.IsNullOrWhiteSpace(startNode))
                {
                    flowControl ??= new ColorVision.Engine.Templates.Flow.FlowControl(MQTTControl.GetInstance(), flowEngine);

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

                    BatchResultMasterModel batch = new();
                    batch.Name = string.IsNullOrEmpty(name) ? sn : name;
                    batch.Code = sn;
                    batch.CreateDate = DateTime.Now;
                    batch.TenantId = 0;
                    BatchResultMasterDao.Instance.Save(batch);
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


        private ColorVision.Engine.Templates.Flow.FlowControl flowControl;

        private void FlowControl_FlowCompleted(object? sender, EventArgs e)
        {
            flowControl.FlowCompleted -= FlowControl_FlowCompleted;
            handler?.Close();
            if (sender is FlowControlData FlowControlData)
            {
                if (FlowControlData.EventName == "Completed" || FlowControlData.EventName == "Canceled" || FlowControlData.EventName == "OverTime" || FlowControlData.EventName == "Failed")
                {
                    stopwatch.Stop();
                    timer.Change(Timeout.Infinite, 100); // 停止定时器
                    if (FlowControlData.EventName == "Completed")
                    {
                        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        bool sucess = true;
                        ProjectKBConfig.Instance.LastFlowTime = stopwatch.ElapsedMilliseconds;
                        log.Info($"流程执行Elapsed Time: {stopwatch.ElapsedMilliseconds} ms");
                        var Batch = BatchResultMasterDao.Instance.GetByCode(FlowControlData.SerialNumber);
                        KBItemMaster kBItem = new KBItemMaster();
                        kBItem.Model = FlowTemplate.Text;
                        kBItem.Id = Batch.Id;
                        kBItem.SN = SNtextBox.Text;
                        foreach (var item in AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id))
                        {
                            if (item.ImgFileType == AlgorithmResultType.KB || item.ImgFileType == AlgorithmResultType.KB_Raw)
                            {
                                var mod = TemplateJsonDao.Instance.GetByParam(new Dictionary<string, object>() { { "name", item.TName }, { "mm_id", 150 } });

                                KBJson kBJson = JsonConvert.DeserializeObject<KBJson>(mod.JsonVal);
                                log.Info(JsonConvert.SerializeObject(kBJson));
                                if (kBJson != null)
                                {
                                    foreach (var keyRect in kBJson.KBKeyRects)
                                    {
                                        KBItem kItem = new KBItem();
                                        kItem.Name = keyRect.Name;
                                        kBItem.Items.Add(kItem);
                                    }
                                    Task.Run(() =>
                                    {
                                        Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            if (File.Exists(item.ResultImagFile))
                                            {
                                                if (CVFileUtil.IsCIEFile(item.ResultImagFile))
                                                {
                                                    CVFileUtil.ReadCIEFileHeader(item.ResultImagFile, out CVCIEFile cVCIEFile);
                                                    kBItem.Exposure = JsonConvert.SerializeObject(cVCIEFile.exp);
                                                }
                                                ImageView.OpenImage(item.ResultImagFile);
                                            }
                                        });
                                    });
                                }
                            }
                            if (item.ImgFileType == AlgorithmResultType.POI_Y)
                            {
                                var pois = PoiPointResultDao.Instance.GetAllByPid(item.Id);
                                if (pois != null)
                                {
                                    foreach (var poi in pois)
                                    {
                                        var list = JsonConvert.DeserializeObject<KBvalue>(poi.Value);
                                        kBItem.Items.First(a => a.Name == poi.PoiName).Lv = list.Y;
                                    }
                                }

                            }
                        }

                        var maxKeyItem = kBItem.Items.OrderByDescending(item => item.Lv).FirstOrDefault();
                        var minLKey = kBItem.Items.OrderByDescending(item => item.Lv).FirstOrDefault();
                        kBItem.MaxLv = maxKeyItem.Lv;
                        kBItem.BrightestKey = maxKeyItem.Name;

                        kBItem.MinLv = minLKey.Lv;
                        kBItem.DrakestKey = minLKey.Name;
                        ViewResluts.Add(kBItem);
                        GenoutputText(kBItem);

                        string resultPath = ProjectKBConfig.Instance.ResultSavePath + $"\\{kBItem.SN}-{kBItem.DateTime:yyyyMMddHHmmssffff}.csv";
                        string result = $"{kBItem.SN},{(kBItem.Result? "Pass":"Fail")}, ,";
                        log.Info($"结果正在写入{resultPath},result:{result}");
                        File.WriteAllText(resultPath,result);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            string csvpath = ProjectKBConfig.Instance.ResultSavePath + "\\output.csv";
                            KBItemMaster.SaveCsv(ViewResluts, csvpath);
                            log.Info($"writecsv:{csvpath}");
                        });
                        //还原Modbus状态；
                        Task.Run(() =>
                        {
                            ModbusControl.GetInstance().SetRegisterValue(0);
                        });
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


        public void GenoutputText(KBItemMaster kmitemmaster)
        {
            string outtext = string.Empty;
            outtext += $"Model:{FlowTemplate.Text}" + Environment.NewLine; 
            outtext += $"SN:{SNtextBox.Text}" + Environment.NewLine;
            outtext += $"Poiints of Interest: " + Environment.NewLine;
            outtext += $"{DateTime.Now:yyyy/MM//dd HH:mm:ss}" + Environment.NewLine;
            outtext += Environment.NewLine;
            string title1 = "PT";
            string title2 = "Lv";
            string title3 = "Cx";
            string title4 = "Cy";
            string title5 = "Lv";
            outtext += $"{title1,-20}   {title2,10}   {title3,10}   {title5,10}" + Environment.NewLine;

            foreach (var item in kmitemmaster.Items)
            {
                string formattedString = $"[{item.Name}]";

                outtext += $"{formattedString,-20}   {item.Lv,-10:F4}   {item.Cx,10:F4}   {item.Lc * 100,10:F2}%" + Environment.NewLine;
            }
            //Random random = new Random();
            //foreach (var item in Enum.GetValues(typeof(System.Windows.Input.Key)).Cast<System.Windows.Input.Key>())
            //{
            //    string formattedString = $"[{item}]";
            //    outtext += $"{formattedString,-20}   {random.NextDouble():F4}   {random.NextDouble():F4}   {random.NextDouble() * 100:F2}%" + Environment.NewLine;
            //}

            outtext += Environment.NewLine;
            outtext += $"Min Lv= {kmitemmaster.MinLv} cd/m2" + Environment.NewLine;
            outtext += $"Max Lv= {kmitemmaster.MaxLv} cd/m2" + Environment.NewLine;
            outtext += $"Darkest Key= {kmitemmaster.DrakestKey}" + Environment.NewLine;
            outtext += $"Brightest Key= {kmitemmaster.BrightestKey} cd/m2" + Environment.NewLine;
            outtext += $"Avg Cx= {kmitemmaster.AvgC1}" + Environment.NewLine;
            outtext += $"Avg Cy= {kmitemmaster.AvgC2}" + Environment.NewLine;

            outtext += Environment.NewLine;
            outtext += $"Pass/Fail Criteria:" + Environment.NewLine;
            outtext += $"NbrFail Points={kmitemmaster.NbrFailPoints}" + Environment.NewLine;
            outtext += $"Avg Lv={kmitemmaster.AvgLv}" + Environment.NewLine;
            outtext += $"Lv Uniformity={kmitemmaster.LvUniformity}" + Environment.NewLine;
            outtext += $"Color Uniformity={kmitemmaster.LvUniformity}" + Environment.NewLine;

            outtext += kmitemmaster.Result ? "Pass" : "Fail" + Environment.NewLine;
            outputText.Background = kmitemmaster.Result ? Brushes.Lime : Brushes.Red;
            outputText.Text = outtext;
            SNtextBox.Focus();
        }


        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {

        }

        private void Button_Click_Clear(object sender, RoutedEventArgs e)
        {

        }

        private void listView1_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }

        private void listView1_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {

        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {

        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {

        }

        private void SNtextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {

        }

        private void SNtextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            new TestWindow().Show();
        }

        private void GridSplitter_DragCompleted1(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            ProjectKBConfig.Instance.SummaryInfo.Width = col1.ActualWidth;
            col1.Width = GridLength.Auto;
        }
    }
}