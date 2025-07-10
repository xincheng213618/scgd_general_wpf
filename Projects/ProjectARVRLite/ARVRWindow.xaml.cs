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
using ColorVision.Engine.Templates.Jsons.FindCross;
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
using log4net.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Asn1.Ocsp;
using Panuon.WPF.UI;
using ProjectARVRLite.Config;
using ProjectARVRLite.Services;
using ST.Library.UI.NodeEditor;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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

namespace ProjectARVRLite
{
    public enum ARVR1TestType
    {
        None = 0,
        W51,
        /// <summary>
        /// 白画面绿图
        /// </summary>
        White,
        /// <summary>
        /// 黑画面25
        /// </summary>
        Black,
        /// <summary>
        /// 25的图像
        /// </summary>
        W25,
        /// <summary>
        /// 棋盘格
        /// </summary>
        Chessboard,
        /// <summary>
        /// MTFHV 
        /// </summary>
        MTFHV,
        /// <summary>
        /// 畸变，9点
        /// </summary>
        Distortion,
        /// <summary>
        /// 光轴偏角
        /// </summary>
        OpticCenter,
        Ghost,
        /// <summary>
        /// 屏幕定位
        /// </summary>
        DotMatrix,
        /// <summary>
        /// 白画面瑕疵检测
        /// </summary>
        WscreeenDefectDetection,
        /// <summary>
        /// 黑画面瑕疵检测
        /// </summary>
        BKscreeenDefectDetection,
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

        public ARVR1TestType TestType { get; set; }

        public ViewResultW25 ViewResultW25 { get; set; } = new ViewResultW25();

        public ViewReslutW51 ViewReslutW51 { get; set; } = new ViewReslutW51();

        public ViewResultWhite ViewResultWhite { get; set; } = new ViewResultWhite();
        public ViewResultBlack ViewResultBlack { get; set; } = new ViewResultBlack();

        public ViewReslutCheckerboard ViewReslutCheckerboard { get; set; } = new ViewReslutCheckerboard();

        public ViewRelsultMTFHV ViewRelsultMTFH { get; set; } = new ViewRelsultMTFHV();

        public ViewReslutDistortionGhost ViewReslutDistortionGhost { get; set; } = new ViewReslutDistortionGhost();

        public ViewResultOpticCenter ViewResultOpticCenter { get; set; } = new ViewResultOpticCenter();

    }


    public class ViewResultOpticCenter
    {
        public FindCrossDetailViewReslut FindCrossDetailViewReslut { get; set; }

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

    public class ViewRelsultMTFHV
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

    public class ViewReslutW51
    {
        public List<AlgResultLightAreaModel> AlgResultLightAreaModels { get; set; }
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

    public class ViewResultW25
    {
        public List<PoiResultCIExyuvData> PoiResultCIExyuvDatas { get; set; }

    }


    public class ViewResultWhite
    {
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

    }

    public class ViewReslutCheckerboard
    {
        public ObservableCollection<PoiResultCIExyuvData> PoiResultCIExyuvDatas { get; set; }

        /// <summary>
        /// 棋盘格对比度 测试项
        /// </summary>
        public ObjectiveTestItem ChessboardContrast { get; set; }
    }


    public class SwitchPG
    {
        public ARVR1TestType ARVRTestType { get; set; }
    }


    public class ARVRWindowConfig : WindowConfig
    {
        public static ARVRWindowConfig Instance => ConfigService.Instance.GetRequiredService<ARVRWindowConfig>();
        public ObservableCollection<ProjectARVRReuslt> ViewResluts { get; set; } = new ObservableCollection<ProjectARVRReuslt>();

    }

    public partial class ARVRWindow : Window, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ARVRWindow));
        public static ARVRWindowConfig Config => ARVRWindowConfig.Instance;
        public ObservableCollection<ProjectARVRReuslt> ViewResluts { get; set; } = Config.ViewResluts;

        public ARVRWindow()
        {
            InitializeComponent();
            this.ApplyCaption(false);
            Config.SetWindow(this);
            SizeChanged += (s, e) => Config.SetConfig(this);
        }

        public ARVR1TestType CurrentTestType = ARVR1TestType.None;

