using ColorVision.UI.Menus;
using System.Reflection;

namespace ColorVision.UI.Desktop.Settings.ExportAndImport
{
    public class MenuConfigImport : GlobalMenuBase
    {
        public override string OwnerGuid => nameof(MenuConfigExportAndImport);
        public override int Order => 2;
        public override string Header => ColorVision.UI.Desktop.Properties.Resources.ImportSettings;
        public override void Execute()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "cvsettings files (*.cvsettings)|*.cvsettings|All files (*.*)|*.*",
                Title = ColorVision.UI.Desktop.Properties.Resources.Config_ImportTitle
            };

            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                string fileName = openFileDialog.FileName;
                ConfigHandler.GetInstance().LoadConfigs(fileName);
            }
        }

        private static string AssemblyCompany => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "ColorVision";
    }
}
