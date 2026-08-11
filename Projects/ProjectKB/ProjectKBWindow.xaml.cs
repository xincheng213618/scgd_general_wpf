#pragma warning disable CA1805,CS4014,CS8601,CS8602,CS8604
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.FlowProcessing.PreProcess;
using ColorVision.Engine;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.Templates.Jsons.KB;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor.Draw;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.LogImp;
using FlowEngineLib;
using FlowEngineLib.Base;
using log4net;
using Newtonsoft.Json;
using ProjectKB.Auth;
using ProjectKB.Modbus;
using SqlSugar;
using ST.Library.UI.NodeEditor;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ProjectKB
{
    sealed class KBvalue
    {
        public double Y { get; set; }
        public int PixNumber { get; set; } = 1;
    }

    public class ProjectKBWindowConfig : WindowConfig
    {
        public static ProjectKBWindowConfig Instance => ConfigService.Instance.GetRequiredService<ProjectKBWindowConfig>();
    }

    /// <summary>
    /// Interaction logic for _windowInstance.xaml
    /// </summary>
    public partial class ProjectKBWindow : Window, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ProjectKBWindow));
        private static readonly TimeSpan RestartServicesTimeout = TimeSpan.FromMinutes(7);
        private static readonly TimeSpan RefreshAfterRestartTimeout = TimeSpan.FromSeconds(20);
        private const double DefaultRestartServicesExpectedDurationMs = 15000;
        private const int CenterDistanceLcNeighborhoodVersion = 2;
        private const double LegacyLcNeighborhoodPaddingPixels = 300;
        private static readonly Regex OutputDetailHeaderRegex = new(@"^\s*按键\s+\(PT\)\s+亮度\s+\(Lv\)\s+局部对比度\s+\(LC\)\s*$", RegexOptions.CultureInvariant);
        private static readonly Regex OutputDetailRowRegex = new(@"^\s*(?<key>\[[^\]\r\n]+\])\s+(?<lv>\S+)\s+(?<lc>\S+%)\s*(?<result>Fail)?\s*$", RegexOptions.CultureInvariant);
        private readonly SemaphoreSlim _refreshGate = new(1, 1);
        private readonly FlowNodeExecutionRecorder _flowNodeExecutionRecorder = new();
        private readonly Dictionary<KBItem, DVRectangle> _keyVisuals = new();
        private bool _isDisposed;
        private bool _isFlowStartPending;
        private bool _isFlowLifecycleActive;
        private int _resultImageRequestId;
        private KBItemMaster? _displayedKeyResult;
        private DVCircle? _lcNeighborhoodCircle;
        public static ViewResultManager ViewResultManager => ViewResultManager.GetInstance();
        public static ObservableCollection<KBItemMaster> ViewResluts => ViewResultManager.ViewResluts;
        public static ProjectKBWindowConfig Config => ProjectKBWindowConfig.Instance;

        public static Summary Summary => SummaryManager.GetInstance().Summary;

        public ProjectKBWindow()
        {
            InitializeComponent();
            outputText.CommandBindings.Add(new CommandBinding(
                ApplicationCommands.Copy,
                OutputText_Copy,
                (s, e) =>
                {
                    e.CanExecute = !outputText.Selection.IsEmpty;
                    e.Handled = true;
                }));
            this.ApplyCaption(false);
            Config.SetWindow(this);
            this.Title += "-" + Assembly.GetAssembly(typeof(ProjectKBWindow))?.GetName().Version?.ToString() ?? "";
        }
        public LogOutput? logOutput { get; set; }

        public static KBAuthManager AuthManager => KBAuthManager.GetInstance();

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = ProjectKBConfig.Instance;

            ViewResultManager.ListView = listView1;
            listView1.CommandBindings.Add(new CommandBinding(
                ApplicationCommands.Delete,
                (s, e) =>
                {
                    if (AuthManager.RequireAdmin(this))
                        ViewResultManager.Delete(listView1.SelectedIndex);
                },
                (s, e) => e.CanExecute = listView1.SelectedIndex > -1));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, (s, e) => listView1.SelectAll(), (s, e) => e.CanExecute = true));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, ListViewUtils.Copy, (s, e) => e.CanExecute = true));
            listView1.ItemsSource = ViewResluts;
            BuildListViewContextMenu();
            ImageView.EditorContext.DrawEditorContext.DrawCanvas.PreviewMouseLeftButtonDown += ImageCanvas_PreviewMouseLeftButtonDown;
            InitFlow();
            EnsureTimedButtonOperations();
            logOutput = new LogOutput("%date{HH:mm:ss} [%thread] %-5level %message%newline", ProjectKBLogConfig.Instance);
            LogGrid.Children.Add(logOutput);
            Task.Run(async () =>
            {
                if (ProjectKBConfig.Instance.AutoModbusConnect)
                {
                    bool con = await ModbusControl.GetInstance().Connect();
                    if (con)
                    {
                        log.Debug("初始化寄存器设置为0");
                        ModbusControl.GetInstance().SetRegisterValue(0);
                    }
                    ModbusControl.GetInstance().StatusChanged += ProjectKBWindow_StatusChanged;
                }
            });

            // 初始化权限系统
            InitAuth();

            this.Closed += (s, e) =>
            {
                ProjectKBConfig.Instance.SNChanged -= Instance_SNChanged;

                SummaryManager.GetInstance().Save();
                ModbusControl.GetInstance().StatusChanged -= ProjectKBWindow_StatusChanged;
                AuthManager.IsAdminChanged -= AuthManager_IsAdminChanged;
                AuthManager.AutoLoggedOut -= AuthManager_AutoLoggedOut;
                AuthManager.Dispose();
                this.Dispose();
            };

        }

        #region Auth

        private void InitAuth()
        {
            AuthManager.IsAdminChanged += AuthManager_IsAdminChanged;
            AuthManager.AutoLoggedOut += AuthManager_AutoLoggedOut;
            ApplyAuthState();
        }

        private void AuthManager_IsAdminChanged(object? sender, EventArgs e)
        {
            ApplyAuthState();
        }

        private void AuthManager_AutoLoggedOut(object? sender, EventArgs e)
        {
            CloseOwnedAdminWindows();
            logTextBox.Text = "空闲超时，已自动退出管理员模式";
            MessageBox.Show(this, $"空闲超时（{AuthManager.IdleTimeoutMinutes}分钟），已自动退出管理员模式。\n如需编辑配置请重新登录。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CloseOwnedAdminWindows()
        {
            foreach (Window ownedWindow in OwnedWindows.Cast<Window>().ToList())
            {
                if (ownedWindow is KBLoginWindow)
                    continue;

                ownedWindow.Close();
            }
        }

        private void ApplyAuthState()
        {
            if (!AuthManager.IsPermissionControlEnabled)
            {
                AuthModeText.Text = "🟡 全部权限";
                AuthModeText.Foreground = Brushes.DarkGoldenrod;
                AuthButton.Content = "权限未启用";
                TestStatusBarItem.IsEnabled = true;
                DatabaseCleanupButton.IsEnabled = true;
                ChangePasswordButton.IsEnabled = true;
                return;
            }

            bool isAdmin = AuthManager.IsAdmin;

            AuthModeText.Text = isAdmin ? "🔧 管理员" : "🟢 产线";
            AuthModeText.Foreground = isAdmin ? Brushes.Orange : Brushes.Green;
            AuthButton.Content = isAdmin ? "🔓 登出" : "🔐 登录";

            TestStatusBarItem.IsEnabled = true;
            DatabaseCleanupButton.IsEnabled = true;
            ChangePasswordButton.IsEnabled = true;
        }

        private void AuthButton_Click(object sender, RoutedEventArgs e)
        {
            if (!AuthManager.IsPermissionControlEnabled)
            {
                MessageBox.Show(this, "ProjectKB权限控制未启用。可在“设置”中开启“启用权限控制”。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (AuthManager.IsAdmin)
            {
                CloseOwnedAdminWindows();
                AuthManager.Logout();
            }
            else
            {
                AuthManager.RequireAdmin(this);
            }
        }

        private void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            if (!AuthManager.RequireAdmin(this)) return;

            var changePasswordWindow = new KBChangePasswordWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            changePasswordWindow.ShowDialog();
        }

        #endregion

        private void ProjectKBWindow_StatusChanged(object? sender, EventArgs e)
        {
            if (ModbusControl.GetInstance().CurrentValue == 1)
            {
                Application.Current.Dispatcher.BeginInvoke(async () =>
                {
                    if (ProjectKBConfig.Instance.IgnoreAutoRunWhenSnEmpty && string.IsNullOrWhiteSpace(SNtextBox.Text))
                    {
                        const string message = "PLC自动触发已忽略：SN为空，未执行流程。";
                        log.Warn(message);
                        logTextBox.Text = message;
                        _ = ModbusControl.GetInstance().SetRegisterValue(0);
                        return;
                    }

                    log.Info("触发拍照，执行流程");
                    await RunTemplate();
                });
            }
        }

        private void OpenDatabaseCleanup_Click(object sender, RoutedEventArgs e)
        {
            if (!AuthManager.RequireAdmin(this)) return;

            DatabaseCleanupWindow.OpenWindow();
        }

        public static RecipeManager RecipeManager => RecipeManager.GetInstance();

        public static KBRecipeConfig RecipeConfig => RecipeManager.RecipeConfig;

        private TimedButtonOperationRegistry EnsureTimedButtonOperations()
        {
            TimedButtonOperationRegistry operations = this.GetTimedButtonOperations(actionKey => $"projectkb:{actionKey}");
            operations.Register(RestartServicesButton, "restart-cv-windows-services", options =>
            {
                options.ContentFactory = stats => TimedButtonOperationTextFormatter.BuildCompactContent(BuildRestartServicesButtonText(), stats);
                options.ToolTipFactory = stats => TimedButtonOperationTextFormatter.BuildTooltip(BuildRestartServicesButtonText(), stats);
                options.RunningText = "重启服务";
            });
            return operations;
        }

        private static string BuildRestartServicesButtonText()
        {
            string version = ServiceConfig.Instance.RegistrationCenterServiceInfo.FileVersion;
            return string.IsNullOrWhiteSpace(version) ? "重启服务" : $"重启{version}";
        }

        private double GetExpectedRestartDurationMs()
        {
            TimedButtonOperationStats? stats = EnsureTimedButtonOperations().Get(RestartServicesButton)?.CurrentStats;
            if (stats?.SuccessCount > 0 && stats.AverageElapsedMs > 0) return stats.AverageElapsedMs;
            if (stats?.WarmupCount > 0 && stats.WarmupElapsedMs > 0) return stats.WarmupElapsedMs;
            return DefaultRestartServicesExpectedDurationMs;
        }

        private async void RestartServicesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!AuthManager.RequireAdmin(this)) return;

            TimedButtonOperationRegistry operations = EnsureTimedButtonOperations();
            if (operations.Get(RestartServicesButton)?.IsRunning == true) return;

            TimedButtonOperationScope? operationScope = operations.Begin(RestartServicesButton, GetExpectedRestartDurationMs(), "重启服务");
            bool success = false;
            try
            {
                logTextBox.Text = "正在重启ColorVision服务...";
                await DisplayFlow.RestartColorVisionServicesAsync().WaitAsync(RestartServicesTimeout);
                success = true;

                try
                {
                    await Refresh().WaitAsync(RefreshAfterRestartTimeout);
                    logTextBox.Text = "服务重启完成，当前流程已刷新";
                }
                catch (TimeoutException ex)
                {
                    log.Warn("服务重启完成，但刷新当前流程超时", ex);
                    logTextBox.Text = "服务重启完成，刷新当前流程超时，可手动切换流程刷新";
                }
            }
            catch (TimeoutException ex)
            {
                log.Error("重启ColorVision服务超时", ex);
                logTextBox.Text = "重启服务超时，已恢复按钮，可稍后重试";
                MessageBox.Show(this, $"重启服务超过 {RestartServicesTimeout.TotalMinutes:F0} 分钟未完成，请检查服务状态后重试。", "重启服务超时", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                log.Error("重启ColorVision服务失败", ex);
                logTextBox.Text = $"服务重启失败：{ex.Message}";
                MessageBox.Show(this, ex.Message, "重启服务失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                operationScope?.Complete(success);
                this.TryGetTimedButtonOperations()?.RefreshIdleState(RestartServicesButton);
            }
        }

        #region FlowRun
        public STNodeEditor STNodeEditorMain { get; set; }
        private FlowEngineControl flowEngine;
        private Timer timer;
        Stopwatch stopwatch = new Stopwatch();
        private int _pendingUiUpdate;

        public void InitFlow()
        {
            flowEngine = new FlowEngineControl(false);
            STNodeEditorMain = new STNodeEditor();
            STNodeEditorMain.LoadAssembly("FlowEngineLib.dll");
            flowEngine.AttachNodeEditor(STNodeEditorMain);
            ProjectKBConfig.Instance.SNChanged += Instance_SNChanged;

            FlowTemplate.SelectionChanged += (s, e) =>
            {
                if (ProjectKBConfig.Instance.TemplateSelectedIndex > -1)
                {
                    string Name = TemplateFlow.Params[ProjectKBConfig.Instance.TemplateSelectedIndex].Key;
                    RecipeManager.SetCurrentTemplate(Name);
                    RecipeManager.Save();

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


        public Task Refresh()
        {
            return FlowTemplate.SelectedItem is TemplateModel<FlowParam> template
                ? Refresh(template)
                : Task.CompletedTask;
        }

        private async Task Refresh(TemplateModel<FlowParam> template)
        {
            await _refreshGate.WaitAsync();
            try
            {
                if (!EnsureFlowEngineAvailable()) return;

                await RefreshCoreAsync(template);
            }
            catch (ObjectDisposedException ex)
            {
                log.Warn("刷新流程时流程编辑器已释放，正在重建流程编辑器", ex);
                if (!RebuildFlowEngine()) return;

                try
                {
                    await RefreshCoreAsync(template);
                }
                catch (Exception retryEx)
                {
                    log.Error("重建流程编辑器后刷新流程失败", retryEx);
                    ClearFlowSafely();
                }
            }
            catch (Exception ex)
            {
                log.Error("刷新流程失败", ex);
                ClearFlowSafely();
            }
            finally
            {
                _refreshGate.Release();
            }
        }

        private Task RefreshCoreAsync(TemplateModel<FlowParam> template)
        {
            if (!EnsureFlowEngineAvailable()) return Task.CompletedTask;

            MqttRCService.GetInstance().QueryServices();
            foreach (CVCommonNode node in STNodeEditorMain.Nodes.OfType<CVCommonNode>())
                node.nodeRunEvent -= UpdateMsg;
            _flowNodeExecutionRecorder.DetachNodes();

            flowEngine.LoadFromBase64(template.Value.DataBase64, MqttRCService.GetInstance().ServiceTokens);

            if (!EnsureFlowEngineAvailable()) return Task.CompletedTask;
            CVCommonNode[] flowNodes = STNodeEditorMain.Nodes.OfType<CVCommonNode>().ToArray();
            foreach (CVCommonNode item in flowNodes)
            {
                item.nodeRunEvent -= UpdateMsg;
                item.nodeRunEvent += UpdateMsg;
            }
            _flowNodeExecutionRecorder.AttachNodes(flowNodes);
            return Task.CompletedTask;
        }

        private bool EnsureFlowEngineAvailable()
        {
            if (_isDisposed) return false;
            return IsFlowEngineAvailable() || RebuildFlowEngine();
        }

        private bool IsFlowEngineAvailable()
        {
            return !_isDisposed
                && flowEngine != null
                && STNodeEditorMain != null;
        }

        private bool RebuildFlowEngine()
        {
            if (_isDisposed) return false;
            if (flowControl?.IsFlowRun == true)
            {
                log.Warn("流程正在运行，跳过流程编辑器重建");
                return false;
            }

            if (flowControl != null)
                flowControl.FlowCompleted -= FlowControl_FlowCompleted;
            flowControl = null;
            try
            {
                flowEngine?.Dispose();
            }
            catch (Exception ex)
            {
                log.Warn("释放旧流程控制器失败", ex);
            }
            try
            {
                STNodeEditorMain?.Dispose();
            }
            catch (Exception ex)
            {
                log.Warn("释放旧流程编辑器失败", ex);
            }

            flowEngine = new FlowEngineControl(false);
            STNodeEditorMain = new STNodeEditor();
            STNodeEditorMain.LoadAssembly("FlowEngineLib.dll");
            flowEngine.AttachNodeEditor(STNodeEditorMain);
            return true;
        }

        private void ClearFlowSafely()
        {
            if (!IsFlowEngineAvailable()) return;

            try
            {
                flowEngine.LoadFromBase64(string.Empty);
            }
            catch (ObjectDisposedException ex)
            {
                log.Warn("流程编辑器已释放，跳过清空流程", ex);
            }
            catch (Exception ex)
            {
                log.Warn("清空流程失败", ex);
            }
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
            if (flowControl == null || !flowControl.IsFlowRun)
                return;

            if (Interlocked.CompareExchange(ref _pendingUiUpdate, 1, 0) != 0)
                return;

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

                msg = $"{FlowName} 上次执行：{LastFlowTime} ms{Environment.NewLine}正在执行节点:{Msg1}{Environment.NewLine}已经执行：{elapsedTime} {Environment.NewLine}预计还需要：{remainingTime}";
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            {
                Interlocked.Exchange(ref _pendingUiUpdate, 0);
                return;
            }

            dispatcher.BeginInvoke(() =>
            {
                try
                {
                    if (flowControl != null && flowControl.IsFlowRun)
                        logTextBox.Text = msg;
                }
                catch (Exception ex)
                {
                    log.Error("刷新流程日志失败", ex);
                }
                finally
                {
                    Interlocked.Exchange(ref _pendingUiUpdate, 0);
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
        private async void TestClick(object sender, RoutedEventArgs e)
        {
            await RunTemplate();
        }

        public async Task RunTemplate()
        {
            if (!Dispatcher.CheckAccess())
            {
                Task dispatchedTask = await Dispatcher.InvokeAsync(RunTemplate);
                await dispatchedTask;
                return;
            }

            if (_isFlowStartPending || _isFlowLifecycleActive || flowControl?.IsFlowRun == true)
            {
                log.Info("当前存在流程执行或正在处理流程结果");
                return;
            }
            if (FlowTemplate.SelectedItem is not TemplateModel<FlowParam> template)
                return;

            _isFlowStartPending = true;
            try
            {
                _currentFlowTemplateId = template.Id;
                FlowName = template.Key;
                string serialNumber = SNtextBox.Text;
                LastFlowTime = await Task.Run(
                    () => FlowNodeRecordDataBaseHelper.GetLastCompletedFlowElapsed(
                        new FlowIdentity(template.Id, template.Key, template.Key)));

                CurrentFlowResult = new KBItemMaster
                {
                    ProductionSessionId = KBProductionDataStore.Instance.EnsureCurrentSession(Summary, template.Key, DateTime.Now),
                    Model = template.Key,
                    SN = serialNumber,
                    Code = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff"),
                    FlowStatus = FlowStatus.Ready,
                };

                KBRecipeConfig currentRecipe = RecipeManager.SetCurrentTemplate(FlowName);
                CurrentFlowResult.RecipeSnapshot = KBRecipeSnapshot.Capture(FlowName, currentRecipe);
                CurrentFlowResult.IsResultPayloadLoaded = true;
                await Refresh(template);

                if (!await PreProcessingAsync(FlowName, CurrentFlowResult.SN))
                {
                    CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                    CurrentFlowResult.Msg = "PreProcessFailed";
                    logTextBox.Text = FlowName + Environment.NewLine + "预处理失败";
                    return;
                }

                flowControl ??= new FlowControl(MQTTControl.GetInstance(), flowEngine);
                flowControl.FlowCompleted -= FlowControl_FlowCompleted;
                flowControl.FlowCompleted += FlowControl_FlowCompleted;
                Interlocked.Exchange(ref _pendingUiUpdate, 0);
                stopwatch.Reset();
                stopwatch.Start();
                CreateCurrentFlowBatch();
                _isFlowLifecycleActive = true;

                if (!await flowControl.TryStartAsync(CurrentFlowResult.Code))
                {
                    flowControl.FlowCompleted -= FlowControl_FlowCompleted;
                    await HandleFlowCompletedAsync(new FlowControlData
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
            catch (Exception ex)
            {
                log.Error("运行流程失败", ex);
                flowControl?.FlowCompleted -= FlowControl_FlowCompleted;
                stopwatch.Stop();
                timer.Change(Timeout.Infinite, 500);
                if (_currentFlowBatch?.Id > 0 && CurrentFlowResult != null)
                {
                    await FinalizeCurrentFlowRunAsync(new FlowControlData
                    {
                        EventName = "Failed",
                        Status = StatusTypeEnum.Failed,
                        SerialNumber = CurrentFlowResult.Code,
                        Message = ex.Message,
                        Params = ex.Message,
                    });
                }
                logTextBox.Text = $"{FlowName}{Environment.NewLine}流程启动失败：{ex.Message}";
                _isFlowLifecycleActive = false;
            }
            finally
            {
                _isFlowStartPending = false;
            }
        }

        private MeasureBatchModel? _currentFlowBatch;
        private void CreateCurrentFlowBatch()
        {
            _currentFlowBatch = new MeasureBatchModel
            {
                TId = _currentFlowTemplateId > 0 ? _currentFlowTemplateId : null,
                Name = CurrentFlowResult.SN,
                Code = CurrentFlowResult.Code,
                CreateDate = DateTime.Now,
            };
            using var db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = MySqlControl.GetConnectionString(),
                DbType = SqlSugar.DbType.MySql,
                IsAutoCloseConnection = true,
            });
            _currentFlowBatch.Id = db.Insertable(_currentFlowBatch).ExecuteReturnIdentity();
            CurrentFlowResult.BatchId = _currentFlowBatch.Id;
            _flowNodeExecutionRecorder.StartRun(_currentFlowBatch.Id, CurrentFlowResult.Code);
        }

        private async Task<bool> PreProcessingAsync(string flowName, string serialNumber)
        {
            var serverNodes = new ObservableCollection<CVBaseServerNode>(STNodeEditorMain.Nodes.OfType<CVBaseServerNode>());
            return await PreProcessManager.GetInstance().ExecuteAsync(flowName, serialNumber, serverNodes);
        }


        private FlowControl? flowControl;
        private async void FlowControl_FlowCompleted(object? sender, FlowControlData flowControlData)
        {
            if (sender is FlowControl completedFlowControl)
                completedFlowControl.FlowCompleted -= FlowControl_FlowCompleted;
            else if (flowControl != null)
                flowControl.FlowCompleted -= FlowControl_FlowCompleted;

            try
            {
                if (!Dispatcher.CheckAccess())
                {
                    Task dispatchedTask = await Dispatcher.InvokeAsync(() => HandleFlowCompletedAsync(flowControlData));
                    await dispatchedTask;
                    return;
                }

                await HandleFlowCompletedAsync(flowControlData);
            }
            catch (Exception ex)
            {
                _isFlowLifecycleActive = false;
                log.Error("处理流程完成事件失败", ex);
            }
        }

        private async Task FinalizeCurrentFlowRunAsync(FlowControlData flowControlData)
        {
            string serialNumber = string.IsNullOrWhiteSpace(flowControlData.SerialNumber)
                ? CurrentFlowResult.Code
                : flowControlData.SerialNumber;
            flowControlData.SerialNumber = serialNumber;
            long elapsedMilliseconds = Math.Max(0, stopwatch.ElapsedMilliseconds);
            CurrentFlowResult.RunTime = elapsedMilliseconds;
            CurrentFlowResult.FlowStatus = flowControlData.FlowStatus;
            FlowNodeRecordDataBaseHelper.RecordFlowRun(
                _currentFlowTemplateId,
                FlowName,
                serialNumber,
                flowControlData.FlowStatus,
                elapsedMilliseconds);

            try
            {
                MeasureBatchModel? batch = _currentFlowBatch;
                if (batch == null && CurrentFlowResult.BatchId > 0)
                    batch = BatchResultMasterDao.Instance.GetById(CurrentFlowResult.BatchId);
                if (batch != null)
                {
                    batch.TId = _currentFlowTemplateId > 0 ? _currentFlowTemplateId : null;
                    batch.TotalTime = elapsedMilliseconds > int.MaxValue ? int.MaxValue : (int)elapsedMilliseconds;
                    batch.FlowStatus = flowControlData.FlowStatus;
                    batch.Result = flowControlData.Params ?? flowControlData.Message ?? flowControlData.EventName;
                    using var db = new SqlSugarClient(new ConnectionConfig
                    {
                        ConnectionString = MySqlControl.GetConnectionString(),
                        DbType = SqlSugar.DbType.MySql,
                        IsAutoCloseConnection = true,
                    });
                    db.Updateable(batch).ExecuteCommand();
                }
            }
            catch (Exception ex)
            {
                log.Error($"回写流程批次失败 => batchId={CurrentFlowResult.BatchId}, serialNumber={serialNumber}", ex);
            }

            try
            {
                await _flowNodeExecutionRecorder.CompleteRunAsync(
                    serialNumber,
                    flushTimeout: TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                log.Error($"结束流程节点统计失败 => batchId={CurrentFlowResult.BatchId}, serialNumber={serialNumber}", ex);
            }
            finally
            {
                _currentFlowBatch = null;
            }
        }

        private async Task HandleFlowCompletedAsync(FlowControlData flowControlData)
        {
            bool isCompleted = flowControlData.EventName == "Completed";
            bool isOverTime = flowControlData.EventName == "OverTime";
            try
            {
                stopwatch.Stop();
                timer.Change(Timeout.Infinite, 500); // 停止定时器
                Interlocked.Exchange(ref _pendingUiUpdate, 0);

                log.Info($"流程执行Elapsed Time: {stopwatch.ElapsedMilliseconds} ms");
                logTextBox.Text = FlowName + Environment.NewLine + flowControlData.EventName;
                CurrentFlowResult.Msg = flowControlData.EventName;

                ProjectKBConfig.Instance.SNlocked = false;
                SNtextBox.Focus();

                if (!isCompleted)
                {
                    string failureMessage = flowControlData.Params ?? flowControlData.Message ?? flowControlData.EventName;
                    CurrentFlowResult.Msg = failureMessage;
                    if (isOverTime)
                    {
                        log.Info("流程运行超时，正在重新尝试");
                        CurrentFlowResult.FlowStatus = FlowStatus.OverTime;
                    }
                    else
                    {
                        log.Error("流程运行失败" + flowControlData.EventName + Environment.NewLine + failureMessage);
                        CurrentFlowResult.FlowStatus = FlowStatus.Failed;

                        if (failureMessage.Contains("SDK return failed"))
                        {
                            MeasureBatchModel Batch = BatchResultMasterDao.Instance.GetByCode(flowControlData.SerialNumber);
                            if (Batch != null)
                            {
                                var values = MeasureImgResultDao.Instance.GetAllByBatchId(Batch.Id);
                                if (values.Count > 0)
                                    CurrentFlowResult.ResultImagFile = values[0].FileUrl;
                            }
                        }
                    }

                    CurrentFlowResult.RunTime = Math.Max(0, stopwatch.ElapsedMilliseconds);
                    ViewResultManager.Save(CurrentFlowResult);
                    logTextBox.Text = FlowName + Environment.NewLine + flowControlData.EventName + Environment.NewLine + failureMessage;

                    // 先让失败状态完成一次 UI 渲染，再等待节点统计写入和批次落库。
                    await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
                }

                await FinalizeCurrentFlowRunAsync(flowControlData);

                if (!isCompleted)
                    ViewResultManager.Save(CurrentFlowResult);

                if (isCompleted)
                {
                    try
                    {
                        await Dispatcher.InvokeAsync(() => Processing(flowControlData.SerialNumber));
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message);
                    }
                }
                else if (isOverTime)
                {
                    ClearFlowSafely();
                    await Refresh();
                }
            }
            finally
            {
                _isFlowLifecycleActive = false;
            }
        }

        KBItemMaster CurrentFlowResult { get; set; }

        #endregion
        private void Processing(string SerialNumber)
        {
            KBItemMaster KBItemMaster = CurrentFlowResult ?? new KBItemMaster();
            KBItemMaster.Model = CurrentFlowResult?.Model ?? FlowName;
            KBItemMaster.SN = CurrentFlowResult?.SN ?? string.Empty;
            KBRecipeConfig resultRecipe = KBItemMaster.RecipeSnapshot?.Recipe ?? RecipeConfig;
            KBItemMaster.CreateTime = DateTime.Now;
            KBItemMaster.FlowStatus = FlowStatus.Completed;

            var Batch = BatchResultMasterDao.Instance.GetByCode(SerialNumber);
            if (Batch == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到批次号，请检查流程配置", "ColorVision");
                ViewResultManager.Save(KBItemMaster);
                return;
            }
            KBItemMaster.BatchId = Batch.Id;
            foreach (var item in AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id))
            {
                if (item.ImgFileType == ViewResultAlgType.KB || item.ImgFileType == ViewResultAlgType.KB_Raw)
                {
                    KBItemMaster.KBTemplate = item.TName;

                    using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

                    var mod = Db.Queryable<ModMasterModel>().Where(x => x.Name == item.TName && x.Pid == 150).First();
                    if (mod == null)
                    {
                        log.Warn($"item.TName{item.TName},Cant find template");
                        continue;
                    }

                    KBJson kBJson = JsonConvert.DeserializeObject<KBJson>(mod.JsonVal);
                    log.Debug(JsonConvert.SerializeObject(kBJson));
                    if (kBJson != null)
                    {
                        foreach (var keyRect in kBJson.KBKeyRects)
                        {
                            KBItem kItem = new KBItem();
                            kItem.Name = keyRect.Name;
                            kItem.KBKeyRect = keyRect;
                            KBItemMaster.Items.Add(kItem);

                        }
                        KBItemMaster.ResultImagFile = item.ResultImagFile;

                    }
                }
                if (item.ImgFileType == ViewResultAlgType.POI_Y)
                {
                    var pois = PoiPointResultDao.Instance.GetAllByPid(item.Id);
                    if (pois != null)
                    {
                        foreach (var poi in pois)
                        {
                            var list = JsonConvert.DeserializeObject<KBvalue>(poi.Value);
                            var key = KBItemMaster.Items.First(a => a.Name == poi.PoiName && poi.PoiWidth == a.KBKeyRect.Width);
                            if (key != null)
                            {
                                key.Lv = list.Y;
                                key.Lv = list.Y * list.PixNumber;
                                if (key.KBKeyRect.KBKey.Area != 0)
                                {
                                    key.Lv = key.Lv / key.KBKeyRect.KBKey.Area;
                                }
                                key.Lv = key.KBKeyRect.KBKey.KeyScale * key.Lv * ProjectKBConfig.Instance.KBLVSacle;

                            }
                        }
                    }
                }
                if (item.ImgFileType == ViewResultAlgType.POI_Y_V2)
                {
                    var pois = PoiPointResultDao.Instance.GetAllByPid(item.Id);
                    if (pois != null)
                    {
                        foreach (var poi in pois)
                        {
                            log.Debug(poi.Value);
                            var list = JsonConvert.DeserializeObject<ObservableCollection<KBvalue>>(poi.Value);

                            var key = KBItemMaster.Items.First(a => a.Name == poi.PoiName && poi.PoiWidth == a.KBKeyRect.Width);
                            if (key != null)
                            {
                                if (list != null && list.Count == 2)
                                {
                                    key.Lv = list[0].Y;
                                    key.Lv = key.Lv * list[0].PixNumber;
                                    if (key.KBKeyRect.KBKey.Area != 0)
                                    {
                                        key.Lv = key.Lv / key.KBKeyRect.KBKey.Area;
                                    }
                                    key.Lv = key.KBKeyRect.KBKey.KeyScale * key.Lv * ProjectKBConfig.Instance.KBLVSacle;
                                    if (key.Lv == 0)
                                    {
                                        key.Lc = 0;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (KBItemMaster.Items.Count == 0)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到对映的按键，请检查流程配置是否计算KB模板", "ColorVision");
                ViewResultManager.Save(KBItemMaster);
                return;
            }

            double keyLcNeighborhoodRadiusMm = resultRecipe.KeyLcNeighborhoodRadiusMm;
            double keyLcPixelsPerMillimeter = resultRecipe.KeyLcPixelsPerMillimeter;
            double keyLcNeighborhoodRadiusPixels = GetLcNeighborhoodRadiusPixels(keyLcNeighborhoodRadiusMm, keyLcPixelsPerMillimeter);
            KBItemMaster.KeyLcNeighborhoodRadiusMm = keyLcNeighborhoodRadiusMm;
            KBItemMaster.KeyLcPixelsPerMillimeter = keyLcPixelsPerMillimeter;
            KBItemMaster.KeyLcNeighborhoodVersion = CenterDistanceLcNeighborhoodVersion;
            CalCulLc(KBItemMaster.Items, keyLcNeighborhoodRadiusPixels);

            foreach (var item in KBItemMaster.Items)
            {
                if (resultRecipe.EnableKeyLvLimit)
                {
                    item.Result = item.Result && item.Lv >= resultRecipe.MinKeyLv;
                    item.Result = item.Result && item.Lv <= resultRecipe.MaxKeyLv;
                }

                if (resultRecipe.EnableKeyLcLimit)
                {
                    item.Result = item.Result && item.Lc >= resultRecipe.MinKeyLc / 100;
                    item.Result = item.Result && item.Lc <= resultRecipe.MaxKeyLc / 100;
                }
            }


            var maxKeyItem = KBItemMaster.Items.OrderByDescending(item => item.Lv).FirstOrDefault();
            var minLKey = KBItemMaster.Items.OrderBy(item => item.Lv).FirstOrDefault();
            KBItemMaster.MaxLv = maxKeyItem.Lv;
            KBItemMaster.BrightestKey = maxKeyItem.Name;
            KBItemMaster.MinLv = minLKey.Lv;
            KBItemMaster.DrakestKey = minLKey.Name;
            KBItemMaster.AvgLv = KBItemMaster.Items.Any() ? KBItemMaster.Items.Average(item => item.Lv) : 0;

            KBItemMaster.LvUniformity = KBItemMaster.MaxLv == 0 ? 0 : KBItemMaster.MinLv / KBItemMaster.MaxLv;
            BacklightAutotuneService.Apply(KBItemMaster, resultRecipe);
            KBItemMaster.SN = SNtextBox.Text;


            CalCulLc(KBItemMaster.Items, keyLcNeighborhoodRadiusPixels);

            KBItemMaster.Result = true;

            if (resultRecipe.EnableKeyLvLimit)
            {
                KBItemMaster.Result = KBItemMaster.Result && BacklightAutotuneService.GetOriginalMinLv(KBItemMaster) >= resultRecipe.MinKeyLv;
                KBItemMaster.Result = KBItemMaster.Result && KBItemMaster.MaxLv <= resultRecipe.MaxKeyLv;
            }

            if (resultRecipe.EnableAvgLvLimit)
            {
                double originalAvgLv = BacklightAutotuneService.GetOriginalAvgLv(KBItemMaster);
                KBItemMaster.Result = KBItemMaster.Result && originalAvgLv >= resultRecipe.MinAvgLv;
                KBItemMaster.Result = KBItemMaster.Result && originalAvgLv <= resultRecipe.MaxAvgLv;
            }

            if (resultRecipe.EnableUniformityLimit)
            {
                KBItemMaster.Result = KBItemMaster.Result && BacklightAutotuneService.GetOriginalLvUniformity(KBItemMaster) >= resultRecipe.MinUniformity / 100;
            }

            if (resultRecipe.EnableKeyLcLimit)
            {
                KBItemMaster.Result = KBItemMaster.Result && KBItemMaster.Items.Min(item => item.Lc) >= resultRecipe.MinKeyLc / 100;
                KBItemMaster.Result = KBItemMaster.Result && KBItemMaster.Items.Max(item => item.Lc) <= resultRecipe.MaxKeyLc / 100;
            }

            KBItemMaster.NbrFailPoints = KBItemMaster.Items.Count(item => !item.Result);

            KBItemMaster.Exposure = "50";

            ViewResultManager.Save(KBItemMaster);

            if (ViewResultManager.Config.SaveText)
            {
                string resultPath = Path.Combine(ViewResultManager.Config.TextSavePath, $"{KBItemMaster.SN}-{KBItemMaster.CreateTime:yyyyMMddHHmmssffff}.txt");
                string result = $"{KBItemMaster.SN},{(KBItemMaster.Result ? "Pass" : "Fail")}, ,";
                log.Info($"结果正在写入{resultPath},result:{result}");
                File.WriteAllText(resultPath, result);
            }


            if (ViewResultManager.Config.SaveSummary)
            {
                try
                {
                    string invalidChars2 = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
                    string regexPattern2 = $"[{Regex.Escape(invalidChars2)}]";
                    string safeModel = Regex.Replace(KBItemMaster.Model ?? string.Empty, regexPattern2, "");
                    string summaryDir = Path.Combine(ViewResultManager.Config.SummarySavePath, safeModel);
                    Directory.CreateDirectory(summaryDir);
                    string summaryPath = Path.Combine(summaryDir, $"{KBItemMaster.SN}-{KBItemMaster.CreateTime:yyyyMMddHHmmssffff}.txt");
                    string summaryText = BuildSummaryText(KBItemMaster);
                    log.Info($"Summary 正在写入 {summaryPath}");
                    File.WriteAllText(summaryPath, summaryText);
                }
                catch (Exception ex)
                {
                    log.Error("写入 Summary 失败", ex);
                }
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
                string regexPattern = $"[{Regex.Escape(invalidChars)}]";

                string csvpath = ViewResultManager.Config.CsvSavePath + $"\\{Regex.Replace(KBItemMaster.Model, regexPattern, "")}_{KBItemMaster.CreateTime:yyyyMMdd}.csv";

                KBItemMaster.SaveCsv(csvpath, ViewResultManager.Config.AppendFalloutSummary);
                log.Info($"writecsv:{csvpath}");
            });
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                log.Debug("流程执行结束，设置寄存器为0，触发移动");
                ModbusControl.GetInstance().SetRegisterValue(0);
            });

            ///回传MEs 确保Mes配置
            log.Info($"UseMes{Summary.UseMes} IsCheckWIP{IsCheckWIP}");
            if (Summary.UseMes && IsCheckWIP)
            {
                try
                {
                    string Barcode_Result = KBItemMaster.Result ? "PASS" : "NG";
                    log.Info($"Collect_test{Summary.Stage},Barcode_NO:{ProjectKBConfig.Instance.SN}Barcode_Result：{Barcode_Result}MachineNO:{Summary.MachineNO}");
                    IntPtr a = MesDll.Collect_test(Summary.Stage, ProjectKBConfig.Instance.SN, Barcode_Result, Summary.MachineNO, Summary.LineNO, Summary.Opno, Barcode_Result, string.Empty);
                    var Collect_test = MesDll.PtrToString(a);
                    logTextBox.Text += Collect_test;
                    log.Info("Collect_test result" + Collect_test);
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }

            }
            IsCheckWIP = false;
            SNtextBox.Text = string.Empty;
            SNtextBox.Focus();
        }

        public static bool IsPointInCircle(double px, double py, double centerX, double centerY, double r)
        {
            return Math.Pow(px - centerX, 2) + Math.Pow(py - centerY, 2) <= Math.Pow(r, 2);
        }

        public static bool IsRectInCircle(KBItem item, double centerX, double centerY, double r)
        {
            Rect rect = new Rect(item.KBKeyRect.X, item.KBKeyRect.Y, item.KBKeyRect.Width, item.KBKeyRect.Height);
            var corners = new[]
{
            (rect.X, rect.Y),
            (rect.X + rect.Width, rect.Y),
            (rect.X, rect.Y + rect.Height),
            (rect.X + rect.Width, rect.Y + rect.Height)
        };
            foreach (var corner in corners)
            {
                if (!IsPointInCircle(corner.Item1, corner.Item2, centerX, centerY, r))
                {
                    return false;
                }
            }
            return true;
        }

        public static double GetLcNeighborhoodRadiusPixels(double neighborhoodRadiusMm, double pixelsPerMillimeter)
        {
            if (!double.IsFinite(neighborhoodRadiusMm) || neighborhoodRadiusMm <= 0)
                throw new ArgumentOutOfRangeException(nameof(neighborhoodRadiusMm), "局部对比度邻域半径必须是大于0的有限值。");
            if (!double.IsFinite(pixelsPerMillimeter) || pixelsPerMillimeter <= 0)
                throw new ArgumentOutOfRangeException(nameof(pixelsPerMillimeter), "图像标定必须是大于0的有限值。");

            double radiusPixels = neighborhoodRadiusMm * pixelsPerMillimeter;
            if (!double.IsFinite(radiusPixels) || radiusPixels <= 0)
                throw new ArgumentOutOfRangeException(nameof(pixelsPerMillimeter), "局部对比度邻域换算后的像素半径必须是大于0的有限值。");

            return radiusPixels;
        }

        public static IReadOnlyList<KBItem> GetLcNeighbors(IEnumerable<KBItem> kBItems, KBItem item, double neighborhoodRadiusPixels)
        {
            ArgumentNullException.ThrowIfNull(kBItems);
            ArgumentNullException.ThrowIfNull(item);
            if (!double.IsFinite(neighborhoodRadiusPixels) || neighborhoodRadiusPixels <= 0)
                throw new ArgumentOutOfRangeException(nameof(neighborhoodRadiusPixels), "局部对比度邻域像素半径必须是大于0的有限值。");

            double centerX = item.KBKeyRect.X + item.KBKeyRect.Width / 2d;
            double centerY = item.KBKeyRect.Y + item.KBKeyRect.Height / 2d;

            return kBItems
                .Where(candidate =>
                {
                    if (ReferenceEquals(candidate, item)) return false;
                    double candidateCenterX = candidate.KBKeyRect.X + candidate.KBKeyRect.Width / 2d;
                    double candidateCenterY = candidate.KBKeyRect.Y + candidate.KBKeyRect.Height / 2d;
                    return IsPointInCircle(candidateCenterX, candidateCenterY, centerX, centerY, neighborhoodRadiusPixels);
                })
                .ToList();
        }

        public static void CalCulLc(IEnumerable<KBItem> kBItems, double neighborhoodRadiusPixels)
        {
            ArgumentNullException.ThrowIfNull(kBItems);
            if (!double.IsFinite(neighborhoodRadiusPixels) || neighborhoodRadiusPixels <= 0)
                throw new ArgumentOutOfRangeException(nameof(neighborhoodRadiusPixels), "局部对比度邻域像素半径必须是大于0的有限值。");

            List<KBItem> items = kBItems.ToList();
            if (items.Count == 0) return;

            foreach (var item in items)
            {
                IReadOnlyList<KBItem> round = GetLcNeighbors(items, item, neighborhoodRadiusPixels);
                List<string> strings = round.Select(keys => keys.Name).ToList();
                log.Debug($"Round Key {item.Name}: {string.Join(",", strings)}");

                double averagelv = round.Count > 0 ? round.Average(item => item.Lv) : 0;
                log.Debug($"Round Key {item.Name}: averagelv{averagelv}");
                if (averagelv == 0)
                {
                    item.Lc = 0;
                }
                else
                {
                    item.Lc = (item.Lv - averagelv) / averagelv;
                }
            }
        }

        public static string BuildSummaryText(KBItemMaster kmitemmaster)
        {
            var sb = new StringBuilder();
            string modelName = string.IsNullOrWhiteSpace(kmitemmaster.Model) ? "KB" : kmitemmaster.Model;
            sb.AppendLine($"型号: {modelName}");
            sb.AppendLine($"系列号: {kmitemmaster.SN}");
            sb.AppendLine($"测量设置: {GetSummaryMeasurementSetting(kmitemmaster)}");
            sb.AppendLine($"LC邻域: {GetLcNeighborhoodDescription(kmitemmaster)}");
            sb.AppendLine($"关注点: {kmitemmaster.KBTemplate}");
            sb.AppendLine($"{kmitemmaster.CreateTime:yyyy/M/d HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("PT\tLv\tLC");

            foreach (var item in kmitemmaster.Items)
            {
                string key = $"[{item.Name}]";
                sb.AppendLine($"{key}\t{item.Lv:F3}\t{item.Lc * 100:F2}%");
            }

            sb.AppendLine();
            sb.AppendLine($"最小亮度= {kmitemmaster.MinLv:F3} cd/m²");
            sb.AppendLine($"最大亮度= {kmitemmaster.MaxLv:F3} cd/m²");
            sb.AppendLine($"最暗的键= [{kmitemmaster.DrakestKey}]");
            sb.AppendLine($"最亮的键= [{kmitemmaster.BrightestKey}]");
            sb.AppendLine();
            sb.AppendLine("合格/不合格标准:");
            sb.AppendLine($"不合格点数= {kmitemmaster.NbrFailPoints}");
            sb.AppendLine($"平均亮度= {kmitemmaster.AvgLv:F3} cd/m²");
            sb.AppendLine($"亮度一致性= {kmitemmaster.LvUniformity * 100:F3}%");
            sb.AppendLine(kmitemmaster.Result ? "PASS" : "FAIL");
            return sb.ToString();
        }

        private static string GetSummaryMeasurementSetting(KBItemMaster kmitemmaster)
        {
            if (!string.IsNullOrWhiteSpace(kmitemmaster.Exposure))
            {
                return kmitemmaster.Exposure;
            }

            if (!string.IsNullOrWhiteSpace(kmitemmaster.MesSpecGroup))
            {
                return kmitemmaster.MesSpecGroup;
            }

            if (!string.IsNullOrWhiteSpace(kmitemmaster.MesModel))
            {
                return kmitemmaster.MesModel;
            }

            return string.Empty;
        }

        private static void AppendBacklightAutotuneSummary(StringBuilder sb, KBItemMaster kmitemmaster)
        {
            if (!kmitemmaster.BacklightAutotuneEnabled)
            {
                return;
            }

            sb.AppendLine();
            sb.AppendLine($"Backlight Autotune= {kmitemmaster.BacklightAutotuneSource} {(kmitemmaster.BacklightAutotuneApplied ? "Applied" : "Not Applied")}");
            sb.AppendLine($"Autotune Steepness= {kmitemmaster.BacklightAutotuneSteepness:F2}");
            sb.AppendLine($"Avg Lv Raw/Adjusted/Q1/Q3= {kmitemmaster.AvgLvRaw:F2}/{kmitemmaster.AvgLvAdjusted:F2}/{kmitemmaster.AvgLvQ1:F2}/{kmitemmaster.AvgLvQ3:F2}");
            sb.AppendLine($"Min Lv Raw/Adjusted/Q1/Q3= {kmitemmaster.MinLvRaw:F2}/{kmitemmaster.MinLvAdjusted:F2}/{kmitemmaster.MinLvQ1:F2}/{kmitemmaster.MinLvQ3:F2}");
            sb.AppendLine($"Lv Uniformity Raw/Adjusted/Q1/Q3= {kmitemmaster.LvUniformityRaw * 100:F2}%/{kmitemmaster.LvUniformityAdjusted * 100:F2}%/{kmitemmaster.UniformityQ1:F2}%/{kmitemmaster.UniformityQ3:F2}%");
        }

        public void GenoutputText(KBItemMaster kmitemmaster)
        {
            outputText.Background = kmitemmaster.Result ? Brushes.Lime : Brushes.Red;
            outputText.Document.Blocks.Clear(); // 清除之前的内容

            KBRecipeConfig? recipe = GetRecipeConfig(kmitemmaster);
            Brush normalTextBrush = kmitemmaster.Result ? Brushes.Black : Brushes.White;

            string outtext = string.Empty;
            outtext += $"机种 (Model):{kmitemmaster.Model}" + Environment.NewLine;
            outtext += $"SN:{kmitemmaster.SN}" + Environment.NewLine;
            outtext += GetRecipeSnapshotDescription(kmitemmaster) + Environment.NewLine;
            outtext += $"LC邻域 (LC Neighborhood): {GetLcNeighborhoodDescription(kmitemmaster)}" + Environment.NewLine;
            outtext += $"按键明细 (Points of Interest): " + Environment.NewLine;
            outtext += $"{kmitemmaster.CreateTime:yyyy/MM/dd HH:mm:ss}" + Environment.NewLine;

            Run run = new Run(outtext);
            run.Foreground = normalTextBrush;
            run.FontSize += 1;

            var paragraph = new Paragraph();
            paragraph.Inlines.Add(run);

            outputText.Document.Blocks.Add(paragraph);
            outtext = string.Empty;

            paragraph = new Paragraph();

            AppendOutputLine(paragraph, $"{"按键 (PT)",-20} {"亮度 (Lv)",-12} {"局部对比度 (LC)",12}", normalTextBrush);

            foreach (var item in kmitemmaster.Items)
            {
                string formattedString = $"[{item.Name}]";
                bool isFailureLine = IsKeyFailure(item, recipe) || !item.Result;
                string resultText = isFailureLine ? "Fail" : string.Empty;

                string line = $"{formattedString,-20} {item.Lv,-12:F2} {item.Lc * 100,12:F2}%  {resultText}";
                AppendOutputLine(paragraph, line, normalTextBrush, isFailureLine);
            }
            outputText.Document.Blocks.Add(paragraph);

            bool minLvFailure = recipe?.EnableKeyLvLimit == true && BacklightAutotuneService.GetOriginalMinLv(kmitemmaster) < recipe.MinKeyLv;
            bool maxLvFailure = recipe?.EnableKeyLvLimit == true && kmitemmaster.MaxLv > recipe.MaxKeyLv;
            Table summaryTable = CreateMetricTable(250, 16, 125, 45);
            TableRowGroup summaryRows = new();
            summaryTable.RowGroups.Add(summaryRows);
            AppendMetricRow(summaryRows, "最小亮度", "Min Lv", $"{kmitemmaster.MinLv:F2} cd/m2", minLvFailure, normalTextBrush);
            AppendMetricRow(summaryRows, "最大亮度", "Max Lv", $"{kmitemmaster.MaxLv:F2} cd/m2", maxLvFailure, normalTextBrush);
            AppendMetricRow(summaryRows, "最暗按键", "Darkest Key", $"[{kmitemmaster.DrakestKey}]", false, normalTextBrush);
            AppendMetricRow(summaryRows, "最亮按键", "Brightest Key", $"[{kmitemmaster.BrightestKey}]", false, normalTextBrush);
            outputText.Document.Blocks.Add(summaryTable);

            paragraph = new Paragraph();
            AppendOutputLine(paragraph, string.Empty, normalTextBrush);
            AppendOutputLine(paragraph, "合格/不合格标准 (Pass/Fail Criteria):", normalTextBrush);
            outputText.Document.Blocks.Add(paragraph);

            Table criteriaTable = CreateCriteriaMetricTable(285, 16, 120, 90, 45);
            TableRowGroup criteriaRows = new();
            criteriaTable.RowGroups.Add(criteriaRows);
            AppendCriteriaMetricRow(criteriaRows, "不合格点数", "Nbr Failed Points", kmitemmaster.NbrFailPoints.ToString(), string.Empty, kmitemmaster.NbrFailPoints > 0, normalTextBrush);
            double originalAvgLv = BacklightAutotuneService.GetOriginalAvgLv(kmitemmaster);
            bool avgLvFailure = recipe?.EnableAvgLvLimit == true && (originalAvgLv < recipe.MinAvgLv || originalAvgLv > recipe.MaxAvgLv);
            AppendCriteriaMetricRow(criteriaRows, "平均亮度", "Avg Lv", $"{kmitemmaster.AvgLv:F2} cd/m2", string.Empty, avgLvFailure, normalTextBrush);
            bool uniformityFailure = recipe?.EnableUniformityLimit == true && BacklightAutotuneService.GetOriginalLvUniformity(kmitemmaster) < recipe.MinUniformity / 100;
            AppendCriteriaMetricRow(criteriaRows, "亮度均匀性", "Lv Uniformity", $"{kmitemmaster.LvUniformity * 100:F2}%", string.Empty, uniformityFailure, normalTextBrush);
            AppendLocalContrastSummary(criteriaRows, kmitemmaster, recipe, normalTextBrush);
            outputText.Document.Blocks.Add(criteriaTable);

            AppendBacklightAutotuneOutput(kmitemmaster, normalTextBrush);
            SNtextBox.Focus();
        }

        private void AppendBacklightAutotuneOutput(KBItemMaster kmitemmaster, Brush normalTextBrush)
        {
            if (!kmitemmaster.BacklightAutotuneEnabled)
            {
                return;
            }

            Paragraph paragraph = new();
            AppendOutputLine(paragraph, string.Empty, normalTextBrush);
            AppendOutputLine(paragraph, $"背光自动修正 (Backlight Autotune): {kmitemmaster.BacklightAutotuneSource}, {(kmitemmaster.BacklightAutotuneApplied ? "Applied" : "Not Applied")}, Steepness={kmitemmaster.BacklightAutotuneSteepness:F2}", normalTextBrush);
            AppendOutputLine(paragraph, $"Avg Lv Raw/Adjusted/Q1/Q3 = {kmitemmaster.AvgLvRaw:F2}/{kmitemmaster.AvgLvAdjusted:F2}/{kmitemmaster.AvgLvQ1:F2}/{kmitemmaster.AvgLvQ3:F2}", normalTextBrush);
            AppendOutputLine(paragraph, $"Min Lv Raw/Adjusted/Q1/Q3 = {kmitemmaster.MinLvRaw:F2}/{kmitemmaster.MinLvAdjusted:F2}/{kmitemmaster.MinLvQ1:F2}/{kmitemmaster.MinLvQ3:F2}", normalTextBrush);
            AppendOutputLine(paragraph, $"Uniformity Raw/Adjusted/Q1/Q3 = {kmitemmaster.LvUniformityRaw * 100:F2}%/{kmitemmaster.LvUniformityAdjusted * 100:F2}%/{kmitemmaster.UniformityQ1:F2}%/{kmitemmaster.UniformityQ3:F2}%", normalTextBrush);
            outputText.Document.Blocks.Add(paragraph);
        }

        private static KBRecipeConfig? GetRecipeConfig(KBItemMaster kmitemmaster)
        {
            return kmitemmaster.RecipeSnapshot?.Recipe;
        }

        private static string GetRecipeSnapshotDescription(KBItemMaster item)
        {
            KBRecipeSnapshot? snapshot = item.RecipeSnapshot;
            if (snapshot == null)
                return "Recipe快照 (Recipe Snapshot): 未记录，仅按当时保存的结果显示";

            string name = string.IsNullOrWhiteSpace(snapshot.RecipeName) ? "未命名" : snapshot.RecipeName;
            return snapshot.Origin == KBRecipeSnapshotOrigin.RebuiltFromCurrentRecipe
                ? $"Recipe快照 (Recipe Snapshot): {name}（由当前关联Recipe重建）"
                : $"Recipe快照 (Recipe Snapshot): {name}（运行时记录）";
        }

        private static void AppendOutputLine(Paragraph paragraph, string line, Brush normalTextBrush, bool highlightFailure = false)
        {
            const string failText = "Fail";

            if (highlightFailure && line.EndsWith(failText, StringComparison.Ordinal))
            {
                string prefix = line[..^failText.Length];
                Run normalRun = new Run(prefix)
                {
                    Foreground = normalTextBrush
                };
                normalRun.FontSize += 1;
                paragraph.Inlines.Add(normalRun);

                Run failRun = new Run(failText + Environment.NewLine)
                {
                    Foreground = Brushes.Yellow,
                    FontWeight = FontWeights.Bold
                };
                failRun.FontSize += 1;
                paragraph.Inlines.Add(failRun);
                return;
            }

            Run run = new Run(line + Environment.NewLine)
            {
                Foreground = normalTextBrush
            };
            run.FontSize += 1;
            paragraph.Inlines.Add(run);
        }

        private void OutputText_Copy(object sender, ExecutedRoutedEventArgs e)
        {
            string selectedText = outputText.Selection.Text;
            if (string.IsNullOrEmpty(selectedText)) return;

            ColorVision.Common.Clipboard.SetText(FormatOutputTextForClipboard(selectedText));
            e.Handled = true;
        }

        public static string FormatOutputTextForClipboard(string selectedText)
        {
            ArgumentNullException.ThrowIfNull(selectedText);
            if (selectedText.Length == 0) return string.Empty;

            string[] lines = selectedText.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (OutputDetailHeaderRegex.IsMatch(lines[i]))
                {
                    lines[i] = "按键 (PT)\t亮度 (Lv)\t局部对比度 (LC)\t结果 (Result)";
                    continue;
                }

                Match detailRow = OutputDetailRowRegex.Match(lines[i]);
                if (!detailRow.Success) continue;

                lines[i] = $"{detailRow.Groups["key"].Value}\t{detailRow.Groups["lv"].Value}\t{detailRow.Groups["lc"].Value}\t{detailRow.Groups["result"].Value}";
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static bool IsKeyFailure(KBItem item, KBRecipeConfig? recipe)
        {
            if (recipe == null)
                return false;

            if (recipe.EnableKeyLvLimit)
            {
                if (item.Lv < recipe.MinKeyLv || item.Lv > recipe.MaxKeyLv)
                {
                    return true;
                }
            }

            if (recipe.EnableKeyLcLimit)
            {
                double lcPercent = item.Lc * 100;
                if (lcPercent < recipe.MinKeyLc || lcPercent > recipe.MaxKeyLc)
                {
                    return true;
                }
            }

            return false;
        }

        private static Table CreateMetricTable(double labelWidth, double equalWidth, double valueWidth, double failWidth)
        {
            Table table = new()
            {
                CellSpacing = 0,
                Margin = new Thickness(0)
            };
            table.Columns.Add(new TableColumn { Width = new GridLength(labelWidth) });
            table.Columns.Add(new TableColumn { Width = new GridLength(equalWidth) });
            table.Columns.Add(new TableColumn { Width = new GridLength(valueWidth) });
            table.Columns.Add(new TableColumn { Width = new GridLength(failWidth) });
            return table;
        }

        private static Table CreateCriteriaMetricTable(double labelWidth, double equalWidth, double valueWidth, double pointWidth, double failWidth)
        {
            Table table = new()
            {
                CellSpacing = 0,
                Margin = new Thickness(0)
            };
            table.Columns.Add(new TableColumn { Width = new GridLength(labelWidth) });
            table.Columns.Add(new TableColumn { Width = new GridLength(equalWidth) });
            table.Columns.Add(new TableColumn { Width = new GridLength(valueWidth) });
            table.Columns.Add(new TableColumn { Width = new GridLength(pointWidth) });
            table.Columns.Add(new TableColumn { Width = new GridLength(failWidth) });
            return table;
        }

        private static void AppendMetricRow(TableRowGroup rowGroup, string chineseLabel, string englishLabel, string value, bool failed, Brush normalTextBrush)
        {
            TableRow row = new();
            row.Cells.Add(CreateMetricCell($"{ExpandChineseLabel(chineseLabel)} ({englishLabel})", normalTextBrush));
            row.Cells.Add(CreateMetricCell("=", normalTextBrush));
            row.Cells.Add(CreateMetricCell(value, normalTextBrush));
            row.Cells.Add(CreateMetricCell(failed ? "Fail" : string.Empty, failed ? Brushes.Yellow : normalTextBrush, failed));
            rowGroup.Rows.Add(row);
        }

        private static void AppendCriteriaMetricRow(TableRowGroup rowGroup, string chineseLabel, string englishLabel, string value, string point, bool failed, Brush normalTextBrush)
        {
            TableRow row = new();
            row.Cells.Add(CreateMetricCell($"{ExpandChineseLabel(chineseLabel)} ({englishLabel})", normalTextBrush));
            row.Cells.Add(CreateMetricCell("=", normalTextBrush));
            row.Cells.Add(CreateMetricCell(value, normalTextBrush));
            row.Cells.Add(CreateMetricCell(point, normalTextBrush));
            row.Cells.Add(CreateMetricCell(failed ? "Fail" : string.Empty, failed ? Brushes.Yellow : normalTextBrush, failed));
            rowGroup.Rows.Add(row);
        }

        private static TableCell CreateMetricCell(string text, Brush foreground, bool bold = false)
        {
            Run run = new(text)
            {
                Foreground = foreground,
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal
            };
            run.FontSize += 1;

            Paragraph paragraph = new(run)
            {
                Margin = new Thickness(0),
                Padding = new Thickness(0)
            };

            return new TableCell(paragraph)
            {
                Padding = new Thickness(0)
            };
        }

        private static string ExpandChineseLabel(string text)
        {
            return string.Join(" ", text.Select(c => c.ToString()));
        }

        private static void AppendLocalContrastSummary(TableRowGroup rowGroup, KBItemMaster kmitemmaster, KBRecipeConfig? recipe, Brush normalTextBrush)
        {
            if (!kmitemmaster.Items.Any())
            {
                return;
            }

            KBItem minLcItem = kmitemmaster.Items.OrderBy(item => item.Lc).First();
            KBItem maxLcItem = kmitemmaster.Items.OrderByDescending(item => item.Lc).First();
            double minLcPercent = minLcItem.Lc * 100;
            double maxLcPercent = maxLcItem.Lc * 100;
            bool minLcFailure = recipe?.EnableKeyLcLimit == true && minLcPercent < recipe.MinKeyLc;
            bool maxLcFailure = recipe?.EnableKeyLcLimit == true && maxLcPercent > recipe.MaxKeyLc;

            AppendCriteriaMetricRow(rowGroup, "最小局部对比度", "Min LC", $"{minLcPercent:F2}%", $"[{minLcItem.Name}]", minLcFailure, normalTextBrush);
            AppendCriteriaMetricRow(rowGroup, "最大局部对比度", "Max LC", $"{maxLcPercent:F2}%", $"[{maxLcItem.Name}]", maxLcFailure, normalTextBrush);
        }


        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            ViewResultManager.Config.Height = row2.ActualHeight;
            row2.Height = GridLength.Auto;
        }

        private void Button_Click_Clear(object sender, RoutedEventArgs e)
        {
            if (!AuthManager.RequireAdmin(this)) return;

            Interlocked.Increment(ref _resultImageRequestId);
            ClearKeyOverlayState();
            ViewResluts.Clear();
            ImageView.Clear();
            outputText.Document.Blocks.Clear();
            outputText.SetResourceReference(Control.BackgroundProperty, "RegionBrush");
        }

        private void listView1_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (sender is not ListView listView) return;

            int requestId = Interlocked.Increment(ref _resultImageRequestId);
            ClearKeyOverlayState();
            ImageView.Clear();
            if (listView.SelectedIndex > -1)
            {
                var kBItem = ViewResluts[listView.SelectedIndex];
                try
                {
                    ViewResultManager.LoadResultPayload(kBItem);
                }
                catch (Exception ex)
                {
                    log.Error($"读取 KB 历史结果失败，Id={kBItem.Id}", ex);
                    MessageBox.Show(this, $"结果明细读取失败：{ex.Message}", "ProjectKB");
                    return;
                }
                GenoutputText(kBItem);

                _ = Task.Run(async () =>
                {
                    if (File.Exists(kBItem.ResultImagFile))
                    {
                        bool imageReady = false;
                        try
                        {
                            var fileInfo = new FileInfo(kBItem.ResultImagFile);
                            using (var fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                            {
                            }
                            imageReady = fileInfo.Length > 0;
                        }
                        catch
                        {
                            log.Warn("文件还在写入");
                            await Task.Delay(ViewResultManager.Config.ViewImageReadDelay);
                            try
                            {
                                imageReady = File.Exists(kBItem.ResultImagFile) && new FileInfo(kBItem.ResultImagFile).Length > 0;
                            }
                            catch
                            {
                                imageReady = false;
                            }
                        }

                        if (!imageReady) return;
                        WriteableBitmap? resultBitmap = TryLoadResultBitmap(kBItem.ResultImagFile);
                        if (resultBitmap == null) return;

                        _ = Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            if (requestId != _resultImageRequestId) return;
                            ImageView.Config.FilePath = kBItem.ResultImagFile;
                            ImageView.OpenImage(resultBitmap);
                            ImageView.UpdateZoomAndScale();
                            RenderKeyOverlays(kBItem);
                        });
                    }
                });
            }
        }

        private static WriteableBitmap? TryLoadResultBitmap(string filePath)
        {
            try
            {
                using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                if (decoder.Frames.Count == 0) return null;

                WriteableBitmap bitmap = new(decoder.Frames[0]);
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                log.Warn($"结果图像加载失败: {filePath}", ex);
                return null;
            }
        }

        private void RenderKeyOverlays(KBItemMaster result)
        {
            ImageView.ImageShow.Clear();
            ClearKeyOverlayState();
            _displayedKeyResult = result;

            KBItem? brightestKey = result.Items.Where(item => item.Result).OrderByDescending(item => item.Lv).FirstOrDefault();
            KBItem? darkestKey = result.Items.Where(item => item.Result).OrderBy(item => item.Lv).FirstOrDefault();

            foreach (KBItem item in result.Items)
            {
                RectangleProperties rectangleProperties = new()
                {
                    Rect = new Rect(item.KBKeyRect.X, item.KBKeyRect.Y, item.KBKeyRect.Width, item.KBKeyRect.Height),
                    Pen = CreateDefaultKeyPen(item, darkestKey, brightestKey),
                    Brush = Brushes.Transparent,
                    Name = item.Name,
                    Id = -1
                };

                DVRectangle rectangle = new(rectangleProperties) { Tag = item };
                rectangle.Render();
                ImageView.ImageShow.AddOverlayVisual(rectangle);
                _keyVisuals[item] = rectangle;
            }
        }

        internal static Pen CreateDefaultKeyPen(KBItem item, KBItem? darkestKey, KBItem? brightestKey)
        {
            if (!item.Result) return new Pen(Brushes.Red, 10);
            if (ReferenceEquals(item, darkestKey)) return new Pen(Brushes.Violet, 10);
            if (ReferenceEquals(item, brightestKey)) return new Pen(Brushes.White, 10);
            return new Pen(Brushes.Gray, 5);
        }

        internal static Pen CreateSelectedKeyPen() => new(Brushes.Lime, 12);

        private void ImageCanvas_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_displayedKeyResult == null) return;

            Point point = e.GetPosition(ImageView.EditorContext.DrawEditorContext.DrawCanvas);
            KBItem? selectedKey = _displayedKeyResult.Items
                .Where(item => new Rect(item.KBKeyRect.X, item.KBKeyRect.Y, item.KBKeyRect.Width, item.KBKeyRect.Height).Contains(point))
                .OrderBy(item => item.KBKeyRect.Width * item.KBKeyRect.Height)
                .FirstOrDefault();

            if (selectedKey == null)
            {
                ClearLcNeighborhoodSelection();
                return;
            }

            ShowLcNeighborhoodSelection(selectedKey);
            e.Handled = true;
        }

        private void ShowLcNeighborhoodSelection(KBItem selectedKey)
        {
            if (_displayedKeyResult == null
                || !_keyVisuals.TryGetValue(selectedKey, out DVRectangle? selectedVisual)
                || selectedVisual == null) return;

            ClearLcNeighborhoodSelection();

            IReadOnlyList<KBItem> neighbors = GetDisplayedLcNeighbors(
                _displayedKeyResult,
                selectedKey,
                out Point neighborhoodCenter,
                out double radiusPixels,
                out string neighborhoodMode);

            foreach (KBItem neighbor in neighbors)
            {
                if (_keyVisuals.TryGetValue(neighbor, out DVRectangle? neighborVisual))
                {
                    neighborVisual.Pen = new Pen(Brushes.DeepSkyBlue, 10) { DashStyle = DashStyles.Dash };
                    neighborVisual.Render();
                }
            }

            selectedVisual.Pen = CreateSelectedKeyPen();
            selectedVisual.Render();

            Pen circlePen = new(Brushes.DeepSkyBlue, 5) { DashStyle = DashStyles.Dash };
            CircleProperties circleProperties = new()
            {
                Center = neighborhoodCenter,
                Radius = radiusPixels,
                Pen = circlePen,
                Brush = Brushes.Transparent,
                Name = $"LC-{selectedKey.Name}"
            };

            _lcNeighborhoodCircle = new DVCircle(circleProperties);
            _lcNeighborhoodCircle.Render();
            ImageView.ImageShow.AddOverlayVisual(_lcNeighborhoodCircle);
            ImageView.ImageShow.BatchTopVisuals(
                _keyVisuals
                    .Where(pair => !ReferenceEquals(pair.Key, selectedKey))
                    .Select(pair => pair.Value)
                    .Append(selectedVisual));
            ImageView.ImageShow.ApplyLayoutScaleToVisuals();

            log.Debug($"Selected Key {selectedKey.Name}: mode={neighborhoodMode}, radius={radiusPixels:F2}px, neighbors={string.Join(",", neighbors.Select(item => item.Name))}");
        }

        private void ClearLcNeighborhoodSelection()
        {
            if (_lcNeighborhoodCircle != null)
            {
                ImageView.ImageShow.RemoveOverlayVisual(_lcNeighborhoodCircle);
                _lcNeighborhoodCircle = null;
            }

            if (_displayedKeyResult == null) return;

            KBItem? brightestKey = _displayedKeyResult.Items.Where(item => item.Result).OrderByDescending(item => item.Lv).FirstOrDefault();
            KBItem? darkestKey = _displayedKeyResult.Items.Where(item => item.Result).OrderBy(item => item.Lv).FirstOrDefault();
            foreach ((KBItem item, DVRectangle visual) in _keyVisuals)
            {
                visual.Pen = CreateDefaultKeyPen(item, darkestKey, brightestKey);
                visual.Render();
            }
            ImageView.ImageShow.ApplyLayoutScaleToVisuals();
        }

        private static IReadOnlyList<KBItem> GetDisplayedLcNeighbors(
            KBItemMaster result,
            KBItem selectedKey,
            out Point neighborhoodCenter,
            out double radiusPixels,
            out string neighborhoodMode)
        {
            if (TryGetRecordedLcNeighborhood(result, out double radiusMm, out double pixelsPerMillimeter, out radiusPixels))
            {
                neighborhoodCenter = new Point(
                    selectedKey.KBKeyRect.X + selectedKey.KBKeyRect.Width / 2d,
                    selectedKey.KBKeyRect.Y + selectedKey.KBKeyRect.Height / 2d);
                neighborhoodMode = $"CenterDistance/{radiusMm:F2}mm/{pixelsPerMillimeter:F4}px-per-mm";
                return GetLcNeighbors(result.Items, selectedKey, radiusPixels);
            }

            double centerX = selectedKey.KBKeyRect.X + selectedKey.KBKeyRect.Width / 2;
            double centerY = selectedKey.KBKeyRect.Y + selectedKey.KBKeyRect.Height / 2;
            double legacyRadiusPixels = selectedKey.KBKeyRect.Width + LegacyLcNeighborhoodPaddingPixels;
            neighborhoodCenter = new Point(centerX, centerY);
            radiusPixels = legacyRadiusPixels;
            neighborhoodMode = "Legacy/WholeRectangle/KeyWidth+300px";
            return result.Items
                .Where(candidate => !ReferenceEquals(candidate, selectedKey) && IsRectInCircle(candidate, centerX, centerY, legacyRadiusPixels))
                .ToList();
        }

        private static string GetLcNeighborhoodDescription(KBItemMaster result)
        {
            return TryGetRecordedLcNeighborhood(result, out double radiusMm, out double pixelsPerMillimeter, out double radiusPixels)
                ? $"中心距 ≤ {radiusMm:F2} mm ({radiusPixels:F2} px，标定 {pixelsPerMillimeter:F4} px/mm)"
                : "旧版：整键位于键宽 + 300 px 圆内";
        }

        private static bool TryGetRecordedLcNeighborhood(KBItemMaster result, out double radiusMm, out double pixelsPerMillimeter, out double radiusPixels)
        {
            radiusMm = result.KeyLcNeighborhoodRadiusMm ?? 0;
            pixelsPerMillimeter = result.KeyLcPixelsPerMillimeter ?? 0;
            radiusPixels = 0;
            if (result.KeyLcNeighborhoodVersion != CenterDistanceLcNeighborhoodVersion
                || !double.IsFinite(radiusMm)
                || radiusMm <= 0
                || !double.IsFinite(pixelsPerMillimeter)
                || pixelsPerMillimeter <= 0)
            {
                return false;
            }

            radiusPixels = radiusMm * pixelsPerMillimeter;
            return double.IsFinite(radiusPixels) && radiusPixels > 0;
        }

        private void ClearKeyOverlayState()
        {
            _displayedKeyResult = null;
            _lcNeighborhoodCircle = null;
            _keyVisuals.Clear();
        }


        private void listView1_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {

        }

        private void BuildListViewContextMenu()
        {
            var openFolderCommand = new RelayCommand(
                _ => ContextMenu_OpenFolderAndSelectFile(),
                _ => listView1.SelectedItem is KBItemMaster item && File.Exists(item.ResultImagFile));
            var flowExecutionAnalysisCommand = new RelayCommand(
                _ => ContextMenu_FlowExecutionAnalysis(),
                _ => listView1.SelectedItem is KBItemMaster item && item.BatchId > 0);

            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(new MenuItem() { Command = ApplicationCommands.Delete });
            contextMenu.Items.Add(new MenuItem() { Command = ApplicationCommands.Copy, Header = "复制" });
            contextMenu.Items.Add(new MenuItem() { Command = ViewResultManager.SaveCommand, Header = "重新导出 CSV..." });
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem() { Command = openFolderCommand, Header = "OpenFolderAndSelectFile" });
            contextMenu.Items.Add(new MenuItem() { Command = flowExecutionAnalysisCommand, Header = "流程执行分析" });
            contextMenu.Opened += (s, e) => CommandManager.InvalidateRequerySuggested();

            listView1.PreviewMouseRightButtonDown += (s, e) =>
            {
                var element = listView1.InputHitTest(e.GetPosition(listView1)) as DependencyObject;
                while (element != null && element is not ListViewItem)
                    element = VisualTreeHelper.GetParent(element);

                if (element is ListViewItem targetItem)
                    targetItem.IsSelected = true;
            };
            listView1.ContextMenu = contextMenu;
        }

        private void ContextMenu_OpenFolderAndSelectFile()
        {
            if (listView1.SelectedItem is KBItemMaster item && !string.IsNullOrWhiteSpace(item.ResultImagFile))
                PlatformHelper.OpenFolderAndSelectFile(item.ResultImagFile);
        }

        private void ContextMenu_FlowExecutionAnalysis()
        {
            MeasureBatchModel? batch = GetSelectedMeasureBatch();
            if (batch == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到批次号，请检查流程配置", "ColorVision");
                return;
            }

            var window = new FlowExecutionAnalysisWindow(batch)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            window.Show();
        }

        private MeasureBatchModel? GetSelectedMeasureBatch()
        {
            return listView1.SelectedItem is KBItemMaster item && item.BatchId > 0
                ? BatchResultMasterDao.Instance.GetById(item.BatchId)
                : null;
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
            if (!AuthManager.RequireAdmin(this)) return;

            new TestWindow().Show();
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            Interlocked.Increment(ref _resultImageRequestId);
            ImageView.EditorContext.DrawEditorContext.DrawCanvas.PreviewMouseLeftButtonDown -= ImageCanvas_PreviewMouseLeftButtonDown;
            ClearKeyOverlayState();
            ProjectKBConfig.Instance.SNChanged -= Instance_SNChanged;
            ModbusControl.GetInstance().StatusChanged -= ProjectKBWindow_StatusChanged;
            if (flowControl != null)
            {
                flowControl.FlowCompleted -= FlowControl_FlowCompleted;
                flowControl.Stop();
            }
            _flowNodeExecutionRecorder.Dispose();
            flowEngine?.Dispose();
            STNodeEditorMain?.Dispose();
            timer?.Dispose();
            logOutput?.Dispose();
            logOutput = null;
            this.DisposeTimedButtonOperations();
            GC.SuppressFinalize(this);
        }

        private void Instance_SNChanged(object? sender, string e)
        {
            if (Summary.AutoUploadSN)
            {
                if (string.IsNullOrWhiteSpace(ProjectKBConfig.Instance.SN)) return;

                DebounceTimer.AddOrResetTimer("KBUploadSN", 500, e => UploadSN(), 0);
            }
        }
        private bool IsCheckWIP = false;
        private bool IsUploadSNing { get; set; }
        private void UploadSN()
        {
            if (IsUploadSNing) return;
            IsUploadSNing = true;
            IsCheckWIP = false;
            if (Summary.UseMes)
            {

                log.Info($"CheckWIP Stage{SummaryManager.GetInstance().Summary.Stage},SN:{ProjectKBConfig.Instance.SN}");
                IntPtr a = MesDll.CheckWIP(SummaryManager.GetInstance().Summary.Stage, ProjectKBConfig.Instance.SN);
                var result = MesDll.PtrToString(a);
                log.Info("CheckWIP Stage result" + result);
                if (result != "N")
                {
                    IsUploadSNing = false;
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), result, "CheckWIP Stage Fail");
                        SNtextBox.Focus();
                        SNtextBox.SelectAll();
                    });

                    return;
                }
                IsCheckWIP = true;
                ProjectKBConfig.Instance.SNlocked = true;
            }
            else
            {
                ProjectKBConfig.Instance.SNlocked = true;
            }
            IsUploadSNing = false;
        }

        private void UploadSN_Click(object sender, RoutedEventArgs e)
        {
            if (IsUploadSNing)
            {
                MessageBox.Show("上一次上传还未完成");
            }
            Task.Run(UploadSN);
        }

        public ObservableCollection<ISearch> Searches { get; set; } = new ObservableCollection<ISearch>();
        public List<ISearch> filteredResults { get; set; } = new List<ISearch>();

        private readonly char[] Chars = new[] { ' ' };
        private void Searchbox_GotFocus(object sender, RoutedEventArgs e)
        {
            Searches.Clear();

            foreach (var item in ProjectKBConfig.Instance.TemplateItemSource)
            {
                ISearch search = new SearchMeta
                {
                    Header = item.Key,
                    GuidId = item.Key,
                    Command = new RelayCommand(a =>
                    {
                        FlowTemplate.Text = item.Key;
                    })
                };
                Searches.Add(search);

            }
        }

        private void Searchbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                string searchtext = textBox.Text;
                if (string.IsNullOrWhiteSpace(searchtext))
                {
                    filteredResults = new List<ISearch>();
                    ListViewSearch.ItemsSource = null;
                    ListViewSearch.SelectedIndex = -1;
                    SearchPopup.IsOpen = false;
                }
                else
                {
                    var keywords = searchtext.Split(Chars, StringSplitOptions.RemoveEmptyEntries);

                    filteredResults = Searches
                        .OfType<ISearch>()
                        .Where(template => keywords.All(keyword =>
                            (!string.IsNullOrEmpty(template.Header) && template.Header.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
                            (template.GuidId != null && template.GuidId.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        ))
                        .ToList();

                    ListViewSearch.ItemsSource = filteredResults;
                    if (filteredResults.Count > 0)
                    {
                        ListViewSearch.SelectedIndex = 0;
                        SearchPopup.IsOpen = true;
                    }
                    else
                    {
                        ListViewSearch.SelectedIndex = -1;
                        SearchPopup.IsOpen = false;
                    }
                }
            }
        }

        private void ExecuteSelectedSearchResult()
        {
            int selectedIndex = ListViewSearch.SelectedIndex;
            if (selectedIndex < 0 || selectedIndex >= filteredResults.Count)
                return;

            ISearch selectedSearch = filteredResults[selectedIndex];
            Searchbox.Text = string.Empty;
            SearchPopup.IsOpen = false;
            selectedSearch.Command?.Execute(this);
        }

        private void Searchbox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                e.Handled = true;
                ExecuteSelectedSearchResult();
            }
            if (e.Key == System.Windows.Input.Key.Up)
            {
                e.Handled = true;
                if (ListViewSearch.SelectedIndex > 0)
                {
                    ListViewSearch.SelectedIndex -= 1;
                    ListViewSearch.ScrollIntoView(filteredResults[ListViewSearch.SelectedIndex]);
                }
            }
            if (e.Key == System.Windows.Input.Key.Down)
            {
                e.Handled = true;
                if (ListViewSearch.SelectedIndex < filteredResults.Count - 1)
                {
                    ListViewSearch.SelectedIndex += 1;
                    ListViewSearch.ScrollIntoView(filteredResults[ListViewSearch.SelectedIndex]);
                }
            }
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                e.Handled = true;
                SearchPopup.IsOpen = false;
            }
        }

        private void ListViewSearch_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ExecuteSelectedSearchResult();
        }

        private void UnSNlocked_Click(object sender, RoutedEventArgs e)
        {
            ProjectKBConfig.Instance.SNlocked = false;
        }
    }
}
