using ColorVision.UI.Properties;

namespace ColorVision.UI.Menus.Base
{
    public class MenuView : GlobalMenuBase
    {
        public override string OwnerGuid => MenuItemConstants.Menu;
        public override string GuidId => "View";
        public override string Header => Resources.MenuView;
        public override int Order => 4;
    }
}
