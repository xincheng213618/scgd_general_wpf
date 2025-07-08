using ColorVision.UI;
using System;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.CfwPort
{
    /// <summary>
    /// InfoSMU.xaml 的交互逻辑
    /// </summary>
    public partial class InfoCfwPort : UserControl
    {
        public DeviceCfwPort Device { get; set; }
        public InfoCfwPort(DeviceCfwPort deviceCfwPort)
        {
            Device = deviceCfwPort;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;
            PropertyEditorHelper.GenCommand(Device, CommandGrid);
        }
    }
}
