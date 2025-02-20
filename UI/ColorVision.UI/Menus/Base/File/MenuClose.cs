using System.Windows.Input;

namespace ColorVision.UI.Menus.Base.File
{
    public class MenuClose : MenuItemFileBase
    {
        public override string GuidId => nameof(MenuClose);
        public override string Header => ColorVision.UI.Properties.Resources.MenuClose;
        public override int Order => 20;
        public override ICommand Command => ApplicationCommands.Close;
    }
}
