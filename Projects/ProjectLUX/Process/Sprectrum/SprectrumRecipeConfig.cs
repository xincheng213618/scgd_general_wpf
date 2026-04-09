using ColorVision.Common.MVVM;
using ProjectLUX.Recipe;
using System.ComponentModel;

namespace ProjectLUX.Process.Sprectrum
{
    public class SprectrumRecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("Sprectrum")]
        public RecipeBase LuminousFlux { get => _LuminousFlux; set { _LuminousFlux = value; OnPropertyChanged(); } }
        private RecipeBase _LuminousFlux = new RecipeBase(0, 0);
    }
}