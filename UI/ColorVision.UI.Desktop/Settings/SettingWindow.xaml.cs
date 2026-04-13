using ColorVision.Common.MVVM;
using ColorVision.Themes;
using log4net;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.UI.Desktop.Settings
{
    /// <summary>
    /// SettingWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SettingWindow 
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SettingWindow));

        public SettingWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
           LoadIConfigSetting();
        }

        public void LoadIConfigSetting()
        {
            var settingStackPanels = new Dictionary<string, StackPanel>
            {
                { ConfigSettingConstants.Universal, UniversalStackPanel }
            };

            var sortedSettings = ConfigSettingManager.GetInstance().GetAllSettings();

            // 为新的分组创建选项卡
            foreach (var group in sortedSettings)
            {
                if (!settingStackPanels.ContainsKey(group.Group))
                {
                    TabItem tabItem = new TabItem() { Header = group.Group, Background = Brushes.Transparent };
                    Grid grid = new Grid();
                    grid.SetResourceReference(Panel.BackgroundProperty, "GlobalBorderBrush");

                    StackPanel stackPanel = new StackPanel() { Margin = new Thickness(10) };
                    grid.Children.Add(stackPanel);
                    tabItem.Content = grid;
                    TabControlSetting.Items.Add(tabItem);

                    settingStackPanels.Add(group.Group, stackPanel);
                }
            }

            // 将配置设置添加到对应面板
            foreach (var item in sortedSettings)
            {
                try
                {
                    AddSettingItem(item, settingStackPanels);
                }
                catch (Exception ex)
                {
                    log.Warn($"Failed to add setting: {item.Name ?? item.BindingName}: {ex.Message}");
                }
            }
        }

        private void AddSettingItem(ConfigSettingMetadata configSetting, Dictionary<string, StackPanel> settingStackPanels)
        {
            if (configSetting.Type == ConfigSettingType.TabItem)
            {
                TabItem tabItem = new TabItem() { Header = configSetting.Name, Background = Brushes.Transparent };
                Grid grid = new Grid();
                grid.SetResourceReference(Panel.BackgroundProperty, "GlobalBorderBrush");
                if (configSetting.ViewType != null)
                {
                    // 懒加载：仅在 TabItem 被选中时才实例化 UserControl
                    tabItem.Tag = configSetting.ViewType;
                    TabControlSetting.SelectionChanged += LazyLoadTabContent;
                }
                tabItem.Content = grid;
                TabControlSetting.Items.Add(tabItem);
            }
            else if (configSetting.Type == ConfigSettingType.Class)
            {
                TabItem tabItem = new TabItem() { Header = configSetting.Name, Background = Brushes.Transparent };
                Grid grid = new Grid();
                grid.SetResourceReference(Panel.BackgroundProperty, "GlobalBorderBrush");
                if (configSetting.ViewType != null)
                {
                    tabItem.Tag = configSetting.ViewType;
                    TabControlSetting.SelectionChanged += LazyLoadTabContent;
                }
                else if (configSetting.Source is ViewModelBase obj)
                {
                    grid.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(obj));
                }
                tabItem.Content = grid;
                TabControlSetting.Items.Add(tabItem);
            }
            else
            {
                if (configSetting.Source == null || configSetting.BindingName == null) return;

                PropertyInfo propertyInfo = configSetting.Source.GetType().GetProperty(configSetting.BindingName);
                DockPanel dockPanel = PropertyEditorHelper.GenProperties(propertyInfo, configSetting.Source);
                dockPanel.Margin = new Thickness(0, 0, 0, 5);
                settingStackPanels[configSetting.Group].Children.Add(dockPanel);
            }
        }

        private static void LazyLoadTabContent(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return;
            if (e.AddedItems[0] is not TabItem tabItem) return;
            if (tabItem.Tag is not Type viewType) return;

            // 已加载，不再重复
            if (tabItem.Content is Grid grid && grid.Children.Count > 0) return;

            try
            {
                if (Activator.CreateInstance(viewType) is UserControl control)
                {
                    if (tabItem.Content is not Grid existingGrid)
                    {
                        existingGrid = new Grid();
                        existingGrid.SetResourceReference(Panel.BackgroundProperty, "GlobalBorderBrush");
                        tabItem.Content = existingGrid;
                    }
                    existingGrid.Children.Add(control);
                    tabItem.Tag = null; // 标记为已加载
                }
            }
            catch (Exception ex)
            {
                LogManager.GetLogger(typeof(SettingWindow)).Warn($"Lazy load failed for {viewType.Name}: {ex.Message}");
            }
        }
    }
}
