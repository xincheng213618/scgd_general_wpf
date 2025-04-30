using ColorVision.Engine.Interfaces;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.KB;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor.Draw;
using ColorVision.Themes;
using FlowEngineLib;
using FlowEngineLib.Base;
using log4net;
using Newtonsoft.Json;
using Panuon.WPF.UI;
using ProjectKB.Config;
using ProjectKB.Modbus;
using ProjectKB.Services;
using ST.Library.UI.NodeEditor;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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

    /// <summary>
    /// Interaction logic for _windowInstance.xaml
    /// </summary>
    public partial class ProjectKBWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ProjectKBWindow));
        public static ObservableCollection<KBItemMaster> ViewResluts => ProjectKBWindowConfig.Instance.ViewResluts;

        public static ProjectKBWindowConfig Config => ProjectKBWindowConfig.Instance;

        public ProjectKBWindow()
        {
            InitializeComponent();
            this.ApplyCaption(false);
            Config.SetWindow(this);
            SizeChanged += (s, e) => Config.SetConfig(this);
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = ProjectKBConfig.Instance;
            listView1.ItemsSource = ViewResluts;
            InitFlow();

            SocketControl.GetInstance().StartServer();
            SocketControl.GetInstance().StatusChanged += ServicesChanged;
            this.Closed += (s, e) =>
            {
                SocketControl.GetInstance().StopServer();
                SocketControl.GetInstance().StatusChanged -= ServicesChanged;
            };
            ImageView.Config.IsLayoutUpdated = false;

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

            this.Closed += (s, e) =>
            {
                ModbusControl.GetInstance().StatusChanged -= ProjectKBWindow_StatusChanged;
            };

        }
        private void ServicesChanged(object? sender, EventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                log.Info("Service触发拍照，执行流程");
                RunTemplate();
            });
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

            FlowTemplate.SelectionChanged += (s, e) =>
            {
                if (ProjectKBConfig.Instance.TemplateSelectedIndex > -1)
                {
                    string Name = TemplateFlow.Params[ProjectKBConfig.Instance.TemplateSelectedIndex].Key;
                    if (ProjectKBConfig.Instance.SPECConfigs.TryGetValue(Name, out SPECConfig sPECConfig))
                    {
                        ProjectKBConfig.Instance.SPECConfig = sPECConfig;
                    }
                    else
                    {
                        sPECConfig = new SPECConfig();
                        ProjectKBConfig.Instance.SPECConfigs.TryAdd(Name, sPECConfig);
                        ProjectKBConfig.Instance.SPECConfig = sPECConfig;
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
                        if (ProjectKBConfig.Instance.LastFlowTime == 0 || ProjectKBConfig.Instance.LastFlowTime - elapsedMilliseconds < 0)
                        {
                            msg = Msg1 + Environment.NewLine + $"已经执行：{elapsedTime}";
                        }
                        else
                        {
                            long remainingMilliseconds = ProjectKBConfig.Instance.LastFlowTime - elapsedMilliseconds;
                            TimeSpan remaining = TimeSpan.FromMilliseconds(remainingMilliseconds);
                            string remainingTime = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}:{elapsed.Milliseconds:D4}";

                            msg = Msg1 + Environment.NewLine + $"已经执行：{elapsedTime}, 上次执行：{ProjectKBConfig.Instance.LastFlowTime} ms, 预计还需要：{remainingTime}";
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
            if (flowControl!=null && flowControl.IsFlowRun) return;
            if (FlowTemplate.SelectedValue is not FlowParam flowParam) 
            { 
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "流程为空，请选择流程运行", "ColorVision");
                return; 
            };

            string startNode = flowEngine.GetStartNodeName();
            if (string.IsNullOrWhiteSpace(startNode))
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "找不到完整流程，运行失败", "ColorVision");
                return;
            };
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
            ProjectKBConfig.Instance.LastFlowTime = stopwatch.ElapsedMilliseconds;
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
                        catch(Exception ex)
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
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到对映的按键，请检查流程配置是否计算KB模板", "ColorVision");
                return;
            }
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
                            kItem.KBKeyRect = keyRect;
                            kBItem.Items.Add(kItem);

                        }
                        kBItem.ResultImagFile = item.ResultImagFile;

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
                            var key = kBItem.Items.First(a => a.Name == poi.PoiName && poi.PoiWidth == a.KBKeyRect.Width);
                            if (key != null)
                            {
                                key.Lv = list.Y;
                                if (key.KBKeyRect.KBKey.Area != 0)
                                {
                                    key.Lv = key.Lv / key.KBKeyRect.KBKey.Area;
                                }
                                key.Lv = key.KBKeyRect.KBKey.KeyScale * key.Lv * ProjectKBConfig.Instance.KBLVSacle;

                            }
                        }
                    }
                }
                if (item.ImgFileType == AlgorithmResultType.POI_Y_V2)
                {
                    var pois = PoiPointResultDao.Instance.GetAllByPid(item.Id);
                    if (pois != null)
                    {
                        foreach (var poi in pois)
                        {
                            var list = JsonConvert.DeserializeObject<ObservableCollection<KBvalue>>(poi.Value);

                            var key = kBItem.Items.First(a => a.Name == poi.PoiName && poi.PoiWidth == a.KBKeyRect.Width);
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

            if (kBItem.Items.Count == 0)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到对映的按键，请检查流程配置是否计算KB模板", "ColorVision");
                return;
            }

            CalCulLc(kBItem.Items);


            foreach (var item in kBItem.Items)
            {

                if (ProjectKBConfig.Instance.SPECConfig.MinKeyLv != 0)
                {
                    item.Result = item.Result && item.Lv >= ProjectKBConfig.Instance.SPECConfig.MinKeyLv;
                }
                else
                {
                    log.Debug("跳过minLv检测");
                }
                if (ProjectKBConfig.Instance.SPECConfig.MaxKeyLv != 0)
                {
                    item.Result = item.Result && item.Lv <= ProjectKBConfig.Instance.SPECConfig.MaxKeyLv;
                }
                else
                {
                    log.Debug("跳过MaxLv检测");
                }

                if (ProjectKBConfig.Instance.SPECConfig.MinKeyLc != 0)
                {
                    item.Result = item.Result && item.Lc >= ProjectKBConfig.Instance.SPECConfig.MinKeyLc / 100;
                }
                else
                {
                    log.Debug("跳过MinKeyLc检测");
                }
                if (ProjectKBConfig.Instance.SPECConfig.MaxKeyLc != 0)
                {
                    item.Result = item.Result && item.Lc <= ProjectKBConfig.Instance.SPECConfig.MaxKeyLc / 100;
                }
                else
                {
                    log.Debug("跳过MaxLv检测");
                }
            }


            var maxKeyItem = kBItem.Items.OrderByDescending(item => item.Lv).FirstOrDefault();
            var minLKey = kBItem.Items.OrderBy(item => item.Lv).FirstOrDefault();
            kBItem.MaxLv = maxKeyItem.Lv;
            kBItem.BrightestKey = maxKeyItem.Name;
            kBItem.MinLv = minLKey.Lv;
            kBItem.DrakestKey = minLKey.Name;
            kBItem.AvgLv = kBItem.Items.Any() ? kBItem.Items.Average(item => item.Lv) : 0;
            kBItem.LvUniformity = kBItem.MinLv / kBItem.MaxLv;
            kBItem.SN = SNtextBox.Text;
            kBItem.NbrFailPoints = kBItem.Items.Count(item => !item.Result);


            CalCulLc(kBItem.Items);

            kBItem.Result = true;

            if (ProjectKBConfig.Instance.SPECConfig.MinKeyLv != 0)
            {
                kBItem.Result = kBItem.Result && kBItem.MinLv >= ProjectKBConfig.Instance.SPECConfig.MinKeyLv;
            }
            else
            {
                log.Debug("跳过minLv检测");
            }
            if (ProjectKBConfig.Instance.SPECConfig.MaxKeyLv != 0)
            {
                kBItem.Result = kBItem.Result && kBItem.MaxLv <= ProjectKBConfig.Instance.SPECConfig.MaxKeyLv;
            }
            else
            {
                log.Debug("跳过MaxLv检测");
            }
            if (ProjectKBConfig.Instance.SPECConfig.MinAvgLv != 0)
            {
                kBItem.Result = kBItem.Result && kBItem.AvgLv >= ProjectKBConfig.Instance.SPECConfig.MinAvgLv;
            }
            else
            {
                log.Debug("跳过MinAvgLv检测");
            }
            if (ProjectKBConfig.Instance.SPECConfig.MaxAvgLv != 0)
            {
                kBItem.Result = kBItem.Result && kBItem.AvgLv <= ProjectKBConfig.Instance.SPECConfig.MaxAvgLv;
            }
            else
            {
                log.Debug("跳过MaxAvgLv检测");
            }

            if (ProjectKBConfig.Instance.SPECConfig.MinUniformity != 0)
            {
                kBItem.Result = kBItem.Result && kBItem.LvUniformity >= ProjectKBConfig.Instance.SPECConfig.MinUniformity / 100;
            }
            else
            {
                log.Debug("跳过Uniformity检测");
            }

            if (ProjectKBConfig.Instance.SPECConfig.MinKeyLc != 0)
            {
                kBItem.Result = kBItem.Result && kBItem.Items.Min(item => item.Lc) >= ProjectKBConfig.Instance.SPECConfig.MinKeyLc / 100;
            }
            else
            {
                log.Debug("跳过MinKeyLc检测");
            }

            if (ProjectKBConfig.Instance.SPECConfig.MaxKeyLc != 0)
            {
                kBItem.Result = kBItem.Result && kBItem.Items.Max(item => item.Lc) <= ProjectKBConfig.Instance.SPECConfig.MaxKeyLc / 100;
            }
            else
            {
                log.Debug("跳过MaxKeyLc检测");
            }


            kBItem.Exposure = "50";

            ProjectKBConfig.Instance.SummaryInfo.ActualProduction += 1;
            if (kBItem.Result)
            {
                ProjectKBConfig.Instance.SummaryInfo.GoodProductCount += 1;
            }
            else
            {
                ProjectKBConfig.Instance.SummaryInfo.DefectiveProductCount += 1;
            }
            ViewResluts.Insert(0, kBItem);
            listView1.SelectedIndex = 0;
            string resultPath = ProjectKBConfig.Instance.ResultSavePath1 + $"\\{kBItem.SN}-{kBItem.DateTime:yyyyMMddHHmmssffff}.txt";
            string result = $"{kBItem.SN},{(kBItem.Result ? "Pass" : "Fail")}, ,";

            log.Debug($"结果正在写入{resultPath},result:{result}");
            File.WriteAllText(resultPath, result);


            Application.Current.Dispatcher.Invoke(() =>
            {
                string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
                string regexPattern = $"[{Regex.Escape(invalidChars)}]";

                string csvpath = ProjectKBConfig.Instance.ResultSavePath + $"\\{Regex.Replace(kBItem.Model, regexPattern, "")}_{kBItem.DateTime:yyyyMMdd}.csv";

                KBItemMaster.SaveCsv(kBItem, csvpath);
                log.Debug($"writecsv:{csvpath}");
            });
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                log.Debug("流程执行结束，设置寄存器为0，触发移动");
                ModbusControl.GetInstance().SetRegisterValue(0);
            });
            SNtextBox.Text = string.Empty;
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
            ProjectKBConfig.Instance.Height = row2.ActualHeight;
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
                            await Task.Delay(ProjectKBConfig.Instance.ViewImageReadDelay);
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
                                DVRectangle Rectangle = new();
                                Rectangle.Attribute.Rect = new Rect(item.KBKeyRect.X, item.KBKeyRect.Y, item.KBKeyRect.Width, item.KBKeyRect.Height);

                                if (item.Result == false)
                                {
                                    Rectangle.Attribute.Pen = new Pen(Brushes.Red, 10);
                                }
                                else if (item.Name == DrakestKey)
                                {
                                    Rectangle.Attribute.Pen = new Pen(Brushes.Violet, 10);
                                }
                                else if (item.Name == BrightestKey)
                                {
                                    Rectangle.Attribute.Pen = new Pen(Brushes.White, 10);
                                }
                                else
                                {
                                    Rectangle.Attribute.Pen = new Pen(Brushes.Gray, 5);
                                }

                                Rectangle.Attribute.Brush = Brushes.Transparent;
                                Rectangle.Attribute.Id = -1;
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
            ProjectKBConfig.Instance.SummaryInfo.Width = col1.ActualWidth;
            col1.Width = GridLength.Auto;
        }
    }
}