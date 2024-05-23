using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.HotKey
{
    /// <summary>
    /// HotKeysSetting.xaml 的交互逻辑
    /// </summary>
    public partial class HotKeysSetting : UserControl
    {
        public HotKeysSetting()
        {
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            foreach (HotKeys hotKeys in HotKeys.HotKeysList)
            {
                HotKeyStackPanel.Children.Add(new HoyKeyControl(hotKeys));
            }
        }

        private void SetDefault_Click(object sender, RoutedEventArgs e)
        {
            HotKeys.SetDefault();
        }

        private void ButtonLoad_Click(object sender, RoutedEventArgs e)
        {
            var hotKeysDictionary = HotKeys.HotKeysList.ToDictionary(hk => hk.Name, hk => hk);

            foreach (var hotKeys in HotKeyConfig.Instance.Hotkeys)
            {
                if (hotKeysDictionary.TryGetValue(hotKeys.Name, out var item))
                {
                    item.Hotkey = hotKeys.Hotkey;
                    item.Kinds = hotKeys.Kinds;
                }
            }
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            HotKeyConfig.Instance.Hotkeys = HotKeys.HotKeysList;
        }
    }
}
