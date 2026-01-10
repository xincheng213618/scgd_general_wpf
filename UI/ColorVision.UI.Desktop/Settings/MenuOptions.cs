using ColorVision.UI.Properties;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.UI.Desktop.Settings
{
    public class MenuOptions : MenuItemBase, IHotKey
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
        public override string? InputGestureText => "Ctrl + I";
        public HotKeys HotKeys => new(Resources.MenuOptions, new Hotkey(Key.I, ModifierKeys.Control), Execute);
        public override void Execute()
        {
            new SettingWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            ConfigService.Instance.SaveConfigs();
        }
    }
}
