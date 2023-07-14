using ColorVision.Controls;
using ColorVision.HotKey;
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

namespace ColorVision.SettingUp
{
    /// <summary>
    /// SettingWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SettingWindow : BaseWindow
    {
        public SoftwareConfig SoftwareConfig { get;set;}
        public SettingWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            SoftwareConfig = GlobalSetting.GetInstance().SoftwareConfig;
            this.DataContext = SoftwareConfig;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void HotKeyStackPanel_Initialized(object sender, EventArgs e)
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
            string json = File.ReadAllText("Hotkey");
            List<HotKeys> HotKeysList = JsonSerializer.Deserialize<List<HotKeys>>(json) ?? new List<HotKeys>();
            foreach (HotKeys hotKeys in HotKeysList)
            {
                foreach (var item in HotKeys.HotKeysList)
                {
                    if (hotKeys.Name == item.Name)
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
            File.WriteAllText("Hotkey", Json);
        }

        private void SetProjectDefault__Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                SoftwareConfig.ProjectConfig.ProjectControl.DefaultSaveName = "yyyy/dd/MM HH:mm:ss";
                ButtonContentChange(button, "已重置");
            }

        }

        private void SetProjectDefaultCreatName_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                SoftwareConfig.ProjectConfig.ProjectControl.DefaultCreatName = "新建工程";
                ButtonContentChange(button, "已重置");
            }
        }

        private static async void ButtonContentChange(Button button, string Content)
        {
            if (button.Content.ToString() != Content)
            {
                var temp = button.Content;
                button.Content = Content;
                await Task.Delay(1000);
                button.Content = temp;
            }
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FocusManager.SetFocusedElement(this, null);
        }
    }
}
