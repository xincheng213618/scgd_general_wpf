using ColorVision.Common.MVVM;
using ProjectARVRPro.Fix;
using System.ComponentModel;

namespace ProjectARVRPro.Process.Blue
{
    public class BlueFixConfig : ViewModelBase, IFixConfig
    {
        [Category("Blue")]
        public double LuminanceUniformity { get => _LuminanceUniformity; set { _LuminanceUniformity = value; OnPropertyChanged(); } }
        private double _LuminanceUniformity = 1;
        [Category("Blue")]
        public double ColorUniformity { get => _ColorUniformity; set { _ColorUniformity = value; OnPropertyChanged(); } }
        private double _ColorUniformity = 1;
        [Category("Blue")]
        public double CenterLunimance { get => _CenterLunimance; set { _CenterLunimance = value; OnPropertyChanged(); } }
        private double _CenterLunimance = 1;
        [Category("Blue")]
        public double CenterCIE1931ChromaticCoordinatesx { get => _CenterCIE1931ChromaticCoordinatesx; set { _CenterCIE1931ChromaticCoordinatesx = value; OnPropertyChanged(); } }
        private double _CenterCIE1931ChromaticCoordinatesx = 1;
        [Category("Blue")]
        public double CenterCIE1931ChromaticCoordinatesy { get => _CenterCIE1931ChromaticCoordinatesy; set { _CenterCIE1931ChromaticCoordinatesy = value; OnPropertyChanged(); } }
        private double _CenterCIE1931ChromaticCoordinatesy = 1;
        [Category("Blue")]
        public double CenterCIE1976ChromaticCoordinatesu { get => _CenterCIE1976ChromaticCoordinatesu; set { _CenterCIE1976ChromaticCoordinatesu = value; OnPropertyChanged(); } }
        private double _CenterCIE1976ChromaticCoordinatesu = 1;
        [Category("Blue")]
        public double CenterCIE1976ChromaticCoordinatesv { get => _CenterCIE1976ChromaticCoordinatesv; set { _CenterCIE1976ChromaticCoordinatesv = value; OnPropertyChanged(); } }
        private double _CenterCIE1976ChromaticCoordinatesv = 1;
        [Category("Blue")]
        public double BlackCenterCorrelatedColorTemperature { get => _BlackCenterCorrelatedColorTemperature; set { _BlackCenterCorrelatedColorTemperature = value; OnPropertyChanged(); } }
        private double _BlackCenterCorrelatedColorTemperature = 1;
    }

}