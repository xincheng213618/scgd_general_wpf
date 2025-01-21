using ColorVision.UI.Properties;
using System.Windows.Input;

namespace ColorVision.UI.Menus.Base.Edit
{
    public class MenuUndo : MenuItemEditBase
    {
        public override string GuidId => "Undo";
        public override string Header => Resources.MenuUndo;
        public override int Order => 10;
        public override ICommand Command => ApplicationCommands.Undo;
        public override string InputGestureText => "Ctrl+Z";

    }
}
