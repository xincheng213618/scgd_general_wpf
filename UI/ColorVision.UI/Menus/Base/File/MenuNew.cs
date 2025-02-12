using ColorVision.UI.Properties;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.UI.Menus.Base.File
{
    public class MenuNewItem : MenuItemFileBase
    {
        public override string GuidId => nameof(MenuNewItem);

        public override int Order => 0;

        public override string Header => Resources.MenuNew;

        public override string InputGestureText => "Ctrl+N";

        public override ICommand Command => ApplicationCommands.New;


    }

}
