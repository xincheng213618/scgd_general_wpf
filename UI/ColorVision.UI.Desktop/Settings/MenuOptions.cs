using ColorVision.UI.Properties;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.UI.Desktop.Settings
{
    public class MenuOptions : GlobalMenuBase, IHotKey
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override string Header => Resources.MenuOptions;
        public override int Order => 100000;
        public override object? Icon
        {
            get
            {
                TextBlock text = new()
                {
                    Text = "\uE713", // 使用Unicode字符
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 15,
                };
                text.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
                return text;
            }
        }
        public HotKeys HotKeys => new(Resources.MenuOptions, new Hotkey(Key.OemComma, ModifierKeys.Control), Execute) { Description = BuiltInHotkeyDescriptions.OpenSettings };
        public override void Execute()
        {
            SettingNavigation.Show(null);
        }
    }
}
