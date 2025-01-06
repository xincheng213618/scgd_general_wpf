using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates;
using ColorVision.UI;
using System.Windows;
using ColorVision.Engine.Templates.Flow;
using System.IO;
using System.Reflection;

namespace ColorVision.Projects.ProjectShiYuan
{
    public class ProjectShiYuanConfig: ViewModelBase, IConfig
    {
        public static ProjectShiYuanConfig Instance => ConfigService.Instance.GetRequiredService<ProjectShiYuanConfig>();
        public RelayCommand OpenTemplateCommand { get; set; }
        public RelayCommand OpenFlowEngineToolCommand { get; set; }
        public RelayCommand OpenLogCommand { get; set; }

        public RelayCommand OpenChangeLogCommand { get; set; }
        public RelayCommand OpenReadMeCommand { get; set; }

        public ProjectShiYuanConfig()
        {
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate());
            OpenFlowEngineToolCommand = new RelayCommand(a => OpenFlowEngineTool());
            OpenLogCommand = new RelayCommand(a => OpenLog());
            OpenChangeLogCommand = new RelayCommand(a => OpenChangeLog());
            OpenReadMeCommand = new RelayCommand(a => OpenReadMe());
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
                    ShowChangeLogWindow(title, content);
                }
            }
        }

        public static void OpenChangeLog()
        {
            // 资源文件的完整名称
            string resourceName = "ProjectShiyuan.CHANGELOG.md";
            OpenResourceName("CHANGELOG", resourceName);
        }
        public static void OpenReadMe()
        {
            // 资源文件的完整名称
            string resourceName = "ProjectShiyuan.CHANGELOG.md";
            OpenResourceName("README", resourceName);
        }


        private static void ShowChangeLogWindow(string title, string content)
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

        public string SN { get => _SN; set { _SN = value; NotifyPropertyChanged(); } }
        private string _SN;


        public bool IsOpenConnect { get => _IsOpenConnect;set { _IsOpenConnect = value; NotifyPropertyChanged(); } }
        private bool _IsOpenConnect;

        public string FlowName { get => _FlowName; set { _FlowName = value; NotifyPropertyChanged(); } }
        private string _FlowName;

        public int DeviceId { get => _DeviceId; set { _DeviceId = value; NotifyPropertyChanged(); } }
        private int _DeviceId;

        public string PortName { get => _PortName; set { _PortName = value; NotifyPropertyChanged(); } }
        private string _PortName;

        public string TestName { get => _TestName; set { _TestName = value; NotifyPropertyChanged(); } }
        private string _TestName = "WBROtest";

        public string DataPath { get => _DataPath; set { _DataPath = value; NotifyPropertyChanged(); } }
        private string _DataPath;

        public bool IsAutoUploadSn { get => _IsAutoUploadSn; set { _IsAutoUploadSn = value; NotifyPropertyChanged(); } }
        private bool _IsAutoUploadSn;

        public long LastFlowTime { get => _LastFlowTime; set { _LastFlowTime = value; NotifyPropertyChanged(); } }
        private long _LastFlowTime;



    }
}
