using ColorVision.UI.Properties;
using System.Windows.Input;

namespace ColorVision.UI.Menus.Base.File
{
    public class MenuPrint : MenuItemFileBase
    {
        public override string GuidId => "Cut";
        public override string Header => Resources.MenuPrint;
        public override int Order => 50;
        public override string InputGestureText => "Ctrl+P";
        public override ICommand Command => ApplicationCommands.Print;
    }
}
