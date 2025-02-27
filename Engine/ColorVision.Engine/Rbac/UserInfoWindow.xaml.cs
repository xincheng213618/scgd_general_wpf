﻿using ColorVision.Themes;
using System;
using System.Windows;

namespace ColorVision.Engine.Rbac
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
