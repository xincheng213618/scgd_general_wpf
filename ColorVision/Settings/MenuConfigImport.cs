using ColorVision.UI;
using ColorVision.UI.Menus;
using ColorVision.Update;
using System;
using System.IO;
using System.Reflection;

namespace ColorVision.Settings
{
    public class MenuConfigImport : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuConfigExportAndImport);
        public override string GuidId => nameof(MenuConfigImport);
        public override int Order => 2;
        public override string Header => "导入设置";

        public override void Execute()
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

            AutoUpdater.RestartApplication(CurrentInstallFile);
        }
        private static string AssemblyCompany => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "ColorVision";
        private static string CurrentInstallFile => Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AssemblyCompany), $"ColorVision-{Assembly.GetExecutingAssembly().GetName().Version}.exe");
    }
}
