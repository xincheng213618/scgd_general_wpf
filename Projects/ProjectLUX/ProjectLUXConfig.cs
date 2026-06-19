#pragma warning disable CA1822
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using Newtonsoft.Json;
using ProjectLUX.PluginConfig;
using ProjectLUX.Process;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace ProjectLUX
{
    public class ProjectLUXConfig: ViewModelBase, IConfig
    {
        public static ProjectLUXConfig Instance => ConfigService.Instance.GetRequiredService<ProjectLUXConfig>();
        public static SummaryManager SummaryManager => SummaryManager.GetInstance();
        public static RecipeManager RecipeManager => RecipeManager.GetInstance();
        public static FixManager FixManager => FixManager.GetInstance();
        public static ViewResultManager ViewResultManager => ViewResultManager.GetInstance();
        public static ProcessManager ProcessManager => ProcessManager.GetInstance();
        public static LUXWindowConfig WindowConfig => LUXWindowConfig.Instance;


        [JsonIgnore]
        public RelayCommand OpenTemplateCommand { get; set; }
        [JsonIgnore]
        public RelayCommand OpenFlowEngineToolCommand { get; set; }
        [JsonIgnore]
        public RelayCommand OpenTemplateLargeCommand { get; set; }
        [JsonIgnore]
        public RelayCommand OpenEditLargeCommand { get; set; }
        [JsonIgnore]
        public RelayCommand OpenConfigCommand { get; set; }

        [JsonIgnore]
        public RelayCommand InitTestCommand { get; set; }


        public ProjectLUXConfig()
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
        private int _StepIndex;

        public bool LogControlVisibility { get => _LogControlVisibility; set { _LogControlVisibility = value; OnPropertyChanged(); } }
        private bool _LogControlVisibility = true;


        [DisplayName("重试次数")]
        public int TryCountMax { get => _TryCountMax; set { _TryCountMax = value; OnPropertyChanged(); } }
        private int _TryCountMax = 2;


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

        [JsonIgnore]
        public string SN { get => _SN; set { _SN = value; OnPropertyChanged(); } }
        private string _SN = string.Empty;


        [PropertyEditorType(typeof(TextSelectFolderPropertiesEditor))]
        public string ResultSavePath { get => _ResultSavePath; set { _ResultSavePath = value; OnPropertyChanged(); } }
        private string _ResultSavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),"TestReslut");

        [DisplayName("CSV导出格式")]
        public ObjectiveTestResultCsvExportProfile CsvExportProfile { get => _CsvExportProfile; set { _CsvExportProfile = value; OnPropertyChanged(); } }
        private ObjectiveTestResultCsvExportProfile _CsvExportProfile = ObjectiveTestResultCsvExportProfile.Current;


        public double Height { get => _Height; set { _Height = value; OnPropertyChanged(); } }
        private double _Height = 300;

        public int ViewImageReadDelay { get => _ViewImageReadDelay; set { _ViewImageReadDelay = value; OnPropertyChanged(); } }
        private int _ViewImageReadDelay = 1000;

    }
}
