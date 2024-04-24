using ColorVision.Common.MVVM;
using cvColorVision;
using System.Collections.Generic;

namespace ColorVision.Services.PhyCameras.Templates
{
    public class ZipCalibrationGroup : ViewModelBase
    {
        public List<ZipCalibrationItem> ZipCalibrationItems { get; set; } = new List<ZipCalibrationItem>();

        public double Gain { get => _Gain; set { _Gain = value; NotifyPropertyChanged(); } }
        private double _Gain;

        public double ExpTime { get => _ExpTime; set { _ExpTime = value; NotifyPropertyChanged(); } }
        private double _ExpTime = 10;

        public double Aperturein { get => _Aperturein; set { _Aperturein = value; NotifyPropertyChanged(); } }
        private double _Aperturein;

        public double ND { get => _ND; set { _ND = value; NotifyPropertyChanged(); } }
        private double _ND;

        public double ShotType { get => _ShotType; set { _ShotType = value; NotifyPropertyChanged(); } }
        private double _ShotType;

        public double Focallength { get => _Focallength; set { _Focallength = value; NotifyPropertyChanged(); } }
        private double _Focallength;

        public double GetImgMode { get => _GetImgMode; set { _GetImgMode = value; NotifyPropertyChanged(); } }
        private double _GetImgMode;

        public double ImgBpp { get => _ImgBpp; set { _ImgBpp = value; NotifyPropertyChanged(); } }
        private double _ImgBpp;
    }


    public class ZipCalibrationItem : ViewModelBase
    {
        public CalibrationType CalibrationType { get; set; }
        public string FileName { get; set; }
        public string Title { get; set; }

        public double Gain { get => _Gain; set { _Gain = value; NotifyPropertyChanged(); } }
        private double _Gain;

        public double ExpTime { get => _ExpTime; set { _ExpTime = value; NotifyPropertyChanged(); } }
        private double _ExpTime = 10;

        public double Aperturein { get => _Aperturein; set { _Aperturein = value; NotifyPropertyChanged(); } }
        private double _Aperturein;

        public double ND { get => _ND; set { _ND = value; NotifyPropertyChanged(); } }
        private double _ND;

        public double ShotType { get => _ShotType; set { _ShotType = value; NotifyPropertyChanged(); } }
        private double _ShotType;

        public double Focallength { get => _Focallength; set { _Focallength = value; NotifyPropertyChanged(); } }
        private double _Focallength;

        public double GetImgMode { get => _GetImgMode; set { _GetImgMode = value; NotifyPropertyChanged(); } }
        private double _GetImgMode;

        public double ImgBpp { get => _ImgBpp; set { _ImgBpp = value; NotifyPropertyChanged(); } }
        private double _ImgBpp;


    }
}
