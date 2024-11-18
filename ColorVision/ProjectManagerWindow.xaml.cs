using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using log4net;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace ColorVision
{
    public class ExportProjectManager : MenuItemBase
    {
        public override string OwnerGuid => "Help";
        public override string GuidId => nameof(ExportProjectManager);
        public override int Order => 10000;
        public override string Header => "项目管理";

        public override Visibility Visibility => ProjectManager.GetInstance().Projects.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            new ProjectManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }

    public class ProjectInfo:ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ProjectInfo));

        public IProject Project { get; set; }
        public Version? AssemblyVersion { get; set; }
        public DateTime? AssemblyBuildDate { get; set; }
        public string? AssemblyName { get; set; }
        public string? AssemblyPath { get; set; }
        public string? AssemblyCulture { get; set; }
        public string? AssemblyPublicKeyToken { get; set; }
        public string InkPath { get => _InkPath; set { _InkPath = value; NotifyPropertyChanged(); } }
        private string _InkPath;

        public RelayCommand OpenProjectCommand { get; set; }
        public RelayCommand CreateShortCutCommand { get; set; }
        public RelayCommand OpenInCmdCommand { get; set; }

        public ProjectInfo(IProject project, Assembly assembly)
        {
            Project = project;
            try
            {
                AssemblyName = assembly.GetName().Name;
                AssemblyVersion = assembly.GetName().Version;
                AssemblyBuildDate = File.GetLastWriteTime(assembly.Location);
                AssemblyPath = assembly.Location;
                AssemblyCulture = assembly.GetName().CultureInfo?.Name ?? "neutral";
                AssemblyPublicKeyToken = BitConverter.ToString(assembly.GetName().GetPublicKeyToken() ?? new byte[0]);
            }
            catch (Exception ex)
            {
                // 记录错误日志
                LogManager.GetLogger(typeof(ProjectInfo)).Error("Error retrieving assembly info", ex);
            }
            OpenProjectCommand = new RelayCommand(a => OpenProject());
            InkPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\" + Project.Header + ".lnk";
            CreateShortCutCommand = new RelayCommand(a => CreateShortCut());
            OpenInCmdCommand = new RelayCommand(a => OpenInCmd());
        }

        public void OpenInCmd()
        {
            string executablePath = Environments.GetExecutablePath();
            string projectName = Project.Header;

            if (!string.IsNullOrEmpty(executablePath) && !string.IsNullOrEmpty(projectName))
            {
                string arguments = $"-project {projectName}";
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{executablePath} {arguments}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(startInfo);
            }

        }



        public void CreateShortCut()
        {
            string GetExecutablePath = Environments.GetExecutablePath();
            string shortcutName = Project.Header;
            string shortcutPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            

            string arguments = $"-project {shortcutName}";
            if (shortcutName != null)
                Common.NativeMethods.ShortcutCreator.CreateShortcut(shortcutName, shortcutPath, GetExecutablePath, arguments);
        }

        public void OpenProject()
        {
            try
            {
                Project.Execute();
                log.Info($"OpenProject {Project.Header}");
            }
            catch (Exception ex)
            {
                log.Error(ex);
                MessageBox.Show(ex.Message);
            }

        }
    }


    public class ProjectManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MainWindow));
        private static ProjectManager _instance;
        private static readonly object _locker = new();
        public static ProjectManager GetInstance() { lock (_locker) { _instance ??= new ProjectManager(); return _instance; } }
        public ObservableCollection<ProjectInfo> Projects { get; private set; } = new ObservableCollection<ProjectInfo>();

        public RelayCommand CreateShortCutCommand { get;  set; }

        public ProjectManager()
        {
            log.Info("正在检索是否存在附加项目");
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(IProject).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is IProject project)
                    {
                        ProjectInfo info = new ProjectInfo(project, assembly);
                        info.AssemblyVersion = assembly.GetName().Version;
                        info.AssemblyBuildDate = File.GetLastWriteTime(assembly.Location);

                        Projects.Add(info);
                        log.Info($"找到外加项目：{project} 名称：{info.AssemblyName} 版本：{info.AssemblyVersion} " +
                                 $"日期：{info.AssemblyBuildDate} 路径：{info.AssemblyPath} 文化：{info.AssemblyCulture} " +
                                 $"公钥标记：{info.AssemblyPublicKeyToken}");
                    }
                }
            }
            CreateShortCutCommand = new RelayCommand(a => CreateShortCut());
        }

        public void CreateShortCut()
        {

            foreach (var item in Projects)
            {
                string GetExecutablePath = Environments.GetExecutablePath();
                string shortcutName = item.Project.Header;
                string shortcutPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string arguments = $"-project {shortcutName}";
                if(shortcutName!=null)
                    Common.NativeMethods.ShortcutCreator.CreateShortcut(shortcutName, shortcutPath, GetExecutablePath, arguments);
            }

        }
    }


    /// <summary>
    /// ProjectManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ProjectManagerWindow : Window
    {
        public ProjectManagerWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, System.EventArgs e)
        {
            this.DataContext = new ProjectManager();
        }
    }
}
