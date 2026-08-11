#pragma warning disable CA1805,CA1822,CS0168,CS0219,CS4014,CS8601
using Azure;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.FlowProcessing.PreProcess;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.FlowProcessing;
using ColorVision.SocketProtocol;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.LogImp;
using FlowEngineLib;
using FlowEngineLib.Base;
using log4net;
using ProjectLUX.Fix;
using ProjectLUX.Process;
using ProjectLUX.Services;
using SqlSugar;
using ST.Library.UI.NodeEditor;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
        public ObservableCollection<ProcessMeta> ProcessMetas => ProcessManager.ProcessMetas;

        public LUXWindow()
        {
            InitializeComponent();
            this.ApplyCaption(false);
            Config.SetWindow(this);
        }

        ObjectiveTestResult ObjectiveTestResult { get; set; } = new ObjectiveTestResult();
        private int ObjectiveTestResultRecordId;
        private NetworkStream? stream;
        public NetworkStream? Stream
        {
            get => stream;
            set
            {
                // 多工位并行时，后续握手/消息不能覆盖当前流程的回包连接。
                if (value != null && flowControl != null && flowControl.IsFlowRun)
                {
                    log.Info("流程运行中，保持当前执行Stream");
                    return;
                }
                stream = value;
            }
        }


        Random Random = new Random();
        public void InitTest(string SN)
        {
            ProjectLUXConfig.Instance.StepIndex = 0;
            ObjectiveTestResult = new ObjectiveTestResult();
            ObjectiveTestResultRecordId = 0;

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

            Application.Current.Dispatcher.Invoke(() =>
            {
                ProjectLUXConfig.Instance.SN = string.IsNullOrWhiteSpace(SN) ? "SN" + Random.NextInt64(1000, 9000).ToString() : SN.Trim();
            });
        }

        /// <summary>
        /// 在当前活动组内根据 SocketCode 查找对应的 ProcessMeta 并执行流程。
        /// </summary>
        public void RunTemplateBySocketCode(string socketCode)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (flowControl != null && flowControl.IsFlowRun)
                {
                    log.Info($"流程运行中，忽略 SocketCode={socketCode}");
                    return;
                }

                var activeGroup = ProcessManager.ActiveGroup;
                if (activeGroup == null)
                {
                    log.Error($"未设置活动流程组，无法执行 SocketCode={socketCode}");
                    if (Stream != null)
                        Stream.Write(Encoding.UTF8.GetBytes(ReturnCode));
                    return;
                }

                var processMeta = ProcessManager.FindProcessMetaBySocketCode(socketCode);
                if (processMeta == null)
                {
                    log.Error($"未在组 {activeGroup.Name} 中找到 SocketCode={socketCode} 对应的流程");
                    if (Stream != null)
                        Stream.Write(Encoding.UTF8.GetBytes(ReturnCode));
                    return;
                }

                int index = activeGroup.ProcessMetas.IndexOf(processMeta);
                ProjectConfig.StepIndex = index;
                var temp = TemplateFlow.Params.FirstOrDefault(a => a.Key.Contains(processMeta.FlowTemplate));
                if (temp != null)
                {
                    FlowTemplate.SelectedValue = temp.Value;
                    RunTemplate();
                }
                else
                {
                    log.Error($"未找到 FlowTemplate={processMeta.FlowTemplate} 对应的模板");
                    if (Stream != null)
                        Stream.Write(Encoding.UTF8.GetBytes(ReturnCode));
                }
            });
        }

        public string ReturnCode { get; set; }

        public STNodeEditor STNodeEditorMain { get; set; }
        private FlowEngineControl flowEngine;
        private Timer timer;
        private bool _isDisposed;
        private readonly CancellationTokenSource _lifetimeCancellation = new();
        private int _resultImageRequestId;
        Stopwatch stopwatch = new Stopwatch();


        LogOutput logOutput;
        private void Window_Initialized(object sender, EventArgs e)
        {
            ProcessManager.GenStepBar(stepBar);
            ProcessManager.ActiveGroupChanged += ProcessManager_ActiveGroupChanged;
            UpdateActiveGroupDisplay();

            // 先挂载集合，再恢复共享的选中索引，避免空列表把校正后的索引写回单例。
            listView1.ItemsSource = ViewResluts;
            this.DataContext = ProjectLUXConfig.Instance;

            flowEngine = new FlowEngineControl(false);
            STNodeEditorMain = new STNodeEditor();
            STNodeEditorMain.LoadAssembly("FlowEngineLib.dll");
            flowEngine.AttachNodeEditor(STNodeEditorMain);



            timer = new Timer(TimeRun, null, 0, 500);
            timer.Change(Timeout.Infinite, 500); // 停止定时器

            if (ProjectLUXConfig.Instance.LogControlVisibility)
            {
                logOutput = new LogOutput("%date{HH:mm:ss} [%thread] %-5level %message%newline", ProjectLUXLogConfig.Instance);
                LogGrid.Children.Add(logOutput);
            }
            else
            {
                LogGrid.Visibility = Visibility.Collapsed;
            }

            this.Closed += (s, e) =>
            {
                Dispose();
            };

            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => Delete(), (s, e) => e.CanExecute = listView1.SelectedIndex > -1));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, (s, e) => listView1.SelectAll(), (s, e) => e.CanExecute = true));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, ListViewUtils.Copy, (s, e) => e.CanExecute = true));

        }

        private void ProcessManager_ActiveGroupChanged(object? sender, EventArgs e)
        {
            if (_isDisposed) return;

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (_isDisposed) return;

                ProcessManager.GenStepBar(stepBar);
                ProjectLUXConfig.Instance.StepIndex = 0;
                UpdateActiveGroupDisplay();
                log.Info($"切换流程组: {ProcessManager.ActiveGroup?.Name}");
            });
        }

        private void UpdateActiveGroupDisplay()
        {
            string groupName = ProcessManager.ActiveGroup?.Name;
            ActiveGroupTextBlock.Text = string.IsNullOrWhiteSpace(groupName)
                ? "当前组: 未设置"
                : $"当前组: {groupName}";
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


        public Task Refresh()
        {
            if (FlowTemplate.SelectedIndex < 0) return Task.CompletedTask;

            flowEngine.LoadFromBase64(TemplateFlow.Params[FlowTemplate.SelectedIndex].Value.DataBase64, MqttRCService.GetInstance().ServiceTokens);

            foreach (var item in STNodeEditorMain.Nodes.OfType<CVCommonNode>())
            {
                item.nodeRunEvent -= UpdateMsg;
                item.nodeRunEvent += UpdateMsg;
            }
            return Task.CompletedTask;
        }


        private void TimeRun(object? state)
        {
            UpdateMsg(state);
        }


        string Msg1;
        private long LastFlowTime;
        private void UpdateMsg(object? sender)
        {
            if (_isDisposed) return;

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (_isDisposed) return;

                try
                {
                    long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                    TimeSpan elapsed = TimeSpan.FromMilliseconds(elapsedMilliseconds);
                    string elapsedTime = $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}:{elapsed.Milliseconds:D4}";
                    string flowName = Volatile.Read(ref activeFlowRun)?.FlowName ?? string.Empty;
                    string msg;
                    if (LastFlowTime == 0 || LastFlowTime - elapsedMilliseconds < 0)
                    {
                        msg = $"{flowName}{Environment.NewLine}正在执行节点:{Msg1}{Environment.NewLine}已经执行：{elapsedTime} {Environment.NewLine}";
                    }
                    else
                    {
                        long remainingMilliseconds = LastFlowTime - elapsedMilliseconds;
                        TimeSpan remaining = TimeSpan.FromMilliseconds(remainingMilliseconds);
                        string remainingTime = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}:{elapsed.Milliseconds:D4}";

                        msg = $"{flowName}{Environment.NewLine}上次执行：{LastFlowTime} ms{Environment.NewLine}正在执行节点:{Msg1}{Environment.NewLine}已经执行：{elapsedTime} {Environment.NewLine}预计还需要：{remainingTime}";
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


        private LUXFlowRunSession? activeFlowRun;
        private readonly Dictionary<string, LUXFlowRunSession> flowRunSessions = new(StringComparer.Ordinal);
        private readonly Dictionary<ProjectLUXReuslt, ViewResultManagerConfig> automaticImageExportConfigs = new(ReferenceEqualityComparer.Instance);
        internal Func<ViewResultManagerConfig>? ResultConfigCaptureOverride { get; set; }
        internal Func<LUXFlowRunSession, Task>? RunTemplateCaptureBarrier { get; set; }

        public Task RunTemplate() => RunTemplateCore(attempt: 1, retainedResultConfig: null);

        private async Task RunTemplateCore(int attempt, ViewResultManagerConfig? retainedResultConfig)
        {
            if (_isDisposed) return;
            LUXFlowRunSession? session = null;
            bool handedToFlow = false;
            try
            {
                if (FlowTemplate.SelectedItem is not TemplateModel<FlowParam> template) return;
                if (Volatile.Read(ref activeFlowRun) != null || (flowControl != null && flowControl.IsFlowRun)) return;

                ViewResultManagerConfig resultConfig = retainedResultConfig
                    ?? ResultConfigCaptureOverride?.Invoke()
                    ?? ViewResultManager.CaptureConfig();
                string flowName = template.Key;
                var result = new ProjectLUXReuslt
                {
                    SN = ProjectLUXConfig.Instance.SN,
                    Model = flowName,
                    Code = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff")
                };
                session = new LUXFlowRunSession(template.Id, flowName, attempt, result, resultConfig);
                if (Interlocked.CompareExchange(ref activeFlowRun, session, null) != null)
                    return;
                lock (flowRunSessions)
                    flowRunSessions.Add(result.Code, session);

                if (RunTemplateCaptureBarrier != null)
                    await RunTemplateCaptureBarrier(session);
                if (_isDisposed) return;

                LastFlowTime = await Task.Run(
                    () => FlowNodeRecordDataBaseHelper.GetLastCompletedFlowElapsed(
                        new FlowIdentity(template.Id, template.Value.FlowKey, flowName)));
                if (_isDisposed) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (ProcessMetas.FirstOrDefault(m => string.Equals(m.FlowTemplate, flowName, StringComparison.OrdinalIgnoreCase)) is ProcessMeta processMeta)
                    {
                        result.TestType = ProcessMetas.IndexOf(processMeta);
                        ProjectLUXConfig.Instance.StepIndex = result.TestType;
                    }
                    else
                    {
                        result.TestType = -1;
                        ProjectLUXConfig.Instance.StepIndex = result.TestType;
                    }
                });

                ProcessMeta? processMeta = ProcessManager.ProcessMetas.FirstOrDefault(a => a.FlowTemplate == flowName);
                if (processMeta != null)
                {
                    int index = ProcessManager.ProcessMetas.IndexOf(processMeta);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ProjectLUXConfig.Instance.StepIndex = index;
                    });
                }

                await Refresh();
                if (_isDisposed) return;

                bool preprocessingSucceeded = await PreProcessing(flowName, result.Code);
                if (_isDisposed) return;
                if (!preprocessingSucceeded)
                {
                    result.FlowStatus = FlowStatus.Failed;
                    result.Msg = "PreProcessFailed";
                    logTextBox.Text = flowName + Environment.NewLine + "预处理失败";
                    return;
                }

                result.FlowStatus = FlowStatus.Ready;

                flowControl ??= new FlowControl(MQTTControl.GetInstance(), flowEngine);
                flowControl.FlowCompleted -= FlowControl_FlowCompleted;
                flowControl.FlowCompleted += FlowControl_FlowCompleted;
                stopwatch.Reset();
                stopwatch.Start();
                MeasureBatchModel measureBatchModel = new MeasureBatchModel() { Name = result.SN, Code = result.Code };
                using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                int id = Db.Insertable(measureBatchModel).ExecuteReturnIdentity();
                result.BatchId = id;

                bool started = await flowControl.TryStartAsync(result.Code, _lifetimeCancellation.Token);
                handedToFlow = started;
                if (_isDisposed)
                {
                    handedToFlow = false;
                    flowControl.FlowCompleted -= FlowControl_FlowCompleted;
                    if (started && flowControl.IsFlowRun)
                        flowControl.Stop();
                    return;
                }
                if (!started)
                {
                    FlowControl_FlowCompleted(flowControl, new FlowControlData
                    {
                        EventName = "Failed",
                        Status = StatusTypeEnum.Failed,
                        SerialNumber = result.Code,
                        Params = "FlowStartRejected"
                    });
                    return;
                }
                timer.Change(0, 500); // 启动定时器
            }
            catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
            {
                if (flowControl != null)
                    flowControl.FlowCompleted -= FlowControl_FlowCompleted;
                log.Debug("窗口已关闭，已取消流程启动等待");
            }
            catch (Exception ex) when (_isDisposed)
            {
                if (flowControl != null)
                {
                    flowControl.FlowCompleted -= FlowControl_FlowCompleted;
                    if (flowControl.IsFlowRun)
                        flowControl.Stop();
                }
                log.Debug("窗口已关闭，忽略迟到的流程启动结果", ex);
            }
            finally
            {
                if (session != null && !handedToFlow)
                    ReleaseFlowRun(session);
            }
        }

        private void ReleaseFlowRun(LUXFlowRunSession session)
        {
            lock (flowRunSessions)
            {
                if (flowRunSessions.TryGetValue(session.Result.Code, out LUXFlowRunSession? current)
                    && ReferenceEquals(current, session))
                {
                    flowRunSessions.Remove(session.Result.Code);
                }
            }
            Interlocked.CompareExchange(ref activeFlowRun, null, session);
        }


        private async Task<bool> PreProcessing(string flowName, string serialNumber)
        {
            var serverNodes = new ObservableCollection<CVBaseServerNode>(STNodeEditorMain.Nodes.OfType<CVBaseServerNode>());
            return await PreProcessManager.GetInstance().ExecuteAsync(flowName, serialNumber, serverNodes);
        }


        private FlowControl flowControl;

        private void FlowControl_FlowCompleted(object? sender, FlowControlData FlowControlData)
        {
            flowControl.FlowCompleted -= FlowControl_FlowCompleted;
            if (_isDisposed) return;

            LUXFlowRunSession? session;
            lock (flowRunSessions)
                flowRunSessions.TryGetValue(FlowControlData.SerialNumber, out session);
            if (session == null)
            {
                log.Warn($"忽略没有运行会话的流程完成事件: {FlowControlData.SerialNumber}");
                return;
            }
            ProjectLUXReuslt flowResult = session.Result;

            stopwatch.Stop();
            timer.Change(Timeout.Infinite, 500); // 停止定时器

            log.Info($"流程执行Elapsed Time: {stopwatch.ElapsedMilliseconds} ms");
            flowResult.RunTime = stopwatch.ElapsedMilliseconds;
            FlowNodeRecordDataBaseHelper.RecordFlowRun(
                session.TemplateId,
                session.FlowName,
                FlowControlData.SerialNumber,
                FlowControlData.FlowStatus,
                flowResult.RunTime);
            logTextBox.Text = session.FlowName + Environment.NewLine + FlowControlData.EventName;

            ReleaseFlowRun(session);

            if (FlowControlData.EventName == "Completed")
            {
                flowResult.Msg = "Completed";
                try
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        Processing(FlowControlData.SerialNumber, session);
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message);
                }
            }
            else if (FlowControlData.EventName == "OverTime")
            {
                log.Info("流程运行超时，正在重新尝试");
                flowResult.FlowStatus = FlowStatus.OverTime;
                flowResult.Msg = logTextBox.Text;
                ViewResultManager.Save(flowResult, session.ResultConfig);

                flowEngine.LoadFromBase64(string.Empty);
                Refresh();

                if (session.Attempt < ProjectLUXConfig.Instance.TryCountMax)
                {
                    Task.Delay(200).ContinueWith(t =>
                    {
                        log.Info("重新尝试运行流程");
                        Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            _ = RunTemplateCore(session.Attempt + 1, session.ResultConfig);
                        });
                    });
                    return;
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(ReturnCode))
                    {
                        ReturnCode += $"FlowFailed:{FlowControlData.EventName},{FlowControlData.Params};";
                        SocketMessageManager.GetInstance().AddMessage(new SocketMessage
                        {
                            Direction = SocketMessageDirection.Sent,
                            Content = ReturnCode,
                            MessageTime = DateTime.Now,
                        });
                        if (Stream != null)
                            Stream.Write(Encoding.UTF8.GetBytes(ReturnCode));
                    }
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(ReturnCode))
                {
                    ReturnCode += $"FlowFailed:{FlowControlData.EventName},{FlowControlData.Params};";
                    SocketMessageManager.GetInstance().AddMessage(new SocketMessage
                    {
                        Direction = SocketMessageDirection.Sent,
                        Content = ReturnCode,
                        MessageTime = DateTime.Now,
                    });
                    if (Stream != null)
                        Stream.Write(Encoding.UTF8.GetBytes(ReturnCode));
                }

                log.Error("流程运行失败" + FlowControlData.EventName + FlowControlData.Params);
                flowResult.FlowStatus = FlowStatus.Failed;
                flowResult.Msg = FlowControlData.Params;

                //算法失败但是图像是有的，可以帮助用户即使发现原因
                if (flowResult.Msg.Contains("SDK return failed") || flowResult.Msg.Contains("BinocularFusion calculation failed") || flowResult.Msg.Contains("Not get cie file"))
                {
                    MeasureBatchModel Batch = BatchResultMasterDao.Instance.GetByCode(FlowControlData.SerialNumber);
                    if (Batch != null)
                    {
                        var values = MeasureImgResultDao.Instance.GetAllByBatchId(Batch.Id);
                        if (values.Count > 0)
                        {
                            flowResult.FileName = values[0].FileUrl;
                        }
                    }
                }
                logTextBox.Text = session.FlowName + Environment.NewLine + FlowControlData.EventName + Environment.NewLine + FlowControlData.Params;
                ViewResultManager.Save(flowResult, session.ResultConfig);
            }
        }

        private void Processing(string SerialNumber, LUXFlowRunSession session)
        {
            ViewResultManagerConfig resultConfig = session.ResultConfig;
            MeasureBatchModel Batch = BatchResultMasterDao.Instance.GetByCode(SerialNumber);

            if (Batch == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到批次号，请检查流程配置", "ColorVision");
                return;
            }
            ProjectLUXReuslt result = session.Result;

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
                        string path = Path.Combine(ProjectLUXConfig.Instance.ResultSavePath, $"C_{result.SN}.csv");
                        if (Directory.Exists(ProjectLUXConfig.Instance.ResultSavePath))
                        {
                            log.Info("savepath" + path);
                            ObjectiveTestResultCsvExporter.ExportToCsv(ObjectiveTestResult, path);
                        }
                        else
                        {
                            log.Info("无法连接到" + ProjectLUXConfig.Instance.ResultSavePath);
                        }

                        if (resultConfig.IsSaveImageReuslt)
                            automaticImageExportConfigs[result] = resultConfig;
                        ViewResultManager.Save(result, resultConfig);
                        ObjectiveTestResult.TotalResult = ObjectiveTestResult.TotalResult && result.Result;
                        SaveObjectiveTestResultRecord(result);

                        if (!string.IsNullOrWhiteSpace(ReturnCode))
                        {
                            if (SummaryManager.GetInstance().Summary.MachineNO == "H03AR" && result.TestType == 0)
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

                            try
                            {
                                if (Stream != null)
                                    Stream.Write(Encoding.UTF8.GetBytes(ReturnCode));
                                else
                                {
                                    log.Info("找不到通信连接");
                                }
                            }
                            catch (Exception ex)
                            {
                                log.Error("socket连接出错", ex);
                            }
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
                    log.Info($"匹配到不到自定义流程");
                }
            }
            catch (Exception ex)
            {
                log.Error("匹配/执行自定义 IProcess 出错，回退内置逻辑", ex);
            }
            if (resultConfig.IsSaveImageReuslt)
                automaticImageExportConfigs[result] = resultConfig;
            ViewResultManager.Save(result, resultConfig);
            ObjectiveTestResult.TotalResult = ObjectiveTestResult.TotalResult && result.Result;
            SaveObjectiveTestResultRecord(result);
        }

        private void SaveObjectiveTestResultRecord(ProjectLUXReuslt result)
        {
            try
            {
                ObjectiveTestResultRecordId = ViewResultManager.SaveObjectiveTestResult(ObjectiveTestResultRecordId, result, ObjectiveTestResult);
                log.Info($"保存 ObjectiveTestResult 记录：{ObjectiveTestResultRecordId}");
            }
            catch (Exception ex)
            {
                log.Error("保存 ObjectiveTestResult 记录失败", ex);
            }
        }

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            ProjectLUXConfig.Instance.Height = row2.ActualHeight;
            row2.Height = GridLength.Auto;
        }

        private void Button_Click_Clear(object sender, RoutedEventArgs e)
        {
            Interlocked.Increment(ref _resultImageRequestId);
            ViewResultManager.ViewReslutsSelectedIndex = -1;
            ViewResluts.Clear();
            ImageView.Clear();
            outputText.Document.Blocks.Clear();
            outputText.Background = Brushes.White;
        }

        private void listView1_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isDisposed) return;
            if (sender is not ListView listView) return;

            int requestId = Interlocked.Increment(ref _resultImageRequestId);
            ImageView.Clear();
            if (listView.SelectedItem is ProjectLUXReuslt result)
            {
                listView.ScrollIntoView(result);
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

                _ = Task.Run(() =>
                {
                    if (_isDisposed || requestId != Volatile.Read(ref _resultImageRequestId) || !File.Exists(result.FileName))
                        return;

                    _ = Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        if (_isDisposed ||
                            requestId != Volatile.Read(ref _resultImageRequestId) ||
                            !ReferenceEquals(listView1.SelectedItem, result))
                            return;

                        ImageView.OpenImage(result.FileName);
                        ImageView.ImageShow.Clear();

                        if (result.FlowStatus != FlowStatus.Completed)
                            return;

                        var meta = ProcessMetas.FirstOrDefault(m => string.Equals(m.FlowTemplate, result.Model, StringComparison.OrdinalIgnoreCase));
                        if (meta?.Process != null)
                        {
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

                        SaveImageResultIfNeeded(result, requestId);
                    });
                });

            }
        }

        internal static void DetachResultListView(ListView listView, SelectionChangedEventHandler selectionChangedHandler)
        {
            listView.SelectionChanged -= selectionChangedHandler;
            BindingOperations.ClearBinding(listView, System.Windows.Controls.Primitives.Selector.SelectedIndexProperty);
            listView.ItemsSource = null;
            listView.ContextMenu = null;
            listView.CommandBindings.Clear();
        }

        private void SaveImageResultIfNeeded(ProjectLUXReuslt result, int requestId)
        {
            if (!automaticImageExportConfigs.Remove(result, out ViewResultManagerConfig? resultConfig))
                return;

            log.Info($"IsSaveImageReuslt:{resultConfig.IsSaveImageReuslt}");
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(resultConfig.SaveImageReusltDelay);
                    if (_isDisposed || requestId != Volatile.Read(ref _resultImageRequestId)) return;

                    bool isCurrentSelection = Application.Current?.Dispatcher.Invoke(() =>
                        !_isDisposed &&
                        requestId == Volatile.Read(ref _resultImageRequestId) &&
                        ReferenceEquals(listView1.SelectedItem, result)) ?? false;
                    if (!isCurrentSelection) return;

                    string filePath = BuildAutomaticImageSavePath(resultConfig, result, DateTime.Now);
                    string? linkPath = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrWhiteSpace(linkPath) && !Directory.Exists(linkPath))
                        Directory.CreateDirectory(linkPath);
                    log.Info(filePath);
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        if (_isDisposed ||
                            requestId != Volatile.Read(ref _resultImageRequestId) ||
                            !ReferenceEquals(listView1.SelectedItem, result))
                            return;

                        ImageView.Save(filePath);
                    });
                }
                catch (Exception ex)
                {
                    log.Error("保存结果截图失败", ex);
                }
            });
        }

        internal static string BuildAutomaticImageSavePath(ViewResultManagerConfig config, ProjectLUXReuslt result, DateTime saveTime)
        {
            string linkPath = config.CsvSavePath;
            if (config.SaveByDate)
                linkPath = Path.Combine(linkPath, saveTime.ToString("yyyy-MM-dd"));

            string sn = result.SN;
            if (!string.IsNullOrWhiteSpace(sn))
            {
                foreach (char c in Path.GetInvalidFileNameChars())
                    sn = sn.Replace(c.ToString(), string.Empty);
                if (!string.IsNullOrWhiteSpace(sn))
                    linkPath = Path.Combine(linkPath, sn);
            }

            return Path.Combine(linkPath, $"{result.Model}.png");
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
            if (_isDisposed)
                return;

            _isDisposed = true;
            _lifetimeCancellation.Cancel();
            Interlocked.Increment(ref _resultImageRequestId);
            automaticImageExportConfigs.Clear();
            lock (flowRunSessions)
                flowRunSessions.Clear();
            Interlocked.Exchange(ref activeFlowRun, null);
            DetachResultListView(listView1, listView1_SelectionChanged);
            ProcessManager.ActiveGroupChanged -= ProcessManager_ActiveGroupChanged;
            ImageView.Dispose();
            if (flowControl != null)
            {
                flowControl.FlowCompleted -= FlowControl_FlowCompleted;
                flowControl.Stop();
            }
            flowEngine?.Dispose();
            STNodeEditorMain?.Dispose();
            timer?.Change(Timeout.Infinite, 500);
            timer?.Dispose();
            logOutput?.Dispose();
            DataContext = null;
            GC.SuppressFinalize(this);
        }

        internal sealed class LUXFlowRunSession
        {
            public LUXFlowRunSession(
                int templateId,
                string flowName,
                int attempt,
                ProjectLUXReuslt result,
                ViewResultManagerConfig resultConfig)
            {
                TemplateId = templateId;
                FlowName = flowName;
                Attempt = attempt;
                Result = result;
                ResultConfig = resultConfig;
            }

            public int TemplateId { get; }
            public string FlowName { get; }
            public int Attempt { get; }
            public ProjectLUXReuslt Result { get; }
            public ViewResultManagerConfig ResultConfig { get; }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string sn = "ssss";
            string path = Path.Combine(ProjectLUXConfig.Instance.ResultSavePath, $"C_{sn}.csv");
            ObjectiveTestResult TestResult = new ObjectiveTestResult();
            TestResult.VRMTFHTestResult = new Process.VR.MTFH.VRMTFHTestResult();
            for (int i = 0; i < 80; i++)
            {
                ObjectiveTestItem objectiveTestItem = new ObjectiveTestItem() { Name = i.ToString()  ,Value = i};
                TestResult.VRMTFHTestResult.ObjectiveTestItems.Add(objectiveTestItem);
            }
            ObjectiveTestResultCsvExporter.ExportToCsv(TestResult, path);
        }

        private void ExportObjectiveTestResult_Click(object sender, RoutedEventArgs e)
        {
            string sn = ProjectLUXConfig.Instance.SN;

            string defaultPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "导出 ObjectiveTestResult",
                InitialDirectory = defaultPath
            };

            if (dialog.ShowDialog(this) != true) return;

            try
            {
                string path = Path.Combine(dialog.FolderName, $"C_{sn}.csv");
                ObjectiveTestResultCsvExporter.ExportToCsv(ObjectiveTestResult, path);
                log.Info("手动导出 ObjectiveTestResult：" + path);
                MessageBox.Show(this, "导出完成：" + path, "ColorVision");
            }
            catch (Exception ex)
            {
                log.Error("手动导出 ObjectiveTestResult 失败", ex);
                MessageBox.Show(this, "导出失败：" + ex.Message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ObjectiveTestResultRecord_Click(object sender, RoutedEventArgs e)
        {
            var window = new ObjectiveTestResultRecordWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.ShowDialog();
        }
    }
}
