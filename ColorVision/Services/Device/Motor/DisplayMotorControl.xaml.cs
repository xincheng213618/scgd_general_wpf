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
    }
}
