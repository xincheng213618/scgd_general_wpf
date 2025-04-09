using ColorVision.UI;
using System;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.SMU
{
    /// <summary>
    /// InfoSMU.xaml 的交互逻辑
    /// </summary>
    public partial class InfoSMU : UserControl
    {
        public DeviceSMU DeviceSMU { get; set; }
        public InfoSMU(DeviceSMU device)
        {
            DeviceSMU = device;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = DeviceSMU;
            PropertyEditorHelper.GenCommand(DeviceSMU, CommandGrid);

        }
    }
}
