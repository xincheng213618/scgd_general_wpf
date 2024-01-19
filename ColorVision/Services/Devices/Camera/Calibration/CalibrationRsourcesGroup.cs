#pragma warning disable CS8603,CS0649
using ColorVision.MVVM;
using System.Collections.Generic;

namespace ColorVision.Services.Devices.Camera.Calibrations
{
    public class CalibrationRsourcesGroup : ViewModelBase
    {
        public string Title { get => _Title; set { _Title = value; NotifyPropertyChanged(); } }
        private string _Title;

        public CalibrationRsourcesGroup()
        {
            DarkNoiseList = CalibrationRsourceService.GetInstance().DarkNoiseList;
            DefectPointList = CalibrationRsourceService.GetInstance().DefectPointList;
            DSNUList = CalibrationRsourceService.GetInstance().DSNUList;
            UniformityList = CalibrationRsourceService.GetInstance().UniformityList;
            DistortionList = CalibrationRsourceService.GetInstance().DistortionList;
            ColorShiftList = CalibrationRsourceService.GetInstance().ColorShiftList;

            LuminanceList = CalibrationRsourceService.GetInstance().LuminanceList;
            LumOneColorList = CalibrationRsourceService.GetInstance().LumOneColorList;
            LumFourColorList = CalibrationRsourceService.GetInstance().LumFourColorList;
            LumMultiColorList = CalibrationRsourceService.GetInstance().LumMultiColorList;
        }


        public List<string> DarkNoiseList { get; set; }
        public List<string> DefectPointList { get; set; }
        public List<string> DSNUList { get; set; }
        public List<string> UniformityList { get; set; }
        public List<string> DistortionList { get; set; }
        public List<string> ColorShiftList { get; set; }
        public List<string> LuminanceList { get; set; }
        public List<string> LumOneColorList { get; set; }
        public List<string> LumFourColorList { get; set; }
        public List<string> LumMultiColorList { get; set; }


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
