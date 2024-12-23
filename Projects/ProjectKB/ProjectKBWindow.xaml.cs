using ColorVision.Common.Utilities;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.DAO;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.KB;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor.Draw;
using ColorVision.Themes;
using FlowEngineLib;
using FlowEngineLib.Base;
using log4net;
using MQTTMessageLib.Algorithm;
using Newtonsoft.Json;
using Panuon.WPF.UI;
using ST.Library.UI.NodeEditor;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection.Emit;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ProjectKB
{
    class KBvalue
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
        public static  ObservableCollection<KBItemMaster> ViewResluts => ProjectKBWindowConfig.Instance.ViewResluts;

        public static ProjectKBWindowConfig Config => ProjectKBWindowConfig.Instance;

        public ProjectKBWindow()
        {
            InitializeComponent();
            this.ApplyCaption(false);
            Config.SetWindow(this);
            SizeChanged += (s, e) => Config.SetConfig(this);
        }

        public STNodeEditor STNodeEditorMain { get; set; }
        private FlowEngineControl flowEngine;
        private Timer timer;
        Stopwatch stopwatch = new Stopwatch();
        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = ProjectKBConfig.Instance;
            listView1.ItemsSource = ViewResluts;

            MQTTConfig mQTTConfig = MQTTSetting.Instance.MQTTConfig;
            MQTTHelper.SetDefaultCfg(mQTTConfig.Host, mQTTConfig.Port, mQTTConfig.UserName, mQTTConfig.UserPwd, false, null);
            flowEngine = new FlowEngineControl(false);
            ImageView.Config.IsLayoutUpdated = false;
            STNodeEditorMain = new STNodeEditor();
            STNodeEditorMain.LoadAssembly("FlowEngineLib.dll");
            flowEngine.AttachNodeEditor(STNodeEditorMain);

            FlowTemplate.SelectionChanged += (s, e) =>
            {
                if (FlowTemplate.SelectedIndex > -1)
                {
                    Refresh();
                }
            };

            timer = new Timer(TimeRun, null, 0, 100);
            timer.Change(Timeout.Infinite, 100); // 停止定时器

            Task.Run(() =>
            {
                if (ProjectKBConfig.Instance.AutoModbusConnect)
                {
                    bool con = ModbusControl.GetInstance().Connect();
                    if (con)
                    {
                        log.Debug("初始化寄存器设置为0");
                        ModbusControl.GetInstance().SetRegisterValue(0);
                    }

                    ModbusControl.GetInstance().StatusChanged += (s, e) =>
                    {
                        if (ModbusControl.GetInstance().CurrentValue == 1)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                log.Info("触发拍照，执行流程");
                                RunTemplate();
                            });
                        }
                    };
                }
            });

        }

        public void Refresh()
        {
            foreach (var item in STNodeEditorMain.Nodes)
            {
                if (item is CVCommonNode algorithmNode)
                {
                    algorithmNode.nodeRunEvent -= UpdateMsg;
                }
            }
            var tokens = ServiceManager.GetInstance().ServiceTokens;
            log.Info($"tokenscount{tokens.Count}");
            flowEngine.LoadFromBase64(FlowParam.Params[FlowTemplate.SelectedIndex].Value.DataBase64, tokens);
            foreach (var item in STNodeEditorMain.Nodes)
            {
                if (item is CVCommonNode algorithmNode)
                {
                    algorithmNode.nodeRunEvent += UpdateMsg;
                }
            }
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
                            msg = Msg1 + Environment.NewLine + $"已经执行：{elapsedTime}";
                        }
                        else
                        {
                            long remainingMilliseconds = ProjectKBConfig.Instance.LastFlowTime - elapsedMilliseconds;
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
            if (handler!=null) return;

            if (FlowTemplate.SelectedValue is FlowParam flowParam)
            {
                string startNode = flowEngine.GetStartNodeName();
                if (!string.IsNullOrWhiteSpace(startNode))
                {
                    flowControl ??= new ColorVision.Engine.Templates.Flow.FlowControl(MQTTControl.GetInstance(), flowEngine);

                    handler = PendingBox.Show(this, "TTL:" + "0", "流程运行", true);
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
                    handler.Cancelling += (s, e) =>
                    {
                        flowControl.Stop();
                        stopwatch.Stop();
                        timer.Change(Timeout.Infinite, 100); // 停止定时器
                        flowControl.FlowCompleted -= FlowControl_FlowCompleted;
                        handler?.Close();
                        handler = null;
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

        private void Handler_Cancelling(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            throw new NotImplementedException();
        }

        private ColorVision.Engine.Templates.Flow.FlowControl flowControl;

        private void FlowControl_FlowCompleted(object? sender, EventArgs e)
        {
            flowControl.FlowCompleted -= FlowControl_FlowCompleted;
            handler?.Close();
            handler = null;
            if (sender is FlowControlData FlowControlData)
            {
                if (FlowControlData.EventName == "Completed" || FlowControlData.EventName == "Canceled" || FlowControlData.EventName == "OverTime" || FlowControlData.EventName == "Failed")
                {
                    stopwatch.Stop();
                    timer.Change(Timeout.Infinite, 100); // 停止定时器
                    if (FlowControlData.EventName == "Completed")
                    {
                        try
                        {
                            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                            bool sucess = true;
                            ProjectKBConfig.Instance.LastFlowTime = stopwatch.ElapsedMilliseconds;
                            log.Info($"流程执行Elapsed Time: {stopwatch.ElapsedMilliseconds} ms");
                            var Batch = BatchResultMasterDao.Instance.GetByCode(FlowControlData.SerialNumber);
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
                                                key.Lv = key.KBKeyRect.KBKey.KeyScale * key.Lv;

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
                                                if(list!=null && list.Count == 2)
                                                {
                                                    key.Lv = list[0].Y;
                                                    key.Lv = key.Lv  * list[0].PixNumber;
                                                    if (key.KBKeyRect.KBKey.Area != 0)
                                                    {
                                                        key.Lv = key.Lv / key.KBKeyRect.KBKey.Area;
                                                    }
                                                    key.Lv = key.KBKeyRect.KBKey.KeyScale * key.Lv;
                                                }
                                            }
                                        }
                                    }
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

                            kBItem.Result = true;

                            if (ProjectKBConfig.Instance.SPECConfig.MinLv!= 0)
                            {
                                kBItem.Result= kBItem.Result && kBItem.MinLv >= ProjectKBConfig.Instance.SPECConfig.MinLv;
                            }
                            else
                            {
                                log.Debug("跳过minLv检测");
                            }
                            if (ProjectKBConfig.Instance.SPECConfig.MaxLv != 0)
                            {
                                kBItem.Result = kBItem.Result && kBItem.MaxLv <= ProjectKBConfig.Instance.SPECConfig.MaxLv;
                            }
                            else
                            {
                                log.Debug("跳过MaxLv检测");
                            }
                            if (ProjectKBConfig.Instance.SPECConfig.AvgLv != 0)
                            {
                                kBItem.Result = kBItem.Result && kBItem.AvgLv >= ProjectKBConfig.Instance.SPECConfig.AvgLv;
                            }
                            else
                            {
                                log.Debug("跳过AvgLv检测");
                            }
                            if (ProjectKBConfig.Instance.SPECConfig.Uniformity != 0)
                            {
                                kBItem.Result = kBItem.Result && kBItem.LvUniformity >= ProjectKBConfig.Instance.SPECConfig.Uniformity;
                            }
                            else
                            {
                                log.Debug("跳过Uniformity检测");
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
                            string resultPath = ProjectKBConfig.Instance.ResultSavePath + $"\\{kBItem.SN}-{kBItem.DateTime:yyyyMMddHHmmssffff}.txt";
                            string result = $"{kBItem.SN},{(kBItem.Result ? "Pass" : "Fail")}, ,";
                            log.Debug($"结果正在写入{resultPath},result:{result}");
                            File.WriteAllText(resultPath, result);


                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                string csvpath = ProjectKBConfig.Instance.ResultSavePath + $"\\{kBItem.DateTime:yyyyMMdd}.csv";
                                KBItemMaster.SaveCsv(kBItem, csvpath);
                                log.Debug($"writecsv:{csvpath}");
                            });
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                log.Debug("流程执行结束，设置寄存器为0，触发移动");
                                ModbusControl.GetInstance().SetRegisterValue(0);
                            });
                            SNtextBox.Text = string.Empty;
                        }
                        catch(Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                        }

                    }
                    else
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), "流程运行失败" + FlowControlData.EventName, "ColorVision");
                    }
                }
                else
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), "流程运行失败" + FlowControlData.EventName, "ColorVision");
                    Refresh();
                }

            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "流程运行异常", "ColorVision");
                Refresh();
            }
        }


        public void GenoutputText(KBItemMaster kmitemmaster)
        {
            string outtext = string.Empty;
            outtext += $"Model:{kmitemmaster.Model}" + Environment.NewLine; 
            outtext += $"SN:{kmitemmaster.SN}" + Environment.NewLine;
            outtext += $"Poiints of Interest: " + Environment.NewLine;
            outtext += $"{DateTime.Now:yyyy/MM//dd HH:mm:ss}" + Environment.NewLine;
            outtext += Environment.NewLine;
            string title1 = "PT";
            string title2 = "Lv";
            string title3 = "Cx";
            string title4 = "Cy";
            string title5 = "Lc";
            outtext += $"{title1,-20}   {title2,-10} {title3,10} {title4,10}" + Environment.NewLine;

            foreach (var item in kmitemmaster.Items)
            {
                string formattedString = $"[{item.Name}]";

                outtext += $"{formattedString,-20}   {item.Lv,-10:F4}   {item.Cx,10:F4}   {item.Cy,10:F4}" + Environment.NewLine;
            }

            outtext += Environment.NewLine;
            outtext += $"Min Lv= {kmitemmaster.MinLv} cd/m2" + Environment.NewLine;
            outtext += $"Max Lv= {kmitemmaster.MaxLv} cd/m2" + Environment.NewLine;
            outtext += $"Darkest Key= {kmitemmaster.DrakestKey}" + Environment.NewLine;
            outtext += $"Brightest Key= {kmitemmaster.BrightestKey}" + Environment.NewLine;
            outtext += $"Avg Cx= {kmitemmaster.AvgC1}" + Environment.NewLine;
            outtext += $"Avg Cy= {kmitemmaster.AvgC2}" + Environment.NewLine;

            outtext += Environment.NewLine;
            outtext += $"Pass/Fail Criteria:" + Environment.NewLine;
            outtext += $"NbrFail Points={kmitemmaster.NbrFailPoints}" + Environment.NewLine;
            outtext += $"Avg Lv={kmitemmaster.AvgLv}" + Environment.NewLine;
            outtext += $"Lv Uniformity={kmitemmaster.LvUniformity}" + Environment.NewLine;
            outtext += $"Color Uniformity={kmitemmaster.LvUniformity}" + Environment.NewLine;

            outtext += kmitemmaster.Result ? "Pass" : "Fail" + Environment.NewLine;
            outputText.Background = kmitemmaster.Result ? Brushes.Lime : Brushes.Red;
            outputText.Text = outtext;
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
            outputText.Text = string.Empty;
            outputText.Background = Brushes.White;
        }

        private void listView1_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex >-1)
            {
                var kBItem = ViewResluts[listView.SelectedIndex];
                GenoutputText(kBItem);
                Task.Run(async () =>
                {
                    await Task.Delay(30);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (File.Exists(kBItem.ResultImagFile))
                        {
                            ImageView.OpenImage(kBItem.ResultImagFile);
                            ImageView.ImageShow.Clear();
                            foreach (var item in kBItem.Items)
                            {
                                DVRectangle Rectangle = new();
                                Rectangle.Attribute.Rect = new Rect(item.KBKeyRect.X, item.KBKeyRect.Y, item.KBKeyRect.Width, item.KBKeyRect.Height);

                                if (item.Name == kBItem.DrakestKey)
                                {
                                    Rectangle.Attribute.Pen = new Pen(Brushes.Violet, 10);
                                }
                                else if (item.Name == kBItem.BrightestKey)
                                {
                                    Rectangle.Attribute.Pen = new Pen(Brushes.White, 10);
                                }
                                else
                                {
                                    Rectangle.Attribute.Pen = new Pen(Brushes.Red, 10);
                                }

                                Rectangle.Attribute.Brush = Brushes.Transparent;
                                Rectangle.Attribute.Id = -1;
                                Rectangle.Render();
                                ImageView.AddVisual(Rectangle);
                            }
                        }
                    });
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