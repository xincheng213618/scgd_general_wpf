using ColorVision.Common.Utilities;
using Microsoft.Win32;
using System.IO;
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
                configHandler.BackupConfigs();
                File.Copy(openFileDialog.FileName, configHandler.ConfigFilePath, true);
                configHandler.LoadConfigs();
                ConfigSettingManager.GetInstance().InvalidateCache();
            }
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            PlatformHelper.OpenFolderAndSelectFile(ConfigHandler.GetInstance().ConfigFilePath);
        }
    }
}
