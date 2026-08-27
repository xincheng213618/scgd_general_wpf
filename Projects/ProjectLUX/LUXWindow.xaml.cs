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
using ColorVision.ImageEditor;
using ColorVision.SocketProtocol;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.LogImp;
using FlowEngineLib;
using FlowEngineLib.Base;
using log4net;
using ProjectLUX.Fix;
using ProjectLUX.ImageExport;
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
using System.Windows.Media.Imaging;

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
        private readonly ResultImagePlaceholderCache _resultImagePlaceholderCache = new();
        private readonly SemaphoreSlim _resultImagePresentationGate = new(1, 1);
        private readonly HashSet<ProjectLUXReuslt> _automaticImageExportResults = new(ReferenceEqualityComparer.Instance);
        private long _resultImagePresentationVersion;
        private CancellationTokenSource? _resultImagePresentationCancellation;
        private static readonly HashSet<string> ResultOverlayConfigNames =
        [
            nameof(ProjectLUXConfig.ResultOverlayShowName),
            nameof(ProjectLUXConfig.ResultOverlayShowDetail),
            nameof(ProjectLUXConfig.ResultOverlayFontSize),
            nameof(ProjectLUXConfig.ResultOverlayAutoRefresh)
        ];
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
            ProjectConfig.PropertyChanged += ProjectConfig_PropertyChanged;
            ApplyResultOverlayConfig();

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
            ImageView.ExternalRenderCompleted += ImageView_ExternalRenderCompleted;

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
        private int _currentFlowTemplateId;
        string FlowName;
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
            if (_isDisposed) return;
            try
            {
                if (flowControl != null && flowControl.IsFlowRun) return;
                if (FlowTemplate.SelectedItem is not TemplateModel<FlowParam> template) return;

                TryCount++;
                _currentFlowTemplateId = template.Id;
                string flowName = template.Key;
                LastFlowTime = await Task.Run(
                    () => FlowNodeRecordDataBaseHelper.GetLastCompletedFlowElapsed(
                        new FlowIdentity(template.Id, template.Value.FlowKey, flowName)));
                if (_isDisposed) return;

                CurrentFlowResult = new ProjectLUXReuslt();
                CurrentFlowResult.SN = ProjectLUXConfig.Instance.SN;
                CurrentFlowResult.Model = flowName;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (ProcessMetas.FirstOrDefault(m => string.Equals(m.FlowTemplate, flowName, StringComparison.OrdinalIgnoreCase)) is ProcessMeta processMeta)
                    {
                        CurrentFlowResult.TestType = ProcessMetas.IndexOf(processMeta);
                        ProjectLUXConfig.Instance.StepIndex = CurrentFlowResult.TestType;
                    }
                    else
                    {
                        CurrentFlowResult.TestType = -1;
                        ProjectLUXConfig.Instance.StepIndex = CurrentFlowResult.TestType;
                    }
                });

                FlowName = flowName;

                ProcessMeta? processMeta = ProcessManager.ProcessMetas.FirstOrDefault(a => a.FlowTemplate == FlowName);
                if (processMeta != null)
                {
                    int index = ProcessManager.ProcessMetas.IndexOf(processMeta);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ProjectLUXConfig.Instance.StepIndex = index;
                    });
                }

                CurrentFlowResult.Code = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");

                await Refresh();
                if (_isDisposed) return;

                bool preprocessingSucceeded = await PreProcessing(FlowName, CurrentFlowResult.Code);
                if (_isDisposed) return;
                if (!preprocessingSucceeded)
                {
                    CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                    CurrentFlowResult.Msg = "PreProcessFailed";
                    logTextBox.Text = FlowName + Environment.NewLine + "预处理失败";
                    TryCount = 0;
                    return;
                }

                CurrentFlowResult.FlowStatus = FlowStatus.Ready;

                flowControl ??= new FlowControl(MQTTControl.GetInstance(), flowEngine);
                flowControl.FlowCompleted -= FlowControl_FlowCompleted;
                flowControl.FlowCompleted += FlowControl_FlowCompleted;
                stopwatch.Reset();
                stopwatch.Start();
                MeasureBatchModel measureBatchModel = new MeasureBatchModel() { Name = CurrentFlowResult.SN, Code = CurrentFlowResult.Code };
                using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                int id = Db.Insertable(measureBatchModel).ExecuteReturnIdentity();
                CurrentFlowResult.BatchId = id;

                bool started = await flowControl.TryStartAsync(CurrentFlowResult.Code, _lifetimeCancellation.Token);
                if (_isDisposed)
                {
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
                        SerialNumber = CurrentFlowResult.Code,
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

            stopwatch.Stop();
            timer.Change(Timeout.Infinite, 500); // 停止定时器

            log.Info($"流程执行Elapsed Time: {stopwatch.ElapsedMilliseconds} ms");
            CurrentFlowResult.RunTime = stopwatch.ElapsedMilliseconds;
            FlowNodeRecordDataBaseHelper.RecordFlowRun(
                _currentFlowTemplateId,
                FlowName,
                FlowControlData.SerialNumber,
                FlowControlData.FlowStatus,
                CurrentFlowResult.RunTime);
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
                TryCount = 0;
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

                        ViewResultManagerConfig exportConfig = ViewResultManager.Config;
                        if (exportConfig.IsSaveImageReuslt || exportConfig.IsSaveSourceImage)
                            _automaticImageExportResults.Add(result);
                        ViewResultManager.Save(result);
                        if (_automaticImageExportResults.Contains(result)
                            && !ReferenceEquals(listView1.SelectedItem, result))
                        {
                            listView1.SelectedItem = result;
                            listView1.ScrollIntoView(result);
                        }
                        ObjectiveTestResult.TotalResult = ObjectiveTestResult.TotalResult && result.Result;
                        SaveObjectiveTestResultRecord(result);

                        if (!string.IsNullOrWhiteSpace(ReturnCode))
                        {
                            if (SummaryManager.GetInstance().Summary.MachineNO == "H03AR"&&CurrentFlowResult?.TestType == 0)
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
            ViewResultManager.Save(result);
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
            Interlocked.Increment(ref _resultImagePresentationVersion);
            Interlocked.Exchange(ref _resultImagePresentationCancellation, null)?.Cancel();
            ViewResultManager.ViewReslutsSelectedIndex = -1;
            ViewResluts.Clear();
            ImageView.Clear();
            outputText.Document.Blocks.Clear();
            outputText.Background = Brushes.White;
        }

        private void Button_Click_EditResultConfig(object sender, RoutedEventArgs e)
        {
            ViewResultManager.Config.SourceImageSupportsBmp = CanCurrentSourceExportBmp();
            ViewResultManager.EditConfig();
        }

        private void listView1_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isDisposed)
                return;

            long requestVersion = Interlocked.Increment(ref _resultImagePresentationVersion);
            Interlocked.Exchange(ref _resultImagePresentationCancellation, null)?.Cancel();

            if (sender is ListView listView && listView.SelectedItem is ProjectLUXReuslt result)
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

                IReadOnlyList<ResultImageFileCandidate> imageCandidates = ResultImageFileCandidates.GetExisting(result);
                CancellationTokenSource requestCancellation = new();
                Interlocked.Exchange(ref _resultImagePresentationCancellation, requestCancellation)?.Cancel();
                _ = Application.Current.Dispatcher.BeginInvoke(async () =>
                {
                    bool gateEntered = false;
                    try
                    {
                        await _resultImagePresentationGate.WaitAsync(requestCancellation.Token);
                        gateEntered = true;
                        if (!IsCurrentResultImageRequest(requestVersion, result))
                            return;

                        bool hasDisplaySurface = false;
                        bool renderOverlays = true;
                        ResultImageFileCandidate? openedCandidate = await ResultImageFileCandidates.OpenFirstAsync(
                            imageCandidates,
                            async (candidate, cancellationToken) =>
                            {
                                BitmapSource? loadedSource = await OpenResultImageAsync(candidate.FilePath, cancellationToken);
                                if (!IsCurrentResultImageRequest(requestVersion, result))
                                    throw new OperationCanceledException(cancellationToken);
                                return loadedSource != null;
                            },
                            (candidate, exception) =>
                            {
                                if (exception is TimeoutException)
                                    log.Warn($"加载结果图片超时，将尝试下一候选图：{candidate.FilePath}", exception);
                                else if (exception != null)
                                    log.Warn($"加载结果图片失败，将尝试下一候选图：{candidate.FilePath}", exception);
                                else
                                    log.Warn($"加载结果图片后没有有效图像，将尝试下一候选图：{candidate.FilePath}");
                            },
                            requestCancellation.Token);
                        if (openedCandidate is ResultImageFileCandidate candidate
                            && GetLoadedImageSource() is BitmapSource)
                        {
                            hasDisplaySurface = true;
                            renderOverlays = candidate.RequiresOverlayRendering;
                            if (candidate.Kind != ResultImageFileKind.Original)
                                log.Info($"算法原图不可用，已改用{DescribeResultImageCandidate(candidate.Kind)}：{candidate.FilePath}");
                        }

                        if (!hasDisplaySurface)
                        {
                            if (TryGetResultImageDimensions(result, out int width, out int height))
                            {
                                ShowResultImagePlaceholder(width, height);
                                hasDisplaySurface = true;
                            }
                            else
                            {
                                ClearResultImageSurface();
                                log.Warn($"结果图片不存在且没有可用尺寸，已清除旧底图：resultId={result.Id}, file={result.FileName}");
                            }
                        }

                        if (hasDisplaySurface && HasResultDisplaySurface())
                        {
                            if (renderOverlays)
                                RenderResultImage(result);
                            else
                                ShowSavedResultImage(result);
                        }
                        else
                        {
                            ImageView.NotifyExternalRenderCompleted(result, succeeded: false);
                        }
                    }
                    catch (OperationCanceledException) when (requestCancellation.IsCancellationRequested)
                    {
                        _automaticImageExportResults.Remove(result);
                    }
                    catch (Exception ex)
                    {
                        if (IsCurrentResultImageRequest(requestVersion, result))
                        {
                            ImageView.NotifyExternalRenderCompleted(result, succeeded: false);
                            ClearResultImageSurface();
                        }
                        log.Error("加载结果图片失败", ex);
                    }
                    finally
                    {
                        if (gateEntered)
                            _resultImagePresentationGate.Release();
                        Interlocked.CompareExchange(ref _resultImagePresentationCancellation, null, requestCancellation);
                        requestCancellation.Dispose();
                    }
                });
            }
            else
            {
                ClearResultImageSurface();
            }
        }

        private void RenderResultImage(ProjectLUXReuslt result)
        {
            bool succeeded = false;
            try
            {
                ImageView.ImageShow.Clear();
                ApplyResultOverlayConfig();

                if (result.FlowStatus != FlowStatus.Completed)
                    return;

                var meta = ProcessMetas.FirstOrDefault(m => string.Equals(m.FlowTemplate, result.Model, StringComparison.OrdinalIgnoreCase));
                if (meta?.Process == null)
                    return;

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
                succeeded = HasResultDisplaySurface();
            }
            catch (Exception ex)
            {
                log.Error("自定义 IProcess 执行异常", ex);
            }
            finally
            {
                ImageView.NotifyExternalRenderCompleted(result, succeeded);
            }
        }

        private void ShowSavedResultImage(ProjectLUXReuslt result)
        {
            ImageView.ImageShow.Clear();
            ImageView.NotifyExternalRenderCompleted(result, succeeded: HasResultDisplaySurface());
        }

        private static string DescribeResultImageCandidate(ResultImageFileKind kind) => kind switch
        {
            ResultImageFileKind.SavedSource => "已保存原图并重新渲染标记",
            ResultImageFileKind.SavedResult => "已保存标记图",
            _ => "算法原图",
        };

        private async Task<BitmapSource?> OpenResultImageAsync(string filePath, CancellationToken cancellationToken)
        {
            string? activeFilePath = ImageView.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath);
            if (string.Equals(activeFilePath, filePath, StringComparison.OrdinalIgnoreCase)
                && GetLoadedImageSource() is BitmapSource currentSource)
                return currentSource;

            TaskCompletionSource<ImageViewImageSourceLoadedEventArgs> imageLoaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<ImageViewImageSourceLoadedEventArgs> imageSourceLoaded = (_, e) => imageLoaded.TrySetResult(e);
            ImageView.ImageSourceLoaded += imageSourceLoaded;
            try
            {
                ImageView.OpenImage(filePath);
                ImageViewImageSourceLoadedEventArgs loaded = await imageLoaded.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
                activeFilePath = ImageView.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath);
                if (!string.Equals(activeFilePath, filePath, StringComparison.OrdinalIgnoreCase)
                    || !ImageView.IsCurrentImageRevision(loaded.ImageRevision))
                {
                    return null;
                }

                return loaded.Source as BitmapSource;
            }
            finally
            {
                ImageView.ImageSourceLoaded -= imageSourceLoaded;
            }
        }

        private bool IsCurrentResultImageRequest(long requestVersion, ProjectLUXReuslt result)
        {
            return !_isDisposed
                && requestVersion == Volatile.Read(ref _resultImagePresentationVersion)
                && ReferenceEquals(listView1.SelectedItem, result);
        }

        private static bool TryGetResultImageDimensions(ProjectLUXReuslt result, out int width, out int height)
        {
            width = result.ImageWidth.GetValueOrDefault();
            height = result.ImageHeight.GetValueOrDefault();
            return width > 0 && height > 0;
        }

        private void ShowResultImagePlaceholder(int width, int height)
        {
            DrawingImage placeholder = _resultImagePlaceholderCache.GetOrCreate(width, height);
            if (!_resultImagePlaceholderCache.IsCurrent(ImageView.ImageShow.Source, width, height))
            {
                ImageView.Clear();
                ImageView.Config.SetImageMetadata(ImageViewPropertyKeys.Cols, width, nameof(LUXWindow), "历史结果坐标空间宽度");
                ImageView.Config.SetImageMetadata(ImageViewPropertyKeys.Rows, height, nameof(LUXWindow), "历史结果坐标空间高度");
                ImageView.Config.SetImageMetadata(ImageViewPropertyKeys.ImageWidth, width, nameof(LUXWindow), "历史结果图像像素宽度");
                ImageView.Config.SetImageMetadata(ImageViewPropertyKeys.ImageHeight, height, nameof(LUXWindow), "历史结果图像像素高度");
                ImageView.SetImageSource(placeholder, enableEditorImageServices: false, configureDefaultLayerController: false);
                ImageView.UpdateZoomAndScale();
            }
        }

        private void ClearResultImageSurface()
        {
            string? activeFilePath = ImageView.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath);
            if (ImageView.ImageShow.Source != null || !string.IsNullOrWhiteSpace(activeFilePath))
                ImageView.Clear();
        }

        private bool HasResultDisplaySurface() => ImageView.ImageShow.Source != null;

        private BitmapSource? GetLoadedImageSource()
        {
            return ImageView.ViewBitmapSource as BitmapSource
                ?? ImageView.ImageShow.Source as BitmapSource;
        }

        private void ImageView_ExternalRenderCompleted(
            object? sender,
            ImageViewExternalRenderCompletedEventArgs e)
        {
            if (e.Context is not ProjectLUXReuslt result
                || !_automaticImageExportResults.Remove(result))
                return;

            if (_isDisposed
                || !e.Succeeded
                || e.Source is not BitmapSource
                || !ImageView.IsCurrentImageRevision(e.ImageRevision))
            {
                log.Warn("图像导出已取消：本次结果的图像加载或外部渲染未成功完成。");
                return;
            }

            log.Info("ImageEditor图像加载及外部点位渲染已完成，开始捕获本次结果快照。");
            StartImageExportFromLoadedImage(result);
        }

        private bool CanCurrentSourceExportBmp()
        {
            if (!ImageView.Dispatcher.CheckAccess())
                return ImageView.Dispatcher.Invoke(CanCurrentSourceExportBmp);

            BitmapSource? source = GetLoadedImageSource();
            return source != null && ColorVision.ImageEditor.ImageView.CanBmpPreserveSourceBitDepth(source.Format);
        }

        private void StartImageExportFromLoadedImage(ProjectLUXReuslt result)
        {
            ViewResultManagerConfig config = ViewResultManager.Config;
            bool saveResultImage = config.IsSaveImageReuslt;
            bool saveSourceImage = config.IsSaveSourceImage;
            ResultImageFormat resultFormat = config.ResultSnapshotFormat;
            ImageExportSize resultSize = config.ResultSnapshotSize;
            bool includeOverlays = saveResultImage && config.ResultSnapshotIncludeOverlays;
            SourceImageFormat sourceFormat = config.SourceExportFormat;
            SourceTiffCompression sourceTiffCompression = config.SourceTiffCompressionMode;
            string outputRoot = config.CsvSavePath;
            bool saveByDate = config.SaveByDate;
            DateTime requestedAt = result.CreateTime == default ? DateTime.Now : result.CreateTime;

            ImageViewSnapshot? snapshot = null;
            try
            {
                if (_isDisposed || (!saveResultImage && !saveSourceImage))
                    return;
                ImageView.Dispatcher.VerifyAccess();

                log.Info($"准备图像导出：8位标记图={saveResultImage}，保留位深原图={saveSourceImage}");

                BitmapSource? loadedSource = GetLoadedImageSource();
                if (loadedSource == null)
                {
                    log.Warn("图像导出失败：渲染完成后ImageEditor仍没有有效像素源；不会回读CVRAW或其他磁盘文件。");
                    return;
                }

                if (saveSourceImage
                    && sourceFormat == SourceImageFormat.BMP
                    && !ColorVision.ImageEditor.ImageView.CanBmpPreserveSourceBitDepth(loadedSource.Format))
                {
                    saveSourceImage = false;
                    log.Warn(
                        $"当前原图格式为 {loadedSource.Format}（{loadedSource.Format.BitsPerPixel}bpp），"
                        + "BMP无法逐像素保留该位深；已跳过原图BMP，请改选PNG或TIFF。");
                }

                if (!saveResultImage && !saveSourceImage)
                    return;

                Stopwatch snapshotStopwatch = Stopwatch.StartNew();
                snapshot = ImageView.CaptureSnapshotForBackgroundSave(includeOverlays);
                snapshotStopwatch.Stop();
                if (snapshot == null)
                {
                    log.Warn("图像导出失败：ImageEditor无法生成后台快照。");
                    return;
                }
                log.Info(
                    $"ImageEditor像素与场景快照准备完成，源格式 {loadedSource.Format}，"
                    + $"耗时 {snapshotStopwatch.ElapsedMilliseconds}ms。");

                if (_isDisposed)
                    return;

                _ = ExportImagesAsync(
                    snapshot,
                    saveResultImage,
                    saveSourceImage,
                    resultFormat,
                    resultSize,
                    includeOverlays,
                    sourceFormat,
                    sourceTiffCompression,
                    result,
                    outputRoot,
                    saveByDate,
                    requestedAt);
                snapshot = null;
            }
            catch (Exception ex)
            {
                log.Error("准备ImageEditor图像导出任务失败", ex);
            }
            finally
            {
                snapshot?.Dispose();
            }
        }

        private async Task ExportImagesAsync(
            ImageViewSnapshot? snapshot,
            bool saveResultImage,
            bool saveSourceImage,
            ResultImageFormat resultFormat,
            ImageExportSize resultSize,
            bool includeOverlays,
            SourceImageFormat sourceFormat,
            SourceTiffCompression sourceTiffCompression,
            ProjectLUXReuslt result,
            string outputRoot,
            bool saveByDate,
            DateTime requestedAt)
        {
            string? renderedFilePath = null;
            string? sourceFilePath = null;
            Stopwatch? exportStopwatch = null;
            bool exportCompleted = false;
            ProjectImageExportAttempt? exportAttempt = null;
            ProjectImageExportAttemptResult exportResult = new();
            try
            {
                if (_isDisposed)
                    return;

                string outputDirectory = ProjectImageExportService.BuildOutputDirectory(
                    outputRoot,
                    saveByDate,
                    requestedAt,
                    result.SN);

                if (snapshot == null)
                    return;

                string sourceName = string.IsNullOrWhiteSpace(result.FileName)
                    ? $"Image_{result.Id}_{requestedAt:yyyyMMddTHHmmssfffffff}"
                    : result.FileName;
                if (saveResultImage)
                {
                    string fileStem = ProjectImageExportService.BuildResultFileStem(sourceName, result.Model);
                    renderedFilePath = ProjectImageExportService.BuildFilePath(
                        outputDirectory,
                        fileStem,
                        ProjectImageExportService.GetResultExtension(resultFormat));
                    string overlayDescription = includeOverlays ? "混合标记" : "仅底图";
                    log.Info(
                        $"后台导出8位标记图：{resultFormat}，{DescribeImageSize(resultSize)}，{overlayDescription}，"
                        + (resultFormat == ResultImageFormat.JPEG ? "JPEG质量100" : "PNG自动压缩"));
                }
                if (saveSourceImage)
                {
                    string fileStem = ProjectImageExportService.BuildSourceFileStem(sourceName, result.Model);
                    sourceFilePath = ProjectImageExportService.BuildFilePath(
                        outputDirectory,
                        fileStem,
                        ProjectImageExportService.GetSourceExtension(sourceFormat));
                    string sourceDescription = sourceFormat switch
                    {
                        SourceImageFormat.TIFF => $"TIFF {sourceTiffCompression}无损压缩",
                        SourceImageFormat.PNG => "PNG自动无损压缩",
                        _ => "BMP（仅8位源图）",
                    };
                    log.Info($"后台导出原尺寸、原位深、无标记原图：{sourceDescription}");
                }

                exportAttempt = new ProjectImageExportAttempt(renderedFilePath, sourceFilePath);
                ImageViewSnapshotExportOptions exportOptions = exportAttempt.CreateOptions(
                    ProjectImageExportService.CreateRenderedOptions(resultFormat, resultSize),
                    ProjectImageExportService.CreateSourceOptions(sourceFormat, sourceTiffCompression));

                exportStopwatch = Stopwatch.StartNew();
                ImageViewSnapshot ownedSnapshot = snapshot;
                snapshot = null;
                await ColorVision.ImageEditor.ImageView.SaveSnapshotExportsAsync(
                    ownedSnapshot,
                    exportOptions).ConfigureAwait(false);
                exportCompleted = true;
            }
            catch (Exception ex)
            {
                log.Error("图像导出任务失败；已停止本任务，之前已经写盘的文件不会回滚。", ex);
            }
            finally
            {
                exportStopwatch?.Stop();
                if (exportAttempt != null)
                {
                    exportResult = exportAttempt.CommitSuccessfulChannels((channel, fileName, ex) =>
                        log.Error($"{channel}已编码，但替换正式导出文件失败：{fileName}", ex));
                    exportAttempt.Dispose();
                }
                ResultImageExportPathUpdate pathUpdate = ResultImageExportPathUpdate.From(
                    exportResult,
                    includeOverlays,
                    result.SavedResultImageFileName);
                if (pathUpdate.UpdateSavedResultImageFileName || pathUpdate.UpdateSavedSourceImageFileName)
                {
                    try
                    {
                        ViewResultManager.UpdateSavedImagePaths(result, pathUpdate);
                    }
                    catch (Exception ex)
                    {
                        log.Error("图像已写盘，但保存本次成功导出路径到结果数据库失败；内存结果未更新。", ex);
                    }
                }
                LogExportedImage("8位标记图", exportResult.RenderedFileName);
                LogExportedImage("原位深原图", exportResult.SourceFileName);
                if (exportStopwatch != null)
                {
                    string outcome = exportCompleted ? "完成" : "结束（含失败）";
                    log.Info($"ImageEditor图像导出任务{outcome}，总耗时 {exportStopwatch.ElapsedMilliseconds}ms。");
                }
                snapshot?.Dispose();
            }
        }

        private static string DescribeImageSize(ImageExportSize size) => size switch
        {
            ImageExportSize.二分之一尺寸 => "1/2尺寸",
            ImageExportSize.四分之一尺寸 => "1/4尺寸",
            _ => "完整尺寸",
        };

        private static void LogExportedImage(string label, string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return;

            FileInfo file = new(filePath);
            log.Info($"{label}写盘完成：{filePath}，{file.Length / 1024d / 1024d:F2}MiB。");
        }

        private void ApplyResultOverlayConfig()
        {
            ProjectLUXConfig config = ProjectLUXConfig.Instance;
            ImageView.Config.IsShowText = config.ResultOverlayShowName;
            ImageView.Config.IsShowMsg = config.ResultOverlayShowDetail;
            ImageView.Config.DrawingTextFontSize = config.ResultOverlayFontSize;
            ImageView.Config.IsLayoutUpdated = config.ResultOverlayAutoRefresh;
            ImageView.ImageShow.TextFontSizeOverride = config.ResultOverlayFontSize;
            ImageView.ImageShow.ApplyLayoutScaleToVisuals();
        }

        private void ProjectConfig_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.PropertyName) && !ResultOverlayConfigNames.Contains(e.PropertyName))
                return;

            ApplyResultOverlayConfig();
        }

        internal static void DetachResultListView(ListView listView, SelectionChangedEventHandler selectionChangedHandler)
        {
            listView.SelectionChanged -= selectionChangedHandler;
            BindingOperations.ClearBinding(listView, System.Windows.Controls.Primitives.Selector.SelectedIndexProperty);
            listView.ItemsSource = null;
            listView.ContextMenu = null;
            listView.CommandBindings.Clear();
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
            Interlocked.Increment(ref _resultImagePresentationVersion);
            Interlocked.Exchange(ref _resultImagePresentationCancellation, null)?.Cancel();
            _automaticImageExportResults.Clear();
            ImageView.ExternalRenderCompleted -= ImageView_ExternalRenderCompleted;
            DetachResultListView(listView1, listView1_SelectionChanged);
            ProcessManager.ActiveGroupChanged -= ProcessManager_ActiveGroupChanged;
            ProjectConfig.PropertyChanged -= ProjectConfig_PropertyChanged;
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
