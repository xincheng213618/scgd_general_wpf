using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Templates
{
    /// <summary>
    /// Calibration.xaml 的交互逻辑
    /// </summary>
    public partial class Calibration : UserControl
    {

        public CalibrationParam CalibrationParam { get; set; }
        public Calibration()
        {
            InitializeComponent();
            this.CalibrationParam = new CalibrationParam();
            this.DataContext = CalibrationParam;
        }
        public Calibration(CalibrationParam calibrationParam)
        {
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
    }


}
