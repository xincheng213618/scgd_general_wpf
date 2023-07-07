using ColorVision.MVVM;
using Newtonsoft.Json;
using System;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Xml.Linq;
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
                        case "DefectWPoint":
                            CalibrationParam.FileNameDefectWPoint = FileName;
                            break;
                        case "DefectBPoint":
                            CalibrationParam.FileNameDefectBPoint = FileName;
                            break;
                        case "Distortion":
                            CalibrationParam.FileNameDistortion = FileName;
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


    public class CalibrationParam: ParamBase
    {
        [JsonProperty("FileName_DarkNoise")]
        public string FileNameDarkNoise { get => _FileNameDarkNoise; set { _FileNameDarkNoise = value; NotifyPropertyChanged(); } }
        private string _FileNameDarkNoise;

        [JsonProperty("Selected_DarkNoise")]
        public bool SelectedDarkNoise { get => _SelectedDarkNoise; set { _SelectedDarkNoise = value; NotifyPropertyChanged(); } }
        private bool _SelectedDarkNoise;

        [JsonProperty("FileName_DSNU")]
        public string FileNameDSNU { get => _FileNameDSNU; set { _FileNameDSNU = value; NotifyPropertyChanged(); } }
        private string _FileNameDSNU;

        [JsonProperty("Selected_DSNU")]
        public bool SelectedDSNU { get => _SelectedDSNU; set { _SelectedDSNU = value; NotifyPropertyChanged(); } }
        private bool _SelectedDSNU;

        [JsonProperty("FileName_Distortion")]
        public string FileNameDistortion { get => _FileNameDistortion; set { _FileNameDistortion = value; NotifyPropertyChanged(); } }
        private string _FileNameDistortion;

        [JsonProperty("Selected_Distortion")]
        public bool SelectedDistortion { get => _SelectedDistortion; set { _SelectedDistortion = value; NotifyPropertyChanged(); } }
        private bool _SelectedDistortion;

        [JsonProperty("FileName_DefectWPoint")]
        public string FileNameDefectWPoint { get => _FileNameDefectWPoint; set { _FileNameDefectWPoint = value; NotifyPropertyChanged(); } }
        private string _FileNameDefectWPoint;

        [JsonProperty("Selected_DefectWPoint")]
        public bool SelectedDefectWPoint { get => _SelectedDefectWPoint; set { _SelectedDefectWPoint = value; NotifyPropertyChanged(); } }
        private bool _SelectedDefectWPoint;

        [JsonProperty("FileName_DefectBPoint")]
        public string FileNameDefectBPoint { get => _FileNameDefectBPoint; set { _FileNameDefectBPoint = value; NotifyPropertyChanged(); } }
        private string _FileNameDefectBPoint;

       [JsonProperty("Selected_DefectBPoint")]
        public bool SelectedDefectBPoint { get => _SelectedDefectBPoint; set { _SelectedDefectBPoint = value; NotifyPropertyChanged(); } }
        private bool _SelectedDefectBPoint;

        [JsonProperty("FileName_Luminance")]
        public string FileNameLuminance { get => _FileNameLuminance; set { _FileNameLuminance = value; NotifyPropertyChanged(); } }
        private string _FileNameLuminance;

        [JsonProperty("Selected_Luminance")]
        public bool SelectedLuminance { get => _SelectedLuminance; set { _SelectedLuminance = value; NotifyPropertyChanged(); } }
        private bool _SelectedLuminance;

        [JsonProperty("FileName_ColorOne")]
        public string FileNameColorOne { get => _FileNameColorOne; set { _FileNameColorOne = value; NotifyPropertyChanged(); } }
        private string _FileNameColorOne;

        [JsonProperty("Selected_ColorOne")]
        public bool SelectedColorOne { get => _SelectedColorOne; set { _SelectedColorOne = value; NotifyPropertyChanged(); } }
        private bool _SelectedColorOne;

        [JsonProperty("FileName_ColorFour")]
        public string FileNameColorFour { get => _FileNameColorFour; set { _FileNameColorFour = value; NotifyPropertyChanged(); } }
        private string _FileNameColorFour;

        [JsonProperty("Selected_ColorFour")]
        public bool SelectedColorFour { get => _SelectedColorFour; set { _SelectedColorFour = value; NotifyPropertyChanged(); } }
        private bool _SelectedColorFour;

        [JsonProperty("FileName_ColorMulti")]
        public string FileNameColorMulti { get => _FileNameColorMulti; set { _FileNameColorMulti = value; NotifyPropertyChanged(); } }
        private string _FileNameColorMulti;

        [JsonProperty("Selected_ColorMulti")]
        public bool SelectedColorMulti { get => _SelectedColorMulti; set { _SelectedColorMulti = value; NotifyPropertyChanged(); } }
        private bool _SelectedColorMulti;

        [JsonProperty("FileName_Uniformity_Y")]
        public string FileNameUniformityY { get => _FileNameUniformityY; set { _FileNameUniformityY = value; NotifyPropertyChanged(); } }
        private string _FileNameUniformityY;

        [JsonProperty("Selected_Uniformity_Y")]
        public bool SelectedUniformityY { get => _SelectedUniformityY; set { _SelectedUniformityY = value; NotifyPropertyChanged(); } }
        private bool _SelectedUniformityY;

        [JsonProperty("FileName_Uniformity_Z")]
        public string FileNameUniformityZ { get => _FileNameUniformityZ; set { _FileNameUniformityZ = value; NotifyPropertyChanged(); } }
        private string _FileNameUniformityZ;

        [JsonProperty("Selected_Uniformity_Z")]
        public bool SelectedUniformityZ { get => _SelectedUniformityZ; set { _SelectedUniformityZ = value; NotifyPropertyChanged(); } }
        private bool _SelectedUniformityZ;

        [JsonProperty("FileName_Uniformity_X")]
        public string FileNameUniformityX { get => _FileNameUniformityX; set { _FileNameUniformityX = value; NotifyPropertyChanged(); } }
        private string _FileNameUniformityX;

        [JsonProperty("Selected_Uniformity_X")]
        public bool SelectedUniformityX { get => _SelectedUniformityX; set { _SelectedUniformityX = value; NotifyPropertyChanged(); } }
        private bool _SelectedUniformityX;

        public CalibrationParam() : base(-1)
        {
        }
    }


}
