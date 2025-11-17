using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services
{
    /// <summary>
    /// CacheSettingWindow.xaml 的交互逻辑
    /// </summary>
    public partial class CacheSettingWindow : Window
    {
        public CacheSettingWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = CacheSettingManager.GetInstance();
        }

        private void ListViewProjects_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
