using ColorVision.Common.MVVM;
using ProjectARVRPro.Recipe;
using System.ComponentModel;

namespace ProjectARVRPro.Process.KeyedResults.FieldOfView
{
    public class FieldOfViewRecipeConfig : ViewModelBase, IRecipeConfig
    {
        private const string CategoryName = "FOV";

        [Category(CategoryName)]
        [DisplayName("Horizontal Field Of View Angle(°)")]
        public RecipeBase HorizontalFieldOfViewAngle { get => _HorizontalFieldOfViewAngle; set { _HorizontalFieldOfViewAngle = value; OnPropertyChanged(); } }
        private RecipeBase _HorizontalFieldOfViewAngle = new(23.5, 24.5);

        [Category(CategoryName)]
        [DisplayName("Vertical Field of View Angle(°)")]
        public RecipeBase VerticalFieldOfViewAngle { get => _VerticalFieldOfViewAngle; set { _VerticalFieldOfViewAngle = value; OnPropertyChanged(); } }
        private RecipeBase _VerticalFieldOfViewAngle = new(21.5, 22.5);

        [Category(CategoryName)]
        [DisplayName("Diagonal Field of View Angle(°)")]
        public RecipeBase DiagonalFieldOfViewAngle { get => _DiagonalFieldOfViewAngle; set { _DiagonalFieldOfViewAngle = value; OnPropertyChanged(); } }
        private RecipeBase _DiagonalFieldOfViewAngle = new(11.5, 12.5);
    }
}
