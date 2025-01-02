using ColorVision.Properties;
using ColorVision.UI.Menus;

namespace ColorVision.Menus
{
    public class MenuHelp : MenuItemBase
    {
        public override string OwnerGuid => "Menu";
        public override string GuidId => "Help";
        public override string Header => Resources.MenuHelp;
        public override int Order => 5;
    }

    


}
