#pragma warning disable
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.LargeFlow;
using ColorVision.UI;
using Newtonsoft.Json.Linq;
using ProjectARVRPro;
using ProjectARVRPro;
using ProjectARVRPro.Process.W255;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace ProjectARVRPro.Process.W255
{
    public class W255RecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("W255")]
        [DisplayName("Luminance uniformity(%) Min")]
        public double W255LuminanceUniformityMin { get => _W255LuminanceUniformityMin; set { _W255LuminanceUniformityMin = value; OnPropertyChanged(); } }
        private double _W255LuminanceUniformityMin = 0.75;
        [Category("W255")]
        [DisplayName("Luminance uniformity(%) Max")]
        public double W255LuminanceUniformityMax { get => _W255LuminanceUniformityMax; set { _W255LuminanceUniformityMax = value; OnPropertyChanged(); } }
        private double _W255LuminanceUniformityMax;
        [Category("W255")]
        public double W255ColorUniformityMin { get => _W255ColorUniformityMin; set { _W255ColorUniformityMin = value; OnPropertyChanged(); } }
        private double _W255ColorUniformityMin;
        [Category("W255")]
        public double W255ColorUniformityMax { get => _W255ColorUniformityMax; set { _W255ColorUniformityMax = value; OnPropertyChanged(); } }
        private double _W255ColorUniformityMax = 0.02;

        [Category("W255")]
        [DisplayName("Center Correlated Color Temperature(K) Min")]
        public double CenterCorrelatedColorTemperatureMin { get => _CenterCorrelatedColorTemperatureMin; set { _CenterCorrelatedColorTemperatureMin = value; OnPropertyChanged(); } }
        private double _CenterCorrelatedColorTemperatureMin = 6000;
        [Category("W255")]
        [DisplayName("Center Correlated Color Temperature(K) Max")]
        public double CenterCorrelatedColorTemperatureMax { get => _CenterCorrelatedColorTemperatureMax; set { _CenterCorrelatedColorTemperatureMax = value; OnPropertyChanged(); } }
        private double _CenterCorrelatedColorTemperatureMax = 7000;

        [Category("W255")]
        public double W255CenterLunimanceMin { get => _W255CenterLunimanceMin; set { _W255CenterLunimanceMin = value; OnPropertyChanged(); } }
        private double _W255CenterLunimanceMin = 0;
        [Category("W255")]
        public double W255CenterLunimanceMax { get => _W255CenterLunimanceMax; set { _W255CenterLunimanceMax = value; OnPropertyChanged(); } }
        private double _W255CenterLunimanceMax = 0;
        [Category("W255")]
        public double W255CenterCIE1931ChromaticCoordinatesxMin { get => _W255CenterCIE1931ChromaticCoordinatesxMin; set { _W255CenterCIE1931ChromaticCoordinatesxMin = value; OnPropertyChanged(); } }
        private double _W255CenterCIE1931ChromaticCoordinatesxMin = 0;
        [Category("W255")]

        public double W255CenterCIE1931ChromaticCoordinatesxMax { get => _W255CenterCIE1931ChromaticCoordinatesxMax; set { _W255CenterCIE1931ChromaticCoordinatesxMax = value; OnPropertyChanged(); } }
        private double _W255CenterCIE1931ChromaticCoordinatesxMax = 0;
        [Category("W255")]
        public double W255CenterCIE1931ChromaticCoordinatesyMin { get => _W255CenterCIE1931ChromaticCoordinatesyMin; set { _W255CenterCIE1931ChromaticCoordinatesyMin = value; OnPropertyChanged(); } }
        private double _W255CenterCIE1931ChromaticCoordinatesyMin = 0;
        [Category("W255")]
        public double W255CenterCIE1931ChromaticCoordinatesyMax { get => _W255CenterCIE1931ChromaticCoordinatesyMax; set { _W255CenterCIE1931ChromaticCoordinatesyMax = value; OnPropertyChanged(); } }
        private double _W255CenterCIE1931ChromaticCoordinatesyMax = 0;
        [Category("W255")]
        public double W255CenterCIE1976ChromaticCoordinatesuMin { get => _W255CenterCIE1976ChromaticCoordinatesuMin; set { _W255CenterCIE1976ChromaticCoordinatesuMin = value; OnPropertyChanged(); } }
        private double _W255CenterCIE1976ChromaticCoordinatesuMin = 0;
        [Category("W255")]
        public double W255CenterCIE1976ChromaticCoordinatesuMax { get => _W255CenterCIE1976ChromaticCoordinatesuMax; set { _W255CenterCIE1976ChromaticCoordinatesuMax = value; OnPropertyChanged(); } }
        private double _W255CenterCIE1976ChromaticCoordinatesuMax = 0;
        [Category("W255")]
        public double W255CenterCIE1976ChromaticCoordinatesvMin { get => _W255CenterCIE1976ChromaticCoordinatesvMin; set { _W255CenterCIE1976ChromaticCoordinatesvMin = value; OnPropertyChanged(); } }
        private double _W255CenterCIE1976ChromaticCoordinatesvMin = 0;
        [Category("W255")]
        public double W255CenterCIE1976ChromaticCoordinatesvMax { get => _W255CenterCIE1976ChromaticCoordinatesvMax; set { _W255CenterCIE1976ChromaticCoordinatesvMax = value; OnPropertyChanged(); } }
        private double _W255CenterCIE1976ChromaticCoordinatesvMax = 0;
    }
}