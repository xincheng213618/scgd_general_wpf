﻿using ColorVision.Services;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Devices.PG
{
    /// <summary>
    /// DevicePGControl.xaml 的交互逻辑
    /// </summary>
    public partial class DevicePGControl : UserControl
    {
        public DevicePG DevicePG { get; set; }
        public bool IsCanEdit { get; set; }
        public DevicePGControl(DevicePG devicePG, bool isCanEdit = true)
        {
            DevicePG = devicePG;
            IsCanEdit = isCanEdit;
            InitializeComponent();


        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (!IsCanEdit) ButtonEdit.Visibility = IsCanEdit ? Visibility.Visible : Visibility.Collapsed;
            this.DataContext = DevicePG;
        }


    }
}
