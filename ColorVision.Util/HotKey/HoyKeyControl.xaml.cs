using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ColorVision.HotKey
{

    public sealed class BoolToStringConverer : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool boll) && (boll) ? ColorVision.Util.Properties.Resource.HotkeyNormal : Util.Properties.Resource.HotkeyNormal;

        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();

    }


    public sealed class HotKeyToStringConverer : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Hotkey hotkey)
                return hotkey.ToString();

            return string.Empty;

        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();

    }



    /// <summary>
    /// HoyKeyControl.xaml 的交互逻辑
    /// </summary>
    public partial class HoyKeyControl : UserControl
    {
        HotKeys HotKeys;
        public HoyKeyControl(HotKeys hotKeys)
        {   
            this.HotKeys = hotKeys;
            InitializeComponent();
            this.DataContext = HotKeys;
        }

        private static bool HasKeyChar(Key key) => key is
     >= Key.A and <= Key.Z or
     // 0 - 9
     >= Key.D0 and <= Key.D9 or
     // Numpad 0 - 9
     >= Key.NumPad0 and <= Key.NumPad9 or
     // The rest
     Key.OemQuestion or Key.OemQuotes or Key.OemPlus or Key.OemOpenBrackets or Key.OemCloseBrackets or
     Key.OemMinus or Key.DeadCharProcessed or Key.Oem1 or Key.Oem5 or Key.Oem7 or Key.OemPeriod or
     Key.OemComma or Key.Add or Key.Divide or Key.Multiply or Key.Subtract or Key.Oem102 or Key.Decimal;

        private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            var key = e.Key;
            // If nothing was pressed - return
            if (key == Key.None)
                return;

            // Get modifiers and key data
            var modifiers = Keyboard.Modifiers;
            //Get modifier Win
            if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.Right))
                modifiers |= ModifierKeys.Windows;

            // If Alt is used as modifier - the key needs to be extracted from SystemKey
            if (key == Key.System)
                key = e.SystemKey;

            // If Delete/Backspace/Escape is pressed without modifiers - clear current value and return
            if (key is Key.Delete or Key.Back or Key.Escape && modifiers == ModifierKeys.None)
            {
                HotKeys.Hotkey = Hotkey.None;
                return;
            }

            // If the only key pressed is one of the modifier keys - return
            if (key is
                Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
                Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or
                Key.Clear or Key.OemClear or Key.Apps)
                return;

            // If Enter/Space/Tab is pressed without modifiers - return
            if (key is Key.Enter or Key.Space or Key.Tab && modifiers == ModifierKeys.None)
                return;

            // If key has a character and pressed without modifiers or only with Shift - return
            if (HasKeyChar(key) && modifiers is ModifierKeys.None or ModifierKeys.Shift)
                return;

            // Set value
            HotKeys.Hotkey = new Hotkey(key, modifiers);
            HotkeyTextBox1.Visibility = Visibility.Collapsed;
        }

        private void HotkeyTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            HotkeyTextBox1.Visibility = Visibility.Visible;
            HotkeyTextBox1.Text = "按按键设置快捷键";
        }

        private void HotkeyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (HotkeyTextBox.Text == "<None>")
            {
                HotkeyTextBox1.Text = "点击设置快捷键";
            }
            else
            {
                HotkeyTextBox1.Visibility = Visibility.Collapsed;
            }

        }
    }
}
