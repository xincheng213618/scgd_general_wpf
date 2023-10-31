using ColorVision.Device;
using ColorVision.Templates;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Device.Motor
{
    /// <summary>
    /// SMUDisplayControl.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayMotorControl : UserControl
    {

        public DeviceMotor Device { get; set; }
        private DeviceServiceMotor DeviceService { get => Device.DeviceService;  }

        public DisplayMotorControl(DeviceMotor device)
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
            if (sender is Button button)
            {
                var msgrecord = DeviceService.Open();
                Helpers.SendCommand(button, msgrecord);
            }
        }

        private void Move_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var msgrecord= DeviceService.Move();
                Helpers.SendCommand(button,msgrecord);
            }
        }
    }
}
