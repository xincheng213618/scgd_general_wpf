using ColorVision.Services.Device.Camera.Calibrations;
using ColorVision.Services.Msg;
using ColorVision.Templates;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Device.Calibration
{
    /// <summary>
    /// DisplaySMUControl.xaml 的交互逻辑
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

        }

        private void Calibration_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (ComboxCalibrationTemplate.SelectedValue is CalibrationParam param)
                {
                    MsgRecord msgRecord = DeviceService.Calibration(param, ImageFile.Text, Device.Config.ExpTimeR, Device.Config.ExpTimeG, Device.Config.ExpTimeB );
                    Helpers.SendCommand(button, msgRecord);

                }
            }
        }

        private void Open_File(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png,*.tif) | *.jpg; *.jpeg; *.png;*.tif";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ImageFile.Text = openFileDialog.FileName;
            }
        }
    }
}
