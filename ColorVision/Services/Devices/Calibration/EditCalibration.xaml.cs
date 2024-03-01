using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using cvColorVision;
using ColorVision.Common.Extension;
using System.Windows.Input;


namespace ColorVision.Services.Devices.Calibration
{
    /// <summary>
    /// EditCamera.xaml 的交互逻辑
    /// </summary>
    public partial class EditCalibration : UserControl
    {
        public DeviceCalibration DeviceCalibration { get; set; }

        public MQTTCalibration Service { get => DeviceCalibration.DeviceService; }


        public EditCalibration(DeviceCalibration  deviceCalibration)
        {
            DeviceCalibration = deviceCalibration;
            InitializeComponent();
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = DeviceCalibration;
        }

    }
}
