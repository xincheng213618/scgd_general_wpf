
using ColorVision.Common.MVVM;
using ProjectLUX.Recipe;
using System.ComponentModel;

namespace ProjectLUX.Process.W255
{
    public class W255RecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("W255")]
        [DisplayName("Luminance uniformity(%)")]
        public RecipeBase LuminanceUniformity { get => _LuminanceUniformity; set { _LuminanceUniformity = value; OnPropertyChanged(); } }
        private RecipeBase _LuminanceUniformity = new RecipeBase(0.75, 0);

        [Category("W255")]
        public RecipeBase ColorUniformity { get => _ColorUniformity; set { _ColorUniformity = value; OnPropertyChanged(); } }
        private RecipeBase _ColorUniformity = new RecipeBase(0, 0.02);

        [Category("W255")]
        [DisplayName("Center Correlated Color Temperature(K)")]
        public RecipeBase CenterCorrelatedColorTemperature { get => _CenterCorrelatedColorTemperature; set { _CenterCorrelatedColorTemperature = value; OnPropertyChanged(); } }
        private RecipeBase _CenterCorrelatedColorTemperature = new RecipeBase(6000, 7000);

        [Category("W255")]
        public RecipeBase CenterLunimance { get => _CenterLunimance; set { _CenterLunimance = value; OnPropertyChanged(); } }
        private RecipeBase _CenterLunimance = new RecipeBase(0, 0);

        [Category("W255")]
        public RecipeBase CenterCIE1931ChromaticCoordinatesx { get => _CenterCIE1931ChromaticCoordinatesx; set { _CenterCIE1931ChromaticCoordinatesx = value; OnPropertyChanged(); } }
        private RecipeBase _CenterCIE1931ChromaticCoordinatesx = new RecipeBase(0, 0);

        [Category("W255")]
        public RecipeBase CenterCIE1931ChromaticCoordinatesy { get => _CenterCIE1931ChromaticCoordinatesy; set { _CenterCIE1931ChromaticCoordinatesy = value; OnPropertyChanged(); } }
        private RecipeBase _CenterCIE1931ChromaticCoordinatesy = new RecipeBase(0, 0);

        [Category("W255")]
        public RecipeBase CenterCIE1976ChromaticCoordinatesu { get => _CenterCIE1976ChromaticCoordinatesu; set { _CenterCIE1976ChromaticCoordinatesu = value; OnPropertyChanged(); } }
        private RecipeBase _CenterCIE1976ChromaticCoordinatesu = new RecipeBase(0, 0);

        [Category("W255")]
        public RecipeBase CenterCIE1976ChromaticCoordinatesv { get => _CenterCIE1976ChromaticCoordinatesv; set { _CenterCIE1976ChromaticCoordinatesv = value; OnPropertyChanged(); } }
        private RecipeBase _CenterCIE1976ChromaticCoordinatesv = new RecipeBase(0, 0);

        public RecipeBase Center_Correlated_Color_Temperature { get => _Center_Correlated_Color_Temperature; set { _Center_Correlated_Color_Temperature = value; OnPropertyChanged(); } }
        private RecipeBase _Center_Correlated_Color_Temperature = new RecipeBase(0, 0);


        [Category("FOV")]
        [DisplayName("Horizontal Field Of View Angle(°)")]
        public RecipeBase HorizontalFieldOfViewAngle { get => _HorizontalFieldOfViewAngle; set { _HorizontalFieldOfViewAngle = value; OnPropertyChanged(); } }
        private RecipeBase _HorizontalFieldOfViewAngle = new RecipeBase(23.5, 24.5);

        [Category("FOV")]
        [DisplayName("Vertical Field of View Angle(°)")]
        public RecipeBase VerticalFieldOfViewAngle { get => _VerticalFieldOfViewAngle; set { _VerticalFieldOfViewAngle = value; OnPropertyChanged(); } }
        private RecipeBase _VerticalFieldOfViewAngle = new RecipeBase(21.5, 22.5);

        [Category("FOV")]
        [DisplayName("Diagonal  Field of View Angle(°)")]
        public RecipeBase DiagonalFieldOfViewAngle { get => _DiagonalFieldOfViewAngle; set { _DiagonalFieldOfViewAngle = value; OnPropertyChanged(); } }
        private RecipeBase _DiagonalFieldOfViewAngle = new RecipeBase(11.5, 12.5);
    }
}