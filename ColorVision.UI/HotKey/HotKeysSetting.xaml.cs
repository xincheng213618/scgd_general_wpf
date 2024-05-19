using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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
