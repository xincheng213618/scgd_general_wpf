using ColorVision.UI.Properties;

namespace ColorVision.UI.Menus.Base
{
    public class MenuHelp : GlobalMenuBase
    {
        public override string GuidId => MenuItemConstants.Help;
        public override string Header => Resources.MenuHelp;
        public override int Order => 5;
    }




}
