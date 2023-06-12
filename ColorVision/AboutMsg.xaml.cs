
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision
{
    /// <summary>
    /// AboutMsg.xaml 的交互逻辑
    /// </summary>
    public partial class AboutMsgWindow:Window
    {
        public AboutMsgWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.Topmost = true;
            this.Icon = null;
            CloseButton.Focus();

        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Image_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Process.Start("explorer", "https://www.color-vision.com.cn/");
            this.Close();
        }
    }
}
