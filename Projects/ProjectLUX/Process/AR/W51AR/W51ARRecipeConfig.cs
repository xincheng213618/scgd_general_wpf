
using ColorVision.Common.MVVM;
using ProjectLUX.Recipe;
using System.ComponentModel;

namespace ProjectLUX.Process.AR.W51AR
{
    public class W51ARRecipeConfig : ViewModelBase, IRecipeConfig
    {
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