﻿using ColorVision.Themes;
using ColorVision.UI;
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
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            PluginLoader.LoadPluginsUS("Plugins");
        }

        private void Window_Initialized(object sender, System.EventArgs e)
        {
      
        }
    }
}