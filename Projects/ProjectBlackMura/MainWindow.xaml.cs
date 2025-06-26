using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Abstractions;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Jsons.BlackMura;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.LogImp;
using FlowEngineLib;
using FlowEngineLib.Base;
using log4net;
using log4net.Util;
using Newtonsoft.Json;
using Panuon.WPF.UI;
using ST.Library.UI.NodeEditor;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Text;
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
        public string Code { get; set; }

        public string SN { get => _SN; set { _SN = value; NotifyPropertyChanged(); } }
        private string _SN;
        public string WhiteFilePath { get => _WhiteFilePath; set { _WhiteFilePath = value; NotifyPropertyChanged(); } }
        private string _WhiteFilePath = string.Empty;

        public string BlackFilePath { get => _BlackFilePath; set { _BlackFilePath = value; NotifyPropertyChanged(); } }
        private string _BlackFilePath = string.Empty;

        public bool Result { get => _Result; set { _Result = value; NotifyPropertyChanged(); } }
        private bool _Result = true;

    }

    public class BlackMuraWindowConfig : WindowConfig
    {
        public static BlackMuraWindowConfig Instance => ConfigService.Instance.GetRequiredService<BlackMuraWindowConfig>();
    }

    /// <summary>
    /// Interaction logic for MarkdownViewWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MainWindow));

        public static ObservableCollection<BlackMuraResult> ViewResluts => ProjectBlackMuraConfig.Instance.ViewResluts;

        public MainWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            BlackMuraWindowConfig.Instance.SetWindow(this);
            this.SizeChanged += (s, e) => BlackMuraWindowConfig.Instance.SetConfig(this);

        }
        private LogOutput? logOutput;

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = ProjectBlackMuraConfig.Instance;
            listView1.ItemsSource = ViewResluts;
            InitFlow();

            ComboBoxSer.ItemsSource = SerialPort.GetPortNames();
            ComboBoxSer.SelectedIndex = 0;
            if (ProjectBlackMuraConfig.Instance.LogControlVisibility)
            {
                logOutput = new LogOutput("%date{HH:mm:ss} [%thread] %-5level %message%newline");
                LogGrid.Children.Add(logOutput);
            }

            this.Closed += (s, e) =>
            {
                timer.Change(Timeout.Infinite, 500); // 停止定时器
                timer?.Dispose();

                logOutput?.Dispose();
            };

            MesGrid.DataContext = HYMesManager.GetInstance();

            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => Delete(), (s, e) => e.CanExecute = listView1.SelectedIndex > -1));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, (s, e) => listView1.SelectAll(), (s, e) => e.CanExecute = true));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, ListViewUtils.Copy, (s, e) => e.CanExecute = true));

        }
        public void Delete()
        {
            if (listView1.SelectedIndex < 0) return;
            var item = listView1.SelectedItem as BlackMuraResult;
            if (item == null) return;
            if (MessageBox.Show(Application.Current.GetActiveWindow(), $"是否删除 {item.SN} 测试结果？", "ColorVision", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                ViewResluts.Remove(item);
                BatchResultMasterDao.Instance.DeleteById(item.Id);
                log.Info($"删除测试结果 {item.SN}");
            }
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

                flowEngine.LoadFromBase64(TemplateFlow.Params[FlowTemplate.SelectedIndex].Value.DataBase64, MqttRCService.GetInstance().ServiceTokens);

                for (int i = 0; i < 200; i++)
                {
                    if (flowEngine.IsReady)
                        break;
                    Thread.Sleep(10);
                }
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
                        if (LastFlowTime == 0 || LastFlowTime - elapsedMilliseconds < 0)
                        {
                            msg = Msg1 + Environment.NewLine + $"已经执行：{elapsedTime}";
                        }
                        else
                        {
                            long remainingMilliseconds = LastFlowTime - elapsedMilliseconds;
                            TimeSpan remaining = TimeSpan.FromMilliseconds(remainingMilliseconds);
                            string remainingTime = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}:{elapsed.Milliseconds:D4}";

                            msg = Msg1 + Environment.NewLine + $"已经执行：{elapsedTime}, 上次执行：{LastFlowTime} ms, 预计还需要：{remainingTime}";
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

        BlackMuraResult CurrentFlowResult;
        long LastFlowTime;
        int TryCount = 0;

        public void RunTemplate()
        {
            if (flowControl != null && flowControl.IsFlowRun) return;

            TryCount++;
            LastFlowTime = FlowConfig.Instance.FlowRunTime.TryGetValue(FlowTemplate.Text, out long time) ? time : 0;

            CurrentFlowResult = new BlackMuraResult();
            CurrentFlowResult.SN = SNtextBox.Name;
            CurrentFlowResult.Code = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            if (string.IsNullOrWhiteSpace(flowEngine.GetStartNodeName())) { log.Info("找不到完整流程，运行失败"); return; }

            //多潘基次次
            log.Info($"IsReady{flowEngine.IsReady}");
            if (!flowEngine.IsReady)
            {
                string base64 = string.Empty;
                flowEngine.LoadFromBase64(base64);
                Refresh();
                log.Info($"IsReady{flowEngine.IsReady}");
                if (!flowEngine.IsReady)
                {
                    flowEngine.LoadFromBase64(base64);
                    Refresh();
                    log.Info($"IsReady{flowEngine.IsReady}");
                    if (!flowEngine.IsReady)
                    {
                        flowEngine.LoadFromBase64(base64);
                        Refresh();
                        log.Info($"IsReady{flowEngine.IsReady}");
                    }

                }
            }


            flowControl ??= new FlowControl(MQTTControl.GetInstance(), flowEngine);

            handler = PendingBox.Show(this, "流程", "流程启动", true);
            handler.Cancelling -= Handler_Cancelling;
            handler.Cancelling += Handler_Cancelling;
            flowControl.FlowCompleted += FlowControl_FlowCompleted;
            stopwatch.Reset();
            stopwatch.Start();

            try
            {
                BatchResultMasterDao.Instance.Save(new BatchResultMasterModel() { Name = CurrentFlowResult.SN, Code = CurrentFlowResult.Code, CreateDate = DateTime.Now });
            }
            catch (Exception ex)
            {
                log.Info(ex);
            }

            flowControl.Start(CurrentFlowResult.Code);
            timer.Change(0, 500); // 启动定时器
        }

        private void Handler_Cancelling(object? sender, CancelEventArgs e)
        {
            stopwatch.Stop();
            timer.Change(Timeout.Infinite, 500); // 停止定时器
            flowControl.Stop();
        }
        private FlowControl flowControl;
        private void FlowControl_FlowCompleted(object? sender, FlowControlData FlowControlData)
        {
            flowControl.FlowCompleted -= FlowControl_FlowCompleted;
            handler?.Close();
            handler = null;
            stopwatch.Stop();
            timer.Change(Timeout.Infinite, 500); // 停止定时器

            FlowConfig.Instance.FlowRunTime[FlowTemplate.Text] = stopwatch.ElapsedMilliseconds;

            log.Info($"流程执行Elapsed Time: {stopwatch.ElapsedMilliseconds} ms");

            if (FlowControlData.EventName == "Completed")
            {
                try
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        Processing(FlowControlData.SerialNumber);
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message);
                }
                TryCount = 0;
            }
            else if (FlowControlData.EventName == "OverTime")
            {
                log.Info("流程运行超时，正在重新尝试");
                flowEngine.LoadFromBase64(string.Empty);
                Refresh();
                if (TryCount < ProjectBlackMuraConfig.Instance.TryCountMax)
                {
                    Task.Delay(200).ContinueWith(t =>
                    {
                        log.Info("重新尝试运行流程");
                        Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            RunTemplate();
                        });
                    });
                    return;
                }
                TryCount = 0;
            }
            else
            {
                log.Info("流程运行失败" + FlowControlData.EventName + Environment.NewLine + FlowControlData.Params);
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
                if(item.ImgFileType == AlgorithmResultType.BlackMura_Calc)
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
            ViewResluts.Insert(0,result);
            listView1.SelectedIndex = 0;
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

        private void Test_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(ProjectBlackMuraConfig.Instance.ResultSavePath))
            {
                ExcelReportGenerator.GenerateExcel( Path.Combine(ProjectBlackMuraConfig.Instance.ResultSavePath,"数据格式报告.xlsx"));
            }
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

        private void PG_PowerOn_Click(object sender, RoutedEventArgs e)
        {
            HYMesManager.GetInstance().PGPowerOn();
        }

        private void PG_PowerOff_Click(object sender, RoutedEventArgs e)
        {
            HYMesManager.GetInstance().PGPowerOff();
        }



        private void PG_PowerSwitch1_Click(object sender, RoutedEventArgs e)
        {
            HYMesManager.GetInstance().PGSwitch(0);
        }

        private void PG_PowerSwitch2_Click(object sender, RoutedEventArgs e)
        {
            HYMesManager.GetInstance().PGSwitch(1);

        }

        private void PG_PowerSwitch3_Click(object sender, RoutedEventArgs e)
        {
            HYMesManager.GetInstance().PGSwitch(2);

        }

        private void PG_PowerSwitch4_Click(object sender, RoutedEventArgs e)
        {
            HYMesManager.GetInstance().PGSwitch(3);
        }

        private void PG_PowerSwitch5_Click(object sender, RoutedEventArgs e)
        {
            HYMesManager.GetInstance().PGSwitch(4);
        }

        private void PG_PowerSwitch6_Click(object sender, RoutedEventArgs e)
        {
            HYMesManager.GetInstance().PGSwitch(15);
        }
    }
}