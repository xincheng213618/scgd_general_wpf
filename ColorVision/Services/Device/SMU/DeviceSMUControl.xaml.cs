using ColorVision.Services;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Device.SMU
{
    /// <summary>
    /// DeviceSMUControl.xaml 的交互逻辑
    /// </summary>
    public partial class DeviceSMUControl : UserControl, IDisposable
    {
        public DeviceSMU MQTTDeviceSMU { get; set; }
        public ServiceManager ServiceControl { get; set; }

        public bool IsCanEdit { get; set; }
        public DeviceSMUControl(DeviceSMU mqttDeviceSMU, bool isCanEdit = true)
        {
            this.MQTTDeviceSMU = mqttDeviceSMU;
            IsCanEdit = isCanEdit;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ButtonEdit.Visibility = IsCanEdit ? Visibility.Visible : Visibility.Collapsed;
            this.DataContext = this.MQTTDeviceSMU;
        }



        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

    }
}
