using ColorVision.Common.MVVM;
using ColorVision.Engine.Interfaces;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Jsons.BlackMura;
using FlowEngineLib;
using FlowEngineLib.Base;
using log4net;
using Panuon.WPF.UI;
using ST.Library.UI.NodeEditor;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ProjectBlackMura
{

    public class BlackMuraResult:ViewModelBase
    {
        public int Id { get => _Id; set { _Id = value; NotifyPropertyChanged(); } }
        private int _Id;

        public string Model { get => _Model; set { _Model = value; NotifyPropertyChanged(); } }
        private string _Model = string.Empty;

        public string SN { get => _SN; set { _SN = value; NotifyPropertyChanged(); } }
        private string _SN;
        public string WhiteFilePath { get => _WhiteFilePath; set { _WhiteFilePath = value; NotifyPropertyChanged(); } }
        private string _WhiteFilePath = string.Empty;

        public string BlackFilePath { get => _BlackFilePath; set { _BlackFilePath = value; NotifyPropertyChanged(); } }
        private string _BlackFilePath = string.Empty;

        public bool Result { get => _Result; set { _Result = value; NotifyPropertyChanged(); } }
        private bool _Result = true;

    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MainWindow));

        public static ObservableCollection<BlackMuraResult> ViewResluts => ProjectBlackMuraConfig.Instance.ViewResluts;

        public MainWindow()
        {
            InitializeComponent();

        }
        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = ProjectBlackMuraConfig.Instance;
            listView1.ItemsSource = ViewResluts;
            InitFlow();
        }

        #region FlowRun
        public STNodeEditor STNodeEditorMain { get; set; }
        private FlowEngineControl flowEngine;
        private Timer timer;
        Stopwatch stopwatch = new Stopwatch();

        public void InitFlow()
        {
            MQTTConfig mqttcfg = MQTTSetting.Instance.MQTTConfig;
            MQTTHelper.SetDefaultCfg(mqttcfg.Host, mqttcfg.Port, mqttcfg.UserName, mqttcfg.UserPwd, false, null);
            flowEngine = new FlowEngineControl(false);
            STNodeEditorMain = new STNodeEditor();
            STNodeEditorMain.LoadAssembly("FlowEngineLib.dll");
            flowEngine.AttachNodeEditor(STNodeEditorMain);

            FlowTemplate.SelectionChanged += (s, e) =>
            {
                if (ProjectBlackMuraConfig.Instance.TemplateSelectedIndex > -1)
                {
                    string Name = TemplateFlow.Params[ProjectBlackMuraConfig.Instance.TemplateSelectedIndex].Key;
                    if (ProjectBlackMuraConfig.Instance.JudgeConfigs.TryGetValue(Name, out JudgeConfig sPECConfig))
                    {
                        ProjectBlackMuraConfig.Instance.JudgeConfig = sPECConfig;
                    }
                    else
                    {
                        sPECConfig = new JudgeConfig();
                        ProjectBlackMuraConfig.Instance.JudgeConfigs.TryAdd(Name, sPECConfig);
                        ProjectBlackMuraConfig.Instance.JudgeConfig = sPECConfig;
                    }

                }
                Refresh();
            };
            timer = new Timer(TimeRun, null, 0, 500);
            timer.Change(Timeout.Infinite, 500); // 停止定时器

            this.Closed += (s, e) =>
            {
                timer.Change(Timeout.Infinite, 500); // 停止定时器
                timer?.Dispose();
            };
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
        IPendingHandler handler { get; set; }
        string Msg1;
        private void UpdateMsg(object? sender)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    if (handler != null)
                    {
                        long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                        TimeSpan elapsed = TimeSpan.FromMilliseconds(elapsedMilliseconds);
                        string elapsedTime = $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}:{elapsed.Milliseconds:D4}";
                        string msg;
                        if (ProjectBlackMuraConfig.Instance.LastFlowTime == 0 || ProjectBlackMuraConfig.Instance.LastFlowTime - elapsedMilliseconds < 0)
                        {
                            msg = Msg1 + Environment.NewLine + $"已经执行：{elapsedTime}";
                        }
                        else
                        {
                            long remainingMilliseconds = ProjectBlackMuraConfig.Instance.LastFlowTime - elapsedMilliseconds;
                            TimeSpan remaining = TimeSpan.FromMilliseconds(remainingMilliseconds);
                            string remainingTime = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}:{elapsed.Milliseconds:D4}";

                            msg = Msg1 + Environment.NewLine + $"已经执行：{elapsedTime}, 上次执行：{ProjectBlackMuraConfig.Instance.LastFlowTime} ms, 预计还需要：{remainingTime}";
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
        private void TestClick(object sender, RoutedEventArgs e)
        {
            RunTemplate();
        }
        bool LastCompleted = true;

        public void RunTemplate()
        {
            if (flowControl != null && flowControl.IsFlowRun) return;
            if (FlowTemplate.SelectedValue is not FlowParam flowParam)
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "流程为空，请选择流程运行", "ColorVision");
                return;
            }
            ;

            string startNode = flowEngine.GetStartNodeName();
            if (string.IsNullOrWhiteSpace(startNode))
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "找不到完整流程，运行失败", "ColorVision");
                return;
            }
            ;
            if (!LastCompleted)
            {
                Refresh();
            }
            LastCompleted = false;
            flowControl ??= new FlowControl(MQTTControl.GetInstance(), flowEngine);

            handler = PendingBox.Show(this, "TTL:" + "0", "流程运行", true);
            handler.Cancelling -= Handler_Cancelling;
            handler.Cancelling += Handler_Cancelling;
            flowControl.FlowCompleted += FlowControl_FlowCompleted;
            string sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            stopwatch.Reset();
            stopwatch.Start();
            flowControl.Start(sn);
            timer.Change(0, 500); // 启动定时器
            string name = string.Empty;

            BatchResultMasterModel batch = new();
            batch.Name = string.IsNullOrEmpty(name) ? sn : name;
            batch.Code = sn;
            batch.CreateDate = DateTime.Now;
            batch.TenantId = 0;
            BatchResultMasterDao.Instance.Save(batch);
        }

        private void Handler_Cancelling(object? sender, CancelEventArgs e)
        {
            stopwatch.Stop();
            timer.Change(Timeout.Infinite, 500); // 停止定时器
            flowControl.Stop();
        }
        private FlowControl flowControl;
        private void FlowControl_FlowCompleted(object? sender, EventArgs e)
        {
            flowControl.FlowCompleted -= FlowControl_FlowCompleted;
            handler?.Close();
            handler = null;
            stopwatch.Stop();
            timer.Change(Timeout.Infinite, 500); // 停止定时器
            ProjectBlackMuraConfig.Instance.LastFlowTime = stopwatch.ElapsedMilliseconds;
            log.Info($"流程执行Elapsed Time: {stopwatch.ElapsedMilliseconds} ms");

            if (sender is FlowControlData FlowControlData)
            {
                if (FlowControlData.EventName == "Completed" || FlowControlData.EventName == "Canceled" || FlowControlData.EventName == "OverTime" || FlowControlData.EventName == "Failed")
                {
                    if (FlowControlData.EventName == "Completed")
                    {
                        LastCompleted = true;
                        try
                        {
                            Processing(FlowControlData.SerialNumber);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message);
                        }

                    }
                    else
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), "流程运行失败" + FlowControlData.EventName + Environment.NewLine + FlowControlData.Params, "ColorVision");
                    }
                }
                else
                {

                    MessageBox.Show(Application.Current.GetActiveWindow(), "流程运行失败" + FlowControlData.EventName + Environment.NewLine + FlowControlData.Params, "ColorVision");
                }

            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "流程运行异常", "ColorVision");
            }
        }
        #endregion

        private void Processing(string SerialNumber)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            bool sucess = true;
            var Batch = BatchResultMasterDao.Instance.GetByCode(SerialNumber);

            if (Batch == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到批次号，请检查流程配置", "ColorVision");
                return;
            }
            BlackMuraResult result = new BlackMuraResult();
            result.Model = FlowTemplate.Text;
            result.Id = Batch.Id;
            result.SN = SNtextBox.Text;


            foreach (var item in AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id))
            {
                if(item.ImgFileType == AlgorithmResultType.BlackMura_Caculate)
                {
                    List<BlackMuraModel> AlgResultModels = BlackMuraDao.Instance.GetAllByPid(item.Id);
                    if (AlgResultModels.Count > 0)
                    {
                        BlackMuraView blackMuraView = new BlackMuraView(AlgResultModels[0]);


                        blackMuraView.ResultJson.LvMax = blackMuraView.ResultJson.LvMax * BlackMuraConfig.Instance.LvMaxScale;
                        blackMuraView.ResultJson.LvMin = blackMuraView.ResultJson.LvMin * BlackMuraConfig.Instance.LvMinScale;
                        blackMuraView.ResultJson.ZaRelMax = blackMuraView.ResultJson.ZaRelMax * BlackMuraConfig.Instance.ZaRelMaxScale;
                        blackMuraView.ResultJson.Uniformity = blackMuraView.ResultJson.LvMin / blackMuraView.ResultJson.LvMax * 100;

                        result.WhiteFilePath = item.ImgFile;
                    }
                }
            }

            SNtextBox.Text = string.Empty;
            ViewResluts.Add(result);
        }



        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                var result = ViewResluts[listView.SelectedIndex];
                GenoutputText(result);

                Task.Run(async () =>
                {
                    if (File.Exists(result.WhiteFilePath))
                    {
                        try
                        {
                            var fileInfo = new FileInfo(result.WhiteFilePath);
                            log.Warn($"fileInfo.Length{fileInfo.Length}");
                            using (var fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                            {
                                log.Warn("文件可以读取，没有被占用。");
                            }
                            if (fileInfo.Length > 0)
                            {
                                _ = Application.Current.Dispatcher.BeginInvoke(() =>
                                {
                                    ImageView.OpenImage(result.WhiteFilePath);
                                    ImageView.ImageShow.Clear();
                                });
                            }
                        }
                        catch
                        {
                            log.Warn("文件还在写入");
                            await Task.Delay(ProjectBlackMuraConfig.Instance.ViewImageReadDelay);
                            _ = Application.Current.Dispatcher.BeginInvoke(() =>
                            {
                                ImageView.OpenImage(result.WhiteFilePath);
                                ImageView.ImageShow.Clear();
                            });
                        }

                        _ = Application.Current.Dispatcher.BeginInvoke(() =>
                        {



                        });

                    }
                });

            }

        }

        public void GenoutputText(BlackMuraResult kmitemmaster)
        {

            outputText.Background = kmitemmaster.Result ? Brushes.Lime : Brushes.Red;
            outputText.Document.Blocks.Clear(); // 清除之前的内容

            string outtext = string.Empty;
            outtext += $"Model:{kmitemmaster.Model}" + Environment.NewLine;
            outtext += $"SN:{kmitemmaster.SN}" + Environment.NewLine;
            outtext += $"Poiints of Interest: " + Environment.NewLine;
            outtext += $"{DateTime.Now:yyyy/MM//dd HH:mm:ss}" + Environment.NewLine;

            Run run = new Run(outtext);
            run.Foreground = kmitemmaster.Result ? Brushes.Black : Brushes.White;
            run.FontSize += 1;

            var paragraph = new Paragraph();
            paragraph.Inlines.Add(run);

            outputText.Document.Blocks.Add(paragraph);
            outtext = string.Empty;

            paragraph = new Paragraph();

            string title1 = "PT";
            string title2 = "Lv";

            string title5 = "Lc";
            outtext += $"{title1,-20}   {title2,-10} {title5,10}" + Environment.NewLine;
            run = new Run(outtext);
            run.Foreground = kmitemmaster.Result ? Brushes.Black : Brushes.White;
            run.FontSize += 1;

            paragraph.Inlines.Add(run);
            outtext = string.Empty;

            outputText.Document.Blocks.Add(paragraph);

            //outtext += $"Min Lv= {kmitemmaster.MinLv:F2} cd/m2" + Environment.NewLine;
            //outtext += $"Max Lv= {kmitemmaster.MaxLv:F2} cd/m2" + Environment.NewLine;
            //outtext += $"Darkest Key= {kmitemmaster.DrakestKey}" + Environment.NewLine;
            //outtext += $"Brightest Key= {kmitemmaster.BrightestKey}" + Environment.NewLine;

            outtext += Environment.NewLine;
            outtext += $"Pass/Fail Criteria:" + Environment.NewLine;
            //outtext += $"NbrFail Points={kmitemmaster.NbrFailPoints}" + Environment.NewLine;
            //outtext += $"Avg Lv={kmitemmaster.AvgLv:F2}" + Environment.NewLine;
            //outtext += $"Lv Uniformity={kmitemmaster.LvUniformity * 100:F2}%" + Environment.NewLine;

            outtext += kmitemmaster.Result ? "Pass" : "Fail" + Environment.NewLine;

            run = new Run(outtext);
            run.Foreground = kmitemmaster.Result ? Brushes.Black : Brushes.White;
            run.FontSize += 1;
            paragraph = new Paragraph(run);
            outtext = string.Empty;
            outputText.Document.Blocks.Add(paragraph);
            SNtextBox.Focus();
        }


        private void listView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {

        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {

        }

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {

        }

        private void SNtextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {

        }

        private void GridSplitter_DragCompleted1(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {

        }

        private void Button_Click_Clear(object sender, RoutedEventArgs e)
        {
            ViewResluts.Clear();
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {

        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}