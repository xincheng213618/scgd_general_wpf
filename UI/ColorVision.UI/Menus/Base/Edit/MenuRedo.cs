using System.Windows;
using System.Windows.Input;

namespace ColorVision.UI.Menus.Base.Edit
{
    public class MenuRedo : MenuItemEditBase
    {
        public override string GuidId => "Redo";
        public override string Header =>ColorVision.UI.Properties.Resources.MenuRedo;
        public override int Order => 10;
        public override ICommand Command => ApplicationCommands.Redo;
        public override object? Icon => Application.Current.TryFindResource("DIRedo");
        public override string InputGestureText => "Ctrl+Y";
    }
}
