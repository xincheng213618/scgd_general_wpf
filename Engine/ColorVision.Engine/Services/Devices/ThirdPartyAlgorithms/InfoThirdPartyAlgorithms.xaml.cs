using ColorVision.UI;
using System;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms
{
    /// <summary>
    /// InfoAlgorithm.xaml 的交互逻辑s
    /// </summary>
    public partial class InfoThirdPartyAlgorithms : UserControl
    {
        public DeviceThirdPartyAlgorithms Device { get; set; }
        public ConfigThirdPartyAlgorithms EditConfig { get; set; }
        public MQTTThirdPartyAlgorithms DService { get => Device.DService; }

        public InfoThirdPartyAlgorithms(DeviceThirdPartyAlgorithms device)
        {
            Device = device;
            InitializeComponent();

        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;
            PropertyEditorHelper.GenCommand(Device, CommandGrid);

        }
    }
}
