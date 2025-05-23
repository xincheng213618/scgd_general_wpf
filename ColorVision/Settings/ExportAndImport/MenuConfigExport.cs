using System;
using System.Windows;
using ColorVision.UI;
using ColorVision.UI.Menus;

namespace ColorVision.Settings.ExportAndImport
{
    //public class MenuConfigExport : MenuItemBase
    //{
    //    public override string OwnerGuid => nameof(MenuConfigExportAndImport);
    //    public override int Order => 1;
    //    public override string Header => "导出设置";
    //    public override void Execute()
    //    {
    //        string defaultFileName = $"Exported-{DateTime.Now:yyyy-MM-dd}.cvsettings";

    //        var saveFileDialog = new Microsoft.Win32.SaveFileDialog
    //        {
    //            Filter = "cvsettings files (*.cvsettings)|*.cvsettings|All files (*.*)|*.*",
    //            DefaultExt = ".cvsettings",
    //            Title = "选择导出文件位置",
    //            FileName = defaultFileName // Set the default file name
    //        };

    //        // Show the dialog and get the selected file CurrentInstallFile
    //        bool? result = saveFileDialog.ShowDialog();

    //        if (result == true)
    //        {
    //            string fileName = saveFileDialog.FileName;
    //            ConfigHandler.GetInstance().SaveConfigs(fileName);
    //        }
    //    }
    //}
}
