using ColorVision.Properties;
using ColorVision.UI.Menus;

namespace ColorVision.Menus
{
    public class MenuView : MenuItemBase
    {
        public override string OwnerGuid => "Menu";
        public override string GuidId => "View";
        public override string Header => Resources.MenuView;
        public override int Order => 4;
    }

    


}
