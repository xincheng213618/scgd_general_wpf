using ColorVision.Device;
using ColorVision.Templates;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Device.FilterWheel
{
    /// <summary>
    /// SMUDisplayControl.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayFilterWheelControl : UserControl
    {

        public DeviceFilterWheel Device { get; set; }
        private DeviceServiceFilterWheel DeviceService { get => Device.DeviceService;  }

        public DisplayFilterWheelControl(DeviceFilterWheel device)
        {
            this.Device = device;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Device;

        }

        private void GetPort_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var msgRecord = DeviceService.GetPort();
                Helpers.SendCommand(button, msgRecord);
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var msgRecord = DeviceService.Open();
                Helpers.SendCommand(button, msgRecord);
            }

        }

        private void SetPort_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var msgRecord = DeviceService.SetPort();
                Helpers.SendCommand(button, msgRecord);
            }
        }
    }
}
