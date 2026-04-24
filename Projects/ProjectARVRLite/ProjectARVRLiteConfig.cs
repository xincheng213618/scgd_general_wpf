using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using Newtonsoft.Json;
using ProjectARVRLite.PluginConfig;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;

namespace ProjectARVRLite
{
    public class ProjectARVRLiteConfig: ViewModelBase, IConfig
    {
        public static ProjectARVRLiteConfig Instance => ConfigService.Instance.GetRequiredService<ProjectARVRLiteConfig>();

        public static ViewResultManager ViewResultManager => ViewResultManager.GetInstance();
        public static RecipeManager RecipeManager => RecipeManager.GetInstance();
        public static FixManager FixManager => FixManager.GetInstance();
        public static SummaryManager SummaryManager => SummaryManager.GetInstance();
        public static TestTypeConfigManager TestTypeConfigManager => TestTypeConfigManager.GetInstance();

        [JsonIgnore]
        public RelayCommand OpenTemplateCommand { get; set; }
        [JsonIgnore]
        public RelayCommand OpenFlowEngineToolCommand { get; set; }
        [JsonIgnore]
        public RelayCommand OpenConfigCommand { get; set; }
        [JsonIgnore]
        public RelayCommand InitTestCommand { get; set; }

        public ProjectARVRLiteConfig()
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
        private int _StepIndex = 5;

        public bool LogControlVisibility { get => _LogControlVisibility; set { _LogControlVisibility = value; OnPropertyChanged(); } }
        private bool _LogControlVisibility = true;


        [DisplayName("重试次数")]
        public int TryCountMax { get => _TryCountMax; set { _TryCountMax = value; OnPropertyChanged(); } }
        private int _TryCountMax = 2;

        [DisplayName("允许测试失败")]
        public bool AllowTestFailures { get => _AllowTestFailures; set { _AllowTestFailures = value; OnPropertyChanged(); } }
        private bool _AllowTestFailures = true;

        public void OpenConfig()
        {
            new PropertyEditorWindow(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            ConfigService.Instance.SaveConfigs();
        }



        [JsonIgnore]
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
