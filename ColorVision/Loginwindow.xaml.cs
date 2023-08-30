using System;
using System.Windows;

namespace ColorVision
{
    /// <summary>
    /// LoginWindow.xaml 的交互逻辑
    /// </summary>
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

            this.Close();
        }
    }
}
