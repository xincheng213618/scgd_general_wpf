using ColorVision.Common.MVVM;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace ColorVision.Services.Devices.Calibration
{
    /// <summary>
    /// EditCamera.xaml 的交互逻辑
    /// </summary>
    public partial class EditCalibration : Window
    {
        public DeviceCalibration DeviceCalibration { get; set; }

        public MQTTCalibration Service { get => DeviceCalibration.DeviceService; }

        public ConfigCalibration EditConfig { get; set; }
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
            EditConfig = DeviceCalibration.Config.Clone();
            EditContent.DataContext = EditConfig;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            EditConfig.CopyTo(DeviceCalibration.Config);
            this.Close();
        }
    }
}
