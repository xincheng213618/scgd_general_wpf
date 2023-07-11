
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorVision.HotKey
{
    /// <summary>
    /// HotKeyManger.xaml 的交互逻辑
    /// </summary>
    public partial class HotKeyManger : Window
    {
        public HotKeyManger()
        {
            InitializeComponent();
            foreach (HotKeys hotKeys in HotKeys.HotKeysList)
            {
                HotKeyStackPanel.Children.Add(new HoyKeyControl(hotKeys));
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            HotKeys.SetDefault();
        }

        private void ButtonLoad_Click(object sender, RoutedEventArgs e)
        {
            string json= File.ReadAllText("Hotkey");
            List<HotKeys> HotKeysList = JsonSerializer.Deserialize<List<HotKeys>>(json)??new List<HotKeys>();
            foreach (HotKeys hotKeys in HotKeysList)
            {
                foreach (var item in HotKeys.HotKeysList)
                {
                    if(hotKeys.Name == item.Name)
                    {
                        item.Hotkey = hotKeys.Hotkey;
                        item.Kinds = hotKeys.Kinds;
                    }
                }
            }
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions() { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };
            string Json = JsonSerializer.Serialize(HotKeys.HotKeysList, jsonSerializerOptions);
            File.WriteAllText("Hotkey",Json);
        }
    }
}
