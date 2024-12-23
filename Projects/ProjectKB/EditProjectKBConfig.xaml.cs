using ColorVision.Common.MVVM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ProjectKB
{
    /// <summary>
    /// EditProjectKBConfig.xaml 的交互逻辑
    /// </summary>
    public partial class EditProjectKBConfig : Window
    {
        public EditProjectKBConfig()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = ProjectKBConfig.Instance;
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            ProjectKBConfig.Instance.Reset();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void SelectDataPath_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new();
            dialog.UseDescriptionForTitle = true;
            dialog.Description = "为新项目选择位置";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (string.IsNullOrEmpty(dialog.SelectedPath))
                {
                    MessageBox.Show("文件夹路径不能为空", "提示");
                    return;
                }
                ProjectKBConfig.Instance.ResultSavePath = dialog.SelectedPath;
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            ColorVision.Common.Utilities.PlatformHelper.OpenFolder(ProjectKBConfig.Instance.ResultSavePath);
        }

    }
}
