using ColorVision.UI.Properties;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.UI.Menus.Base.Edit
{
    public class MenuRename : MenuItemEditBase
    {
        public override string GuidId => "Rename";
        public override string Header => Resources.MenuRename;
        public override int Order => 99;
        public override ICommand Command => Commands.ReName;
        public override object? Icon => MenuItemIcon.TryFindResource("DIRename");

        public override string InputGestureText => "F2";
    }
}
