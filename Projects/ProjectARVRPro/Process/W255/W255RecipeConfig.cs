#pragma warning disable
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.LargeFlow;
using ColorVision.UI;
using Newtonsoft.Json.Linq;
using ProjectARVRPro;
using ProjectARVRPro;
using ProjectARVRPro.Process.W255;
using ProjectARVRPro.Recipe;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace ProjectARVRPro.Process.W255
{
    public class W255RecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("W255")]
        [DisplayName("Luminance uniformity(%)")]
        public RecipeBase W255LuminanceUniformity { get => _W255LuminanceUniformity; set { _W255LuminanceUniformity = value; OnPropertyChanged(); } }
        private RecipeBase _W255LuminanceUniformity = new RecipeBase(0.75, 0);

        [Category("W255")]
        public RecipeBase W255ColorUniformity { get => _W255ColorUniformity; set { _W255ColorUniformity = value; OnPropertyChanged(); } }
        private RecipeBase _W255ColorUniformity = new RecipeBase(0, 0.02);

        [Category("W255")]
        [DisplayName("Center Correlated Color Temperature(K)")]
        public RecipeBase CenterCorrelatedColorTemperature { get => _CenterCorrelatedColorTemperature; set { _CenterCorrelatedColorTemperature = value; OnPropertyChanged(); } }
        private RecipeBase _CenterCorrelatedColorTemperature = new RecipeBase(6000, 7000);

        [Category("W255")]
        public RecipeBase W255CenterLunimance { get => _W255CenterLunimance; set { _W255CenterLunimance = value; OnPropertyChanged(); } }
        private RecipeBase _W255CenterLunimance = new RecipeBase(0, 0);

        [Category("W255")]
        public RecipeBase W255CenterCIE1931ChromaticCoordinatesx { get => _W255CenterCIE1931ChromaticCoordinatesx; set { _W255CenterCIE1931ChromaticCoordinatesx = value; OnPropertyChanged(); } }
        private RecipeBase _W255CenterCIE1931ChromaticCoordinatesx = new RecipeBase(0, 0);

        [Category("W255")]
        public RecipeBase W255CenterCIE1931ChromaticCoordinatesy { get => _W255CenterCIE1931ChromaticCoordinatesy; set { _W255CenterCIE1931ChromaticCoordinatesy = value; OnPropertyChanged(); } }
        private RecipeBase _W255CenterCIE1931ChromaticCoordinatesy = new RecipeBase(0, 0);

        [Category("W255")]
        public RecipeBase W255CenterCIE1976ChromaticCoordinatesu { get => _W255CenterCIE1976ChromaticCoordinatesu; set { _W255CenterCIE1976ChromaticCoordinatesu = value; OnPropertyChanged(); } }
        private RecipeBase _W255CenterCIE1976ChromaticCoordinatesu = new RecipeBase(0, 0);

        [Category("W255")]
        public RecipeBase W255CenterCIE1976ChromaticCoordinatesv { get => _W255CenterCIE1976ChromaticCoordinatesv; set { _W255CenterCIE1976ChromaticCoordinatesv = value; OnPropertyChanged(); } }
        private RecipeBase _W255CenterCIE1976ChromaticCoordinatesv = new RecipeBase(0, 0);
    }
}