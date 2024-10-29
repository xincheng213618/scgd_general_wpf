using ColorVision.UI.Menus;

namespace ColorVision.Settings
{
    public class MenuConfigExportAndImport : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override string GuidId => nameof(MenuConfigExportAndImport);
        public override int Order => 99998;
        public override string Header => "导入和导出设置";
    }
}