        ObjectiveTestResult ObjectiveTestResult { get; set; } = new ObjectiveTestResult();
        Random Random = new Random();
        public void InitTest()
        {
            ObjectiveTestResult = new ObjectiveTestResult();
            CurrentTestType = ARVR1TestType.None;

            Application.Current.Dispatcher.Invoke(() =>
            {
                ProjectARVRLiteConfig.Instance.SN = "SN" + Random.NextInt64(10000, 90000).ToString();
            });
        }

        public void SwitchPGCompleted()
        {
            log.Info("PG切换结束");
            var values = Enum.GetValues(typeof(ARVR1TestType));
            int currentIndex = Array.IndexOf(values, CurrentTestType);
            int nextIndex = (currentIndex + 1) % values.Length;
            // 跳过 None（假设 None 是第一个）
            if ((ARVR1TestType)values.GetValue(nextIndex) == ARVR1TestType.None)
                nextIndex = (nextIndex + 1) % values.Length;
            CurrentTestType = (ARVR1TestType)values.GetValue(nextIndex);

            if (CurrentTestType == ARVR1TestType.W51)
            {
                FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("White51")).Value;
                RunTemplate();
            }
            if (CurrentTestType == ARVR1TestType.White)
            {
                FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("White255")).Value;
                RunTemplate();
            }
            if (CurrentTestType == ARVR1TestType.Black)
            {
                FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("Black")).Value;
                RunTemplate();
            }
            if (CurrentTestType == ARVR1TestType.W25)
            {
                FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("White25")).Value;
                RunTemplate();
            }
            if (CurrentTestType == ARVR1TestType.Chessboard)
            {
                FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("Chessboard")).Value;
                RunTemplate();
            }
            if (CurrentTestType == ARVR1TestType.MTFHV)
            {
                FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("MTF_HV")).Value;
                RunTemplate();
            }
            if (CurrentTestType == ARVR1TestType.Distortion)
            {
                FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("Distortion")).Value;
                RunTemplate();
            }
            if (CurrentTestType == ARVR1TestType.OpticCenter)
            {
                FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("OpticCenter")).Value;
                RunTemplate();
            }
        }

        public STNodeEditor STNodeEditorMain { get; set; }
        private FlowEngineControl flowEngine;
        private Timer timer;
        Stopwatch stopwatch = new Stopwatch();

        public static SPECConfig SPECConfig => ProjectARVRLiteConfig.Instance.SPECConfig;

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = ProjectARVRLiteConfig.Instance;
            ImageView.SetConfig(ProjectARVRLiteConfig.Instance.ImageViewConfig);

            MQTTConfig mQTTConfig = MQTTSetting.Instance.MQTTConfig;
            MQTTHelper.SetDefaultCfg(mQTTConfig.Host, mQTTConfig.Port, mQTTConfig.UserName, mQTTConfig.UserPwd, false, null);
            flowEngine = new FlowEngineControl(false);
            STNodeEditorMain = new STNodeEditor();
            STNodeEditorMain.LoadAssembly("FlowEngineLib.dll");
            flowEngine.AttachNodeEditor(STNodeEditorMain);

            FlowTemplate.SelectionChanged += (s, e) =>
            {
                if (ProjectARVRLiteConfig.Instance.TemplateSelectedIndex > -1)
                {
                    string Name = TemplateFlow.Params[ProjectARVRLiteConfig.Instance.TemplateSelectedIndex].Key;
                    if (ProjectARVRLiteConfig.Instance.SPECConfigs.TryGetValue(Name, out SPECConfig sPECConfig))
                    {
                        ProjectARVRLiteConfig.Instance.SPECConfig = sPECConfig;
                    }
                    else
                    {
                        sPECConfig = new SPECConfig();
                        ProjectARVRLiteConfig.Instance.SPECConfigs.TryAdd(Name, sPECConfig);
                        ProjectARVRLiteConfig.Instance.SPECConfig = sPECConfig;
                    }

                }
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

        public async Task RunTemplate()
        {
            if (flowControl != null && flowControl.IsFlowRun) return;

            TryCount++;
            LastFlowTime = FlowConfig.Instance.FlowRunTime.TryGetValue(FlowTemplate.Text, out long time) ? time : 0;

            CurrentFlowResult = new ProjectARVRReuslt();
            CurrentFlowResult.SN = SNtextBox.Name;
            CurrentFlowResult.Code = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");

            await Refresh();

            if (string.IsNullOrWhiteSpace(flowEngine.GetStartNodeName())) { log.Info( "找不到完整流程，运行失败");return; }


            log.Info($"IsReady{flowEngine.IsReady}");
            if (!flowEngine.IsReady)
            {
                string base64 = string.Empty;
                flowEngine.LoadFromBase64(base64);
                await Refresh();
                log.Info($"IsReady{flowEngine.IsReady}");
            }


            flowControl ??= new FlowControl(MQTTControl.GetInstance(), flowEngine);

            handler = PendingBox.Show(this, "流程", "流程启动", true);
            handler.Cancelling -= Handler_Cancelling;
            handler.Cancelling += Handler_Cancelling;
            flowControl.FlowCompleted += FlowControl_FlowCompleted;
            stopwatch.Reset();
            stopwatch.Start();

            BatchResultMasterDao.Instance.Save(new BatchResultMasterModel() { Name = CurrentFlowResult.SN, Code = CurrentFlowResult.Code, CreateDate = DateTime.Now });

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

        private bool IsSavePicture = false;
        

        private void Processing(string SerialNumber)
        {
            IsSavePicture = true;
            BatchResultMasterModel Batch = BatchResultMasterDao.Instance.GetByCode(SerialNumber);


            if (Batch == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到批次号，请检查流程配置", "ColorVision");
                return;
            }
            ProjectARVRReuslt result = new ProjectARVRReuslt();

            if (ViewResluts.FirstOrDefault(a => a.SN == ProjectARVRLiteConfig.Instance.SN) is ProjectARVRReuslt result1)
            {
                result1.CopyTo(result);
            }

            result.Model = FlowTemplate.Text;
            result.Id = Batch.Id;
            result.SN = ProjectARVRLiteConfig.Instance.SN;
            result.Result = true;

            if (result.Model.Contains("White51"))
            {
                log.Info("正在解析White51的流程");
                result.TestType = ARVR1TestType.W51;
                var values = MeasureImgResultDao.Instance.GetAllByBatchId(Batch.Id);
                if (values.Count > 0)
                {
                    result.FileName = values[0].FileUrl;
                }
                var AlgResultMasterlists = AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id);
                log.Info($"AlgResultMasterlists count {AlgResultMasterlists.Count}");
                foreach (var AlgResultMaster in AlgResultMasterlists)
                {
                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.Abstractions.AlgorithmResultType.FindLightArea)
                    {
                        result.ViewReslutW51.AlgResultLightAreaModels = AlgResultLightAreaDao.Instance.GetAllByPid(AlgResultMaster.Id);
                    }

                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.Abstractions.AlgorithmResultType.FOV)
                    {
                        List<DetailCommonModel> AlgResultModels = DeatilCommonDao.Instance.GetAllByPid(AlgResultMaster.Id);
                        if (AlgResultModels.Count == 1)
                        {
                            DFovView view1 = new DFovView(AlgResultModels[0]);
                            result.ViewResultWhite.DFovView = view1;

                            ObjectiveTestResult.W51DiagonalFieldOfViewAngle = new ObjectiveTestItem()
                            {
                                Name = "DiagonalFieldOfViewAngle",
                                LowLimit = SPECConfig.DiagonalFieldOfViewAngleMin,
                                UpLimit = SPECConfig.DiagonalFieldOfViewAngleMax,
                                Value = view1.Result.result.D_Fov,
                                TestValue = view1.Result.result.D_Fov.ToString("F3")
                            };

                            ObjectiveTestResult.W51HorizontalFieldOfViewAngle = new ObjectiveTestItem()
                            {
                                Name = "HorizontalFieldOfViewAngle",
                                LowLimit = SPECConfig.HorizontalFieldOfViewAngleMin,
                                UpLimit = SPECConfig.HorizontalFieldOfViewAngleMax,
                                Value = view1.Result.result.ClolorVisionH_Fov,
                                TestValue = view1.Result.result.ClolorVisionH_Fov.ToString("F3")
                            };
                            ObjectiveTestResult.W51VerticalFieldOfViewAngle = new ObjectiveTestItem()
                            {
                                Name = "VerticalFieldOfViewAngle",
                                LowLimit = SPECConfig.VerticalFieldOfViewAngleMin,
                                UpLimit = SPECConfig.VerticalFieldOfViewAngleMax,
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
                result.TestType = ARVR1TestType.White;
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
                                ObjectiveTestResult.BlackCenterCorrelatedColorTemperature = objectiveTestItem;
                                result.ViewResultWhite.CenterCorrelatedColorTemperature = objectiveTestItem;
                                result.Result = result.Result && objectiveTestItem.TestResult;
                            }
                            result.ViewResultWhite.PoiResultCIExyuvDatas.Add(poiResultCIExyuvData);
                        }
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
                                ObjectiveTestResult.W255LuminanceUniformity = LuminanceUniformity;
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
                                //ObjectiveTestResult.ColorUniformity = ColorUniformity;
                                result.ViewResultWhite.ColorUniformity = ColorUniformity;
                                result.Result = result.Result && ColorUniformity.TestResult;

                            }
                        }
                    }

                }


            }

            else if (result.Model.Contains("White25"))
            {
                log.Info("正在解析White25画面的流程");
                result.TestType = ARVR1TestType.W25;
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
                            ObjectiveTestResult.W25CenterLunimance = new ObjectiveTestItem()
                            {
                                Name = "W25CenterLunimance",
                                LowLimit = SPECConfig.ChessboardContrastMin,
                                UpLimit = SPECConfig.ChessboardContrastMax,
                                Value = result.ViewResultW25.PoiResultCIExyuvDatas[0].Y,
                                TestValue = result.ViewResultW25.PoiResultCIExyuvDatas[0].Y.ToString("F3")
                            };
                            ObjectiveTestResult.W25CenterCIE1931ChromaticCoordinatesx = new ObjectiveTestItem()
                            {
                                Name = "W25CenterCIE1931ChromaticCoordinatesx",
                                LowLimit = SPECConfig.ChessboardContrastMin,
                                UpLimit = SPECConfig.ChessboardContrastMax,
                                Value = result.ViewResultW25.PoiResultCIExyuvDatas[0].x,
                                TestValue = result.ViewResultW25.PoiResultCIExyuvDatas[0].x.ToString("F3")
                            };
                            ObjectiveTestResult.W25CenterCIE1931ChromaticCoordinatesy = new ObjectiveTestItem()
                            {
                                Name = "W25CenterCIE1931ChromaticCoordinatesy",
                                LowLimit = SPECConfig.ChessboardContrastMin,
                                UpLimit = SPECConfig.ChessboardContrastMax,
                                Value = result.ViewResultW25.PoiResultCIExyuvDatas[0].y,
                                TestValue = result.ViewResultW25.PoiResultCIExyuvDatas[0].y.ToString("F3")
                            };
                            ObjectiveTestResult.W25CenterCIE1976ChromaticCoordinatesu = new ObjectiveTestItem()
                            {
                                Name = "W25CenterCIE1976ChromaticCoordinatesu",
                                LowLimit = SPECConfig.ChessboardContrastMin,
                                UpLimit = SPECConfig.ChessboardContrastMax,
                                Value = result.ViewResultW25.PoiResultCIExyuvDatas[0].y,
                                TestValue = result.ViewResultW25.PoiResultCIExyuvDatas[0].u.ToString("F3")
                            };
                            ObjectiveTestResult.W25CenterCIE1976ChromaticCoordinatesv = new ObjectiveTestItem()
                            {
                                Name = "W25CenterCIE1976ChromaticCoordinatesv",
                                LowLimit = SPECConfig.ChessboardContrastMin,
                                UpLimit = SPECConfig.ChessboardContrastMax,
                                Value = result.ViewResultW25.PoiResultCIExyuvDatas[0].y,
                                TestValue = result.ViewResultW25.PoiResultCIExyuvDatas[0].v.ToString("F3")
                            };
                        }


                    }


                }
            }

            else if (result.Model.Contains("Black"))
            {
                log.Info("正在解析黑画面的流程");
                result.TestType = ARVR1TestType.Black;

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
                result.TestType = ARVR1TestType.Chessboard;


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
                        result.ViewReslutCheckerboard.PoiResultCIExyuvDatas = new ObservableCollection<PoiResultCIExyuvData>();

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
                                    TestValue = ChessboardContrast.ToString("F3")
                                };

                                result.ViewReslutCheckerboard.ChessboardContrast = ObjectiveTestResult.ChessboardContrast;
                                result.Result = result.Result && ObjectiveTestResult.ChessboardContrast.TestResult;

                            }
                        }
                    }

                    try
                    {
                        string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        string filePath = Path.Combine(ProjectARVRLiteConfig.Instance.ResultSavePath, $"Chessboard_{timeStr}.csv");
                        PoiResultCIExyuvData.SaveCsv(result.ViewReslutCheckerboard.PoiResultCIExyuvDatas, filePath);

                        var csvBuilder = new StringBuilder();
                        csvBuilder.AppendLine();
                        csvBuilder.AppendLine($"Chessboard");
                        csvBuilder.AppendLine($"{result.ViewReslutCheckerboard.ChessboardContrast.Value}");
                        File.AppendAllText(filePath, csvBuilder.ToString(), Encoding.UTF8);
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex);
                    }
                }
            }
            else if (result.Model.Contains("MTF_HV"))
            {
                log.Info("正在解析MTF_HV画面的流程");
                result.TestType = ARVR1TestType.MTFHV;
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

                            foreach (var mtf in mtfresults.MTFResult.resultChild)
                            {
                                if (mtf.name == "Center_0F")
                                {
                                    ObjectiveTestResult.MTF_HV_H_Center_0F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_H_Center_0F",
                                        LowLimit = SPECConfig.MTF_HV_H_Center_0FMin,
                                        UpLimit = SPECConfig.MTF_HV_H_Center_0FMax,
                                        Value = mtf.horizontalAverage,
                                        TestValue = mtf.horizontalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_H_Center_0F.TestResult;

                                    ObjectiveTestResult.MTF_HV_V_Center_0F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_V_Center_0F",
                                        LowLimit = SPECConfig.MTF_HV_V_Center_0FMin,
                                        UpLimit = SPECConfig.MTF_HV_V_Center_0FMax,
                                        Value = mtf.verticalAverage,
                                        TestValue = mtf.verticalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_V_Center_0F.TestResult;
                                }

                                if (mtf.name == "LeftUp_0.4F")
                                {
                                    ObjectiveTestResult.MTF_HV_H_LeftUp_0_4F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_H_LeftUp_0_4F",
                                        LowLimit = SPECConfig.MTF_HV_H_LeftUp_0_4FMin,
                                        UpLimit = SPECConfig.MTF_HV_H_LeftUp_0_4FMax,
                                        Value = mtf.horizontalAverage,
                                        TestValue = mtf.horizontalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_H_LeftUp_0_4F.TestResult;

                                    ObjectiveTestResult.MTF_HV_V_LeftUp_0_4F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_V_LeftUp_0_4F",
                                        LowLimit = SPECConfig.MTF_HV_V_LeftUp_0_4FMin,
                                        UpLimit = SPECConfig.MTF_HV_V_LeftUp_0_4FMax,
                                        Value = mtf.verticalAverage,
                                        TestValue = mtf.verticalAverage.ToString()
                                    };

                                }

                                if (mtf.name == "RightUp_0.4F")
                                {
                                    ObjectiveTestResult.MTF_HV_H_RightUp_0_4F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_H_RightUp_0_4F",
                                        LowLimit = SPECConfig.MTF_HV_H_RightUp_0_4FMin,
                                        UpLimit = SPECConfig.MTF_HV_H_RightUp_0_4FMax,
                                        Value = mtf.horizontalAverage,
                                        TestValue = mtf.horizontalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_H_RightUp_0_4F.TestResult;
                                    ObjectiveTestResult.MTF_HV_V_RightUp_0_4F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_V_RightUp_0_4F",
                                        LowLimit = SPECConfig.MTF_HV_V_RightUp_0_4FMin,
                                        UpLimit = SPECConfig.MTF_HV_V_RightUp_0_4FMax,
                                        Value = mtf.verticalAverage,
                                        TestValue = mtf.verticalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_V_RightUp_0_4F.TestResult;

                                }
                                if (mtf.name == "LeftDown_0.4F")
                                {
                                    ObjectiveTestResult.MTF_HV_H_LeftDown_0_4F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_H_LeftDown_0_4F",
                                        LowLimit = SPECConfig.MTF_HV_H_LeftDown_0_4FMin,
                                        UpLimit = SPECConfig.MTF_HV_H_LeftDown_0_4FMax,
                                        Value = mtf.horizontalAverage,
                                        TestValue = mtf.horizontalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_H_LeftDown_0_4F.TestResult;
                                    ObjectiveTestResult.MTF_HV_V_LeftDown_0_4F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_V_LeftDown_0_4F",
                                        LowLimit = SPECConfig.MTF_HV_V_LeftDown_0_4FMin,
                                        UpLimit = SPECConfig.MTF_HV_V_LeftDown_0_4FMax,
                                        Value = mtf.verticalAverage,
                                        TestValue = mtf.verticalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_V_LeftDown_0_4F.TestResult;
                                }
                                if (mtf.name == "RightDown_0.4F")
                                {
                                    ObjectiveTestResult.MTF_HV_H_RightDown_0_4F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_H_RightDown_0_4F",
                                        LowLimit = SPECConfig.MTF_HV_H_RightDown_0_4FMin,
                                        UpLimit = SPECConfig.MTF_HV_H_RightDown_0_4FMax,
                                        Value = mtf.horizontalAverage,
                                        TestValue = mtf.horizontalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_H_RightDown_0_4F.TestResult;
                                    ObjectiveTestResult.MTF_HV_V_RightDown_0_4F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_V_RightDown_0_4F",
                                        LowLimit = SPECConfig.MTF_HV_V_RightDown_0_4FMin,
                                        UpLimit = SPECConfig.MTF_HV_V_RightDown_0_4FMax,
                                        Value = mtf.verticalAverage,
                                        TestValue = mtf.verticalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_V_RightDown_0_4F.TestResult;
                                }

                                if (mtf.name == "LeftUp_0.8F")
                                {
                                    ObjectiveTestResult.MTF_HV_H_LeftUp_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_H_LeftUp_0_8F",
                                        LowLimit = SPECConfig.MTF_HV_H_LeftUp_0_8FMin,
                                        UpLimit = SPECConfig.MTF_HV_H_LeftUp_0_8FMax,
                                        Value = mtf.horizontalAverage,
                                        TestValue = mtf.horizontalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_H_LeftUp_0_8F.TestResult;
                                    ObjectiveTestResult.MTF_HV_V_LeftUp_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_V_LeftUp_0_8F",
                                        LowLimit = SPECConfig.MTF_HV_V_LeftUp_0_8FMin,
                                        UpLimit = SPECConfig.MTF_HV_V_LeftUp_0_8FMax,
                                        Value = mtf.verticalAverage,
                                        TestValue = mtf.verticalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_V_LeftUp_0_8F.TestResult;
                                }
                                if (mtf.name == "RightUp_0.8F")
                                {
                                    ObjectiveTestResult.MTF_HV_H_RightUp_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_H_RightUp_0_8F",
                                        LowLimit = SPECConfig.MTF_HV_H_RightUp_0_8FMin,
                                        UpLimit = SPECConfig.MTF_HV_H_RightUp_0_8FMax,
                                        Value = mtf.horizontalAverage,
                                        TestValue = mtf.horizontalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_H_RightUp_0_8F.TestResult;
                                    ObjectiveTestResult.MTF_HV_V_RightUp_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_V_RightUp_0_8F",
                                        LowLimit = SPECConfig.MTF_HV_V_RightUp_0_8FMin,
                                        UpLimit = SPECConfig.MTF_HV_V_RightUp_0_8FMax,
                                        Value = mtf.verticalAverage,
                                        TestValue = mtf.verticalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_V_RightUp_0_8F.TestResult;
                                }
                                if (mtf.name == "LeftDown_0.8F")
                                {
                                    ObjectiveTestResult.MTF_HV_H_LeftDown_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_H_LeftDown_0_8F",
                                        LowLimit = SPECConfig.MTF_HV_H_LeftDown_0_8FMin,
                                        UpLimit = SPECConfig.MTF_HV_H_LeftDown_0_8FMax,
                                        Value = mtf.horizontalAverage,
                                        TestValue = mtf.horizontalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_H_LeftDown_0_8F.TestResult;
                                    ObjectiveTestResult.MTF_HV_V_LeftDown_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_V_LeftDown_0_8F",
                                        LowLimit = SPECConfig.MTF_HV_V_LeftDown_0_8FMin,
                                        UpLimit = SPECConfig.MTF_HV_V_LeftDown_0_8FMax,
                                        Value = mtf.verticalAverage,
                                        TestValue = mtf.verticalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_V_LeftDown_0_8F.TestResult;
                                }
                                if (mtf.name == "RightDown_0.8F")
                                {
                                    ObjectiveTestResult.MTF_HV_H_RightDown_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_H_RightDown_0_8F",
                                        LowLimit = SPECConfig.MTF_HV_H_RightDown_0_8FMin,
                                        UpLimit = SPECConfig.MTF_HV_H_RightDown_0_8FMax,
                                        Value = mtf.horizontalAverage,
                                        TestValue = mtf.horizontalAverage.ToString()
                                    };
                                    result.Result = result.Result && ObjectiveTestResult.MTF_HV_H_RightDown_0_8F.TestResult;
                                    ObjectiveTestResult.MTF_HV_V_RightDown_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_HV_V_RightDown_0_8F",
                                        LowLimit = SPECConfig.MTF_HV_V_RightDown_0_8FMin,
                                        UpLimit = SPECConfig.MTF_HV_V_RightDown_0_8FMax,
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
                            string filePath = Path.Combine(ProjectARVRLiteConfig.Instance.ResultSavePath, $"MTF_H_{timeStr}.csv");
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
                result.TestType = ARVR1TestType.Distortion;
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
                result.TestType = ARVR1TestType.OpticCenter;

                var values = MeasureImgResultDao.Instance.GetAllByBatchId(Batch.Id);
                if (values.Count > 0)
                {
                    result.FileName = values[0].FileUrl;
                }
                var AlgResultMasterlists = AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id);


                foreach (var AlgResultMaster in AlgResultMasterlists)
                {
                    if (AlgResultMaster.ImgFileType == ColorVision.Engine.Abstractions.AlgorithmResultType.FindCross)
                    {

                        List<DetailCommonModel> detailCommonModels = DeatilCommonDao.Instance.GetAllByPid(AlgResultMaster.Id);
                        if (detailCommonModels.Count == 1)
                        {
                            FindCrossDetailViewReslut mtfresult = new FindCrossDetailViewReslut(detailCommonModels[0]);
                            result.ViewResultOpticCenter.FindCrossDetailViewReslut = mtfresult;

                            ObjectiveTestResult.OptCenterXTilt = new ObjectiveTestItem()
                            {
                                Name = "OptCenterXTilt",
                                LowLimit = SPECConfig.XTiltMin,
                                UpLimit = SPECConfig.XTiltMax,
                                Value = mtfresult.FindCrossResult.result[0].tilt.tilt_x,
                                TestValue = mtfresult.FindCrossResult.result[0].tilt.tilt_x.ToString("F4")
                            };
                            ObjectiveTestResult.OptCenterYTilt = new ObjectiveTestItem()
                            {
                                Name = "OptCenterYTilt",
                                LowLimit = SPECConfig.YTiltMin,
                                UpLimit = SPECConfig.YTiltMax,
                                Value = mtfresult.FindCrossResult.result[0].tilt.tilt_y,
                                TestValue = mtfresult.FindCrossResult.result[0].tilt.tilt_y.ToString("F4")
                            };

                            ObjectiveTestResult.OptCenterRotation = new ObjectiveTestItem()
                            {
                                Name = "OptCenterRotation",
                                LowLimit = SPECConfig.RotationMin,
                                UpLimit = SPECConfig.RotationMax,
                                Value = mtfresult.FindCrossResult.result[0].rotationAngle,
                                TestValue = mtfresult.FindCrossResult.result[0].rotationAngle.ToString("F4")
                            };

                            result.ViewResultOpticCenter.XTilt = ObjectiveTestResult.OptCenterXTilt;
                            result.ViewResultOpticCenter.YTilt = ObjectiveTestResult.OptCenterYTilt;
                            result.ViewResultOpticCenter.Rotation = ObjectiveTestResult.OptCenterRotation;

                            result.Result = result.Result && ObjectiveTestResult.OptCenterXTilt.TestResult;
                            result.Result = result.Result && ObjectiveTestResult.OptCenterYTilt.TestResult;
                            result.Result = result.Result && ObjectiveTestResult.OptCenterRotation.TestResult;

                        }

                        List<BinocularFusionModel> AlgResultModels = BinocularFusionDao.Instance.GetAllByPid(AlgResultMaster.Id);
                        if (AlgResultModels.Count == 1)
                        {



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
                try
                {
                    if (SocketControl.Current.Stream != null)
                    {
                        var values = Enum.GetValues(typeof(ARVR1TestType));
                        int currentIndex = Array.IndexOf(values, CurrentTestType);
                        int nextIndex = (currentIndex + 1) % values.Length;
                        // 跳过 None（假设 None 是第一个）
                        if ((ARVR1TestType)values.GetValue(nextIndex) == ARVR1TestType.None)
                            nextIndex = (nextIndex + 1) % values.Length;
                        ARVR1TestType aRVRTestType = (ARVR1TestType)values.GetValue(nextIndex);

                        if (aRVRTestType == ARVR1TestType.Ghost)
                        {
                            log.Info("ARVR测试完成");

                            ObjectiveTestResult.TotalResult = true;

                            string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                            string filePath = Path.Combine(ProjectARVRLiteConfig.Instance.ResultSavePath, $"ObjectiveTestResults_{timeStr}.csv");

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
                catch(Exception ex)
                {
                    log.ErrorExt(ex);
                }

            }
            else
            {
                log.Info("找不到连接的Socket");
            }
        }



        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            ProjectARVRLiteConfig.Instance.Height = row2.ActualHeight;
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
                            await Task.Delay(ProjectARVRLiteConfig.Instance.ViewImageReadDelay);
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
                Task.Run(async () =>
                {
                    await Task.Delay(200);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (IsSavePicture)
                        {
                            IsSavePicture = false;
                            string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                            if (Directory.Exists(ProjectARVRLiteConfig.Instance.ResultSavePath))
                            {
                                string filePath = Path.Combine(ProjectARVRLiteConfig.Instance.ResultSavePath, $"{result.TestType}_{timeStr}.png");
                                ImageView.ImageViewModel.Save(filePath);
                            }
                        }
                    });
                });

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
                            outtext += $"{item.Name}  X:{item.X.ToString("F2")} Y:{item.Y.ToString("F2")} Z:{item.Z.ToString("F2")} x:{item.x.ToString("F2")} y:{item.y.ToString("F2")} u:{item.u.ToString("F2")} v:{item.v.ToString("F2")} cct:{item.CCT.ToString("F2")} wave:{item.Wave.ToString("F2")}{Environment.NewLine}";
                        }
                    }

                    outtext += $"CenterCorrelatedColorTemperature:{result.ViewResultWhite.CenterCorrelatedColorTemperature.TestValue}  LowLimit:{result.ViewResultWhite.CenterCorrelatedColorTemperature.LowLimit} UpLimit:{result.ViewResultWhite.CenterCorrelatedColorTemperature.UpLimit},Rsult{(result.ViewResultWhite.CenterCorrelatedColorTemperature.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                    outtext += $"Luminance_uniformity:{result.ViewResultWhite.LuminanceUniformity.TestValue} LowLimit:{result.ViewResultWhite.LuminanceUniformity.LowLimit}  UpLimit:{result.ViewResultWhite.LuminanceUniformity.UpLimit},Rsult{(result.ViewResultWhite.LuminanceUniformity.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                    outtext += $"Color_uniformity:{result.ViewResultWhite.ColorUniformity.TestValue} LowLimit:{result.ViewResultWhite.ColorUniformity.LowLimit} UpLimit:{result.ViewResultWhite.ColorUniformity.UpLimit},Rsult{(result.ViewResultWhite.ColorUniformity.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";

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
                    outtext += $"中心点x:{result.ViewResultOpticCenter.FindCrossDetailViewReslut.FindCrossResult.result[0].center.x} 中心点y:{result.ViewResultOpticCenter.FindCrossDetailViewReslut.FindCrossResult.result[0].center.y}" + Environment.NewLine;

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