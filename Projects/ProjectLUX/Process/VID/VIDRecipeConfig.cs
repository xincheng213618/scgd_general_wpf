using ColorVision.Common.MVVM;
using ProjectLUX.Recipe;
using System.ComponentModel;

namespace ProjectLUX.Process.VID
{
    public class VIDRecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("VID")]
        public RecipeBase VID { get => _VID; set { _VID = value; OnPropertyChanged(); } }
        private RecipeBase _VID = new RecipeBase(0, 0);
    }
}