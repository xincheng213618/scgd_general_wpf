#pragma warning disable
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.LargeFlow;
using ColorVision.UI;
using Newtonsoft.Json.Linq;
using ProjectARVRPro;
using ProjectARVRPro;
using ProjectARVRPro.Process.W25;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace ProjectARVRPro.Process.W25
{
    public class W25RecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("W25")]
        public double W25CenterLunimanceMin { get => _W25CenterLunimanceMin; set { _W25CenterLunimanceMin = value; OnPropertyChanged(); } }
        private double _W25CenterLunimanceMin = 0;
        [Category("W25")]
        public double W25CenterLunimanceMax { get => _W25CenterLunimanceMax; set { _W25CenterLunimanceMax = value; OnPropertyChanged(); } }
        private double _W25CenterLunimanceMax = 0;

        [Category("W25")]
        public double W25CenterCIE1931ChromaticCoordinatesxMin { get => _W25CenterCIE1931ChromaticCoordinatesxMin; set { _W25CenterCIE1931ChromaticCoordinatesxMin = value; OnPropertyChanged(); } }
        private double _W25CenterCIE1931ChromaticCoordinatesxMin = 0;
        [Category("W25")]
        public double W25CenterCIE1931ChromaticCoordinatesxMax { get => _W25CenterCIE1931ChromaticCoordinatesxMax; set { _W25CenterCIE1931ChromaticCoordinatesxMax = value; OnPropertyChanged(); } }
        private double _W25CenterCIE1931ChromaticCoordinatesxMax = 0;
        [Category("W25")]
        public double W25CenterCIE1931ChromaticCoordinatesyMin { get => _W25CenterCIE1931ChromaticCoordinatesyMin; set { _W25CenterCIE1931ChromaticCoordinatesyMin = value; OnPropertyChanged(); } }
        private double _W25CenterCIE1931ChromaticCoordinatesyMin = 0;
        [Category("W25")]
        public double W25CenterCIE1931ChromaticCoordinatesyMax { get => _W25CenterCIE1931ChromaticCoordinatesyMax; set { _W25CenterCIE1931ChromaticCoordinatesyMax = value; OnPropertyChanged(); } }
        private double _W25CenterCIE1931ChromaticCoordinatesyMax = 0;
        [Category("W25")]
        public double W25CenterCIE1976ChromaticCoordinatesuMin { get => _W25CenterCIE1976ChromaticCoordinatesuMin; set { _W25CenterCIE1976ChromaticCoordinatesuMin = value; OnPropertyChanged(); } }
        private double _W25CenterCIE1976ChromaticCoordinatesuMin = 0;
        [Category("W25")]
        public double W25CenterCIE1976ChromaticCoordinatesuMax { get => _W25CenterCIE1976ChromaticCoordinatesuMax; set { _W25CenterCIE1976ChromaticCoordinatesuMax = value; OnPropertyChanged(); } }
        private double _W25CenterCIE1976ChromaticCoordinatesuMax = 0;
        [Category("W25")]
        public double W25CenterCIE1976ChromaticCoordinatesvMin { get => _W25CenterCIE1976ChromaticCoordinatesvMin; set { _W25CenterCIE1976ChromaticCoordinatesvMin = value; OnPropertyChanged(); } }
        private double _W25CenterCIE1976ChromaticCoordinatesvMin = 0;
        [Category("W25")]
        public double W25CenterCIE1976ChromaticCoordinatesvMax { get => _W25CenterCIE1976ChromaticCoordinatesvMax; set { _W25CenterCIE1976ChromaticCoordinatesvMax = value; OnPropertyChanged(); } }
        private double _W25CenterCIE1976ChromaticCoordinatesvMax = 0;
    }
}