using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates.Flow;
using ColorVision.SocketProtocol;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.LogImp;
using FlowEngineLib;
using FlowEngineLib.Base;
using log4net;
using Newtonsoft.Json;
using ProjectLUX.Fix;
using ProjectLUX.Process;
using ProjectLUX.Services;
using ST.Library.UI.NodeEditor;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ProjectLUX
{
    public class LUXWindowConfig : WindowConfig
    {
        public static LUXWindowConfig Instance => ConfigService.Instance.GetRequiredService<LUXWindowConfig>();
    }

    public partial class LUXWindow : Window, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LUXWindow));
        public static LUXWindowConfig Config => LUXWindowConfig.Instance;

        public static ProjectLUXConfig ProjectConfig => ProjectLUXConfig.Instance;

        public static ViewResultManager ViewResultManager => ViewResultManager.GetInstance();

        public static ObservableCollection<ProjectLUXReuslt> ViewResluts { get; set; } = ViewResultManager.ViewResluts;

        public static FixConfig ObjectiveTestResultFix => FixManager.GetInstance().FixConfig;
        public static RecipeManager RecipeManager => RecipeManager.GetInstance();
        public static RecipeConfig RecipeConfig => RecipeManager.RecipeConfig;

        public static ProcessManager ProcessManager => ProcessManager.GetInstance();
        public ObservableCollection<ProcessMeta> ProcessMetas { get; } = ProcessManager.ProcessMetas;

        public LUXWindow()
        {
            InitializeComponent();
            this.ApplyCaption(false);
            Config.SetWindow(this);
        }

        public int CurrentTestType = -1;
        ObjectiveTestResult ObjectiveTestResult { get; set; } = new ObjectiveTestResult();


        Random Random = new Random();
        public void InitTest(string SN)
        {
            ProjectLUXConfig.Instance.StepIndex = 0;
            ObjectiveTestResult = new ObjectiveTestResult();
            CurrentTestType = -1;

            if (!Directory.Exists(ProjectLUXConfig.Instance.ResultSavePath))
            {
                try
                {
                    Directory.CreateDirectory(ProjectLUXConfig.Instance.ResultSavePath);
                }
                catch (Exception ex)
                {
                    log.Error("创建结果保存目录失败：" + ex.Message);
                }
            }

            if (string.IsNullOrWhiteSpace(SN))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProjectLUXConfig.Instance.SN = "SN" + Random.NextInt64(1000, 9000).ToString();
                });
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProjectLUXConfig.Instance.SN = "SN" + Random.NextInt64(1000, 9000).ToString();
                });
            }
        }

        public void RunTemplate(int index,string templatename)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ProjectLUXConfig.Instance.StepIndex = index;
                CurrentTestType = index;
                var temp = TemplateFlow.Params.FirstOrDefault(a => a.Key.Contains(templatename));     
                if (ProjectLUXConfig.Instance.LUXTestOpen && temp !=null)
                {
                    FlowTemplate.SelectedValue = temp.Value;
                    RunTemplate();
                }
                else
                {
                    log.Error($"cant find {templatename} error");
                    SocketControl.Current.Stream.Write(Encoding.UTF8.GetBytes(ReturnCode));
                }
            });
        }

        public string ReturnCode { get; set; }

        public STNodeEditor STNodeEditorMain { get; set; }
        private FlowEngineControl flowEngine;
        private Timer timer;
        Stopwatch stopwatch = new Stopwatch();


        LogOutput logOutput;
        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = ProjectLUXConfig.Instance;

            MQTTConfig mQTTConfig = MQTTSetting.Instance.MQTTConfig;
            MQTTHelper.SetDefaultCfg(mQTTConfig.Host, mQTTConfig.Port, mQTTConfig.UserName, mQTTConfig.UserPwd, false, null);
            flowEngine = new FlowEngineControl(false);
            STNodeEditorMain = new STNodeEditor();
            STNodeEditorMain.LoadAssembly("FlowEngineLib.dll");
            flowEngine.AttachNodeEditor(STNodeEditorMain);



            timer = new Timer(TimeRun, null, 0, 500);
            timer.Change(Timeout.Infinite, 500); // 停止定时器

            if (ProjectLUXConfig.Instance.LogControlVisibility)
            {
                logOutput = new LogOutput("%date{HH:mm:ss} [%thread] %-5level %message%newline");
                LogGrid.Children.Add(logOutput);
            }
            else
            {
                LogGrid.Visibility = Visibility.Collapsed;
            }

            this.Closed += (s, e) =>
            {
                timer.Change(Timeout.Infinite, 500); // 停止定时器
                timer?.Dispose();

                logOutput?.Dispose();
            };
            ViewResultManager.ListView = listView1;
            listView1.ItemsSource = ViewResluts;

            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => Delete(), (s, e) => e.CanExecute = listView1.SelectedIndex > -1));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, (s, e) => listView1.SelectAll(), (s, e) => e.CanExecute = true));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, ListViewUtils.Copy, (s, e) => e.CanExecute = true));

        }

        public void Delete()
        {
            if (listView1.SelectedIndex < 0) return;
            var item = listView1.SelectedItem as ProjectLUXReuslt;
            if (item == null) return;
            if (MessageBox.Show(Application.Current.GetActiveWindow(), $"是否删除 {item.SN} 测试结果？", "ColorVision", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                ViewResluts.Remove(item);
                log.Info($"删除测试结果 {item.SN}");
            }
        }

        private void ServicesChanged(object? sender, EventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                log.Info("Service触发拍照，执行流程");
                RunTemplate();
            });
        }


        public  async Task Refresh()
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
                    await Task.Delay(10);
                }
                foreach (var item in STNodeEditorMain.Nodes.OfType<CVCommonNode>())
                    item.nodeRunEvent += UpdateMsg;
            }
            catch (Exception ex)
            {
                flowEngine.LoadFromBase64(string.Empty);
            }
        }


        private void TimeRun(object? state)
        {
            UpdateMsg(state);
        }


        string Msg1;
        private long LastFlowTime;
        string FlowName;
        private void UpdateMsg(object? sender)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                    TimeSpan elapsed = TimeSpan.FromMilliseconds(elapsedMilliseconds);
                    string elapsedTime = $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}:{elapsed.Milliseconds:D4}";
                    string msg;
                    if (LastFlowTime == 0 || LastFlowTime - elapsedMilliseconds < 0)
                    {
                        msg = $"{FlowName}{Environment.NewLine}正在执行节点:{Msg1}{Environment.NewLine}已经执行：{elapsedTime} {Environment.NewLine}";
                    }
                    else
                    {
                        long remainingMilliseconds = LastFlowTime - elapsedMilliseconds;
                        TimeSpan remaining = TimeSpan.FromMilliseconds(remainingMilliseconds);
                        string remainingTime = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}:{elapsed.Milliseconds:D4}";

                        msg = $"{FlowName}{Environment.NewLine}上次执行：{LastFlowTime} ms{Environment.NewLine}正在执行节点:{Msg1}{Environment.NewLine}已经执行：{elapsedTime} {Environment.NewLine}预计还需要：{remainingTime}";
                    }
                    logTextBox.Text = msg;
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


        ProjectLUXReuslt CurrentFlowResult { get; set; }
        int TryCount = 0;

        public async Task RunTemplate()
        {
            if (flowControl != null && flowControl.IsFlowRun) return;

            TryCount++;
            LastFlowTime = FlowEngineConfig.Instance.FlowRunTime.TryGetValue(FlowTemplate.Text, out long time) ? time : 0;

            CurrentFlowResult = new ProjectLUXReuslt();
            CurrentFlowResult.SN = ProjectLUXConfig.Instance.SN;
            CurrentFlowResult.Model = FlowTemplate.Text;

            CurrentFlowResult.TestType = CurrentTestType;

            FlowName = FlowTemplate.Text;
            CurrentFlowResult.Code = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");

            await Refresh();

            if (string.IsNullOrWhiteSpace(flowEngine.GetStartNodeName())) { log.Info("找不到完整流程，运行失败"); return; }

            if (!flowEngine.IsReady)
            {
                string base64 = string.Empty;
                flowEngine.LoadFromBase64(base64);
                await Refresh();
                log.Info($"IsReady{flowEngine.IsReady}");
            }
            CurrentFlowResult.FlowStatus = FlowStatus.Ready;

            flowControl ??= new FlowControl(MQTTControl.GetInstance(), flowEngine);
            flowControl.FlowCompleted += FlowControl_FlowCompleted;
            stopwatch.Reset();
            stopwatch.Start();
            MeasureBatchModel measureBatchModel = new MeasureBatchModel() { Name = CurrentFlowResult.SN, Code = CurrentFlowResult.Code};
            int id = MySqlControl.GetInstance().DB.Insertable(measureBatchModel).ExecuteReturnIdentity();
            CurrentFlowResult.BatchId = id;
            flowControl.Start(CurrentFlowResult.Code);
            timer.Change(0, 500); // 启动定时器
        }

        private FlowControl flowControl;

        private void FlowControl_FlowCompleted(object? sender, FlowControlData FlowControlData)
        {
            flowControl.FlowCompleted -= FlowControl_FlowCompleted;
            stopwatch.Stop();
            timer.Change(Timeout.Infinite, 500); // 停止定时器
            FlowEngineConfig.Instance.FlowRunTime[FlowTemplate.Text] = stopwatch.ElapsedMilliseconds;

            log.Info($"流程执行Elapsed Time: {stopwatch.ElapsedMilliseconds} ms");
            CurrentFlowResult.RunTime = stopwatch.ElapsedMilliseconds;
            logTextBox.Text = FlowName + Environment.NewLine + FlowControlData.EventName;

            if (FlowControlData.EventName == "Completed")
            {
                CurrentFlowResult.Msg = "Completed";
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
                CurrentFlowResult.FlowStatus = FlowStatus.OverTime;
                CurrentFlowResult.Msg = logTextBox.Text;
                ViewResultManager.Save(CurrentFlowResult);

                flowEngine.LoadFromBase64(string.Empty);
                Refresh();

                if (TryCount < ProjectLUXConfig.Instance.TryCountMax)
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
                else
                {
                    if (!string.IsNullOrWhiteSpace(ReturnCode))
                    {
                        SocketControl.Current.Stream.Write(Encoding.UTF8.GetBytes(ReturnCode));
                    }
                }
                TryCount = 0;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(ReturnCode))
                {
                    SocketControl.Current.Stream.Write(Encoding.UTF8.GetBytes(ReturnCode));
                }

                log.Error("流程运行失败" + FlowControlData.EventName + FlowControlData.Params);
                CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                CurrentFlowResult.Msg = FlowControlData.Params;

                //算法失败但是图像是有的，可以帮助用户即使发现原因
                if (CurrentFlowResult.Msg.Contains("SDK return failed") || CurrentFlowResult.Msg.Contains("BinocularFusion calculation failed") || CurrentFlowResult.Msg.Contains("Not get cie file"))
                {
                    MeasureBatchModel Batch = BatchResultMasterDao.Instance.GetByCode(FlowControlData.SerialNumber);
                    if (Batch != null)
                    {
                        var values = MeasureImgResultDao.Instance.GetAllByBatchId(Batch.Id);
                        if (values.Count > 0)
                        {
                            CurrentFlowResult.FileName = values[0].FileUrl;
                        }
                    }
                }
                logTextBox.Text = FlowName + Environment.NewLine + FlowControlData.EventName + Environment.NewLine + FlowControlData.Params;
                ViewResultManager.Save(CurrentFlowResult);
                TryCount = 0;
            }
        }

        private void Processing(string SerialNumber)
        {
            MeasureBatchModel Batch = BatchResultMasterDao.Instance.GetByCode(SerialNumber);

            if (Batch == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到批次号，请检查流程配置", "ColorVision");
                return;
            }
            ProjectLUXReuslt result = CurrentFlowResult ?? new ProjectLUXReuslt();

            result.BatchId = Batch.Id;
            result.FlowStatus = FlowStatus.Completed;
            result.CreateTime = DateTime.Now;
            result.Result = true;

            try
            {
                log.Info($"{result.Model}");

                var meta = ProcessMetas.FirstOrDefault(m => string.Equals(m.FlowTemplate, result.Model, StringComparison.OrdinalIgnoreCase));
                if (meta?.Process != null)
                {
                    log.Info($"匹配到自定义流程 {meta.Name} -> {meta.ProcessTypeName}; 使用 IProcess 处理 {result.Model}");

                    bool executed = false;
                    try
                    {
                        var ctx = new IProcessExecutionContext
                        {
                            Batch = Batch,
                            Result = result,
                            ObjectiveTestResult = ObjectiveTestResult,
                            FixConfig = ObjectiveTestResultFix,
                            RecipeConfig = RecipeConfig,
                            ImageView = ImageView,
                            Logger = log
                        };
                        executed = meta.Process.Execute(ctx);
                    }
                    catch (Exception ex)
                    {
                        log.Error("自定义 IProcess 执行异常", ex);
                    }
                    if (executed)
                    {
                        //每次结束都保存
                        string path = Path.Combine(ProjectLUXConfig.Instance.ResultSavePath, $"C_{ProjectLUXConfig.Instance.SN}.csv");
                        ObjectiveTestResultCsvExporter.ExportToCsv(ObjectiveTestResult, path);

                        ViewResultManager.Save(result);
                        ObjectiveTestResult.TotalResult = ObjectiveTestResult.TotalResult && result.Result;

                        if (!string.IsNullOrWhiteSpace(ReturnCode))
                        {
                            if(CurrentTestType == 0)
                            {
                                log.Info("IsOC");
                                if(ObjectiveTestResult.OpticCenterTestResult != null)
                                {
                                    ReturnCode += $",{ObjectiveTestResult.OpticCenterTestResult.OptCenterRotation.Value},{ObjectiveTestResult.OpticCenterTestResult.OptCenterXTilt.Value},{ObjectiveTestResult.OpticCenterTestResult.OptCenterYTilt.Value},00;";
                                }
                                else
                                {
                                    log.Info("ObjectiveTestResult.OpticCenterTestResult null");
                                }
                            }
                            SocketControl.Current.Stream.Write(Encoding.UTF8.GetBytes(ReturnCode));
                        }

                        if (IsTestTypeCompleted())
                        {
                            TestCompleted();
                        }
                        return; // 已处理，直接返回
                    }
                    else
                    {
                        log.Warn("自定义 IProcess 执行失败，继续使用内置解析逻辑");
                    }
                }
                else
                {
                    log.Info($"匹配到不到自定义流程 {meta.Name} -> {meta.ProcessTypeName};");
                }
            }
            catch (Exception ex)
            {
                log.Error("匹配/执行自定义 IProcess 出错，回退内置逻辑", ex);
            }
            ViewResultManager.Save(result);
            ObjectiveTestResult.TotalResult = ObjectiveTestResult.TotalResult && result.Result;
        }
        private bool IsTestTypeCompleted() => CurrentTestType + 1 >= ProcessMetas.Count;


        private void TestCompleted()
        {
            if (SocketManager.GetInstance().TcpClients.Count <= 0 || SocketControl.Current.Stream == null)
            {
                log.Info("找不到连接的Socket");
                return;
            }

            ObjectiveTestResult.TotalResult = true;
            log.Info($"ARVR测试完成,TotalResult {ObjectiveTestResult.TotalResult}");

            string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            string filePath = Path.Combine(ViewResultManager.Config.CsvSavePath, $"ObjectiveTestResults_{timeStr}.csv");
            ObjectiveTestResultCsvExporter.ExportToCsv(ObjectiveTestResult, filePath);
            var response = new SocketResponse
            {
                Version = "1.0",
                MsgID = string.Empty,
                EventName = "ProjectLUXResult",
                Code = 0,
                SerialNumber = SNtextBox.Text,
                Msg = "ARVR Test Completed",
                Data = ObjectiveTestResult
            };
            string respString = JsonConvert.SerializeObject(response);
            log.Info(respString);
            SocketControl.Current.Stream.Write(Encoding.UTF8.GetBytes(respString));
        }

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            ProjectLUXConfig.Instance.Height = row2.ActualHeight;
            row2.Height = GridLength.Auto;
        }

        private void Button_Click_Clear(object sender, RoutedEventArgs e)
        {
            ImageView.Clear();
            outputText.Document.Blocks.Clear();
            outputText.Background = Brushes.White;
        }

        private void listView1_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                var result = ViewResluts[listView.SelectedIndex];
                try
                {
                    if (result.FlowStatus == FlowStatus.Completed)
                    {
                        GenoutputText(result);
                    }
                    else
                    {
                        outputText.Background = Brushes.White;
                        outputText.Document.Blocks.Clear(); // 清除之前的内容
                    }

                }
                catch (Exception ex)
                {
                    log.Info("展示图片报错");
                    log.Error(ex);
                }

                Task.Run(async () =>
                {
                    if (File.Exists(result.FileName))
                    {
                        _ = Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            ImageView.OpenImage(result.FileName);
                            ImageView.ImageShow.Clear();

                            if (result.FlowStatus != FlowStatus.Completed)
                                return;

                            var meta = ProcessMetas.FirstOrDefault(m => string.Equals(m.FlowTemplate, result.Model, StringComparison.OrdinalIgnoreCase));
                            if (meta?.Process != null)
                            {
                                bool executed = false;
                                try
                                {
                                    var ctx = new IProcessExecutionContext
                                    {
                                        Result = result,
                                        ObjectiveTestResult = ObjectiveTestResult,
                                        FixConfig = ObjectiveTestResultFix,
                                        RecipeConfig = RecipeConfig,
                                        ImageView = ImageView,
                                        Logger = log
                                    };
                                    meta.Process.Render(ctx);
                                }
                                catch (Exception ex)
                                {
                                    log.Error("自定义 IProcess 执行异常", ex);
                                }
                            }

                        });
                    }
                });

            }
        }

        public void GenoutputText(ProjectLUXReuslt result)
        {
            outputText.Background = result.Result ? Brushes.Lime : Brushes.Red;
            outputText.Document.Blocks.Clear(); // 清除之前的内容

            string outtext = string.Empty;
            outtext += $"Model:{result.Model}  SN:{result.SN}  {DateTime.Now:yyyy/MM//dd HH:mm:ss}";
            Run run = new Run(outtext);
            run.Foreground = result.Result ? Brushes.Black : Brushes.White;
            run.FontSize += 1;

            var paragraph = new Paragraph();
            paragraph.Inlines.Add(run);
            outputText.Document.Blocks.Add(paragraph);
            outtext = string.Empty;


            var meta = ProcessMetas.FirstOrDefault(m => string.Equals(m.FlowTemplate, result.Model, StringComparison.OrdinalIgnoreCase));
            if (meta?.Process != null)
            {
                bool executed = false;
                try
                {
                    var ctx = new IProcessExecutionContext
                    {
                        Result = result,
                        ObjectiveTestResult = ObjectiveTestResult,
                        FixConfig = ObjectiveTestResultFix,
                        RecipeConfig = RecipeConfig,
                        ImageView = ImageView,
                        Logger = log
                    };
                    outtext += meta.Process.GenText(ctx);
                }
                catch (Exception ex)
                {
                    log.Error("自定义 IProcess 执行异常", ex);
                }
            }

            outtext += Environment.NewLine + $"Pass/Fail Criteria:" + Environment.NewLine;
            outtext += result.Result ? "Pass" : "Fail" + Environment.NewLine;



            run = new Run(outtext);
            run.Foreground = result.Result ? Brushes.Black : Brushes.White;
            run.FontSize += 1;
            paragraph = new Paragraph(run);
            outtext = string.Empty;
            outputText.Document.Blocks.Add(paragraph);
            SNtextBox.Focus();
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
        public void Dispose()
        {
            timer?.Dispose();
            GC.SuppressFinalize(this);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string sn = "ssss";
            string path = Path.Combine(ProjectLUXConfig.Instance.ResultSavePath, $"C_{sn}.csv");
            ObjectiveTestResult TestResult = new ObjectiveTestResult();
            TestResult.W51ARTestResult = new Process.AR.W51AR.W51ARTestResult();
            TestResult.W255ARTestResult = new Process.W255AR.W255ARTestResult();
            TestResult.MTFHVARTestResult = new Process.MTFHVAR.MTFHARVTestResult();
            TestResult.ChessboardARTestResult = new Process.ChessboardAR.ChessboardARTestResult();
            TestResult.DistortionARTestResult = new Process.DistortionAR.DistortionARTestResult();
            TestResult.OpticCenterTestResult = new Process.OpticCenter.OpticCenterTestResult();
            ObjectiveTestResultCsvExporter.ExportToCsv(TestResult, path);
        }
    }
}