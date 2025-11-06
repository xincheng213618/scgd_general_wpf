
using ColorVision.Common.MVVM;
using ProjectLUX.Recipe;
using System.ComponentModel;

namespace ProjectLUX.Process.OpticCenter
{
    public class OpticCenterRecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("OpticCenter")]
        public RecipeBase OptCenterXTilt { get => _OptCenterXTilt; set { _OptCenterXTilt = value; OnPropertyChanged(); } }
        private RecipeBase _OptCenterXTilt = new RecipeBase(-0.16, 0.16);

        [Category("OpticCenter")]
        public RecipeBase OptCenterYTilt { get => _OptCenterYTilt; set { _OptCenterYTilt = value; OnPropertyChanged(); } }
        private RecipeBase _OptCenterYTilt = new RecipeBase(-0.16, 0.16);

        [Category("OpticCenter")]
        public RecipeBase OptCenterRotation { get => _OptCenterRotation; set { _OptCenterRotation = value; OnPropertyChanged(); } }
        private RecipeBase _OptCenterRotation = new RecipeBase(-0.16, 0.16);

        [Category("OpticCenter")]
        public RecipeBase ImageCenterXTilt { get => _ImageCenterXTilt; set { _ImageCenterXTilt = value; OnPropertyChanged(); } }
        private RecipeBase _ImageCenterXTilt = new RecipeBase(-0.16, 0.16);

        [Category("OpticCenter")]
        public RecipeBase ImageCenterYTilt { get => _ImageCenterYTilt; set { _ImageCenterYTilt = value; OnPropertyChanged(); } }
        private RecipeBase _ImageCenterYTilt = new RecipeBase(-0.16, 0.16);

        [Category("OpticCenter")]
        public RecipeBase ImageCenterRotation { get => _ImageCenterRotation; set { _ImageCenterRotation = value; OnPropertyChanged(); } }
        private RecipeBase _ImageCenterRotation = new RecipeBase(-0.16, 0.16);
    }
}