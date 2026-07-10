using ColorVision.Common.MVVM;
using ProjectARVRPro.Recipe;
using System.ComponentModel;

namespace ProjectARVRPro.Process.RGB.LuminanceChromaticity
{
    public class LuminanceChromaticityRecipeConfig : ViewModelBase, IRecipeConfig
    {
        private const string CategoryName = "亮色度测试";

        [Category(CategoryName)]
        [DisplayName("Luminance uniformity(%)")]
        public RecipeBase LuminanceUniformity { get => _LuminanceUniformity; set { _LuminanceUniformity = value; OnPropertyChanged(); } }
        private RecipeBase _LuminanceUniformity = new(0.75, 0);

        [Category(CategoryName)]
        public RecipeBase ColorUniformity { get => _ColorUniformity; set { _ColorUniformity = value; OnPropertyChanged(); } }
        private RecipeBase _ColorUniformity = new(0, 0.02);

        [Category(CategoryName)]
        [DisplayName("Center Correlated Color Temperature(K)")]
        public RecipeBase CenterCorrelatedColorTemperature { get => _CenterCorrelatedColorTemperature; set { _CenterCorrelatedColorTemperature = value; OnPropertyChanged(); } }
        private RecipeBase _CenterCorrelatedColorTemperature = new(6000, 7000);

        [Category(CategoryName)]
        public RecipeBase CenterLuminance { get => _CenterLuminance; set { _CenterLuminance = value; OnPropertyChanged(); } }
        private RecipeBase _CenterLuminance = new(0, 0);

        [Category(CategoryName)]
        public RecipeBase CenterCIE1931ChromaticCoordinatesx { get => _CenterCIE1931ChromaticCoordinatesx; set { _CenterCIE1931ChromaticCoordinatesx = value; OnPropertyChanged(); } }
        private RecipeBase _CenterCIE1931ChromaticCoordinatesx = new(0, 0);

        [Category(CategoryName)]
        public RecipeBase CenterCIE1931ChromaticCoordinatesy { get => _CenterCIE1931ChromaticCoordinatesy; set { _CenterCIE1931ChromaticCoordinatesy = value; OnPropertyChanged(); } }
        private RecipeBase _CenterCIE1931ChromaticCoordinatesy = new(0, 0);

        [Category(CategoryName)]
        public RecipeBase CenterCIE1976ChromaticCoordinatesu { get => _CenterCIE1976ChromaticCoordinatesu; set { _CenterCIE1976ChromaticCoordinatesu = value; OnPropertyChanged(); } }
        private RecipeBase _CenterCIE1976ChromaticCoordinatesu = new(0, 0);

        [Category(CategoryName)]
        public RecipeBase CenterCIE1976ChromaticCoordinatesv { get => _CenterCIE1976ChromaticCoordinatesv; set { _CenterCIE1976ChromaticCoordinatesv = value; OnPropertyChanged(); } }
        private RecipeBase _CenterCIE1976ChromaticCoordinatesv = new(0, 0);
    }
}
