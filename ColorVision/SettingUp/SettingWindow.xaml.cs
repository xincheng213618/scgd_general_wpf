using ColorVision.Controls;
using ColorVision.HotKey;
using ColorVision.MQTT;
using ColorVision.MySql;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ColorVision.Extension;
using ColorVision.Theme;
using ColorVision.Language;

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
            AutoRunDock.DataContext = GlobalSetting.GetInstance();
            GlobalConst.LogLevel.ForEach(it =>
            {
                cmbloglevel.Items.Add(it);
            });

            cmtheme.ItemsSource = from e1 in Enum.GetValues(typeof(Theme.Theme)).Cast<Theme.Theme>()
                                  select new KeyValuePair<Theme.Theme, string>(e1, e1.ToDescription());

            cmtheme.SelectedValuePath = "Key";
            cmtheme.DisplayMemberPath = "Value";
            cmtheme.SelectionChanged += Cmtheme_SelectionChanged;

            
            if (LanguageManager.Current.Languages.Count <= 1)
                lauagDock.Visibility = Visibility.Collapsed;

            cmlauage.ItemsSource = LanguageManager.Current.Languages;
            cmlauage.SelectionChanged += (s, e) =>
            {
                if (cmlauage.SelectedValue is string str)
                    LanguageManager.Current.LanguageChange(str);
            };

            //BitmapImage bitmapImage = new BitmapImage(new Uri(AppDomain.CurrentDomain.BaseDirectory + SoftwareConfig.UserConfig.UserImage));
            //HeaderImage.Source = bitmapImage;
        }

        private void Cmtheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Application.Current.ApplyTheme(SoftwareConfig.SoftwareSetting.Theme);
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
                SoftwareConfig.SolutionConfig.SolutionSetting.DefaultSaveName = "yyyy/dd/MM HH:mm:ss";
                ButtonContentChange(button, "已重置");
            }

        }

        private void SetProjectDefaultCreatName_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                SoftwareConfig.SolutionConfig.SolutionSetting.DefaultCreatName = "新建工程";
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


        private void TextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            new MQTTConnect() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        private void TextBlock_MouseLeftButtonDown1(object sender, MouseButtonEventArgs e)
        {
            new MySqlConnect() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            SoftwareConfig.MQTTSetting.MsgRecords.Clear();
            GlobalSetting.GetInstance().SaveSoftwareConfig();
            MessageBox.Show("MQTT历史记录清理完毕", "ColorVision");
        }

    }
}
