#pragma warning disable
using ColorVision.Common.Algorithms;
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine;
using ColorVision.Engine.Media;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Services.Types;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.FindLightArea;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.BinocularFusion;
using ColorVision.Engine.Templates.Jsons.BlackMura;
using ColorVision.Engine.Templates.Jsons.FOV2;
using ColorVision.Engine.Templates.Jsons.LargeFlow;
using ColorVision.Engine.Templates.Jsons.MTF2;
using ColorVision.Engine.Templates.Jsons.PoiAnalysis;
using ColorVision.Engine.Templates.MTF;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.Engine.Templates.POI.Image;
using ColorVision.ImageEditor.Draw;
using ColorVision.SocketProtocol;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Extension;
using ColorVision.UI.LogImp;
using CVCommCore.CVAlgorithm;
using FlowEngineLib;
using FlowEngineLib.Base;
using LiveChartsCore.Kernel;
using log4net;
using log4net.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Asn1.Ocsp;
using ProjectARVR;
using ProjectARVR.Services;
using SqlSugar;
using ST.Library.UI.NodeEditor;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ProjectARVR
{

    public class SwitchPG
    {
        public ARVRTestType ARVRTestType { get; set; }
    }


    public class ARVRWindowConfig : WindowConfig
    {
        public static ARVRWindowConfig Instance => ConfigService.Instance.GetRequiredService<ARVRWindowConfig>();
    }

    public partial class ARVRWindow : Window, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ARVRWindow));
        public static ARVRWindowConfig Config => ARVRWindowConfig.Instance;

        public static ProjectARVRConfig ProjectConfig => ProjectARVRConfig.Instance;

        public static ViewResultManager ViewResultManager => ViewResultManager.GetInstance();

        public static ObservableCollection<ProjectARVRReuslt> ViewResluts { get; set; } = ViewResultManager.ViewResluts;

        public static ObjectiveTestResultFix ObjectiveTestResultFix => FixManager.GetInstance().ObjectiveTestResultFix;


        public ARVRWindow()
        {
            InitializeComponent();
            this.ApplyCaption(false);
            Config.SetWindow(this);
            this.Title += "-" + Assembly.GetAssembly(typeof(ARVRWindow))?.GetName().Version?.ToString() ?? "";
        }

        public ARVRTestType CurrentTestType = ARVRTestType.None;
        ObjectiveTestResult ObjectiveTestResult { get; set; } = new ObjectiveTestResult();


        Random Random = new Random();
        public void InitTest(string SN)
        {
            ProjectARVRConfig.Instance.StepIndex = 0;
            ObjectiveTestResult = new ObjectiveTestResult();
            CurrentTestType = ARVRTestType.None;
            if (string.IsNullOrWhiteSpace(SN))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProjectARVRConfig.Instance.SN = "SN" + Random.NextInt64(1000, 9000).ToString();
                });
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProjectARVRConfig.Instance.SN = "SN" + Random.NextInt64(1000, 9000).ToString();
                });
            }
        }

        bool IsSwitchRun = false;
        public void SwitchPGCompleted()
        {
            if (IsSwitchRun)
            {
                log.Info("重复触发PG");
                return;
            }
            IsSwitchRun = true;

            if (flowControl.IsFlowRun)
            {
                log.Info("PG切换错误，正在执行流程");
                return;
            }
            var values = Enum.GetValues(typeof(ARVRTestType));
            int currentIndex = Array.IndexOf(values, CurrentTestType);
            int nextIndex = (currentIndex + 1) % values.Length;
            // 跳过 None（假设 None 是第一个）
            if ((ARVRTestType)values.GetValue(nextIndex) == ARVRTestType.None)
                nextIndex = (nextIndex + 1) % values.Length;
            var TestType = (ARVRTestType)values.GetValue(nextIndex);

            try
            {
                if (TestType == ARVRTestType.White2)
                {
                    ProjectARVRConfig.Instance.StepIndex = 1;
                    FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("WhiteFOV")).Value;
                    CurrentTestType = TestType;
                    RunTemplate();
                }
                if (TestType == ARVRTestType.White)
                {
                    ProjectARVRConfig.Instance.StepIndex = 2;
                    FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("White255")).Value;
                    CurrentTestType = TestType;
                    RunTemplate();
                }
                if (TestType == ARVRTestType.White1)
                {
                    ProjectARVRConfig.Instance.StepIndex = 3;
                    FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("White_calibrate")).Value;
                    CurrentTestType = TestType;
                    RunTemplate();
                }
                if (TestType == ARVRTestType.Black)
                {
                    ProjectARVRConfig.Instance.StepIndex = 4;
                    FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("Black")).Value;
                    CurrentTestType = TestType;
                    RunTemplate();
                }
                if (TestType == ARVRTestType.Chessboard)
                {
                    ProjectARVRConfig.Instance.StepIndex = 5;
                    FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("Chessboard")).Value;
                    CurrentTestType = TestType;
                    RunTemplate();
                }
                if (TestType == ARVRTestType.MTFH)
                {
                    ProjectARVRConfig.Instance.StepIndex = 6;
                    FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("MTF_H")).Value;
                    CurrentTestType = TestType;
                    RunTemplate();
                }

                if (TestType == ARVRTestType.MTFV)
                {
                    ProjectARVRConfig.Instance.StepIndex = 7;
                    FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("MTF_V")).Value;
                    CurrentTestType = TestType;
                    RunTemplate();
                }

                if (TestType == ARVRTestType.Distortion)
                {
                    ProjectARVRConfig.Instance.StepIndex = 8;
                    FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("Distortion")).Value;
                    CurrentTestType = TestType;
                    RunTemplate();
                }
                if (TestType == ARVRTestType.OpticCenter)
                {
                    ProjectARVRConfig.Instance.StepIndex = 9;
                    FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("OpticCenter")).Value;
                    CurrentTestType = TestType;
                    RunTemplate();
                }
            }
            catch(Exception ex)
            {
                log.Error(ex);
            }

            IsSwitchRun = false;
        }

        public STNodeEditor STNodeEditorMain { get; set; }
        private FlowEngineControl flowEngine;
        private Timer timer;
        Stopwatch stopwatch = new Stopwatch();

        public static RecipeManager RecipeManager => RecipeManager.GetInstance();
        public static ARVRRecipeConfig SPECConfig => RecipeManager.RecipeConfig;
        LogOutput logOutput;
        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = ProjectARVRConfig.Instance;

            MQTTConfig mQTTConfig = MQTTSetting.Instance.MQTTConfig;
            MQTTHelper.SetDefaultCfg(mQTTConfig.Host, mQTTConfig.Port, mQTTConfig.UserName, mQTTConfig.UserPwd, false, null);
            flowEngine = new FlowEngineControl(false);
            STNodeEditorMain = new STNodeEditor();
            STNodeEditorMain.LoadAssembly("FlowEngineLib.dll");
            flowEngine.AttachNodeEditor(STNodeEditorMain);
            flowControl = new FlowControl(MQTTControl.GetInstance(), flowEngine);

            string Name = "Default";
            if (RecipeManager.RecipeConfigs.TryGetValue(Name, out ARVRRecipeConfig recipeConfig))
            {
                RecipeManager.RecipeConfig = recipeConfig;
            }
            else
            {
                recipeConfig = new ARVRRecipeConfig();
                RecipeManager.RecipeConfigs.TryAdd(Name, recipeConfig);
                RecipeManager.RecipeConfig = recipeConfig;
                RecipeManager.Save();
            }


            timer = new Timer(TimeRun, null, 0, 500);
            timer.Change(Timeout.Infinite, 500); // 停止定时器

            if (ProjectARVRConfig.Instance.LogControlVisibility)
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
            var item = listView1.SelectedItem as ProjectARVRReuslt;
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


        ProjectARVRReuslt CurrentFlowResult { get; set; }
        int TryCount = 0;

        public async Task RunTemplate()
        {
            if (flowControl != null && flowControl.IsFlowRun) return;

            TryCount++;
            LastFlowTime = FlowEngineConfig.Instance.FlowRunTime.TryGetValue(FlowTemplate.Text, out long time) ? time : 0;

            CurrentFlowResult = new ProjectARVRReuslt();
            CurrentFlowResult.SN = ProjectARVRConfig.Instance.SN;
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
                    //如果没有执行完，先切换PG，并且提前设置流程
                    if (!IsTestTypeCompleted())
                    {
                        SwitchPG();
                    }

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

                if (TryCount < ProjectARVRConfig.Instance.TryCountMax)
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
                    ObjectiveTestResult.TotalResult = false;
                    var response = new SocketResponse
                    {
                        Version = "1.0",
                        MsgID = "",
                        EventName = "ProjectARVRResult",
                        Code = -2,
                        Msg = "ARVR Test OverTime",
                        Data = ObjectiveTestResult
                    };
                    string respString = JsonConvert.SerializeObject(response);
                    log.Info(respString);
                    SocketControl.Current.Stream.Write(Encoding.UTF8.GetBytes(respString));
                }
                TryCount = 0;
            }
            else
            {
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

                if (ProjectConfig.AllowTestFailures)
                {
                    //如果允许失败，则切换PG，并且提前设置流程,执行结束时直接发送结束
                    if (!IsTestTypeCompleted())
                    {
                        SwitchPG();
                    }
                    else
                    {
                        TestCompleted();
                    }
                }
                else
                {
                    if (SocketManager.GetInstance().TcpClients.Count > 0 && SocketControl.Current.Stream != null)
                    {
                        ObjectiveTestResult.TotalResult = false;
                        var response = new SocketResponse
                        {
                            Version = "1.0",
                            MsgID = "",
                            EventName = "ProjectARVRResult",
                            Code = -1,
                            Msg = "ARVR Test Fail",
                            Data = ObjectiveTestResult
                        };
                        string respString = JsonConvert.SerializeObject(response);
                        log.Info(respString);
                        SocketControl.Current.Stream.Write(Encoding.UTF8.GetBytes(respString));
                    }
                }
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
            ProjectARVRReuslt result = CurrentFlowResult ?? new ProjectARVRReuslt();

            result.BatchId = Batch.Id;
            result.FlowStatus = FlowStatus.Completed;
            result.CreateTime = DateTime.Now;
            result.Result = true;

            if (result.Model.Contains("White_calibrate"))
            {
                log.Info("正在解析白画面的流程");
                result.TestType = ARVRTestType.White1;
                var values = MeasureImgResultDao.Instance.GetAllByBatchId(Batch.Id);
                if (values.Count > 0)
                {
                    result.FileName = values[0].FileUrl;
                }
                var AlgResultMasterlists = AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id);
                log.Info($"AlgResultMasterlists count {AlgResultMasterlists.Count}");
                foreach (var AlgResultMaster in AlgResultMasterlists)
                {
                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.ViewResultAlgType.POI_XYZ)
                    {
                        List<PoiPointResultModel> POIPointResultModels = PoiPointResultDao.Instance.GetAllByPid(AlgResultMaster.Id);
                        int id = 0;
                        foreach (var item in POIPointResultModels)
                        {
                            PoiResultCIExyuvData poiResultCIExyuvData = new PoiResultCIExyuvData(item) { Id = id++ };

                            if (item.PoiName == "P_1")
                            {
                                var White1CenterCorrelatedColorTemperature = new ObjectiveTestItem()
                                {
                                    Name = "White1CenterCorrelatedColorTemperature",
                                    TestValue = poiResultCIExyuvData.CCT.ToString(),
                                    Value = poiResultCIExyuvData.CCT,
                                    LowLimit = SPECConfig.White1CenterCorrelatedColorTemperatureMin,
                                    UpLimit = SPECConfig.White1CenterCorrelatedColorTemperatureMax
                                };
                                ObjectiveTestResult.White1CenterCorrelatedColorTemperature = White1CenterCorrelatedColorTemperature;
                                result.Result = result.Result && White1CenterCorrelatedColorTemperature.TestResult;

                                var White1CenterLuminace = new ObjectiveTestItem()
                                {
                                    Name = "White1CenterLuminace",
                                    TestValue = poiResultCIExyuvData.Y.ToString(),
                                    Value = poiResultCIExyuvData.Y,
                                    LowLimit = SPECConfig.White1CenterLuminaceMin,
                                    UpLimit = SPECConfig.White1CenterLuminaceMax
                                };
                                ObjectiveTestResult.White1CenterLuminace = White1CenterLuminace;
                                result.Result = result.Result && White1CenterLuminace.TestResult;
                            }
                        }
                    }
                }
            }

            if (result.Model.Contains("WhiteFOV"))
            {
                log.Info("正在解析白画面的流程");
                result.TestType = ARVRTestType.White;
                ObjectiveTestResult.FlowWhiteTestReslut = true;

                var values = MeasureImgResultDao.Instance.GetAllByBatchId(Batch.Id);
                if (values.Count > 0)
                {
                    result.FileName = values[0].FileUrl;
                }
                var AlgResultMasterlists = AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id);
                log.Info($"AlgResultMasterlists count {AlgResultMasterlists.Count}");
                foreach (var AlgResultMaster in AlgResultMasterlists)
                {
                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.ViewResultAlgType.FindLightArea)
                    {
                        result.ViewResultWhite.AlgResultLightAreaModels = AlgResultLightAreaDao.Instance.GetAllByPid(AlgResultMaster.Id);
                    }

                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.ViewResultAlgType.PoiAnalysis)
                    {
                        if (AlgResultMaster.TName.Contains("Luminance_uniformity"))
                        {
                            List<DetailCommonModel> detailCommonModels = DeatilCommonDao.Instance.GetAllByPid(AlgResultMaster.Id);
                            if (detailCommonModels.Count == 1)
                            {
                                PoiAnalysisDetailViewReslut viewReslut = new PoiAnalysisDetailViewReslut(detailCommonModels[0]);

                                viewReslut.PoiAnalysisResult.result.Value = viewReslut.PoiAnalysisResult.result.Value * ObjectiveTestResultFix.LuminanceUniformity;
                                var LuminanceUniformity = new ObjectiveTestItem()
                                {
                                    Name = "Luminance_uniformity(%)",
                                    TestValue = (viewReslut.PoiAnalysisResult.result.Value * 100).ToString("F3") + "%",
                                    Value = viewReslut.PoiAnalysisResult.result.Value,
                                    LowLimit = SPECConfig.LuminanceUniformityMin,
                                    UpLimit = SPECConfig.LuminanceUniformityMax,
                                };
                                ObjectiveTestResult.LuminanceUniformity = LuminanceUniformity;
                                result.ViewResultWhite.LuminanceUniformity = LuminanceUniformity;

                                result.Result = result.Result && LuminanceUniformity.TestResult;

                            }

                        }
                        if (AlgResultMaster.TName.Contains("Color_uniformity"))
                        {
                            List<DetailCommonModel> detailCommonModels = DeatilCommonDao.Instance.GetAllByPid(AlgResultMaster.Id);
                            if (detailCommonModels.Count == 1)
                            {
                                PoiAnalysisDetailViewReslut viewReslut = new PoiAnalysisDetailViewReslut(detailCommonModels[0]);
                                viewReslut.PoiAnalysisResult.result.Value = viewReslut.PoiAnalysisResult.result.Value * ObjectiveTestResultFix.ColorUniformity;

                                var ColorUniformity = new ObjectiveTestItem()
                                {
                                    Name = "Color_uniformity",
                                    TestValue = (viewReslut.PoiAnalysisResult.result.Value).ToString("F5"),
                                    Value = viewReslut.PoiAnalysisResult.result.Value,
                                    LowLimit = SPECConfig.ColorUniformityMin,
                                    UpLimit = SPECConfig.ColorUniformityMax
                                };
                                ObjectiveTestResult.ColorUniformity = ColorUniformity;
                                result.ViewResultWhite.ColorUniformity = ColorUniformity;

                                result.Result = result.Result && ColorUniformity.TestResult;

                            }
                        }
                    }

                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.ViewResultAlgType.FOV)
                    {
                        List<DetailCommonModel> AlgResultModels = DeatilCommonDao.Instance.GetAllByPid(AlgResultMaster.Id);
                        if (AlgResultModels.Count == 1)
                        {
                            DFovView view1 = new DFovView(AlgResultModels[0]);
                            view1.Result.result.D_Fov = view1.Result.result.D_Fov * ObjectiveTestResultFix.DiagonalFieldOfViewAngle;
                            view1.Result.result.ClolorVisionH_Fov = view1.Result.result.H_Fov * ObjectiveTestResultFix.HorizontalFieldOfViewAngle;
                            view1.Result.result.ClolorVisionV_Fov = view1.Result.result.V_FOV * ObjectiveTestResultFix.VerticalFieldOfViewAngle;

                            result.ViewResultWhite.DFovView = view1;



                            ObjectiveTestResult.DiagonalFieldOfViewAngle = new ObjectiveTestItem()
                            {
                                Name = "DiagonalFieldOfViewAngle",
                                LowLimit = SPECConfig.DiagonalFieldOfViewAngleMin,
                                UpLimit = SPECConfig.DiagonalFieldOfViewAngleMax,
                                Value = view1.Result.result.D_Fov,
                                TestValue = view1.Result.result.D_Fov.ToString("F3")
                            };

                            ObjectiveTestResult.HorizontalFieldOfViewAngle = new ObjectiveTestItem()
                            {
                                Name = "HorizontalFieldOfViewAngle",
                                LowLimit = SPECConfig.HorizontalFieldOfViewAngleMin,
                                UpLimit = SPECConfig.HorizontalFieldOfViewAngleMax,
                                Value = view1.Result.result.ClolorVisionH_Fov,
                                TestValue = view1.Result.result.ClolorVisionH_Fov.ToString("F3")
                            };
                            ObjectiveTestResult.VerticalFieldOfViewAngle = new ObjectiveTestItem()
                            {
                                Name = "VerticalFieldOfViewAngle",
                                LowLimit = SPECConfig.VerticalFieldOfViewAngleMin,
                                UpLimit = SPECConfig.VerticalFieldOfViewAngleMax,
                                Value = view1.Result.result.ClolorVisionV_Fov,
                                TestValue = view1.Result.result.ClolorVisionV_Fov.ToString("F3")
                            };
                            result.ViewResultWhite.DiagonalFieldOfViewAngle = ObjectiveTestResult.DiagonalFieldOfViewAngle;
                            result.ViewResultWhite.HorizontalFieldOfViewAngle = ObjectiveTestResult.HorizontalFieldOfViewAngle;
                            result.ViewResultWhite.VerticalFieldOfViewAngle = ObjectiveTestResult.VerticalFieldOfViewAngle;


                            result.Result = result.Result && ObjectiveTestResult.DiagonalFieldOfViewAngle.TestResult;
                            result.Result = result.Result && ObjectiveTestResult.HorizontalFieldOfViewAngle.TestResult;
                            result.Result = result.Result && ObjectiveTestResult.VerticalFieldOfViewAngle.TestResult;
                        }

                    }
                }
            }

            if (result.Model.Contains("White255"))
            {
                log.Info("正在解析白画面的流程");
                result.TestType = ARVRTestType.White;
                ObjectiveTestResult.FlowWhiteTestReslut = true;

                var values = MeasureImgResultDao.Instance.GetAllByBatchId(Batch.Id);
                if (values.Count > 0)
                {
                    result.FileName = values[0].FileUrl;
                }
                var AlgResultMasterlists = AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id);
                log.Info($"AlgResultMasterlists count {AlgResultMasterlists.Count}");
                foreach (var AlgResultMaster in AlgResultMasterlists)
                {
                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.ViewResultAlgType.POI_XYZ)
                    {
                        result.ViewResultWhite.PoiResultCIExyuvDatas = new List<PoiResultCIExyuvData>();

                        List<PoiPointResultModel> POIPointResultModels = PoiPointResultDao.Instance.GetAllByPid(AlgResultMaster.Id);
                        int id = 0;
                        foreach (var item in POIPointResultModels)
                        {
                            PoiResultCIExyuvData poiResultCIExyuvData = new(item) { Id = id++ };

                            if (item.PoiName == "POI_5")
                            {
                                poiResultCIExyuvData.CCT = poiResultCIExyuvData.CCT * ObjectiveTestResultFix.CenterCorrelatedColorTemperature;
                                poiResultCIExyuvData.Y = poiResultCIExyuvData.Y * ObjectiveTestResultFix.CenterLuminace;

                                var objectiveTestItem = new ObjectiveTestItem()
                                {
                                    Name = "CenterCorrelatedColorTemperature",
                                    TestValue = poiResultCIExyuvData.CCT.ToString(),
                                    Value = poiResultCIExyuvData.CCT,
                                    LowLimit = SPECConfig.CenterCorrelatedColorTemperatureMin,
                                    UpLimit = SPECConfig.CenterCorrelatedColorTemperatureMax
                                };
                                ObjectiveTestResult.CenterCorrelatedColorTemperature = objectiveTestItem;
                                result.ViewResultWhite.CenterCorrelatedColorTemperature = objectiveTestItem;
                                result.Result = result.Result && objectiveTestItem.TestResult;

                                var objectiveTestItem1 = new ObjectiveTestItem()
                                {
                                    Name = "CenterLuminace",
                                    TestValue = poiResultCIExyuvData.Y.ToString(),
                                    Value = poiResultCIExyuvData.Y,
                                    LowLimit = SPECConfig.CenterLuminaceMin,
                                    UpLimit = SPECConfig.CenterLuminaceMax
                                };
                                ObjectiveTestResult.CenterLuminace = objectiveTestItem1;

                                result.Result = result.Result && objectiveTestItem.TestResult;

                            }


                            result.ViewResultWhite.PoiResultCIExyuvDatas.Add(poiResultCIExyuvData);
                        }
                    }

                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.ViewResultAlgType.FindLightArea)
                    {
                        result.ViewResultWhite.AlgResultLightAreaModels = AlgResultLightAreaDao.Instance.GetAllByPid(AlgResultMaster.Id);
                    }

                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.ViewResultAlgType.PoiAnalysis)
                    {
                        if (AlgResultMaster.TName.Contains("Luminance_uniformity"))
                        {
                            List<DetailCommonModel> detailCommonModels = DeatilCommonDao.Instance.GetAllByPid(AlgResultMaster.Id);
                            if (detailCommonModels.Count == 1)
                            {
                                PoiAnalysisDetailViewReslut viewReslut = new PoiAnalysisDetailViewReslut(detailCommonModels[0]);

                                viewReslut.PoiAnalysisResult.result.Value = viewReslut.PoiAnalysisResult.result.Value * ObjectiveTestResultFix.LuminanceUniformity;
                                var LuminanceUniformity = new ObjectiveTestItem()
                                {
                                    Name = "Luminance_uniformity(%)",
                                    TestValue = (viewReslut.PoiAnalysisResult.result.Value * 100).ToString("F3") + "%",
                                    Value = viewReslut.PoiAnalysisResult.result.Value,
                                    LowLimit = SPECConfig.LuminanceUniformityMin,
                                    UpLimit = SPECConfig.LuminanceUniformityMax,
                                };
                                ObjectiveTestResult.LuminanceUniformity = LuminanceUniformity;
                                result.ViewResultWhite.LuminanceUniformity = LuminanceUniformity;

                                result.Result = result.Result && LuminanceUniformity.TestResult;

                            }

                        }
                        if (AlgResultMaster.TName.Contains("Color_uniformity"))
                        {
                            List<DetailCommonModel> detailCommonModels = DeatilCommonDao.Instance.GetAllByPid(AlgResultMaster.Id);
                            if (detailCommonModels.Count == 1)
                            {
                                PoiAnalysisDetailViewReslut viewReslut = new PoiAnalysisDetailViewReslut(detailCommonModels[0]);
                                viewReslut.PoiAnalysisResult.result.Value = viewReslut.PoiAnalysisResult.result.Value * ObjectiveTestResultFix.ColorUniformity;

                                var ColorUniformity = new ObjectiveTestItem()
                                {
                                    Name = "Color_uniformity",
                                    TestValue = (viewReslut.PoiAnalysisResult.result.Value).ToString("F5"),
                                    Value = viewReslut.PoiAnalysisResult.result.Value,
                                    LowLimit = SPECConfig.ColorUniformityMin,
                                    UpLimit = SPECConfig.ColorUniformityMax
                                };
                                ObjectiveTestResult.ColorUniformity = ColorUniformity;
                                result.ViewResultWhite.ColorUniformity = ColorUniformity;

                                result.Result = result.Result && ColorUniformity.TestResult;

                            }
                        }
                    }

                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.ViewResultAlgType.FOV)
                    {
                        List<DetailCommonModel> AlgResultModels = DeatilCommonDao.Instance.GetAllByPid(AlgResultMaster.Id);
                        if (AlgResultModels.Count == 1)
                        {
                            DFovView view1 = new DFovView(AlgResultModels[0]);
                            view1.Result.result.D_Fov = view1.Result.result.D_Fov * ObjectiveTestResultFix.DiagonalFieldOfViewAngle;
                            view1.Result.result.ClolorVisionH_Fov = view1.Result.result.H_Fov * ObjectiveTestResultFix.HorizontalFieldOfViewAngle;
                            view1.Result.result.ClolorVisionV_Fov = view1.Result.result.V_FOV * ObjectiveTestResultFix.VerticalFieldOfViewAngle;

                            result.ViewResultWhite.DFovView = view1;



                            ObjectiveTestResult.DiagonalFieldOfViewAngle = new ObjectiveTestItem()
                            {
                                Name = "DiagonalFieldOfViewAngle",
                                LowLimit = SPECConfig.DiagonalFieldOfViewAngleMin,
                                UpLimit = SPECConfig.DiagonalFieldOfViewAngleMax,
                                Value = view1.Result.result.D_Fov,
                                TestValue = view1.Result.result.D_Fov.ToString("F3")
                            };

                            ObjectiveTestResult.HorizontalFieldOfViewAngle = new ObjectiveTestItem()
                            {
                                Name = "HorizontalFieldOfViewAngle",
                                LowLimit = SPECConfig.HorizontalFieldOfViewAngleMin,
                                UpLimit = SPECConfig.HorizontalFieldOfViewAngleMax,
                                Value = view1.Result.result.ClolorVisionH_Fov,
                                TestValue = view1.Result.result.ClolorVisionH_Fov.ToString("F3")
                            };
                            ObjectiveTestResult.VerticalFieldOfViewAngle = new ObjectiveTestItem()
                            {
                                Name = "VerticalFieldOfViewAngle",
                                LowLimit = SPECConfig.VerticalFieldOfViewAngleMin,
                                UpLimit = SPECConfig.VerticalFieldOfViewAngleMax,
                                Value = view1.Result.result.ClolorVisionV_Fov,
                                TestValue = view1.Result.result.ClolorVisionV_Fov.ToString("F3")
                            };
                            result.ViewResultWhite.DiagonalFieldOfViewAngle = ObjectiveTestResult.DiagonalFieldOfViewAngle;
                            result.ViewResultWhite.HorizontalFieldOfViewAngle = ObjectiveTestResult.HorizontalFieldOfViewAngle;
                            result.ViewResultWhite.VerticalFieldOfViewAngle = ObjectiveTestResult.VerticalFieldOfViewAngle;


                            result.Result = result.Result && ObjectiveTestResult.DiagonalFieldOfViewAngle.TestResult;
                            result.Result = result.Result && ObjectiveTestResult.HorizontalFieldOfViewAngle.TestResult;
                            result.Result = result.Result && ObjectiveTestResult.VerticalFieldOfViewAngle.TestResult;
                        }

                    }
                }
            }
            else if (result.Model.Contains("Black"))
            {
                log.Info("正在解析黑画面的流程");
                result.TestType = ARVRTestType.Black;
                ObjectiveTestResult.FlowBlackTestReslut = true;

                if (ViewResluts.FirstOrDefault(a => a.SN == ProjectARVRConfig.Instance.SN) is ProjectARVRReuslt result1)
                {
                    result.ViewResultWhite = result1.ViewResultWhite;
                }

                var values = MeasureImgResultDao.Instance.GetAllByBatchId(Batch.Id);
                if (values.Count > 0)
                {
                    result.FileName = values[0].FileUrl;
                }
                var AlgResultMasterlists = AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id);

                foreach (var AlgResultMaster in AlgResultMasterlists)
                {
                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.ViewResultAlgType.POI_XYZ)
                    {
                        result.ViewResultBlack.PoiResultCIExyuvDatas = new List<PoiResultCIExyuvData>();

                        List<PoiPointResultModel> POIPointResultModels = PoiPointResultDao.Instance.GetAllByPid(AlgResultMaster.Id);
                        int id = 0;
                        foreach (var item in POIPointResultModels)
                        {
                            PoiResultCIExyuvData poiResultCIExyuvData = new(item) { Id = id++ };
                            result.ViewResultBlack.PoiResultCIExyuvDatas.Add(poiResultCIExyuvData);
                        }
                        try
                        {
                            if (result.ViewResultWhite != null && result.ViewResultWhite.PoiResultCIExyuvDatas.Count == 9 && result.ViewResultBlack.PoiResultCIExyuvDatas.Count == 1)
                            {
                                var contrast1 = result.ViewResultWhite.PoiResultCIExyuvDatas[5].Y / result.ViewResultBlack.PoiResultCIExyuvDatas[0].Y;

                                contrast1 = contrast1 * ObjectiveTestResultFix.FOFOContrast;

                                var FOFOContrast = new ObjectiveTestItem()
                                {
                                    Name = "FOFOContrast",
                                    LowLimit = SPECConfig.FOFOContrastMin,
                                    UpLimit = SPECConfig.FOFOContrastMax,
                                    Value = contrast1,
                                    TestValue = contrast1.ToString("F2")
                                };

                                ObjectiveTestResult.FOFOContrast = FOFOContrast;
                                result.ViewResultBlack.FOFOContrast = FOFOContrast;
                                result.Result = result.Result && FOFOContrast.TestResult;
                            }
                        }
                        catch (Exception ex)
                        {
                            log.Info($"找不到白画面对应的黑画面{ex}");
                        }

                    }
                }
            }
            else if (result.Model.Contains("Chessboard"))
            {
                log.Info("正在解析棋盘格画面的流程");
                result.TestType = ARVRTestType.Chessboard;
                ObjectiveTestResult.FlowChessboardTestReslut = true;

                var values = MeasureImgResultDao.Instance.GetAllByBatchId(Batch.Id);
                if (values.Count > 0)
                {
                    result.FileName = values[0].FileUrl;
                }
                var AlgResultMasterlists = AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id);

                foreach (var AlgResultMaster in AlgResultMasterlists)
                {
                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.ViewResultAlgType.POI_XYZ)
                    {
                        result.ViewReslutCheckerboard.PoiResultCIExyuvDatas = new ObservableCollection<PoiResultCIExyuvData>();

                        List<PoiPointResultModel> POIPointResultModels = PoiPointResultDao.Instance.GetAllByPid(AlgResultMaster.Id);
                        int id = 0;
                        foreach (var item in POIPointResultModels)
                        {
                            PoiResultCIExyuvData poiResultCIExyuvData = new(item) { Id = id++ };
                            result.ViewReslutCheckerboard.PoiResultCIExyuvDatas.Add(poiResultCIExyuvData);
                        }
                    }

                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.ViewResultAlgType.PoiAnalysis)
                    {
                        if (AlgResultMaster.TName.Contains("Chessboard_Contrast"))
                        {
                            List<DetailCommonModel> detailCommonModels = DeatilCommonDao.Instance.GetAllByPid(AlgResultMaster.Id);
                            if (detailCommonModels.Count == 1)
                            {
                                PoiAnalysisDetailViewReslut viewReslut = new PoiAnalysisDetailViewReslut(detailCommonModels[0]);
                                viewReslut.PoiAnalysisResult.result.Value = viewReslut.PoiAnalysisResult.result.Value * ObjectiveTestResultFix.ChessboardContrast;

                                var ChessboardContrast = viewReslut.PoiAnalysisResult.result.Value;

                                ObjectiveTestResult.ChessboardContrast = new ObjectiveTestItem()
                                {
                                    Name = "Chessboard_Contrast",
                                    LowLimit = SPECConfig.ChessboardContrastMin,
                                    UpLimit = SPECConfig.ChessboardContrastMax,
                                    Value = viewReslut.PoiAnalysisResult.result.Value,
                                    TestValue = ChessboardContrast.ToString("F2")
                                };

                                result.ViewReslutCheckerboard.ChessboardContrast = ObjectiveTestResult.ChessboardContrast;
                                result.Result = result.Result && ObjectiveTestResult.ChessboardContrast.TestResult;

                            }
                        }
                    }

                    //if(result.ViewReslutCheckerboard.PoiResultCIExyuvDatas != null)
                    //{
                    //    try
                    //    {
                    //        string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    //        string filePath = Path.Combine(ProjectARVRConfig.Instance.ResultSavePath, $"Chessboard_{timeStr}.csv");
                    //        PoiResultCIExyuvData.SaveCsv(result.ViewReslutCheckerboard.PoiResultCIExyuvDatas, filePath);
                    //        var csvBuilder = new StringBuilder();
                    //        csvBuilder.AppendLine();
                    //        csvBuilder.AppendLine($"Chessboard");
                    //        csvBuilder.AppendLine($"{result.ViewReslutCheckerboard.ChessboardContrast.Value}");
                    //        File.AppendAllText(filePath, csvBuilder.ToString(), Encoding.UTF8);
                    //    }
                    //    catch (Exception ex)
                    //    {
                    //        log.Error(ex);
                    //    }
                    //}

                }
            }
            else if (result.Model.Contains("MTF_H"))
            {
                log.Info("正在解析MTF_H画面的流程");
                result.TestType = ARVRTestType.MTFH;
                ObjectiveTestResult.FlowMTFHTestReslut = true;

                var values = MeasureImgResultDao.Instance.GetAllByBatchId(Batch.Id);
                if (values.Count > 0)
                {
                    result.FileName = values[0].FileUrl;
                }
                var AlgResultMasterlists = AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id);

                foreach (var AlgResultMaster in AlgResultMasterlists)
                {
                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.ViewResultAlgType.MTF && AlgResultMaster.version == "2.0")
                    {

                        List<DetailCommonModel> detailCommonModels = DeatilCommonDao.Instance.GetAllByPid(AlgResultMaster.Id);
                        if (detailCommonModels.Count == 1)
                        {
                            MTFDetailViewReslut mtfresults = new MTFDetailViewReslut(detailCommonModels[0]);

                            foreach (var mtf in mtfresults.MTFResult.result)
                            {
                                if (mtf.name == "Center_0F_H")
                                {
                                    mtf.mtfValue = mtf.mtfValue * ObjectiveTestResultFix.MTF_H_Center_0F;
                                    ObjectiveTestResult.MTF_H_Center_0F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_H_Center_0F",
                                        LowLimit = SPECConfig.MTF_H_Center_0FMin,
                                        UpLimit = SPECConfig.MTF_H_Center_0FMax,
                                        Value = mtf.mtfValue ?? 0,
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_H_Center_0F.TestResult;

                                }

                                if (mtf.name == "LeftUp_0.5F_H")
                                {
                                    mtf.mtfValue = mtf.mtfValue * ObjectiveTestResultFix.MTF_H_LeftUp_0_5F;
                                    ObjectiveTestResult.MTF_H_LeftUp_0_5F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_H_LeftUp_0_5F",
                                        LowLimit = SPECConfig.MTF_H_LeftUp_0_5FMin,
                                        UpLimit = SPECConfig.MTF_H_LeftUp_0_5FMax,
                                        Value = mtf.mtfValue ?? 0,
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_H_LeftUp_0_5F.TestResult;
                                }
                                if (mtf.name == "RightUp_0.5F_H")
                                {
                                    mtf.mtfValue = mtf.mtfValue * ObjectiveTestResultFix.MTF_H_RightUp_0_5F;
                                    ObjectiveTestResult.MTF_H_RightUp_0_5F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_H_RightUp_0_5F",
                                        LowLimit = SPECConfig.MTF_H_RightUp_0_5FMin,
                                        UpLimit = SPECConfig.MTF_H_RightUp_0_5FMax,
                                        Value = mtf.mtfValue ?? 0,
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_H_RightUp_0_5F.TestResult;
                                }
                                if (mtf.name == "LeftDown_0.5F_H")
                                {
                                    mtf.mtfValue = mtf.mtfValue * ObjectiveTestResultFix.MTF_H_LeftDown_0_5F;
                                    ObjectiveTestResult.MTF_H_LeftDown_0_5F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_H_LeftDown_0_5F",
                                        LowLimit = SPECConfig.MTF_H_LeftDown_0_5FMin,
                                        UpLimit = SPECConfig.MTF_H_LeftDown_0_5FMax,
                                        Value = mtf.mtfValue ?? 0,
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_H_LeftDown_0_5F.TestResult;
                                }
                                if (mtf.name == "RightDown_0.5F_H")
                                {
                                    mtf.mtfValue = mtf.mtfValue * ObjectiveTestResultFix.MTF_H_RightDown_0_5F;
                                    ObjectiveTestResult.MTF_H_RightDown_0_5F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_H_RightDown_0_5F",
                                        LowLimit = SPECConfig.MTF_H_RightDown_0_5FMin,
                                        UpLimit = SPECConfig.MTF_H_RightDown_0_5FMax,
                                        Value = mtf.mtfValue ?? 0,
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_H_RightDown_0_5F.TestResult;
                                }

                                if (mtf.name == "LeftUp_0.8F_H")
                                {
                                    mtf.mtfValue = mtf.mtfValue * ObjectiveTestResultFix.MTF_H_LeftUp_0_8F;
                                    ObjectiveTestResult.MTF_H_LeftUp_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_H_LeftUp_0_8F",
                                        LowLimit = SPECConfig.MTF_H_LeftUp_0_8FMin,
                                        UpLimit = SPECConfig.MTF_H_LeftUp_0_8FMax,
                                        Value = mtf.mtfValue ?? 0,
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_H_LeftUp_0_8F.TestResult;
                                }
                                if (mtf.name == "RightUp_0.8F_H")
                                {
                                    mtf.mtfValue = mtf.mtfValue * ObjectiveTestResultFix.MTF_H_RightUp_0_8F;
                                    ObjectiveTestResult.MTF_H_RightUp_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_H_RightUp_0_8F",
                                        LowLimit = SPECConfig.MTF_H_RightUp_0_8FMin,
                                        UpLimit = SPECConfig.MTF_H_RightUp_0_8FMax,
                                        Value = mtf.mtfValue ?? 0,
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_H_RightUp_0_8F.TestResult;
                                }
                                if (mtf.name == "LeftDown_0.8F_H")
                                {
                                    mtf.mtfValue = mtf.mtfValue * ObjectiveTestResultFix.MTF_H_LeftDown_0_8F;
                                    ObjectiveTestResult.MTF_H_LeftDown_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_H_LeftDown_0_8F",
                                        LowLimit = SPECConfig.MTF_H_LeftDown_0_8FMin,
                                        UpLimit = SPECConfig.MTF_H_LeftDown_0_8FMax,
                                        Value = mtf.mtfValue ?? 0,
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_H_LeftDown_0_8F.TestResult;
                                }
                                if (mtf.name == "RightDown_0.8F_H")
                                {
                                    mtf.mtfValue = mtf.mtfValue * ObjectiveTestResultFix.MTF_H_RightDown_0_8F;
                                    ObjectiveTestResult.MTF_H_RightDown_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_H_RightDown_0_8F",
                                        LowLimit = SPECConfig.MTF_H_RightDown_0_8FMin,
                                        UpLimit = SPECConfig.MTF_H_RightDown_0_8FMax,
                                        Value = mtf.mtfValue ?? 0,
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_H_RightDown_0_8F.TestResult;
                                }
                            }
                            result.ViewRelsultMTFH.MTFDetailViewReslut = mtfresults;
                        }
                        try
                        {
                            string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                            string filePath = Path.Combine(ProjectARVRConfig.Instance.ResultSavePath, $"MTF_H_{timeStr}.csv");
                            var csvBuilder = new StringBuilder();
                            csvBuilder.AppendLine($"name,x,y,w,h,mtfValue");
                            var mtfs = result.ViewRelsultMTFH.MTFDetailViewReslut.MTFResult?.result;
                            if (mtfs != null)
                            {
                                foreach (var item in mtfs)
                                {
                                    csvBuilder.AppendLine($"{item.name},{item.x},{item.y},{item.w},{item.h},{item.mtfValue}");
                                }
                            }
                            File.AppendAllText(filePath, csvBuilder.ToString(), Encoding.UTF8);
                        }
                        catch (Exception ex)
                        {
                            log.Error(ex);
                        }

                    }
                }

            }
            else if (result.Model.Contains("MTF_V"))
            {
                log.Info("正在解析MTF_V画面的流程");
                result.TestType = ARVRTestType.MTFV;
                ObjectiveTestResult.FlowMTFVTestReslut = true;

                var values = MeasureImgResultDao.Instance.GetAllByBatchId(Batch.Id);
                if (values.Count > 0)
                {
                    result.FileName = values[0].FileUrl;
                }
                var AlgResultMasterlists = AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id);

                foreach (var AlgResultMaster in AlgResultMasterlists)
                {
                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.ViewResultAlgType.MTF && AlgResultMaster.version == "2.0")
                    {

                        List<DetailCommonModel> detailCommonModels = DeatilCommonDao.Instance.GetAllByPid(AlgResultMaster.Id);
                        if (detailCommonModels.Count == 1)
                        {
                            MTFDetailViewReslut mtfresults = new MTFDetailViewReslut(detailCommonModels[0]);
                            foreach (var mtf in mtfresults.MTFResult.result)
                            {
                                if (mtf.name == "Center_0F_V")
                                {
                                    mtf.mtfValue = mtf.mtfValue * ObjectiveTestResultFix.MTF_V_Center_0F;
                                    ObjectiveTestResult.MTF_V_Center_0F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_V_Center_0F",
                                        LowLimit = SPECConfig.MTF_V_Center_0FMin,
                                        UpLimit = SPECConfig.MTF_V_Center_0FMax,
                                        Value = mtf.mtfValue ?? 0,
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_V_Center_0F.TestResult;
                                }
                                if (mtf.name == "LeftUp_0.5F_V")
                                {
                                    mtf.mtfValue = mtf.mtfValue * ObjectiveTestResultFix.MTF_V_LeftUp_0_5F;
                                    ObjectiveTestResult.MTF_V_LeftUp_0_5F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_V_LeftUp_0_5F",
                                        LowLimit = SPECConfig.MTF_V_LeftUp_0_5FMin,
                                        UpLimit = SPECConfig.MTF_V_LeftUp_0_5FMax,
                                        Value = mtf.mtfValue ?? 0,
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_V_LeftUp_0_5F.TestResult;
                                }
                                if (mtf.name == "RightUp_0.5F_V")
                                {
                                    mtf.mtfValue = mtf.mtfValue * ObjectiveTestResultFix.MTF_V_RightUp_0_5F;
                                    ObjectiveTestResult.MTF_V_RightUp_0_5F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_V_RightUp_0_5F",
                                        LowLimit = SPECConfig.MTF_V_RightUp_0_5FMin,
                                        UpLimit = SPECConfig.MTF_V_RightUp_0_5FMax,
                                        Value = mtf.mtfValue ?? 0,
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_V_RightUp_0_5F.TestResult;
                                }
                                if (mtf.name == "LeftDown_0.5F_V")
                                {
                                    mtf.mtfValue = mtf.mtfValue * ObjectiveTestResultFix.MTF_V_LeftDown_0_5F;
                                    ObjectiveTestResult.MTF_V_LeftDown_0_5F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_V_LeftDown_0_5F",
                                        LowLimit = SPECConfig.MTF_V_LeftDown_0_5FMin,
                                        UpLimit = SPECConfig.MTF_V_LeftDown_0_5FMax,
                                        Value = mtf.mtfValue ?? 0,
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_V_LeftDown_0_5F.TestResult;
                                }
                                if (mtf.name == "RightDown_0.5F_V")
                                {
                                    mtf.mtfValue = mtf.mtfValue * ObjectiveTestResultFix.MTF_V_RightDown_0_5F;
                                    ObjectiveTestResult.MTF_V_RightDown_0_5F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_V_RightDown_0_5F",
                                        LowLimit = SPECConfig.MTF_V_RightDown_0_5FMin,
                                        UpLimit = SPECConfig.MTF_V_RightDown_0_5FMax,
                                        Value = mtf.mtfValue ?? 0,
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_V_RightDown_0_5F.TestResult;
                                }
                                if (mtf.name == "LeftUp_0.8F_V")
                                {
                                    mtf.mtfValue = mtf.mtfValue * ObjectiveTestResultFix.MTF_V_LeftUp_0_8F;
                                    ObjectiveTestResult.MTF_V_LeftUp_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_V_LeftUp_0_8F",
                                        LowLimit = SPECConfig.MTF_V_LeftUp_0_8FMin,
                                        UpLimit = SPECConfig.MTF_V_LeftUp_0_8FMax,
                                        Value = mtf.mtfValue ?? 0,
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_V_LeftUp_0_8F.TestResult;
                                }
                                if (mtf.name == "RightUp_0.8F_V")
                                {
                                    mtf.mtfValue = mtf.mtfValue * ObjectiveTestResultFix.MTF_V_RightUp_0_8F;
                                    ObjectiveTestResult.MTF_V_RightUp_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_V_RightUp_0_8F",
                                        LowLimit = SPECConfig.MTF_V_RightUp_0_8FMin,
                                        UpLimit = SPECConfig.MTF_V_RightUp_0_8FMax,
                                        Value = mtf.mtfValue ?? 0,
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_V_RightUp_0_8F.TestResult;
                                }
                                if (mtf.name == "LeftDown_0.8F_V")
                                {
                                    mtf.mtfValue = mtf.mtfValue * ObjectiveTestResultFix.MTF_V_LeftDown_0_8F;
                                    ObjectiveTestResult.MTF_V_LeftDown_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_V_LeftDown_0_8F",
                                        LowLimit = SPECConfig.MTF_V_LeftDown_0_8FMin,
                                        UpLimit = SPECConfig.MTF_V_LeftDown_0_8FMax,
                                        Value = mtf.mtfValue ?? 0,
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_V_LeftDown_0_8F.TestResult;
                                }
                                if (mtf.name == "RightDown_0.8F_V")
                                {
                                    mtf.mtfValue = mtf.mtfValue * ObjectiveTestResultFix.MTF_V_RightDown_0_8F;
                                    ObjectiveTestResult.MTF_V_RightDown_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_V_RightDown_0_8F",
                                        LowLimit = SPECConfig.MTF_V_RightDown_0_8FMin,
                                        UpLimit = SPECConfig.MTF_V_RightDown_0_8FMax,
                                        Value = mtf.mtfValue ?? 0,
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_V_RightDown_0_8F.TestResult;
                                }
                            }

                            result.ViewRelsultMTFV.MTFDetailViewReslut = mtfresults;
                        }

                    }
                }

                try
                {
                    string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string filePath = Path.Combine(ProjectARVRConfig.Instance.ResultSavePath, $"MTF_V_{timeStr}.csv");
                    var csvBuilder = new StringBuilder();
                    csvBuilder.AppendLine($"name,x,y,w,h,mtfValue");
                    var mtfs = result.ViewRelsultMTFV.MTFDetailViewReslut.MTFResult?.result;
                    if (mtfs != null)
                    {
                        foreach (var item in mtfs)
                        {
                            csvBuilder.AppendLine($"{item.name},{item.x},{item.y},{item.w},{item.h},{item.mtfValue}");
                        }
                    }

                    File.AppendAllText(filePath, csvBuilder.ToString(), Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }


            }
            else if (result.Model.Contains("Distortion"))
            {
                log.Info("正在解析Distortion画面的流程");
                result.TestType = ARVRTestType.Distortion;
                ObjectiveTestResult.FlowDistortionTestReslut = true;

                var values = MeasureImgResultDao.Instance.GetAllByBatchId(Batch.Id);
                if (values.Count > 0)
                {
                    result.FileName = values[0].FileUrl;
                }
                var AlgResultMasterlists = AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id);

                foreach (var AlgResultMaster in AlgResultMasterlists)
                {
                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.ViewResultAlgType.Distortion && AlgResultMaster.version == "2.0")
                    {
                        List<DetailCommonModel> AlgResultModels = DeatilCommonDao.Instance.GetAllByPid(AlgResultMaster.Id);
                        if (AlgResultModels.Count == 1)
                        {
                            ColorVision.Engine.Templates.Jsons.Distortion2.Distortion2View blackMuraView = new ColorVision.Engine.Templates.Jsons.Distortion2.Distortion2View(AlgResultModels[0]);

                            blackMuraView.DistortionReslut.TVDistortion.HorizontalRatio = blackMuraView.DistortionReslut.TVDistortion.HorizontalRatio * ObjectiveTestResultFix.HorizontalTVDistortion;
                            blackMuraView.DistortionReslut.TVDistortion.VerticalRatio = blackMuraView.DistortionReslut.TVDistortion.VerticalRatio * ObjectiveTestResultFix.VerticalTVDistortion;

                            result.ViewReslutDistortionGhost.Distortion2View = blackMuraView;


                            ObjectiveTestResult.HorizontalTVDistortion = new ObjectiveTestItem()
                            {
                                Name = "HorizontalTVDistortion",
                                LowLimit = SPECConfig.HorizontalTVDistortionMin,
                                UpLimit = SPECConfig.HorizontalTVDistortionMax,
                                Value = blackMuraView.DistortionReslut.TVDistortion.HorizontalRatio,
                                TestValue = blackMuraView.DistortionReslut.TVDistortion.HorizontalRatio.ToString("F5")
                            };

                            ObjectiveTestResult.VerticalTVDistortion = new ObjectiveTestItem()
                            {
                                Name = "VerticalTVDistortion",
                                LowLimit = SPECConfig.VerticalTVDistortionMin,
                                UpLimit = SPECConfig.VerticalTVDistortionMax,
                                Value = blackMuraView.DistortionReslut.TVDistortion.VerticalRatio,
                                TestValue = blackMuraView.DistortionReslut.TVDistortion.VerticalRatio.ToString("F5")
                            };
                            result.ViewReslutDistortionGhost.HorizontalTVDistortion = ObjectiveTestResult.HorizontalTVDistortion;
                            result.ViewReslutDistortionGhost.VerticalTVDistortion = ObjectiveTestResult.VerticalTVDistortion;

                            result.Result = result.Result && ObjectiveTestResult.HorizontalTVDistortion.TestResult;
                            result.Result = result.Result && ObjectiveTestResult.VerticalTVDistortion.TestResult;


                        }

                    }
                }

            }
            else if (result.Model.Contains("OpticCenter"))
            {
                log.Info("正在解析OpticCenter画面的流程");
                result.TestType = ARVRTestType.OpticCenter;
                ObjectiveTestResult.FlowDistortionTestReslut = true;

                var values = MeasureImgResultDao.Instance.GetAllByBatchId(Batch.Id);
                if (values.Count > 0)
                {
                    result.FileName = values[0].FileUrl;
                }
                var AlgResultMasterlists = AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id);


                foreach (var AlgResultMaster in AlgResultMasterlists)
                {
                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.ViewResultAlgType.ARVR_BinocularFusion)
                    {
                        List<BinocularFusionModel> AlgResultModels = BinocularFusionDao.Instance.GetAllByPid(AlgResultMaster.Id);
                        if (AlgResultModels.Count == 1)
                        {
                            result.ViewResultOpticCenter.BinocularFusionModel = AlgResultModels[0];

                            result.ViewResultOpticCenter.BinocularFusionModel.XDegree = (float)(result.ViewResultOpticCenter.BinocularFusionModel.XDegree * ObjectiveTestResultFix.XTilt);
                            result.ViewResultOpticCenter.BinocularFusionModel.YDegree = (float)(result.ViewResultOpticCenter.BinocularFusionModel.YDegree * ObjectiveTestResultFix.YTilt);
                            result.ViewResultOpticCenter.BinocularFusionModel.ZDegree = (float)(result.ViewResultOpticCenter.BinocularFusionModel.ZDegree * ObjectiveTestResultFix.Rotation);

                            ObjectiveTestResult.XTilt = new ObjectiveTestItem()
                            {
                                Name = "XTilt",
                                LowLimit = SPECConfig.XTiltMin,
                                UpLimit = SPECConfig.XTiltMax,
                                Value = result.ViewResultOpticCenter.BinocularFusionModel.XDegree,
                                TestValue = result.ViewResultOpticCenter.BinocularFusionModel.XDegree.ToString("F4")
                            };
                            ObjectiveTestResult.YTilt = new ObjectiveTestItem()
                            {
                                Name = "YTilt",
                                LowLimit = SPECConfig.YTiltMin,
                                UpLimit = SPECConfig.YTiltMax,
                                Value = result.ViewResultOpticCenter.BinocularFusionModel.YDegree,
                                TestValue = result.ViewResultOpticCenter.BinocularFusionModel.YDegree.ToString("F4")
                            };
                            ObjectiveTestResult.Rotation = new ObjectiveTestItem()
                            {
                                Name = "Rotation",
                                LowLimit = SPECConfig.RotationMin,
                                UpLimit = SPECConfig.RotationMax,
                                Value = result.ViewResultOpticCenter.BinocularFusionModel.ZDegree,
                                TestValue = result.ViewResultOpticCenter.BinocularFusionModel.ZDegree.ToString("F4")
                            };


                            result.ViewResultOpticCenter.XTilt = ObjectiveTestResult.XTilt;
                            result.ViewResultOpticCenter.YTilt = ObjectiveTestResult.YTilt;
                            result.ViewResultOpticCenter.Rotation = ObjectiveTestResult.Rotation;

                            result.Result = result.Result && ObjectiveTestResult.XTilt.TestResult;
                            result.Result = result.Result && ObjectiveTestResult.YTilt.TestResult;
                            result.Result = result.Result && ObjectiveTestResult.Rotation.TestResult;
                        }

                    }
                }

            }
            else
            {
                var values = MeasureImgResultDao.Instance.GetAllByBatchId(Batch.Id);
                if (values.Count > 0)
                {
                    result.FileName = values[0].FileUrl;
                }
            }

            ViewResultManager.Save(result);


            ObjectiveTestResult.TotalResult = ObjectiveTestResult.TotalResult && result.Result;

            if (IsTestTypeCompleted())
            {
                TestCompleted();
            }
        }

        private bool IsTestTypeCompleted()
        {
            var values = Enum.GetValues(typeof(ARVRTestType));
            int currentIndex = Array.IndexOf(values, CurrentTestType);
            int nextIndex = (currentIndex + 1) % values.Length;
            // 跳过 None（假设 None 是第一个）
            if ((ARVRTestType)values.GetValue(nextIndex) == ARVRTestType.None)
                nextIndex = (nextIndex + 1) % values.Length;
            ARVRTestType aRVRTestType = (ARVRTestType)values.GetValue(nextIndex);

            return aRVRTestType >= ARVRTestType.Ghost;
        }

        private void SwitchPG()
        {
            if (SocketManager.GetInstance().TcpClients.Count <= 0 || SocketControl.Current.Stream == null)
            {
                log.Info("找不到连接的Socket");
                return;
            }
            log.Info("Socket已经链接 ");

            var values = Enum.GetValues(typeof(ARVRTestType));
            int currentIndex = Array.IndexOf(values, CurrentTestType);
            int nextIndex = (currentIndex + 1) % values.Length;
            // 跳过 None（假设 None 是第一个）
            if ((ARVRTestType)values.GetValue(nextIndex) == ARVRTestType.None)
                nextIndex = (nextIndex + 1) % values.Length;
            ARVRTestType aRVRTestType = (ARVRTestType)values.GetValue(nextIndex);

            var response = new SocketResponse
            {
                Version = "1.0",
                MsgID = string.Empty,
                EventName = "SwitchPG",
                Code = 0,
                Msg = "Switch PG",
                SerialNumber = SNtextBox.Text,
                Data = new SwitchPG
                {
                    ARVRTestType = aRVRTestType
                },
            };
            string respString = JsonConvert.SerializeObject(response);
            log.Info(respString);
            SocketControl.Current.Stream.Write(Encoding.UTF8.GetBytes(respString));

        }

        private void TestCompleted()
        {
            if (SocketManager.GetInstance().TcpClients.Count <= 0 || SocketControl.Current.Stream == null)
            {
                log.Info("找不到连接的Socket");
                return;
            }
            ObjectiveTestResult.TotalResult = true;
            ObjectiveTestResult.TotalResult = ObjectiveTestResult.TotalResult && ObjectiveTestResult.FlowWhiteTestReslut;
            ObjectiveTestResult.TotalResult = ObjectiveTestResult.TotalResult && ObjectiveTestResult.FlowBlackTestReslut;
            ObjectiveTestResult.TotalResult = ObjectiveTestResult.TotalResult && ObjectiveTestResult.FlowChessboardTestReslut;
            ObjectiveTestResult.TotalResult = ObjectiveTestResult.TotalResult && ObjectiveTestResult.FlowMTFVTestReslut;
            ObjectiveTestResult.TotalResult = ObjectiveTestResult.TotalResult && ObjectiveTestResult.FlowMTFHTestReslut;
            ObjectiveTestResult.TotalResult = ObjectiveTestResult.TotalResult && ObjectiveTestResult.FlowDistortionTestReslut;
            ObjectiveTestResult.TotalResult = ObjectiveTestResult.TotalResult && ObjectiveTestResult.FlowOpticCenterTestReslut;
            log.Info($"ARVR测试完成,TotalResult {ObjectiveTestResult.TotalResult}");

            string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");



            string filePath = Path.Combine(ViewResultManager.Config.SavePathCsv, $"ObjectiveTestResults_{timeStr}.csv");

            List<ObjectiveTestResult> objectiveTestResults = new List<ObjectiveTestResult>();

            objectiveTestResults.Add(ObjectiveTestResult);
            ObjectiveTestResultCsvExporter.ExportToCsv(objectiveTestResults, filePath);
            var response = new SocketResponse
            {
                Version = "1.0",
                MsgID = string.Empty,
                EventName = "ProjectARVRResult",
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
            ProjectARVRConfig.Instance.Height = row2.ActualHeight;
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


                if (result.TestType == ARVRTestType.White)
                {
                    DVPolygon polygon = new DVPolygon();
                    List<System.Windows.Point> point1s = new List<System.Windows.Point>();

                    if(result.ViewResultWhite == null || result.ViewResultWhite.PoiResultCIExyuvDatas ==null || result.ViewResultWhite.AlgResultLightAreaModels ==null)
                    {
                        log.Info("找不到白画面的结果");
                        return;
                    }

                    foreach (var item in result.ViewResultWhite.AlgResultLightAreaModels)
                    {
                        point1s.Add(new System.Windows.Point((int)item.PosX, (int)item.PosY));
                    }
                    foreach (var item in GrahamScan.ComputeConvexHull(point1s))
                    {
                        polygon.Attribute.Points.Add(new Point(item.X, item.Y));
                    }
                    polygon.Attribute.Brush = Brushes.Transparent;
                    polygon.Attribute.Pen = new Pen(Brushes.Blue, 1);
                    polygon.Attribute.Id = -1;
                    polygon.IsComple = true;
                    polygon.Render();
                    ImageView.AddVisual(polygon);

                    foreach (var poiResultCIExyuvData in result.ViewResultWhite.PoiResultCIExyuvDatas)
                    {
                        var item = poiResultCIExyuvData.Point;
                        switch (item.PointType)
                        {
                            case POIPointTypes.Circle:
                                DVCircleText Circle = new DVCircleText();
                                Circle.Attribute.Center = new Point(item.PixelX, item.PixelY);
                                Circle.Attribute.Radius = item.Radius;
                                Circle.Attribute.Brush = Brushes.Transparent;
                                Circle.Attribute.Pen = new Pen(Brushes.Red, 1);
                                Circle.Attribute.Id = item.Id ?? -1;
                                Circle.Attribute.Text = item.Name;
                                Circle.Attribute.Msg = PoiImageViewComponent.FormatMessage(CVCIEShowConfig.Instance.Template, poiResultCIExyuvData);
                                Circle.Render();
                                ImageView.AddVisual(Circle);
                                break;
                            case POIPointTypes.Rect:
                                DVRectangleText Rectangle = new DVRectangleText();
                                Rectangle.Attribute.Rect = new Rect(item.PixelX - item.Width / 2, item.PixelY - item.Height / 2, item.Width, item.Height);
                                Rectangle.Attribute.Brush = Brushes.Transparent;
                                Rectangle.Attribute.Pen = new Pen(Brushes.Red, 1);
                                Rectangle.Attribute.Id = item.Id ?? -1;
                                Rectangle.Attribute.Text = item.Name;
                                Rectangle.Attribute.Msg = PoiImageViewComponent.FormatMessage(CVCIEShowConfig.Instance.Template, poiResultCIExyuvData);
                                Rectangle.Render();
                                ImageView.AddVisual(Rectangle);
                                break;
                            default:
                                break;
                        }
                    }
                }

                if (result.TestType == ARVRTestType.Black)
                {
                    if (result.ViewResultBlack == null || result.ViewResultBlack.PoiResultCIExyuvDatas == null)
                    {
                        log.Info("找不到黑画面的结果");
                        return;
                    }
                    foreach (var poiResultCIExyuvData in result.ViewResultBlack.PoiResultCIExyuvDatas)
                    {
                        var item = poiResultCIExyuvData.Point;
                        switch (item.PointType)
                        {
                            case POIPointTypes.Circle:
                                DVCircleText Circle = new DVCircleText();
                                Circle.Attribute.Center = new Point(item.PixelX, item.PixelY);
                                Circle.Attribute.Radius = item.Radius;
                                Circle.Attribute.Brush = Brushes.Transparent;
                                Circle.Attribute.Pen = new Pen(Brushes.Red, 1);
                                Circle.Attribute.Id = item.Id ?? -1;
                                Circle.Attribute.Text = item.Name;
                                Circle.Attribute.Msg = PoiImageViewComponent.FormatMessage(CVCIEShowConfig.Instance.Template, poiResultCIExyuvData);
                                Circle.Render();
                                ImageView.AddVisual(Circle);
                                break;
                            case POIPointTypes.Rect:
                                DVRectangleText Rectangle = new DVRectangleText();
                                Rectangle.Attribute.Rect = new Rect(item.PixelX - item.Width / 2, item.PixelY - item.Height / 2, item.Width, item.Height);
                                Rectangle.Attribute.Brush = Brushes.Transparent;
                                Rectangle.Attribute.Pen = new Pen(Brushes.Red, 1);
                                Rectangle.Attribute.Id = item.Id ?? -1;
                                Rectangle.Attribute.Text = item.Name;
                                Rectangle.Attribute.Msg = PoiImageViewComponent.FormatMessage(CVCIEShowConfig.Instance.Template, poiResultCIExyuvData);
                                Rectangle.Render();
                                ImageView.AddVisual(Rectangle);
                                break;
                            default:
                                break;
                        }
                    }
                }

                if (result.TestType == ARVRTestType.Chessboard)
                {
                    if (result.ViewReslutCheckerboard == null || result.ViewReslutCheckerboard.PoiResultCIExyuvDatas == null)
                    {
                        log.Info("找不到棋盘格的结果");
                        return;
                    }
                    foreach (var poiResultCIExyuvData in result.ViewReslutCheckerboard.PoiResultCIExyuvDatas)
                    {
                        var item = poiResultCIExyuvData.Point;
                        switch (item.PointType)
                        {
                            case POIPointTypes.Circle:
                                DVCircleText Circle = new DVCircleText();
                                Circle.Attribute.Center = new Point(item.PixelX, item.PixelY);
                                Circle.Attribute.Radius = item.Radius;
                                Circle.Attribute.Brush = Brushes.Transparent;
                                Circle.Attribute.Pen = new Pen(Brushes.Red, 1);
                                Circle.Attribute.Id = item.Id ?? -1;
                                Circle.Attribute.Text = item.Name;
                                Circle.Attribute.Msg = PoiImageViewComponent.FormatMessage("Y:@Y:F2", poiResultCIExyuvData);
                                Circle.Render();
                                ImageView.AddVisual(Circle);
                                break;
                            case POIPointTypes.Rect:
                                DVRectangleText Rectangle = new DVRectangleText();
                                Rectangle.Attribute.Rect = new Rect(item.PixelX - item.Width / 2, item.PixelY - item.Height / 2, item.Width, item.Height);
                                Rectangle.Attribute.Brush = Brushes.Transparent;
                                Rectangle.Attribute.Pen = new Pen(Brushes.Red, 1);
                                Rectangle.Attribute.Id = item.Id ?? -1;
                                Rectangle.Attribute.Text = item.Name;
                                Rectangle.Attribute.Msg = PoiImageViewComponent.FormatMessage("Y:@Y:F2", poiResultCIExyuvData);
                                Rectangle.Render();
                                ImageView.AddVisual(Rectangle);
                                break;
                            default:
                                break;
                        }
                    }
                }

                if (result.TestType == ARVRTestType.MTFH)
                {

                    if (result.ViewRelsultMTFH == null || result.ViewRelsultMTFH.MTFDetailViewReslut == null)
                    {
                        log.Info("找不到MTFH的结果");
                        return;
                    }

                    int id = 0;
                    if (result.ViewRelsultMTFH.MTFDetailViewReslut.MTFResult.result.Count != 0)
                    {
                        foreach (var item in result.ViewRelsultMTFH.MTFDetailViewReslut.MTFResult.result)
                        {
                            id++;
                            DVRectangleText Rectangle = new();
                            Rectangle.Attribute.Rect = new Rect(item.x, item.y, item.w, item.h);
                            Rectangle.Attribute.Brush = Brushes.Transparent;
                            Rectangle.Attribute.Pen = new Pen(Brushes.Red, 1);
                            Rectangle.Attribute.Id = id;
                            Rectangle.Attribute.Text = item.name;
                            Rectangle.Attribute.Msg = item.mtfValue.ToString();
                            Rectangle.Render();
                            ImageView.AddVisual(Rectangle);
                        }
                    }
                }
                if (result.TestType == ARVRTestType.MTFV)
                {
                    if (result.ViewRelsultMTFV == null || result.ViewRelsultMTFV.MTFDetailViewReslut == null)
                    {
                        log.Info("找不到MTFV的结果");
                        return;
                    }
                    int id = 0;
                    if (result.ViewRelsultMTFV.MTFDetailViewReslut.MTFResult.result.Count != 0)
                    {
                        foreach (var item in result.ViewRelsultMTFV.MTFDetailViewReslut.MTFResult.result)
                        {
                            id++;
                            DVRectangleText Rectangle = new();
                            Rectangle.Attribute.Rect = new Rect(item.x, item.y, item.w, item.h);
                            Rectangle.Attribute.Brush = Brushes.Transparent;
                            Rectangle.Attribute.Pen = new Pen(Brushes.Red, 1);
                            Rectangle.Attribute.Id = id;
                            Rectangle.Attribute.Text = item.name;
                            Rectangle.Attribute.Msg = item.mtfValue.ToString();
                            Rectangle.Render();
                            ImageView.AddVisual(Rectangle);
                        }
                    }
                }

                if (result.TestType == ARVRTestType.Distortion)
                {
                    if (result.ViewReslutDistortionGhost == null || result.ViewReslutDistortionGhost.Distortion2View == null)
                    {
                        log.Info("找不到畸变的结果");
                        return;
                    }

                    if (result.ViewReslutDistortionGhost.Distortion2View.DistortionReslut.TVDistortion != null)
                    {
                        if (result.ViewReslutDistortionGhost.Distortion2View.DistortionReslut.TVDistortion.FinalPoints != null)
                        {
                            foreach (var points in result.ViewReslutDistortionGhost.Distortion2View.DistortionReslut.TVDistortion.FinalPoints)
                            {
                                DVCircleText Circle = new();
                                Circle.Attribute.Center = new System.Windows.Point(points.X, points.Y);
                                Circle.Attribute.Radius = 20 / ImageView.Zoombox1.ContentMatrix.M11;
                                Circle.Attribute.Brush = Brushes.Transparent;
                                Circle.Attribute.Pen = new Pen(Brushes.Red, 1 / ImageView.Zoombox1.ContentMatrix.M11);
                                Circle.Attribute.Text = $"id:{points.Id}{Environment.NewLine} X:{points.X.ToString("F0")}{Environment.NewLine}Y:{points.Y.ToString("F0")}";
                                Circle.Attribute.Id = points.Id;
                                Circle.Render();
                                ImageView.AddVisual(Circle);
                            }
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

            outtext += $"{result.CreateTime:yyyy/MM//dd HH:mm:ss}" + Environment.NewLine;

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

                    if (result.ViewResultWhite.AlgResultLightAreaModels != null)
                    {
                        foreach (var item in result.ViewResultWhite.AlgResultLightAreaModels)
                        {
                            outtext += $"AlgResultLightAreaModel:{item.PosX},{item.PosY}" + Environment.NewLine;
                        }
                    }

                    if (result.ViewResultWhite.PoiResultCIExyuvDatas != null)
                    {
                        foreach (var item in result.ViewResultWhite.PoiResultCIExyuvDatas)
                        {
                            outtext += $"X:{item.X.ToString("F2")} Y:{item.Y.ToString("F2")} Z:{item.Z.ToString("F2")} x:{item.x.ToString("F2")} y:{item.y.ToString("F2")} u:{item.u.ToString("F2")} v:{item.v.ToString("F2")} cct:{item.CCT.ToString("F2")} wave:{item.Wave.ToString("F2")}{Environment.NewLine}";
                        }
                    }
                    outtext += $"CenterCorrelatedColorTemperature:{result.ViewResultWhite.CenterCorrelatedColorTemperature.TestValue}  LowLimit:{result.ViewResultWhite.CenterCorrelatedColorTemperature.LowLimit} UpLimit:{result.ViewResultWhite.CenterCorrelatedColorTemperature.UpLimit},Rsult{(result.ViewResultWhite.CenterCorrelatedColorTemperature.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                    outtext += $"Luminance_uniformity:{result.ViewResultWhite.LuminanceUniformity.TestValue} LowLimit:{result.ViewResultWhite.LuminanceUniformity.LowLimit}  UpLimit:{result.ViewResultWhite.LuminanceUniformity.UpLimit},Rsult{(result.ViewResultWhite.LuminanceUniformity.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                    outtext += $"Color_uniformity:{result.ViewResultWhite.ColorUniformity.TestValue} LowLimit:{result.ViewResultWhite.ColorUniformity.LowLimit} UpLimit:{result.ViewResultWhite.ColorUniformity.UpLimit},Rsult{(result.ViewResultWhite.ColorUniformity.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                    outtext += $"DiagonalFieldOfViewAngle:{result.ViewResultWhite.DiagonalFieldOfViewAngle.TestValue}  LowLimit:{result.ViewResultWhite.DiagonalFieldOfViewAngle.LowLimit} UpLimit:{result.ViewResultWhite.DiagonalFieldOfViewAngle.UpLimit},Rsult{(result.ViewResultWhite.DiagonalFieldOfViewAngle.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                    outtext += $"HorizontalFieldOfViewAngle:{result.ViewResultWhite.HorizontalFieldOfViewAngle.TestValue} LowLimit:{result.ViewResultWhite.HorizontalFieldOfViewAngle.LowLimit} UpLimit:{result.ViewResultWhite.HorizontalFieldOfViewAngle.UpLimit} ,Rsult{(result.ViewResultWhite.HorizontalFieldOfViewAngle.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                    outtext += $"VerticalFieldOfViewAngle:{result.ViewResultWhite.VerticalFieldOfViewAngle.TestValue} LowLimit:{result.ViewResultWhite.VerticalFieldOfViewAngle.LowLimit} UpLimit:{result.ViewResultWhite.VerticalFieldOfViewAngle.UpLimit},Rsult{(result.ViewResultWhite.VerticalFieldOfViewAngle.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";

                    break;
                case ARVRTestType.Black:
                    outtext += $"黑画面 测试项：自动AA区域定位算法+关注点算法+序列对比度算法(中心亮度比值)" + Environment.NewLine;
                    if (result.ViewResultBlack.PoiResultCIExyuvDatas != null)
                    {
                        foreach (var item in result.ViewResultBlack.PoiResultCIExyuvDatas)
                        {
                            outtext += $"{item.Name}  X:{item.X.ToString("F2")} Y:{item.Y.ToString("F2")} Z:{item.Z.ToString("F2")} x:{item.x.ToString("F2")} y:{item.y.ToString("F2")} u:{item.u.ToString("F2")} v:{item.v.ToString("F2")} cct:{item.CCT.ToString("F2")} wave:{item.Wave.ToString("F2")}{Environment.NewLine}";
                        }
                    }
                    if (result.ViewResultBlack.FOFOContrast !=null)
                        outtext += $"FOFOContrast:{result.ViewResultBlack.FOFOContrast.TestValue}  LowLimit:{result.ViewResultBlack.FOFOContrast.LowLimit} UpLimit:{result.ViewResultBlack.FOFOContrast.UpLimit},Rsult{(result.ViewResultBlack.FOFOContrast.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                    break;
                case ARVRTestType.MTFH:
                    outtext += $"水平MTF 测试项：自动AA区域定位算法+关注点+MTF算法" + Environment.NewLine;
                    if (result.ViewRelsultMTFH.MTFDetailViewReslut.MTFResult != null)
                    {
                        foreach (var item in result.ViewRelsultMTFH.MTFDetailViewReslut.MTFResult.result)
                        {
                            outtext += $"{item.name},{item.mtfValue}" + Environment.NewLine;
                        }
                    }

                    break;
                case ARVRTestType.MTFV:
                    outtext += $"垂直MTF 测试项：自动AA区域定位算法+关注点+MTF算法" + Environment.NewLine;
                    if (result.ViewRelsultMTFV.MTFDetailViewReslut.MTFResult != null)
                    {
                        foreach (var item in result.ViewRelsultMTFV.MTFDetailViewReslut.MTFResult.result)
                        {
                            outtext += $"{item.name},{item.mtfValue}" + Environment.NewLine;
                        }
                    }
                    break;
                case ARVRTestType.Distortion:
                    outtext += $"畸变鬼影 测试项：自动AA区域定位算法+畸变算法+鬼影算法" + Environment.NewLine;

                    foreach (var item in result.ViewReslutDistortionGhost.Distortion2View.DistortionReslut.TVDistortion.FinalPoints)
                    {
                        outtext += $"id:{item.Id} X:{item.X} Y:{item.Y}" + Environment.NewLine;
                    }
                    outtext += $"HorizontalTVDistortion:{result.ViewReslutDistortionGhost.HorizontalTVDistortion.TestValue} LowLimit:{result.ViewReslutDistortionGhost.HorizontalTVDistortion.LowLimit}  UpLimit:{result.ViewReslutDistortionGhost.HorizontalTVDistortion.UpLimit},Rsult{(result.ViewReslutDistortionGhost.HorizontalTVDistortion.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                    outtext += $"VerticalTVDistortion:{result.ViewReslutDistortionGhost.VerticalTVDistortion.TestValue} LowLimit:{result.ViewReslutDistortionGhost.VerticalTVDistortion.LowLimit}  UpLimit:{result.ViewReslutDistortionGhost.VerticalTVDistortion.UpLimit},Rsult{(result.ViewReslutDistortionGhost.VerticalTVDistortion.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                    break;
                case ARVRTestType.Chessboard:
                    outtext += $"棋盘格 测试项：" + Environment.NewLine;
                    outtext += $"ChessboardContrast:{result.ViewReslutCheckerboard.ChessboardContrast.TestValue} LowLimit:{result.ViewReslutCheckerboard.ChessboardContrast.LowLimit}  UpLimit:{result.ViewReslutCheckerboard.ChessboardContrast.UpLimit},Rsult{(result.ViewReslutCheckerboard.ChessboardContrast.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                    break;
                case ARVRTestType.OpticCenter:
                    outtext += $"OpticCenter 测试项：" + Environment.NewLine;
                    outtext += $"中心点x:{result.ViewResultOpticCenter.BinocularFusionModel.CrossMarkCenterX} 中心点y:{result.ViewResultOpticCenter.BinocularFusionModel.CrossMarkCenterY}" + Environment.NewLine;

                    outtext += $"XTilt:{result.ViewResultOpticCenter.XTilt.TestValue} LowLimit:{result.ViewResultOpticCenter.XTilt.LowLimit}  UpLimit:{result.ViewResultOpticCenter.XTilt.UpLimit},Rsult{(result.ViewResultOpticCenter.XTilt.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                    outtext += $"YTilt:{result.ViewResultOpticCenter.YTilt.TestValue} LowLimit:{result.ViewResultOpticCenter.YTilt.LowLimit}  UpLimit:{result.ViewResultOpticCenter.YTilt.UpLimit},Rsult{(result.ViewResultOpticCenter.YTilt.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                    outtext += $"Rotation:{result.ViewResultOpticCenter.Rotation.TestValue} LowLimit:{result.ViewResultOpticCenter.Rotation.LowLimit}  UpLimit:{result.ViewResultOpticCenter.Rotation.UpLimit},Rsult{(result.ViewResultOpticCenter.Rotation.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";

                    break;
                default:
                    break;
            }


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
        public void Dispose()
        {
            timer?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}