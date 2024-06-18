using System;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Motor
{
    /// <summary>
    /// InfoSMU.xaml 的交互逻辑
    /// </summary>
    public partial class InfoMotor : UserControl
    {
        public DeviceMotor Device { get; set; }
        public InfoMotor(DeviceMotor deviceMotor)
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
