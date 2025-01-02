using ColorVision.UI.Properties;

namespace ColorVision.UI.Menus
{
    public class MenuExit : MenuItemBase
    {
        public override string OwnerGuid => "File";
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
