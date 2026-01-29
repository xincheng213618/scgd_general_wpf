
using ColorVision.Common.MVVM;
using ProjectARVRPro.Recipe;
using System.ComponentModel;

namespace ProjectARVRPro.Process.Black
{

    public class BlackRecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("Black")]
        public RecipeBase FOFOContrast { get => _FOFOContrast; set { _FOFOContrast = value; OnPropertyChanged(); } }
        private RecipeBase _FOFOContrast = new RecipeBase(100000, 0);
    }

}