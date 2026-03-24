using ColorVision.UI.Menus;

namespace ColorVision.UI.Desktop.Settings.ExportAndImport
{
    public class MenuConfigExportAndImport : GlobalMenuBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override int Order => 99998;
        public override string Header => ColorVision.UI.Desktop.Properties.Resources.ImportExportSettings;
    }
}
