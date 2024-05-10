using ColorVision.Common.Sorts;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Camera;
using ColorVision.Services.PhyCameras.Dao;
using ColorVision.Services.PhyCameras.Templates;
using ColorVision.Services.Templates;
using ColorVision.Settings;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ColorVision.Services.PhyCameras
{
    /// <summary>
    /// InfoPG.xaml 的交互逻辑
    /// </summary>
    public partial class InfoPhyCamera : UserControl
    {
        public PhyCamera Device { get; set; }

        public bool IsCanEdit { get; set; }
        public InfoPhyCamera(PhyCamera deviceCamera,bool isCanEdit =true)
        {
            Device = deviceCamera;
            IsCanEdit = isCanEdit;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (!IsCanEdit) ButtonEdit.Visibility = IsCanEdit ? Visibility.Visible : Visibility.Collapsed;
            DataContext = Device;
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

                        var ITemplate = new TemplateCalibrationParam() { Device = Device, TemplateParams = Device.CalibrationParams, CalibrationControl = calibration, Code = ModMasterType.Calibration, Title = "校正参数设置" };
                        windowTemplate = new WindowTemplate(ITemplate);
                        windowTemplate.Owner = Window.GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                }
            }
        }

        private void TextBlock_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                NativeMethods.Clipboard.SetText(textBlock.Text);
                MessageBox.Show(textBlock.Text);
            }
        }

        private void UniformGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is UniformGrid uniformGrid)
            {
                uniformGrid.Columns = uniformGrid.ActualWidth > 0 ? (int)(uniformGrid.ActualWidth / 200) : 1;
                uniformGrid.Rows = (int)Math.Ceiling(uniformGrid.Children.Count / (double)uniformGrid.Columns);
            }
        }
    }
}
