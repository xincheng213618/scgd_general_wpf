#pragma warning disable
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.LargeFlow;
using ColorVision.UI;
using Newtonsoft.Json.Linq;
using ProjectLUX;
using ProjectLUX;
using ProjectLUX.Process.Distortion;
using ProjectLUX.Recipe;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace ProjectLUX.Process.Distortion
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