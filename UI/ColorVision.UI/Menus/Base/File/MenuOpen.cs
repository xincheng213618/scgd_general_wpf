using ColorVision.UI.Properties;

namespace ColorVision.UI.Menus.Base.File
{
    public class MenuOpen : MenuItemFileBase
    {
        public override string GuidId => nameof(MenuOpen);

        public override int Order => 0;

        public override string Header => Resources.MenuOpen;

    }

}
