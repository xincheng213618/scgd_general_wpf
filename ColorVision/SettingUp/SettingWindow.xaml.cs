using ColorVision.HotKey;
using ColorVision.MQTT;
using ColorVision.MySql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ColorVision.Extension;
using ColorVision.Themes;
using ColorVision.Language;
using System.Globalization;
using ColorVision.Themes.Controls;
using ColorVision.RC;
using System.Threading;

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
            IsBlurEnabled = GlobalSetting.GetInstance().SoftwareConfig.SoftwareSetting.TransparentWindow && IsBlurEnabled;
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

            cmtheme.ItemsSource = from e1 in Enum.GetValues(typeof(Theme)).Cast<Theme>()
                                  select new KeyValuePair<Theme, string>(e1, Properties.Resource.ResourceManager.GetString(e1.ToDescription(), CultureInfo.CurrentUICulture)??"");

            cmtheme.SelectedValuePath = "Key";
            cmtheme.DisplayMemberPath = "Value";
            cmtheme.SelectionChanged += Cmtheme_SelectionChanged;

            
            if (LanguageManager.Current.Languages.Count <= 1)
                lauagDock.Visibility = Visibility.Collapsed;

            cmlauage.ItemsSource = from e1 in LanguageManager.Current.Languages
                                   select new KeyValuePair<string, string>(e1, LanguageManager.keyValuePairs.TryGetValue(e1, out string value) ? value : e1);
            cmlauage.SelectedValuePath = "Key";
            cmlauage.DisplayMemberPath = "Value";

            string temp = Thread.CurrentThread.CurrentUICulture.Name;


            cmlauage.SelectionChanged += (s, e) =>
            {
                if (cmlauage.SelectedValue is string str)
                {
                    if (!LanguageManager.Current.LanguageChange(str))
                    {
                        SoftwareConfig.SoftwareSetting.UICulture = temp;
                    }
                }
            };
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
            //string json = File.ReadAllText("Hotkey");
            //List<HotKeys> HotKeysList = JsonSerializer.Deserialize<List<HotKeys>>(json) ?? new List<HotKeys>();
            //foreach (HotKeys hotKeys in HotKeysList)
            //{
            //    foreach (var item in HotKeys.HotKeysList)
            //    {
            //        if (hotKeys.Name == item.Name)
            //        {
            //            item.Hotkey = hotKeys.Hotkey;
            //            item.Kinds = hotKeys.Kinds;
            //        }
            //    }
            //}
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            //JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions() { Encoder = JavaScriptEncoder.CreateSolution(UnicodeRanges.All) };
            //string Json = JsonSerializer.Serialize(HotKeys.HotKeysList, jsonSerializerOptions);
            //File.WriteAllText("Hotkey", Json);
        }

        private void SetProjectDefault__Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                SoftwareConfig.SolutionConfig.SolutionSetting.DefaultSaveName = "yyyy/dd/MM HH:mm:ss";
                ButtonContentChange(button, Properties.Resource.Reseted);
            }

        }

        private void SetProjectDefaultCreatName_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                SoftwareConfig.SolutionConfig.SolutionSetting.DefaultCreatName = "新建工程";
                ButtonContentChange(button, Properties.Resource.Reseted);
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

        private void TextBlock_MouseLeftButtonDown2(object sender, MouseButtonEventArgs e)
        {
            new RCServiceConnect() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
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
