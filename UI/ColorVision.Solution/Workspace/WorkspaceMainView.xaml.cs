using ColorVision.Themes;
using ColorVision.UI.LogImp;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Solution.Workspace
{
    /// <summary>
    /// WorkspaceMainView.xaml 的交互逻辑
    /// </summary>
    public partial class WorkspaceMainView : UserControl
    {
        private LogOutput? _logOutput;

        public WorkspaceMainView()
        {
            InitializeComponent();
            WorkspaceManager.SolutionView = this;
            WorkspaceManager.layoutRoot = _layoutRoot;
            WorkspaceManager.LayoutDocumentPane = LayoutDocumentPane;
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (sender, e) => Colsed()));
            InputBindings.Add(new KeyBinding(ApplicationCommands.Close, new KeyGesture(Key.W, ModifierKeys.Control)));

            foreach (var action in WorkspaceManager.DealyLoad)
            {
                action();
            }
            WorkspaceManager.DealyLoad.Clear();
        }

        public void Colsed()
        {
            var pannel = WorkspaceManager.FindDocumentActive(LayoutDocumentPane);
            pannel.Close();
        }

        private void UserControl_Initialized(object sender, System.EventArgs e)
        {
            void ThemeChange(Theme theme)
            {
                if (theme == Theme.Dark)
                {
                    DockingManager1.Theme = new AvalonDock.Themes.Vs2013DarkTheme();
                }
                else
                {
                    DockingManager1.Theme = new AvalonDock.Themes.Vs2013LightTheme();
                }
            };

            ThemeManager.Current.CurrentUIThemeChanged += ThemeChange;
            ThemeChange(ThemeManager.Current.CurrentUITheme);

            // 初始化日志面板
            _logOutput = new LogOutput("%date{HH:mm:ss} [%thread] %-5level %message%newline");
            LogPanelGrid.Children.Add(_logOutput);

            // 初始化停靠布局管理器
            var layoutManager = new DockLayoutManager(DockingManager1);
            layoutManager.RegisterPanel("LogPanel", LogPanelGrid, "日志", PanelPosition.Bottom);
            WorkspaceManager.LayoutManager = layoutManager;

            // 尝试加载已保存的布局
            layoutManager.LoadLayout();
        }  
    }
}
