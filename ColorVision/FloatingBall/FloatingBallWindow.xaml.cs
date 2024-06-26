﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.FloatingBall
{
    /// <summary>
    /// FloatingBallWindow.xaml 的交互逻辑
    /// </summary>
    public partial class FloatingBallWindow : Window
    {
        public FloatingBallWindow()
        {
            InitializeComponent();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if ( e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            ContextMenu contextMenu = new();
            MenuItem menuItem1 = new() { Header = "隐藏界面" };
            menuItem1.Click += (s, e) => { Close(); };
            contextMenu.Items.Add(menuItem1);


            MenuItem menuItem = new() { Header ="退出程序" };
            menuItem.Click += (s, e) => { Environment.Exit(0); };
            contextMenu.Items.Add(menuItem);

            ContextMenu = contextMenu;
        }
    }
}
