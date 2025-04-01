using System.Collections.Generic;
using System.Windows;

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
            ProjectManager.GetInstance().Config.SetWindow(this);
            this.SizeChanged += (s, e) => ProjectManager.GetInstance().Config.SetConfig(this);
        }

        private void Window_Initialized(object sender, System.EventArgs e)
        {
            this.DataContext = ProjectManager.GetInstance();
            DefalutSearchComboBox.ItemsSource = new List<string>() { "ProjectKB", "ProjectBlackMura", "ProjectHeyuan", "ProjectShiyuan", "ProjectBase" };
            ListViewProjects.SelectedIndex = 0;
        }


        private void ListViewProjects_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }
    }
}
