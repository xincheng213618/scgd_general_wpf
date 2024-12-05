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

namespace ProjectKB
{
    public class KBItem : ViewModelBase
    {
        public int Id { get => _Id; set { _Id = value; NotifyPropertyChanged(); } }
        private int _Id;
        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;

        public string SN { get => _SN; set { _SN = value; NotifyPropertyChanged(); } }
        private string _SN;

        public double Exposure { get => _Exposure; set { _Exposure = value; NotifyPropertyChanged(); } }
        private double _Exposure;

        public double AvgLv { get => _AvgLv; set { _AvgLv = value; NotifyPropertyChanged(); } }
        private double _AvgLv;

        public double AvgC1 { get => _AvgC1; set { _AvgC1 = value; NotifyPropertyChanged(); } }
        private double _AvgC1;

        public double AvgC2 { get => _AvgC2; set { _AvgC2 = value; NotifyPropertyChanged(); } }
        private double _AvgC2;

        public double MinLv { get => _MinLv; set { _MinLv = value; NotifyPropertyChanged(); } }
        private double _MinLv;
    }

    /// <summary>
    /// Interaction logic for ProjectKBWindow.xaml
    /// </summary>
    public partial class ProjectKBWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ProjectKBWindow));

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
                            algorithmNode.nodeRunEvent -= UpdateMsg;
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
                            msg = $"已经执行：{elapsedTime}";
                        }
                        else
                        {
                            long remainingMilliseconds = ProjectKBConfig.Instance.LastFlowTime - elapsedMilliseconds;
                            TimeSpan remaining = TimeSpan.FromMilliseconds(remainingMilliseconds);

                            string remainingTime = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}:{elapsed.Milliseconds:D4}";

                            msg = $"已经执行：{elapsedTime}, 上次执行：{ProjectKBConfig.Instance.LastFlowTime} ms, 预计还需要：{remainingTime}";
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

                        foreach (var item in AlgResultMasterDao.Instance.GetAllByBatchid(Batch.Id))
                        {
                            if (item.ImgFileType == AlgorithmResultType.KB|| item.ImgFileType == AlgorithmResultType.KB_Raw)
                            {
                                KBJson kBJson = JsonConvert.DeserializeObject<KBJson>(item.Params);
                                if (kBJson != null)
                                {
                                    if (File.Exists(item.ImgFile))
                                    {
                                        ImageView.OpenImage(item.ImgFile);
                                    }
                                }
                            }

                        }
                        Random random = new Random();
                        string outstr = string.Empty;
                        foreach (var item in Enum.GetValues(typeof(System.Windows.Input.Key)).Cast<System.Windows.Input.Key>())
                        {
                            string formattedString = $"[{item}]";

                            outstr += $"{formattedString,-20}   {random.NextDouble():F4}   {random.NextDouble():F4}   {random.NextDouble() * 100:F2}%" + Environment.NewLine;
                        }
                        outputText.Text = outstr;

                        SNtextBox.Focus();

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
    }
}