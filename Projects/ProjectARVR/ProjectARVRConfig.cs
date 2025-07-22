using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Jsons.LargeFlow;
using ColorVision.ImageEditor;
using ColorVision.UI;
using Newtonsoft.Json;
using ProjectARVR.Config;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;

namespace ProjectARVR
{
    public class ProjectARVRConfig: ViewModelBase, IConfig
    {
        public static ProjectARVRConfig Instance => ConfigService.Instance.GetRequiredService<ProjectARVRConfig>();
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
        public RelayCommand EditSPECConfigcommand { get; set; }

        public ProjectARVRConfig()
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

            EditSPECConfigcommand = new RelayCommand(a => EditSPECConfig());
        }

        [DisplayName("重试次数")]
        public int TryCountMax { get => _TryCountMax; set { _TryCountMax = value; NotifyPropertyChanged(); } }
        private int _TryCountMax = 2;

        [DisplayName("允许测试失败")]
        public bool AllowTestFailures { get => _AllowTestFailures; set { _AllowTestFailures = value; NotifyPropertyChanged(); } }
        private bool _AllowTestFailures = true;


        public void EditSPECConfig()
        {
            PropertyEditorWindow propertyEditorWindow = new PropertyEditorWindow(SPECConfig ,false) { Owner = Application.Current.GetActiveWindow() };
            propertyEditorWindow.ShowDialog();
        }


        public ImageViewConfig ImageViewConfig { get; set; } = new ImageViewConfig() { IsLayoutUpdated = true };

        public static void OpenConfig()
        {
            EditARVRConfig editProjectKBConfig = new EditARVRConfig() { Owner = Application.Current.GetActiveWindow() };
            editProjectKBConfig.ShowDialog();
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
            string resourceName = "ProjectARVR.CHANGELOG.md";
            OpenResourceName("CHANGELOG", resourceName);
        }
        public static void OpenReadMe()
        {
            // 资源文件的完整名称
            string resourceName = "ProjectARVR.README.md";
            OpenResourceName("README",resourceName);
        }
        public static void OpenLog()
        {
            WindowLog windowLog = new WindowLog() { Owner = Application.Current.GetActiveWindow() };
            windowLog.Show();
        }

        [JsonIgnore]
        public ObservableCollection<TemplateModel<FlowParam>> TemplateItemSource { get => _TemplateItemSource; set { _TemplateItemSource = value; NotifyPropertyChanged(); } }
        private ObservableCollection<TemplateModel<FlowParam>> _TemplateItemSource;

        public int TemplateSelectedIndex { get => _TemplateSelectedIndex; set { _TemplateSelectedIndex = value; NotifyPropertyChanged(); } }
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
        public ObservableCollection<TemplateModel<TJLargeFlowParam>> TemplateLargeItemSource { get => _TemplateLargeItemSource; set { _TemplateLargeItemSource = value; NotifyPropertyChanged(); } }
        private ObservableCollection<TemplateModel<TJLargeFlowParam>> _TemplateLargeItemSource;
        public int TemplateLargeSelectedIndex { get => _TemplateLargeSelectedIndex; set { _TemplateLargeSelectedIndex = value; NotifyPropertyChanged(); } }
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
        public string SN { get => _SN; set
            {
                if (!string.IsNullOrEmpty(value) && value.Length > SNMax)
                {
                    // 移除最前面的字符，使其长度为 14
                    _SN = value.Substring(value.Length - SNMax);
                }
                else
                {
                    _SN = value;
                }
                NotifyPropertyChanged(); } }
        private string _SN;

        public int SNMax { get => _SMMax; set { _SMMax = value; NotifyPropertyChanged(); } }
        private int _SMMax = 17;

        public bool IsAutoUploadSn { get => _IsAutoUploadSn; set { _IsAutoUploadSn = value; NotifyPropertyChanged(); } }
        private bool _IsAutoUploadSn;

        public string ResultSavePath { get => _ResultSavePath; set { _ResultSavePath = value; NotifyPropertyChanged(); } }
        private string _ResultSavePath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);


        public string ResultSavePath1 { get => _ResultSavePath1; set { _ResultSavePath1 = value; NotifyPropertyChanged(); } }
        private string _ResultSavePath1 = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        public double Height { get => _Height; set { _Height = value; NotifyPropertyChanged(); } }
        private double _Height = 300;
        public bool AutoModbusConnect { get => _AutoModbusConnect; set { _AutoModbusConnect = value; NotifyPropertyChanged(); } }
        private bool _AutoModbusConnect = true;

        public int ViewImageReadDelay { get => _ViewImageReadDelay; set { _ViewImageReadDelay = value; NotifyPropertyChanged(); } }
        private int _ViewImageReadDelay = 1000;

        public SummaryInfo SummaryInfo { get => _SummaryInfo; set { _SummaryInfo = value; NotifyPropertyChanged(); } }
        private SummaryInfo _SummaryInfo = new SummaryInfo();

        public static ARVRWindowConfig ProjectKBWindowConfig => ARVRWindowConfig.Instance;
        public Dictionary<string, SPECConfig> SPECConfigs { get; set; } = new Dictionary<string, SPECConfig>();

        public SPECConfig SPECConfig { get => _SPECConfig; set { _SPECConfig = value; NotifyPropertyChanged(); } }
        private SPECConfig _SPECConfig = new SPECConfig();

    }
}
