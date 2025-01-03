using ColorVision.UI.Properties;

namespace ColorVision.UI.Menus.Base
{
    public class MenuHelp : MenuItemMenuBase
    {
        public override string GuidId => MenuItemConstants.Help;
        public override string Header => Resources.MenuHelp;
        public override int Order => 5;
    }




}
