
using ColorVision.Common.MVVM;
using ProjectARVRPro.Fix;
using System.ComponentModel;

namespace ProjectARVRPro.Process.W255
{
    public class W255FixConfig : ViewModelBase, IFixConfig
    {
        [Category("W255")]
        public double LuminanceUniformity { get => _LuminanceUniformity; set { _LuminanceUniformity = value; OnPropertyChanged(); } }
        private double _LuminanceUniformity = 1;
        [Category("W255")]
        public double ColorUniformity { get => _ColorUniformity; set { _ColorUniformity = value; OnPropertyChanged(); } }
        private double _ColorUniformity = 1;
        [Category("W255")]
        public double CenterLunimance { get => _CenterLunimance; set { _CenterLunimance = value; OnPropertyChanged(); } }
        private double _CenterLunimance = 1;
        [Category("W255")]
        public double CenterCIE1931ChromaticCoordinatesx { get => _CenterCIE1931ChromaticCoordinatesx; set { _CenterCIE1931ChromaticCoordinatesx = value; OnPropertyChanged(); } }
        private double _CenterCIE1931ChromaticCoordinatesx = 1;
        [Category("W255")]
        public double CenterCIE1931ChromaticCoordinatesy { get => _CenterCIE1931ChromaticCoordinatesy; set { _CenterCIE1931ChromaticCoordinatesy = value; OnPropertyChanged(); } }
        private double _CenterCIE1931ChromaticCoordinatesy = 1;
        [Category("W255")]
        public double CenterCIE1976ChromaticCoordinatesu { get => _CenterCIE1976ChromaticCoordinatesu; set { _CenterCIE1976ChromaticCoordinatesu = value; OnPropertyChanged(); } }
        private double _CenterCIE1976ChromaticCoordinatesu = 1;
        [Category("W255")]
        public double CenterCIE1976ChromaticCoordinatesv { get => _CenterCIE1976ChromaticCoordinatesv; set { _CenterCIE1976ChromaticCoordinatesv = value; OnPropertyChanged(); } }
        private double _CenterCIE1976ChromaticCoordinatesv = 1;
        [Category("W255")]
        public double CenterCorrelatedColorTemperature { get => _BlackCenterCorrelatedColorTemperature; set { _BlackCenterCorrelatedColorTemperature = value; OnPropertyChanged(); } }
        private double _BlackCenterCorrelatedColorTemperature = 1;

        [Category("FOV")]
        public double W51HorizontalFieldOfViewAngle { get => _W51HorizontalFieldOfViewAngle; set { _W51HorizontalFieldOfViewAngle = value; OnPropertyChanged(); } }
        private double _W51HorizontalFieldOfViewAngle = 1;
        [Category("FOV")]
        public double W51VerticalFieldOfViewAngle { get => _W51VerticalFieldOfViewAngle; set { _W51VerticalFieldOfViewAngle = value; OnPropertyChanged(); } }
        private double _W51VerticalFieldOfViewAngle = 1;
        [Category("FOV")]
        public double W51DiagonalFieldOfViewAngle { get => _W51DiagonalFieldOfViewAngle; set { _W51DiagonalFieldOfViewAngle = value; OnPropertyChanged(); } }
        private double _W51DiagonalFieldOfViewAngle = 1;
    }

}