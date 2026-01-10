using ColorVision.UI.Menus;

namespace ColorVision.UI.Desktop.Settings.ExportAndImport
{
    public class MenuConfigExportAndImport : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override int Order => 99998;
        public override string Header => "导入和导出设置";
    }
}
