using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Devices.Algorithm
{
    /// <summary>
    /// DeviceAlgorithmControl.xaml 的交互逻辑s
    /// </summary>
    public partial class DeviceAlgorithmControl : UserControl
    {
        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.MQTTService; }

        public bool IsCanEdit { get; set; }

        public DeviceAlgorithmControl(DeviceAlgorithm device, bool isCanEdit = true)
        {
            this.Device = device;
            IsCanEdit = isCanEdit;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (!IsCanEdit) ButtonEdit.Visibility = IsCanEdit ? Visibility.Visible : Visibility.Collapsed;
            this.DataContext = Device;
        }

        private void ServiceCache_Click(object sender, RoutedEventArgs e)
        {
            DService.CacheClear();
        }
    }
}
