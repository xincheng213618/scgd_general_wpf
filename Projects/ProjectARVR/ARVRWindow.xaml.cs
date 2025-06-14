﻿#pragma warning disable
using ColorVision.Common.Algorithms;
using ColorVision.Common.MVVM;
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
using ColorVision.UI.Extension;
using CVCommCore.CVAlgorithm;
using FlowEngineLib;
using FlowEngineLib.Base;
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
    }

    public class ViewReslutDistortionGhost
    {
        public ColorVision.Engine.Templates.Jsons.Distortion2.Distortion2View Distortion2View { get; set; }

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

        public double Contrast { get; set; }
    }

    public class ViewResultWhite
    {
        public List<AlgResultLightAreaModel> AlgResultLightAreaModels { get; set; }

        public List<PoiResultCIExyuvData> PoiResultCIExyuvDatas { get; set; }

        public DFovView DFovView { get; set; }

        public double Luminance_uniformity { get; set; }
        public double Color_uniformity { get; set; }
    }

    public class ViewReslutCheckerboard
    {
        public List<PoiResultCIExyuvData> PoiResultCIExyuvDatas { get; set; }

        public double Chessboard_Contrast { get; set; }
    }


    public class SwitchPG
    {
        public ARVRTestType ARVRTestType { get; set; }
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
            if (flowControl != null && flowControl.IsFlowRun) return;
            if (FlowTemplate.SelectedValue is not FlowParam flowParam)
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "流程为空，请选择流程运行", "ColorVision");
                return;
            }
            ;
            string startNode = flowEngine.GetStartNodeName();
            if (string.IsNullOrWhiteSpace(startNode))
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "找不到完整流程，运行失败", "ColorVision");
                return;
            }
            ;
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
            try
            {
                BatchResultMasterModel batch = new BatchResultMasterModel();
                batch.Name = string.IsNullOrEmpty(name) ? sn : name;
                batch.Code = sn;
                batch.CreateDate = DateTime.Now;
                batch.TenantId = 0;
                BatchResultMasterDao.Instance.Save(batch);
            }
            catch (Exception ex)
            {
                log.Info(ex);
            }
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

            if (ViewResluts.FirstOrDefault(a => a.SN == ProjectARVRConfig.Instance.SN) is ProjectARVRReuslt result1)
            {
                result1.CopyTo(result);
            }

            result.Model = FlowTemplate.Text;
            result.Id = Batch.Id;
            result.SN = ProjectARVRConfig.Instance.SN;

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

                                ObjectiveTestResult.CenterCorrelatedColorTemperature = new ObjectiveTestItem()
                                {
                                    Name = "CenterCorrelatedColorTemperature",
                                    TestValue = poiResultCIExyuvData.CCT.ToString(),
                                    LowLimit = SPECConfig.CenterCorrelatedColorTemperatureMin.ToString(),
                                    UpLimit = SPECConfig.CenterCorrelatedColorTemperatureMax.ToString()
                                };
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
                                result.ViewResultWhite.Luminance_uniformity = viewReslut.PoiAnalysisResult.result.Value;


                                ObjectiveTestResult.LuminanceUniformity = new ObjectiveTestItem()
                                {
                                    Name = "Luminance_uniformity(%)",
                                    TestValue = (viewReslut.PoiAnalysisResult.result.Value * 100).ToString("F3") + "%",
                                    LowLimit = SPECConfig.LuminanceUniformityMin.ToString(),
                                    UpLimit = SPECConfig.LuminanceUniformityMax.ToString(),
                                };

                            }

                        }
                        if (AlgResultMaster.TName.Contains("Color_uniformity"))
                        {
                            List<DetailCommonModel> detailCommonModels = DeatilCommonDao.Instance.GetAllByPid(AlgResultMaster.Id);
                            if (detailCommonModels.Count == 1)
                            {
                                PoiAnalysisDetailViewReslut viewReslut = new PoiAnalysisDetailViewReslut(detailCommonModels[0]);
                                result.ViewResultWhite.Color_uniformity = viewReslut.PoiAnalysisResult.result.Value;

                                ObjectiveTestResult.ColorUniformity = new ObjectiveTestItem()
                                {
                                    Name = "Color_uniformity",
                                    TestValue = (viewReslut.PoiAnalysisResult.result.Value).ToString("F5"),
                                    LowLimit = SPECConfig.ColorUniformityMin.ToString(),
                                    UpLimit = SPECConfig.ColorUniformityMax.ToString()
                                };
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
                                LowLimit = SPECConfig.DiagonalFieldOfViewAngleMin.ToString(),
                                UpLimit = SPECConfig.DiagonalFieldOfViewAngleMax.ToString(),

                                TestValue = view1.Result.result.D_Fov.ToString("F3")
                            };
                            ObjectiveTestResult.HorizontalFieldOfViewAngle = new ObjectiveTestItem()
                            {
                                Name = "HorizontalFieldOfViewAngle",
                                LowLimit = SPECConfig.HorizontalFieldOfViewAngleMin.ToString(),
                                UpLimit = SPECConfig.HorizontalFieldOfViewAngleMax.ToString(),
                                TestValue = view1.Result.result.ClolorVisionH_Fov.ToString("F3")
                            };
                            ObjectiveTestResult.VerticalFieldOfViewAngle = new ObjectiveTestItem()
                            {
                                Name = "VerticalFieldOfViewAngle",
                                LowLimit = SPECConfig.VerticalFieldOfViewAngleMin.ToString(),
                                UpLimit = SPECConfig.VerticalFieldOfViewAngleMax.ToString(),
                                TestValue = view1.Result.result.ClolorVisionV_Fov.ToString("F3")
                            };

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
                                result.ViewResultBlack.Contrast = result.ViewResultWhite.PoiResultCIExyuvDatas[5].Y / result.ViewResultBlack.PoiResultCIExyuvDatas[0].Y;

                                ObjectiveTestResult.FOFOContrast = new ObjectiveTestItem()
                                {
                                    Name = "FOFOContrast",
                                    LowLimit = SPECConfig.FOFOContrastMin.ToString(),
                                    UpLimit = SPECConfig.FOFOContrastMax.ToString(),
                                    TestValue = result.ViewResultBlack.Contrast.ToString("F2")
                                };
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


                                result.ViewReslutCheckerboard.Chessboard_Contrast = viewReslut.PoiAnalysisResult.result.Value;


                                ObjectiveTestResult.ChessboardContrast = new ObjectiveTestItem()
                                {
                                    Name = "Chessboard_Contrast",
                                    LowLimit = SPECConfig.ChessboardContrastMin.ToString(),
                                    UpLimit = SPECConfig.ChessboardContrastMax.ToString(),
                                    TestValue = result.ViewReslutCheckerboard.Chessboard_Contrast.ToString("F2")
                                };
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
                                        LowLimit = SPECConfig.MTF_H_Center_0FMin.ToString(),
                                        UpLimit = SPECConfig.MTF_H_Center_0FMax.ToString(),
                                        TestValue = mtf.mtfValue.ToString() 
                                    };
                                }

                                if (mtf.name == "LeftUp_0.5F_H")
                                {
                                    ObjectiveTestResult.MTF_H_LeftUp_0_5F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_H_LeftUp_0_5F",
                                        LowLimit = SPECConfig.MTF_H_LeftUp_0_5FMin.ToString(),
                                        UpLimit = SPECConfig.MTF_H_LeftUp_0_5FMax.ToString(),
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                }
                                if (mtf.name == "RightUp_0.5F_H")
                                {
                                    ObjectiveTestResult.MTF_H_RightUp_0_5F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_H_RightUp_0_5F",
                                        LowLimit = SPECConfig.MTF_H_RightUp_0_5FMin.ToString(),
                                        UpLimit = SPECConfig.MTF_H_RightUp_0_5FMax.ToString(),
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                }
                                if (mtf.name == "LeftDown_0.5F_H")
                                {
                                    ObjectiveTestResult.MTF_H_LeftDown_0_5F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_H_LeftDown_0_5F",
                                        LowLimit = SPECConfig.MTF_H_LeftDown_0_5FMin.ToString(),
                                        UpLimit = SPECConfig.MTF_H_LeftDown_0_5FMax.ToString(),
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                }
                                if (mtf.name == "RightDown_0.5F_H")
                                {
                                    ObjectiveTestResult.MTF_H_RightDown_0_5F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_H_RightDown_0_5F",
                                        LowLimit = SPECConfig.MTF_H_RightDown_0_5FMin.ToString(),
                                        UpLimit = SPECConfig.MTF_H_RightDown_0_5FMax.ToString(),
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                }

                                if (mtf.name == "LeftUp_0.8F_H")
                                {
                                    ObjectiveTestResult.MTF_H_LeftUp_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_H_LeftUp_0_8F",
                                        LowLimit = SPECConfig.MTF_H_LeftUp_0_8FMin.ToString(),
                                        UpLimit = SPECConfig.MTF_H_LeftUp_0_8FMax.ToString(),
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                }
                                if (mtf.name == "RightUp_0.8F_H")
                                {
                                    ObjectiveTestResult.MTF_H_RightUp_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_H_RightUp_0_8F",
                                        LowLimit = SPECConfig.MTF_H_RightUp_0_8FMin.ToString(),
                                        UpLimit = SPECConfig.MTF_H_RightUp_0_8FMax.ToString(),
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                }
                                if (mtf.name == "LeftDown_0.8F_H")
                                {
                                    ObjectiveTestResult.MTF_H_LeftDown_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_H_LeftDown_0_8F",
                                        LowLimit = SPECConfig.MTF_H_LeftDown_0_8FMin.ToString(),
                                        UpLimit = SPECConfig.MTF_H_LeftDown_0_8FMax.ToString(),
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                }
                                if (mtf.name == "RightDown_0.8F_H")
                                {
                                    ObjectiveTestResult.MTF_H_RightDown_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_H_RightDown_0_8F",
                                        LowLimit = SPECConfig.MTF_H_RightDown_0_8FMin.ToString(),
                                        UpLimit = SPECConfig.MTF_H_RightDown_0_8FMax.ToString(),
                                        TestValue = mtf.mtfValue.ToString()
                                    };
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
                                        LowLimit = SPECConfig.MTF_V_Center_0FMin.ToString(),
                                        UpLimit = SPECConfig.MTF_V_Center_0FMax.ToString(),
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                }
                                if (mtf.name == "LeftUp_0.5F_V")
                                {
                                    ObjectiveTestResult.MTF_V_LeftUp_0_5F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_V_LeftUp_0_5F",
                                        LowLimit = SPECConfig.MTF_V_LeftUp_0_5FMin.ToString(),
                                        UpLimit = SPECConfig.MTF_V_LeftUp_0_5FMax.ToString(),
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                }
                                if (mtf.name == "RightUp_0.5F_V")
                                {
                                    ObjectiveTestResult.MTF_V_RightUp_0_5F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_V_RightUp_0_5F",
                                        LowLimit = SPECConfig.MTF_V_RightUp_0_5FMin.ToString(),
                                        UpLimit = SPECConfig.MTF_V_RightUp_0_5FMax.ToString(),
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                }
                                if (mtf.name == "LeftDown_0.5F_V")
                                {
                                    ObjectiveTestResult.MTF_V_LeftDown_0_5F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_V_LeftDown_0_5F",
                                        LowLimit = SPECConfig.MTF_V_LeftDown_0_5FMin.ToString(),
                                        UpLimit = SPECConfig.MTF_V_LeftDown_0_5FMax.ToString(),
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                }
                                if (mtf.name == "RightDown_0.5F_V")
                                {
                                    ObjectiveTestResult.MTF_V_RightDown_0_5F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_V_RightDown_0_5F",
                                        LowLimit = SPECConfig.MTF_V_RightDown_0_5FMin.ToString(),
                                        UpLimit = SPECConfig.MTF_V_RightDown_0_5FMax.ToString(),
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                }
                                if (mtf.name == "LeftUp_0.8F_V")
                                {
                                    ObjectiveTestResult.MTF_V_LeftUp_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_V_LeftUp_0_8F",
                                        LowLimit = SPECConfig.MTF_V_LeftUp_0_8FMin.ToString(),
                                        UpLimit = SPECConfig.MTF_V_LeftUp_0_8FMax.ToString(),
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                }
                                if (mtf.name == "RightUp_0.8F_V")
                                {
                                    ObjectiveTestResult.MTF_V_RightUp_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_V_RightUp_0_8F",
                                        LowLimit = SPECConfig.MTF_V_RightUp_0_8FMin.ToString(),
                                        UpLimit = SPECConfig.MTF_V_RightUp_0_8FMax.ToString(),
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                }
                                if (mtf.name == "LeftDown_0.8F_V")
                                {
                                    ObjectiveTestResult.MTF_V_LeftDown_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_V_LeftDown_0_8F",
                                        LowLimit = SPECConfig.MTF_V_LeftDown_0_8FMin.ToString(),
                                        UpLimit = SPECConfig.MTF_V_LeftDown_0_8FMax.ToString(),
                                        TestValue = mtf.mtfValue.ToString()
                                    };
                                }
                                if (mtf.name == "RightDown_0.8F_V")
                                {
                                    ObjectiveTestResult.MTF_V_RightDown_0_8F = new ObjectiveTestItem()
                                    {
                                        Name = "MTF_V_RightDown_0_8F",
                                        LowLimit = SPECConfig.MTF_V_RightDown_0_8FMin.ToString(),
                                        UpLimit = SPECConfig.MTF_V_RightDown_0_8FMax.ToString(),
                                        TestValue = mtf.mtfValue.ToString()
                                    };
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
                                LowLimit = SPECConfig.HorizontalTVDistortionMin.ToString(),
                                UpLimit = SPECConfig.HorizontalTVDistortionMax.ToString(),
                                TestValue = blackMuraView.DistortionReslut.TVDistortion.HorizontalRatio.ToString("F5")
                            };
                            ObjectiveTestResult.VerticalTVDistortion = new ObjectiveTestItem()
                            {
                                Name = "VerticalTVDistortion",
                                LowLimit = SPECConfig.VerticalTVDistortionMin.ToString(),
                                UpLimit = SPECConfig.VerticalTVDistortionMax.ToString(),
                                TestValue = blackMuraView.DistortionReslut.TVDistortion.VerticalRatio.ToString("F5")
                            };
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
                                LowLimit = SPECConfig.XTiltMin.ToString(),
                                UpLimit = SPECConfig.XTiltMax.ToString(),
                                TestValue = result.ViewResultOpticCenter.BinocularFusionModel.XDegree.ToString("F4")
                            };
                            ObjectiveTestResult.YTilt = new ObjectiveTestItem()
                            {
                                Name = "YTilt",
                                LowLimit = SPECConfig.YTiltMin.ToString(),
                                UpLimit = SPECConfig.YTiltMax.ToString(),
                                TestValue = result.ViewResultOpticCenter.BinocularFusionModel.YDegree.ToString("F4")
                            };
                            ObjectiveTestResult.Rotation = new ObjectiveTestItem()
                            {
                                Name = "Rotation",
                                LowLimit = SPECConfig.RotationMin.ToString(),
                                UpLimit = SPECConfig.RotationMax.ToString(),
                                TestValue = result.ViewResultOpticCenter.BinocularFusionModel.ZDegree.ToString("F4")
                            };
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
            ViewResluts.Add(result);

            listView1.SelectedIndex = ViewResluts.Count - 1;
            if (SocketManager.GetInstance().TcpClients.Count > 0)
            {
                log.Info("连接的Socket ");
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
                        ObjectiveTestResult.TotalResultString = ObjectiveTestResult.TotalResult ? "PASS" : "Fail";

                        var response = new SocketResponse
                        {
                            Version = "1.0",
                            MsgID = "",
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
                            MsgID = "",
                            EventName = "SwitchPG",
                            Code = -1,
                            Msg = "",
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
                    outtext += $"Luminance_uniformity:{result.ViewResultWhite.Luminance_uniformity}" + Environment.NewLine;
                    outtext += $"Color_uniformity:{result.ViewResultWhite.Color_uniformity}" + Environment.NewLine;
                    break;
                case ARVRTestType.Black:
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
                    outtext += $"Luminance_uniformity:{result.ViewResultWhite.Luminance_uniformity}" + Environment.NewLine;
                    outtext += $"Color_uniformity:{result.ViewResultWhite.Color_uniformity}" + Environment.NewLine;

                    outtext += $"黑画面 测试项：自动AA区域定位算法+关注点算法+序列对比度算法(中心亮度比值)" + Environment.NewLine;

                    outtext += $"Contrast:{result.ViewResultBlack.Contrast}" + Environment.NewLine;
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
                    outtext += $"HorizontalRatio:{result.ViewReslutDistortionGhost.Distortion2View.DistortionReslut.TVDistortion.HorizontalRatio}" + Environment.NewLine;
                    outtext += $"VerticalRatio:{result.ViewReslutDistortionGhost.Distortion2View.DistortionReslut.TVDistortion.VerticalRatio}" + Environment.NewLine;

                    break;
                case ARVRTestType.Chessboard:
                    outtext += $"棋盘格 测试项：" + Environment.NewLine;
                    outtext += $"Chessboard_Contrast:{result.ViewReslutCheckerboard.Chessboard_Contrast}" + Environment.NewLine;
                    break;
                case ARVRTestType.OpticCenter:
                    outtext += $"OpticCenter 测试项：" + Environment.NewLine;
                    outtext += $"中心点x:{result.ViewResultOpticCenter.BinocularFusionModel.CrossMarkCenterX} 中心点y:{result.ViewResultOpticCenter.BinocularFusionModel.CrossMarkCenterY}" + Environment.NewLine;
                    outtext += $"XDegree:{result.ViewResultOpticCenter.BinocularFusionModel.XDegree} YDegree:{result.ViewResultOpticCenter.BinocularFusionModel.YDegree} ZDegree:{result.ViewResultOpticCenter.BinocularFusionModel.ZDegree}" + Environment.NewLine;
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
        public void Dispose()
        {
            timer?.Dispose();
            GC.SuppressFinalize(this);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (SocketManager.GetInstance().TcpClients.Count > 0)
            {
                TcpClient tcpClient = SocketManager.GetInstance().TcpClients[0];



                SocketResponse request = new SocketResponse() { EventName = "SwitchPG", Data = new SwitchPG() { ARVRTestType = ARVRTestType.White } };
                byte[] response1 = Encoding.UTF8.GetBytes(request.ToJsonN());
                tcpClient.GetStream().Write(response1, 0, response1.Length);
            }
            else
            {
                MessageBox.Show("找不到链接的客户端");
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (SocketManager.GetInstance().TcpClients.Count > 0)
            {
                TcpClient tcpClient = SocketManager.GetInstance().TcpClients[0];
                SocketResponse request = new SocketResponse() { EventName = "SwitchPG", Data = new SwitchPG() { ARVRTestType = ARVRTestType.Black } };
                byte[] response1 = Encoding.UTF8.GetBytes(request.ToJsonN());
                tcpClient.GetStream().Write(response1, 0, response1.Length);


            }
            else
            {
                MessageBox.Show("找不到链接的客户端");
            }
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            if (SocketManager.GetInstance().TcpClients.Count > 0)
            {
                TcpClient tcpClient = SocketManager.GetInstance().TcpClients[0];
                SocketResponse request = new SocketResponse() { EventName = "SwitchPG", Data = new SwitchPG() { ARVRTestType = ARVRTestType.Distortion } };

                //string value = request.ToJsonN();
                //var response = JsonConvert.DeserializeObject<SocketResponse>(value);

                //if (response.EventName == "SwitchPG")
                //{
                //    var switchPg = (response.Data as JObject)?.ToObject<SwitchPG>();

                //}

                byte[] response1 = Encoding.UTF8.GetBytes(request.ToJsonN());
                tcpClient.GetStream().Write(response1, 0, response1.Length);
            }
            else
            {
                MessageBox.Show("找不到链接的客户端");
            }
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            if (SocketManager.GetInstance().TcpClients.Count > 0)
            {
                TcpClient tcpClient = SocketManager.GetInstance().TcpClients[0];
                SocketResponse request = new SocketResponse() { EventName = "SwitchPG", Data = new SwitchPG() { ARVRTestType = ARVRTestType.Chessboard } };
                byte[] response1 = Encoding.UTF8.GetBytes(request.ToJsonN());
                tcpClient.GetStream().Write(response1, 0, response1.Length);
            }
            else
            {
                MessageBox.Show("找不到链接的客户端");
            }
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            if (SocketManager.GetInstance().TcpClients.Count > 0)
            {
                TcpClient tcpClient = SocketManager.GetInstance().TcpClients[0];
                SocketResponse request = new SocketResponse() { EventName = "SwitchPG", Data = new SwitchPG() { ARVRTestType = ARVRTestType.MTFH } };
                byte[] response1 = Encoding.UTF8.GetBytes(request.ToJsonN());
                tcpClient.GetStream().Write(response1, 0, response1.Length);
            }
            else
            {
                MessageBox.Show("找不到链接的客户端");
            }
        }

        private void Button_Click_6(object sender, RoutedEventArgs e)
        {
            if (SocketManager.GetInstance().TcpClients.Count > 0)
            {
                TcpClient tcpClient = SocketManager.GetInstance().TcpClients[0];
                SocketResponse request = new SocketResponse() { EventName = "SwitchPG", Data = new SwitchPG() { ARVRTestType = ARVRTestType.MTFV } };
                byte[] response1 = Encoding.UTF8.GetBytes(request.ToJsonN());
                tcpClient.GetStream().Write(response1, 0, response1.Length);
            }
            else
            {
                MessageBox.Show("找不到链接的客户端");
            }
        }

        private void Button_Click_7(object sender, RoutedEventArgs e)
        {
            if (SocketManager.GetInstance().TcpClients.Count > 0)
            {
                TcpClient tcpClient = SocketManager.GetInstance().TcpClients[0];
                SocketResponse request = new SocketResponse() { EventName = "SwitchPG", Data = new SwitchPG() { ARVRTestType = ARVRTestType.BKscreeenDefectDetection } };
                byte[] response1 = Encoding.UTF8.GetBytes(request.ToJsonN());
                tcpClient.GetStream().Write(response1, 0, response1.Length);
            }
            else
            {
                MessageBox.Show("找不到链接的客户端");
            }
        }

        private void Button_Click_8(object sender, RoutedEventArgs e)
        {
            if (SocketManager.GetInstance().TcpClients.Count > 0)
            {
                TcpClient tcpClient = SocketManager.GetInstance().TcpClients[0];
                SocketResponse request = new SocketResponse() { EventName = "SwitchPG", Data = new SwitchPG() { ARVRTestType = ARVRTestType.Chessboard } };
                byte[] response1 = Encoding.UTF8.GetBytes(request.ToJsonN());
                tcpClient.GetStream().Write(response1, 0, response1.Length);
            }
            else
            {
                MessageBox.Show("找不到链接的客户端");
            }
        }

        private void Button_Click_9(object sender, RoutedEventArgs e)
        {
            if (SocketManager.GetInstance().TcpClients.Count > 0)
            {
                TcpClient tcpClient = SocketManager.GetInstance().TcpClients[0];
                SocketResponse request = new SocketResponse() { EventName = "ProjectARVRResult", Data = new ProjectARVRReuslt() };
                byte[] response1 = Encoding.UTF8.GetBytes(request.ToJsonN());
                tcpClient.GetStream().Write(response1, 0, response1.Length);
            }
            else
            {
                MessageBox.Show("找不到链接的客户端");
            }
        }
    }
}