using ColorVision.Themes;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Projects
{
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
            DefalutSearchComboBox.ItemsSource = new List<string>() { "ProjectKB", "ProjectBlackMura", "ProjectHeyuan", "ProjectShiyuan", "ProjectBase", "ProjectARVR" };
            ListViewProjects.SelectedIndex = 0;
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => ProjectManager.GetInstance().Projects[ListViewProjects.SelectedIndex].Delete(), (s, e) => e.CanExecute = ListViewProjects.SelectedIndex > -1));
        }


        private void ListViewProjects_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }
    }
}
