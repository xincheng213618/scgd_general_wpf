#pragma warning disable CA1805,CS4014,CS8601,CS8602,CS8604
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Batch;
using ColorVision.Engine;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates.Flow;
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
using System.ComponentModel;
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
        public static ViewResultManager ViewResultManager => ViewResultManager.GetInstance();
        public static ObservableCollection<KBItemMaster> ViewResluts => ViewResultManager.ViewResluts;
        public static ProjectKBWindowConfig Config => ProjectKBWindowConfig.Instance;

        public static Summary Summary => SummaryManager.GetInstance().Summary;

        public ProjectKBWindow()
        {
            InitializeComponent();
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
            listView1.ItemsSource = ViewResluts;
            InitFlow();
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

            ProjectKBConfig.Instance.PropertyChanged += ProjectKBConfig_PropertyChanged;
            ApplyLogControlVisibility();

            // 初始化权限系统
            InitAuth();

            this.Closed += (s, e) =>
            {
                ProjectKBConfig.Instance.SNChanged -= Instance_SNChanged;
                ProjectKBConfig.Instance.PropertyChanged -= ProjectKBConfig_PropertyChanged;

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

        private void ProjectKBConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProjectKBConfig.LogControlVisibility))
            {
                ApplyLogControlVisibility();
            }

        }

        private void ApplyLogControlVisibility()
        {
            if (ProjectKBConfig.Instance.LogControlVisibility)
            {
                LogGrid.Visibility = Visibility.Visible;
                if (logOutput == null)
                {
                    logOutput = new LogOutput("%date{HH:mm:ss} [%thread] %-5level %message%newline");
                    LogGrid.Children.Add(logOutput);
                }
                return;
            }

            LogGrid.Visibility = Visibility.Collapsed;
            if (logOutput == null)
            {
                return;
            }

            LogGrid.Children.Remove(logOutput);
            logOutput.Dispose();
            logOutput = null;
        }

        private void ProjectKBWindow_StatusChanged(object? sender, EventArgs e)
        {
            if (ModbusControl.GetInstance().CurrentValue == 1)
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
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
                    RunTemplate();
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

        #region FlowRun
        public STNodeEditor STNodeEditorMain { get; set; }
        private FlowEngineControl flowEngine;
        private Timer timer;
        Stopwatch stopwatch = new Stopwatch();
        private int _pendingUiUpdate;

        public void InitFlow()
        {
            MQTTConfig mQTTConfig = MQTTSetting.Instance.MQTTConfig;
            MQTTHelper.SetDefaultCfg(mQTTConfig.Host, mQTTConfig.Port, mQTTConfig.UserName, mQTTConfig.UserPwd, false, null);
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


        public async Task Refresh()
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
                log.Error("刷新流程失败", ex);
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
        private void TestClick(object sender, RoutedEventArgs e)
        {
            RunTemplate();
        }

        int TryCount;
        public async Task RunTemplate()
        {
            if (flowControl != null && flowControl.IsFlowRun)
            {
                log.Info("当前存在流程执行");
                return;
            }

            TryCount++;
            LastFlowTime = FlowEngineConfig.Instance.FlowRunTime.TryGetValue(FlowTemplate.Text, out long time) ? time : 0;
            FlowName = FlowTemplate.Text;
            CurrentFlowResult = new KBItemMaster();
            CurrentFlowResult.Id = -1;
            CurrentFlowResult.Model = FlowTemplate.Text;
            CurrentFlowResult.SN = SNtextBox.Text;
            CurrentFlowResult.Code = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");

            RecipeManager.SetCurrentTemplate(FlowName);

            CurrentFlowResult.FlowStatus = FlowStatus.Ready;
            await Refresh();
            if (string.IsNullOrWhiteSpace(flowEngine.GetStartNodeName()))
            {
                log.Info("找不到完整流程，运行失败");
                TryCount = 0;
                return;
            }

            log.Info($"IsReady{flowEngine.IsReady}");
            if (!flowEngine.IsReady)
            {
                string base64 = string.Empty;
                flowEngine.LoadFromBase64(base64);
                await Refresh();
                log.Info($"IsReady{flowEngine.IsReady}");
            }

            if (!await PreProcessingAsync(FlowName, CurrentFlowResult.SN))
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
            Interlocked.Exchange(ref _pendingUiUpdate, 0);
            stopwatch.Reset();
            stopwatch.Start();
            BatchResultMasterDao.Instance.Save(new MeasureBatchModel() { Name = CurrentFlowResult.SN, Code = CurrentFlowResult.Code, CreateDate = DateTime.Now });

            flowControl.Start(CurrentFlowResult.Code);
            timer.Change(0, 500); // 启动定时器
        }

        private async Task<bool> PreProcessingAsync(string flowName, string serialNumber)
        {
            var serverNodes = new ObservableCollection<CVBaseServerNode>(STNodeEditorMain.Nodes.OfType<CVBaseServerNode>());
            return await PreProcessManager.GetInstance().ExecuteAsync(flowName, serialNumber, serverNodes);
        }


        private FlowControl flowControl;
        private void FlowControl_FlowCompleted(object? sender, FlowControlData FlowControlData)
        {
            flowControl.FlowCompleted -= FlowControl_FlowCompleted;

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(() => HandleFlowCompleted(FlowControlData));
                return;
            }

            HandleFlowCompleted(FlowControlData);
        }

        private void HandleFlowCompleted(FlowControlData FlowControlData)
        {
            stopwatch.Stop();
            timer.Change(Timeout.Infinite, 500); // 停止定时器
            Interlocked.Exchange(ref _pendingUiUpdate, 0);
            FlowEngineConfig.Instance.FlowRunTime[FlowTemplate.Text] = stopwatch.ElapsedMilliseconds;

            log.Info($"流程执行Elapsed Time: {stopwatch.ElapsedMilliseconds} ms");
            CurrentFlowResult.RunTime = stopwatch.ElapsedMilliseconds;

            logTextBox.Text = FlowName + Environment.NewLine + FlowControlData.EventName;
            CurrentFlowResult.Msg = FlowControlData.EventName;


            ProjectKBConfig.Instance.SNlocked = false;
            SNtextBox.Focus();

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
                CurrentFlowResult.FlowStatus = FlowStatus.OverTime;
                ViewResluts.Insert(0, CurrentFlowResult); //倒序插入
                flowEngine.LoadFromBase64(string.Empty);
                Refresh();
                if (TryCount < ProjectKBConfig.Instance.TryCountMax)
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
                TryCount = 0;
                log.Error("流程运行失败" + FlowControlData.EventName + Environment.NewLine + FlowControlData.Params);
                CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                CurrentFlowResult.Msg = FlowControlData.Params;

                if (CurrentFlowResult.Msg.Contains("SDK return failed"))
                {
                    MeasureBatchModel Batch = BatchResultMasterDao.Instance.GetByCode(FlowControlData.SerialNumber);
                    if (Batch != null)
                    {
                        var values = MeasureImgResultDao.Instance.GetAllByBatchId(Batch.Id);
                        if (values.Count > 0)
                        {
                            CurrentFlowResult.ResultImagFile = values[0].FileUrl;
                        }
                    }
                }

                ViewResluts.Insert(0, CurrentFlowResult); //倒序插入
                logTextBox.Text = FlowName + Environment.NewLine + FlowControlData.EventName + Environment.NewLine + FlowControlData.Params;
            }
        }

        KBItemMaster CurrentFlowResult { get; set; }

        #endregion
        private void Processing(string SerialNumber)
        {
            KBItemMaster KBItemMaster = CurrentFlowResult ?? new KBItemMaster();
            KBItemMaster.Model = FlowTemplate.Text;
            KBItemMaster.SN = SNtextBox.Text;
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

            CalCulLc(KBItemMaster.Items);

            foreach (var item in KBItemMaster.Items)
            {
                if (RecipeConfig.EnableKeyLvLimit)
                {
                    item.Result = item.Result && item.Lv >= RecipeConfig.MinKeyLv;
                    item.Result = item.Result && item.Lv <= RecipeConfig.MaxKeyLv;
                }

                if (RecipeConfig.EnableKeyLcLimit)
                {
                    item.Result = item.Result && item.Lc >= RecipeConfig.MinKeyLc / 100;
                    item.Result = item.Result && item.Lc <= RecipeConfig.MaxKeyLc / 100;
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
            BacklightAutotuneService.Apply(KBItemMaster, RecipeConfig);
            KBItemMaster.SN = SNtextBox.Text;


            CalCulLc(KBItemMaster.Items);

            KBItemMaster.Result = true;

            if (RecipeConfig.EnableKeyLvLimit)
            {
                KBItemMaster.Result = KBItemMaster.Result && BacklightAutotuneService.GetOriginalMinLv(KBItemMaster) >= RecipeConfig.MinKeyLv;
                KBItemMaster.Result = KBItemMaster.Result && KBItemMaster.MaxLv <= RecipeConfig.MaxKeyLv;
            }

            if (RecipeConfig.EnableAvgLvLimit)
            {
                double originalAvgLv = BacklightAutotuneService.GetOriginalAvgLv(KBItemMaster);
                KBItemMaster.Result = KBItemMaster.Result && originalAvgLv >= RecipeConfig.MinAvgLv;
                KBItemMaster.Result = KBItemMaster.Result && originalAvgLv <= RecipeConfig.MaxAvgLv;
            }

            if (RecipeConfig.EnableUniformityLimit)
            {
                KBItemMaster.Result = KBItemMaster.Result && BacklightAutotuneService.GetOriginalLvUniformity(KBItemMaster) >= RecipeConfig.MinUniformity / 100;
            }

            if (RecipeConfig.EnableKeyLcLimit)
            {
                KBItemMaster.Result = KBItemMaster.Result && KBItemMaster.Items.Min(item => item.Lc) >= RecipeConfig.MinKeyLc / 100;
                KBItemMaster.Result = KBItemMaster.Result && KBItemMaster.Items.Max(item => item.Lc) <= RecipeConfig.MaxKeyLc / 100;
            }

            KBItemMaster.NbrFailPoints = KBItemMaster.Items.Count(item => !item.Result);

            KBItemMaster.Exposure = "50";

            Summary.ActualProduction += 1;
            if (KBItemMaster.Result)
            {
                Summary.GoodProductCount += 1;
            }
            else
            {
                Summary.DefectiveProductCount += 1;
            }
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
        public static void CalCulLc(ObservableCollection<KBItem> kBItems)
        {
            if (kBItems.Count == 0) return;
            foreach (var item in kBItems)
            {
                double centex = item.KBKeyRect.X + item.KBKeyRect.Width / 2;
                double centey = item.KBKeyRect.Y + item.KBKeyRect.Height / 2;

                List<KBItem> round = new List<KBItem>();
                foreach (var keys in kBItems.Where(a => a != item))
                {
                    if (IsRectInCircle(keys, centex, centey, item.KBKeyRect.Width + 300))
                        round.Add(keys);
                }
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
            NGResult.Text = kmitemmaster.Result ? "OK" : "NG";
            NGResult.Foreground = kmitemmaster.Result ? Brushes.Green : Brushes.Red;

            outputText.Background = kmitemmaster.Result ? Brushes.Lime : Brushes.Red;
            outputText.Document.Blocks.Clear(); // 清除之前的内容

            KBRecipeConfig recipe = GetRecipeConfig(kmitemmaster);
            Brush normalTextBrush = kmitemmaster.Result ? Brushes.Black : Brushes.White;

            string outtext = string.Empty;
            outtext += $"机种 (Model):{kmitemmaster.Model}" + Environment.NewLine;
            outtext += $"SN:{kmitemmaster.SN}" + Environment.NewLine;
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

            bool minLvFailure = recipe.EnableKeyLvLimit && BacklightAutotuneService.GetOriginalMinLv(kmitemmaster) < recipe.MinKeyLv;
            bool maxLvFailure = recipe.EnableKeyLvLimit && kmitemmaster.MaxLv > recipe.MaxKeyLv;
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
            bool avgLvFailure = recipe.EnableAvgLvLimit && (originalAvgLv < recipe.MinAvgLv || originalAvgLv > recipe.MaxAvgLv);
            AppendCriteriaMetricRow(criteriaRows, "平均亮度", "Avg Lv", $"{kmitemmaster.AvgLv:F2} cd/m2", string.Empty, avgLvFailure, normalTextBrush);
            bool uniformityFailure = recipe.EnableUniformityLimit && BacklightAutotuneService.GetOriginalLvUniformity(kmitemmaster) < recipe.MinUniformity / 100;
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

        private static KBRecipeConfig GetRecipeConfig(KBItemMaster kmitemmaster)
        {
            RecipeManager recipeManager = RecipeManager.GetInstance();
            return recipeManager.RecipeConfigs.TryGetValue(kmitemmaster.Model, out KBRecipeConfig? matchedRecipe)
                ? matchedRecipe
                : RecipeConfig;
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

        private static bool IsKeyFailure(KBItem item, KBRecipeConfig recipe)
        {
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

        private static void AppendLocalContrastSummary(TableRowGroup rowGroup, KBItemMaster kmitemmaster, KBRecipeConfig recipe, Brush normalTextBrush)
        {
            if (!kmitemmaster.Items.Any())
            {
                return;
            }

            KBItem minLcItem = kmitemmaster.Items.OrderBy(item => item.Lc).First();
            KBItem maxLcItem = kmitemmaster.Items.OrderByDescending(item => item.Lc).First();
            double minLcPercent = minLcItem.Lc * 100;
            double maxLcPercent = maxLcItem.Lc * 100;
            bool minLcFailure = recipe.EnableKeyLcLimit && minLcPercent < recipe.MinKeyLc;
            bool maxLcFailure = recipe.EnableKeyLcLimit && maxLcPercent > recipe.MaxKeyLc;

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

            ViewResluts.Clear();
            ImageView.Clear();
            outputText.Document.Blocks.Clear();
            outputText.Background = Brushes.White;
            NGResult.Text = string.Empty;
        }

        private void listView1_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                var kBItem = ViewResluts[listView.SelectedIndex];
                GenoutputText(kBItem);

                var maxKeyItem = kBItem.Items.Where(a => a.Result).OrderByDescending(item => item.Lv).FirstOrDefault();
                var minLKey = kBItem.Items.Where(a => a.Result).OrderBy(item => item.Lv).FirstOrDefault();


                string DrakestKey = minLKey?.Name;
                string BrightestKey = maxKeyItem?.Name;
                Task.Run(async () =>
                {
                    if (File.Exists(kBItem.ResultImagFile))
                    {
                        try
                        {
                            var fileInfo = new FileInfo(kBItem.ResultImagFile);
                            using (var fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                            {

                            }
   
                            if (fileInfo.Length > 0)
                            {
                                _ = Application.Current.Dispatcher.BeginInvoke(() =>
                                {
                                    ImageView.OpenImage(kBItem.ResultImagFile);
                                    ImageView.ImageShow.Clear();
                                });
                            }
                        }
                        catch
                        {
                            log.Warn("文件还在写入");
                            await Task.Delay(ViewResultManager.Config.ViewImageReadDelay);
                            _ = Application.Current.Dispatcher.BeginInvoke(() =>
                            {
                                ImageView.OpenImage(kBItem.ResultImagFile);
                                ImageView.ImageShow.Clear();
                            });
                        }
                        _ = Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            foreach (var item in kBItem.Items)
                            {
                                RectangleProperties rectangleProperties = new RectangleProperties();
                                rectangleProperties.Rect = new Rect(item.KBKeyRect.X, item.KBKeyRect.Y, item.KBKeyRect.Width, item.KBKeyRect.Height);

                                if (item.Result == false)
                                {
                                    rectangleProperties.Pen = new Pen(Brushes.Red, 10);
                                }
                                else if (item.Name == DrakestKey)
                                {
                                    rectangleProperties.Pen = new Pen(Brushes.Violet, 10);
                                }
                                else if (item.Name == BrightestKey)
                                {
                                    rectangleProperties.Pen = new Pen(Brushes.White, 10);
                                }
                                else
                                {
                                    rectangleProperties.Pen = new Pen(Brushes.Gray, 5);
                                }

                                rectangleProperties.Brush = Brushes.Transparent;
                                rectangleProperties.Name = item.Name;
                                rectangleProperties.Id = -1;

                                DVRectangle Rectangle = new DVRectangle(rectangleProperties);

                                Rectangle.Render();
                                ImageView.AddVisual(Rectangle);
                            }


                        });

                    }
                });

            }
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
            if (!AuthManager.RequireAdmin(this)) return;

            new TestWindow().Show();
        }

        private void GridSplitter_DragCompleted1(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            Summary.Width = col1.ActualWidth;
            col1.Width = GridLength.Auto;
        }

        public void Dispose()
        {
            ProjectKBConfig.Instance.SNChanged -= Instance_SNChanged;
            ProjectKBConfig.Instance.PropertyChanged -= ProjectKBConfig_PropertyChanged;
            ModbusControl.GetInstance().StatusChanged -= ProjectKBWindow_StatusChanged;
            if (flowControl != null)
            {
                flowControl.FlowCompleted -= FlowControl_FlowCompleted;
                flowControl.Stop();
            }
            STNodeEditorMain?.Dispose();
            timer?.Dispose();
            logOutput?.Dispose();
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
