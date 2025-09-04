using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine;
using ColorVision.Engine.MQTT;
using ColorVision.Database;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Jsons.BlackMura;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor.Draw;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.LogImp;
using CVCommCore.CVAlgorithm;
using FlowEngineLib;
using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ProjectBlackMura
{

    public class BlackMuraResult:ViewModelBase
    {
        public int Id { get => _Id; set { _Id = value; OnPropertyChanged(); } }
        private int _Id;

        public string Model { get => _Model; set { _Model = value; OnPropertyChanged(); } }
        private string _Model = string.Empty;
        public string Code { get; set; }

        public string SN { get => _SN; set { _SN = value; OnPropertyChanged(); } }
        private string _SN;
        public string WhiteFilePath { get => _WhiteFilePath; set { _WhiteFilePath = value; OnPropertyChanged(); } }
        private string _WhiteFilePath = string.Empty;


        public bool Result { get => _Result; set { _Result = value; OnPropertyChanged(); } }
        private bool _Result = true;

        public List<PoiResultCIExyuvData> PoiResultCIExyuvDatas { get; set; } = new List<PoiResultCIExyuvData>();

    }

    public class BlackMuraWindowConfig : WindowConfig
    {
        public static BlackMuraWindowConfig Instance => ConfigService.Instance.GetRequiredService<BlackMuraWindowConfig>();
    }

    /// <summary>
    /// Interaction logic for MarkdownViewWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MainWindow));

        public static ObservableCollection<BlackMuraResult> ViewResluts => ProjectBlackMuraConfig.Instance.ViewResluts;

        public MainWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            BlackMuraWindowConfig.Instance.SetWindow(this);

        }
        private LogOutput? logOutput;

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = ProjectBlackMuraConfig.Instance;
            listView1.ItemsSource = ViewResluts;
            InitFlow();

            ComboBoxSer.ItemsSource = SerialPort.GetPortNames();
            ComboBoxSer.SelectedIndex = 0;
            if (ProjectBlackMuraConfig.Instance.LogControlVisibility)
            {
                logOutput = new LogOutput("%date{HH:mm:ss} [%thread] %-5level %message%newline");
                LogGrid.Children.Add(logOutput);
            }

            this.Closed += (s, e) =>
            {
                HYMesManager.GetInstance().CCPICompleted -= MainWindow_CCPICompleted;
                HYMesManager.GetInstance().CONCompleted -= MainWindow_CONCompleted;

                timer.Change(Timeout.Infinite, 500); // 停止定时器
                timer?.Dispose();
                logOutput?.Dispose();
            };

            MesGrid.DataContext = HYMesManager.GetInstance();

            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => Delete(), (s, e) => e.CanExecute = listView1.SelectedIndex > -1));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, (s, e) => listView1.SelectAll(), (s, e) => e.CanExecute = true));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, ListViewUtils.Copy, (s, e) => e.CanExecute = true));
            HYMesManager.GetInstance().CCPICompleted += MainWindow_CCPICompleted;
            HYMesManager.GetInstance().CONCompleted += MainWindow_CONCompleted;

        }

        private void MainWindow_CONCompleted(object? sender, bool e)
        {
            if (HYMesConfig.Instance.IsSingleMes) return;
            ProjectBlackMuraConfig.Instance.StepIndex = 1;
            HYMesManager.GetInstance().PGSwitch(0);
        }

        public void Delete()
        {
            if (listView1.SelectedIndex < 0) return;
            var item = listView1.SelectedItem as BlackMuraResult;
            if (item == null) return;
            if (MessageBox.Show(Application.Current.GetActiveWindow(), $"是否删除 {item.SN} 测试结果？", "ColorVision", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                ViewResluts.Remove(item);
            }
        }

        #region FlowRun
        public STNodeEditor STNodeEditorMain { get; set; }
        private FlowEngineControl flowEngine;
        private Timer timer;
        Stopwatch stopwatch = new Stopwatch();

        public void InitFlow()
        {
            MQTTConfig mqttcfg = MQTTSetting.Instance.MQTTConfig;
            MQTTHelper.SetDefaultCfg(mqttcfg.Host, mqttcfg.Port, mqttcfg.UserName, mqttcfg.UserPwd, false, null);
            flowEngine = new FlowEngineControl(false);
            STNodeEditorMain = new STNodeEditor();
            STNodeEditorMain.LoadAssembly("FlowEngineLib.dll");
            flowEngine.AttachNodeEditor(STNodeEditorMain);

            FlowTemplate.SelectionChanged += (s, e) =>
            {
                if (ProjectBlackMuraConfig.Instance.TemplateSelectedIndex > -1)
                {
                    string Name = TemplateFlow.Params[ProjectBlackMuraConfig.Instance.TemplateSelectedIndex].Key;
                    if (ProjectBlackMuraConfig.Instance.JudgeConfigs.TryGetValue(Name, out JudgeConfig sPECConfig))
                    {
                        ProjectBlackMuraConfig.Instance.JudgeConfig = sPECConfig;
                    }
                    else
                    {
                        sPECConfig = new JudgeConfig();
                        ProjectBlackMuraConfig.Instance.JudgeConfigs.TryAdd(Name, sPECConfig);
                        ProjectBlackMuraConfig.Instance.JudgeConfig = sPECConfig;
                    }
                }
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

        public async Task RefreshAsync()
        {
            if (FlowTemplate.SelectedIndex < 0) return;

            try
            {
                foreach (var item in STNodeEditorMain.Nodes.OfType<CVCommonNode>())
                    item.nodeRunEvent -= UpdateMsg;

                flowEngine.LoadFromBase64(TemplateFlow.Params[FlowTemplate.SelectedIndex].Value.DataBase64, MqttRCService.GetInstance().ServiceTokens);

                for (int i = 0; i < 2000; i++)
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
                MessageBox.Show(ex.Message);
                flowEngine.LoadFromBase64(string.Empty);
            }
        }


        private void TimeRun(object? state)
        {
            UpdateMsg(state);
        }
        string Msg1;
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
                        msg = Msg1 + Environment.NewLine + $"已经执行：{elapsedTime}";
                    }
                    else
                    {
                        long remainingMilliseconds = LastFlowTime - elapsedMilliseconds;
                        TimeSpan remaining = TimeSpan.FromMilliseconds(remainingMilliseconds);
                        string remainingTime = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}:{elapsed.Milliseconds:D4}";

                        msg = Msg1 + Environment.NewLine + $"已经执行：{elapsedTime}, 上次执行：{LastFlowTime} ms, 预计还需要：{remainingTime}";
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

        bool IsSingle;
        private void TestClick(object sender, RoutedEventArgs e)
        {
            IsSingle = true;
            if (string.IsNullOrWhiteSpace(SNtextBox.Text))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "产品编号为空");
                return;
            }
            BlackMudraResult = new BlackMudraResult();
            BlackMudraResult.SN = SNtextBox.Text;

            CurrentTestType = BlackMuraTestType.None;
            if (FlowTemplate.Text.Contains("White"))
                CurrentTestType = BlackMuraTestType.White;
            if (FlowTemplate.Text.Contains("Black"))
                CurrentTestType = BlackMuraTestType.Black;
            if (FlowTemplate.Text.Contains("Red"))
                CurrentTestType = BlackMuraTestType.Red;
            if (FlowTemplate.Text.Contains("Green"))
                CurrentTestType = BlackMuraTestType.Green;
            if (FlowTemplate.Text.Contains("Blue"))
                CurrentTestType = BlackMuraTestType.Blue;
            log.Info(CurrentTestType);

            _= RunTemplate();
        }
        bool LastCompleted = true;

        BlackMuraResult CurrentFlowResult;
        long LastFlowTime;
        int TryCount;

        public async Task RunTemplate()
        {
            if (flowControl != null && flowControl.IsFlowRun) return;

            TryCount++;
            LastFlowTime = FlowEngineConfig.Instance.FlowRunTime.TryGetValue(FlowTemplate.Text, out long time) ? time : 0;

            CurrentFlowResult = new BlackMuraResult();
            CurrentFlowResult.SN = SNtextBox.Text;
            CurrentFlowResult.Code = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            await RefreshAsync();
            if (string.IsNullOrWhiteSpace(flowEngine.GetStartNodeName())) { log.Info("找不到完整流程，运行失败"); return; } 
            //多潘基次次
            log.Info($"IsReady{flowEngine.IsReady}");
            if (!flowEngine.IsReady)
            {
                string base64 = string.Empty;
                flowEngine.LoadFromBase64(base64);
                await RefreshAsync();
                log.Info($"IsReady1{flowEngine.IsReady}");
            }


            flowControl ??= new FlowControl(MQTTControl.GetInstance(), flowEngine);

            flowControl.FlowCompleted += FlowControl_FlowCompleted;
            stopwatch.Reset();
            stopwatch.Start();

            BatchResultMasterDao.Instance.Save(new MeasureBatchModel() { Name = CurrentFlowResult.SN, Code = CurrentFlowResult.Code, CreateDate = DateTime.Now });

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
            stopwatch.Stop();
            timer.Change(Timeout.Infinite, 500); // 停止定时器

            FlowEngineConfig.Instance.FlowRunTime[FlowTemplate.Text] = stopwatch.ElapsedMilliseconds;

            log.Info($"流程执行Elapsed Time: {stopwatch.ElapsedMilliseconds} ms");

            if (FlowControlData.EventName == "Completed")
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        Processing(FlowControlData.SerialNumber);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message);
                    }
                });
                TryCount = 0;
            }
            else if (FlowControlData.EventName == "OverTime")
            {
                log.Info("流程运行超时，正在重新尝试");
                log.Info($"IsReady{flowEngine.IsReady}");
                if (TryCount < ProjectBlackMuraConfig.Instance.TryCountMax)
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
            }
        }


        #endregion

        private void Processing(string SerialNumber)
        {
            var Batch = BatchResultMasterDao.Instance.GetByCode(SerialNumber);
            if (Batch == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到批次号，请检查流程配置", "ColorVision");
                return;
            }
            BlackMuraResult result = new BlackMuraResult();
            result.Model = FlowTemplate.Text;
            result.Id = Batch.Id;
            result.SN = BlackMudraResult.SN;
            if (CurrentTestType == BlackMuraTestType.White)
            {
                foreach (var item in AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id))
                {
                    if (item.ImgFileType == ViewResultAlgType.BlackMura_Calc)
                    {
                        List<BlackMuraModel> AlgResultModels = BlackMuraDao.Instance.GetAllByPid(item.Id);
                        if (AlgResultModels.Count > 0)
                        {
                            BlackMuraView blackMuraView = new BlackMuraView(AlgResultModels[0]);

                            blackMuraView.ResultJson.LvAvg = blackMuraView.ResultJson.LvAvg;
                            blackMuraView.ResultJson.LvMax = blackMuraView.ResultJson.LvMax * BlackMuraConfig.Instance.WLvMaxScale;
                            blackMuraView.ResultJson.LvMin = blackMuraView.ResultJson.LvMin * BlackMuraConfig.Instance.WLvMinScale;
                            blackMuraView.ResultJson.ZaRelMax = blackMuraView.ResultJson.ZaRelMax * BlackMuraConfig.Instance.WZaRelMaxScale;
                            blackMuraView.ResultJson.Uniformity = blackMuraView.ResultJson.LvMin / blackMuraView.ResultJson.LvMax * 100;

                            result.WhiteFilePath = item.ImgFile;

                            BlackMudraResult.WhiteImage.Mean = blackMuraView.ResultJson.LvAvg;
                            BlackMudraResult.WhiteImage.Max = blackMuraView.ResultJson.LvMax;
                            BlackMudraResult.WhiteImage.Min = blackMuraView.ResultJson.LvMin;
                            BlackMudraResult.WhiteImage.Uniformity = blackMuraView.ResultJson.Uniformity;
                            BlackMudraResult.WhiteImage.ZaRelmax = blackMuraView.ResultJson.ZaRelMax;
                            BlackMudraResult.WhiteImage.Bordersize = blackMuraView.ResultJson.Nle;

                        }
                    }

                    if (item.ImgFileType == ViewResultAlgType.POI_XYZ)
                    {
                        List<PoiPointResultModel> POIPointResultModels = PoiPointResultDao.Instance.GetAllByPid(item.Id);
                        int id = 0;
                        foreach (var model in POIPointResultModels)
                        {
                            PoiResultCIExyuvData poiResultCIExyuvData = new PoiResultCIExyuvData(model) { Id = id++ };
                            BlackMudraResult.WhiteImage.PoiResultCIExyuvDatas.Add(poiResultCIExyuvData);
                            result.PoiResultCIExyuvDatas.Add(poiResultCIExyuvData);

                        }

                        if (BlackMudraResult.WhiteImage.PoiResultCIExyuvDatas.Count > 0)
                        {
                            PoiResultCIExyuvData poiResultCIExyuvData = BlackMudraResult.WhiteImage.PoiResultCIExyuvDatas[BlackMudraResult.WhiteImage.PoiResultCIExyuvDatas.Count/2];
                            BlackMudraResult.WhiteImage.Wavelength = poiResultCIExyuvData.Wave;
                            BlackMudraResult.WhiteImage.Saturation = poiResultCIExyuvData.CCT;
                        }

                    }
                }
            }
            if (CurrentTestType == BlackMuraTestType.Black)
            {
                foreach (var item in AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id))
                {
                    if (item.ImgFileType == ViewResultAlgType.BlackMura_Calc)
                    {
                        List<BlackMuraModel> AlgResultModels = BlackMuraDao.Instance.GetAllByPid(item.Id);
                        if (AlgResultModels.Count > 0)
                        {
                            BlackMuraView blackMuraView = new BlackMuraView(AlgResultModels[0]);

                            blackMuraView.ResultJson.LvMax = blackMuraView.ResultJson.LvMax * BlackMuraConfig.Instance.BLvMaxScale;
                            blackMuraView.ResultJson.LvMin = blackMuraView.ResultJson.LvMin * BlackMuraConfig.Instance.BLvMinScale;
                            blackMuraView.ResultJson.ZaRelMax = blackMuraView.ResultJson.ZaRelMax * BlackMuraConfig.Instance.BZaRelMaxScale;
                            blackMuraView.ResultJson.Uniformity = blackMuraView.ResultJson.LvMin / blackMuraView.ResultJson.LvMax * 100;
                            result.WhiteFilePath = item.ImgFile;

                            BlackMudraResult.BlackImage.Mean = blackMuraView.ResultJson.LvAvg;
                            BlackMudraResult.BlackImage.Max = blackMuraView.ResultJson.LvMax;
                            BlackMudraResult.BlackImage.Min = blackMuraView.ResultJson.LvMin;
                            BlackMudraResult.BlackImage.Uniformity = blackMuraView.ResultJson.Uniformity;
                            BlackMudraResult.BlackImage.ZaRelmax = blackMuraView.ResultJson.ZaRelMax;
                            BlackMudraResult.BlackImage.Bordersize = blackMuraView.ResultJson.Nle;

                        }
                    }
                    if (item.ImgFileType == ViewResultAlgType.POI_XYZ)
                    {
                        List<PoiPointResultModel> POIPointResultModels = PoiPointResultDao.Instance.GetAllByPid(item.Id);
                        int id = 0;
                        foreach (var model in POIPointResultModels)
                        {
                            PoiResultCIExyuvData poiResultCIExyuvData = new PoiResultCIExyuvData(model) { Id = id++ };
                            BlackMudraResult.BlackImage.PoiResultCIExyuvDatas.Add(poiResultCIExyuvData);
                            result.PoiResultCIExyuvDatas.Add(poiResultCIExyuvData);
                        }


                        if (BlackMudraResult.BlackImage.PoiResultCIExyuvDatas.Count > 0)
                        {
                            PoiResultCIExyuvData poiResultCIExyuvData = BlackMudraResult.BlackImage.PoiResultCIExyuvDatas[BlackMudraResult.WhiteImage.PoiResultCIExyuvDatas.Count / 2];
                            BlackMudraResult.BlackImage.Wavelength = poiResultCIExyuvData.Wave;
                            BlackMudraResult.BlackImage.Saturation = poiResultCIExyuvData.CCT;
                        }
                    }
                }
            }

            if (CurrentTestType == BlackMuraTestType.Red)
            {
                foreach (var item in AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id))
                {
                    if (item.ImgFileType == ViewResultAlgType.BlackMura_Calc)
                    {
                        List<BlackMuraModel> AlgResultModels = BlackMuraDao.Instance.GetAllByPid(item.Id);
                        if (AlgResultModels.Count > 0)
                        {
                            BlackMuraView blackMuraView = new BlackMuraView(AlgResultModels[0]);

                            blackMuraView.ResultJson.LvMax = blackMuraView.ResultJson.LvMax * BlackMuraConfig.Instance.WLvMaxScale;
                            blackMuraView.ResultJson.LvMin = blackMuraView.ResultJson.LvMin * BlackMuraConfig.Instance.WLvMinScale;
                            blackMuraView.ResultJson.ZaRelMax = blackMuraView.ResultJson.ZaRelMax * BlackMuraConfig.Instance.WZaRelMaxScale;
                            blackMuraView.ResultJson.Uniformity = blackMuraView.ResultJson.LvMin / blackMuraView.ResultJson.LvMax * 100;

                            result.WhiteFilePath = item.ImgFile;

                            BlackMudraResult.RedImage.Mean = blackMuraView.ResultJson.LvAvg;
                            BlackMudraResult.RedImage.Max = blackMuraView.ResultJson.LvMax;
                            BlackMudraResult.RedImage.Min = blackMuraView.ResultJson.LvMin;
                            BlackMudraResult.RedImage.Uniformity = blackMuraView.ResultJson.Uniformity;
                            BlackMudraResult.RedImage.ZaRelmax = blackMuraView.ResultJson.ZaRelMax;
                            BlackMudraResult.RedImage.Bordersize = blackMuraView.ResultJson.Nle;

                        }
                    }
                    if (item.ImgFileType == ViewResultAlgType.POI_XYZ)
                    {
                        List<PoiPointResultModel> POIPointResultModels = PoiPointResultDao.Instance.GetAllByPid(item.Id);
                        int id = 0;
                        foreach (var model in POIPointResultModels)
                        {
                            PoiResultCIExyuvData poiResultCIExyuvData = new PoiResultCIExyuvData(model) { Id = id++ };
                            BlackMudraResult.RedImage.PoiResultCIExyuvDatas.Add(poiResultCIExyuvData);
                            result.PoiResultCIExyuvDatas.Add(poiResultCIExyuvData);
                        }
                        if (BlackMudraResult.RedImage.PoiResultCIExyuvDatas.Count > 0)
                        {
                            PoiResultCIExyuvData poiResultCIExyuvData = BlackMudraResult.RedImage.PoiResultCIExyuvDatas[BlackMudraResult.WhiteImage.PoiResultCIExyuvDatas.Count / 2];
                            BlackMudraResult.RedImage.Wavelength = poiResultCIExyuvData.Wave;
                            BlackMudraResult.RedImage.Saturation = poiResultCIExyuvData.CCT;
                        }
                    }
                }
            }

            if (CurrentTestType == BlackMuraTestType.Green)
            {
                foreach (var item in AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id))
                {
                    if (item.ImgFileType == ViewResultAlgType.BlackMura_Calc)
                    {
                        List<BlackMuraModel> AlgResultModels = BlackMuraDao.Instance.GetAllByPid(item.Id);
                        if (AlgResultModels.Count > 0)
                        {
                            BlackMuraView blackMuraView = new BlackMuraView(AlgResultModels[0]);

                            blackMuraView.ResultJson.LvMax = blackMuraView.ResultJson.LvMax * BlackMuraConfig.Instance.WLvMaxScale;
                            blackMuraView.ResultJson.LvMin = blackMuraView.ResultJson.LvMin * BlackMuraConfig.Instance.WLvMinScale;
                            blackMuraView.ResultJson.ZaRelMax = blackMuraView.ResultJson.ZaRelMax * BlackMuraConfig.Instance.WZaRelMaxScale;
                            blackMuraView.ResultJson.Uniformity = blackMuraView.ResultJson.LvMin / blackMuraView.ResultJson.LvMax * 100;

                            result.WhiteFilePath = item.ImgFile;

                            BlackMudraResult.GreenImage.Mean = blackMuraView.ResultJson.LvAvg;
                            BlackMudraResult.GreenImage.Max = blackMuraView.ResultJson.LvMax;
                            BlackMudraResult.GreenImage.Min = blackMuraView.ResultJson.LvMin;
                            BlackMudraResult.GreenImage.Uniformity = blackMuraView.ResultJson.Uniformity;
                            BlackMudraResult.GreenImage.ZaRelmax = blackMuraView.ResultJson.ZaRelMax;
                            BlackMudraResult.GreenImage.Bordersize = blackMuraView.ResultJson.Nle;

                        }
                    }
                    if (item.ImgFileType == ViewResultAlgType.POI_XYZ)
                    {
                        List<PoiPointResultModel> POIPointResultModels = PoiPointResultDao.Instance.GetAllByPid(item.Id);
                        int id = 0;
                        foreach (var model in POIPointResultModels)
                        {
                            PoiResultCIExyuvData poiResultCIExyuvData = new PoiResultCIExyuvData(model) { Id = id++ };
                            BlackMudraResult.GreenImage.PoiResultCIExyuvDatas.Add(poiResultCIExyuvData);
                            result.PoiResultCIExyuvDatas.Add(poiResultCIExyuvData);
                        }
                        if (BlackMudraResult.GreenImage.PoiResultCIExyuvDatas.Count > 0)
                        {
                            PoiResultCIExyuvData poiResultCIExyuvData = BlackMudraResult.GreenImage.PoiResultCIExyuvDatas[BlackMudraResult.WhiteImage.PoiResultCIExyuvDatas.Count / 2];
                            BlackMudraResult.GreenImage.Wavelength = poiResultCIExyuvData.Wave;
                            BlackMudraResult.GreenImage.Saturation = poiResultCIExyuvData.CCT;
                        }
                    }
                }
            }

            if (CurrentTestType == BlackMuraTestType.Blue)
            {
                foreach (var item in AlgResultMasterDao.Instance.GetAllByBatchId(Batch.Id))
                {
                    if (item.ImgFileType == ViewResultAlgType.BlackMura_Calc)
                    {
                        List<BlackMuraModel> AlgResultModels = BlackMuraDao.Instance.GetAllByPid(item.Id);
                        if (AlgResultModels.Count > 0)
                        {
                            BlackMuraView blackMuraView = new BlackMuraView(AlgResultModels[0]);

                            blackMuraView.ResultJson.LvMax = blackMuraView.ResultJson.LvMax * BlackMuraConfig.Instance.WLvMaxScale;
                            blackMuraView.ResultJson.LvMin = blackMuraView.ResultJson.LvMin * BlackMuraConfig.Instance.WLvMinScale;
                            blackMuraView.ResultJson.ZaRelMax = blackMuraView.ResultJson.ZaRelMax * BlackMuraConfig.Instance.WZaRelMaxScale;
                            blackMuraView.ResultJson.Uniformity = blackMuraView.ResultJson.LvMin / blackMuraView.ResultJson.LvMax * 100;

                            result.WhiteFilePath = item.ImgFile;

                            BlackMudraResult.BlueImage.Mean = blackMuraView.ResultJson.LvAvg;
                            BlackMudraResult.BlueImage.Max = blackMuraView.ResultJson.LvMax;
                            BlackMudraResult.BlueImage.Min = blackMuraView.ResultJson.LvMin;
                            BlackMudraResult.BlueImage.Uniformity = blackMuraView.ResultJson.Uniformity;
                            BlackMudraResult.BlueImage.ZaRelmax = blackMuraView.ResultJson.ZaRelMax;
                            BlackMudraResult.BlueImage.Bordersize = blackMuraView.ResultJson.Nle;

                        }
                    }
                    if (item.ImgFileType == ViewResultAlgType.POI_XYZ)
                    {
                        List<PoiPointResultModel> POIPointResultModels = PoiPointResultDao.Instance.GetAllByPid(item.Id);
                        int id = 0;
                        foreach (var model in POIPointResultModels)
                        {
                            PoiResultCIExyuvData poiResultCIExyuvData = new PoiResultCIExyuvData(model) { Id = id++ };
                            BlackMudraResult.BlueImage.PoiResultCIExyuvDatas.Add(poiResultCIExyuvData);
                            result.PoiResultCIExyuvDatas.Add(poiResultCIExyuvData);
                        }

                        if (BlackMudraResult.BlueImage.PoiResultCIExyuvDatas.Count > 0)
                        {
                            PoiResultCIExyuvData poiResultCIExyuvData = BlackMudraResult.BlueImage.PoiResultCIExyuvDatas[BlackMudraResult.WhiteImage.PoiResultCIExyuvDatas.Count / 2];
                            BlackMudraResult.BlueImage.Wavelength = poiResultCIExyuvData.Wave;
                            BlackMudraResult.BlueImage.Saturation = poiResultCIExyuvData.CCT;
                        }
                    }
                }
            }


            ViewResluts.Insert(0,result);
            listView1.SelectedIndex = 0;
            ProcessCompleted();

        } 

        public void ProcessCompleted()
        {
            if (IsSingle)
            {
                try
                {
                    if (Directory.Exists(ProjectBlackMuraConfig.Instance.ResultSavePath))
                    {
                        ExcelReportGenerator.GenerateExcel(Path.Combine(ProjectBlackMuraConfig.Instance.ResultSavePath, $"{BlackMudraResult.SN}.xlsx"), BlackMudraResult);
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
                IsSingle = false;
            }
            if (CurrentTestType  == BlackMuraTestType.White)
            {
                HYMesManager.GetInstance().PGSwitch(1);
            }
            else if (CurrentTestType == BlackMuraTestType.Black)
            {
                HYMesManager.GetInstance().PGSwitch(2);
            }
            else if (CurrentTestType == BlackMuraTestType.Red)
            {
                HYMesManager.GetInstance().PGSwitch(3);
            }
            else if (CurrentTestType == BlackMuraTestType.Green)
            {
                HYMesManager.GetInstance().PGSwitch(4);
            }
            else if (CurrentTestType == BlackMuraTestType.Blue)
            {
                HYMesManager.GetInstance().UploadSN(BlackMudraResult.SN);
                try
                {
                    if (Directory.Exists(ProjectBlackMuraConfig.Instance.ResultSavePath))
                    {
                        ExcelReportGenerator.GenerateExcel(Path.Combine(ProjectBlackMuraConfig.Instance.ResultSavePath, $"{BlackMudraResult.SN}.xlsx"), BlackMudraResult);
                    }
                }catch(Exception ex)
                {
                    log.Error(ex);
                }
                ProjectBlackMuraConfig.Instance.StepIndex = 6;
                HYMesManager.GetInstance().PGPowerOff();
            }

        }


        public async void AddPOIPoint(List<PoiResultCIExyuvData> PoiPoints)
        {
            ImageView.ImageShow.Clear();
            await Task.Delay(1000);
            for (int i = 0; i < PoiPoints.Count; i++)
            {
                if (i % 10000 == 0)
                    await Task.Delay(30);

                var item = PoiPoints[i];
                switch (item.POIPoint.PointType)
                {
                    case POIPointTypes.Circle:
                        CircleTextProperties circleTextProperties = new CircleTextProperties();
                        circleTextProperties.Center = new Point(item.POIPoint.PixelX, item.POIPoint.PixelY);
                        circleTextProperties.Radius = item.POIPoint.Radius;
                        circleTextProperties.Brush = Brushes.Transparent;
                        circleTextProperties.Pen = new Pen(Brushes.Red, 1);
                        circleTextProperties.Id = item.Id;
                        circleTextProperties.Text = item.Name;
                        DVCircleText Circle = new DVCircleText(circleTextProperties);
                        Circle.Render();
                        ImageView.AddVisual(Circle);
                        break;
                    case POIPointTypes.Rect:
                        DVRectangleText Rectangle = new DVRectangleText();
                        Rectangle.Attribute.Rect = new Rect(item.POIPoint.PixelX - item.POIPoint.Width / 2, item.POIPoint.PixelY - item.POIPoint.Height / 2, item.POIPoint.Width, item.POIPoint.Height);
                        Rectangle.Attribute.Brush = Brushes.Transparent;
                        Rectangle.Attribute.Pen = new Pen(Brushes.Red, 1);
                        Rectangle.Attribute.Id = item.Id;
                        Rectangle.Attribute.Text = item.Name;
                        Rectangle.Render();
                        ImageView.AddVisual(Rectangle);
                        break;
                    default:
                        break;
                }
            }
        }


        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                var result = ViewResluts[listView.SelectedIndex];
                GenoutputText();

                Task.Run(async () =>
                {
                    if (File.Exists(result.WhiteFilePath))
                    {
                        try
                        {
                            var fileInfo = new FileInfo(result.WhiteFilePath);
                            log.Debug($"fileInfo.Length{fileInfo.Length}");
                            using (var fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                            {
                                log.Debug("文件可以读取，没有被占用。");
                            }
                            if (fileInfo.Length > 0)
                            {
                                _ = Application.Current.Dispatcher.BeginInvoke(() =>
                                {
                                    ImageView.ImageShow.Clear();
                                    ImageView.OpenImage(result.WhiteFilePath);
                                    AddPOIPoint(result.PoiResultCIExyuvDatas);
                                });
                            }
                        }
                        catch
                        {
                            log.Debug("文件还在写入");
                            await Task.Delay(ProjectBlackMuraConfig.Instance.ViewImageReadDelay);
                            _ = Application.Current.Dispatcher.BeginInvoke(() =>
                            {
                                ImageView.ImageShow.Clear();
                                ImageView.OpenImage(result.WhiteFilePath);
                                AddPOIPoint(result.PoiResultCIExyuvDatas);
                            });
                        }
                    }
                });

            }

        }

        public void GenoutputText()
        {
            outputText.Background = BlackMudraResult.Result ? Brushes.Lime : Brushes.Red;
            outputText.Document.Blocks.Clear(); // 清除之前的内容

            string outtext = string.Empty;
            outtext += $"SN:{BlackMudraResult.SN}" + Environment.NewLine;
            outtext += $"{DateTime.Now:yyyy/MM//dd HH:mm:ss}" + Environment.NewLine;

            Run run = new Run(outtext);
            run.Foreground = BlackMudraResult.Result ? Brushes.Black : Brushes.White;
            run.FontSize += 1;

            var paragraph = new Paragraph();
            paragraph.Inlines.Add(run);

            outputText.Document.Blocks.Add(paragraph);
            outtext = string.Empty;

            paragraph = new Paragraph();

            outtext += $"WhiteUniformity %  {BlackMudraResult.WhiteImage.Uniformity:F2} Pass" + Environment.NewLine;
            outtext += $"BlackUniformity %  {BlackMudraResult.BlackImage.Uniformity:F2} Pass" + Environment.NewLine;
            outtext += $"Gradient W - %/Dpixel  {BlackMudraResult.WhiteImage.ZaRelmax:F4} Pass" + Environment.NewLine;
            outtext += $"Gradient B - %/Dpixel  {BlackMudraResult.BlackImage.ZaRelmax:F4} Pass" + Environment.NewLine;
            outtext += Environment.NewLine; ;
            outtext += $"      White        Black        Red       Green      Blue" + Environment.NewLine;
            outtext += $"Lv {BlackMudraResult.WhiteImage.PoiResultCIExyuvDatas.AverageOrDefault(x => x.Y),10:F2} {BlackMudraResult.BlackImage.PoiResultCIExyuvDatas.AverageOrDefault(x => x.Y),10:F2} {BlackMudraResult.RedImage.PoiResultCIExyuvDatas.AverageOrDefault(x => x.Y),10:F2} {BlackMudraResult.GreenImage.PoiResultCIExyuvDatas.AverageOrDefault(x => x.Y),10:F2} {BlackMudraResult.BlueImage.PoiResultCIExyuvDatas.AverageOrDefault(x => x.Y),10:F2}" + Environment.NewLine;
            outtext += $"x  {BlackMudraResult.WhiteImage.PoiResultCIExyuvDatas.AverageOrDefault(x => x.x),10:F4} {BlackMudraResult.BlackImage.PoiResultCIExyuvDatas.AverageOrDefault(x => x.x),10:F4} {BlackMudraResult.RedImage.PoiResultCIExyuvDatas.AverageOrDefault(x => x.x),10:F4} {BlackMudraResult.GreenImage.PoiResultCIExyuvDatas.AverageOrDefault(x => x.x),10:F4} {BlackMudraResult.BlueImage.PoiResultCIExyuvDatas.AverageOrDefault(x => x.x),10:F4}" + Environment.NewLine;
            outtext += $"y  {BlackMudraResult.WhiteImage.PoiResultCIExyuvDatas.AverageOrDefault(x => x.y),10:F4} {BlackMudraResult.BlackImage.PoiResultCIExyuvDatas.AverageOrDefault(x => x.y),10:F4} {BlackMudraResult.RedImage.PoiResultCIExyuvDatas.AverageOrDefault(x => x.y),10:F4} {BlackMudraResult.GreenImage.PoiResultCIExyuvDatas.AverageOrDefault(x => x.y),10:F4} {BlackMudraResult.BlueImage.PoiResultCIExyuvDatas.AverageOrDefault(x => x.y),10:F4}" + Environment.NewLine;


            run = new Run(outtext);
            run.Foreground = BlackMudraResult.Result ? Brushes.Black : Brushes.White;
            run.FontSize += 1;

            paragraph.Inlines.Add(run);
            outtext = string.Empty;

            outputText.Document.Blocks.Add(paragraph);

            outtext += Environment.NewLine;
            outtext += $"Pass/Fail Criteria:" + Environment.NewLine;
            outtext += BlackMudraResult.Result ? "Pass" : "Fail" + Environment.NewLine;

            run = new Run(outtext);
            run.Foreground = BlackMudraResult.Result ? Brushes.Black : Brushes.White;
            run.FontSize += 1;
            paragraph = new Paragraph(run);
            outtext = string.Empty;
            outputText.Document.Blocks.Add(paragraph);
            SNtextBox.Focus();
        }


        private void listView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {

        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {

        }

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {

        }

        private void SNtextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {

        }

        private void GridSplitter_DragCompleted1(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {

        }

        private void Button_Click_Clear(object sender, RoutedEventArgs e)
        {
            ViewResluts.Clear();
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {

        }

        public void Dispose()
        {
            timer?.Dispose();
            logOutput?.Dispose();
            GC.SuppressFinalize(this);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (!HYMesManager.GetInstance().IsConnect)
            {
                _= HYMesManager.GetInstance().OpenPortAsync(ComboBoxSer.Text);

            }
            else
            {
                HYMesManager.GetInstance().Close();
            }
        }

        private void PG_PowerOn_Click(object sender, RoutedEventArgs e)
        {
            HYMesManager.GetInstance().PGPowerOn();
        }

        private void PG_PowerOff_Click(object sender, RoutedEventArgs e)
        {
            HYMesManager.GetInstance().PGPowerOff();
        }

        private void PG_PowerSwitch1_Click(object sender, RoutedEventArgs e)
        {
            HYMesManager.GetInstance().PGSwitch(0);
        }

        private void PG_PowerSwitch2_Click(object sender, RoutedEventArgs e)
        {
            HYMesManager.GetInstance().PGSwitch(1);

        }

        private void PG_PowerSwitch3_Click(object sender, RoutedEventArgs e)
        {
            HYMesManager.GetInstance().PGSwitch(2);

        }

        private void PG_PowerSwitch4_Click(object sender, RoutedEventArgs e)
        {
            HYMesManager.GetInstance().PGSwitch(3);
        }

        private void PG_PowerSwitch5_Click(object sender, RoutedEventArgs e)
        {
            HYMesManager.GetInstance().PGSwitch(4);
        }

        private void PG_PowerSwitch6_Click(object sender, RoutedEventArgs e)
        {
            HYMesManager.GetInstance().PGSwitch(15);
        }

        private void Test1_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SNtextBox.Text))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "产品编号为空");
                return;
            }
            BlackMudraResult = new BlackMudraResult();
            BlackMudraResult.SN = SNtextBox.Text;

            CurrentTestType = BlackMuraTestType.None;
            ProjectBlackMuraConfig.Instance.StepIndex = 0;
            HYMesManager.GetInstance().PGPowerOn();
        }
        public BlackMudraResult BlackMudraResult { get; set; } = new BlackMudraResult();
        public BlackMuraTestType CurrentTestType { get; set; } = BlackMuraTestType.None;

        private void MainWindow_CCPICompleted(object? sender, bool e)
        {
            if (HYMesConfig.Instance.IsSingleMes) return;
            var values = Enum.GetValues(typeof(BlackMuraTestType));
            int currentIndex = Array.IndexOf(values, CurrentTestType);
            int nextIndex = (currentIndex + 1) % values.Length;
            // 跳过 None（假设 None 是第一个）
            if ((BlackMuraTestType)values.GetValue(nextIndex) == BlackMuraTestType.None)
                nextIndex = (nextIndex + 1) % values.Length;
            CurrentTestType = (BlackMuraTestType)values.GetValue(nextIndex);

            if (CurrentTestType == BlackMuraTestType.White)
            {
                FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("White")).Value;
                _= RunTemplate();

            }
            else if (CurrentTestType == BlackMuraTestType.Black)
            {
                ProjectBlackMuraConfig.Instance.StepIndex = 2;

                FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("Black")).Value;
                _ = RunTemplate();
            }
            else if (CurrentTestType == BlackMuraTestType.Red)
            {
                ProjectBlackMuraConfig.Instance.StepIndex = 3;

                FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("Red")).Value;
                _ = RunTemplate();
            }
            else if (CurrentTestType == BlackMuraTestType.Green)
            {
                ProjectBlackMuraConfig.Instance.StepIndex = 4;
                FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("Green")).Value;
                _ = RunTemplate();
            }
            else if (CurrentTestType == BlackMuraTestType.Blue)
            {
                ProjectBlackMuraConfig.Instance.StepIndex = 5;
                FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains("Blue")).Value;
                _ = RunTemplate();
            }
        }

    }

    public enum BlackMuraTestType
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
        Red =3,
        Green = 4,
        Blue =5,
    }
}