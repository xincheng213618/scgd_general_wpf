using ColorVision.Properties;
using ColorVision.UI.Menus;

namespace ColorVision.Update
{
    public class MenuUpdate : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Help;
        public override string Header => Resources.Update;
        public override int Order => 10001;
    }
}
