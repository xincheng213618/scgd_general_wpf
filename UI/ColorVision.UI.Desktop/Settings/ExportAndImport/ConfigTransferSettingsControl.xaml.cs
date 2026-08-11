using ColorVision.Common.Utilities;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Desktop.Settings.ExportAndImport
{
    public partial class ConfigTransferSettingsControl : UserControl
    {
        public ConfigTransferSettingsControl()
        {
            InitializeComponent();
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            string defaultFileName = $"Exported-{DateTime.Now:yyyy-MM-dd}.cvsettings";

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "cvsettings files (*.cvsettings)|*.cvsettings|All files (*.*)|*.*",
                DefaultExt = ".cvsettings",
                Title = ColorVision.UI.Desktop.Properties.Resources.Config_ExportTitle,
                FileName = defaultFileName
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                ConfigHandler.GetInstance().SaveConfigs(saveFileDialog.FileName);
            }
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "cvsettings files (*.cvsettings)|*.cvsettings|All files (*.*)|*.*",
                Title = ColorVision.UI.Desktop.Properties.Resources.Config_ImportTitle
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var configHandler = ConfigHandler.GetInstance();
                ConfigReloadResult reloadResult = configHandler.ImportConfigsWithResult(openFileDialog.FileName);
                if (reloadResult.SourceReadStatus != ConfigSourceReadStatus.Succeeded
                    || reloadResult.Failures.Any(failure => failure.Kind == ConfigReloadFailureKind.SourceInstall))
                {
                    MessageBox.Show(
                        $"配置文件未导入，当前配置保持不变。\n\n{reloadResult.BuildFailureSummary()}",
                        "ColorVision",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                ConfigSettingManager.GetInstance().InvalidateCache();
                if (!reloadResult.Succeeded)
                {
                    MessageBox.Show(
                        $"配置已导入，但 {reloadResult.Failures.Count} 个运行时组件未能应用新配置。\n\n{reloadResult.BuildFailureSummary()}",
                        "ColorVision",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            PlatformHelper.OpenFolderAndSelectFile(ConfigHandler.GetInstance().ConfigFilePath);
        }
    }
}
