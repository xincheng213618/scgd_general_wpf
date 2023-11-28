using ColorVision.Services;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Device.CfwPort
{
    /// <summary>
    /// DeviceSMUControl.xaml 的交互逻辑
    /// </summary>
    public partial class DeviceCfwPortControl : UserControl, IDisposable
    {
        public DeviceCfwPort Device { get; set; }
        public ServiceManager ServiceControl { get; set; }

        public bool IsCanEdit { get; set; }

        public DeviceCfwPortControl(DeviceCfwPort deviceCfwPort, bool isCanEdit = true)
        {
            this.Device = deviceCfwPort;
            IsCanEdit = isCanEdit;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ButtonEdit.Visibility = IsCanEdit ? Visibility.Visible : Visibility.Collapsed;

            this.DataContext = this.Device;
            List<string> Serials = new List<string> { "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8" };
            TextSerial.ItemsSource = Serials;
            List<int> BaudRates = new List<int> { 115200, 9600, 300, 600, 1200, 2400, 4800, 14400, 19200, 38400, 57600 };
            TextBaudRate.ItemsSource = BaudRates;
        }

        private void ButtonEdit_Click(object sender, RoutedEventArgs e)
        {
            ButtonEdit.Visibility = Visibility.Collapsed;
            MQTTShowContent.Visibility = Visibility.Collapsed;
            MQTTEditContent.Visibility = Visibility.Visible;
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MQTTEditContent.Visibility = Visibility.Collapsed;
            MQTTShowContent.Visibility = Visibility.Visible;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }


    }
}
