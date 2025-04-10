using ColorVision.UI;
using System;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Camera
{
    /// <summary>
    /// InfoPG.xaml 的交互逻辑
    /// </summary>
    public partial class InfoCamera : UserControl
    {
        public DeviceCamera Device { get; set; }

        public MQTTCamera DService { get => Device.DService; }

        public InfoCamera(DeviceCamera deviceCamera)
        {
            Device = deviceCamera;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;
            PropertyEditorHelper.GenCommand(Device, CommandGrid);
        }
    }
}
