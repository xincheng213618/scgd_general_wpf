using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Template
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

        public void SetCalibrationParam(CalibrationParam calibrationParam)
        {
            this.CalibrationParam = calibrationParam;
            this.DataContext = CalibrationParam;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
                openFileDialog.InitialDirectory = Environment.CurrentDirectory;
                openFileDialog.Filter = "DAT|*.dat||";
                openFileDialog.RestoreDirectory = true;
                openFileDialog.FilterIndex = 1;
                if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string FileName = openFileDialog.FileName;
                    if (FileName.Contains(Environment.CurrentDirectory))
                    {
                        FileName =FileName.Replace(Environment.CurrentDirectory +"\\","");
                    }
                    switch (button.Tag)
                    {
                        case "DarkNoise":
                            CalibrationParam.FileNameDarkNoise = FileName;
                            break;
                        case "DSNU":
                            CalibrationParam.FileNameDSNU = FileName;
                            break;
                        case "DefectPoint":
                            CalibrationParam.FileNameDefectPoint = FileName;
                            break;
                        case "Distortion":
                            CalibrationParam.FileNameDistortion = FileName;
                            break;
                        case "ColorShift":
                            CalibrationParam.FileNameColorShift = FileName;
                            break;
                        case "Luminance":
                            CalibrationParam.FileNameLuminance = FileName;
                            break;
                        case "ColorOne":
                            CalibrationParam.FileNameColorOne = FileName;
                            break;
                        case "ColorFour":
                            CalibrationParam.FileNameColorFour = FileName;
                            break;
                        case "ColorMulti":
                            CalibrationParam.FileNameColorMulti = FileName;
                            break;
                        case "UniformityY":
                            CalibrationParam.FileNameUniformityY = FileName;
                            break;
                        case "UniformityX":
                            CalibrationParam.FileNameUniformityX = FileName;
                            break;
                        case "UniformityZ":
                            CalibrationParam.FileNameUniformityZ = FileName;
                            break;

                    }
                }

            }
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
