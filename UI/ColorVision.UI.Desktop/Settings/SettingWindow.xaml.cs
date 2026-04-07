using ColorVision.Common.MVVM;
using ColorVision.Themes;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.UI.Desktop.Settings
{
    /// <summary>
    /// SettingWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SettingWindow 
    {
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
            Dictionary<string,StackPanel> SettingStackPanels = new Dictionary<string, StackPanel>();
            SettingStackPanels.Add(ConfigSettingConstants.Universal, UniversalStackPanel);


            void Add(ConfigSettingMetadata configSetting)
            {

                if (configSetting.Type == ConfigSettingType.TabItem)
                {
                    TabItem tabItem = new TabItem() { Header = configSetting.Name, Background = Brushes.Transparent };
                    Grid grid = new Grid();
                    grid.SetResourceReference(Panel.BackgroundProperty, "GlobalBorderBrush");
                    if (configSetting.ViewType != null)
                    {
                        if (Activator.CreateInstance(configSetting.ViewType) is UserControl lazyControl)
                            grid.Children.Add(lazyControl);
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
                        if (Activator.CreateInstance(configSetting.ViewType) is UserControl lazyControl)
                            grid.Children.Add(lazyControl);
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
                    SettingStackPanels[configSetting.Group].Children.Add(dockPanel);
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

            // Attribute-based discovery: scan types decorated with [ConfigSetting]
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    var attr = type.GetCustomAttribute<ConfigSettingAttribute>();
                    if (attr != null)
                    {
                        allSettings.Add(new ConfigSettingMetadata
                        {
                            Name = attr.Name ?? type.Name,
                            Group = attr.Group,
                            Order = attr.Order,
                            Description = attr.Description,
                            Type = attr.Type,
                            ViewType = attr.ViewType,
                            Source = attr.Type != ConfigSettingType.TabItem ? Activator.CreateInstance(type) : null,
                        });
                    }
                }
            }

            // 先按 ConfigSettingType 分组，再在每个组内按 Order 排序
            var sortedSettings = allSettings
                .GroupBy(setting => setting.Type)
                .SelectMany(group => group.OrderBy(setting => setting.Order));

            foreach (var group in sortedSettings) 
            {
                if (!SettingStackPanels.ContainsKey(group.Group))
                {
                    TabItem tabItem = new TabItem() { Header = group.Group, Background = Brushes.Transparent };
                    Grid grid = new Grid();
                    grid.SetResourceReference(Panel.BackgroundProperty, "GlobalBorderBrush");

                    StackPanel stackPanel = new StackPanel() { Margin = new Thickness(10) };
                    grid.Children.Add(stackPanel);
                    tabItem.Content = grid;
                    TabControlSetting.Items.Add(tabItem);

                    SettingStackPanels.Add(group.Group, stackPanel);
                }
            }

            // 将排序后的配置设置添加到集合中
            foreach (var item in sortedSettings)
            {
                try
                {
                    Add(item);
                }
                catch (Exception ex)
                {

                }
            }
        }
    }
}
