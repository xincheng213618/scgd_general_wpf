using ColorVision.Templates;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Device.Sensor
{
    /// <summary>
    /// SMUDisplayControl.xaml 的交互逻辑
    /// </summary>
    public partial class DisplaySensorControl : UserControl
    {

        public DeviceSensor Device { get; set; }
        private DeviceServiceSensor DeviceService { get => Device.DeviceService;  }

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
