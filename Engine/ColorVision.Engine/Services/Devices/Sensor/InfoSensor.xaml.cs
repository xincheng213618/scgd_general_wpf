using ColorVision.UI;
using System;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Sensor
{
    /// <summary>
    /// InfoPG.xaml 的交互逻辑
    /// </summary>
    public partial class InfoSensor : UserControl
    {
        public DeviceSensor DeviceSensor { get; set; }
        public InfoSensor(DeviceSensor deviceSensor)
        {
            DeviceSensor = deviceSensor;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = DeviceSensor;
            PropertyEditorHelper.GenCommand(DeviceSensor, CommandGrid);
        }
    }
}
