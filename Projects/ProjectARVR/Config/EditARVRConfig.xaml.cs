using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.Windows;

namespace ProjectARVR
{
    /// <summary>
    /// EditARVRConfig.xaml 的交互逻辑
    /// </summary>
    public partial class EditARVRConfig : Window
    {
        public EditARVRConfig()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = ProjectARVRConfig.Instance;
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            ProjectARVRConfig.Instance.Reset();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            ConfigHandler.GetInstance().SaveConfigs();
            this.Close();
        }

        private void SelectDataPath_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new();
            dialog.UseDescriptionForTitle = true;
            dialog.Description = "为新项目选择位置";
            dialog.InitialDirectory = ProjectARVRConfig.Instance.ResultSavePath;
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (string.IsNullOrEmpty(dialog.SelectedPath))
                {
                    MessageBox.Show("文件夹路径不能为空", "提示");
                    return;
                }
                ProjectARVRConfig.Instance.ResultSavePath = dialog.SelectedPath;
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            ColorVision.Common.Utilities.PlatformHelper.OpenFolder(ProjectARVRConfig.Instance.ResultSavePath);
        }

        private void SelectDataPath1_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new();
            dialog.UseDescriptionForTitle = true;
            dialog.Description = "为新项目选择位置";
            dialog.InitialDirectory = ProjectARVRConfig.Instance.ResultSavePath;
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (string.IsNullOrEmpty(dialog.SelectedPath))
                {
                    MessageBox.Show("文件夹路径不能为空", "提示");
                    return;
                }
                ProjectARVRConfig.Instance.ResultSavePath1 = dialog.SelectedPath;
            }
        }

        private void Open1_Click(object sender, RoutedEventArgs e)
        {
            ColorVision.Common.Utilities.PlatformHelper.OpenFolder(ProjectARVRConfig.Instance.ResultSavePath1);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ConfigService.Instance.SaveConfigs();
            this.Close();
        }
    }
}
