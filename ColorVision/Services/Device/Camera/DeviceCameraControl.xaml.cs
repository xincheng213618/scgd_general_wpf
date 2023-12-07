using ColorVision.Extension;
using ColorVision.Services.Device;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Device.Camera
{
    /// <summary>
    /// DevicePGControl.xaml 的交互逻辑
    /// </summary>
    public partial class DeviceCameraControl : UserControl
    {
        public DeviceCamera DeviceCamera { get; set; }

        public DeviceServiceCamera Service { get => DeviceCamera.DeviceService; }

        public bool IsCanEdit { get; set; }
        public DeviceCameraControl(DeviceCamera mQTTDeviceCamera,bool isCanEdit =true)
        {
            DeviceCamera = mQTTDeviceCamera;
            IsCanEdit = isCanEdit;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (!IsCanEdit) ButtonEdit.Visibility = IsCanEdit ? Visibility.Visible : Visibility.Collapsed;
            this.DataContext = DeviceCamera;
            if (IsCanEdit)
            {
                UserControl userControl = DeviceCamera.GetEditControl();
                if (userControl.Parent is Panel grid)
                    grid.Children.Remove(userControl);
                MQTTEditContent.Children.Add(userControl);
            }
        }
    }
}
