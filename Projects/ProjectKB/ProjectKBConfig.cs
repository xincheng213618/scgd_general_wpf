using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using Newtonsoft.Json;
using ProjectKB.Auth;
using ProjectKB.Modbus;
using ProjectKB.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;

namespace ProjectKB
{
    public class ProjectKBConfig: ViewModelBase, IConfig
    {
        public static ProjectKBConfig Instance => ConfigService.Instance.GetRequiredService<ProjectKBConfig>();
        [JsonIgnore]
        public RelayCommand OpenTemplateCommand { get; set; }
        [JsonIgnore]
        public RelayCommand OpenFlowEngineToolCommand { get; set; }
        [JsonIgnore]
        public RelayCommand OpenLogCommand { get; set; }
        [JsonIgnore]
        public RelayCommand OpenModbusCommand { get; set; }
        [JsonIgnore]
        public RelayCommand EditConfigCommand { get; set; }
        [JsonIgnore]
        public RelayCommand OpenSocketConfigCommand { get; set; }

        public static ViewResultManager ViewResultManager => ViewResultManager.GetInstance();
        public static SummaryManager SummaryManager => SummaryManager.GetInstance();
        public static RecipeManager RecipeManager => RecipeManager.GetInstance();

        public ProjectKBConfig()
        {
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate(), a => KBAuthManager.GetInstance().IsAdmin);
            OpenFlowEngineToolCommand = new RelayCommand(a => OpenFlowEngineTool(), a => KBAuthManager.GetInstance().IsAdmin);
            TemplateItemSource = TemplateFlow.Params;
            OpenLogCommand = new RelayCommand(a => OpenLog());
            EditConfigCommand = new RelayCommand(a => EditConfig(), a => KBAuthManager.GetInstance().IsAdmin);
            OpenModbusCommand = new RelayCommand(a => OpenModbus(), a => KBAuthManager.GetInstance().IsAdmin);
            OpenSocketConfigCommand = new RelayCommand(a => OepnSocketConfig(), a => KBAuthManager.GetInstance().IsAdmin);

            KBAuthManager.GetInstance().IsAdminChanged += (s, e) =>
            {
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            };
        }

        [DisplayName("显示日志面板"), Category("KB")]
        [Description("关闭后隐藏主界面运行日志和底部实时日志面板；仍可通过状态栏“日志”按钮打开独立日志窗口。")]
        public bool LogControlVisibility { get => _LogControlVisibility; set { _LogControlVisibility = value; OnPropertyChanged(); } }
        private bool _LogControlVisibility = true;

        [DisplayName("重试次数")]
        public int TryCountMax { get => _TryCountMax; set { _TryCountMax = value; OnPropertyChanged(); } }
        private int _TryCountMax = 2;

        [DisplayName("允许测试失败")]
        public bool AllowTestFailures { get => _AllowTestFailures; set { _AllowTestFailures = value; OnPropertyChanged(); } }
        private bool _AllowTestFailures = true;

        [DisplayName("自动触发时SN为空不检测"), Category("KB")]
        [Description("开启后，PLC自动触发时如果SN为空，只记录日志并忽略本次指令；手动检测不受影响。")]
        public bool IgnoreAutoRunWhenSnEmpty { get => _IgnoreAutoRunWhenSnEmpty; set { _IgnoreAutoRunWhenSnEmpty = value; OnPropertyChanged(); } }
        private bool _IgnoreAutoRunWhenSnEmpty;

        public void EditConfig()
        {
            new PropertyEditorWindow(this) { Owner = Application.Current.GetActiveWindow(),WindowStartupLocation =WindowStartupLocation.CenterOwner }.ShowDialog();
            ConfigService.Instance.SaveConfigs();
        }
        public static void OepnSocketConfig()
        {
            PropertyEditorWindow propertyEditorWindow = new PropertyEditorWindow(SocketConfig.Instance) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            propertyEditorWindow.Show();
        }


        public static void OpenModbus()
        {
            ModbusConnect modbusConnect = new ModbusConnect() { Owner = Application.Current.GetActiveWindow() };
            modbusConnect.ShowDialog();
        }

        public static void OpenLog()
        {
            WindowLog windowLog = new WindowLog() { Owner = Application.Current.GetActiveWindow() };
            windowLog.Show();
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
        public string SN { get => _SN; set { if (SNlocked) return;  _SN = value; OnPropertyChanged(); SNChanged?.Invoke(this, value); } }
        private string _SN = string.Empty;

        public static ModbusControl ModbusControl => ModbusControl.GetInstance();

        [DisplayName("自动连接Modbus"), Category("KB")]
        public bool AutoModbusConnect { get => _AutoModbusConnect; set { _AutoModbusConnect = value; OnPropertyChanged(); } }
        private bool _AutoModbusConnect = true;
        [DisplayName("KBLVSacle"), Category("KB")]
        public double KBLVSacle { get => _KBLVSacle; set { _KBLVSacle = value; OnPropertyChanged(); } }
        private double _KBLVSacle = 0.006583904;





    }
}
