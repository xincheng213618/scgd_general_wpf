using ColorVision.UI.Properties;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.UI.Menus.Base.Edit
{
    public class MenuPaste : MenuItemEditBase
    {
        public override string GuidId => "Paste";
        public override string Header => Resources.MenuPaste;
        public override int Order => 22;
        public override ICommand Command => ApplicationCommands.Paste;
        public override object? Icon => MenuItemIcon.TryFindResource("DIPaste");
        public override string InputGestureText => "Ctrl+V";

    }
}
