using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Jsons.LargeFlow;
using ColorVision.UI;
using Newtonsoft.Json;
using ProjectLUX.PluginConfig;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
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

        [JsonIgnore]
        public RelayCommand OpenTemplateCommand { get; set; }
        [JsonIgnore]
        public RelayCommand OpenFlowEngineToolCommand { get; set; }
        [JsonIgnore]
        public RelayCommand OpenTemplateLargeCommand { get; set; }
        [JsonIgnore]
        public RelayCommand OpenEditLargeCommand { get; set; }
        [JsonIgnore]
        public RelayCommand OpenLogCommand { get; set; }
        [JsonIgnore]
        public RelayCommand OpenConfigCommand { get; set; }
        [JsonIgnore]
        public RelayCommand OpenChangeLogCommand { get; set; }
        [JsonIgnore]
        public RelayCommand OpenReadMeCommand { get; set; }

        [JsonIgnore]
        public RelayCommand InitTestCommand { get; set; }


        public ProjectLUXConfig()
        {
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate());
            OpenFlowEngineToolCommand = new RelayCommand(a => OpenFlowEngineTool());
            TemplateItemSource = TemplateFlow.Params;

            OpenTemplateLargeCommand = new RelayCommand(a => OpenTemplateLarge());
            OpenEditLargeCommand = new RelayCommand(a => OpenEditLargeFlow());
            TemplateLargeItemSource = TemplateLargeFlow.Params;
            OpenLogCommand = new RelayCommand(a => OpenLog());
            OpenConfigCommand = new RelayCommand(a => OpenConfig());
            OpenChangeLogCommand = new RelayCommand(a => OpenChangeLog());
            OpenReadMeCommand = new RelayCommand(a => OpenReadMe());

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

        [DisplayName("允许测试失败")]
        public bool AllowTestFailures { get => _AllowTestFailures; set { _AllowTestFailures = value; OnPropertyChanged(); } }
        private bool _AllowTestFailures = true;

        [DisplayName("RefreshResult")]
        public bool RefreshResult { get => _RefreshResult; set { _RefreshResult = value; OnPropertyChanged(); } }
        private bool _RefreshResult = true;



        public void OpenConfig()
        {
            new PropertyEditorWindow(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            ConfigService.Instance.SaveConfigs();
        }

        public static void OpenResourceName(string title, string resourceName)
        {
            // 获取当前执行的程序集
            Assembly assembly = Assembly.GetExecutingAssembly();

            // 资源文件的完整名称

            // 确保资源名称正确
            string[] resourceNames = assembly.GetManifestResourceNames();

            // 读取资源文件内容
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    Console.WriteLine("资源文件未找到。请检查资源名称。");
                    return;
                }

                using (StreamReader reader = new StreamReader(stream))
                {
                    string content = reader.ReadToEnd();

                    string html = Markdig.Markdown.ToHtml(content);
                    new MarkdownViewWindow(html) { Title = title, Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
                }
            }
        }

        public static void OpenChangeLog()
        {
            // 资源文件的完整名称
            string resourceName = "ProjectLUX.CHANGELOG.md";
            OpenResourceName("CHANGELOG", resourceName);
        }
        public static void OpenReadMe()
        {
            // 资源文件的完整名称
            string resourceName = "ProjectLUX.README.md";
            OpenResourceName("README",resourceName);
        }
        public static void OpenLog()
        {
            WindowLog windowLog = new WindowLog() { Owner = Application.Current.GetActiveWindow() };
            windowLog.Show();
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
        public ObservableCollection<TemplateModel<TJLargeFlowParam>> TemplateLargeItemSource { get => _TemplateLargeItemSource; set { _TemplateLargeItemSource = value; OnPropertyChanged(); } }
        private ObservableCollection<TemplateModel<TJLargeFlowParam>> _TemplateLargeItemSource;
        public int TemplateLargeSelectedIndex { get => _TemplateLargeSelectedIndex; set { _TemplateLargeSelectedIndex = value; OnPropertyChanged(); } }
        private int _TemplateLargeSelectedIndex;
        public void OpenTemplateLarge()
        {
            new TemplateEditorWindow(new TemplateLargeFlow(), TemplateLargeSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
        public void OpenEditLargeFlow()
        {
            new EditLargeFlow(TemplateLargeFlow.Params[TemplateLargeSelectedIndex].Value) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }



        [JsonIgnore]
        public string SN { get => _SN; set { _SN = value; OnPropertyChanged(); } }
        private string _SN = string.Empty;

        public bool IsAutoUploadSn { get => _IsAutoUploadSn; set { _IsAutoUploadSn = value; OnPropertyChanged(); } }
        private bool _IsAutoUploadSn;

        public string ResultSavePath { get => _ResultSavePath; set { _ResultSavePath = value; OnPropertyChanged(); } }
        private string _ResultSavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),"TestReslut");


        public string ResultSavePath1 { get => _ResultSavePath1; set { _ResultSavePath1 = value; OnPropertyChanged(); } }
        private string _ResultSavePath1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "TestReslut");

        public double Height { get => _Height; set { _Height = value; OnPropertyChanged(); } }
        private double _Height = 300;
        public bool AutoModbusConnect { get => _AutoModbusConnect; set { _AutoModbusConnect = value; OnPropertyChanged(); } }
        private bool _AutoModbusConnect = true;

        public int ViewImageReadDelay { get => _ViewImageReadDelay; set { _ViewImageReadDelay = value; OnPropertyChanged(); } }
        private int _ViewImageReadDelay = 1000;


        public static LUXWindowConfig ProjectKBWindowConfig => LUXWindowConfig.Instance;

    }
}
