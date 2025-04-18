using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.Windows;

namespace ProjectARVR
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
            ConfigHandler.GetInstance().SaveConfigs();
            this.Close();
        }

        private void SelectDataPath_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new();
            dialog.UseDescriptionForTitle = true;
            dialog.Description = "为新项目选择位置";
            dialog.InitialDirectory = ProjectKBConfig.Instance.ResultSavePath;
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

        private void SelectDataPath1_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new();
            dialog.UseDescriptionForTitle = true;
            dialog.Description = "为新项目选择位置";
            dialog.InitialDirectory = ProjectKBConfig.Instance.ResultSavePath;
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (string.IsNullOrEmpty(dialog.SelectedPath))
                {
                    MessageBox.Show("文件夹路径不能为空", "提示");
                    return;
                }
                ProjectKBConfig.Instance.ResultSavePath1 = dialog.SelectedPath;
            }
        }

        private void Open1_Click(object sender, RoutedEventArgs e)
        {
            ColorVision.Common.Utilities.PlatformHelper.OpenFolder(ProjectKBConfig.Instance.ResultSavePath1);
        }

        private void SaveSetting_Click(object sender, RoutedEventArgs e)
        {
            string defaultFileName = $"Exported-{DateTime.Now:yyyy-MM-dd}.cvsettings";

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "cvsettings files (*.cvsettings)|*.cvsettings|All files (*.*)|*.*",
                DefaultExt = ".cvsettings",
                Title = "选择导出文件位置",
                FileName = defaultFileName // Set the default file name
            };

            // Show the dialog and get the selected file CurrentInstallFile
            bool? result = saveFileDialog.ShowDialog();

            if (result == true)
            {
                string fileName = saveFileDialog.FileName;
                ConfigHandler.GetInstance().SaveConfigs(fileName);
            }
        }

        private void LoadSetting_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "cvsettings files (*.cvsettings)|*.cvsettings|All files (*.*)|*.*",
                Title = "选择导入文件"
            };

            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                string fileName = openFileDialog.FileName;
                ConfigHandler.GetInstance().LoadConfigs(fileName);
            }
        }
    }
}
