using System;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.FlowDevice
{
    /// <summary>
    /// InfoSMU.xaml 的交互逻辑
    /// </summary>
    public partial class InfoFlowDevice : UserControl
    {
        public DeviceFlowDevice Device { get; set; }
        public InfoFlowDevice(DeviceFlowDevice deviceMotor)
        {
            Device = deviceMotor;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;
        }

    }
}
