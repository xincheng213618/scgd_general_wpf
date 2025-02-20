using ColorVision.UI.Properties;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.UI.Menus.Base.Edit
{
    public class MenuDelete : MenuItemEditBase
    {
        public override string GuidId => "Delete";
        public override string Header => Resources.MenuDelete;
        public override int Order => 23;
        public override ICommand Command => ApplicationCommands.Delete;
        public override object? Icon => MenuItemIcon.TryFindResource("DIDelete");
        public override string InputGestureText => "Del";

    }
}
