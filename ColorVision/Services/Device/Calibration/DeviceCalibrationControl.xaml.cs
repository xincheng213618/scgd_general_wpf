﻿using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Device.Calibration
{
    /// <summary>
    /// DeviceSMUControl.xaml 的交互逻辑
    /// </summary>
    public partial class DeviceCalibrationControl : UserControl, IDisposable
    {
        public DeviceCalibration Device { get; set; }
        public ServiceManager ServiceControl { get; set; }

        public bool IsCanEdit { get; set; }

        public DeviceCalibrationControl(DeviceCalibration deviceCalibration, bool isCanEdit = true)
        {
            this.Device = deviceCalibration;
            IsCanEdit = isCanEdit;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (!IsCanEdit) ButtonEdit.Visibility = IsCanEdit ? Visibility.Visible : Visibility.Collapsed;
            this.DataContext = this.Device;
        }
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

    }
}
