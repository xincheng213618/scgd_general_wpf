using ColorVision.UI.Properties;

namespace ColorVision.UI.Menus.Base
{
    public class MenuFile : MenuItemMenuBase
    {
        public override string GuidId => MenuItemConstants.File;
        public override string Header => Resources.MenuFile;
        public override int Order => 1;
    }

    public class MenuExit : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.File;
        public override string GuidId => "MenuExit";
        public override string Header => Resources.MenuExit;
        public override string? InputGestureText => "Alt + F4";
        public override int Order => 1000000;

        public override void Execute()
        {
            Environment.Exit(0);
        }
    }

}
