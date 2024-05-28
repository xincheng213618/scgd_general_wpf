using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.HotKey;
using ColorVision.Properties;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ColorVision.UI.Menus;

namespace ColorVision.Settings
{
    public class ExportSetting : IHotKey,IMenuItem
    {
        public HotKeys HotKeys => new(Properties.Resources.MenuOptions, new Hotkey(Key.I, ModifierKeys.Control), Execute);
        private void Execute()
        {
            new SettingWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public string? OwnerGuid => "Tool";

        public string? GuidId => "MenuOptions";
        public Visibility Visibility => Visibility.Visible;

        public int Order => 100000;

        public string? Header => Resources.MenuOptions;

        public string? InputGestureText => "Ctrl + I";

        public object? Icon { 
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
        public RelayCommand Command => new(A => Execute());
    }
}
