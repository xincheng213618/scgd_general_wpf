using System.Windows.Controls;
using System.Windows.Input;
using ColorVision.Device.Camera;
using ColorVision.Services.Device.Camera.Calibrations;

namespace ColorVision.Services.Device.Camera.Calibrations
{
    /// <summary>
    /// CalibrationControl.xaml 的交互逻辑
    /// </summary>
    public partial class CalibrationControl : UserControl
    {
        public CalibrationParam CalibrationParam { get; set; }

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
                string key = comboBox.Text;
                if (DeviceCamera.Config.Calibration.TryGetValue(key, out var colorVisionVCalibratioItems))
                {


                    foreach (var item in colorVisionVCalibratioItems)
                    {
                        switch (item.CalibrationType)
                        {
                            case cvColorVision.CalibrationType.DarkNoise:
                                CalibrationParam.Normal.DarkNoise.FilePath = item.Title;
                                break;
                            case cvColorVision.CalibrationType.DefectWPoint:
                                CalibrationParam.Normal.DefectPoint.FilePath = item.Title;
                                break;
                            case cvColorVision.CalibrationType.DefectBPoint:
                                CalibrationParam.Normal.DefectPoint.FilePath = item.Title;
                                break;
                            case cvColorVision.CalibrationType.DefectPoint:
                                CalibrationParam.Normal.DefectPoint.FilePath = item.Title;
                                break;
                            case cvColorVision.CalibrationType.DSNU:
                                CalibrationParam.Normal.DSNU.FilePath = item.Title;
                                break;
                            case cvColorVision.CalibrationType.Uniformity:
                                CalibrationParam.Normal.Uniformity.FilePath = item.Title;
                                break;
                            case cvColorVision.CalibrationType.Luminance:
                                CalibrationParam.Color.Luminance.FilePath = item.Title;
                                break;
                            case cvColorVision.CalibrationType.LumOneColor:
                                CalibrationParam.Color.LumOneColor.FilePath = item.Title;
                                break;
                            case cvColorVision.CalibrationType.LumFourColor:
                                CalibrationParam.Color.LumFourColor.FilePath = item.Title;
                                break;
                            case cvColorVision.CalibrationType.LumMultiColor:
                                CalibrationParam.Color.LumMultiColor.FilePath = item.Title;
                                break;
                            case cvColorVision.CalibrationType.LumColor:
                                CalibrationParam.Color.Luminance.FilePath = item.Title;
                                break;
                            case cvColorVision.CalibrationType.Distortion:
                                CalibrationParam.Normal.Distortion.FilePath = item.Title;
                                break;
                            case cvColorVision.CalibrationType.ColorShift:
                                CalibrationParam.Normal.ColorShift.FilePath = item.Title;
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
    }


}
