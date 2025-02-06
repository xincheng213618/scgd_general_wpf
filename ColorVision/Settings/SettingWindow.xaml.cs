using ColorVision.Themes;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Linq;
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
                    Background = IsBlurEnabled ? Background : GetThemeBackGround();
                };
            }
            else
            {
                IsBlurEnabled = ThemeConfig.Instance.TransparentWindow && IsBlurEnabled;
            }
            Background = IsBlurEnabled ? Background : GetThemeBackGround();

        }



        private void Window_Initialized(object sender, EventArgs e)
        {
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
                    toggleSwitch.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, new Binding(configSetting.BindingName));
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
                    Grid grid = new Grid();
                    grid.SetResourceReference(Panel.BackgroundProperty, "GlobalBorderBrush");
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
                else if (configSetting.Type == ConfigSettingType.Text)
                {
                    DockPanel dockPanel = new DockPanel() { Margin = new Thickness(5) };
                    TextBox textBox = new TextBox() { ToolTip = configSetting.Description ,MaxWidth =250};
                    textBox.SetBinding(TextBox.TextProperty, new Binding(configSetting.BindingName));
                    textBox.DataContext = configSetting.Source;
                    DockPanel.SetDock(textBox, Dock.Right);
                    dockPanel.Children.Add(textBox);
                    dockPanel.Children.Add(new TextBlock() { Text = configSetting.Name });
                    UniversalStackPanel.Children.Add(dockPanel);
                }
            }
            var allSettings = new List<ConfigSettingMetadata>();

            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (var type in assembly.GetTypes().Where(t => typeof(IConfigSettingProvider).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is IConfigSettingProvider configSetting)
                    {
                        allSettings.AddRange(configSetting.GetConfigSettings());
                    }
                }
            }
            // 先按 ConfigSettingType 分组，再在每个组内按 Order 排序
            var sortedSettings = allSettings
                .GroupBy(setting => setting.Type)
                .SelectMany(group => group.OrderBy(setting => setting.Order));

            // 将排序后的配置设置添加到集合中
            foreach (var item in sortedSettings)
            {
                Add(item);
            }
        }
    }
}
