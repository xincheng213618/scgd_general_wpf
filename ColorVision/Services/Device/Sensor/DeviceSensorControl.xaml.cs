using ColorVision.Device.PG;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Device.Sensor
{
    /// <summary>
    /// DevicePGControl.xaml 的交互逻辑
    /// </summary>
    public partial class DeviceSensorControl : UserControl
    {
        public DeviceSensor DeviceSensor { get; set; }

        public MQTTSensor Service { get => DeviceSensor.DeviceService; }

        public bool IsCanEdit { get; set; }
        public DeviceSensorControl(DeviceSensor deviceSensor, bool isCanEdit = true)
        {
            DeviceSensor = deviceSensor;
            IsCanEdit = isCanEdit;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (!IsCanEdit) ButtonEdit.Visibility = IsCanEdit ? Visibility.Visible : Visibility.Collapsed;
            this.DataContext = DeviceSensor;

            ComboxSensorType.ItemsSource = from e1 in Enum.GetValues(typeof(CommunicateType)).Cast<CommunicateType>()
                                          select new KeyValuePair<CommunicateType, string>(e1, e1.ToString()); 

        }

    }
}
