using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Types;
using cvColorVision;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.PhyCameras.Group
{
    public class ZipCalibrationGroup : ViewModelBase
    {
        public List<ZipCalibrationItem> List { get; set; } = new List<ZipCalibrationItem>();

        public double Gain { get => _Gain; set { _Gain = value; OnPropertyChanged(); } }
        private double _Gain;

        public double ExpTime { get => _ExpTime; set { _ExpTime = value; OnPropertyChanged(); } }
        private double _ExpTime = 10;

        public double Aperturein { get => _Aperturein; set { _Aperturein = value; OnPropertyChanged(); } }
        private double _Aperturein;

        public double ND { get => _ND; set { _ND = value; OnPropertyChanged(); } }
        private double _ND;

        public double ShotType { get => _ShotType; set { _ShotType = value; OnPropertyChanged(); } }
        private double _ShotType;

        public double Focallength { get => _Focallength; set { _Focallength = value; OnPropertyChanged(); } }
        private double _Focallength;

        public double GetImgMode { get => _GetImgMode; set { _GetImgMode = value; OnPropertyChanged(); } }
        private double _GetImgMode;

        public double ImgBpp { get => _ImgBpp; set { _ImgBpp = value; OnPropertyChanged(); } }
        private double _ImgBpp;
    }


    public class ZipCalibrationItem : ViewModelBase
    {
        public CalibrationType CalibrationType { get; set; }

        public string FileName { get; set; }
        public string Title { get; set; }

        internal CalibrationPackageResourceKey ResourceKey => new(
            CalibrationType.ToResouceType(),
            Title,
            FileName?.Replace('\\', '/').ToUpperInvariant() ?? string.Empty);

        public double Gain { get => _Gain; set { _Gain = value; OnPropertyChanged(); } }
        private double _Gain;

        public double ExpTime { get => _ExpTime; set { _ExpTime = value; OnPropertyChanged(); } }
        private double _ExpTime = 10;

        public double Aperturein { get => _Aperturein; set { _Aperturein = value; OnPropertyChanged(); } }
        private double _Aperturein;

        public double ND { get => _ND; set { _ND = value; OnPropertyChanged(); } }
        private double _ND;

        public double ShotType { get => _ShotType; set { _ShotType = value; OnPropertyChanged(); } }
        private double _ShotType;

        public double Focallength { get => _Focallength; set { _Focallength = value; OnPropertyChanged(); } }
        private double _Focallength;

        public double GetImgMode { get => _GetImgMode; set { _GetImgMode = value; OnPropertyChanged(); } }
        private double _GetImgMode;

        public double ImgBpp { get => _ImgBpp; set { _ImgBpp = value; OnPropertyChanged(); } }
        private double _ImgBpp;


    }

    internal readonly record struct CalibrationPackageResourceKey(ServiceTypes Type, string Title, string FileName);
}
