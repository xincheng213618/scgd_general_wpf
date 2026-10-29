#pragma warning disable CA1805,CA1822
using ColorVision.Common.MVVM;
using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using Newtonsoft.Json;
using ProjectARVRPro.PluginConfig;
using ProjectARVRPro.Process;
using ProjectARVRPro.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace ProjectARVRPro
{
    public sealed class ResultStatisticsWindowState
    {
        public int SelectedTabIndex { get; set; }
        public ResultStatisticsPeriodMode HomePeriodMode { get; set; } = ResultStatisticsPeriodMode.Day;
        public bool HomeHourlyProduction { get; set; }
        public DateTime HomeAnchorDate { get; set; } = DateTime.Today;
        public ResultStatisticsPeriodMode RecordPeriodMode { get; set; } = ResultStatisticsPeriodMode.Day;
        public DateTime RecordAnchorDate { get; set; } = DateTime.Today;
        public string RecordSn { get; set; } = string.Empty;
        public int RecordResultIndex { get; set; }
        public ResultStatisticsPeriodMode CombinedPeriodMode { get; set; } = ResultStatisticsPeriodMode.Day;
        public DateTime CombinedAnchorDate { get; set; } = DateTime.Today;
        public string CombinedSn { get; set; } = string.Empty;
        public int CombinedResultIndex { get; set; }
        public ResultStatisticsPeriodMode FlowPeriodMode { get; set; } = ResultStatisticsPeriodMode.Day;
        public DateTime FlowAnchorDate { get; set; } = DateTime.Today;
        public string FlowName { get; set; } = string.Empty;
        public int FlowResultIndex { get; set; }
    }

    public sealed class ProjectARVRProLogConfig : RealtimeLogViewConfig, IConfig
    {
        public static ProjectARVRProLogConfig Instance => ConfigService.Instance.GetRequiredService<ProjectARVRProLogConfig>();
    }

    public enum LuxVidMeasurementMode
    {
        [Description("自动聚焦")] AutoFocus,
        [Description("直接获取位置")] GetPosition
    }

    [DisplayName("模组检测设置")]
    public class ProjectARVRProConfig: ViewModelBase, IConfig
    {
        public static ProjectARVRProConfig Instance => ConfigService.Instance.GetRequiredService<ProjectARVRProConfig>();
        public static ViewResultManager ViewResultManager => ViewResultManager.GetInstance();
        public static ProcessManager ProcessManager => ProcessManager.GetInstance();
        public static ThunderbirdSerialManager ThunderbirdSerialManager => ThunderbirdSerialManager.GetInstance();

        [Browsable(false)]
        [JsonIgnore]
        public RelayCommand OpenTemplateCommand { get; set; }
        [Browsable(false)]
        [JsonIgnore]
        public RelayCommand OpenFlowEngineToolCommand { get; set; }
        [Browsable(false)]
        [JsonIgnore]
        public RelayCommand OpenConfigCommand { get; set; }
        [Browsable(false)]
        [JsonIgnore]
        public RelayCommand InitTestCommand { get; set; }

        public ProjectARVRProConfig()
        {
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate());
            OpenFlowEngineToolCommand = new RelayCommand(a => OpenFlowEngineTool());
            TemplateItemSource = TemplateFlow.Params;
            OpenConfigCommand = new RelayCommand(a => OpenConfig());
            InitTestCommand = new RelayCommand(a => InitTest());
        }

        public void InitTest()
        {
            ProjectWindowInstance.WindowInstance.InitTest(string.Empty);
        }

        [Browsable(false)]
        public int StepIndex { get => _StepIndex; set { _StepIndex = value; OnPropertyChanged(); } }
        private int _StepIndex = 0;

        [DisplayName("显示日志栏"), Category("界面")]
        public bool LogControlVisibility { get => _LogControlVisibility; set { _LogControlVisibility = value; OnPropertyChanged(); } }
        private bool _LogControlVisibility = true;

        [DisplayName("显示结果栏"), Category("界面")]
        public bool ResultControlVisibility { get => _ResultControlVisibility; set { _ResultControlVisibility = value; OnPropertyChanged(); } }
        private bool _ResultControlVisibility = true;

        [DisplayName("显示状态栏"), Category("界面")]
        public bool StatusControlVisibility { get => _StatusControlVisibility; set { _StatusControlVisibility = value; OnPropertyChanged(); } }
        private bool _StatusControlVisibility = true;

        [DisplayName("显示进度栏"), Category("界面")]
        public bool ProgressControlVisibility { get => _ProgressControlVisibility; set { _ProgressControlVisibility = value; OnPropertyChanged(); } }
        private bool _ProgressControlVisibility = true;


        [DisplayName("最大尝试次数"), Category("测试策略")]
        [Description("单步测试的尝试上限，包含首次执行；0或1均不追加重试。一键执行不自动重试。")]
        public int TryCountMax { get => _TryCountMax; set { _TryCountMax = value; OnPropertyChanged(); } }
        private int _TryCountMax = 2;

        [DisplayName("失败后继续执行"), Category("测试策略")]
        [Description("流程或结果处理失败后继续下一项，不会把失败结果改成通过。")]
        public bool AllowTestFailures { get => _AllowTestFailures; set { _AllowTestFailures = value; OnPropertyChanged(); } }
        private bool _AllowTestFailures = true;

        [DisplayName("LUX 协议设备号"), Category("LUX协议")]
        [Description("原 LUX 文本回包的前缀，例如 H03 或 H03AR；不参与流程批次 Code 的生成。")]
        public string LuxMachineNo { get => _LuxMachineNo; set { _LuxMachineNo = value; OnPropertyChanged(); } }
        private string _LuxMachineNo = "H03";

        [DisplayName("VID 测量方式"), Category("LUX协议")]
        [Description("自动聚焦使用相机当前选择的对焦模板；直接获取位置仅读取当前位置对应的 VID。两者使用相同的修正与限值。")]
        public LuxVidMeasurementMode LuxVidMode { get => _LuxVidMode; set { _LuxVidMode = value; OnPropertyChanged(); } }
        private LuxVidMeasurementMode _LuxVidMode;

        [DisplayName("VID 下限"), Category("LUX协议")]
        public double LuxVidMin { get => _LuxVidMin; set { _LuxVidMin = value; OnPropertyChanged(); } }
        private double _LuxVidMin;

        [DisplayName("VID 上限"), Category("LUX协议")]
        public double LuxVidMax { get => _LuxVidMax; set { _LuxVidMax = value; OnPropertyChanged(); } }
        private double _LuxVidMax;

        [DisplayName("VID 修正系数 K"), Category("LUX协议")]
        public double LuxVidFix { get => _LuxVidFix; set { _LuxVidFix = value; OnPropertyChanged(); } }
        private double _LuxVidFix = 1;

        [DisplayName("VID 修正偏移 B"), Category("LUX协议")]
        public double LuxVidB { get => _LuxVidB; set { _LuxVidB = value; OnPropertyChanged(); } }
        private double _LuxVidB;

        [DisplayName("串口名称"), Category("串口参数")]
        [Description("例如 COM3；从流程处理配置的“雷鸟连接与切图”中连接和调试设备。")]
        public string ThunderbirdPortName { get => _ThunderbirdPortName; set { _ThunderbirdPortName = value; OnPropertyChanged(); } }
        private string _ThunderbirdPortName = string.Empty;

        [DisplayName("波特率"), Category("串口参数"), PropertyEditorType(typeof(TextBaudRatePropertiesEditor))]
        public int ThunderbirdBaudRate { get => _ThunderbirdBaudRate; set { _ThunderbirdBaudRate = value; OnPropertyChanged(); } }
        private int _ThunderbirdBaudRate = 115200;

        [DisplayName("超时时间（ms）"), Category("串口参数")]
        public int ThunderbirdTimeoutMs { get => _ThunderbirdTimeoutMs; set { _ThunderbirdTimeoutMs = value; OnPropertyChanged(); } }
        private int _ThunderbirdTimeoutMs = 1000;

        [DisplayName("切图时自动连接"), Category("串口参数")]
        [Description("切图前若尚未连接，按以上参数尝试连接串口。")]
        public bool ThunderbirdAutoConnect { get => _ThunderbirdAutoConnect; set { _ThunderbirdAutoConnect = value; OnPropertyChanged(); } }
        private bool _ThunderbirdAutoConnect;

        [DisplayName("结果点位名称"), Category("结果图层")]
        public bool ResultOverlayShowName { get => _ResultOverlayShowName; set { _ResultOverlayShowName = value; OnPropertyChanged(); } }
        private bool _ResultOverlayShowName = true;

        [DisplayName("结果详细数据"), Category("结果图层")]
        public bool ResultOverlayShowDetail { get => _ResultOverlayShowDetail; set { _ResultOverlayShowDetail = value; OnPropertyChanged(); } }
        private bool _ResultOverlayShowDetail = true;

        [DisplayName("结果文字字号"), Category("结果图层")]
        public double ResultOverlayFontSize { get => _ResultOverlayFontSize; set { _ResultOverlayFontSize = Math.Max(0, value); OnPropertyChanged(); } }
        private double _ResultOverlayFontSize = 80;

        [DisplayName("结果图层自动刷新"), Category("结果图层")]
        public bool ResultOverlayAutoRefresh { get => _ResultOverlayAutoRefresh; set { _ResultOverlayAutoRefresh = value; OnPropertyChanged(); } }
        private bool _ResultOverlayAutoRefresh;

        [Browsable(false)]
        [JsonIgnore]
        // Search selections belong to this application session, not the saved configuration.
        public ResultStatisticsWindowState ResultStatisticsWindowState { get; set; } = new();

        public void OpenConfig()
        {
            ProjectWindowInstance.WindowInstance.OpenSettings(ProjectSettingsPage.Testing);
        }


        [JsonIgnore]
        [Browsable(false)]
        public ObservableCollection<TemplateModel<FlowParam>> TemplateItemSource { get => _TemplateItemSource; set { _TemplateItemSource = value; OnPropertyChanged(); } }
        private ObservableCollection<TemplateModel<FlowParam>> _TemplateItemSource;

        [Browsable(false)]
        public int TemplateSelectedIndex { get => _TemplateSelectedIndex; set { _TemplateSelectedIndex = value; OnPropertyChanged(); } }
        private int _TemplateSelectedIndex;
        public void OpenTemplate()
        {
            new FlowTemplateManagerWindow(new TemplateFlow(), TemplateSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public void OpenFlowEngineTool()
        {
            new FlowEngineToolWindow(TemplateFlow.GetParamOrDefault(TemplateSelectedIndex)) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public event EventHandler<string> SNChanged;

        [DisplayName("锁定 SN"), Category("测试策略")]
        [Description("锁定后不再接受 SN 修改；解除锁定后恢复更新。")]
        public bool SNlocked { get => _SNlocked; set { _SNlocked = value; OnPropertyChanged(); } }
        private bool _SNlocked;

        [JsonIgnore]
        [Browsable(false)]
        public string SN { get => _SN; set { if (SNlocked) return; _SN = value; OnPropertyChanged(); SNChanged?.Invoke(this, value); } }
        private string _SN = string.Empty;
    }
}
