using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.Flow;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows;

namespace ProjectKB
{
    public class ProjectKBConfig: ViewModelBase, IConfig
    {
        public static ProjectKBConfig Instance => ConfigService.Instance.GetRequiredService<ProjectKBConfig>();
        public RelayCommand OpenTemplateCommand { get; set; }
        public RelayCommand OpenFlowEngineToolCommand { get; set; }
        public RelayCommand OpenLogCommand { get; set; }
        public RelayCommand OpenModbusCommand { get; set; }
        public RelayCommand OpenChangeLogCommand { get; set; }
        public RelayCommand OpenConfigCommand { get; set; }


        public ProjectKBConfig()
        {
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate());
            OpenFlowEngineToolCommand = new RelayCommand(a => OpenFlowEngineTool());
            TemplateItemSource = FlowParam.Params;
            OpenLogCommand = new RelayCommand(a => OpenLog());
            OpenModbusCommand = new RelayCommand(a => OpenModbus());
            OpenChangeLogCommand = new RelayCommand(a => OpenChangeLog());
            OpenConfigCommand = new RelayCommand(a => OpenConfig());
        }

        public static void OpenConfig()
        {
            EditProjectKBConfig editProjectKBConfig = new EditProjectKBConfig() { Owner = Application.Current.GetActiveWindow() };
            editProjectKBConfig.ShowDialog();
        }

        public static void OpenChangeLog()
        {
            // 获取当前执行的程序集
            Assembly assembly = Assembly.GetExecutingAssembly();

            // 资源文件的完整名称
            string resourceName = "ProjectKB.CHANGELOG.md";

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
                    MessageBox.Show(content);
                }
            }
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
            new FlowEngineToolWindow(FlowParam.Params[TemplateSelectedIndex].Value) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
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
    }
}
