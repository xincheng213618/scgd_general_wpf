using ColorVision.MQTT;
using ColorVision.MySql;
using ColorVision.Services.Msg;
using ColorVision.Services.RC;
using ColorVision.Solution;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using ColorVision.UI.Configs;
using ColorVision.UI.HotKey;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Settings
{
    /// <summary>
    /// SettingWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SettingWindow : BaseWindow
    {
        public SettingWindow()
        {
            InitializeComponent();

            if (IsWin10)
            {
                IsBlurEnabled = false;
                ThemeManager.Current.CurrentUIThemeChanged += (e) =>
                {
                    Background = IsBlurEnabled ? Background : e == Theme.Light ? Brushes.White : Brushes.Black;
                };
            }
            else
            {
                IsBlurEnabled = ThemeConfig.Instance.TransparentWindow && IsBlurEnabled;
            }
            Background = IsBlurEnabled ? Background :ThemeManager.Current.CurrentUITheme == Theme.Light?Brushes.White:Brushes.Black;

        }
        private void Window_Initialized(object sender, EventArgs e)
        {
            DataContext = SoftwareConfig.Instance;
            GlobalConst.LogLevel.ForEach(it =>
            {
                cmbloglevel.Items.Add(it);
            });


            LoadIConfigSetting();
        }


        public void LoadIConfigSetting()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes().Where(t => typeof(IConfigSetting).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is IConfigSetting configSetting)
                    {
                        if (configSetting.Type == ConfigSettingType.Bool)
                        {
                            DockPanel dockPanel = new DockPanel() { Margin = new Thickness(5) };
                            Wpf.Ui.Controls.ToggleSwitch toggleSwitch = new ();
                            toggleSwitch.SetBinding(Wpf.Ui.Controls.ToggleSwitch.IsCheckedProperty, new Binding(configSetting.BindingName));
                            toggleSwitch.DataContext = configSetting.Source;
                            DockPanel.SetDock(toggleSwitch, Dock.Right);
                            dockPanel.Children.Add(toggleSwitch);
                            dockPanel.Children.Add(new TextBlock() { Text = configSetting.Name });
                            UniversalStackPanel.Children.Add(dockPanel);
                        }
                        if (configSetting.Type == ConfigSettingType.ComboBox)
                        {
                            DockPanel dockPanel = new DockPanel() { Margin = new Thickness(5) };
                            ComboBox comboBox = configSetting.ComboBox;
                            DockPanel.SetDock(comboBox, Dock.Right);
                            dockPanel.Children.Add(comboBox);
                            dockPanel.Children.Add(new TextBlock() { Text = configSetting.Name });
                            UniversalStackPanel.Children.Add(dockPanel);
                        }
                    }
                }
            }
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
            //        if (hotKeys.DisPlayName == item.DisPlayName)
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
                SolutionSetting.Instance.DefaultSaveName = "yyyy/dd/MM HH:mm:ss";
                ButtonContentChange(button, Properties.Resource.Reseted);
            }

        }

        private void SetProjectDefaultCreatName_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                SolutionSetting.Instance.DefaultCreatName = "新建工程";
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
            MsgConfig.Instance.MsgRecords.Clear();
            MessageBox.Show("MQTT历史记录清理完毕", "ColorVision");
        }


    }
}
