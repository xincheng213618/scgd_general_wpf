using ColorVision.Common.MVVM;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Jsons.LargeFlow;
using ColorVision.Engine.Templates.MTF;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor.Draw;
using ColorVision.Themes;
using CVCommCore.CVAlgorithm;
using FlowEngineLib;
using FlowEngineLib.Base;
using log4net;
using Panuon.WPF.UI;
using ProjectARVR.Config;
using ProjectARVR.Services;
using ST.Library.UI.NodeEditor;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ProjectARVR
{
    public enum ARVRTestType
    {
        White =1,
        Black =2,
        DistortionGhost =4,
        MTFH =5,
        MTFV =6,
    }


    public class ProjectARVRReuslt:ViewModelBase
    {
        public int Id { get; set; }
        public string Model { get; set; }
        public DateTime CreateTime { get; set; } = DateTime.Now;

        public string FileName { get; set; }

        public string SN { get; set; }

        public bool Result { get; set; } = true;

        public ARVRTestType TestType { get; set; }

        public List<ViewResultMTF> MTFHs { get; set; } = new List<ViewResultMTF>();
        public List<ViewResultMTF> MTFvs { get; set; } = new List<ViewResultMTF>();
    }

    public partial class ARVRWindow : Window,IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ARVRWindow));
        public static ARVRWindowConfig Config => ARVRWindowConfig.Instance;

        public ObservableCollection<ProjectARVRReuslt> ViewResluts { get; set; } = new ObservableCollection<ProjectARVRReuslt>();

        public ARVRWindow()
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
            this.DataContext = ProjectARVRConfig.Instance;

            ImageView.SetConfig(ProjectARVRConfig.Instance.ImageViewConfig);

            MQTTConfig mQTTConfig = MQTTSetting.Instance.MQTTConfig;
            MQTTHelper.SetDefaultCfg(mQTTConfig.Host, mQTTConfig.Port, mQTTConfig.UserName, mQTTConfig.UserPwd, false, null);
            flowEngine = new FlowEngineControl(false);
            ImageView.Config.IsLayoutUpdated = false;
            STNodeEditorMain = new STNodeEditor();
            STNodeEditorMain.LoadAssembly("FlowEngineLib.dll");
            flowEngine.AttachNodeEditor(STNodeEditorMain);

            FlowTemplate.SelectionChanged += (s, e) =>
            {
                if (ProjectARVRConfig.Instance.TemplateSelectedIndex > -1)
                {
                    string Name = TemplateFlow.Params[ProjectARVRConfig.Instance.TemplateSelectedIndex].Key;
                    if (ProjectARVRConfig.Instance.SPECConfigs.TryGetValue(Name,out SPECConfig sPECConfig))
                    {
                        ProjectARVRConfig.Instance.SPECConfig = sPECConfig;
                    }
                    else
                    {
                        sPECConfig = new SPECConfig();
                        ProjectARVRConfig.Instance.SPECConfigs.TryAdd(Name, sPECConfig);
                        ProjectARVRConfig.Instance.SPECConfig = sPECConfig;
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

                LogOutput1?.Dispose();
            };
            listView1.ItemsSource = ViewResluts;

        }
        private void ServicesChanged(object? sender, EventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                log.Info("Service触发拍照，执行流程");
                RunTemplate();
            });
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
                        if (ProjectARVRConfig.Instance.LastFlowTime == 0 || ProjectARVRConfig.Instance.LastFlowTime - elapsedMilliseconds < 0)
                        {
                            msg = Msg1 + Environment.NewLine + $"已经执行：{elapsedTime}";
                        }
                        else
                        {
                            long remainingMilliseconds = ProjectARVRConfig.Instance.LastFlowTime - elapsedMilliseconds;
                            TimeSpan remaining = TimeSpan.FromMilliseconds(remainingMilliseconds);
                            string remainingTime = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}:{elapsed.Milliseconds:D4}";

                            msg = Msg1 + Environment.NewLine + $"已经执行：{elapsedTime}, 上次执行：{ProjectARVRConfig.Instance.LastFlowTime} ms, 预计还需要：{remainingTime}";
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
            if (SocketControl.Current.Stream != null)
            {
                byte[] response1 = Encoding.ASCII.GetBytes($"Run 5555");
                SocketControl.Current.Stream.Write(response1, 0, response1.Length);
            }

            
            RunTemplate();
        }
        private void LargeTest_Click(object sender, RoutedEventArgs e)
        {
            if (CBLargeTemplate.SelectedValue is TJLargeFlowParam jLargeFlowParam)
            {
                var ListFlows = jLargeFlowParam.GetFlows();
                if (ListFlows.Count == 0)
                {
                    log.Info("大流程没有配置距离的模板");
                }
                foreach (var item in ListFlows.Reverse())
                {
                    LargetStack.Push(item);
                }
                if (LargetStack.Count != 0)
                {
                    FlowTemplate.SelectedValue = LargetStack.Pop().Value; ;
                    RunTemplate();
                }

            }
        }

        Stack<TemplateModel<FlowParam>> LargetStack = new Stack<TemplateModel<FlowParam>>();

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
            LargetStack.Clear();
        }

        private int id;
        private FlowControl flowControl;

        private void FlowControl_FlowCompleted(object? sender, EventArgs e)
        {
            id++;
            flowControl.FlowCompleted -= FlowControl_FlowCompleted;
            handler?.Close();
            handler = null;
            stopwatch.Stop();
            timer.Change(Timeout.Infinite, 500); // 停止定时器
            ProjectARVRConfig.Instance.LastFlowTime = stopwatch.ElapsedMilliseconds;
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
                            Application.Current.Dispatcher.BeginInvoke(() =>
                            {
                                Processing(FlowControlData.SerialNumber);
                            });
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message);
                        }
                        Task.Run(async () =>
                        {
                            await Task.Delay(100);
                            Application.Current.Dispatcher.BeginInvoke(() =>
                            {
                                if (LargetStack.Count != 0)
                                {
                                    FlowTemplate.SelectedValue = LargetStack.Pop().Value; ;
                                    RunTemplate();
                                }
                            });
                        });

                    }
                    else
                    {
                        LargetStack.Clear();
                        MessageBox.Show(Application.Current.GetActiveWindow(), "流程运行失败" + FlowControlData.EventName + Environment.NewLine + FlowControlData.Params, "ColorVision");
                    }
                }
                else
                {
                    LargetStack.Clear();
                    MessageBox.Show(Application.Current.GetActiveWindow(), "流程运行失败" + FlowControlData.EventName + Environment.NewLine + FlowControlData.Params, "ColorVision");
                }

            }
            else
            {
                LargetStack.Clear();
                MessageBox.Show(Application.Current.GetActiveWindow(), "流程运行异常", "ColorVision");
            }
        }

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

            ProjectARVRReuslt result = new ProjectARVRReuslt();
            result.Model = FlowTemplate.Text;
            result.Id = Batch.Id;
            result.SN = SNtextBox.Text;

            var values = MeasureImgResultDao.Instance.GetAllByBatchId(Batch.Id);

            if (values.Count > 0)
            {
                result.FileName = values[0].FileUrl;
            }

            foreach (var item in AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id))
            {
                if (result.Model.Contains("White"))
                {
                    result.TestType = ARVRTestType.White;
                    List<PoiPointResultModel> AlgResultMTFModels = PoiPointResultDao.Instance.GetAllByPid(item.Id);
                    log.Debug($"AlgResultMTFModels Count={AlgResultMTFModels.Count} for {item.Id}");
                    foreach (var poiResultDat1a in AlgResultMTFModels)
                    {
                        ViewResultMTF poiResultData = new(poiResultDat1a);
                        result.MTFHs.Add(poiResultData);
                    }
                }
                if (result.Model.Contains("Black"))
                {
                    result.TestType = ARVRTestType.Black;
                    List<PoiPointResultModel> AlgResultMTFModels = PoiPointResultDao.Instance.GetAllByPid(item.Id);
                    log.Debug($"AlgResultMTFModels Count={AlgResultMTFModels.Count} for {item.Id}");
                    foreach (var poiResultDat1a in AlgResultMTFModels)
                    {
                        ViewResultMTF poiResultData = new(poiResultDat1a);
                        result.MTFHs.Add(poiResultData);
                    }
                }
                if (result.Model.Contains("Ghost"))
                {
                    result.TestType = ARVRTestType.DistortionGhost;
                    List<PoiPointResultModel> AlgResultMTFModels = PoiPointResultDao.Instance.GetAllByPid(item.Id);
                    log.Debug($"AlgResultMTFModels Count={AlgResultMTFModels.Count} for {item.Id}");
                    foreach (var poiResultDat1a in AlgResultMTFModels)
                    {
                        ViewResultMTF poiResultData = new(poiResultDat1a);
                        result.MTFHs.Add(poiResultData);
                    }
                }

                if (result.Model.Contains("MTF_H"))
                {
                    result.TestType = ARVRTestType.MTFH;
                    List<PoiPointResultModel> AlgResultMTFModels = PoiPointResultDao.Instance.GetAllByPid(item.Id);
                    log.Debug($"AlgResultMTFModels Count={AlgResultMTFModels.Count} for {item.Id}");
                    foreach (var poiResultDat1a in AlgResultMTFModels)
                    {
                        ViewResultMTF poiResultData = new(poiResultDat1a);
                        result.MTFHs.Add(poiResultData);
                    }
                }

                if (result.Model.Contains("MTF_V"))
                {
                    result.TestType = ARVRTestType.MTFV;
                    List<PoiPointResultModel> AlgResultMTFModels = PoiPointResultDao.Instance.GetAllByPid(item.Id);
                    log.Debug($"AlgResultMTFModels Count={AlgResultMTFModels.Count} for {item.Id}");
                    foreach (var poiResultDat1a in AlgResultMTFModels)
                    {
                        ViewResultMTF poiResultData = new(poiResultDat1a);
                        result.MTFvs.Add(poiResultData);
                    }

                }
            }

            SNtextBox.Text = string.Empty;
            ViewResluts.Add(result);
            listView1.SelectedIndex = ViewResluts.Count - 1;
        }



        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            ProjectARVRConfig.Instance.Height = row2.ActualHeight;
            row2.Height = GridLength.Auto;
        }

        private void Button_Click_Clear(object sender, RoutedEventArgs e)
        {
            ImageView.Clear();
            outputText.Document.Blocks.Clear();
            outputText.Background = Brushes.White;
            NGResult.Text = string.Empty;
        }

        private void listView1_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex >-1)
            {
                var result = ViewResluts[listView.SelectedIndex];
                GenoutputText(result);

                Task.Run(async () =>
                {
                    if (File.Exists(result.FileName))
                    {
                        try
                        {
                            var fileInfo = new FileInfo(result.FileName);
                            log.Debug($"fileInfo.Length{fileInfo.Length}");
                            using (var fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                            {
                                log.Debug("文件可以读取，没有被占用。");
                            }
                            if (fileInfo.Length > 0)
                            {
                                OpenImage(result);
                            }
                        }
                        catch
                        {
                            log.Debug("文件还在写入");
                            await Task.Delay(ProjectARVRConfig.Instance.ViewImageReadDelay);
                            OpenImage(result);
                        }
                    }
                });

            }
        }

        public void OpenImage(ProjectARVRReuslt result)
        {
            _ = Application.Current.Dispatcher.BeginInvoke(() =>
            {
                ImageView.OpenImage(result.FileName);
                ImageView.ImageShow.Clear();
                if (result.TestType == ARVRTestType.MTFH)
                {
                    foreach (var poiResultData in result.MTFHs)
                    {
                        switch (poiResultData.Point.PointType)
                        {
                            case POIPointTypes.Circle:
                                DVCircleText Circle = new();
                                Circle.Attribute.Center = new Point(poiResultData.Point.PixelX, poiResultData.Point.PixelY);
                                Circle.Attribute.Radius = poiResultData.Point.Height / 2;
                                Circle.Attribute.Brush = Brushes.Transparent;
                                Circle.Attribute.Pen = new Pen(Brushes.Red, 1);
                                Circle.Attribute.Id = poiResultData.Id;
                                Circle.Attribute.Text = poiResultData.Name;
                                Circle.Attribute.Msg = poiResultData.Articulation.ToString();
                                Circle.Render();
                                ImageView.AddVisual(Circle);
                                break;
                            case POIPointTypes.Rect:
                                DVRectangleText Rectangle = new();
                                Rectangle.Attribute.Rect = new Rect(poiResultData.Point.PixelX - poiResultData.Point.Width / 2, poiResultData.Point.PixelY - poiResultData.Point.Height / 2, poiResultData.Point.Width, poiResultData.Point.Height);
                                Rectangle.Attribute.Brush = Brushes.Transparent;
                                Rectangle.Attribute.Pen = new Pen(Brushes.Red, 1);
                                Rectangle.Attribute.Id = poiResultData.Id;
                                Rectangle.Attribute.Text = poiResultData.Name;
                                Rectangle.Attribute.Msg = poiResultData.Articulation.ToString();
                                Rectangle.Render();
                                ImageView.AddVisual(Rectangle);
                                break;
                            default:
                                break;
                        }
                    }
                }
                if (result.TestType == ARVRTestType.MTFV)
                {
                    foreach (var poiResultData in result.MTFvs)
                    {
                        switch (poiResultData.Point.PointType)
                        {
                            case POIPointTypes.Circle:
                                DVCircleText Circle = new();
                                Circle.Attribute.Center = new Point(poiResultData.Point.PixelX, poiResultData.Point.PixelY);
                                Circle.Attribute.Radius = poiResultData.Point.Height / 2;
                                Circle.Attribute.Brush = Brushes.Transparent;
                                Circle.Attribute.Pen = new Pen(Brushes.Red, 1);
                                Circle.Attribute.Id = poiResultData.Id;
                                Circle.Attribute.Text = poiResultData.Name;
                                Circle.Attribute.Msg = poiResultData.Articulation.ToString();
                                Circle.Render();
                                ImageView.AddVisual(Circle);
                                break;
                            case POIPointTypes.Rect:
                                DVRectangleText Rectangle = new();
                                Rectangle.Attribute.Rect = new Rect(poiResultData.Point.PixelX - poiResultData.Point.Width / 2, poiResultData.Point.PixelY - poiResultData.Point.Height / 2, poiResultData.Point.Width, poiResultData.Point.Height);
                                Rectangle.Attribute.Brush = Brushes.Transparent;
                                Rectangle.Attribute.Pen = new Pen(Brushes.Red, 1);
                                Rectangle.Attribute.Id = poiResultData.Id;
                                Rectangle.Attribute.Text = poiResultData.Name;
                                Rectangle.Attribute.Msg = poiResultData.Articulation.ToString();
                                Rectangle.Render();
                                ImageView.AddVisual(Rectangle);
                                break;
                            default:
                                break;
                        }
                    }


                }


            });

        }

        public void GenoutputText(ProjectARVRReuslt result)
        {

            outputText.Background = result.Result ? Brushes.Lime : Brushes.Red;
            outputText.Document.Blocks.Clear(); // 清除之前的内容

            string outtext = string.Empty;
            outtext += $"Model:{result.Model}" + Environment.NewLine;
            outtext += $"SN:{result.SN}" + Environment.NewLine;
            outtext += $"{DateTime.Now:yyyy/MM//dd HH:mm:ss}" + Environment.NewLine;

            Run run = new Run(outtext);
            run.Foreground = result.Result ? Brushes.Black : Brushes.White;
            run.FontSize += 1;



            var paragraph = new Paragraph();
            paragraph.Inlines.Add(run);

            outputText.Document.Blocks.Add(paragraph);
            outtext = string.Empty;

            paragraph = new Paragraph();

            outtext = string.Empty;

            outputText.Document.Blocks.Add(paragraph);

            switch (result.TestType)
            {
                case ARVRTestType.White:
                    outtext += $"白画面 测试项：自动AA区域定位算法+关注点算法+FOV算法+亮度均匀性+颜色均匀性算法+" + Environment.NewLine;
                    break;
                case ARVRTestType.Black:
                    outtext += $"黑画面 测试项：自动AA区域定位算法+关注点算法+序列对比度算法(中心亮度比值)" + Environment.NewLine;
                    break;
                case ARVRTestType.MTFH:
                    outtext += $"水平MTF 测试项：自动AA区域定位算法+关注点+MTF算法" + Environment.NewLine;
                    break;
                case ARVRTestType.MTFV:
                    outtext += $"垂直MTF 测试项：自动AA区域定位算法+关注点+MTF算法" + Environment.NewLine;
                    break;
                case ARVRTestType.DistortionGhost:
                    outtext += $"黑画面 测试项：自动AA区域定位算法+畸变算法+鬼影算法" + Environment.NewLine;
                    break;
                default:
                    break;
            }

            //outtext += $"Min Lv= {result.MinLv:F2} cd/m2" + Environment.NewLine;
            //outtext += $"Max Lv= {result.MaxLv:F2} cd/m2" + Environment.NewLine;
            //outtext += $"Darkest Key= {result.DrakestKey}" + Environment.NewLine;
            //outtext += $"Brightest Key= {result.BrightestKey}" + Environment.NewLine;

            outtext += Environment.NewLine;
            outtext += $"Pass/Fail Criteria:" + Environment.NewLine;


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


        private void GridSplitter_DragCompleted1(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            ProjectARVRConfig.Instance.SummaryInfo.Width = col1.ActualWidth;
            col1.Width = GridLength.Auto;
        }

        public void Dispose()
        {
            timer?.Dispose();
            GC.SuppressFinalize(this);
        }


    }
}