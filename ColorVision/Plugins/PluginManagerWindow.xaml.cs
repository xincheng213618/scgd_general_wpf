using ColorVision.Themes;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Plugins
{

    /// <summary>
    /// PluginManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class PluginManagerWindow : Window
    {

        public PluginManagerWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            PluginWindowConfig.Instance.SetWindow(this);
            this.SizeChanged += (s, e) => PluginWindowConfig.Instance.SetConfig(this);
        }

        private void Window_Initialized(object sender, System.EventArgs e)
        {
            this.DataContext = PluginManager.GetInstance(); ;
            DefalutSearchComboBox.ItemsSource = new List<string>() { "ColorVisonChat", "EventVWR", "ScreenRecorder", "SystemMonitor", "WindowsServicePlugin" };
            ListViewPlugins.SelectedIndex = 0;
        }

        private void ListViewPlugins_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ListViewPlugins.SelectedIndex > -1)
            {
                BorderContent.DataContext = PluginManager.GetInstance().Plugins[ListViewPlugins.SelectedIndex];
            }
        }
    }
}
