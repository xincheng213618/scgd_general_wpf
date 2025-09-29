using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
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
using ProjectKB.Modbus;
using SqlSugar;
using ST.Library.UI.NodeEditor;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
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
    public partial class ProjectKBWindow : Window,IDisposable
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
        public LogOutput logOutput { get; set; }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = ProjectKBConfig.Instance;

            ViewResultManager.ListView = listView1;
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => ViewResultManager.Delete(listView1.SelectedIndex), (s, e) => e.CanExecute = listView1.SelectedIndex > -1));
            listView1.ItemsSource = ViewResluts;
            InitFlow();
            Task.Run(async() =>
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

            if (ProjectKBConfig.Instance.LogControlVisibility)
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
                ProjectKBConfig.Instance.SNChanged -= Instance_SNChanged;

                SummaryManager.GetInstance().Save();
                ModbusControl.GetInstance().StatusChanged -= ProjectKBWindow_StatusChanged;
                this.Dispose();
            };

        }


        private void ProjectKBWindow_StatusChanged(object? sender, EventArgs e)
        {
            if (ModbusControl.GetInstance().CurrentValue == 1)
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    log.Info("触发拍照，执行流程");
                    RunTemplate();
                });
            }
        }

        public static RecipeManager RecipeManager => RecipeManager.GetInstance();

        public static KBRecipeConfig RecipeConfig => RecipeManager.RecipeConfig;

        #region FlowRun
        public STNodeEditor STNodeEditorMain { get; set; }
        private FlowEngineControl flowEngine;
        private Timer timer;
        Stopwatch stopwatch = new Stopwatch();

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
                    if (RecipeManager.RecipeConfigs.TryGetValue(Name, out KBRecipeConfig sPECConfig))
                    {
                        RecipeManager.RecipeConfig = sPECConfig;
                    }
                    else
                    {
                        sPECConfig = new KBRecipeConfig();
                        RecipeManager.RecipeConfigs.TryAdd(Name, sPECConfig);
                        RecipeManager.RecipeConfig = sPECConfig;
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

                        msg = $"{FlowName} 上次执行：{LastFlowTime} ms{Environment.NewLine}正在执行节点:{Msg1}{Environment.NewLine}已经执行：{elapsedTime} {Environment.NewLine}预计还需要：{remainingTime}";
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

        int TryCount;
        public async Task RunTemplate()
        {
            if (flowControl!=null && flowControl.IsFlowRun) return;

            TryCount++;
            LastFlowTime = FlowEngineConfig.Instance.FlowRunTime.TryGetValue(FlowTemplate.Text, out long time) ? time : 0;
            FlowName = FlowTemplate.Text;
            CurrentFlowResult = new KBItemMaster();
            CurrentFlowResult.Id = -1;
            CurrentFlowResult.Model = FlowTemplate.Text;
            CurrentFlowResult.SN = SNtextBox.Text;
            CurrentFlowResult.Code = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");

            CurrentFlowResult.FlowStatus = FlowStatus.Ready;
            await Refresh();
            if (string.IsNullOrWhiteSpace(flowEngine.GetStartNodeName())) { log.Info("找不到完整流程，运行失败"); return; }

            log.Info($"IsReady{flowEngine.IsReady}");
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
            BatchResultMasterDao.Instance.Save(new MeasureBatchModel() { Name = CurrentFlowResult.SN, Code = CurrentFlowResult.Code, CreateDate = DateTime.Now });

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
                   
                    var mod = MySqlControl.GetInstance().DB.Queryable<ModMasterModel>().Where(x => x.Name == item.TName && x.Pid == 150).First();
                    if (mod == null)
                    {
                        log.Warn($"item.TName{item.TName},Cant find template");
                        continue;
                    }

                    KBJson kBJson = JsonConvert.DeserializeObject<KBJson>(mod.JsonVal);
                    log.Info(JsonConvert.SerializeObject(kBJson));
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
                                key.Lv = list.Y*list.PixNumber;
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
                            log.Info(poi.Value);
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

                if (RecipeConfig.MinKeyLv != 0)
                {
                    item.Result = item.Result && item.Lv >= RecipeConfig.MinKeyLv;
                }
                else
                {
                    log.Debug("跳过minLv检测");
                }
                if (RecipeConfig.MaxKeyLv != 0)
                {
                    item.Result = item.Result && item.Lv <= RecipeConfig.MaxKeyLv;
                }
                else
                {
                    log.Debug("跳过MaxLv检测");
                }

                if (RecipeConfig.MinKeyLc != 0)
                {
                    item.Result = item.Result && item.Lc >= RecipeConfig.MinKeyLc / 100;
                }
                else
                {
                    log.Debug("跳过MinKeyLc检测");
                }
                if (RecipeConfig.MaxKeyLc != 0)
                {
                    item.Result = item.Result && item.Lc <= RecipeConfig.MaxKeyLc / 100;
                }
                else
                {
                    log.Debug("跳过MaxLv检测");
                }
            }


            var maxKeyItem = KBItemMaster.Items.OrderByDescending(item => item.Lv).FirstOrDefault();
            var minLKey = KBItemMaster.Items.OrderBy(item => item.Lv).FirstOrDefault();
            KBItemMaster.MaxLv = maxKeyItem.Lv;
            KBItemMaster.BrightestKey = maxKeyItem.Name;
            KBItemMaster.MinLv = minLKey.Lv;
            KBItemMaster.DrakestKey = minLKey.Name;
            KBItemMaster.AvgLv = KBItemMaster.Items.Any() ? KBItemMaster.Items.Average(item => item.Lv) : 0;

            KBItemMaster.LvUniformity = KBItemMaster.MaxLv ==0 ? 0: KBItemMaster.MinLv / KBItemMaster.MaxLv;
            KBItemMaster.SN = SNtextBox.Text;
            KBItemMaster.NbrFailPoints = KBItemMaster.Items.Count(item => !item.Result);


            CalCulLc(KBItemMaster.Items);

            KBItemMaster.Result = true;

            if (RecipeConfig.MinKeyLv != 0)
            {
                KBItemMaster.Result = KBItemMaster.Result && KBItemMaster.MinLv >= RecipeConfig.MinKeyLv;
            }
            else
            {
                log.Debug("跳过minLv检测");
            }
            if (RecipeConfig.MaxKeyLv != 0)
            {
                KBItemMaster.Result = KBItemMaster.Result && KBItemMaster.MaxLv <= RecipeConfig.MaxKeyLv;
            }
            else
            {
                log.Debug("跳过MaxLv检测");
            }
            if (RecipeConfig.MinAvgLv != 0)
            {
                KBItemMaster.Result = KBItemMaster.Result && KBItemMaster.AvgLv >= RecipeConfig.MinAvgLv;
            }
            else
            {
                log.Debug("跳过MinAvgLv检测");
            }
            if (RecipeConfig.MaxAvgLv != 0)
            {
                KBItemMaster.Result = KBItemMaster.Result && KBItemMaster.AvgLv <= RecipeConfig.MaxAvgLv;
            }
            else
            {
                log.Debug("跳过MaxAvgLv检测");
            }

            if (RecipeConfig.MinUniformity != 0)
            {
                KBItemMaster.Result = KBItemMaster.Result && KBItemMaster.LvUniformity >= RecipeConfig.MinUniformity / 100;
            }
            else
            {
                log.Debug("跳过Uniformity检测");
            }

            if (RecipeConfig.MinKeyLc != 0)
            {
                KBItemMaster.Result = KBItemMaster.Result && KBItemMaster.Items.Min(item => item.Lc) >= RecipeConfig.MinKeyLc / 100;
            }
            else
            {
                log.Debug("跳过MinKeyLc检测");
            }

            if (RecipeConfig.MaxKeyLc != 0)
            {
                KBItemMaster.Result = KBItemMaster.Result && KBItemMaster.Items.Max(item => item.Lc) <= RecipeConfig.MaxKeyLc / 100;
            }
            else
            {
                log.Debug("跳过MaxKeyLc检测");
            }


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

            string resultPath = ViewResultManager.Config.TextSavePath + $"\\{KBItemMaster.SN}-{KBItemMaster.CreateTime:yyyyMMddHHmmssffff}.txt";
            string result = $"{KBItemMaster.SN},{(KBItemMaster.Result ? "Pass" : "Fail")}, ,";

            log.Debug($"结果正在写入{resultPath},result:{result}");
            File.WriteAllText(resultPath, result);

            Application.Current.Dispatcher.Invoke(() =>
            {
                string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
                string regexPattern = $"[{Regex.Escape(invalidChars)}]";

                string csvpath = ViewResultManager.Config.CsvSavePath + $"\\{Regex.Replace(KBItemMaster.Model, regexPattern, "")}_{KBItemMaster.CreateTime:yyyyMMdd}.csv";

                KBItemMaster.SaveCsv(csvpath);
                log.Debug($"writecsv:{csvpath}");
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
                foreach (var keys in kBItems.Where(a=>a != item))
                {
                    if (IsRectInCircle(keys, centex, centey, item.KBKeyRect.Width + 300))
                        round.Add(keys);
                }
                List<string> strings = round.Select(keys => keys.Name).ToList();
                log.Debug($"Round Key {item.Name}: {string.Join(",", strings)}");

                double averagelv = round.Count>0 ? round.Average(item => item.Lv) : 0;
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

        public void GenoutputText(KBItemMaster kmitemmaster)
        {
            NGResult.Text = kmitemmaster.Result ? "OK" : "NG";
            NGResult.Foreground = kmitemmaster.Result ? Brushes.Green : Brushes.Red;

            outputText.Background = kmitemmaster.Result ? Brushes.Lime : Brushes.Red;
            outputText.Document.Blocks.Clear(); // 清除之前的内容

            string outtext = string.Empty;
            outtext += $"Model:{kmitemmaster.Model}" + Environment.NewLine; 
            outtext += $"SN:{kmitemmaster.SN}" + Environment.NewLine;
            outtext += $"Poiints of Interest: " + Environment.NewLine;
            outtext += $"{kmitemmaster.CreateTime:yyyy/MM//dd HH:mm:ss}" + Environment.NewLine;

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

            foreach (var item in kmitemmaster.Items)
            {
                string formattedString = $"[{item.Name}]";

                outtext += $"{formattedString,-20} {item.Lv,-10:F2}   {item.Lc*100,10:F2}%  {(item.Result?"":"Fail")}" +Environment.NewLine;
                run = new Run(outtext);
                run.Foreground = kmitemmaster.Result ? Brushes.Black : Brushes.White;
                run.FontSize += 1;
                paragraph.Inlines.Add(run);
                outtext = string.Empty;
            }
            outputText.Document.Blocks.Add(paragraph);

            outtext += $"Min Lv= {kmitemmaster.MinLv:F2} cd/m2" + Environment.NewLine;
            outtext += $"Max Lv= {kmitemmaster.MaxLv:F2} cd/m2" + Environment.NewLine;
            outtext += $"Darkest Key= {kmitemmaster.DrakestKey}" + Environment.NewLine;
            outtext += $"Brightest Key= {kmitemmaster.BrightestKey}" + Environment.NewLine;

            outtext += Environment.NewLine;
            outtext += $"Pass/Fail Criteria:" + Environment.NewLine;
            outtext += $"NbrFail Points={kmitemmaster.NbrFailPoints}" + Environment.NewLine;
            outtext += $"Avg Lv={kmitemmaster.AvgLv:F2}" + Environment.NewLine;
            outtext += $"Lv Uniformity={kmitemmaster.LvUniformity * 100:F2}%" + Environment.NewLine;

            outtext += kmitemmaster.Result ? "Pass" : "Fail" + Environment.NewLine;

            run = new Run(outtext);
            run.Foreground = kmitemmaster.Result ? Brushes.Black : Brushes.White;
            run.FontSize += 1;
            paragraph = new Paragraph(run);
            outtext = string.Empty;
            outputText.Document.Blocks.Add(paragraph);
            SNtextBox.Focus();
        }


        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            ViewResultManager.Config.Height = row2.ActualHeight;
            row2.Height = GridLength.Auto;
        }

        private void Button_Click_Clear(object sender, RoutedEventArgs e)
        {
            ViewResluts.Clear();
            ImageView.Clear();
            outputText.Document.Blocks.Clear();
            outputText.Background = Brushes.White;
            NGResult.Text = string.Empty;
        }

        private void listView1_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex >-1)
            {
                var kBItem = ViewResluts[listView.SelectedIndex];
                GenoutputText(kBItem);

                var maxKeyItem = kBItem.Items.Where(a=>a.Result).OrderByDescending(item => item.Lv).FirstOrDefault();
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
                            log.Warn($"fileInfo.Length{fileInfo.Length}");
                            using (var fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                            {
                                log.Warn("文件可以读取，没有被占用。");
                            }
                            if (fileInfo.Length > 0)
                            {
                                _=Application.Current.Dispatcher.BeginInvoke(() =>
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
                            _=Application.Current.Dispatcher.BeginInvoke(() =>
                            {
                                ImageView.OpenImage(kBItem.ResultImagFile);
                                ImageView.ImageShow.Clear();
                            });
                        }
                        _=Application.Current.Dispatcher.BeginInvoke(() =>
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
            new TestWindow().Show();
        }

        private void GridSplitter_DragCompleted1(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            Summary.Width = col1.ActualWidth;
            col1.Width = GridLength.Auto;
        }

        public void Dispose()
        {
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
                    IsUploadSNing =false;
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), result,"CheckWIP Stage Fail");
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
                    SearchPopup.IsOpen = false;
                }
                else
                {
                    SearchPopup.IsOpen = true;
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
                    }
                }
            }
        }

        private void Searchbox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                if (ListViewSearch.SelectedIndex > -1)
                {
                    Searchbox.Text = string.Empty;
                    filteredResults[ListViewSearch.SelectedIndex].Command?.Execute(this);
                }
            }
            if (e.Key == System.Windows.Input.Key.Up)
            {
                if (ListViewSearch.SelectedIndex > 0)
                    ListViewSearch.SelectedIndex -= 1;
            }
            if (e.Key == System.Windows.Input.Key.Down)
            {
                if (ListViewSearch.SelectedIndex < filteredResults.Count - 1)
                    ListViewSearch.SelectedIndex += 1;
            }
        }

        private void ListViewSearch_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ListViewSearch.SelectedIndex > -1)
            {
                Searchbox.Text = string.Empty;
                filteredResults[ListViewSearch.SelectedIndex].Command?.Execute(this);
            }
        }

        private void ListViewSearch_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListViewSearch.SelectedIndex > -1)
            {
                Searchbox.Text = string.Empty;
                filteredResults[ListViewSearch.SelectedIndex].Command?.Execute(this);
            }
        }

        private void UnSNlocked_Click(object sender, RoutedEventArgs e)
        {
            ProjectKBConfig.Instance.SNlocked = false;
        }
    }
}