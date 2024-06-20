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

        public InfoSMU(DeviceSMU mqttDeviceSMU)
        {
            MQTTDeviceSMU = mqttDeviceSMU;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = MQTTDeviceSMU;
        }



        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

    }
}
