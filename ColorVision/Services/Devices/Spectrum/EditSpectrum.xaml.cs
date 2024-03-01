using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Devices.Spectrum
{
    /// <summary>
    /// EditSpectrum.xaml 的交互逻辑
    /// </summary>
    public partial class EditSpectrum : UserControl
    {
        public DeviceSpectrum DeviceSpectrum { get;set;}
        public EditSpectrum(DeviceSpectrum deviceSpectrum)
        {
            DeviceSpectrum = deviceSpectrum;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            List<string> Serials = new List<string> { "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8" };
            TextSerial.ItemsSource = Serials;
            List<int> BaudRates = new List<int> { 115200, 9600, 300, 600, 1200, 2400, 4800, 14400, 19200, 38400, 57600 };
            TextBaudRate.ItemsSource = BaudRates;
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DeviceSpectrum.DeviceService?.SetParam(DeviceSpectrum.Config.MaxIntegralTime, DeviceSpectrum.Config.BeginIntegralTime);
        }
    }
}
