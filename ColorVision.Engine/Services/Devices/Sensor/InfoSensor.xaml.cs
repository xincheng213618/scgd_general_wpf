using ColorVision.Services.Devices.PG;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Devices.Sensor
{
    /// <summary>
    /// InfoPG.xaml 的交互逻辑
    /// </summary>
    public partial class InfoSensor : UserControl
    {
        public DeviceSensor DeviceSensor { get; set; }

        public MQTTSensor Service { get => DeviceSensor.DeviceService; }

        public bool IsCanEdit { get; set; }
        public InfoSensor(DeviceSensor deviceSensor, bool isCanEdit = true)
        {
            DeviceSensor = deviceSensor;
            IsCanEdit = isCanEdit;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (!IsCanEdit) ButtonEdit.Visibility = IsCanEdit ? Visibility.Visible : Visibility.Collapsed;

            DataContext = DeviceSensor;
        }

    }
}
