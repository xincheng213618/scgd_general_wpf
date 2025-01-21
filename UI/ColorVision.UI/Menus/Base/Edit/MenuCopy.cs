using ColorVision.UI.Properties;
using System.Windows.Input;

namespace ColorVision.UI.Menus.Base.Edit
{
    public class MenuCopy : MenuItemEditBase
    {
        public override string GuidId => "Copy";
        public override string Header => Resources.MenuCopy;
        public override int Order => 20;

        public override ICommand Command => ApplicationCommands.Copy;
        public override string InputGestureText => "Ctrl+C";
    }
}
