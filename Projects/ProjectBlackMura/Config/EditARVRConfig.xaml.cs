using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.Windows;

namespace ProjectBlackMura
{
    /// <summary>
    /// EditBlackMuraConfig.xaml 的交互逻辑
    /// </summary>
    public partial class EditBlackMuraConfig : Window
    {
        public EditBlackMuraConfig()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = ProjectBlackMuraConfig.Instance;
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            ProjectBlackMuraConfig.Instance.Reset();
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
            dialog.InitialDirectory = ProjectBlackMuraConfig.Instance.ResultSavePath;
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (string.IsNullOrEmpty(dialog.SelectedPath))
                {
                    MessageBox.Show("文件夹路径不能为空", "提示");
                    return;
                }
                ProjectBlackMuraConfig.Instance.ResultSavePath = dialog.SelectedPath;
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            ColorVision.Common.Utilities.PlatformHelper.OpenFolder(ProjectBlackMuraConfig.Instance.ResultSavePath);
        }

        private void SelectDataPath1_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new();
            dialog.UseDescriptionForTitle = true;
            dialog.Description = "为新项目选择位置";
            dialog.InitialDirectory = ProjectBlackMuraConfig.Instance.ResultSavePath;
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (string.IsNullOrEmpty(dialog.SelectedPath))
                {
                    MessageBox.Show("文件夹路径不能为空", "提示");
                    return;
                }
                ProjectBlackMuraConfig.Instance.ResultSavePath1 = dialog.SelectedPath;
            }
        }

        private void Open1_Click(object sender, RoutedEventArgs e)
        {
            ColorVision.Common.Utilities.PlatformHelper.OpenFolder(ProjectBlackMuraConfig.Instance.ResultSavePath1);
        }
    }
}
