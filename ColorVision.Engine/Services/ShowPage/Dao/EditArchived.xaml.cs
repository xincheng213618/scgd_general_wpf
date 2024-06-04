using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using System;
using System.Windows;
using System.Windows.Input;


namespace ColorVision.Services.ShowPage.Dao
{
    /// <summary>
    /// EditCalibration.xaml 的交互逻辑
    /// </summary>
    public partial class EditArchived : Window
    {
        ConfigArchivedModel configArchivedModel;
        public ConfigArchivedModel EditConfig { get; set; }
        public EditArchived(ConfigArchivedModel configArchivedModel)
        {
            this.configArchivedModel = configArchivedModel;
            InitializeComponent();
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Common.NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (configArchivedModel != null)
            {
                EditConfig = configArchivedModel.Clone();
                EditContent.DataContext = EditConfig;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            EditConfig.CopyTo(configArchivedModel);
            ConfigArchivedDao.Instance.Save(configArchivedModel);
            MessageBox.Show(Application.Current.GetActiveWindow(), "保存成功", "ColorVision");
            Close();
        }

        private void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new();
            dialog.UseDescriptionForTitle = true;
            dialog.Description = "为新项目选择位置";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (string.IsNullOrEmpty(dialog.SelectedPath))
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(),"文件夹路径不能为空", "提示");
                    return;
                }
                EditConfig.Path = dialog.SelectedPath;
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            PlatformHelper.Open("https://cron.qqe2.com/");
        }
    }
}
