using ColorVision.UI;
using System;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.FileServer
{
    /// <summary>
    /// DeviceImageControl.xaml 的交互逻辑
    /// </summary>
    public partial class InfoFileServer : UserControl
    {
        public DeviceFileServer Device { get; set; }
        public InfoFileServer(DeviceFileServer device)
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
