using System.Windows.Input;

namespace ColorVision.UI.Menus.Base.File
{
    public class MenuSave : MenuItemFileBase
    {

        public override string GuidId => nameof(MenuSave);
        public override string Header => "Save";
        public override int Order => 30;
        public override string InputGestureText => "Ctrl+S";
        public override ICommand Command => ApplicationCommands.Save;
    }

}
