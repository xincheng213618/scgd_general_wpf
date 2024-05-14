using ColorVision.Common.Extension;
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.HotKey;
using ColorVision.UI.Languages;
using ColorVision.MQTT;
using ColorVision.MySql;
using ColorVision.Properties;
using ColorVision.Services.RC;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Settings
{
    public class ExportSetting : IHotKey,IMenuItem
    {
        public HotKeys HotKeys => new(Properties.Resource.MenuOptions, new Hotkey(Key.I, ModifierKeys.Control), Execute);
        private void Execute()
        {
            new SettingWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public string? OwnerGuid => "Tool";

        public string? GuidId => "MenuOptions";

        public int Order => 100000;

        public string? Header => Resource.MenuOptions;

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


    /// <summary>
    /// SettingWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SettingWindow : BaseWindow
    {
        
        public SoftwareConfig SoftwareConfig { get;set;}
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
                IsBlurEnabled = ConfigHandler.GetInstance().SoftwareConfig.SoftwareSetting.TransparentWindow && IsBlurEnabled;
            }
            Background = IsBlurEnabled ? Background :ThemeManager.Current.CurrentUITheme == Theme.Light?Brushes.White:Brushes.Black;

        }
        private void Window_Initialized(object sender, EventArgs e)
        {
            SoftwareConfig = ConfigHandler.GetInstance().SoftwareConfig;
            DataContext = SoftwareConfig;
            AutoRunDock.DataContext = ConfigHandler.GetInstance();
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
                        LanguageConfig.Instance.UICulture = temp;
                    }
                }
            };

            lauagDock.DataContext = LanguageConfig.Instance;
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
                SoftwareConfig.SolutionSetting.DefaultSaveName = "yyyy/dd/MM HH:mm:ss";
                ButtonContentChange(button, Properties.Resource.Reseted);
            }

        }

        private void SetProjectDefaultCreatName_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                SoftwareConfig.SolutionSetting.DefaultCreatName = "新建工程";
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
            ConfigHandler.GetInstance().SaveConfig();
            MessageBox.Show("MQTT历史记录清理完毕", "ColorVision");
        }


    }
}
