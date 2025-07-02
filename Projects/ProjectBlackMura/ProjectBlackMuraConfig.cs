﻿using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;

namespace ProjectBlackMura
{
    public class JudgeConfig:ViewModelBase
    {


    }

    public class ProjectBlackMuraConfig: ViewModelBase, IConfig
    {
        public static ProjectBlackMuraConfig Instance => ConfigService.Instance.GetRequiredService<ProjectBlackMuraConfig>();

        public ObservableCollection<BlackMuraResult> ViewResluts { get; set; } = new ObservableCollection<BlackMuraResult>();
        public Dictionary<string, JudgeConfig> JudgeConfigs { get; set; } = new Dictionary<string, JudgeConfig>();
        public JudgeConfig JudgeConfig { get => _JudgeConfig; set { _JudgeConfig = value; NotifyPropertyChanged(); } }
        private JudgeConfig _JudgeConfig = new JudgeConfig();

        public RelayCommand OpenTemplateCommand { get; set; }
        public RelayCommand OpenFlowEngineToolCommand { get; set; }
        public RelayCommand OpenLogCommand { get; set; }
        public RelayCommand OpenConfigCommand { get; set; }

        public RelayCommand OpenChangeLogCommand { get; set; }
        public RelayCommand OpenReadMeCommand { get; set; }
        public RelayCommand OpenHYMesConfigCommand { get; set; }

        public ProjectBlackMuraConfig()
        {
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate());
            OpenFlowEngineToolCommand = new RelayCommand(a => OpenFlowEngineTool());
            TemplateItemSource = TemplateFlow.Params;
            OpenLogCommand = new RelayCommand(a => OpenLog());
            OpenConfigCommand = new RelayCommand(a => OpenConfig());
            OpenChangeLogCommand = new RelayCommand(a => OpenChangeLog());
            OpenReadMeCommand = new RelayCommand(a => OpenReadMe());
            OpenHYMesConfigCommand = new RelayCommand(a => OpenHYMesConfig());

        }
        public int StepIndex { get => _StepIndex; set { _StepIndex = value; NotifyPropertyChanged(); } }
        private int _StepIndex;

        public bool LogControlVisibility { get => _LogControlVisibility; set { _LogControlVisibility = value; NotifyPropertyChanged(); } }
        private bool _LogControlVisibility = true;

        public void OpenHYMesConfig()
        {
            PropertyEditorWindow propertyEditorWindow = new PropertyEditorWindow(HYMesConfig.Instance, false) { Owner = Application.Current.GetActiveWindow() };
            propertyEditorWindow.ShowDialog();
        }


        public void OpenConfig()
        {
            new EditBlackMuraConfig() { Owner = Application.Current.GetActiveWindow() }.ShowDialog();
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
            string resourceName = "ProjectBlackMura.CHANGELOG.md";
            OpenResourceName("CHANGELOG", resourceName);
        }
        public static void OpenReadMe()
        {
            // 资源文件的完整名称
            string resourceName = "ProjectBlackMura.README.md";
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

       
        public string ResultSavePath { get => _ResultSavePath; set { _ResultSavePath = value; NotifyPropertyChanged(); } }
        private string _ResultSavePath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);


        

        public string ResultSavePath1 { get => _ResultSavePath1; set { _ResultSavePath1 = value; NotifyPropertyChanged(); } }
        private string _ResultSavePath1 = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        public double Height { get => _Height; set { _Height = value; NotifyPropertyChanged(); } }
        private double _Height = 300;

        [DisplayName("重试次数")]
        public int TryCountMax { get => _TryCountMax; set { _TryCountMax = value; NotifyPropertyChanged(); } }
        private int _TryCountMax = 2;

        public int ViewImageReadDelay { get => _ViewImageReadDelay; set { _ViewImageReadDelay = value; NotifyPropertyChanged(); } }
        private int _ViewImageReadDelay = 1000;

    }
}
