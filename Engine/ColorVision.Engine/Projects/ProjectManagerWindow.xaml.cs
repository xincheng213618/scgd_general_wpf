using ColorVision.Engine.Properties;
using ColorVision.Themes;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Projects
{
    public class MenuProjectManager : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override int Order => 9000;
        public override string Header => Resources.ProjectManagerWindow;

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            new ProjectManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
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
            this.ApplyCaption();
            ProjectManager.GetInstance().Config.SetWindow(this);
        }

        private void Window_Initialized(object sender, System.EventArgs e)
        {
            this.DataContext = ProjectManager.GetInstance();
            ListViewProjects.SelectedIndex = 0;
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => ProjectManager.GetInstance().Projects[ListViewProjects.SelectedIndex].Delete(), (s, e) => e.CanExecute = ListViewProjects.SelectedIndex > -1));
        }


        private void ListViewProjects_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }
    }
}
