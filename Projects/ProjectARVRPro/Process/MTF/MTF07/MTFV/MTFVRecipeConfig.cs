using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using ProjectARVRPro.Process.MTF.MTF07;
using ProjectARVRPro.Recipe;
using System.ComponentModel;

namespace ProjectARVRPro.Process.MTF.MTFV
{
    public sealed class MTFVRecipeConfig : ViewModelBase, IRecipeConfig, IMTF07DynamicRecipeConfig
    {
        [Category("MTF07-V")]
        [DisplayName("统一上下限")]
        [Description("MTF07-V所有动态测量区域共用的修正值和上下限。")]
        public RecipeBase UnifiedRecipe { get => _UnifiedRecipe; set { _UnifiedRecipe = value ?? new(); OnPropertyChanged(); } }
        private RecipeBase _UnifiedRecipe = new(0.5, 0);

        [JsonProperty("MTF_V_Center_0F")]
        private RecipeBase? LegacyCenterRecipe { set { if (value != null) UnifiedRecipe = value; } }
    }
}
