using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.SMU
{
    /// <summary>
    /// InfoSMU.xaml 的交互逻辑
    /// </summary>
    public partial class InfoSMU : UserControl, IDisposable
    {
        public DeviceSMU MQTTDeviceSMU { get; set; }
        public ServiceManager ServiceControl { get; set; }

        public bool IsCanEdit { get; set; }
        public InfoSMU(DeviceSMU mqttDeviceSMU, bool isCanEdit = true)
        {
            MQTTDeviceSMU = mqttDeviceSMU;
            IsCanEdit = isCanEdit;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (!IsCanEdit) ButtonEdit.Visibility = IsCanEdit ? Visibility.Visible : Visibility.Collapsed;
            DataContext = MQTTDeviceSMU;
        }



        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

    }
}
