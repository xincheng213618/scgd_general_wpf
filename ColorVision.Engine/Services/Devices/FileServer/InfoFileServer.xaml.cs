using System;
using System.Windows;
using System.Windows.Controls;
using ColorVision.Common.Utilities;

namespace ColorVision.Services.Devices.FileServer
{
    /// <summary>
    /// DeviceImageControl.xaml 的交互逻辑
    /// </summary>
    public partial class InfoFileServer : UserControl
    {
        public DeviceFileServer DeviceFileServer { get; set; }
        public MQTTFileServer DService { get => DeviceFileServer.MQTTFileServer; }

        public bool IsCanEdit { get; set; }
        public InfoFileServer(DeviceFileServer device, bool isCanEdit = true)
        {
            DeviceFileServer = device;
            IsCanEdit = isCanEdit;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (!IsCanEdit) ButtonEdit.Visibility = IsCanEdit ? Visibility.Visible : Visibility.Collapsed;
            DataContext = DeviceFileServer;
        }
    }
}
