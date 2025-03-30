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
        }

        private void Window_Initialized(object sender, System.EventArgs e)
        {
            this.DataContext = ProjectManager.GetInstance();
            DefalutSearchComboBox.ItemsSource = new List<string>() { "ProjectKB", "ProjectBlackMura", "ProjectHeyuan", "ProjectShiyuan", "ProjectBase" };
        }


        private void ListViewProjects_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ListViewProjects.SelectedIndex > -1)
            {
                BorderContent.DataContext = ProjectManager.GetInstance().Projects[ListViewProjects.SelectedIndex];
            }
        }
    }
}
