using ColorVision.UI.Menus;
using System.IO;
using System.Reflection;

namespace ColorVision.UI.Desktop.Settings.ExportAndImport
{
    public class MenuConfigImport : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuConfigExportAndImport);
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
        }

        private static string AssemblyCompany => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "ColorVision";
    }
}
