using System;
using System.Windows;
using System.Windows.Controls;
using HandyControl.Tools.Extension;

namespace ColorVision.Services.Algorithm
{
    /// <summary>
    /// DeviceAlgorithmControl.xaml 的交互逻辑s
    /// </summary>
    public partial class DeviceAlgorithmControl : UserControl
    {
        public DeviceAlgorithm Device { get; set; }

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
    }
}
