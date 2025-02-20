using System.Windows.Input;

namespace ColorVision.UI.Menus.Base.Edit
{
    public class MenuSelectAll : MenuItemEditBase
    {
        public override string GuidId => "SelectAll";
        public override string Header => "SelectAll";
        public override int Order => 50;
        public override ICommand Command => ApplicationCommands.SelectAll;
        public override string InputGestureText => "Ctrl+A";
    }
}
