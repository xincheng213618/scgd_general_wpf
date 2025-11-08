
using ColorVision.Common.MVVM;
using ProjectARVRPro.Recipe;
using System.ComponentModel;

namespace ProjectARVRPro.Process.W25
{
    public class W25RecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("W25")]
        public RecipeBase W25CenterLunimance { get => _W25CenterLunimance; set { _W25CenterLunimance = value; OnPropertyChanged(); } }
        private RecipeBase _W25CenterLunimance = new RecipeBase(0, 0);

        [Category("W25")]
        public RecipeBase W25CenterCIE1931ChromaticCoordinatesx { get => _W25CenterCIE1931ChromaticCoordinatesx; set { _W25CenterCIE1931ChromaticCoordinatesx = value; OnPropertyChanged(); } }
        private RecipeBase _W25CenterCIE1931ChromaticCoordinatesx = new RecipeBase(0, 0);

        [Category("W25")]
        public RecipeBase W25CenterCIE1931ChromaticCoordinatesy { get => _W25CenterCIE1931ChromaticCoordinatesy; set { _W25CenterCIE1931ChromaticCoordinatesy = value; OnPropertyChanged(); } }
        private RecipeBase _W25CenterCIE1931ChromaticCoordinatesy = new RecipeBase(0, 0);

        [Category("W25")]
        public RecipeBase W25CenterCIE1976ChromaticCoordinatesu { get => _W25CenterCIE1976ChromaticCoordinatesu; set { _W25CenterCIE1976ChromaticCoordinatesu = value; OnPropertyChanged(); } }
        private RecipeBase _W25CenterCIE1976ChromaticCoordinatesu = new RecipeBase(0, 0);

        [Category("W25")]
        public RecipeBase W25CenterCIE1976ChromaticCoordinatesv { get => _W25CenterCIE1976ChromaticCoordinatesv; set { _W25CenterCIE1976ChromaticCoordinatesv = value; OnPropertyChanged(); } }
        private RecipeBase _W25CenterCIE1976ChromaticCoordinatesv = new RecipeBase(0, 0);
    }
}