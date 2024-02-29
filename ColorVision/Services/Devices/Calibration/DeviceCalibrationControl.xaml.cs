using ColorVision.Services.Devices.Camera.Calibrations;
using ColorVision.Services.Templates;
using ColorVision.Settings;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Devices.Calibration
{
    /// <summary>
    /// DeviceSMUControl.xaml 的交互逻辑
    /// </summary>
    public partial class DeviceCalibrationControl : UserControl, IDisposable
    {
        public DeviceCalibration Device { get; set; }
        public ServiceManager ServiceControl { get; set; }

        public bool IsCanEdit { get; set; }

        public DeviceCalibrationControl(DeviceCalibration deviceCalibration, bool isCanEdit = true)
        {
            this.Device = deviceCalibration;
            IsCanEdit = isCanEdit;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = this.Device;
            if (!IsCanEdit) ButtonEdit.Visibility = IsCanEdit ? Visibility.Visible : Visibility.Collapsed;
            if (IsCanEdit)
            {
                UserControl userControl = Device.GetEditControl();
                if (userControl.Parent is Panel grid)
                    grid.Children.Remove(userControl);
                MQTTEditContent.Children.Add(userControl);
            }
        }
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        private void MenuItem_Template(object sender, RoutedEventArgs e)
        {
            if (sender is Control control)
            {
                SoftwareConfig SoftwareConfig = ConfigHandler.GetInstance().SoftwareConfig;
                WindowTemplate windowTemplate;
                if (SoftwareConfig.IsUseMySql && !SoftwareConfig.MySqlControl.IsConnect)
                {
                    MessageBox.Show("数据库连接失败，请先连接数据库在操作", "ColorVision");
                    return;
                }
                switch (control.Tag?.ToString() ?? string.Empty)
                {
                    case "Calibration":
                        CalibrationControl calibration = Device.CalibrationParams.Count == 0 ? new CalibrationControl(Device) : new CalibrationControl(Device, Device.CalibrationParams[0].Value);
                        windowTemplate = new WindowTemplate(TemplateType.Calibration, calibration, Device);
                        windowTemplate.Owner = Window.GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                }
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            CalibrationEdit CalibrationEdit = new CalibrationEdit(Device);
            CalibrationEdit.Show();
        }
    }
}
