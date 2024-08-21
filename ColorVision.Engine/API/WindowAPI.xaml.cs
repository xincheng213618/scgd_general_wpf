using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Windows;

namespace ColorVision.Engine.API
{
    /// <summary>
    /// WindowAPI.xaml 的交互逻辑
    /// </summary>
    public partial class WindowAPI : Window
    {
        public WindowAPI()
        {   
            InitializeComponent();
        }

        private void StartApiHost_Click(object sender, RoutedEventArgs e)
        {
        }

    }
}
