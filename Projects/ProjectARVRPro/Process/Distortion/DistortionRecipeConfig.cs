
using ColorVision.Common.MVVM;
using ProjectARVRPro.Recipe;
using System.ComponentModel;

namespace ProjectARVRPro.Process.Distortion
{
    public class DistortionRecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("TV")]
        public RecipeBase HorizontalTVDistortion { get => _HorizontalTVDistortion; set { _HorizontalTVDistortion = value; OnPropertyChanged(); } }
        private RecipeBase _HorizontalTVDistortion = new RecipeBase(0, 2.1);

        [Category("TV")]
        public RecipeBase VerticalTVDistortion { get => _VerticalTVDistortion; set { _VerticalTVDistortion = value; OnPropertyChanged(); } }
        private RecipeBase _VerticalTVDistortion = new RecipeBase(0, 2.1);

        [Category("Optic")]
        [DisplayName("Optic_Distortion")]
        public RecipeBase OpticDistortion { get => _OpticDistortion; set { _OpticDistortion = value; OnPropertyChanged(); } }
        private RecipeBase _OpticDistortion = new RecipeBase(0, 0);

        [Category("Point9")]
        public RecipeBase DistortionTop { get => _DistortionTop; set { _DistortionTop = value; OnPropertyChanged(); } }
        private RecipeBase _DistortionTop = new RecipeBase(0, 0);

        [Category("Point9")]
        public RecipeBase DistortionBottom { get => _DistortionBottom; set { _DistortionBottom = value; OnPropertyChanged(); } }
        private RecipeBase _DistortionBottom = new RecipeBase(0, 0);

        [Category("Point9")]
        public RecipeBase DistortionLeft { get => _DistortionLeft; set { _DistortionLeft = value; OnPropertyChanged(); } }
        private RecipeBase _DistortionLeft = new RecipeBase(0, 0);

        [Category("Point9")]
        public RecipeBase DistortionRight { get => _DistortionRight; set { _DistortionRight = value; OnPropertyChanged(); } }
        private RecipeBase _DistortionRight = new RecipeBase(0, 0);

        [Category("Point9")]
        public RecipeBase KeystoneHoriz { get => _KeystoneHoriz; set { _KeystoneHoriz = value; OnPropertyChanged(); } }
        private RecipeBase _KeystoneHoriz = new RecipeBase(0, 0);

        [Category("Point9")]
        public RecipeBase KeystoneVert { get => _KeystoneVert; set { _KeystoneVert = value; OnPropertyChanged(); } }
        private RecipeBase _KeystoneVert = new RecipeBase(0, 0);
    }
}