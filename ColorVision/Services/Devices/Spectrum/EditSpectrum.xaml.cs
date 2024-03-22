using ColorVision.Common.Extension;
using ColorVision.Common.MVVM;
using ColorVision.Services.Devices.Calibration;
using ColorVision.Services.Devices.Spectrum;
using ColorVision.Services.Devices.Spectrum.Configs;
using cvColorVision;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing.Text;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace ColorVision.Services.Devices.Spectrum
{
    /// <summary>
    /// EditCamera.xaml 的交互逻辑
    /// </summary>
    public partial class EditSpectrum : Window
    {
        public DeviceSpectrum Device { get; set; }
        public ConfigSpectrum EditConfig {  get; set; }
        public EditSpectrum(DeviceSpectrum deviceSpectrum)
        {
            Device = deviceSpectrum;
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
            List<string> Serials = new List<string> { "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8" };
            TextSerial.ItemsSource = Serials;
            List<int> BaudRates = new List<int> { 115200, 9600, 300, 600, 1200, 2400, 4800, 14400, 19200, 38400, 57600 };
            TextBaudRate.ItemsSource = BaudRates;
            this.DataContext = Device;

            EditConfig = Device.Config.Clone();
            EditContent.DataContext = EditConfig;
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            EditConfig.CopyTo(Device.Config);
            Device.DeviceService?.SetParam(Device.Config.MaxIntegralTime, Device.Config.BeginIntegralTime);
            this.Close();
        }
    }
}
