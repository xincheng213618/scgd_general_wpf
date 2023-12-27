using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Device.FileServer
{
    /// <summary>
    /// DeviceImageControl.xaml 的交互逻辑
    /// </summary>
    public partial class DeviceFileServerControl : UserControl
    {
        public DeviceFileServer DeviceFileServer { get; set; }
        public FileServerService DService { get => DeviceFileServer.DeviceService; }

        public bool IsCanEdit { get; set; }
        public DeviceFileServerControl(DeviceFileServer device, bool isCanEdit = true)
        {
            DeviceFileServer = device;
            IsCanEdit = isCanEdit;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (!IsCanEdit) ButtonEdit.Visibility = IsCanEdit ? Visibility.Visible : Visibility.Collapsed;
            this.DataContext = DeviceFileServer;
        }

        private void ServiceCache_Click(object sender, RoutedEventArgs e)
        {
            DService.CacheClear();
        }
    }
}
