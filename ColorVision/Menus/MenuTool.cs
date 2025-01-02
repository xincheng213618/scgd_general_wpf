using ColorVision.Properties;
using ColorVision.UI.Menus;

namespace ColorVision.Menus
{
    public class MenuTool : MenuItemBase
    {
        public override string OwnerGuid => "Menu";
        public override string GuidId => "Tool";
        public override string Header => Resources.MenuTool;
        public override int Order => 3;
    }

    


}
