using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using log4net;
using System;
using System.Collections.ObjectModel;
using System.Linq;
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


    public class ProjectManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MainWindow));
        private static ProjectManager _instance;
        private static readonly object _locker = new();
        public static ProjectManager GetInstance() { lock (_locker) { _instance ??= new ProjectManager(); return _instance; } }
        public ObservableCollection<IProject> Projects { get; private set; } = new ObservableCollection<IProject>();

        public RelayCommand CreateShortCutCommand { get;  set; }

        public ProjectManager()
        {
            log.Info("正在检索是否存在附加项目");
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(IProject).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is IProject projects)
                    {
                        Projects.Add(projects);
                        log.Info("找到外加项目：" + projects);
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
                string shortcutName = item.Header;
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
