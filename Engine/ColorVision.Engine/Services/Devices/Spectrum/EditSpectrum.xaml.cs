using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.PhyCameras.Dao;
using ColorVision.Themes;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;


namespace ColorVision.Engine.Services.Devices.Spectrum
{
    /// <summary>
    /// EditSpectrum.xaml 的交互逻辑
    /// </summary>
    public partial class EditSpectrum : Window
    {
        public DeviceSpectrum Device { get; set; }
        public ConfigSpectrum EditConfig {  get; set; }
        public EditSpectrum(DeviceSpectrum deviceSpectrum)
        {
            Device = deviceSpectrum;
            InitializeComponent();
            this.ApplyCaption();
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Common.NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            List<string> Serials = new() { "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8" };
            TextSerial.ItemsSource = Serials;
            List<int> BaudRates = new() { 115200, 9600, 300, 600, 1200, 2400, 4800, 14400, 19200, 38400, 57600 };
            TextBaudRate.ItemsSource = BaudRates;
            DataContext = Device;

            EditConfig = Device.Config.Clone();
            EditContent.DataContext = EditConfig;

            ComboBoxSn.ItemsSource = CameraLicenseDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "lic_type",1} });
            ComboBoxSn.DisplayMemberPath = "MacAddress";
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            EditConfig.CopyTo(Device.Config);
            Device.DService?.SetParam(Device.Config.MaxIntegralTime, Device.Config.BeginIntegralTime);
            Close();
        }
    }
}
