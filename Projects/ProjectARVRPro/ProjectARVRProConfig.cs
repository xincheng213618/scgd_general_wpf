#pragma warning disable CA1805,CA1822
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using Newtonsoft.Json;
using ProjectARVRPro.PluginConfig;
using ProjectARVRPro.Process;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace ProjectARVRPro
{
    public class ProjectARVRProConfig: ViewModelBase, IConfig
    {
        public static ProjectARVRProConfig Instance => ConfigService.Instance.GetRequiredService<ProjectARVRProConfig>();

        public static ViewResultManager ViewResultManager => ViewResultManager.GetInstance();
        public static SummaryManager SummaryManager => SummaryManager.GetInstance();
        public static ProcessManager ProcessManager => ProcessManager.GetInstance();

        [JsonIgnore]
        public RelayCommand OpenTemplateCommand { get; set; }
        [JsonIgnore]
        public RelayCommand OpenFlowEngineToolCommand { get; set; }
        [JsonIgnore]
        public RelayCommand OpenConfigCommand { get; set; }
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


        public int StepIndex { get => _StepIndex; set { _StepIndex = value; OnPropertyChanged(); } }
        private int _StepIndex = 0;

        public bool LogControlVisibility { get => _LogControlVisibility; set { _LogControlVisibility = value; OnPropertyChanged(); } }
        private bool _LogControlVisibility = true;


        [DisplayName("重试次数")]
        public int TryCountMax { get => _TryCountMax; set { _TryCountMax = value; OnPropertyChanged(); } }
        private int _TryCountMax = 2;

        [DisplayName("允许测试失败")]
        public bool AllowTestFailures { get => _AllowTestFailures; set { _AllowTestFailures = value; OnPropertyChanged(); } }
        private bool _AllowTestFailures = true;

        [DisplayName("雷鸟串口")]
        public string ThunderbirdPortName { get => _ThunderbirdPortName; set { _ThunderbirdPortName = value; OnPropertyChanged(); } }
        private string _ThunderbirdPortName = string.Empty;

        [DisplayName("雷鸟波特率")]
        public int ThunderbirdBaudRate { get => _ThunderbirdBaudRate; set { _ThunderbirdBaudRate = value; OnPropertyChanged(); } }
        private int _ThunderbirdBaudRate = 115200;

        [DisplayName("雷鸟超时(ms)")]
        public int ThunderbirdTimeoutMs { get => _ThunderbirdTimeoutMs; set { _ThunderbirdTimeoutMs = value; OnPropertyChanged(); } }
        private int _ThunderbirdTimeoutMs = 1000;

        [DisplayName("雷鸟自动连接")]
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
        private double _ResultOverlayFontSize = 8;

        [DisplayName("结果图层自动刷新"), Category("结果图层")]
        public bool ResultOverlayAutoRefresh { get => _ResultOverlayAutoRefresh; set { _ResultOverlayAutoRefresh = value; OnPropertyChanged(); } }
        private bool _ResultOverlayAutoRefresh;

        public void OpenConfig()
        {
            new PropertyEditorWindow(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            ConfigService.Instance.SaveConfigs();
        }


        [JsonIgnore]
        [Browsable(false)]
        public ObservableCollection<TemplateModel<FlowParam>> TemplateItemSource { get => _TemplateItemSource; set { _TemplateItemSource = value; OnPropertyChanged(); } }
        private ObservableCollection<TemplateModel<FlowParam>> _TemplateItemSource;

        public int TemplateSelectedIndex { get => _TemplateSelectedIndex; set { _TemplateSelectedIndex = value; OnPropertyChanged(); } }
        private int _TemplateSelectedIndex;
        public void OpenTemplate()
        {
            new TemplateEditorWindow(new TemplateFlow(), TemplateSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public void OpenFlowEngineTool()
        {
            new FlowEngineToolWindow(TemplateFlow.Params[TemplateSelectedIndex].Value) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public event EventHandler<string> SNChanged;

        [DisplayName("SN锁")]
        public bool SNlocked { get => _SNlocked; set { _SNlocked = value; OnPropertyChanged(); } }
        private bool _SNlocked;

        [JsonIgnore]
        public string SN { get => _SN; set { if (SNlocked) return; _SN = value; OnPropertyChanged(); SNChanged?.Invoke(this, value); } }
        private string _SN = string.Empty;
    }
}
