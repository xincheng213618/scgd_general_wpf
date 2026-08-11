using ColorVision.UI.Properties;

using System.Windows;

namespace ColorVision.UI.Menus.Base.File
{
    public class MenuExit : MenuItemFileBase
    {
        public override string GuidId => nameof(MenuExit);
        public override string Header => Resources.MenuExit;
        public override string? InputGestureText => "Alt+F4";
        public override int Order => 1000000;
        public override void Execute()
        {
            Application.Current?.Shutdown();
        }
    }

}
