using ColorVision.UI.Properties;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.UI.Menus.Base.File
{
    public class MenuOpen : MenuItemFileBase
    {
        public override string GuidId => nameof(MenuOpen);

        public override int Order => 0;

        public override string Header => Resources.MenuOpen;

        public override string InputGestureText => "Ctrl+O";

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
    }

}
