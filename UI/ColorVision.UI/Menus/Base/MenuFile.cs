using ColorVision.UI.Properties;
using System.Windows.Input;

namespace ColorVision.UI.Menus.Base
{
    public class MenuFile : MenuItemMenuBase
    {
        public override string GuidId => MenuItemConstants.File;
        public override string Header => Resources.MenuFile;
        public override int Order => 1;
    }

    public class MenuSave : MenuItemEditBase
    {
        public override string OwnerGuid => MenuItemConstants.File;

        public override string GuidId => "Save";
        public override string Header => "Save";
        public override int Order => 20;
        public override ICommand Command => ApplicationCommands.Save;
    }

    public class MenuSaveAs : MenuItemEditBase
    {
        public override string OwnerGuid => MenuItemConstants.File;

        public override string GuidId => "Save";
        public override string Header => "Save as";
        public override int Order => 20;
        public override ICommand Command => ApplicationCommands.SaveAs;
    }

    public class MenuPrint : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.File;
        public override string GuidId => "Cut";
        public override string Header => Resources.MenuPrint;
        public override int Order => 30;
        public override ICommand Command => ApplicationCommands.Print;
    }

    public class MenuExit : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.File;
        public override string GuidId => "MenuExit";
        public override string Header => Resources.MenuExit;
        public override string? InputGestureText => "Alt + F4";
        public override int Order => 1000000;

        public override void Execute()
        {
            Environment.Exit(0);
        }
    }

}
