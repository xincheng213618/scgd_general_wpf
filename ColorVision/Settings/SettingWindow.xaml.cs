using ColorVision.Services.Msg;
using ColorVision.Solution;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using ColorVision.UI.Configs;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
            DataContext = new SoftwareConfig();
            GlobalConst.LogLevel.ForEach(it =>
            {
                cmbloglevel.Items.Add(it);
            });

           LoadIConfigSetting();
        }


        public void LoadIConfigSetting()
        {

            void Add(ConfigSettingMetadata configSetting)
            {
                if (configSetting.Type == ConfigSettingType.Bool)
                {
                    DockPanel dockPanel = new DockPanel() { Margin = new Thickness(5) };
                    Wpf.Ui.Controls.ToggleSwitch toggleSwitch = new() { ToolTip = configSetting.Description };
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
                if (configSetting.Type == ConfigSettingType.TabItem)
                {
                    TabItem tabItem = new TabItem() { Header = configSetting.Name , Background = Brushes.Transparent};
                    Grid grid = new Grid { Background = (Brush)Application.Current.Resources["GlobalBorderBrush"] };
                    GroupBox groupBox = new GroupBox
                    {
                        Header = new TextBlock { Text = configSetting.Name, FontSize = 20 },
                        Background = Brushes.Transparent,
                        Template = (ControlTemplate)Resources["GroupBoxHeader1"]
                    };
                    groupBox.Content = configSetting.UserControl;
                    grid.Children.Add(groupBox);
                    tabItem.Content = grid;
                    TabControlSetting.Items.Add(tabItem);
                }
            }
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes().Where(t => typeof(IConfigSettingProvider).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is IConfigSettingProvider configSetting)
                    {
                        foreach (var item in configSetting.GetConfigSettings())
                        {
                            Add(item);
                        }
                    }

                }
            }   
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


        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            MsgConfig.Instance.MsgRecords.Clear();
            MessageBox.Show("MQTT历史记录清理完毕", "ColorVision");
        }


    }
}
