using ColorVision.UI.Properties;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Menus.Base.Edit
{
    public class MenuCut : MenuItemEditBase
    {
        public override string GuidId => "Cut";
        public override string Header => Resources.MenuCut;
        public override int Order => 20;
        public override ICommand Command => ApplicationCommands.Cut;
        public override object? Icon => Application.Current.TryFindResource("DICut");
        public override string InputGestureText => "Ctrl+X";

    }

}
