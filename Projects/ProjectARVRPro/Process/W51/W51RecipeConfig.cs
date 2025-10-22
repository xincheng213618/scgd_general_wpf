#pragma warning disable
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.LargeFlow;
using ColorVision.UI;
using Newtonsoft.Json.Linq;
using ProjectARVRPro;
using ProjectARVRPro;
using ProjectARVRPro.Process.W51;
using ProjectARVRPro.Recipe;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace ProjectARVRPro.Process.W51
{
    public class W51RecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("W51")]
        [DisplayName("Horizontal Field Of View Angle(°)")]
        public RecipeBase HorizontalFieldOfViewAngle { get => _HorizontalFieldOfViewAngle; set { _HorizontalFieldOfViewAngle = value; OnPropertyChanged(); } }
        private RecipeBase _HorizontalFieldOfViewAngle = new RecipeBase(23.5, 24.5);

        [Category("W51")]
        [DisplayName("Vertical Field of View Angle(°)")]
        public RecipeBase VerticalFieldOfViewAngle { get => _VerticalFieldOfViewAngle; set { _VerticalFieldOfViewAngle = value; OnPropertyChanged(); } }
        private RecipeBase _VerticalFieldOfViewAngle = new RecipeBase(21.5, 22.5);

        [Category("W51")]
        [DisplayName("Diagonal  Field of View Angle(°)")]
        public RecipeBase DiagonalFieldOfViewAngle { get => _DiagonalFieldOfViewAngle; set { _DiagonalFieldOfViewAngle = value; OnPropertyChanged(); } }
        private RecipeBase _DiagonalFieldOfViewAngle = new RecipeBase(11.5, 12.5);
    }
}