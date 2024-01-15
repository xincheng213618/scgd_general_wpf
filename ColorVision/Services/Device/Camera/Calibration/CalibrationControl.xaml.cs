using System.Windows.Controls;
using System.Windows.Input;
using ColorVision.Device.Camera;

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



        public CalibrationControl()
        {
            InitializeComponent();
            this.CalibrationParam = new CalibrationParam();
            this.DataContext = CalibrationParam;
        }

        public DeviceCamera DeviceCamera { get; set; }
        public CalibrationControl(DeviceCamera DeviceCamera)
        {
            this.DeviceCamera = DeviceCamera;
            InitializeComponent();
            this.CalibrationParam = new CalibrationParam();
            this.DataContext = CalibrationParam;
        }

        public CalibrationControl(DeviceCamera DeviceCamera,CalibrationParam calibrationParam)
        {
            this.DeviceCamera = DeviceCamera;
            InitializeComponent();
            this.CalibrationParam = calibrationParam;
            this.DataContext = CalibrationParam;
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

                string key = comboBox.Text;
                if (DeviceCamera.Config.Calibration.TryGetValue(key, out var colorVisionVCalibratioItems))
                {
                    foreach (var item in colorVisionVCalibratioItems)
                    {
                        switch (item.CalibrationType)
                        {
                            case cvColorVision.CalibrationType.DarkNoise:
                                CalibrationParam.Normal.DarkNoise.FilePath = item.Title;
                                CalibrationParam.Normal.DarkNoise.IsSelected = true;
                                break;
                            case cvColorVision.CalibrationType.DefectWPoint:
                                CalibrationParam.Normal.DefectPoint.FilePath = item.Title;
                                CalibrationParam.Normal.DefectPoint.IsSelected = true;
                                break;
                            case cvColorVision.CalibrationType.DefectBPoint:
                                CalibrationParam.Normal.DefectPoint.FilePath = item.Title;
                                CalibrationParam.Normal.DefectPoint.IsSelected = true;
                                break;
                            case cvColorVision.CalibrationType.DefectPoint:
                                CalibrationParam.Normal.DefectPoint.FilePath = item.Title;
                                CalibrationParam.Normal.DefectPoint.IsSelected = true;
                                break;
                            case cvColorVision.CalibrationType.DSNU:
                                CalibrationParam.Normal.DSNU.FilePath = item.Title;
                                CalibrationParam.Normal.DSNU.IsSelected = true;
                                break;
                            case cvColorVision.CalibrationType.Uniformity:
                                CalibrationParam.Normal.Uniformity.FilePath = item.Title;
                                CalibrationParam.Normal.Uniformity.IsSelected = true;

                                break;
                            case cvColorVision.CalibrationType.Luminance:
                                CalibrationParam.Color.Luminance.FilePath = item.Title;
                                CalibrationParam.Color.Luminance.IsSelected = true;
                                break;
                            case cvColorVision.CalibrationType.LumOneColor:
                                CalibrationParam.Color.LumOneColor.FilePath = item.Title;
                                CalibrationParam.Color.LumOneColor.IsSelected = true;
                                break;
                            case cvColorVision.CalibrationType.LumFourColor:
                                CalibrationParam.Color.LumFourColor.FilePath = item.Title;
                                CalibrationParam.Color.LumFourColor.IsSelected = true;
                                break;
                            case cvColorVision.CalibrationType.LumMultiColor:
                                CalibrationParam.Color.LumMultiColor.FilePath = item.Title;
                                CalibrationParam.Color.LumMultiColor.IsSelected = true;
                                break;
                            case cvColorVision.CalibrationType.LumColor:
                                CalibrationParam.Color.Luminance.FilePath = item.Title;
                                CalibrationParam.Color.Luminance.IsSelected = true;
                                break;
                            case cvColorVision.CalibrationType.Distortion:
                                CalibrationParam.Normal.Distortion.FilePath = item.Title;
                                CalibrationParam.Normal.Distortion.IsSelected = true;
                                break;
                            case cvColorVision.CalibrationType.ColorShift:
                                CalibrationParam.Normal.ColorShift.FilePath = item.Title;
                                CalibrationParam.Normal.ColorShift.IsSelected = true;
                                break;
                            case cvColorVision.CalibrationType.Empty_Num:
                                break;
                            default:
                                break;
                        }
   
                    }
                }

            }
        }

        private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            CalibrationEdit CalibrationEdit = new CalibrationEdit();
            CalibrationEdit.Show();
        }
    }


}
