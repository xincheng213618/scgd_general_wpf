using ColorVision.Engine.Properties;
using ColorVision.UI.Menus;

namespace ColorVision.Engine.Templates
{
    public class MenuTemplate : MenuItemBase
    {
        public override string OwnerGuid => "Menu";
        public override string GuidId => "Template";
        public override string Header => Resources.MenuTemplate;
        public override int Order => 2;
    }
}