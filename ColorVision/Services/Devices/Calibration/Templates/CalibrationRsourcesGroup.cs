using ColorVision.MVVM;

namespace ColorVision.Services.Devices.Calibration.Templates
{
    public class CalibrationRsourcesGroup : ViewModelBase
    {
        public string Title { get => _Title; set { _Title = value; NotifyPropertyChanged(); } }
        private string _Title;

        public CalibrationRsourcesGroup()
        {

        }

        public string Luminance { get => _Luminance; set { _Luminance = value; NotifyPropertyChanged(); } }
        private string _Luminance;

        public string LumOneColor { get => _LumOneColor; set { _LumOneColor = value; NotifyPropertyChanged(); } }
        private string _LumOneColor;

        public string LumFourColor { get => _LumFourColor; set { _LumFourColor = value; NotifyPropertyChanged(); } }
        private string _LumFourColor;

        public string LumMultiColor { get => _LumMultiColor; set { _LumMultiColor = value; NotifyPropertyChanged(); } }
        private string _LumMultiColor;


        public string DarkNoise { get => _DarkNoise; set { _DarkNoise = value; NotifyPropertyChanged(); } }
        private string _DarkNoise;
        public string DefectPoint { get => _DefectPoint; set { _DefectPoint = value; NotifyPropertyChanged(); } }
        private string _DefectPoint;
        public string DSNU { get => _DSNU; set { _DSNU = value; NotifyPropertyChanged(); } }
        private string _DSNU;
        public string Uniformity { get => _Uniformity; set { _Uniformity = value; NotifyPropertyChanged(); } }
        private string _Uniformity;

        public string Distortion { get => _Distortion; set { _Distortion = value; NotifyPropertyChanged(); } }
        private string _Distortion;

        public string ColorShift { get => _ColorShift; set { _ColorShift = value; NotifyPropertyChanged(); } }
        private string _ColorShift;
    }


}
