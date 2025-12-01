
using ColorVision.Common.MVVM;
using ProjectARVRPro.Recipe;
using System.ComponentModel;

namespace ProjectARVRPro.Process.W25
{
    public class W25RecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("W25")]
        public RecipeBase CenterLunimance { get => _CenterLunimance; set { _CenterLunimance = value; OnPropertyChanged(); } }
        private RecipeBase _CenterLunimance = new RecipeBase(0, 0);

        [Category("W25")]
        public RecipeBase CenterCIE1931ChromaticCoordinatesx { get => _CenterCIE1931ChromaticCoordinatesx; set { _CenterCIE1931ChromaticCoordinatesx = value; OnPropertyChanged(); } }
        private RecipeBase _CenterCIE1931ChromaticCoordinatesx = new RecipeBase(0, 0);

        [Category("W25")]
        public RecipeBase CenterCIE1931ChromaticCoordinatesy { get => _CenterCIE1931ChromaticCoordinatesy; set { _CenterCIE1931ChromaticCoordinatesy = value; OnPropertyChanged(); } }
        private RecipeBase _CenterCIE1931ChromaticCoordinatesy = new RecipeBase(0, 0);

        [Category("W25")]
        public RecipeBase CenterCIE1976ChromaticCoordinatesu { get => _CenterCIE1976ChromaticCoordinatesu; set { _CenterCIE1976ChromaticCoordinatesu = value; OnPropertyChanged(); } }
        private RecipeBase _CenterCIE1976ChromaticCoordinatesu = new RecipeBase(0, 0);

        [Category("W25")]
        public RecipeBase CenterCIE1976ChromaticCoordinatesv { get => _CenterCIE1976ChromaticCoordinatesv; set { _CenterCIE1976ChromaticCoordinatesv = value; OnPropertyChanged(); } }
        private RecipeBase _CenterCIE1976ChromaticCoordinatesv = new RecipeBase(0, 0);
    }
}