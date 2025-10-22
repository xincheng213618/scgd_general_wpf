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
        public double W25CenterLunimance { get => _W25CenterLunimance; set { _W25CenterLunimance = value; OnPropertyChanged(); } }
        private double _W25CenterLunimance = 1;
        [Category("W25")]
        public double W25CenterCIE1931ChromaticCoordinatesx { get => _W25CenterCIE1931ChromaticCoordinatesx; set { _W25CenterCIE1931ChromaticCoordinatesx = value; OnPropertyChanged(); } }
        private double _W25CenterCIE1931ChromaticCoordinatesx = 1;
        [Category("W25")]
        public double W25CenterCIE1931ChromaticCoordinatesy { get => _W25CenterCIE1931ChromaticCoordinatesy; set { _W25CenterCIE1931ChromaticCoordinatesy = value; OnPropertyChanged(); } }
        private double _W25CenterCIE1931ChromaticCoordinatesy = 1;
        [Category("W25")]
        public double W25CenterCIE1976ChromaticCoordinatesu { get => _W25CenterCIE1976ChromaticCoordinatesu; set { _W25CenterCIE1976ChromaticCoordinatesu = value; OnPropertyChanged(); } }
        private double _W25CenterCIE1976ChromaticCoordinatesu = 1;
        [Category("W25")]
        public double W25CenterCIE1976ChromaticCoordinatesv { get => _W25CenterCIE1976ChromaticCoordinatesv; set { _W25CenterCIE1976ChromaticCoordinatesv = value; OnPropertyChanged(); } }
        private double _W25CenterCIE1976ChromaticCoordinatesv = 1;

    }

}