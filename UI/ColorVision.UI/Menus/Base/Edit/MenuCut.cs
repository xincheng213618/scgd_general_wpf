using ColorVision.UI.Properties;
using System.Windows.Input;

namespace ColorVision.UI.Menus.Base.Edit
{
    public class MenuCut : MenuItemEditBase
    {
        public override string GuidId => "Cut";
        public override string Header => Resources.MenuCut;
        public override int Order => 20;
        public override ICommand Command => ApplicationCommands.Cut;
    }

}
