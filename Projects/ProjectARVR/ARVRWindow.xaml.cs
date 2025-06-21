#pragma warning disable
using ColorVision.Common.Algorithms;
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Abstractions;
using ColorVision.Engine.Media;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.FindLightArea;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.BinocularFusion;
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
using CVCommCore.CVAlgorithm;
using FlowEngineLib;
using FlowEngineLib.Base;
using LiveChartsCore.Kernel;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Asn1.Ocsp;
using Panuon.WPF.UI;
using ProjectARVR.Config;
using ProjectARVR.Services;
using ST.Library.UI.NodeEditor;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
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
        None = 0,
        /// <summary>
        /// 白画面
        /// </summary>
        White = 1,
        /// <summary>
        /// 黑画面
        /// </summary>
        Black = 2,
        /// <summary>
        /// 棋盘格
        /// </summary>
        Chessboard = 3,
        /// <summary>
        /// MTF 横
        /// </summary>
        MTFH = 4,
        /// <summary>
        /// MTF垂直
        /// </summary>
        MTFV = 5,
        /// <summary>
        /// 畸变
        /// </summary>
        Distortion = 6,
        /// <summary>
        /// 光轴偏角
        /// </summary>
        OpticCenter = 7,
        /// <summary>
        /// 鬼影
        /// </summary>
        Ghost = 8,
        /// <summary>
        /// 屏幕定位
        /// </summary>
        DotMatrix = 9,
        /// <summary>
        /// 白画面瑕疵检测
        /// </summary>
        WscreeenDefectDetection = 10,
        /// <summary>
        /// 黑画面瑕疵检测
        /// </summary>
        BKscreeenDefectDetection = 11
    }


    public class ProjectARVRReuslt : ViewModelBase
    {
        public int Id { get; set; }
        public string Model { get; set; }
        public DateTime CreateTime { get; set; } = DateTime.Now;

        public string FileName { get; set; }

        public string SN { get; set; }

        public string Code { get; set; }

        public bool Result { get; set; } = true;

        public ARVRTestType TestType { get; set; }


        public ViewResultWhite ViewResultWhite { get; set; } = new ViewResultWhite();
        public ViewResultBlack ViewResultBlack { get; set; } = new ViewResultBlack();

        public ViewReslutCheckerboard ViewReslutCheckerboard { get; set; } = new ViewReslutCheckerboard();

        public ViewRelsultMTFH ViewRelsultMTFH { get; set; } = new ViewRelsultMTFH();

        public ViewRelsultMTFV ViewRelsultMTFV { get; set; } = new ViewRelsultMTFV();

        public ViewReslutDistortionGhost ViewReslutDistortionGhost { get; set; } = new ViewReslutDistortionGhost();

        public ViewResultOpticCenter ViewResultOpticCenter { get; set; } = new ViewResultOpticCenter();

    }


    public class ViewResultOpticCenter
    {
        public BinocularFusionModel BinocularFusionModel { get; set; }

        /// <summary>
        /// X轴倾斜角(°) 测试项
        /// </summary>
        public ObjectiveTestItem XTilt { get; set; }

        /// <summary>
        /// Y轴倾斜角(°) 测试项
        /// </summary>
        public ObjectiveTestItem YTilt { get; set; }

        /// <summary>
        /// 旋转角(°) 测试项
        /// </summary>
        public ObjectiveTestItem Rotation { get; set; }
    }

    public class ViewReslutDistortionGhost
    {
        public ColorVision.Engine.Templates.Jsons.Distortion2.Distortion2View Distortion2View { get; set; }

        /// <summary>
        /// 水平TV畸变(%) 测试项
        /// </summary>
        public ObjectiveTestItem HorizontalTVDistortion { get; set; }

        /// <summary>
        /// 垂直TV畸变(%) 测试项
        /// </summary>
        public ObjectiveTestItem VerticalTVDistortion { get; set; }

    }

    public class ViewRelsultMTFH
    {
        public MTFDetailViewReslut MTFDetailViewReslut { get; set; }

    }
    public class ViewRelsultMTFV
    {
        public MTFDetailViewReslut MTFDetailViewReslut { get; set; }

    }

    public class ViewResultBlack
    {
        public List<PoiResultCIExyuvData> PoiResultCIExyuvDatas { get; set; }

        /// <summary>
        /// FOFO对比度 测试项
        /// </summary>
        public ObjectiveTestItem FOFOContrast { get; set; }


    }

    public class ViewResultWhite
    {
        public List<AlgResultLightAreaModel> AlgResultLightAreaModels { get; set; }

        public List<PoiResultCIExyuvData> PoiResultCIExyuvDatas { get; set; }

        public DFovView DFovView { get; set; }

        /// <summary>
        /// 中心相关色温(K) 测试项
        /// </summary>
        public ObjectiveTestItem CenterCorrelatedColorTemperature { get; set; }

        /// <summary>
        /// 亮度均匀性(%) 测试项
        /// </summary>
        public ObjectiveTestItem LuminanceUniformity { get; set; }

        /// <summary>
        /// 色彩均匀性 测试项
        /// </summary>
        public ObjectiveTestItem ColorUniformity { get; set; }

        /// <summary>
        /// 水平视场角(°) 测试项
        /// </summary>
        public ObjectiveTestItem HorizontalFieldOfViewAngle { get; set; }

        /// <summary>
        /// 垂直视场角(°) 测试项
        /// </summary>
        public ObjectiveTestItem VerticalFieldOfViewAngle { get; set; }

        /// <summary>
        /// 对角线视场角(°) 测试项
        /// </summary>
        public ObjectiveTestItem DiagonalFieldOfViewAngle { get; set; }


    }

    public class ViewReslutCheckerboard
    {
        public List<PoiResultCIExyuvData> PoiResultCIExyuvDatas { get; set; }

        /// <summary>
        /// 棋盘格对比度 测试项
        /// </summary>
        public ObjectiveTestItem ChessboardContrast { get; set; }
    }


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
        public ObservableCollection<ProjectARVRReuslt> ViewResluts { get; set; } = new ObservableCollection<ProjectARVRReuslt>();

        public ARVRWindow()
        {
            InitializeComponent();
            this.ApplyCaption(false);
            Config.SetWindow(this);
            SizeChanged += (s, e) => Config.SetConfig(this);
        }

        public ARVRTestType CurrentTestType = ARVRTestType.None;

        ObjectiveTestResult ObjectiveTestResult { get; set; } = new ObjectiveTestResult();
        Random Random = new Random();
        public void InitTest()
        {
            ObjectiveTestResult = new ObjectiveTestResult();
            CurrentTestType = ARVRTestType.None;

            Application.Current.Dispatcher.Invoke(() =>
            {
                ProjectARVRConfig.Instance.SN = "SN" + Random.NextInt64(10000, 90000).ToString();
            });
        }

        public void SwitchPGCompleted()
        {
            log.Info("PG切换结束");
            var values = Enum.GetValues(typeof(ARVRTestType));
            int currentIndex = Array.IndexOf(values, CurrentTestType);
            int nextIndex = (currentIndex + 1) % values.Length;
            // 跳过 None（假设 None 是第一个）
            if ((ARVRTestType)values.GetValue(nextIndex) == ARVRTestType.None)
                nextIndex = (nextIndex + 1) % values.Length;
            CurrentTestType = (ARVRTestType)values.GetValue(nextIndex);

            if (CurrentTestType == ARVRTestType.White)
            {
                FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("White")).Value;
                RunTemplate();
            }
            if (CurrentTestType == ARVRTestType.Black)
            {
                FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("Black")).Value;
                RunTemplate();
            }
            if (CurrentTestType == ARVRTestType.Chessboard)
            {
                FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("Chessboard")).Value;
                RunTemplate();
            }
            if (CurrentTestType == ARVRTestType.MTFH)
            {
                FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("MTF_H")).Value;
                RunTemplate();
            }

            if (CurrentTestType == ARVRTestType.MTFV)
            {
                FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("MTF_V")).Value;
                RunTemplate();
            }

            if (CurrentTestType == ARVRTestType.Distortion)
            {
                FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("Distortion")).Value;
                RunTemplate();
            }
            if (CurrentTestType == ARVRTestType.OpticCenter)
            {
                FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("OpticCenter")).Value;
                RunTemplate();
            }
        }

        public STNodeEditor STNodeEditorMain { get; set; }
        private FlowEngineControl flowEngine;
        private Timer timer;
        Stopwatch stopwatch = new Stopwatch();

        public static SPECConfig SPECConfig => ProjectARVRConfig.Instance.SPECConfig;

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = ProjectARVRConfig.Instance;
            ImageView.SetConfig(ProjectARVRConfig.Instance.ImageViewConfig);

            MQTTConfig mQTTConfig = MQTTSetting.Instance.MQTTConfig;
            MQTTHelper.SetDefaultCfg(mQTTConfig.Host, mQTTConfig.Port, mQTTConfig.UserName, mQTTConfig.UserPwd, false, null);
            flowEngine = new FlowEngineControl(false);
            STNodeEditorMain = new STNodeEditor();
            STNodeEditorMain.LoadAssembly("FlowEngineLib.dll");
            flowEngine.AttachNodeEditor(STNodeEditorMain);

            FlowTemplate.SelectionChanged += (s, e) =>
            {
                if (ProjectARVRConfig.Instance.TemplateSelectedIndex > -1)
                {
                    string Name = TemplateFlow.Params[ProjectARVRConfig.Instance.TemplateSelectedIndex].Key;
                    if (ProjectARVRConfig.Instance.SPECConfigs.TryGetValue(Name, out SPECConfig sPECConfig))
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
                BatchResultMasterDao.Instance.DeleteById(item.Id);
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


        public void Refresh()
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
                    Thread.Sleep(10);
                }
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
        private long LastFlowTime;
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
                        if (LastFlowTime == 0 || LastFlowTime - elapsedMilliseconds < 0)
                        {
                            msg = Msg1 + Environment.NewLine + $"已经执行：{elapsedTime}";
                        }
                        else
                        {
                            long remainingMilliseconds = LastFlowTime - elapsedMilliseconds;
                            TimeSpan remaining = TimeSpan.FromMilliseconds(remainingMilliseconds);
                            string remainingTime = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}:{elapsed.Milliseconds:D4}";

                            msg = Msg1 + Environment.NewLine + $"已经执行：{elapsedTime}, 上次执行：{LastFlowTime} ms, 预计还需要：{remainingTime}";
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


        ProjectARVRReuslt CurrentFlowResult { get; set; }
        int TryCount = 0;

        public void RunTemplate()
        {
            if (flowControl != null && flowControl.IsFlowRun) return;

            TryCount++;
            LastFlowTime = FlowConfig.Instance.FlowRunTime.TryGetValue(FlowTemplate.Text, out long time) ? time : 0;

            CurrentFlowResult = new ProjectARVRReuslt();
            CurrentFlowResult.SN = SNtextBox.Name;
            CurrentFlowResult.Code = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            if (string.IsNullOrWhiteSpace(flowEngine.GetStartNodeName())) { log.Info( "找不到完整流程，运行失败");return; }


            //多潘基次次
            log.Info($"IsReady{flowEngine.IsReady}");
            if (!flowEngine.IsReady)
            {
                string base64 = string.Empty;
                flowEngine.LoadFromBase64(base64);
                Refresh();
                log.Info($"IsReady{flowEngine.IsReady}");
                if (!flowEngine.IsReady)
                {
                    flowEngine.LoadFromBase64(base64);
                    Refresh();
                    log.Info($"IsReady{flowEngine.IsReady}");
                    if (!flowEngine.IsReady)
                    {
                        flowEngine.LoadFromBase64(base64);
                        Refresh();
                        log.Info($"IsReady{flowEngine.IsReady}");
                    }

                }
            }




            flowControl ??= new FlowControl(MQTTControl.GetInstance(), flowEngine);

            handler = PendingBox.Show(this, "流程", "流程启动", true);
            handler.Cancelling -= Handler_Cancelling;
            handler.Cancelling += Handler_Cancelling;
            flowControl.FlowCompleted += FlowControl_FlowCompleted;
            stopwatch.Reset();
            stopwatch.Start();

            try
            {
                BatchResultMasterDao.Instance.Save(new BatchResultMasterModel() { Name = CurrentFlowResult.SN, Code = CurrentFlowResult.Code, CreateDate = DateTime.Now });
            }
            catch (Exception ex)
            {
                log.Info(ex);
            }

            flowControl.Start(CurrentFlowResult.Code);
            timer.Change(0, 500); // 启动定时器
        }

        private void Handler_Cancelling(object? sender, CancelEventArgs e)
        {
            stopwatch.Stop();
            timer.Change(Timeout.Infinite, 500); // 停止定时器
            flowControl.Stop();
        }

        private FlowControl flowControl;

        private void FlowControl_FlowCompleted(object? sender, FlowControlData FlowControlData)
        {
            flowControl.FlowCompleted -= FlowControl_FlowCompleted;
            handler?.Close();
            handler = null;
            stopwatch.Stop();
            timer.Change(Timeout.Infinite, 500); // 停止定时器

            FlowConfig.Instance.FlowRunTime[FlowTemplate.Text] = stopwatch.ElapsedMilliseconds;

            log.Info($"流程执行Elapsed Time: {stopwatch.ElapsedMilliseconds} ms");

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
                TryCount = 0;
            }
            else
            {
                log.Info("流程运行失败" + FlowControlData.EventName + Environment.NewLine + FlowControlData.Params);
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

        private void Processing(string SerialNumber)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            bool sucess = true;
            BatchResultMasterModel Batch = null;
            try
            {
                Batch = BatchResultMasterDao.Instance.GetByCode(SerialNumber);
            }catch(Exception ex)
            {
                try
                {
                    Batch = BatchResultMasterDao.Instance.GetByCode(SerialNumber);
                }
                catch(Exception ex1)
                {
                    log.Error(ex1);
                }
            }

            if (Batch == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到批次号，请检查流程配置", "ColorVision");
                return;
            }
            ProjectARVRReuslt result = new ProjectARVRReuslt();

            if (ViewResluts.FirstOrDefault(a => a.SN == ProjectARVRConfig.Instance.SN) is ProjectARVRReuslt result1)
            {
                result1.CopyTo(result);
            }

            result.Model = FlowTemplate.Text;
            result.Id = Batch.Id;
            result.SN = ProjectARVRConfig.Instance.SN;
            result.Result = true;
            if (result.Model.Contains("White"))
            {
                log.Info("正在解析白画面的流程");
                result.TestType = ARVRTestType.White;
                var values = MeasureImgResultDao.Instance.GetAllByBatchId(Batch.Id);
                if (values.Count > 0)
                {
                    result.FileName = values[0].FileUrl;
                }
                var AlgResultMasterlists = AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id);
                log.Info($"AlgResultMasterlists count {AlgResultMasterlists.Count}");
                foreach (var AlgResultMaster in AlgResultMasterlists)
                {
                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.Abstractions.AlgorithmResultType.POI_XYZ)
                    {
                        result.ViewResultWhite.PoiResultCIExyuvDatas = new List<PoiResultCIExyuvData>();

                        List<PoiPointResultModel> POIPointResultModels = PoiPointResultDao.Instance.GetAllByPid(AlgResultMaster.Id);
                        int id = 0;
                        foreach (var item in POIPointResultModels)
                        {
                            PoiResultCIExyuvData poiResultCIExyuvData = new(item) { Id = id++ };

                            if (item.PoiName == "POI_5")
                            {
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
                            }
                            result.ViewResultWhite.PoiResultCIExyuvDatas.Add(poiResultCIExyuvData);
                        }
                    }

                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.Abstractions.AlgorithmResultType.FindLightArea)
                    {
                        result.ViewResultWhite.AlgResultLightAreaModels = AlgResultLightAreaDao.Instance.GetAllByPid(AlgResultMaster.Id);
                    }

                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.Abstractions.AlgorithmResultType.PoiAnalysis)
                    {
                        if (AlgResultMaster.TName.Contains("Luminance_uniformity"))
                        {
                            List<DetailCommonModel> detailCommonModels = DeatilCommonDao.Instance.GetAllByPid(AlgResultMaster.Id);
                            if (detailCommonModels.Count == 1)
                            {
                                PoiAnalysisDetailViewReslut viewReslut = new PoiAnalysisDetailViewReslut(detailCommonModels[0]);
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

                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.Abstractions.AlgorithmResultType.FOV)
                    {
                        List<DetailCommonModel> AlgResultModels = DeatilCommonDao.Instance.GetAllByPid(AlgResultMaster.Id);
                        if (AlgResultModels.Count == 1)
                        {
                            DFovView view1 = new DFovView(AlgResultModels[0]);
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

                var values = MeasureImgResultDao.Instance.GetAllByBatchId(Batch.Id);
                if (values.Count > 0)
                {
                    result.FileName = values[0].FileUrl;
                }
                var AlgResultMasterlists = AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id);

                foreach (var AlgResultMaster in AlgResultMasterlists)
                {
                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.Abstractions.AlgorithmResultType.POI_XYZ)
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


                var values = MeasureImgResultDao.Instance.GetAllByBatchId(Batch.Id);
                if (values.Count > 0)
                {
                    result.FileName = values[0].FileUrl;
                }
                var AlgResultMasterlists = AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id);

                foreach (var AlgResultMaster in AlgResultMasterlists)
                {
                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.Abstractions.AlgorithmResultType.POI_XYZ)
                    {
                        result.ViewReslutCheckerboard.PoiResultCIExyuvDatas = new List<PoiResultCIExyuvData>();

                        List<PoiPointResultModel> POIPointResultModels = PoiPointResultDao.Instance.GetAllByPid(AlgResultMaster.Id);
                        int id = 0;
                        foreach (var item in POIPointResultModels)
                        {
                            PoiResultCIExyuvData poiResultCIExyuvData = new(item) { Id = id++ };
                            result.ViewReslutCheckerboard.PoiResultCIExyuvDatas.Add(poiResultCIExyuvData);
                        }
                    }

                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.Abstractions.AlgorithmResultType.PoiAnalysis)
                    {
                        if (AlgResultMaster.TName.Contains("Chessboard_Contrast"))
                        {
                            List<DetailCommonModel> detailCommonModels = DeatilCommonDao.Instance.GetAllByPid(AlgResultMaster.Id);
                            if (detailCommonModels.Count == 1)
                            {
                                PoiAnalysisDetailViewReslut viewReslut = new PoiAnalysisDetailViewReslut(detailCommonModels[0]);


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
                }
            }
            else if (result.Model.Contains("MTF_H"))
            {
                log.Info("正在解析MTF_H画面的流程");
                result.TestType = ARVRTestType.MTFH;
                var values = MeasureImgResultDao.Instance.GetAllByBatchId(Batch.Id);
                if (values.Count > 0)
                {
                    result.FileName = values[0].FileUrl;
                }
                var AlgResultMasterlists = AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id);

                foreach (var AlgResultMaster in AlgResultMasterlists)
                {
                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.Abstractions.AlgorithmResultType.MTF && AlgResultMaster.version == "2.0")
                    {

                        List<DetailCommonModel> detailCommonModels = DeatilCommonDao.Instance.GetAllByPid(AlgResultMaster.Id);
                        if (detailCommonModels.Count == 1)
                        {
                            MTFDetailViewReslut mtfresults = new MTFDetailViewReslut(detailCommonModels[0]);

                            foreach (var mtf in mtfresults.MTFResult.result)
                            {
                                if (mtf.name == "Center_0F_H")
                                {
                                    ObjectiveTestResult.MTF_H_Center_0F = new ObjectiveTestItem()
                                    { 
                                        Name = "MTF_H_Center_0F", 
                                        LowLimit = SPECConfig.MTF_H_Center_0FMin,
                                        UpLimit = SPECConfig.MTF_H_Center_0FMax,
                                        Value = mtf.mtfValue ??0,
                                        TestValue = mtf.mtfValue.ToString() 
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_H_Center_0F.TestResult;

                                }

                                if (mtf.name == "LeftUp_0.5F_H")
                                {
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

                    }
                }

            }
            else if (result.Model.Contains("MTF_V"))
            {
                log.Info("正在解析MTF_V画面的流程");
                result.TestType = ARVRTestType.MTFV;
                var values = MeasureImgResultDao.Instance.GetAllByBatchId(Batch.Id);
                if (values.Count > 0)
                {
                    result.FileName = values[0].FileUrl;
                }
                var AlgResultMasterlists = AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id);

                foreach (var AlgResultMaster in AlgResultMasterlists)
                {
                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.Abstractions.AlgorithmResultType.MTF && AlgResultMaster.version == "2.0")
                    {

                        List<DetailCommonModel> detailCommonModels = DeatilCommonDao.Instance.GetAllByPid(AlgResultMaster.Id);
                        if (detailCommonModels.Count == 1)
                        {
                            MTFDetailViewReslut mtfresults = new MTFDetailViewReslut(detailCommonModels[0]);
                            foreach (var mtf in mtfresults.MTFResult.result)
                            {
                                if (mtf.name == "Center_0F_V")
                                {
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
                                    ObjectiveTestResult.MTF_V_LeftUp_0_5F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_V_LeftUp_0_5F",
                                        LowLimit = SPECConfig.MTF_V_LeftUp_0_5FMin,
                                        UpLimit = SPECConfig.MTF_V_LeftUp_0_5FMax   ,
                                        Value = mtf.mtfValue ?? 0,
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_V_LeftUp_0_5F.TestResult;
                                }
                                if (mtf.name == "RightUp_0.5F_V")
                                {
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
            }
            else if (result.Model.Contains("Distortion"))
            {
                log.Info("正在解析Distortion画面的流程");
                result.TestType = ARVRTestType.Distortion;
                var values = MeasureImgResultDao.Instance.GetAllByBatchId(Batch.Id);
                if (values.Count > 0)
                {
                    result.FileName = values[0].FileUrl;
                }
                var AlgResultMasterlists = AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id);

                foreach (var AlgResultMaster in AlgResultMasterlists)
                {
                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.Abstractions.AlgorithmResultType.Distortion && AlgResultMaster.version == "2.0")
                    {
                        List<DetailCommonModel> AlgResultModels = DeatilCommonDao.Instance.GetAllByPid(AlgResultMaster.Id);
                        if (AlgResultModels.Count == 1)
                        {
                            ColorVision.Engine.Templates.Jsons.Distortion2.Distortion2View blackMuraView = new ColorVision.Engine.Templates.Jsons.Distortion2.Distortion2View(AlgResultModels[0]);
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

                var values = MeasureImgResultDao.Instance.GetAllByBatchId(Batch.Id);
                if (values.Count > 0)
                {
                    result.FileName = values[0].FileUrl;
                }
                var AlgResultMasterlists = AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id);


                foreach (var AlgResultMaster in AlgResultMasterlists)
                {
                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.Abstractions.AlgorithmResultType.ARVR_BinocularFusion)
                    {
                        List<BinocularFusionModel> AlgResultModels = BinocularFusionDao.Instance.GetAllByPid(AlgResultMaster.Id);
                        if (AlgResultModels.Count == 1)
                        {
                            result.ViewResultOpticCenter.BinocularFusionModel = AlgResultModels[0];
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

            ViewResluts.Insert(0,result); //倒序插入
            listView1.SelectedIndex = 0;

            if (SocketManager.GetInstance().TcpClients.Count > 0)
            {
                log.Info("Socket已经链接 ");
                if (SocketControl.Current.Stream != null)
                {
                    var values = Enum.GetValues(typeof(ARVRTestType));
                    int currentIndex = Array.IndexOf(values, CurrentTestType);
                    int nextIndex = (currentIndex + 1) % values.Length;
                    // 跳过 None（假设 None 是第一个）
                    if ((ARVRTestType)values.GetValue(nextIndex) == ARVRTestType.None)
                        nextIndex = (nextIndex + 1) % values.Length;
                    ARVRTestType aRVRTestType = (ARVRTestType)values.GetValue(nextIndex);

                    if (aRVRTestType == ARVRTestType.Ghost)
                    {
                        log.Info("ARVR测试完成");

                        ObjectiveTestResult.TotalResult = true;

                        string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        string filePath = Path.Combine(ProjectARVRConfig.Instance.ResultSavePath, $"ObjectiveTestResults_{timeStr}.csv");

                        List<ObjectiveTestResult> objectiveTestResults = new List<ObjectiveTestResult>();
                      
                        objectiveTestResults.Add(ObjectiveTestResult);
                        ObjectiveTestResultCsvExporter.ExportToCsv(objectiveTestResults, filePath);
                        var response = new SocketResponse
                        {
                            Version = "1.0",
                            MsgID = string.Empty,
                            EventName = "ProjectARVRResult",
                            Code = 0,
                            Msg = "ARVR Test Completed",
                            Data = ObjectiveTestResult
                        };

                        string respString = JsonConvert.SerializeObject(response);
                        log.Info(respString);
                        SocketControl.Current.Stream.Write(Encoding.UTF8.GetBytes(respString));
                    }
                    else
                    {

                        var response = new SocketResponse
                        {
                            Version = "1.0",
                            MsgID = string.Empty,
                            EventName = "SwitchPG",
                            Code = 0,
                            Msg = "Switch PG",
                            Data = new SwitchPG
                            {
                                ARVRTestType = aRVRTestType
                            },
                        };
                        string respString = JsonConvert.SerializeObject(response);
                        log.Info(respString);
                        SocketControl.Current.Stream.Write(Encoding.UTF8.GetBytes(respString));
                    }

                }
                else
                {
                    log.Info("Socket流为空，无法发送数据");
                }
            }
            else
            {
                log.Info("找不到连接的Socket");
            }
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

                if (result.TestType == ARVRTestType.White)
                {
                    DVPolygon polygon = new DVPolygon();
                    List<System.Windows.Point> point1s = new List<System.Windows.Point>();
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