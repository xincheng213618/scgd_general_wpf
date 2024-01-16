using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;
using ColorVision.Device.Camera;
using MQTTMessageLib.Camera;

namespace ColorVision.Services.Device.Camera.Calibrations
{
    /// <summary>
    /// CalibrationControl.xaml 的交互逻辑
    /// </summary>
    public partial class CalibrationControl : UserControl
    {
        private bool IsFirst = true;
        public CalibrationParam CalibrationParam { get => _CalibrationParam; set { _CalibrationParam = value; IsFirst = true; } }
        private CalibrationParam _CalibrationParam;

        public DeviceCamera DeviceCamera { get; set; }

        public CalibrationControl(DeviceCamera DeviceCamera)
        {
            this.DeviceCamera = DeviceCamera;
            InitializeComponent();
            this.CalibrationParam = new CalibrationParam();
            this.DataContext = CalibrationParam;
        }

        public Dictionary<string, List<ColorVisionVCalibratioItem>> CalibrationModeList { get; set; }

        public CalibrationControl(DeviceCamera DeviceCamera,CalibrationParam calibrationParam)
        {
            this.DeviceCamera = DeviceCamera;
            InitializeComponent();
            this.CalibrationParam = calibrationParam;
            this.DataContext = CalibrationParam;
        }

        public void Initializedsss(DeviceCamera DeviceCamera, CalibrationParam calibrationParam)
        {
            this.DeviceCamera = DeviceCamera;
            this.CalibrationParam = calibrationParam;
            this.DataContext = CalibrationParam;


            ComboBoxList.ItemsSource = DeviceCamera.Config.CalibrationRsourcesGroups;
            ComboBoxList.DisplayMemberPath = "Key";
            ComboBoxList.SelectedValuePath = "Value";

        }

        private void UserControl_Initialized(object sender, System.EventArgs e)
        {
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NativeMethods.Keyboard.PressKey(0x09);
            }
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsFirst)
            {
                IsFirst = false;
                return;
            }

            if (sender is ComboBox comboBox)
            {
                CalibrationParam.Normal.DarkNoise.FilePath = string.Empty;
                CalibrationParam.Normal.DefectPoint.FilePath = string.Empty;
                CalibrationParam.Normal.DSNU.FilePath = string.Empty;
                CalibrationParam.Normal.Distortion.FilePath = string.Empty;
                CalibrationParam.Normal.ColorShift.FilePath = string.Empty;
                CalibrationParam.Normal.Uniformity.FilePath = string.Empty;

                CalibrationParam.Color.Luminance.FilePath = string.Empty;
                CalibrationParam.Color.LumFourColor.FilePath = string.Empty;
                CalibrationParam.Color.LumMultiColor.FilePath = string.Empty;
                CalibrationParam.Color.LumOneColor.FilePath = string.Empty;

                CalibrationParam.Normal.DarkNoise.IsSelected = false;
                CalibrationParam.Normal.DefectPoint.IsSelected = false;
                CalibrationParam.Normal.DSNU.IsSelected = false;
                CalibrationParam.Normal.Distortion.IsSelected = false;
                CalibrationParam.Normal.ColorShift.IsSelected = false;
                CalibrationParam.Normal.Uniformity.IsSelected = false;

                CalibrationParam.Color.Luminance.IsSelected = false;
                CalibrationParam.Color.LumFourColor.IsSelected = false;
                CalibrationParam.Color.LumMultiColor.IsSelected = false;
                CalibrationParam.Color.LumOneColor.IsSelected = false;

                if (comboBox.SelectedValue is CalibrationRsourcesGroup calibrationRsourcesGroup)
                {
                    CalibrationParam.Normal.DarkNoise.FilePath = calibrationRsourcesGroup.DarkNoise;
                    CalibrationParam.Normal.DefectPoint.FilePath = calibrationRsourcesGroup.DefectPoint;
                    CalibrationParam.Normal.DSNU.FilePath = calibrationRsourcesGroup.DSNU;
                    CalibrationParam.Normal.Distortion.FilePath = calibrationRsourcesGroup.Distortion;
                    CalibrationParam.Normal.ColorShift.FilePath = calibrationRsourcesGroup.ColorShift;
                    CalibrationParam.Normal.Uniformity.FilePath = calibrationRsourcesGroup.Uniformity;
                    CalibrationParam.Color.Luminance.FilePath = calibrationRsourcesGroup.Luminance;
                    CalibrationParam.Color.LumFourColor.FilePath = calibrationRsourcesGroup.LumFourColor;
                    CalibrationParam.Color.LumMultiColor.FilePath = calibrationRsourcesGroup.LumMultiColor;
                    CalibrationParam.Color.LumOneColor.FilePath = calibrationRsourcesGroup.LumOneColor;
                }

            }
        }

        private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            CalibrationEdit CalibrationEdit = new CalibrationEdit(DeviceCamera);
            CalibrationEdit.Show();
        }


    }


}
