using AvalonDock.Layout;
using ColorVision.Solution.Editor;
using ColorVision.Themes;
using ColorVision.UI.Views;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Solution.Workspace
{
    /// <summary>
    /// WorkspaceMainView.xaml 的交互逻辑
    /// </summary>
    public partial class WorkspaceMainView : UserControl
    {
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


        }  
    }
}
