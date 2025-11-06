using ColorVision.Common.MVVM;
using ProjectLUX.Recipe;
using System.ComponentModel;

namespace ProjectLUX.Process.DistortionAR
{
    public class DistortionRecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("Distortion")]
        public RecipeBase HorizontalTVDistortion { get => _HorizontalTVDistortion; set { _HorizontalTVDistortion = value; OnPropertyChanged(); } }
        private RecipeBase _HorizontalTVDistortion = new RecipeBase(0, 2.1);

        [Category("Distortion")]
        public RecipeBase VerticalTVDistortion { get => _VerticalTVDistortion; set { _VerticalTVDistortion = value; OnPropertyChanged(); } }
        private RecipeBase _VerticalTVDistortion = new RecipeBase(0, 2.1);
    }
}