#pragma warning disable

using ColorVision.Common.MVVM;
using ProjectARVRPro;
using ProjectARVRPro.Fix;
using ProjectARVRPro.Process.W25;
using System.ComponentModel;

namespace ProjectARVRPro.Process.W25
{
    public class W25FixConfig : ViewModelBase, IFixConfig
    {
        [Category("W25")]
        public double CenterLunimance { get => _CenterLunimance; set { _CenterLunimance = value; OnPropertyChanged(); } }
        private double _CenterLunimance = 1;
        [Category("W25")]
        public double CenterCIE1931ChromaticCoordinatesx { get => _CenterCIE1931ChromaticCoordinatesx; set { _CenterCIE1931ChromaticCoordinatesx = value; OnPropertyChanged(); } }
        private double _CenterCIE1931ChromaticCoordinatesx = 1;
        [Category("W25")]
        public double CenterCIE1931ChromaticCoordinatesy { get => _CenterCIE1931ChromaticCoordinatesy; set { _CenterCIE1931ChromaticCoordinatesy = value; OnPropertyChanged(); } }
        private double _CenterCIE1931ChromaticCoordinatesy = 1;
        [Category("W25")]
        public double CenterCIE1976ChromaticCoordinatesu { get => _CenterCIE1976ChromaticCoordinatesu; set { _CenterCIE1976ChromaticCoordinatesu = value; OnPropertyChanged(); } }
        private double _CenterCIE1976ChromaticCoordinatesu = 1;
        [Category("W25")]
        public double CenterCIE1976ChromaticCoordinatesv { get => _CenterCIE1976ChromaticCoordinatesv; set { _CenterCIE1976ChromaticCoordinatesv = value; OnPropertyChanged(); } }
        private double _CenterCIE1976ChromaticCoordinatesv = 1;

    }

}