using ColorVision.Common.MVVM;
using ProjectLUX.Recipe;
using System.ComponentModel;

namespace ProjectLUX.Process.Green
{
    public class GreenRecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("Green")]
        [DisplayName("Luminance uniformity(%)")]
        public RecipeBase LuminanceUniformity { get => _LuminanceUniformity; set { _LuminanceUniformity = value; OnPropertyChanged(); } }
        private RecipeBase _LuminanceUniformity = new RecipeBase(0.75, 0);

        [Category("Green")]
        public RecipeBase ColorUniformity { get => _ColorUniformity; set { _ColorUniformity = value; OnPropertyChanged(); } }
        private RecipeBase _ColorUniformity = new RecipeBase(0, 0.02);

        [Category("Green")]
        [DisplayName("Center Correlated Color Temperature(K)")]
        public RecipeBase CenterCorrelatedColorTemperature { get => _CenterCorrelatedColorTemperature; set { _CenterCorrelatedColorTemperature = value; OnPropertyChanged(); } }
        private RecipeBase _CenterCorrelatedColorTemperature = new RecipeBase(6000, 7000);

        [Category("Green")]
        public RecipeBase CenterLunimance { get => _CenterLunimance; set { _CenterLunimance = value; OnPropertyChanged(); } }
        private RecipeBase _CenterLunimance = new RecipeBase(0, 0);

        [Category("Green")]
        public RecipeBase CenterCIE1931ChromaticCoordinatesx { get => _CenterCIE1931ChromaticCoordinatesx; set { _CenterCIE1931ChromaticCoordinatesx = value; OnPropertyChanged(); } }
        private RecipeBase _CenterCIE1931ChromaticCoordinatesx = new RecipeBase(0, 0);

        [Category("Green")]
        public RecipeBase CenterCIE1931ChromaticCoordinatesy { get => _CenterCIE1931ChromaticCoordinatesy; set { _CenterCIE1931ChromaticCoordinatesy = value; OnPropertyChanged(); } }
        private RecipeBase _CenterCIE1931ChromaticCoordinatesy = new RecipeBase(0, 0);

        [Category("Green")]
        public RecipeBase CenterCIE1976ChromaticCoordinatesu { get => _CenterCIE1976ChromaticCoordinatesu; set { _CenterCIE1976ChromaticCoordinatesu = value; OnPropertyChanged(); } }
        private RecipeBase _CenterCIE1976ChromaticCoordinatesu = new RecipeBase(0, 0);

        [Category("Green")]
        public RecipeBase CenterCIE1976ChromaticCoordinatesv { get => _CenterCIE1976ChromaticCoordinatesv; set { _CenterCIE1976ChromaticCoordinatesv = value; OnPropertyChanged(); } }
        private RecipeBase _CenterCIE1976ChromaticCoordinatesv = new RecipeBase(0, 0);
    }
}