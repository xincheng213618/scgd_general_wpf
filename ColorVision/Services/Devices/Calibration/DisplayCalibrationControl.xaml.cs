using ColorVision.Common.Utilities;
using ColorVision.Services.Devices.Camera.Calibrations;
using ColorVision.Services.Interfaces;
using ColorVision.Services.Msg;
using ColorVision.Themes;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Services.Devices.Calibration
{


    /// <summary>
    /// DisplaySMUControl.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayCalibrationControl : UserControl, IDisPlayControl
    {

        public DeviceCalibration Device { get; set; }
        private MQTTCalibration DeviceService { get => Device.DeviceService;  }

        public DisplayCalibrationControl(DeviceCalibration device)
        {
            this.Device = device;
            InitializeComponent();
            this.PreviewMouseDown += UserControl_PreviewMouseDown;

        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Device;
        }

        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; DisPlayBorder.BorderBrush = value ? ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#5649B0" : "#A79CF1") : ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#EAEAEA" : "#151515");  } }
        private bool _IsSelected;

        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (this.Parent is StackPanel stackPanel)
            {
                if (stackPanel.Tag is IDisPlayControl disPlayControl)
                    disPlayControl.IsSelected = false;
                stackPanel.Tag = this;
                IsSelected = true;
            }
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
