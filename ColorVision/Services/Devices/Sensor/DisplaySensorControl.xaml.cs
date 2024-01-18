using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Devices.Sensor
{
    /// <summary>
    /// DisplaySMUControl.xaml 的交互逻辑
    /// </summary>
    public partial class DisplaySensorControl : UserControl
    {

        public DeviceSensor Device { get; set; }
        private MQTTSensor DeviceService { get => Device.DeviceService;  }

        public DisplaySensorControl(DeviceSensor device)
        {
            this.Device = device;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Device;
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            DeviceService.Init();
        }
    }
}
