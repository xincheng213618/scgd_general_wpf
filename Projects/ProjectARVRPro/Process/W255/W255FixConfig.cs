#pragma warning disable

using ColorVision.Common.MVVM;
using ProjectARVRPro;
using ProjectARVRPro.Fix;
using ProjectARVRPro.Process.W255;
using System.ComponentModel;

namespace ProjectARVRPro.Process.W255
{
    public class W255FixConfig : ViewModelBase, IFixConfig
    {
        [Category("W255")]
        public double W255LuminanceUniformity { get => _W255LuminanceUniformity; set { _W255LuminanceUniformity = value; OnPropertyChanged(); } }
        private double _W255LuminanceUniformity = 1;
        [Category("W255")]
        public double W255ColorUniformity { get => _W255ColorUniformity; set { _W255ColorUniformity = value; OnPropertyChanged(); } }
        private double _W255ColorUniformity = 1;
        [Category("W255")]
        public double W255CenterLunimance { get => _W255CenterLunimance; set { _W255CenterLunimance = value; OnPropertyChanged(); } }
        private double _W255CenterLunimance = 1;
        [Category("W255")]
        public double W255CenterCIE1931ChromaticCoordinatesx { get => _W255CenterCIE1931ChromaticCoordinatesx; set { _W255CenterCIE1931ChromaticCoordinatesx = value; OnPropertyChanged(); } }
        private double _W255CenterCIE1931ChromaticCoordinatesx = 1;
        [Category("W255")]
        public double W255CenterCIE1931ChromaticCoordinatesy { get => _W255CenterCIE1931ChromaticCoordinatesy; set { _W255CenterCIE1931ChromaticCoordinatesy = value; OnPropertyChanged(); } }
        private double _W255CenterCIE1931ChromaticCoordinatesy = 1;
        [Category("W255")]
        public double W255CenterCIE1976ChromaticCoordinatesu { get => _W255CenterCIE1976ChromaticCoordinatesu; set { _W255CenterCIE1976ChromaticCoordinatesu = value; OnPropertyChanged(); } }
        private double _W255CenterCIE1976ChromaticCoordinatesu = 1;
        [Category("W255")]
        public double W255CenterCIE1976ChromaticCoordinatesv { get => _W255CenterCIE1976ChromaticCoordinatesv; set { _W255CenterCIE1976ChromaticCoordinatesv = value; OnPropertyChanged(); } }
        private double _W255CenterCIE1976ChromaticCoordinatesv = 1;
        [Category("W255")]
        public double BlackCenterCorrelatedColorTemperature { get => _BlackCenterCorrelatedColorTemperature; set { _BlackCenterCorrelatedColorTemperature = value; OnPropertyChanged(); } }
        private double _BlackCenterCorrelatedColorTemperature = 1;
    }

}