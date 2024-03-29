﻿using System;
using System.Windows;

namespace ColorVision.Services.Devices
{

    public interface IUploadMsg
    {
        public string Msg { get; }

        public event EventHandler UploadClosed;
    }


    /// <summary>
    /// UploadMsg.xaml 的交互逻辑
    /// </summary>
    public partial class UploadMsg : Window
    {
        public IUploadMsg IUploadMsg { get; set; }
        public UploadMsg(IUploadMsg iUploadMsg)
        {
            IUploadMsg = iUploadMsg;
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = IUploadMsg;
            IUploadMsg.UploadClosed += (s, e) => this.Close();
        }
    }
}
