
using ColorVision.Common.MVVM;
using ProjectARVRPro.Recipe;
using System.ComponentModel;

namespace ProjectARVRPro.Process.W51
{
    public class W51RecipeConfig : ViewModelBase, IRecipeConfig
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