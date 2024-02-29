using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Devices.Calibration
{
    /// <summary>
    /// DeviceSMUControl.xaml 的交互逻辑
    /// </summary>
    public partial class DeviceCalibrationControl : UserControl, IDisposable
    {
        public DeviceCalibration DeviceCalibration { get; set; }
        public ServiceManager ServiceControl { get; set; }

        public bool IsCanEdit { get; set; }

        public DeviceCalibrationControl(DeviceCalibration deviceCalibration, bool isCanEdit = true)
        {
            this.DeviceCalibration = deviceCalibration;
            IsCanEdit = isCanEdit;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = this.DeviceCalibration;
            if (!IsCanEdit) ButtonEdit.Visibility = IsCanEdit ? Visibility.Visible : Visibility.Collapsed;
            if (IsCanEdit)
            {
                UserControl userControl = DeviceCalibration.GetEditControl();
                if (userControl.Parent is Panel grid)
                    grid.Children.Remove(userControl);
                MQTTEditContent.Children.Add(userControl);
            }
        }
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

    }
}
