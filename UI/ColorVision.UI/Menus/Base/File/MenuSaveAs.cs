using System.Windows.Input;

using ColorVision.UI.Properties;

namespace ColorVision.UI.Menus.Base.File
{
    public class MenuSaveAs : MenuItemFileBase
    {
        public override string GuidId => nameof(MenuSaveAs);
        public override string Header => Resources.MenuSaveAs;
        public override int Order => 30;
        public override ICommand Command => ApplicationCommands.SaveAs;
    }

}
