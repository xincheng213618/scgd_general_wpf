using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using Newtonsoft.Json;
using ProjectARVR.Config;
using ProjectARVR.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows;

namespace ProjectARVR
{
    public class ProjectARVRConfig: ViewModelBase, IConfig
    {
        public static ProjectARVRConfig Instance => ConfigService.Instance.GetRequiredService<ProjectARVRConfig>();
        public RelayCommand OpenTemplateCommand { get; set; }
        public RelayCommand OpenFlowEngineToolCommand { get; set; }
        public RelayCommand OpenLogCommand { get; set; }
        public RelayCommand OpenModbusCommand { get; set; }
        public RelayCommand OpenConfigCommand { get; set; }
        public RelayCommand OpenSocketConfigCommand { get; set; }

        public RelayCommand OpenChangeLogCommand { get; set; }
        public RelayCommand OpenReadMeCommand { get; set; }


        public ProjectARVRConfig()
        {
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate());
            OpenFlowEngineToolCommand = new RelayCommand(a => OpenFlowEngineTool());
            TemplateItemSource = TemplateFlow.Params;
            OpenLogCommand = new RelayCommand(a => OpenLog());
            OpenConfigCommand = new RelayCommand(a => OpenConfig());
            OpenChangeLogCommand = new RelayCommand(a => OpenChangeLog());
            OpenReadMeCommand = new RelayCommand(a => OpenReadMe());
            OpenSocketConfigCommand = new RelayCommand(a => OepnSocketConfig());
        }

        public static void OepnSocketConfig()
        {
            PropertyEditorWindow propertyEditorWindow = new PropertyEditorWindow(SocketConfig.Instance) { Owner = Application.Current.GetActiveWindow() };
            propertyEditorWindow.Show();
        }


        public static void OpenConfig()
        {
            EditProjectKBConfig editProjectKBConfig = new EditProjectKBConfig() { Owner = Application.Current.GetActiveWindow() };
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
                    ShowChangeLogWindow(title,content);
                }
            }
        }

        public static void OpenChangeLog()
        {
            // 资源文件的完整名称
            string resourceName = "ProjectKB.CHANGELOG.md";
            OpenResourceName("CHANGELOG", resourceName);
        }
        public static void OpenReadMe()
        {
            // 资源文件的完整名称
            string resourceName = "ProjectKB.README.md";
            OpenResourceName("README",resourceName);
        }


        private static void ShowChangeLogWindow(string title,string content)
        {
            // 创建一个新的窗口
            Window window = new Window
            {
                Title = title,
                Width = 600,
                Height = 400,
                Content = new System.Windows.Controls.TextBox
                {
                    Text = content,
                    IsReadOnly = true,
                    TextWrapping = System.Windows.TextWrapping.Wrap,
                    VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                    BorderThickness = new Thickness(0),
                    Margin = new Thickness(5)
                }
            };

            // 显示窗口
            window.ShowDialog();
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

        public long LastFlowTime { get => _LastFlowTime; set { _LastFlowTime = value; NotifyPropertyChanged(); } }
        private long _LastFlowTime;
       
        public string ResultSavePath { get => _ResultSavePath; set { _ResultSavePath = value; NotifyPropertyChanged(); } }
        private string _ResultSavePath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);



        public string ResultSavePath1 { get => _ResultSavePath1; set { _ResultSavePath1 = value; NotifyPropertyChanged(); } }
        private string _ResultSavePath1 = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        public double Height { get => _Height; set { _Height = value; NotifyPropertyChanged(); } }
        private double _Height = 300;
        public bool AutoModbusConnect { get => _AutoModbusConnect; set { _AutoModbusConnect = value; NotifyPropertyChanged(); } }
        private bool _AutoModbusConnect = true;


        public double KBLVSacle { get => _KBLVSacle; set { _KBLVSacle = value; NotifyPropertyChanged(); } }
        private double _KBLVSacle = 0.006583904;

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
