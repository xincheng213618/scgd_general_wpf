﻿using ColorVision.Themes;
using System.Windows;

namespace WindowsServicePlugin
{
    public interface IUpdate
    {
        public string DownloadTile { get; set; }
    }

    /// <summary>
    /// WindowUpdate.xaml 的交互逻辑
    /// </summary>
    public partial class WindowUpdate : Window
    {
        IUpdate Update { get; set; }
        public WindowUpdate(IUpdate update)
        {
            Update = update;
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            DataContext = Update;
        }
    }
}
