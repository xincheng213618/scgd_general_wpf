#pragma warning disable
using ColorVision.Common.Algorithms;
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Media;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.FindLightArea;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.BinocularFusion;
using ColorVision.Engine.Templates.Jsons.FindCross;
using ColorVision.Engine.Templates.Jsons.FOV2;
using ColorVision.Engine.Templates.Jsons.LargeFlow;
using ColorVision.Engine.Templates.Jsons.MTF2;
using ColorVision.Engine.Templates.Jsons.PoiAnalysis;
using ColorVision.Engine.Templates.MTF;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.Engine.Templates.POI.Image;
using ColorVision.ImageEditor.Draw;
using ColorVision.Scheduler;
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
using ProjectARVRPro;
using ProjectARVRPro.PluginConfig;
using ProjectARVRPro.Services;
using Quartz;
using SqlSugar;
using ST.Library.UI.NodeEditor;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ProjectARVRPro
{
    public class ProjectARVRLitetestJob : IJob
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ProjectARVRLitetestJob));

        public Task Execute(IJobExecutionContext context)
        {
            var schedulerInfo = QuartzSchedulerManager.GetInstance().TaskInfos.First(x => x.JobName == context.JobDetail.Key.Name && x.GroupName == context.JobDetail.Key.Group);
            schedulerInfo.RunCount++;
            Application.Current.Dispatcher.Invoke(() =>
            {
                schedulerInfo.Status = SchedulerStatus.Running;
            });
            Application.Current.Dispatcher.Invoke(() =>
            {
                ProjectWindowInstance.WindowInstance.SwitchPGCompleted();

                ProjectWindowInstance.WindowInstance.RunTemplate();

                schedulerInfo.Status = SchedulerStatus.Ready;
            });
            return Task.CompletedTask;
        }
    }

    public class SwitchPG
    {
        public ARVR1TestType ARVRTestType { get; set; }
    }


    public class ARVRWindowConfig : WindowConfig
    {
        public static ARVRWindowConfig Instance => ConfigService.Instance.GetRequiredService<ARVRWindowConfig>();
    }

    public partial class ARVRWindow : Window, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ARVRWindow));
        public static ARVRWindowConfig Config => ARVRWindowConfig.Instance;

        public static ProjectARVRLiteConfig ProjectConfig => ProjectARVRLiteConfig.Instance;

        public static ViewResultManager ViewResultManager => ViewResultManager.GetInstance();

        public static ObservableCollection<ProjectARVRReuslt> ViewResluts { get; set; } = ViewResultManager.ViewResluts;

        public static ObjectiveTestResultFix ObjectiveTestResultFix => FixManager.GetInstance().ObjectiveTestResultFix;

        public ARVRWindow()
        {
            InitializeComponent();
            this.ApplyCaption(false);
            Config.SetWindow(this);
        }

        public ARVR1TestType CurrentTestType = ARVR1TestType.None;

        ObjectiveTestResult ObjectiveTestResult { get; set; } = new ObjectiveTestResult();
        Random Random = new Random();
        public void InitTest(string SN)
        {
            ProjectConfig.StepIndex = 0;
            ObjectiveTestResult = new ObjectiveTestResult();
            CurrentTestType = ARVR1TestType.None;
            if (string.IsNullOrWhiteSpace(SN))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProjectARVRLiteConfig.Instance.SN = "SN" + Random.NextInt64(10000, 90000).ToString();
                });
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProjectARVRLiteConfig.Instance.SN = "SN" + Random.NextInt64(10000, 90000).ToString();
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
            var values = Enum.GetValues(typeof(ARVR1TestType));
            int currentIndex = Array.IndexOf(values, CurrentTestType);
            int nextIndex = (currentIndex + 1) % values.Length;
            // 跳过 None（假设 None 是第一个）
            if ((ARVR1TestType)values.GetValue(nextIndex) == ARVR1TestType.None)
                nextIndex = (nextIndex + 1) % values.Length;
            var TestType = (ARVR1TestType)values.GetValue(nextIndex);

            try
            {
                if (TestType == ARVR1TestType.W51)
                {
                    ProjectConfig.StepIndex = 1;
                    FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("White51")).Value;
                    CurrentTestType = TestType;
                    RunTemplate();
                }
                if (TestType == ARVR1TestType.White)
                {
                    ProjectConfig.StepIndex = 2;
                    FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("White255")).Value;
                    CurrentTestType = TestType;
                    RunTemplate();
                }
                if (TestType == ARVR1TestType.Black)
                {
                    ProjectConfig.StepIndex = 3;
                    FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("Black")).Value;
                    CurrentTestType = TestType;
                    RunTemplate();
                }
                if (TestType == ARVR1TestType.W25)
                {
                    ProjectConfig.StepIndex = 4;

                    FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("White25")).Value;
                    CurrentTestType = TestType;
                    RunTemplate();
                }
                if (TestType == ARVR1TestType.Chessboard)
                {
                    ProjectConfig.StepIndex = 5;

                    FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("Chessboard")).Value;
                    CurrentTestType = TestType;
                    RunTemplate();
                }
                if (TestType == ARVR1TestType.MTFHV)
                {
                    ProjectConfig.StepIndex = 6;

                    FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("MTF_HV")).Value;
                    CurrentTestType = TestType;
                    RunTemplate();
                }
                if (TestType == ARVR1TestType.Distortion)
                {
                    ProjectConfig.StepIndex = 7;

                    FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("Distortion")).Value;
                    CurrentTestType = TestType;
                    RunTemplate();
                }
                if (TestType == ARVR1TestType.OpticCenter)
                {
                    ProjectConfig.StepIndex = 8;
                    FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("OpticCenter")).Value;
                    CurrentTestType = TestType;
                    RunTemplate();
                }
                if (CurrentTestType != TestType)
                {
                    log.Info("无法找到测试类型对应的测试模板,正在切换下一张图");
                    if (!IsTestTypeCompleted())
                    {
                        SwitchPG();
                    }
                    else
                    {
                        TestCompleted();
                    }
                }
            }
            catch (Exception ex)
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
        public static ARVRRecipeConfig recipeConfig => RecipeManager.RecipeConfig;


        private LogOutput? logOutput;
        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = ProjectARVRLiteConfig.Instance;
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
            timer = new Timer(TimeRun, null, 0, 100);
            timer.Change(Timeout.Infinite, 100); // 停止定时器


            if (ProjectARVRLiteConfig.Instance.LogControlVisibility)
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
                this.Dispose();
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
                MySqlControl.GetInstance().DB.Deleteable<MeasureBatchModel>().Where(it => it.Id == item.Id).ExecuteCommand();
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


        public async Task Refresh()
        {
            if (FlowTemplate.SelectedIndex < 0) return;
            string Refreshdata = TemplateFlow.Params[FlowTemplate.SelectedIndex].Value.DataBase64;

            try
            {
                foreach (var item in STNodeEditorMain.Nodes.OfType<CVCommonNode>())
                    item.nodeRunEvent -= UpdateMsg;

                flowEngine.LoadFromBase64(Refreshdata, MqttRCService.GetInstance().ServiceTokens);

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
            log.Info($"IsReady{flowEngine.IsReady}");
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
            if (flowControl.IsFlowRun)
            {
                log.Info("当前flowControl存在流程执行");
                return;
            }
            flowControl.IsFlowRun = true;
            TryCount++;
            LastFlowTime = FlowEngineConfig.Instance.FlowRunTime.TryGetValue(FlowTemplate.Text, out long time) ? time : 0;

            CurrentFlowResult = new ProjectARVRReuslt();
            CurrentFlowResult.SN = ProjectARVRLiteConfig.Instance.SN;
            CurrentFlowResult.Model = FlowTemplate.Text;
            ;

            CurrentFlowResult.TestType = CurrentTestType;

            FlowName = FlowTemplate.Text;
            CurrentFlowResult.Code = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");

            await Refresh();

            if (string.IsNullOrWhiteSpace(flowEngine.GetStartNodeName())) { log.Info( "找不到完整流程，运行失败");return; }

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

            MeasureBatchModel measureBatchModel = new MeasureBatchModel() { Name = CurrentFlowResult.SN, Code = CurrentFlowResult.Code };
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
            CurrentFlowResult.RunTime  = stopwatch.ElapsedMilliseconds;
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

                if (TryCount < ProjectARVRLiteConfig.Instance.TryCountMax)
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


                ViewResultManager.Save(CurrentFlowResult);
                logTextBox.Text = FlowName + Environment.NewLine + FlowControlData.EventName + Environment.NewLine + FlowControlData.Params;

                TryCount = 0;

                if (ProjectARVRLiteConfig.Instance.AllowTestFailures)
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


            if (result.Model.Contains("White51"))
            {
                log.Info("正在解析White51的流程");

                ObjectiveTestResult.FlowW51TestReslut = true;

                var values = MeasureImgResultDao.Instance.GetAllByBatchId(Batch.Id);
                if (values.Count > 0)
                {
                    result.FileName = values[0].FileUrl;
                }
                var AlgResultMasterlists = AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id);
                log.Info($"AlgResultMasterlists count {AlgResultMasterlists.Count}");
                foreach (var AlgResultMaster in AlgResultMasterlists)
                {
                    if (AlgResultMaster.ImgFileType == ViewResultAlgType.FindLightArea)
                    {
                        result.ViewReslutW51.AlgResultLightAreaModels = AlgResultLightAreaDao.Instance.GetAllByPid(AlgResultMaster.Id);
                    }

                    if (AlgResultMaster.ImgFileType == ViewResultAlgType.FOV)
                    {
                        List<DetailCommonModel> AlgResultModels = DeatilCommonDao.Instance.GetAllByPid(AlgResultMaster.Id);
                        if (AlgResultModels.Count == 1)
                        {
                            DFovView view1 = new DFovView(AlgResultModels[0]);

                            view1.Result.result.D_Fov = view1.Result.result.D_Fov * ObjectiveTestResultFix.W51DiagonalFieldOfViewAngle;
                            view1.Result.result.ClolorVisionH_Fov = view1.Result.result.ClolorVisionH_Fov * ObjectiveTestResultFix.W51HorizontalFieldOfViewAngle;
                            view1.Result.result.ClolorVisionV_Fov = view1.Result.result.ClolorVisionV_Fov * ObjectiveTestResultFix.W51VerticalFieldOfViewAngle;

                            result.ViewResultWhite.DFovView = view1;
                            ObjectiveTestResult.W51DiagonalFieldOfViewAngle = new ObjectiveTestItem()
                            {
                                Name = "DiagonalFieldOfViewAngle",
                                LowLimit = recipeConfig.DiagonalFieldOfViewAngleMin,
                                UpLimit = recipeConfig.DiagonalFieldOfViewAngleMax,
                                Value = view1.Result.result.D_Fov,
                                TestValue = view1.Result.result.D_Fov.ToString("F3")
                            };

                            ObjectiveTestResult.W51HorizontalFieldOfViewAngle = new ObjectiveTestItem()
                            {
                                Name = "HorizontalFieldOfViewAngle",
                                LowLimit = recipeConfig.HorizontalFieldOfViewAngleMin,
                                UpLimit = recipeConfig.HorizontalFieldOfViewAngleMax,
                                Value = view1.Result.result.ClolorVisionH_Fov,
                                TestValue = view1.Result.result.ClolorVisionH_Fov.ToString("F3")
                            };
                            ObjectiveTestResult.W51VerticalFieldOfViewAngle = new ObjectiveTestItem()
                            {
                                Name = "VerticalFieldOfViewAngle",
                                LowLimit = recipeConfig.VerticalFieldOfViewAngleMin,
                                UpLimit = recipeConfig.VerticalFieldOfViewAngleMax,
                                Value = view1.Result.result.ClolorVisionV_Fov,
                                TestValue = view1.Result.result.ClolorVisionV_Fov.ToString("F3")
                            };
                            result.ViewReslutW51.DiagonalFieldOfViewAngle = ObjectiveTestResult.W51DiagonalFieldOfViewAngle;
                            result.ViewReslutW51.HorizontalFieldOfViewAngle = ObjectiveTestResult.W51HorizontalFieldOfViewAngle;
                            result.ViewReslutW51.VerticalFieldOfViewAngle = ObjectiveTestResult.W51VerticalFieldOfViewAngle;


                            result.Result = result.Result && ObjectiveTestResult.W51DiagonalFieldOfViewAngle.TestResult;
                            result.Result = result.Result && ObjectiveTestResult.W51HorizontalFieldOfViewAngle.TestResult;
                            result.Result = result.Result && ObjectiveTestResult.W51VerticalFieldOfViewAngle.TestResult;
                        }

                    }
                }
            }

            if (result.Model.Contains("White255"))
            {
                log.Info("正在解析白画面的流程");
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
                    if (AlgResultMaster.ImgFileType == ViewResultAlgType.POI_XYZ)
                    {
                        result.ViewResultWhite.PoiResultCIExyuvDatas = new List<PoiResultCIExyuvData>();
                        List<PoiPointResultModel> POIPointResultModels = PoiPointResultDao.Instance.GetAllByPid(AlgResultMaster.Id);
                        int id = 0;
                        ObjectiveTestResult.W255PoixyuvDatas.Clear();
                        foreach (var item in POIPointResultModels)
                        {
                            PoiResultCIExyuvData poiResultCIExyuvData = new PoiResultCIExyuvData(item) { Id = id++ };
                            ObjectiveTestResult.W255PoixyuvDatas.Add(new PoixyuvData()
                            {
                                Id = poiResultCIExyuvData.Id,
                                Name = poiResultCIExyuvData.Name,
                                CCT = poiResultCIExyuvData.CCT * ObjectiveTestResultFix.BlackCenterCorrelatedColorTemperature,
                                X = poiResultCIExyuvData.X,
                                Y = poiResultCIExyuvData.Y * ObjectiveTestResultFix.W255CenterLunimance,
                                Z =poiResultCIExyuvData.Z,
                                Wave =poiResultCIExyuvData.Wave,
                                x = poiResultCIExyuvData.x * ObjectiveTestResultFix.W255CenterCIE1931ChromaticCoordinatesx,
                                y = poiResultCIExyuvData.y * ObjectiveTestResultFix.W255CenterCIE1931ChromaticCoordinatesy,
                                u = poiResultCIExyuvData.u * ObjectiveTestResultFix.W255CenterCIE1976ChromaticCoordinatesu,
                                v = poiResultCIExyuvData.v * ObjectiveTestResultFix.W255CenterCIE1976ChromaticCoordinatesv
                            });
                            if (item.PoiName == "POI_5")
                            {
                                poiResultCIExyuvData.CCT = poiResultCIExyuvData.CCT * ObjectiveTestResultFix.BlackCenterCorrelatedColorTemperature;
                                poiResultCIExyuvData.Y = poiResultCIExyuvData.Y * ObjectiveTestResultFix.W255CenterLunimance;
                                poiResultCIExyuvData.x = poiResultCIExyuvData.x * ObjectiveTestResultFix.W255CenterCIE1931ChromaticCoordinatesx;
                                poiResultCIExyuvData.y = poiResultCIExyuvData.y * ObjectiveTestResultFix.W255CenterCIE1931ChromaticCoordinatesy;
                                poiResultCIExyuvData.u = poiResultCIExyuvData.u * ObjectiveTestResultFix.W255CenterCIE1976ChromaticCoordinatesu;
                                poiResultCIExyuvData.v = poiResultCIExyuvData.v * ObjectiveTestResultFix.W255CenterCIE1976ChromaticCoordinatesv;



                                var objectiveTestItem = new ObjectiveTestItem()
                                {
                                    Name = "CenterCorrelatedColorTemperature",
                                    TestValue = poiResultCIExyuvData.CCT.ToString(),
                                    Value = poiResultCIExyuvData.CCT,
                                    LowLimit = recipeConfig.CenterCorrelatedColorTemperatureMin,
                                    UpLimit = recipeConfig.CenterCorrelatedColorTemperatureMax
                                };
                                ObjectiveTestResult.BlackCenterCorrelatedColorTemperature = objectiveTestItem;
                                result.ViewResultWhite.CenterCorrelatedColorTemperature = objectiveTestItem;
                                result.Result = result.Result && objectiveTestItem.TestResult;


                                ObjectiveTestResult.W255CenterLunimance = new ObjectiveTestItem()
                                {
                                    Name = "W255CenterLunimance",
                                    LowLimit = recipeConfig.W255CenterLunimanceMin,
                                    UpLimit = recipeConfig.W255CenterLunimanceMax,
                                    Value = poiResultCIExyuvData.Y,
                                    TestValue = poiResultCIExyuvData.Y.ToString("F3") + " nit"
                                };
                                ObjectiveTestResult.W255CenterCIE1931ChromaticCoordinatesx = new ObjectiveTestItem()
                                {
                                    Name = "W255CenterCIE1931ChromaticCoordinatesx",
                                    LowLimit = recipeConfig.W255CenterCIE1931ChromaticCoordinatesxMin,
                                    UpLimit = recipeConfig.W255CenterCIE1931ChromaticCoordinatesxMax,
                                    Value = poiResultCIExyuvData.x,
                                    TestValue = poiResultCIExyuvData.x.ToString("F3")
                                };
                                ObjectiveTestResult.W255CenterCIE1931ChromaticCoordinatesy = new ObjectiveTestItem()
                                {
                                    Name = "W255CenterCIE1931ChromaticCoordinatesy",
                                    LowLimit = recipeConfig.W255CenterCIE1931ChromaticCoordinatesyMin,
                                    UpLimit = recipeConfig.W255CenterCIE1931ChromaticCoordinatesyMax,
                                    Value = poiResultCIExyuvData.y,
                                    TestValue = poiResultCIExyuvData.y.ToString("F3")
                                };
                                ObjectiveTestResult.W255CenterCIE1976ChromaticCoordinatesu = new ObjectiveTestItem()
                                {
                                    Name = "W255CenterCIE1976ChromaticCoordinatesu",
                                    LowLimit = recipeConfig.W255CenterCIE1976ChromaticCoordinatesuMin,
                                    UpLimit = recipeConfig.W255CenterCIE1976ChromaticCoordinatesuMax,
                                    Value = poiResultCIExyuvData.u,
                                    TestValue = poiResultCIExyuvData.u.ToString("F3")
                                };
                                ObjectiveTestResult.W255CenterCIE1976ChromaticCoordinatesv = new ObjectiveTestItem()
                                {
                                    Name = "W255CenterCIE1976ChromaticCoordinatesv",
                                    LowLimit = recipeConfig.W255CenterCIE1976ChromaticCoordinatesvMin,
                                    UpLimit = recipeConfig.W255CenterCIE1976ChromaticCoordinatesvMax,
                                    Value = poiResultCIExyuvData.v,
                                    TestValue=poiResultCIExyuvData.v.ToString("F3")
                                };


                                result.Result = result.Result && ObjectiveTestResult.W255CenterLunimance.TestResult;
                                result.Result = result.Result && ObjectiveTestResult.W255CenterCIE1931ChromaticCoordinatesx.TestResult;
                                result.Result = result.Result && ObjectiveTestResult.W255CenterCIE1931ChromaticCoordinatesy.TestResult;
                                result.Result = result.Result && ObjectiveTestResult.W255CenterCIE1976ChromaticCoordinatesu.TestResult;
                                result.Result = result.Result && ObjectiveTestResult.W255CenterCIE1976ChromaticCoordinatesv.TestResult;


                            }

                            result.ViewResultWhite.PoiResultCIExyuvDatas.Add(poiResultCIExyuvData);
                        }
                    }
                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.ViewResultAlgType.PoiAnalysis)
                    {
                        if (AlgResultMaster.TName.Contains("Luminance_uniformity"))
                        {
                            List<DetailCommonModel> detailCommonModels = DeatilCommonDao.Instance.GetAllByPid(AlgResultMaster.Id);
                            if (detailCommonModels.Count == 1)
                            {
                                PoiAnalysisDetailViewReslut viewReslut = new PoiAnalysisDetailViewReslut(detailCommonModels[0]);

                                viewReslut.PoiAnalysisResult.result.Value = viewReslut.PoiAnalysisResult.result.Value * ObjectiveTestResultFix.W255LuminanceUniformity;
           

                                var LuminanceUniformity = new ObjectiveTestItem()
                                {
                                    Name = "Luminance_uniformity(%)",
                                    TestValue = (viewReslut.PoiAnalysisResult.result.Value * 100).ToString("F3") + "%",
                                    Value = viewReslut.PoiAnalysisResult.result.Value,
                                    LowLimit = recipeConfig.W255LuminanceUniformityMin,
                                    UpLimit = recipeConfig.W255LuminanceUniformityMax,
                                };
                                ObjectiveTestResult.W255LuminanceUniformity = LuminanceUniformity;
                                result.ViewResultWhite.W255LuminanceUniformity = LuminanceUniformity;

                                result.Result = result.Result && LuminanceUniformity.TestResult;

                            }

                        }
                        if (AlgResultMaster.TName.Contains("Color_uniformity"))
                        {
                            List<DetailCommonModel> detailCommonModels = DeatilCommonDao.Instance.GetAllByPid(AlgResultMaster.Id);
                            if (detailCommonModels.Count == 1)
                            {
                                PoiAnalysisDetailViewReslut viewReslut = new PoiAnalysisDetailViewReslut(detailCommonModels[0]);
                                viewReslut.PoiAnalysisResult.result.Value = viewReslut.PoiAnalysisResult.result.Value * ObjectiveTestResultFix.W255ColorUniformity;

                                var ColorUniformity = new ObjectiveTestItem()
                                {
                                    Name = "Color_uniformity",
                                    TestValue = (viewReslut.PoiAnalysisResult.result.Value).ToString("F5"),
                                    Value = viewReslut.PoiAnalysisResult.result.Value,
                                    LowLimit = recipeConfig.W255ColorUniformityMin,
                                    UpLimit = recipeConfig.W255ColorUniformityMax
                                };
                                ObjectiveTestResult.W255ColorUniformity = ColorUniformity;
                                result.ViewResultWhite.W255ColorUniformity = ColorUniformity;
                                result.Result = result.Result && ColorUniformity.TestResult;

                            }
                        }
                    }

                }
            }

            else if (result.Model.Contains("White25"))
            {
                log.Info("正在解析White25画面的流程");
                ObjectiveTestResult.FlowW25TestReslut = true;

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
                        result.ViewResultW25.PoiResultCIExyuvDatas = new List<PoiResultCIExyuvData>();

                        List<PoiPointResultModel> POIPointResultModels = PoiPointResultDao.Instance.GetAllByPid(AlgResultMaster.Id);
                        int id = 0;
                        foreach (var item in POIPointResultModels)
                        {
                            PoiResultCIExyuvData poiResultCIExyuvData = new(item) { Id = id++ };
                            result.ViewResultW25.PoiResultCIExyuvDatas.Add(poiResultCIExyuvData);
                        }
                        if (result.ViewResultW25.PoiResultCIExyuvDatas.Count == 1)
                        {
                            result.ViewResultW25.PoiResultCIExyuvDatas[0].Y = result.ViewResultW25.PoiResultCIExyuvDatas[0].Y * ObjectiveTestResultFix.W25CenterLunimance;
                            result.ViewResultW25.PoiResultCIExyuvDatas[0].x = result.ViewResultW25.PoiResultCIExyuvDatas[0].x * ObjectiveTestResultFix.W25CenterCIE1931ChromaticCoordinatesx;
                            result.ViewResultW25.PoiResultCIExyuvDatas[0].y = result.ViewResultW25.PoiResultCIExyuvDatas[0].y * ObjectiveTestResultFix.W25CenterCIE1931ChromaticCoordinatesy;
                            result.ViewResultW25.PoiResultCIExyuvDatas[0].u = result.ViewResultW25.PoiResultCIExyuvDatas[0].u * ObjectiveTestResultFix.W25CenterCIE1976ChromaticCoordinatesu;
                            result.ViewResultW25.PoiResultCIExyuvDatas[0].v = result.ViewResultW25.PoiResultCIExyuvDatas[0].v * ObjectiveTestResultFix.W25CenterCIE1976ChromaticCoordinatesv;


                            ObjectiveTestResult.W25CenterLunimance = new ObjectiveTestItem()
                            {
                                Name = "W25CenterLunimance",
                                LowLimit = recipeConfig.W25CenterLunimanceMin,
                                UpLimit = recipeConfig.W25CenterLunimanceMax,
                                Value = result.ViewResultW25.PoiResultCIExyuvDatas[0].Y,
                                TestValue = result.ViewResultW25.PoiResultCIExyuvDatas[0].Y.ToString("F3") + " nit"
                            };
                            ObjectiveTestResult.W25CenterCIE1931ChromaticCoordinatesx = new ObjectiveTestItem()
                            {
                                Name = "W25CenterCIE1931ChromaticCoordinatesx",
                                LowLimit = recipeConfig.W25CenterCIE1931ChromaticCoordinatesxMin,
                                UpLimit = recipeConfig.W25CenterCIE1931ChromaticCoordinatesxMax,
                                Value = result.ViewResultW25.PoiResultCIExyuvDatas[0].x,
                                TestValue = result.ViewResultW25.PoiResultCIExyuvDatas[0].x.ToString("F3")
                            };
                            ObjectiveTestResult.W25CenterCIE1931ChromaticCoordinatesy = new ObjectiveTestItem()
                            {
                                Name = "W25CenterCIE1931ChromaticCoordinatesy",
                                LowLimit = recipeConfig.W25CenterCIE1931ChromaticCoordinatesyMin,
                                UpLimit = recipeConfig.W25CenterCIE1931ChromaticCoordinatesyMax,
                                Value = result.ViewResultW25.PoiResultCIExyuvDatas[0].y,
                                TestValue = result.ViewResultW25.PoiResultCIExyuvDatas[0].y.ToString("F3")
                            };
                            ObjectiveTestResult.W25CenterCIE1976ChromaticCoordinatesu = new ObjectiveTestItem()
                            {
                                Name = "W25CenterCIE1976ChromaticCoordinatesu",
                                LowLimit = recipeConfig.W25CenterCIE1976ChromaticCoordinatesuMin,
                                UpLimit = recipeConfig.W25CenterCIE1976ChromaticCoordinatesuMax,
                                Value = result.ViewResultW25.PoiResultCIExyuvDatas[0].u,
                                TestValue = result.ViewResultW25.PoiResultCIExyuvDatas[0].u.ToString("F3")
                            };
                            ObjectiveTestResult.W25CenterCIE1976ChromaticCoordinatesv = new ObjectiveTestItem()
                            {
                                Name = "W25CenterCIE1976ChromaticCoordinatesv",
                                LowLimit = recipeConfig.W25CenterCIE1976ChromaticCoordinatesvMin,
                                UpLimit = recipeConfig.W25CenterCIE1976ChromaticCoordinatesvMax,
                                Value = result.ViewResultW25.PoiResultCIExyuvDatas[0].v,
                                TestValue = result.ViewResultW25.PoiResultCIExyuvDatas[0].v.ToString("F3")
                            };


                            result.Result = result.Result && ObjectiveTestResult.W25CenterLunimance.TestResult;
                            result.Result = result.Result && ObjectiveTestResult.W25CenterCIE1931ChromaticCoordinatesx.TestResult;
                            result.Result = result.Result && ObjectiveTestResult.W25CenterCIE1931ChromaticCoordinatesy.TestResult;
                            result.Result = result.Result && ObjectiveTestResult.W25CenterCIE1976ChromaticCoordinatesu.TestResult;
                            result.Result = result.Result && ObjectiveTestResult.W25CenterCIE1976ChromaticCoordinatesv.TestResult;

                        }
                    }


                }

            }

            else if (result.Model.Contains("Black"))
            {
                log.Info("正在解析黑画面的流程");
                ObjectiveTestResult.FlowBlackTestReslut = true;
                if (ViewResluts.FirstOrDefault(a => a.SN == ProjectARVRLiteConfig.Instance.SN) is ProjectARVRReuslt result1)
                {
                    result.ViewResultWhite =result1.ViewResultWhite;
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

                        if (result.ViewResultWhite != null && result.ViewResultWhite.PoiResultCIExyuvDatas != null && result.ViewResultWhite.PoiResultCIExyuvDatas.Count == 9 && result.ViewResultBlack.PoiResultCIExyuvDatas.Count == 1)
                        {
                            var contrast1 = result.ViewResultWhite.PoiResultCIExyuvDatas[5].Y / result.ViewResultBlack.PoiResultCIExyuvDatas[0].Y;

                            contrast1 = contrast1 * ObjectiveTestResultFix.FOFOContrast;

                            var FOFOContrast = new ObjectiveTestItem()
                            {
                                Name = "FOFOContrast",
                                LowLimit = recipeConfig.FOFOContrastMin,
                                UpLimit = recipeConfig.FOFOContrastMax,
                                Value = contrast1,
                                TestValue = contrast1.ToString("F2")
                            };

                            ObjectiveTestResult.FOFOContrast = FOFOContrast;
                            result.ViewResultBlack.FOFOContrast = FOFOContrast;
                            result.Result = result.Result && FOFOContrast.TestResult;
                        }
                        else
                        {
                            log.Info($"计算对比度前需要白画面的亮度");
                        }

                    }
                }
            }
            else if (result.Model.Contains("Chessboard"))
            {
                log.Info("正在解析棋盘格画面的流程");
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
                                    LowLimit = recipeConfig.ChessboardContrastMin,
                                    UpLimit = recipeConfig.ChessboardContrastMax,
                                    Value = viewReslut.PoiAnalysisResult.result.Value,
                                    TestValue = ChessboardContrast.ToString("F3")
                                };

                                result.ViewReslutCheckerboard.ChessboardContrast = ObjectiveTestResult.ChessboardContrast;
                                result.Result = result.Result && ObjectiveTestResult.ChessboardContrast.TestResult;

                            }
                        }
                    }

                }
            }
            else if (result.Model.Contains("MTF_HV"))
            {
                log.Info("正在解析MTF_HV画面的流程");
                ObjectiveTestResult.FlowMTFHVTestReslut = true;

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

                            foreach (var mtf in mtfresults.MTFResult.resultChild)
                            {
                                if (mtf.name == "Center_0F")
                                {
                                    mtf.horizontalAverage = mtf.horizontalAverage * ObjectiveTestResultFix.MTF_HV_H_Center_0F;
                                    mtf.verticalAverage = mtf.verticalAverage * ObjectiveTestResultFix.MTF_HV_V_Center_0F;

                                    ObjectiveTestResult.MTF_HV_H_Center_0F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_H_Center_0F",
                                        LowLimit = recipeConfig.MTF_HV_H_Center_0FMin,
                                        UpLimit = recipeConfig.MTF_HV_H_Center_0FMax,
                                        Value = mtf.horizontalAverage,
                                        TestValue = mtf.horizontalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_H_Center_0F.TestResult;

                                    ObjectiveTestResult.MTF_HV_V_Center_0F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_V_Center_0F",
                                        LowLimit = recipeConfig.MTF_HV_V_Center_0FMin,
                                        UpLimit = recipeConfig.MTF_HV_V_Center_0FMax,
                                        Value = mtf.verticalAverage,
                                        TestValue = mtf.verticalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_V_Center_0F.TestResult;
                                }

                                if (mtf.name == "LeftUp_0.4F")
                                {
                                    mtf.horizontalAverage = mtf.horizontalAverage * ObjectiveTestResultFix.MTF_HV_H_LeftUp_0_4F;
                                    mtf.verticalAverage = mtf.verticalAverage * ObjectiveTestResultFix.MTF_HV_V_LeftUp_0_4F;

                                    ObjectiveTestResult.MTF_HV_H_LeftUp_0_4F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_H_LeftUp_0_4F",
                                        LowLimit = recipeConfig.MTF_HV_H_LeftUp_0_4FMin,
                                        UpLimit = recipeConfig.MTF_HV_H_LeftUp_0_4FMax,
                                        Value = mtf.horizontalAverage,
                                        TestValue = mtf.horizontalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_H_LeftUp_0_4F.TestResult;

                                    ObjectiveTestResult.MTF_HV_V_LeftUp_0_4F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_V_LeftUp_0_4F",
                                        LowLimit = recipeConfig.MTF_HV_V_LeftUp_0_4FMin,
                                        UpLimit = recipeConfig.MTF_HV_V_LeftUp_0_4FMax,
                                        Value = mtf.verticalAverage,
                                        TestValue = mtf.verticalAverage.ToString()
                                    };

                                }

                                if (mtf.name == "RightUp_0.4F")
                                {
                                    mtf.horizontalAverage = mtf.horizontalAverage * ObjectiveTestResultFix.MTF_HV_H_RightUp_0_4F;
                                    mtf.verticalAverage = mtf.verticalAverage * ObjectiveTestResultFix.MTF_HV_V_RightUp_0_4F;

                                    ObjectiveTestResult.MTF_HV_H_RightUp_0_4F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_H_RightUp_0_4F",
                                        LowLimit = recipeConfig.MTF_HV_H_RightUp_0_4FMin,
                                        UpLimit = recipeConfig.MTF_HV_H_RightUp_0_4FMax,
                                        Value = mtf.horizontalAverage,
                                        TestValue = mtf.horizontalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_H_RightUp_0_4F.TestResult;
                                    ObjectiveTestResult.MTF_HV_V_RightUp_0_4F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_V_RightUp_0_4F",
                                        LowLimit = recipeConfig.MTF_HV_V_RightUp_0_4FMin,
                                        UpLimit = recipeConfig.MTF_HV_V_RightUp_0_4FMax,
                                        Value = mtf.verticalAverage,
                                        TestValue = mtf.verticalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_V_RightUp_0_4F.TestResult;

                                }
                                if (mtf.name == "LeftDown_0.4F")
                                {
                                    mtf.horizontalAverage = mtf.horizontalAverage * ObjectiveTestResultFix.MTF_HV_H_LeftDown_0_4F;
                                    mtf.verticalAverage = mtf.verticalAverage * ObjectiveTestResultFix.MTF_HV_V_LeftDown_0_4F;

                                    ObjectiveTestResult.MTF_HV_H_LeftDown_0_4F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_H_LeftDown_0_4F",
                                        LowLimit = recipeConfig.MTF_HV_H_LeftDown_0_4FMin,
                                        UpLimit = recipeConfig.MTF_HV_H_LeftDown_0_4FMax,
                                        Value = mtf.horizontalAverage,
                                        TestValue = mtf.horizontalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_H_LeftDown_0_4F.TestResult;
                                    ObjectiveTestResult.MTF_HV_V_LeftDown_0_4F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_V_LeftDown_0_4F",
                                        LowLimit = recipeConfig.MTF_HV_V_LeftDown_0_4FMin,
                                        UpLimit = recipeConfig.MTF_HV_V_LeftDown_0_4FMax,
                                        Value = mtf.verticalAverage,
                                        TestValue = mtf.verticalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_V_LeftDown_0_4F.TestResult;
                                }
                                if (mtf.name == "RightDown_0.4F")
                                {
                                    mtf.horizontalAverage = mtf.horizontalAverage * ObjectiveTestResultFix.MTF_HV_H_RightDown_0_4F;
                                    mtf.verticalAverage = mtf.verticalAverage * ObjectiveTestResultFix.MTF_HV_V_RightDown_0_4F;

                                    ObjectiveTestResult.MTF_HV_H_RightDown_0_4F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_H_RightDown_0_4F",
                                        LowLimit = recipeConfig.MTF_HV_H_RightDown_0_4FMin,
                                        UpLimit = recipeConfig.MTF_HV_H_RightDown_0_4FMax,
                                        Value = mtf.horizontalAverage,
                                        TestValue = mtf.horizontalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_H_RightDown_0_4F.TestResult;
                                    ObjectiveTestResult.MTF_HV_V_RightDown_0_4F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_V_RightDown_0_4F",
                                        LowLimit = recipeConfig.MTF_HV_V_RightDown_0_4FMin,
                                        UpLimit = recipeConfig.MTF_HV_V_RightDown_0_4FMax,
                                        Value = mtf.verticalAverage,
                                        TestValue = mtf.verticalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_V_RightDown_0_4F.TestResult;
                                }

                                if (mtf.name == "LeftUp_0.8F")
                                {
                                    mtf.horizontalAverage = mtf.horizontalAverage * ObjectiveTestResultFix.MTF_HV_H_LeftUp_0_8F;
                                    mtf.verticalAverage = mtf.verticalAverage * ObjectiveTestResultFix.MTF_HV_V_LeftUp_0_8F;

                                    ObjectiveTestResult.MTF_HV_H_LeftUp_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_H_LeftUp_0_8F",
                                        LowLimit = recipeConfig.MTF_HV_H_LeftUp_0_8FMin,
                                        UpLimit = recipeConfig.MTF_HV_H_LeftUp_0_8FMax,
                                        Value = mtf.horizontalAverage,
                                        TestValue = mtf.horizontalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_H_LeftUp_0_8F.TestResult;
                                    ObjectiveTestResult.MTF_HV_V_LeftUp_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_V_LeftUp_0_8F",
                                        LowLimit = recipeConfig.MTF_HV_V_LeftUp_0_8FMin,
                                        UpLimit = recipeConfig.MTF_HV_V_LeftUp_0_8FMax,
                                        Value = mtf.verticalAverage,
                                        TestValue = mtf.verticalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_V_LeftUp_0_8F.TestResult;
                                }
                                if (mtf.name == "RightUp_0.8F")
                                {
                                    mtf.horizontalAverage = mtf.horizontalAverage * ObjectiveTestResultFix.MTF_HV_H_RightUp_0_8F;
                                    mtf.verticalAverage = mtf.verticalAverage * ObjectiveTestResultFix.MTF_HV_V_RightUp_0_8F;

                                    ObjectiveTestResult.MTF_HV_H_RightUp_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_H_RightUp_0_8F",
                                        LowLimit = recipeConfig.MTF_HV_H_RightUp_0_8FMin,
                                        UpLimit = recipeConfig.MTF_HV_H_RightUp_0_8FMax,
                                        Value = mtf.horizontalAverage,
                                        TestValue = mtf.horizontalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_H_RightUp_0_8F.TestResult;
                                    ObjectiveTestResult.MTF_HV_V_RightUp_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_V_RightUp_0_8F",
                                        LowLimit = recipeConfig.MTF_HV_V_RightUp_0_8FMin,
                                        UpLimit = recipeConfig.MTF_HV_V_RightUp_0_8FMax,
                                        Value = mtf.verticalAverage,
                                        TestValue = mtf.verticalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_V_RightUp_0_8F.TestResult;
                                }
                                if (mtf.name == "LeftDown_0.8F")
                                {
                                    mtf.horizontalAverage = mtf.horizontalAverage * ObjectiveTestResultFix.MTF_HV_H_LeftDown_0_8F;
                                    mtf.verticalAverage = mtf.verticalAverage * ObjectiveTestResultFix.MTF_HV_V_LeftDown_0_8F;

                                    ObjectiveTestResult.MTF_HV_H_LeftDown_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_H_LeftDown_0_8F",
                                        LowLimit = recipeConfig.MTF_HV_H_LeftDown_0_8FMin,
                                        UpLimit = recipeConfig.MTF_HV_H_LeftDown_0_8FMax,
                                        Value = mtf.horizontalAverage,
                                        TestValue = mtf.horizontalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_H_LeftDown_0_8F.TestResult;
                                    ObjectiveTestResult.MTF_HV_V_LeftDown_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_V_LeftDown_0_8F",
                                        LowLimit = recipeConfig.MTF_HV_V_LeftDown_0_8FMin,
                                        UpLimit = recipeConfig.MTF_HV_V_LeftDown_0_8FMax,
                                        Value = mtf.verticalAverage,
                                        TestValue = mtf.verticalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_V_LeftDown_0_8F.TestResult;
                                }
                                if (mtf.name == "RightDown_0.8F")
                                {
                                    mtf.horizontalAverage = mtf.horizontalAverage * ObjectiveTestResultFix.MTF_HV_H_RightDown_0_8F;
                                    mtf.verticalAverage = mtf.verticalAverage * ObjectiveTestResultFix.MTF_HV_V_RightDown_0_8F;

                                    ObjectiveTestResult.MTF_HV_H_RightDown_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_H_RightDown_0_8F",
                                        LowLimit = recipeConfig.MTF_HV_H_RightDown_0_8FMin,
                                        UpLimit = recipeConfig.MTF_HV_H_RightDown_0_8FMax,
                                        Value = mtf.horizontalAverage,
                                        TestValue = mtf.horizontalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_H_RightDown_0_8F.TestResult;
                                    ObjectiveTestResult.MTF_HV_V_RightDown_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_V_RightDown_0_8F",
                                        LowLimit = recipeConfig.MTF_HV_V_RightDown_0_8FMin,
                                        UpLimit = recipeConfig.MTF_HV_V_RightDown_0_8FMax,
                                        Value = mtf.verticalAverage,
                                        TestValue = mtf.verticalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_V_RightDown_0_8F.TestResult;
                                }
                            }
                            result.ViewRelsultMTFH.MTFDetailViewReslut = mtfresults;
                        }
                        try
                        {
                            string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                            string filePath = Path.Combine(ViewResultManager.Config.SavePathCsv, $"MTF_H_{timeStr}.csv");
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
            else if (result.Model.Contains("Distortion"))
            {
                log.Info("正在解析Distortion画面的流程");
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
                            ColorVision.Engine.Templates.Jsons.Distortion2.Distortion2View Distortion2View = new ColorVision.Engine.Templates.Jsons.Distortion2.Distortion2View(AlgResultModels[0]);

                            Distortion2View.DistortionReslut.TVDistortion.HorizontalRatio = Distortion2View.DistortionReslut.TVDistortion.HorizontalRatio * ObjectiveTestResultFix.HorizontalTVDistortion;
                            Distortion2View.DistortionReslut.TVDistortion.VerticalRatio = Distortion2View.DistortionReslut.TVDistortion.VerticalRatio * ObjectiveTestResultFix.VerticalTVDistortion;


                            result.ViewReslutDistortionGhost.Distortion2View = Distortion2View;

                            ObjectiveTestResult.HorizontalTVDistortion = new ObjectiveTestItem()
                            {
                                Name = "HorizontalTVDistortion",
                                LowLimit = recipeConfig.HorizontalTVDistortionMin,
                                UpLimit = recipeConfig.HorizontalTVDistortionMax,
                                Value = Distortion2View.DistortionReslut.TVDistortion.HorizontalRatio,
                                TestValue = Distortion2View.DistortionReslut.TVDistortion.HorizontalRatio.ToString("F5") +"%"
                            };

                            ObjectiveTestResult.VerticalTVDistortion = new ObjectiveTestItem()
                            {
                                Name = "VerticalTVDistortion",
                                LowLimit = recipeConfig.VerticalTVDistortionMin,
                                UpLimit = recipeConfig.VerticalTVDistortionMax,
                                Value = Distortion2View.DistortionReslut.TVDistortion.VerticalRatio,
                                TestValue = Distortion2View.DistortionReslut.TVDistortion.VerticalRatio.ToString("F5") + "%"
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
                ObjectiveTestResult.FlowOpticCenterTestReslut = true;

                var values = MeasureImgResultDao.Instance.GetAllByBatchId(Batch.Id);
                if (values.Count > 0)
                {
                    result.FileName = values[0].FileUrl;
                }
                var AlgResultMasterlists = AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id);


                foreach (var AlgResultMaster in AlgResultMasterlists)
                {
                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.ViewResultAlgType.FindCross )
                    {
                        log.Info(AlgResultMaster.Id);
                        List<DetailCommonModel> detailCommonModels = DeatilCommonDao.Instance.GetAllByPid(AlgResultMaster.Id);
                        if (detailCommonModels.Count == 1)
                        {
                            FindCrossDetailViewReslut findresult = new FindCrossDetailViewReslut(detailCommonModels[0]);

                            if (AlgResultMaster.TName == "optCenter")
                            {
                                findresult.FindCrossResult.result[0].tilt.tilt_x = findresult.FindCrossResult.result[0].tilt.tilt_x * ObjectiveTestResultFix.OptCenterXTilt;
                                findresult.FindCrossResult.result[0].tilt.tilt_y = findresult.FindCrossResult.result[0].tilt.tilt_y * ObjectiveTestResultFix.OptCenterYTilt;
                                findresult.FindCrossResult.result[0].rotationAngle = findresult.FindCrossResult.result[0].rotationAngle * ObjectiveTestResultFix.OptCenterRotation;


                                result.ViewResultOpticCenter.FindCrossDetailViewReslut = findresult;
                                ObjectiveTestResult.OptCenterXTilt = new ObjectiveTestItem()
                                {
                                    Name = "OptCenterXTilt",
                                    LowLimit = recipeConfig.OptCenterXTiltMin,
                                    UpLimit = recipeConfig.OptCenterXTiltMax,
                                    Value = findresult.FindCrossResult.result[0].tilt.tilt_x,
                                    TestValue = findresult.FindCrossResult.result[0].tilt.tilt_x.ToString("F4")
                                };
                                ObjectiveTestResult.OptCenterYTilt = new ObjectiveTestItem()
                                {
                                    Name = "OptCenterYTilt",
                                    LowLimit = recipeConfig.OptCenterYTiltMin,
                                    UpLimit = recipeConfig.OptCenterYTiltMax,
                                    Value = findresult.FindCrossResult.result[0].tilt.tilt_y,
                                    TestValue = findresult.FindCrossResult.result[0].tilt.tilt_y.ToString("F4")
                                };

                                ObjectiveTestResult.OptCenterRotation = new ObjectiveTestItem()
                                {
                                    Name = "OptCenterRotation",
                                    LowLimit = recipeConfig.OptCenterRotationMin,
                                    UpLimit = recipeConfig.OptCenterRotationMax,
                                    Value = findresult.FindCrossResult.result[0].rotationAngle,
                                    TestValue = findresult.FindCrossResult.result[0].rotationAngle.ToString("F4")
                                };

                                result.ViewResultOpticCenter.OptCenterXTilt = ObjectiveTestResult.OptCenterXTilt;
                                result.ViewResultOpticCenter.OptCenterYTilt = ObjectiveTestResult.OptCenterYTilt;
                                result.ViewResultOpticCenter.OptCenterRotation = ObjectiveTestResult.OptCenterRotation;

                                 result.Result = result.Result && ObjectiveTestResult.OptCenterXTilt.TestResult;
                                 result.Result = result.Result && ObjectiveTestResult.OptCenterYTilt.TestResult;
                                 result.Result = result.Result && ObjectiveTestResult.OptCenterRotation.TestResult;
                            }
                            if (AlgResultMaster.TName == "ImageCenter")
                            {

                                findresult.FindCrossResult.result[0].tilt.tilt_x = findresult.FindCrossResult.result[0].tilt.tilt_x * ObjectiveTestResultFix.ImageCenterXTilt;
                                findresult.FindCrossResult.result[0].tilt.tilt_y = findresult.FindCrossResult.result[0].tilt.tilt_y * ObjectiveTestResultFix.ImageCenterYTilt;
                                findresult.FindCrossResult.result[0].rotationAngle = findresult.FindCrossResult.result[0].rotationAngle * ObjectiveTestResultFix.ImageCenterRotation;

                                result.ViewResultOpticCenter.FindCrossDetailViewReslut1 = findresult;
                                ObjectiveTestResult.ImageCenterXTilt = new ObjectiveTestItem()
                                {
                                    Name = "ImageCenterXTilt",
                                    LowLimit = recipeConfig.ImageCenterXTiltMin,
                                    UpLimit = recipeConfig.ImageCenterXTiltMax,
                                    Value = findresult.FindCrossResult.result[0].tilt.tilt_x,
                                    TestValue = findresult.FindCrossResult.result[0].tilt.tilt_x.ToString("F4")
                                };
                                ObjectiveTestResult.ImageCenterYTilt = new ObjectiveTestItem()
                                {
                                    Name = "ImageCenterYTilt",
                                    LowLimit = recipeConfig.ImageCenterYTiltMin,
                                    UpLimit = recipeConfig.ImageCenterYTiltMax,
                                    Value = findresult.FindCrossResult.result[0].tilt.tilt_y,
                                    TestValue = findresult.FindCrossResult.result[0].tilt.tilt_y.ToString("F4")
                                };

                                ObjectiveTestResult.ImageCenterRotation = new ObjectiveTestItem()
                                {
                                    Name = "ImageCenterRotation",
                                    LowLimit = recipeConfig.ImageCenterRotationMin,
                                    UpLimit = recipeConfig.ImageCenterRotationMax,
                                    Value = findresult.FindCrossResult.result[0].rotationAngle,
                                    TestValue = findresult.FindCrossResult.result[0].rotationAngle.ToString("F4")
                                };

                                result.ViewResultOpticCenter.ImageCenterXTilt = ObjectiveTestResult.ImageCenterXTilt;
                                result.ViewResultOpticCenter.ImageCenterYTilt = ObjectiveTestResult.ImageCenterYTilt;
                                result.ViewResultOpticCenter.ImageCenterRotation = ObjectiveTestResult.ImageCenterRotation;

                                result.Result = result.Result && ObjectiveTestResult.ImageCenterXTilt.TestResult;
                                result.Result = result.Result && ObjectiveTestResult.ImageCenterYTilt.TestResult;
                                result.Result = result.Result && ObjectiveTestResult.ImageCenterRotation.TestResult;
                            }

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
            var values = Enum.GetValues(typeof(ARVR1TestType));
            int currentIndex = Array.IndexOf(values, CurrentTestType);
            int nextIndex = (currentIndex + 1) % values.Length;
            // 跳过 None（假设 None 是第一个）
            if ((ARVR1TestType)values.GetValue(nextIndex) == ARVR1TestType.None)
                nextIndex = (nextIndex + 1) % values.Length;
            ARVR1TestType aRVRTestType = (ARVR1TestType)values.GetValue(nextIndex);

            return aRVRTestType >= ProjectConfig.TestTypeCompleted;
        }

        private void SwitchPG()
        {
            if (SocketManager.GetInstance().TcpClients.Count <= 0 || SocketControl.Current.Stream == null)
            {
                log.Info("找不到连接的Socket");
                return;
            }
            log.Info("Socket已经链接 ");

            var values = Enum.GetValues(typeof(ARVR1TestType));
            int currentIndex = Array.IndexOf(values, CurrentTestType);
            int nextIndex = (currentIndex + 1) % values.Length;
            // 跳过 None（假设 None 是第一个）
            if ((ARVR1TestType)values.GetValue(nextIndex) == ARVR1TestType.None)
                nextIndex = (nextIndex + 1) % values.Length;
            ARVR1TestType aRVRTestType = (ARVR1TestType)values.GetValue(nextIndex);

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
            ObjectiveTestResult.TotalResult = ObjectiveTestResult.TotalResult && ObjectiveTestResult.FlowW51TestReslut;
            ObjectiveTestResult.TotalResult = ObjectiveTestResult.TotalResult && ObjectiveTestResult.FlowWhiteTestReslut;
            ObjectiveTestResult.TotalResult = ObjectiveTestResult.TotalResult && ObjectiveTestResult.FlowBlackTestReslut;
            ObjectiveTestResult.TotalResult = ObjectiveTestResult.TotalResult && ObjectiveTestResult.FlowW25TestReslut;
            ObjectiveTestResult.TotalResult = ObjectiveTestResult.TotalResult && ObjectiveTestResult.FlowChessboardTestReslut;
            ObjectiveTestResult.TotalResult = ObjectiveTestResult.TotalResult && ObjectiveTestResult.FlowMTFHVTestReslut;
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
            ViewResultManager.Config.Height = row2.ActualHeight;
            row2.Height = GridLength.Auto;
        }

        private void Button_Click_Clear(object sender, RoutedEventArgs e)
        {
            ViewResluts.Clear();
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
                            using (var fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
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
                            await Task.Delay(ViewResultManager.Config.ViewImageReadDelay);
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

                if (result.FlowStatus != FlowStatus.Completed)
                    return;

                if (result.TestType == ARVR1TestType.W51)
                {
                    DVPolygon polygon = new DVPolygon();
                    List<System.Windows.Point> point1s = new List<System.Windows.Point>();
                    foreach (var item in result.ViewReslutW51.AlgResultLightAreaModels)
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
                }

                if (result.TestType == ARVR1TestType.White)
                {
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

                if (result.TestType == ARVR1TestType.W25)
                {
                    foreach (var poiResultCIExyuvData in result.ViewResultW25.PoiResultCIExyuvDatas)
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

                if (result.TestType == ARVR1TestType.Black)
                {

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

                if (result.TestType == ARVR1TestType.Chessboard)
                {
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

                if (result.TestType == ARVR1TestType.MTFHV)
                {

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
                            Rectangle.Attribute.Text = item.name + "_"+ item.id;
                            Rectangle.Attribute.Msg = item.mtfValue.ToString();
                            Rectangle.Render();
                            ImageView.AddVisual(Rectangle);
                        }
                    }
                }

                if (result.TestType == ARVR1TestType.Distortion)
                {
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
            outtext += $"Model:{result.Model}  SN:{result.SN}  {DateTime.Now:yyyy/MM//dd HH:mm:ss}";
            Run run = new Run(outtext);
            run.Foreground = result.Result ? Brushes.Black : Brushes.White;
            run.FontSize += 1;

            var paragraph = new Paragraph();
            paragraph.Inlines.Add(run);
            outputText.Document.Blocks.Add(paragraph);
            outtext = string.Empty;

            switch (result.TestType)
            {
                case ARVR1TestType.W51:
                    outtext += $"白画面绿图W51 测试项：自动AA区域定位算法+FOV算法" + Environment.NewLine;

                    outtext += $"发光区角点：" + Environment.NewLine;
                    if (result.ViewReslutW51.AlgResultLightAreaModels != null)
                    {
                        foreach (var item in result.ViewReslutW51.AlgResultLightAreaModels)
                        {
                            outtext += $"{item.PosX},{item.PosY}" + Environment.NewLine;
                        }
                    }

                    outtext += $"DiagonalFieldOfViewAngle:{result.ViewReslutW51.DiagonalFieldOfViewAngle.TestValue}  LowLimit:{result.ViewReslutW51.DiagonalFieldOfViewAngle.LowLimit} UpLimit:{result.ViewReslutW51.DiagonalFieldOfViewAngle.UpLimit},Rsult{(result.ViewReslutW51.DiagonalFieldOfViewAngle.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                    outtext += $"HorizontalFieldOfViewAngle:{result.ViewReslutW51.HorizontalFieldOfViewAngle.TestValue} LowLimit:{result.ViewReslutW51.HorizontalFieldOfViewAngle.LowLimit} UpLimit:{result.ViewReslutW51.HorizontalFieldOfViewAngle.UpLimit} ,Rsult{(result.ViewReslutW51.HorizontalFieldOfViewAngle.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                    outtext += $"VerticalFieldOfViewAngle:{result.ViewReslutW51.VerticalFieldOfViewAngle.TestValue} LowLimit:{result.ViewReslutW51.VerticalFieldOfViewAngle.LowLimit} UpLimit:{result.ViewReslutW51.VerticalFieldOfViewAngle.UpLimit},Rsult{(result.ViewReslutW51.VerticalFieldOfViewAngle.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                    break;
                case ARVR1TestType.White:
                    outtext += $"白画面九点圆 测试项：关注点算法+亮度均匀性+颜色均匀性算法+" + Environment.NewLine;

                    if (result.ViewResultWhite.PoiResultCIExyuvDatas != null)
                    {
                        foreach (var item in result.ViewResultWhite.PoiResultCIExyuvDatas)
                        {
                            outtext += $"X:{item.X.ToString("F2")} Y:{item.Y.ToString("F2")} Z:{item.Z.ToString("F2")} x:{item.x.ToString("F2")} y:{item.y.ToString("F2")} u:{item.u.ToString("F2")} v:{item.v.ToString("F2")} cct:{item.CCT.ToString("F2")} wave:{item.Wave.ToString("F2")}{Environment.NewLine}";
                        }
                    }

                    outtext += $"CenterCorrelatedColorTemperature:{result.ViewResultWhite.CenterCorrelatedColorTemperature.TestValue}  LowLimit:{result.ViewResultWhite.CenterCorrelatedColorTemperature.LowLimit} UpLimit:{result.ViewResultWhite.CenterCorrelatedColorTemperature.UpLimit},Rsult{(result.ViewResultWhite.CenterCorrelatedColorTemperature.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                    outtext += $"Luminance_uniformity:{result.ViewResultWhite.W255LuminanceUniformity.TestValue} LowLimit:{result.ViewResultWhite.W255LuminanceUniformity.LowLimit}  UpLimit:{result.ViewResultWhite.W255LuminanceUniformity.UpLimit},Rsult{(result.ViewResultWhite.W255LuminanceUniformity.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                    outtext += $"Color_uniformity:{result.ViewResultWhite.W255ColorUniformity.TestValue} LowLimit:{result.ViewResultWhite.W255ColorUniformity.LowLimit} UpLimit:{result.ViewResultWhite.W255ColorUniformity.UpLimit},Rsult{(result.ViewResultWhite.W255ColorUniformity.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";

                    break;
                case ARVR1TestType.Black:
                    outtext += $"黑画面 测试项：自动AA区域定位算法+关注点算法+序列对比度算法(中心亮度比值)" + Environment.NewLine;
                    if (result.ViewResultBlack.PoiResultCIExyuvDatas != null)
                    {
                        foreach (var item in result.ViewResultBlack.PoiResultCIExyuvDatas)
                        {
                            outtext += $"{item.Name}  X:{item.X.ToString("F2")} Y:{item.Y.ToString("F2")} Z:{item.Z.ToString("F2")} x:{item.x.ToString("F2")} y:{item.y.ToString("F2")} u:{item.u.ToString("F2")} v:{item.v.ToString("F2")} cct:{item.CCT.ToString("F2")} wave:{item.Wave.ToString("F2")}{Environment.NewLine}";
                        }
                    }

                    outtext += $"FOFOContrast:{result.ViewResultBlack.FOFOContrast.TestValue}  LowLimit:{result.ViewResultBlack.FOFOContrast.LowLimit} UpLimit:{result.ViewResultBlack.FOFOContrast.UpLimit},Rsult{(result.ViewResultBlack.FOFOContrast.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                    break;
                case ARVR1TestType.W25:
                    outtext += $"W25 测试项：自动AA区域定位算法+关注点算法+序列对比度算法(中心亮度比值)" + Environment.NewLine;
                    if (result.ViewResultW25.PoiResultCIExyuvDatas != null)
                    {
                        foreach (var item in result.ViewResultW25.PoiResultCIExyuvDatas)
                        {
                            outtext += $"{item.Name}  X:{item.X.ToString("F2")} Y:{item.Y.ToString("F2")} Z:{item.Z.ToString("F2")} x:{item.x.ToString("F2")} y:{item.y.ToString("F2")} u:{item.u.ToString("F2")} v:{item.v.ToString("F2")} cct:{item.CCT.ToString("F2")} wave:{item.Wave.ToString("F2")}{Environment.NewLine}";
                        }
                    }
                    break;
                case ARVR1TestType.MTFHV:
                    outtext += $"水平MTF 测试项：自动AA区域定位算法+MTFHV算法" + Environment.NewLine;

                    outtext += $"name,horizontalAverage,verticalAverage,Average," + Environment.NewLine;

                    if (result.ViewRelsultMTFH.MTFDetailViewReslut.MTFResult != null)
                    {
                        foreach (var item in result.ViewRelsultMTFH.MTFDetailViewReslut.MTFResult.resultChild)
                        {
                            outtext += $"{item.name},{item.horizontalAverage},{item.verticalAverage},{item.Average}" + Environment.NewLine;
                        }
                    }

                    break;
                case ARVR1TestType.Distortion:
                    outtext += $"畸变鬼影 测试项：自动AA区域定位算法+畸变算法+鬼影算法" + Environment.NewLine;

                    foreach (var item in result.ViewReslutDistortionGhost.Distortion2View.DistortionReslut.TVDistortion.FinalPoints)
                    {
                        outtext += $"id:{item.Id} X:{item.X} Y:{item.Y}" + Environment.NewLine;
                    }
                    outtext += $"HorizontalTVDistortion:{result.ViewReslutDistortionGhost.HorizontalTVDistortion.TestValue} LowLimit:{result.ViewReslutDistortionGhost.HorizontalTVDistortion.LowLimit}  UpLimit:{result.ViewReslutDistortionGhost.HorizontalTVDistortion.UpLimit},Rsult{(result.ViewReslutDistortionGhost.HorizontalTVDistortion.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                    outtext += $"VerticalTVDistortion:{result.ViewReslutDistortionGhost.VerticalTVDistortion.TestValue} LowLimit:{result.ViewReslutDistortionGhost.VerticalTVDistortion.LowLimit}  UpLimit:{result.ViewReslutDistortionGhost.VerticalTVDistortion.UpLimit},Rsult{(result.ViewReslutDistortionGhost.VerticalTVDistortion.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                    break;
                case ARVR1TestType.Chessboard:
                    outtext += $"棋盘格 测试项：" + Environment.NewLine;

                    if (result.ViewReslutCheckerboard.PoiResultCIExyuvDatas != null)
                    {
                        foreach (var item in result.ViewReslutCheckerboard.PoiResultCIExyuvDatas)
                        {
                            outtext += $"{item.Name}  Y:{item.Y.ToString("F2")}{Environment.NewLine}";
                        }
                    }
                    outtext += $"ChessboardContrast:{result.ViewReslutCheckerboard.ChessboardContrast.TestValue} LowLimit:{result.ViewReslutCheckerboard.ChessboardContrast.LowLimit}  UpLimit:{result.ViewReslutCheckerboard.ChessboardContrast.UpLimit},Rsult{(result.ViewReslutCheckerboard.ChessboardContrast.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                    break;
                case ARVR1TestType.OpticCenter:
                    outtext += $"OpticCenter 测试项：" + Environment.NewLine;

                    if (result.ViewResultOpticCenter.FindCrossDetailViewReslut != null)
                    {
                        outtext += $"Opt中心点x:{result.ViewResultOpticCenter.FindCrossDetailViewReslut.FindCrossResult.result[0].center.x} 中心点y:{result.ViewResultOpticCenter.FindCrossDetailViewReslut.FindCrossResult.result[0].center.y}" + Environment.NewLine;
                        if (result.ViewResultOpticCenter.OptCenterXTilt != null)
                            outtext += $"OptCenterXTilt:{result.ViewResultOpticCenter.OptCenterXTilt.TestValue} LowLimit:{result.ViewResultOpticCenter.OptCenterXTilt.LowLimit}  UpLimit:{result.ViewResultOpticCenter.OptCenterXTilt.UpLimit},Rsult{(result.ViewResultOpticCenter.OptCenterXTilt.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                        if (result.ViewResultOpticCenter.OptCenterYTilt != null)
                            outtext += $"OptCenterYTilt:{result.ViewResultOpticCenter.OptCenterYTilt.TestValue} LowLimit:{result.ViewResultOpticCenter.OptCenterYTilt.LowLimit}  UpLimit:{result.ViewResultOpticCenter.OptCenterYTilt.UpLimit},Rsult{(result.ViewResultOpticCenter.OptCenterYTilt.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                        if (result.ViewResultOpticCenter.OptCenterRotation != null)
                            outtext += $"OptCenterRotation:{result.ViewResultOpticCenter.OptCenterRotation.TestValue} LowLimit:{result.ViewResultOpticCenter.OptCenterRotation.LowLimit}  UpLimit:{result.ViewResultOpticCenter.OptCenterRotation.UpLimit},Rsult{(result.ViewResultOpticCenter.OptCenterRotation.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";

                    }

                    if (result.ViewResultOpticCenter.FindCrossDetailViewReslut1 !=null)
                    {
                        outtext += $"Image中心点x:{result.ViewResultOpticCenter.FindCrossDetailViewReslut1.FindCrossResult.result[0].center.x} 中心点y:{result.ViewResultOpticCenter.FindCrossDetailViewReslut1.FindCrossResult.result[0].center.y}" + Environment.NewLine;
                        if (result.ViewResultOpticCenter.ImageCenterXTilt != null)
                            outtext += $"ImageCenterXTilt:{result.ViewResultOpticCenter.ImageCenterXTilt.TestValue} LowLimit:{result.ViewResultOpticCenter.ImageCenterXTilt.LowLimit}  UpLimit:{result.ViewResultOpticCenter.ImageCenterXTilt.UpLimit},Rsult{(result.ViewResultOpticCenter.ImageCenterXTilt.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                        if (result.ViewResultOpticCenter.ImageCenterYTilt != null)
                            outtext += $"ImageCenterYTilt:{result.ViewResultOpticCenter.ImageCenterYTilt.TestValue} LowLimit:{result.ViewResultOpticCenter.ImageCenterYTilt.LowLimit}  UpLimit:{result.ViewResultOpticCenter.ImageCenterYTilt.UpLimit},Rsult{(result.ViewResultOpticCenter.ImageCenterYTilt.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                        if (result.ViewResultOpticCenter.ImageCenterRotation != null)
                            outtext += $"ImageCenterRotation:{result.ViewResultOpticCenter.ImageCenterRotation.TestValue} LowLimit:{result.ViewResultOpticCenter.ImageCenterRotation.LowLimit}  UpLimit:{result.ViewResultOpticCenter.ImageCenterRotation.UpLimit},Rsult{(result.ViewResultOpticCenter.ImageCenterRotation.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                    }
                    break;
                default:
                    break;
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
            flowControl.Stop();
            STNodeEditorMain.Dispose();
            timer.Change(Timeout.Infinite, 500); // 停止定时器
            timer?.Dispose();
            logOutput?.Dispose();
            GC.SuppressFinalize(this);
        }

        private void Button_Click_Search(object sender, RoutedEventArgs e)
        {
            PropertyEditorWindow propertyEditorWindow = new PropertyEditorWindow(ObjectiveTestResult, false) { Owner = Application.Current.GetActiveWindow() };
            propertyEditorWindow.ShowDialog();
        }
    }
}