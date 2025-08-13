using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using Newtonsoft.Json;
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
        [JsonIgnore]
        public RelayCommand OpenChangeLogCommand { get; set; }
        [JsonIgnore]
        public RelayCommand OpenReadMeCommand { get; set; }

        public static ViewResultManager ViewResultManager => ViewResultManager.GetInstance();
        public static SummaryManager SummaryManager => SummaryManager.GetInstance();
        public static RecipeManager RecipeManager => RecipeManager.GetInstance();

        public ProjectKBConfig()
        {
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate());
            OpenFlowEngineToolCommand = new RelayCommand(a => OpenFlowEngineTool());
            TemplateItemSource = TemplateFlow.Params;
            OpenLogCommand = new RelayCommand(a => OpenLog());
            EditConfigCommand = new RelayCommand(a => EditConfig());
            OpenChangeLogCommand = new RelayCommand(a => OpenChangeLog());
            OpenReadMeCommand = new RelayCommand(a => OpenReadMe());
            OpenModbusCommand = new RelayCommand(a => OpenModbus());
            OpenSocketConfigCommand = new RelayCommand(a => OepnSocketConfig());
        }

        [DisplayName("日志开关(重启窗口后生效)")]
        public bool LogControlVisibility { get => _LogControlVisibility; set { _LogControlVisibility = value; NotifyPropertyChanged(); } }
        private bool _LogControlVisibility = true;

        [DisplayName("重试次数")]
        public int TryCountMax { get => _TryCountMax; set { _TryCountMax = value; NotifyPropertyChanged(); } }
        private int _TryCountMax = 2;

        [DisplayName("允许测试失败")]
        public bool AllowTestFailures { get => _AllowTestFailures; set { _AllowTestFailures = value; NotifyPropertyChanged(); } }
        private bool _AllowTestFailures = true;
        public void EditConfig()
        {
            new PropertyEditorWindow(this) { Owner = Application.Current.GetActiveWindow(),WindowStartupLocation =WindowStartupLocation.CenterOwner }.ShowDialog();
        }
        public static void OepnSocketConfig()
        {
            PropertyEditorWindow propertyEditorWindow = new PropertyEditorWindow(SocketConfig.Instance) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            propertyEditorWindow.Show();
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
            string resourceName = "ProjectKB.CHANGELOG.md";
            OpenResourceName("CHANGELOG", resourceName);
        }
        public static void OpenReadMe()
        {
            // 资源文件的完整名称
            string resourceName = "ProjectKB.README.md";
            OpenResourceName("README",resourceName);
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
        public string SN { get => _SN; set { _SN = value; NotifyPropertyChanged(); } }
        private string _SN = string.Empty;

        public static ModbusControl ModbusControl => ModbusControl.GetInstance();

        [DisplayName("自动连接Modbus"), Category("KB")]
        public bool AutoModbusConnect { get => _AutoModbusConnect; set { _AutoModbusConnect = value; NotifyPropertyChanged(); } }
        private bool _AutoModbusConnect = true;
        [DisplayName("KBLVSacle"), Category("KB")]
        public double KBLVSacle { get => _KBLVSacle; set { _KBLVSacle = value; NotifyPropertyChanged(); } }
        private double _KBLVSacle = 0.006583904;





    }
}
