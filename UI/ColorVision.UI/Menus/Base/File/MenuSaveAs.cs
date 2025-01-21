using System.Windows.Input;

namespace ColorVision.UI.Menus.Base.File
{
    public class MenuSaveAs : MenuItemFileBase
    {
        public override string GuidId => nameof(MenuSaveAs);
        public override string Header => "Save as";
        public override int Order => 30;
        public override ICommand Command => ApplicationCommands.SaveAs;
    }

}
