using ColorVision.Common.Extension;
using ColorVision.Engine.Services;
using ColorVision.Themes;
using ColorVision.UI.Authorizations;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.UserSpace
{
    /// <summary>
    /// UserInfoWindow.xaml 的交互逻辑
    /// </summary>
    public partial class UserInfoWindow : Window
    {
        public UserInfoWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }
        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = UserManager.GetInstance();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
