using System;
using System.Windows;
using System.Windows.Controls;
using ColorVision.Common.Utilities;

namespace ColorVision.Engine.Services.Devices.FileServer
{
    /// <summary>
    /// DeviceImageControl.xaml 的交互逻辑
    /// </summary>
    public partial class InfoFileServer : UserControl
    {
        public DeviceFileServer DeviceFileServer { get; set; }
        public MQTTFileServer DService { get => DeviceFileServer.MQTTFileServer; }
        public InfoFileServer(DeviceFileServer device)
        {
            DeviceFileServer = device;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = DeviceFileServer;
        }
    }
}
