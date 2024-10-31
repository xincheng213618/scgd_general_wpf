using ColorVision.Properties;
using ColorVision.UI.Menus;

namespace ColorVision.Update.Export
{
    public class MenuUpdate : MenuItemBase
    {
        public override string OwnerGuid => "Help";
        public override string GuidId => "Update";
        public override string Header => Resources.Update;
        public override int Order => 10001;
    }
}
