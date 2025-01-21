using ColorVision.UI.Properties;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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
        public override string InputGestureText => "Ctrl+S";
        public override ICommand Command => ApplicationCommands.Save;
    }

    public class MenuOpen : MenuItemBase
    {

        public override string OwnerGuid => MenuItemConstants.File;

        public override string GuidId => "Open";

        public override int Order => 0;

        public override string Header => "新建(_N)";

        public override string InputGestureText => "Ctrl+N";

        public override object? Icon
        {
            get
            {
                TextBlock text = new()
                {
                    Text = "\uE8F4", // 使用Unicode字符
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 15,
                };
                text.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
                return text;
            }
        }
        public override ICommand Command => ApplicationCommands.Open;
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
        public override string InputGestureText => "Ctrl+P";
        public override ICommand Command => ApplicationCommands.Print;
    }

    public class MenuExit : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.File;
        public override string GuidId => "MenuExit";
        public override string Header => Resources.MenuExit;
        public override string? InputGestureText => "Alt+F4";
        public override int Order => 1000000;

        public override void Execute()
        {
            Environment.Exit(0);
        }
    }

}
