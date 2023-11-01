using ColorVision.Device;
using ColorVision.Services.Msg;
using ColorVision.Templates;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Device.Calibration
{
    /// <summary>
    /// SMUDisplayControl.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayCalibrationControl : UserControl
    {

        public DeviceCalibration Device { get; set; }
        private DeviceServiceCalibration DeviceService { get => Device.DeviceService;  }

        public DisplayCalibrationControl(DeviceCalibration device)
        {
            this.Device = device;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Device;


            ComboxCalibrationTemplate.ItemsSource = TemplateControl.GetInstance().CalibrationParams;
            ComboxCalibrationTemplate.SelectedIndex = 0;

        }

        private void Calibration_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (ComboxCalibrationTemplate.SelectedValue is CalibrationParam param)
                {
                    MsgRecord msgRecord = DeviceService.Calibration(param);
                    Helpers.SendCommand(button, msgRecord);

                }
            }
        }

        private void Open_File(object sender, RoutedEventArgs e)
        {

        }
    }
}
